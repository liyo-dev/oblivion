using System.Collections;
using Alex.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
[System.Obsolete("Usa NPCBehaviourManager con el módulo de ambientación.")]
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
    IAmbientAnim _ambientAnim; // Bridge opcional hacia tu sistema de animaciones
    Animator _animator;

    static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _ambientAnim = GetComponentInChildren<IAmbientAnim>();
        _animator = GetComponentInChildren<Animator>(true);

        if (_agent == null)
            Debug.LogError($"[{nameof(SimpleNPCWander)}] No NavMeshAgent en {name}.");

        if (agentSpeed > 0f && _agent != null)
            _agent.speed = agentSpeed;

        if (_animator != null)
            _animator.applyRootMotion = false;
    }

    void OnEnable()
    {
        StopAllCoroutines();

        if (!NavMeshAgentUtility.EnsureAgentOnNavMesh(_agent, transform.position, wanderRadius))
            return;

        StartCoroutine(WanderLoop());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        NavMeshAgentUtility.SafeSetStopped(_agent, true);
    }

    IEnumerator WanderLoop()
    {
        // pequeña desincronización para que múltiples NPCs no arranquen a la vez
        yield return new WaitForSeconds(Random.Range(0f, 0.6f));

        while (isActiveAndEnabled)
        {
            yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));

            if (_agent == null || !NavMeshAgentUtility.EnsureAgentOnNavMesh(_agent, transform.position, wanderRadius))
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            if (!NavMeshAgentUtility.TryGetRandomPoint(transform.position, wanderRadius, out var destination))
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            NavMeshAgentUtility.SetDestination(_agent, destination);
            UpdateMovementAnimation(1f);

            while (ShouldContinueWalking())
            {
                UpdateMovementAnimation(NavMeshAgentUtility.ComputeSpeedFactor(_agent));

                if (!pickWhileMoving && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                    break;

                yield return null;
            }

            NavMeshAgentUtility.SafeSetStopped(_agent, true);
            UpdateMovementAnimation(0f);

            yield return null;
        }
    }

    bool ShouldContinueWalking()
    {
        return isActiveAndEnabled &&
               _agent != null &&
               _agent.isOnNavMesh &&
               !_agent.pathPending &&
               _agent.remainingDistance > _agent.stoppingDistance + 0.1f;
    }

    void UpdateMovementAnimation(float speed01)
    {
        if (_ambientAnim != null)
        {
            if (speed01 <= 0.01f) _ambientAnim.PlayIdle();
            else _ambientAnim.PlayWalk(speed01);
            return;
        }

        if (_animator != null)
            _animator.SetFloat(InputMagnitudeHash, Mathf.Clamp01(speed01), 0.1f, Time.deltaTime);
    }

    // API pública
    public void SetWanderRadius(float radius)
    {
        wanderRadius = Mathf.Max(0f, radius);
    }
}
