using UnityEngine;
using System.Collections;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SimpleNPCWander : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("Radio máximo en metros donde el NPC elegirá puntos aleatorios para vagar.")]
    public float wanderRadius = 8f;
    [Tooltip("Tiempo mínimo que esperará en idle antes de moverse otra vez.")]
    public float minIdleTime = 1.2f;
    [Tooltip("Tiempo máximo que esperará en idle antes de moverse otra vez.")]
    public float maxIdleTime = 3.0f;
    [Tooltip("Permite que el NPC elija un nuevo destino mientras está moviéndose (true) o que espere a llegar (false).")]
    public bool pickWhileMoving = false;

    [Header("Agent Settings")]
    [Tooltip("Velocidad del NavMeshAgent (si 0 usa la velocidad ya configurada en el agente).")]
    public float agentSpeed = 0f;

    NavMeshAgent _agent;
    IAmbientAnim _ambientAnim; // opcional: puente a sistema de animaciones existente
    Animator _animator;

    // Helper para evitar llamar a propiedades que lanzan excepción si el agente no está en el NavMesh
    void SafeSetAgentStopped(bool stopped)
    {
        if (_agent != null && _agent.isOnNavMesh)
        {
            _agent.isStopped = stopped;
        }
    }

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _ambientAnim = GetComponentInChildren<IAmbientAnim>();
        _animator = GetComponentInChildren<Animator>(true);

        if (_agent == null)
            Debug.LogError($"[SimpleNPCWander] No NavMeshAgent en {name}.");

        if (agentSpeed > 0f && _agent != null)
            _agent.speed = agentSpeed;
    }

    void OnEnable()
    {
        StopAllCoroutines();

        // Si el agente existe pero no está sobre el NavMesh, intentar colocarlo en el NavMesh
        if (_agent != null && !_agent.isOnNavMesh)
        {
            NavMeshHit hit;
            float sampleRadius = Mathf.Max(1f, wanderRadius);
            if (NavMesh.SamplePosition(transform.position, out hit, sampleRadius, NavMesh.AllAreas))
            {
                // Warp mueve el agente a la posición sin pedir que esté en NavMesh previamente
                _agent.Warp(hit.position);
            }
            else
            {
                // No hay NavMesh cerca: mantener seguro y no iniciar el bucle para evitar errores
                return;
            }
        }

        StartCoroutine(WanderLoop());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        SafeSetAgentStopped(true);
    }

    IEnumerator WanderLoop()
    {
        // Pequeña pausa aleatoria inicial para evitar que todos los NPCs se muevan sincronizados
        yield return new WaitForSeconds(Random.Range(0f, 0.6f));

        while (isActiveAndEnabled)
        {
            // Esperar un tiempo en idle antes de intentar moverse
            yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));

            if (_agent == null || !_agent.isOnNavMesh)
            {
                // Si no hay agente válido en NavMesh, esperar un poco antes de reintentar para no saturar la CPU
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            Vector3 dest;
            if (TryGetRandomNavmeshPoint(transform.position, wanderRadius, out dest))
            {
                SafeSetAgentStopped(false);
                if (_agent == null || !_agent.isOnNavMesh)
                {
                    // Si el agente no está en NavMesh, no podemos establecer destino de forma segura
                    yield return null;
                    continue;
                }
                _agent.SetDestination(dest);

                // Si existe el bridge de animaciones, indicamos que camine; si no, actualizamos InputMagnitude como fallback.
                if (_ambientAnim != null)
                    _ambientAnim.PlayWalk(1f);

                // Esperamos mientras se mueva hacia el destino
                while (isActiveAndEnabled && _agent != null && _agent.isOnNavMesh && !_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance + 0.1f)
                {
                    float speed01 = (_agent.speed <= 0.01f) ? 0f : Mathf.Clamp01(_agent.velocity.magnitude / _agent.speed);
                    if (_ambientAnim != null)
                        _ambientAnim.PlayWalk(speed01);
                    else if (_animator != null)
                        _animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, speed01);

                    if (!pickWhileMoving && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                        break;

                    yield return null;
                }

                // Llegó al destino o se canceló
                SafeSetAgentStopped(true);
                if (_ambientAnim != null)
                    _ambientAnim.PlayIdle();
                else if (_animator != null)
                    _animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, 0f);
            }
            else
            {
                // No encontró punto válido en NavMesh; esperar y reintentar
                yield return new WaitForSeconds(0.5f);
            }

            yield return null;
        }
    }

    bool TryGetRandomNavmeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++) // varios intentos
        {
            Vector3 randomPoint = origin + Random.insideUnitSphere * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = origin;
        return false;
    }

    // API pública para ajustar el radio desde otros scripts o el inspector en tiempo de ejecución
    public void SetWanderRadius(float r) { wanderRadius = Mathf.Max(0f, r); }
}
