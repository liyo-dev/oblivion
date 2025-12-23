using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Sistema profesional de animaciones para NPCs.
/// El ÚNICO responsable de controlar las animaciones del NPC.
/// Todos los demás sistemas (FSM, Combat, Quest, etc.) piden animaciones aquí.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class NPCSimpleAnimator : MonoBehaviour
{
    #region Serialized Fields
    
    [Header("Core Components")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent navAgent;
    
    [Header("Animation States")]
    [Tooltip("Estado de locomotion base (ej: Free Locomotion blend tree)")]
    [SerializeField] private string locomotionState = "Free Locomotion";
    
    [Tooltip("Estado de idle normal")]
    [SerializeField] private string idleNormalState = "Idle_Normal_NoWeapon";
    
    [Tooltip("Estado de idle en batalla")]
    [SerializeField] private string idleBattleState = "Idle_Battle_NoWeapon";
    
    [Header("Animation Layers")]
    [Tooltip("Índice de la capa para animaciones de torso superior (ataques)")]
    [SerializeField] private int upperBodyLayer = 1;
    
    [Tooltip("Estado idle de la capa superior")]
    [SerializeField] private string upperBodyIdleState = "UpperIdle";
    
    [Header("Locomotion Settings")]
    [Tooltip("Velocidad mínima para considerar que está en movimiento")]
    [SerializeField, Range(0.01f, 0.5f)] private float movementThreshold = 0.1f;
    
    [Tooltip("Multiplicador de velocidad de animación durante locomoción")]
    [SerializeField, Range(0.5f, 2f)] private float locomotionSpeedMultiplier = 1.0f;
    
    [Tooltip("Tiempo de blend para transiciones de locomoción")]
    [SerializeField, Range(0f, 0.5f)] private float locomotionBlendTime = 0.1f;
    
    [Tooltip("Dampening del parámetro InputMagnitude")]
    [SerializeField, Range(0f, 0.5f)] private float inputMagnitudeDampTime = 0.1f;
    
    [Header("Rotation Settings")]
    [Tooltip("Velocidad de rotación del NPC (grados/segundo)")]
    [SerializeField, Range(90f, 720f)] private float rotationSpeed = 360f;
    
    [Tooltip("Suavizado de rotación")]
    [SerializeField, Range(0f, 1f)] private float rotationSmoothness = 0.15f;
    
    [Tooltip("Ángulo mínimo para considerar que debe rotar")]
    [SerializeField, Range(1f, 45f)] private float minRotationAngle = 5f;
    
    [Header("Interaction")]
    [SerializeField] private string interactState = "InteractWithPeople_NoWeapon";
    [SerializeField] private string greetingState = "Greeting01_NoWeapon";
    
    [Header("Combat Animations")]
    [SerializeField] private string challengingState = "Challenging_NoWeapon";
    [SerializeField] private string senseSomethingState = "SenseSomethingStart_NoWeapon";
    [SerializeField] private string defendState = "Idle_Battle_NoWeapon"; // Fallback a Battle Idle si no existe Defend
    [SerializeField] private string getHitState = "GetHit02_NoWeapon";
    [SerializeField] private string dieState = "Die02_NoWeapon";
    [SerializeField] private string victoryState = "Dance_NoWeapon"; // Usar Dance como victoria si no hay Victory
    
    [Header("NavMesh Agent Sync")]
    [Tooltip("Sincronizar automáticamente con NavMeshAgent")]
    [SerializeField] private bool syncWithNavAgent = true;
    
    [Tooltip("Usar Root Motion durante animaciones especiales")]
    [SerializeField] private bool useRootMotionForSpecialAnims = false;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private bool drawGizmos = true;
    
    #endregion
    
    #region Private Fields
    
    // Animation state
    private AnimationState _currentState = AnimationState.Idle;
    private bool _isInBattle;
    private bool _isInteracting;
    private float _currentMovementSpeed;
    private Vector3 _lastPosition;
    private float _actualSpeed;
    
    // Animator parameters
    private static readonly int InputMagnitudeHash = Animator.StringToHash("InputMagnitude");
    
    // References
    private Transform _player;
    private Transform _playerCam;
    private Interactable _interactable;
    
    // Coroutines
    private Coroutine _oneShotCoroutine;
    private Coroutine _rotationCoroutine;
    
    // Caches
    private AnimatorStateCache _stateCache;
    private Dictionary<string, float> _clipLengthCache = new Dictionary<string, float>();
    
    // Smooth rotation
    private Quaternion _targetRotation;
    private float _rotationVelocity;
    
    #endregion
    
    #region Enums
    
    public enum AnimationState
    {
        Idle,
        Walking,
        Running,
        Battle,
        Interacting,
        OneShot,
        Dead
    }
    
    #endregion
    
    #region Unity Lifecycle
    
    void Reset()
    {
        animator = GetComponent<Animator>();
        navAgent = GetComponent<NavMeshAgent>();
    }
    
    void Awake()
    {
        // Get components
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();
        
        _interactable = GetComponent<Interactable>();
        
        // Initialize
        if (animator != null)
        {
            animator.applyRootMotion = useRootMotionForSpecialAnims;
            _stateCache = new AnimatorStateCache(animator);
            CacheAnimationClips();
        }
        
        _lastPosition = transform.position;
        _targetRotation = transform.rotation;
        
        // Bind to interactable if exists
        if (_interactable != null)
        {
            _interactable.OnStarted.AddListener(OnInteractionStarted);
            _interactable.OnFinished.AddListener(OnInteractionFinished);
        }
        
        ResolvePlayerReferences();
    }
    
    void Start()
    {
        // Start in idle state
        TransitionToIdle();
    }
    
    void Update()
    {
        if (animator == null)
            return;
        
        // Update actual speed based on position
        UpdateActualSpeed();
        
        // Sync with NavMeshAgent if enabled
        if (syncWithNavAgent && navAgent != null && navAgent.enabled)
        {
            SyncWithNavMeshAgent();
        }
    }
    
    void LateUpdate()
    {
        // Apply smooth rotation in LateUpdate for best results
        ApplySmoothRotation();
    }
    
    #endregion
    
    #region Public API - Movement & Speed
    
    /// <summary>
    /// Establece la velocidad de movimiento del NPC (0-1 normalizado)
    /// </summary>
    public void SetMovementSpeed(float normalizedSpeed, float dampTime = -1f)
    {
        if (animator == null)
            return;
        
        _currentMovementSpeed = Mathf.Clamp01(normalizedSpeed);
        
        // Use configured damp time if not specified
        float damp = dampTime < 0 ? inputMagnitudeDampTime : dampTime;
        
        // Set animator parameter
        animator.SetFloat(InputMagnitudeHash, _currentMovementSpeed, damp, Time.deltaTime);
        
        // Adjust animation speed to match movement speed (reduces foot sliding)
        if (_currentMovementSpeed > movementThreshold)
        {
            animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, _currentMovementSpeed);
            
            // Ensure we're in locomotion state if moving
            // Permitir transición a locomoción desde Idle o Battle (para que funcione durante combate)
            if ((_currentState == AnimationState.Idle || _currentState == AnimationState.Battle) && !_isInteracting)
            {
                TransitionToLocomotion();
            }
        }
        else
        {
            animator.speed = 1f;
        }
        
        if (debugMode)
            Debug.Log($"[NPCAnimator] SetMovementSpeed: {normalizedSpeed:F2}, actual speed: {_actualSpeed:F2}");
    }
    
    /// <summary>
    /// Resetea el movimiento a 0
    /// </summary>
    public void ResetMovement()
    {
        SetMovementSpeed(0f, 0f);
    }
    
    /// <summary>
    /// Para todo movimiento inmediatamente
    /// </summary>
    public void StopMovement()
    {
        ResetMovement();
        if (animator != null)
            animator.speed = 1f;
    }
    
    #endregion
    
    #region Public API - Battle Mode
    
    /// <summary>
    /// Activa/desactiva el modo batalla
    /// </summary>
    public void SetBattleMode(bool enable)
    {
        _isInBattle = enable;
        
        if (animator == null)
            return;
        
        // Set upper body layer weight
        if (upperBodyLayer > 0 && upperBodyLayer < animator.layerCount)
        {
            animator.SetLayerWeight(upperBodyLayer, enable ? 1f : 0f);
        }
        
        // Transition to appropriate state
        if (enable)
        {
            _currentState = AnimationState.Battle;
            if (_currentMovementSpeed < movementThreshold)
            {
                CrossFadeToState(idleBattleState, 0.2f);
            }
        }
        else
        {
            _currentState = AnimationState.Idle;
            TransitionToIdle();
        }
        
        if (debugMode)
            Debug.Log($"[NPCAnimator] Battle mode: {enable}");
    }
    
    /// <summary>
    /// Reproduce el idle de batalla
    /// </summary>
    public void PlayBattleIdle()
    {
        if (_isInBattle && !string.IsNullOrEmpty(idleBattleState))
        {
            CrossFadeToState(idleBattleState, 0.2f);
        }
    }
    
    /// <summary>
    /// Restaura el idle normal (permite transiciones de locomoción)
    /// </summary>
    public void PlayIdleNormal()
    {
        if (!string.IsNullOrEmpty(idleNormalState))
        {
            // Desactivar modo batalla para permitir transiciones de locomoción
            _isInBattle = false;
            _currentState = AnimationState.Idle;
            CrossFadeToState(idleNormalState, 0.2f);
            Debug.Log($"[NPCSimpleAnimator] Restaurando Idle Normal: {idleNormalState}");
        }
    }
    
    /// <summary>
    /// Entra en modo batalla (método de conveniencia)
    /// </summary>
    public void EnterBattleMode()
    {
        SetBattleMode(true);
    }
    
    /// <summary>
    /// Sale del modo batalla (método de conveniencia)
    /// </summary>
    public void ExitBattleMode()
    {
        SetBattleMode(false);
    }
    
    #endregion
    
    #region Public API - One Shot Animations
    
    /// <summary>
    /// Reproduce una animación one-shot (ataque, habilidad, etc.)
    /// </summary>
    public void PlayOneShot(string stateName, int layer = 0, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(stateName))
            return;
        
        // Stop any current one-shot
        if (_oneShotCoroutine != null)
        {
            StopCoroutine(_oneShotCoroutine);
        }
        
        _oneShotCoroutine = StartCoroutine(PlayOneShotCoroutine(stateName, layer, onComplete));
    }
    
    private IEnumerator PlayOneShotCoroutine(string stateName, int layer, Action onComplete)
    {
        _currentState = AnimationState.OneShot;
        
        // Ensure animation speed is normal
        if (animator != null)
            animator.speed = 1f;
        
        // Set layer weight if needed
        if (layer > 0 && layer < animator.layerCount)
        {
            animator.SetLayerWeight(layer, 1f);
        }
        
        // Play animation
        CrossFadeToState(stateName, 0.08f, layer);
        
        yield return null; // Wait one frame for transition to start
        
        // Wait for animation to complete
        float clipLength = GetClipLength(stateName);
        float waitTime = Mathf.Max(0.1f, clipLength);
        
        if (debugMode)
            Debug.Log($"[NPCAnimator] Playing one-shot: {stateName}, length: {waitTime:F2}s");
        
        // Wait using normalized time for accuracy
        float elapsed = 0f;
        while (elapsed < waitTime + 0.2f)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 0.95f)
            {
                break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        // Callback
        onComplete?.Invoke();
        
        // Return to idle if not interacting (let the callback handle battle state)
        if (!_isInteracting && !_isInBattle)
        {
            _currentState = AnimationState.Idle;
            TransitionToIdle();
        }
        
        _oneShotCoroutine = null;
    }
    
    #endregion
    
    #region Public API - Interaction
    
    /// <summary>
    /// Inicia una interacción
    /// </summary>
    public void BeginInteraction()
    {
        if (_isInteracting)
            return;
        
        _isInteracting = true;
        _currentState = AnimationState.Interacting;
        
        // Face player if available
        if (_player != null)
        {
            FaceTarget(_player.position);
        }
        
        // Play interaction animation
        CrossFadeToState(interactState, 0.15f);
        
        if (debugMode)
            Debug.Log("[NPCAnimator] Begin interaction");
    }
    
    /// <summary>
    /// Finaliza una interacción
    /// </summary>
    public void EndInteraction()
    {
        if (!_isInteracting)
            return;
        
        _isInteracting = false;
        
        // Return to appropriate state
        if (_isInBattle)
        {
            PlayBattleIdle();
            _currentState = AnimationState.Battle;
        }
        else
        {
            TransitionToIdle();
        }
        
        if (debugMode)
            Debug.Log("[NPCAnimator] End interaction");
    }
    
    /// <summary>
    /// Reproduce un saludo
    /// </summary>
    public void PlayGreeting()
    {
        if (!_isInteracting && !string.IsNullOrEmpty(greetingState))
        {
            PlayOneShot(greetingState);
        }
    }
    
    /// <summary>
    /// Establece si está hablando (para animaciones de boca)
    /// NOTA: Requiere parámetro 'IsTalking' en el Animator Controller
    /// </summary>
    public void SetTalking(bool isTalking)
    {
        if (animator != null && animator.parameterCount > 0)
        {
            // Solo intentar si el parámetro existe
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "IsTalking")
                {
                    animator.SetBool("IsTalking", isTalking);
                    return;
                }
            }
            
            if (debugMode)
                Debug.LogWarning($"[NPCAnimator] Parámetro 'IsTalking' no encontrado en Animator Controller");
        }
    }
    
    #endregion
    
    #region Public API - Combat Animations
    
    /// <summary>
    /// Reproduce animación de desafío
    /// </summary>
    public void PlayChallenging()
    {
        if (!string.IsNullOrEmpty(challengingState))
        {
            Debug.Log($"[NPCSimpleAnimator] Reproduciendo animación de desafío: {challengingState}");
            
            // Usar PlayOneShot que maneja mejor las transiciones
            // Esto permite que después vuelva automáticamente a la locomotion
            PlayOneShot(challengingState, 0, () =>
            {
                if (debugMode)
                    Debug.Log("[NPCSimpleAnimator] Animación de desafío completada, volviendo a locomotion");
            });
        }
        else
        {
            Debug.LogWarning("[NPCSimpleAnimator] No hay animación de desafío configurada");
        }
    }
    
    /// <summary>
    /// Reproduce animación de Challenge y luego va a Idle Normal (para que el exit time permita Locomotion)
    /// </summary>
    public void PlayChallengingForBattle()
    {
        if (!string.IsNullOrEmpty(challengingState))
        {
            Debug.Log($"[NPCSimpleAnimator] Reproduciendo Challenge para batalla: {challengingState}");
            
            // Usar PlayOneShot estándar con callback
            PlayOneShot(challengingState, 0, () =>
            {
                // Al terminar Challenge, ir a Idle_Normal para permitir transición natural a Locomotion
                Debug.Log($"[NPCSimpleAnimator] Challenge completado → Idle de batalla: {idleBattleState}");
                
                // Activar modo batalla
                _isInBattle = true;
                _currentState = AnimationState.Battle;
                
                // Transicionar a Idle_Battle
                if (!string.IsNullOrEmpty(idleBattleState))
                {
                    CrossFadeToState(idleBattleState, 0.2f);
                }
            });
        }
        else
        {
            Debug.LogWarning("[NPCSimpleAnimator] No hay animación de desafío configurada");
            // Fallback: ir directo a Idle de batalla
            _isInBattle = true;
            _currentState = AnimationState.Battle;
            if (!string.IsNullOrEmpty(idleBattleState))
            {
                CrossFadeToState(idleBattleState, 0.15f);
            }
        }
    }


    /// <summary>
    /// Reproduce animación de alerta
    /// </summary>
    public void PlaySenseSomething()
    {
        if (!string.IsNullOrEmpty(senseSomethingState))
        {
            PlayOneShot(senseSomethingState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de defensa
    /// </summary>
    public void PlayDefend()
    {
        if (!string.IsNullOrEmpty(defendState))
        {
            PlayOneShot(defendState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de recibir daño
    /// </summary>
    public void PlayGetHit()
    {
        if (!string.IsNullOrEmpty(getHitState))
        {
            PlayOneShot(getHitState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de muerte
    /// </summary>
    public void PlayDeath()
    {
        if (!string.IsNullOrEmpty(dieState))
        {
            _currentState = AnimationState.Dead;
            CrossFadeToState(dieState, 0.1f);
            
            // Disable further updates
            enabled = false;
        }
    }
    
    /// <summary>
    /// Reproduce animación de victoria
    /// </summary>
    public void PlayVictory()
    {
        if (!string.IsNullOrEmpty(victoryState))
        {
            PlayOneShot(victoryState);
        }
    }
    
    #endregion
    
    #region Public API - Rotation
    
    /// <summary>
    /// Hace que el NPC mire hacia un objetivo
    /// </summary>
    public void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f;
        
        if (direction.sqrMagnitude < 0.001f)
            return;
        
        _targetRotation = Quaternion.LookRotation(direction.normalized);
    }
    
    /// <summary>
    /// Hace que el NPC mire hacia una dirección
    /// </summary>
    public void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        
        if (direction.sqrMagnitude < 0.001f)
            return;
        
        _targetRotation = Quaternion.LookRotation(direction.normalized);
    }
    
    /// <summary>
    /// Rota el NPC suavemente hacia un objetivo durante un tiempo
    /// </summary>
    public void RotateTowardsTarget(Transform target, float duration = 0.3f)
    {
        if (_rotationCoroutine != null)
        {
            StopCoroutine(_rotationCoroutine);
        }
        
        _rotationCoroutine = StartCoroutine(RotateTowardsCoroutine(target, duration));
    }
    
    private IEnumerator RotateTowardsCoroutine(Transform target, float duration)
    {
        float elapsed = 0f;
        Quaternion startRotation = transform.rotation;
        
        while (elapsed < duration && target != null)
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction.normalized);
                float t = Mathf.Clamp01(elapsed / duration);
                transform.rotation = Quaternion.Slerp(startRotation, targetRot, t);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _rotationCoroutine = null;
    }
    
    #endregion
    
    #region Public API - Utility
    
    /// <summary>
    /// Establece el override de interacción (estado custom)
    /// </summary>
    public void SetInteractOverride(string stateName, bool clearOnEnd = true)
    {
        if (!string.IsNullOrEmpty(stateName))
        {
            interactState = stateName;
        }
    }
    
    /// <summary>
    /// Limpia el override de interacción
    /// </summary>
    public void ClearInteractOverride()
    {
        interactState = "InteractWithPeople_NoWeapon";
    }
    
    /// <summary>
    /// Establece la referencia del jugador
    /// </summary>
    public void SetPlayer(Transform player, Transform playerCam = null)
    {
        _player = player;
        if (playerCam != null)
            _playerCam = playerCam;
    }
    
    /// <summary>
    /// Obtiene el estado actual de animación
    /// </summary>
    public AnimationState GetCurrentState() => _currentState;
    
    /// <summary>
    /// Verifica si está reproduciendo una animación
    /// </summary>
    public bool IsPlayingAnimation() => _oneShotCoroutine != null;
    
    #endregion
    
    #region Private Methods - Core
    
    private void UpdateActualSpeed()
    {
        // Calculate actual movement speed based on position change
        Vector3 currentPosition = transform.position;
        float distance = Vector3.Distance(currentPosition, _lastPosition);
        _actualSpeed = distance / Time.deltaTime;
        _lastPosition = currentPosition;
    }
    
    private void SyncWithNavMeshAgent()
    {
        if (!navAgent.enabled || !navAgent.isOnNavMesh)
            return;
        
        // Get agent velocity
        float agentSpeed = navAgent.velocity.magnitude;
        float maxSpeed = navAgent.speed;
        
        // Normalize speed
        float normalizedSpeed = maxSpeed > 0 ? Mathf.Clamp01(agentSpeed / maxSpeed) : 0f;
        
        // Apply to animation
        SetMovementSpeed(normalizedSpeed);
        
        // Update rotation based on velocity
        if (agentSpeed > movementThreshold && navAgent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 direction = navAgent.velocity.normalized;
            FaceDirection(direction);
        }
    }
    
    private void ApplySmoothRotation()
    {
        // Calculate angle difference
        float angle = Quaternion.Angle(transform.rotation, _targetRotation);
        
        if (angle < minRotationAngle)
            return;
        
        // Apply smooth rotation
        float maxDegreesDelta = rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            _targetRotation,
            maxDegreesDelta
        );
    }
    
    #endregion
    
    #region Private Methods - State Transitions
    
    private void TransitionToIdle()
    {
        _currentState = AnimationState.Idle;
        CrossFadeToState(idleNormalState, 0.2f);
    }
    
    private void TransitionToLocomotion()
    {
        if (_currentMovementSpeed > movementThreshold * 2f)
        {
            _currentState = AnimationState.Running;
        }
        else
        {
            _currentState = AnimationState.Walking;
        }
        
        CrossFadeToState(locomotionState, locomotionBlendTime);
    }
    
    #endregion
    
    #region Private Methods - Animation Helpers
    
    private void CrossFadeToState(string stateName, float transitionTime, int layer = 0)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
            return;
        
        int stateHash = Animator.StringToHash(stateName);
        
        // Check if state exists in specified layer
        if (animator.HasState(layer, stateHash))
        {
            animator.CrossFadeInFixedTime(stateHash, transitionTime, layer, 0f);
        }
        else if (debugMode)
        {
            Debug.LogWarning($"[NPCAnimator] State '{stateName}' not found in layer {layer}");
        }
    }
    
    private float GetClipLength(string stateName)
    {
        // Check cache first
        if (_clipLengthCache.TryGetValue(stateName, out float cachedLength))
        {
            return cachedLength;
        }
        
        // Try to find clip length
        float length = 0f;
        
        if (animator != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains(stateName))
                {
                    length = clip.length;
                    break;
                }
            }
        }
        
        // Cache result
        if (length > 0f)
        {
            _clipLengthCache[stateName] = length;
        }
        
        return length > 0f ? length : 1f; // Fallback to 1 second
    }
    
    private void CacheAnimationClips()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;
        
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (!_clipLengthCache.ContainsKey(clip.name))
            {
                _clipLengthCache[clip.name] = clip.length;
            }
        }
        
        if (debugMode)
            Debug.Log($"[NPCAnimator] Cached {_clipLengthCache.Count} animation clips");
    }
    
    private void ResolvePlayerReferences()
    {
        _player = PlayerLocator.ResolvePlayer();
        _playerCam = PlayerLocator.ResolvePlayerCamera();
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnInteractionStarted()
    {
        BeginInteraction();
    }
    
    private void OnInteractionFinished()
    {
        EndInteraction();
    }
    
    #endregion
    
    #region Gizmos
    
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;
        
        // Draw movement direction
        if (_currentMovementSpeed > movementThreshold)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 2f);
        }
        
        // Draw target rotation
        Gizmos.color = Color.yellow;
        Vector3 targetDir = _targetRotation * Vector3.forward;
        Gizmos.DrawRay(transform.position + Vector3.up * 1.5f, targetDir * 1.5f);
        
        // Draw state
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2.5f,
            $"Anim: {_currentState}\nSpeed: {_currentMovementSpeed:F2}",
            new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.white },
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            }
        );
        #endif
    }
    
    #endregion
}
