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

    [Header("Interacci�n con el jugador")]
    [Tooltip("Si es true, al iniciar un di�logo mirará al jugador.")]
    public bool lookAtPlayerOnInteract = true;
    [Tooltip("Velocidad de rotación al mirar al jugador.")]
    public float interactRotateSpeed = 10f;
    [Tooltip("Pose a reproducir en IAmbientAnim al hablar con el jugador.")]
    public SpotPose interactPose = SpotPose.Talk;
    [Tooltip("Nombre del estado de animación a reproducir si no hay IAmbientAnim.")]
    public string interactState = "InteractWithPeople_NoWeapon";

    NavMeshAgent _agent;
    IAmbientAnim _ambientAnim; // Bridge opcional hacia tu sistema de animaciones
    Animator _animator;
    Interactable _interactable;

    static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");

    Transform _player;
    bool _isInteracting;
    Coroutine _faceRoutine;

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

        _interactable = GetComponent<Interactable>();
        if (_interactable != null)
        {
            _interactable.OnStarted.AddListener(BeginInteraction);
            _interactable.OnFinished.AddListener(EndInteraction);
        }
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
        StopFacing();
        _isInteracting = false;
    }

    void OnDestroy()
    {
        if (_interactable != null)
        {
            _interactable.OnStarted.RemoveListener(BeginInteraction);
            _interactable.OnFinished.RemoveListener(EndInteraction);
        }
    }

    IEnumerator WanderLoop()
    {
        // pequeña desincronización para que múltiples NPCs no arranquen a la vez
        yield return new WaitForSeconds(Random.Range(0f, 0.6f));

        while (isActiveAndEnabled)
        {
            yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));

            while (_isInteracting)
                yield return null;

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
                if (_isInteracting)
                    break;

                UpdateMovementAnimation(NavMeshAgentUtility.ComputeSpeedFactor(_agent));

                if (!pickWhileMoving && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                    break;

                yield return null;
            }

            NavMeshAgentUtility.SafeSetStopped(_agent, true);
            UpdateMovementAnimation(0f);

            while (_isInteracting)
                yield return null;

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

    void BeginInteraction()
    {
        if (_isInteracting)
            return;

        _isInteracting = true;
        NavMeshAgentUtility.SafeSetStopped(_agent, true);

        if (lookAtPlayerOnInteract)
        {
            ResolvePlayerReference();
            if (_player != null)
            {
                StopFacing();
                _faceRoutine = StartCoroutine(FacePlayerRoutine());
            }
        }

        if (_ambientAnim != null)
        {
            _ambientAnim.PlayPose(interactPose);
        }
        else if (_animator != null && !string.IsNullOrEmpty(interactState))
        {
            _animator.CrossFade(interactState, 0.1f, 0, 0f);
        }
    }

    void EndInteraction()
    {
        if (!_isInteracting)
            return;

        _isInteracting = false;
        StopFacing();
        _ambientAnim?.ClearPose();
        UpdateMovementAnimation(0f);
    }

    void ResolvePlayerReference()
    {
        if (_player == null || !_player)
            _player = PlayerLocator.ResolvePlayer();
    }

    IEnumerator FacePlayerRoutine()
    {
        while (_isInteracting)
        {
            if (_player == null)
            {
                ResolvePlayerReference();
                if (_player == null)
                {
                    yield return null;
                    continue;
                }
            }

            Vector3 dir = _player.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired, Time.deltaTime * interactRotateSpeed);
            }

            yield return null;
        }
    }

    void StopFacing()
    {
        if (_faceRoutine != null)
        {
            StopCoroutine(_faceRoutine);
            _faceRoutine = null;
        }
    }
}
