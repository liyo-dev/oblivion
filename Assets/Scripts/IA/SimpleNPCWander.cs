using UnityEngine;
using System.Collections;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class SimpleNPCWander : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("Radio máximo en metros donde el NPC elegirá puntos aleatorios para vagar.")]
    public float wanderRadius = 8f;
    [Tooltip("Tiempo mínimo que esperará en idle antes de moverse otra vez.")]
    public float minIdleTime = 1.2f;
    [Tooltip("Tiempo máximo que esperará en idle antes de moverse otra vez.")]
    public float maxIdleTime = 3.0f;
    [Tooltip("Permite elegir un nuevo destino mientras está moviéndose (true) o esperar a llegar (false).")]
    public bool pickWhileMoving = false;

    [Header("Agent Settings")]
    [Tooltip("Velocidad del NavMeshAgent (si 0 usa la ya configurada).")]
    public float agentSpeed = 0f;

    NavMeshAgent _agent;
    IAmbientAnim _ambientAnim;     // opcional: puente a tu sistema de animaciones
    Animator _animator;

    static readonly int InputMagnitude_Hash = Animator.StringToHash("InputMagnitude");

    void SafeSetAgentStopped(bool stopped)
    {
        if (_agent != null && _agent.isOnNavMesh)
            _agent.isStopped = stopped;
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

        if (_animator) _animator.applyRootMotion = false; // manda el Agent
    }

    void OnEnable()
    {
        StopAllCoroutines();

        if (_agent != null && !_agent.isOnNavMesh)
        {
            NavMeshHit hit;
            float sampleRadius = Mathf.Max(1f, wanderRadius);
            if (NavMesh.SamplePosition(transform.position, out hit, sampleRadius, NavMesh.AllAreas))
                _agent.Warp(hit.position);
            else
                return; // no hay NavMesh cerca; no arrancar el loop
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
        // desincroniza un poco a los NPCs
        yield return new WaitForSeconds(Random.Range(0f, 0.6f));

        while (isActiveAndEnabled)
        {
            // idle previo al siguiente destino
            yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));

            if (_agent == null || !_agent.isOnNavMesh)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (TryGetRandomNavmeshPoint(transform.position, wanderRadius, out var dest))
            {
                SafeSetAgentStopped(false);

                if (_agent == null || !_agent.isOnNavMesh)
                {
                    yield return null;
                    continue;
                }

                _agent.SetDestination(dest);

                // animación de caminar
                if (_ambientAnim != null) _ambientAnim.PlayWalk(1f);

                // seguimiento de movimiento
                while (isActiveAndEnabled &&
                       _agent != null &&
                       _agent.isOnNavMesh &&
                       !_agent.pathPending &&
                       _agent.remainingDistance > _agent.stoppingDistance + 0.1f)
                {
                    float speed01 = (_agent.speed <= 0.01f)
                        ? 0f
                        : Mathf.Clamp01(_agent.velocity.magnitude / _agent.speed);

                    if (_ambientAnim != null)
                        _ambientAnim.PlayWalk(speed01);
                    else if (_animator != null)
                        _animator.SetFloat(InputMagnitude_Hash, speed01, 0.1f, Time.deltaTime); // suavizado

                    if (!pickWhileMoving && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                        break;

                    yield return null;
                }

                // llegada o cancelación
                SafeSetAgentStopped(true);

                if (_ambientAnim != null) _ambientAnim.PlayIdle();
                else if (_animator != null) _animator.SetFloat(InputMagnitude_Hash, 0f, 0.1f, Time.deltaTime);
            }
            else
            {
                // no encontró punto válido
                yield return new WaitForSeconds(0.5f);
            }

            yield return null;
        }
    }

    bool TryGetRandomNavmeshPoint(Vector3 origin, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector3 randomPoint = origin + Random.insideUnitSphere * radius;
            if (NavMesh.SamplePosition(randomPoint, out var hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = origin;
        return false;
    }

    // API pública
    public void SetWanderRadius(float r) { wanderRadius = Mathf.Max(0f, r); }
}
