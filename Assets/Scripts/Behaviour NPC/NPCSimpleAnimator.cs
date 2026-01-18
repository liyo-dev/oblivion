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
    [SerializeField] private string searchingState = "SenseSomethingSearching_NoWeapon"; // Animación cuando pierde de vista al jugador
    [SerializeField] private string defendState = "Idle_Battle_NoWeapon"; // Fallback a Battle Idle si no existe Defend
    
    [Tooltip("Animaciones de daño (se alterna aleatoriamente entre ellas para variedad)")]
    [SerializeField] private string[] getHitStates = new string[] { "TakeDamage", "TakeDamage_2" };
    
    [SerializeField] private string defendHitState = "DefendHit_NoWeapon"; // Animación cuando el escudo bloquea un ataque
    [SerializeField] private string dieState = "Die02_NoWeapon";
    [SerializeField] private string dizzyState = "Dizzy_NoWeapon"; // Animación de mareo después de levantarse
    [SerializeField] private string victoryState = "Victory_NoWeapon"; // Animación de victoria cuando el NPC gana
    
    [Header("Spell Cast Animations (UpperBody Layer)")]
    [Tooltip("Animación de disparo con mano izquierda")]
    [SerializeField] private string spellCastLeftState = "MagicLeft";
    
    [Tooltip("Animación de disparo con mano derecha")]
    [SerializeField] private string spellCastRightState = "MagicRight";
    
    [Tooltip("Animación de disparo especial (ambas manos)")]
    [SerializeField] private string spellCastSpecialState = "MagicSpecial";
    
    [Header("Root Motion")]
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
    
    // Spell casting hand alternation
    private int _lastSpellHand = -1; // -1 = ninguno, 0 = izquierda, 1 = derecha
    
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
    
    // ✅ Anti-spam para animaciones de idle
    private float _lastBattleIdleTime = -999f;
    private const float BattleIdleCooldown = 0.5f; // Mínimo 0.5s entre llamadas (antes 0.3s)
    private bool _disableAutoRotation; // Flag para desactivar rotación automática (usado durante diálogos)
    
    // ✅ Corrutina para seguimiento de rotación durante diálogos
    private Coroutine _dialogueLookAtCoroutine;
    
    #endregion
    
    #region Public Properties
    
    /// <summary>
    /// Permite a sistemas externos (como narrativas) tomar control de la rotación.
    /// </summary>
    public bool AllowManualRotation { get; set; }

    /// <summary>
    /// Indica si el NPC está en modo batalla
    /// </summary>
    public bool IsInBattle => _isInBattle;
    
    /// <summary>
    /// Indica si el NPC está en modo batalla (layer de Battle activo)
    /// </summary>
    public bool IsInBattleMode => _isInBattle;
    
    /// <summary>
    /// Indica si el NPC está actualmente reproduciendo la animación de mareo (dizzy)
    /// </summary>
    public bool IsInDizzyAnimation()
    {
        if (animator == null || string.IsNullOrEmpty(dizzyState))
            return false;
        
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(dizzyState);
    }
    
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
        
        // ✅ FIX CRÍTICO: Configurar NavMeshAgent para control de rotación correcto
        if (navAgent != null)
        {
            // Desactivar rotación automática del NavMeshAgent
            navAgent.updateRotation = false;
            
            // Asegurar que angularSpeed sea lo suficientemente alto
            if (navAgent.angularSpeed < 120f)
            {
                if (debugMode)
                    Debug.LogWarning($"[NPCAnimator] NavMeshAgent.angularSpeed muy bajo ({navAgent.angularSpeed}), aumentando a 360°/s");
                navAgent.angularSpeed = 360f;
            }
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
        
        // ✅ Suscribirse a eventos del DialogueManager
        DialogueManager.OnDialogueStarted += OnDialogueStarted;
        DialogueManager.OnDialogueClosed += OnDialogueClosed;
    }
    
    void OnDestroy()
    {
        // ✅ Desuscribirse de eventos del DialogueManager
        DialogueManager.OnDialogueStarted -= OnDialogueStarted;
        DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        
        // Detener corrutina si existe
        if (_dialogueLookAtCoroutine != null)
        {
            StopCoroutine(_dialogueLookAtCoroutine);
            _dialogueLookAtCoroutine = null;
        }
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
        
        // ✅ No procesar nada si el NPC está muerto
        if (_currentState == AnimationState.Dead)
            return;
        
        // Update actual speed based on position
        UpdateActualSpeed();
        
        // ✅ Sincronizar automáticamente con NavMeshAgent si existe y está activo
        // No necesita configuración manual - es siempre automático
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
        {
            SyncWithNavMeshAgent();
        }
    }
    
    void LateUpdate()
    {
        // ✅ No procesar rotación si el NPC está muerto
        if (_currentState == AnimationState.Dead)
            return;
            
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
        {
            Debug.LogWarning("[NPCAnimator] SetMovementSpeed llamado pero animator es null");
            return;
        }
        
        // ✅ No procesar movimiento si el NPC está muerto
        if (_currentState == AnimationState.Dead)
            return;
        
        _currentMovementSpeed = Mathf.Clamp01(normalizedSpeed);
        
        // Use configured damp time if not specified
        float damp = dampTime < 0 ? inputMagnitudeDampTime : dampTime;
        
        // Set animator parameter
        animator.SetFloat(InputMagnitudeHash, _currentMovementSpeed, damp, Time.deltaTime);
        
        // ✅ FIX CRÍTICO: SIEMPRE asegurar que animator.speed sea 1.0 cuando está quieto
        // Esto previene el temblor en Battle Idle cuando _currentMovementSpeed es muy bajo
        if (_currentMovementSpeed <= movementThreshold)
        {
            // ✅ IMPERATIVO: Velocidad normal cuando está quieto (evita temblor)
            animator.speed = 1f;
        }
        // Adjust animation speed to match movement speed (reduces foot sliding)
        else if (_currentMovementSpeed > movementThreshold)
        {
            animator.speed = Mathf.Lerp(1f, locomotionSpeedMultiplier, _currentMovementSpeed);
            
            // Ensure we're in locomotion state if moving
            if (_isInBattle && !_isInteracting)
            {
                if (_currentState != AnimationState.Walking && _currentState != AnimationState.Running)
                {
                    TransitionToLocomotion();
                }
            }
            else if ((_currentState == AnimationState.Idle) && !_isInteracting)
            {
                TransitionToLocomotion();
            }
        }
    }
    
    /// <summary>
    /// Resetea el movimiento a 0
    /// </summary>
    public void ResetMovement()
    {
        // ✅ No hacer nada si el NPC está muerto
        if (_currentState == AnimationState.Dead)
            return;
        
        // ✅ Forzar a 0 INMEDIATAMENTE sin dampening para evitar bugs de "andando en el sitio"
        _currentMovementSpeed = 0f;
        if (animator != null)
        {
            animator.SetFloat(InputMagnitudeHash, 0f);
            animator.speed = 1f;
        }
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
        // ✅ No hacer nada si el NPC está muerto
        if (_currentState == AnimationState.Dead)
        {
            if (debugMode)
                Debug.Log($"[NPCAnimator] SetBattleMode({enable}) ignorado - NPC está muerto");
            return;
        }
        
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
    /// Con protección anti-spam: solo cambia si no está ya en el estado
    /// </summary>
    public void PlayBattleIdle()
    {
        if (_isInBattle && !string.IsNullOrEmpty(idleBattleState))
        {
            // ✅ CRÍTICO: Cooldown para evitar spam de CrossFade
            // Si se llama demasiado frecuentemente, se reinicia la animación constantemente
            // causando temblor en el modelo y el Enemy Marker
            float timeSinceLastCall = Time.time - _lastBattleIdleTime;
            if (timeSinceLastCall < BattleIdleCooldown)
            {
                // Llamada demasiado frecuente, ignorar
                return;
            }
            
            // ✅ Solo crossfade si NO está ya en este estado (evita spam)
            int targetHash = Animator.StringToHash(idleBattleState);
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            
            if (currentState.shortNameHash != targetHash)
            {
                _lastBattleIdleTime = Time.time;
                CrossFadeToState(idleBattleState, 0.2f);
            }
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
        
        // ✅ NO hacer transición a Idle si el NPC está muerto
        // Esto previene que se cancele la animación de muerte
        if (_currentState == AnimationState.Dead)
        {
            if (debugMode)
                Debug.Log($"[NPCAnimator] OneShot completado pero NPC está muerto - NO transicionar a Idle");
            _oneShotCoroutine = null;
            yield break; // ✅ Usar yield break en lugar de return en coroutines
        }
        
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
        
        // ✅ FIX: Solo girar hacia el jugador si la rotación automática NO está deshabilitada
        // Si DialogueManager ya controló la rotación, no interferir
        if (_player != null && !_disableAutoRotation)
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
            PlayOneShot(challengingState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de Challenge y luego va a Idle Battle
    /// </summary>
    public void PlayChallengingForBattle()
    {
        if (!string.IsNullOrEmpty(challengingState))
        {
            PlayOneShot(challengingState, 0, () =>
            {
                _isInBattle = true;
                _currentState = AnimationState.Battle;
                
                if (!string.IsNullOrEmpty(idleBattleState))
                {
                    CrossFadeToState(idleBattleState, 0.2f);
                }
            });
        }
        else
        {
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
    /// Reproduce animación de búsqueda (cuando pierde de vista al jugador).
    /// Se usa típicamente cuando el NPC huye y luego se detiene sin ver al player.
    /// </summary>
    public void PlaySearching()
    {
        if (!string.IsNullOrEmpty(searchingState))
        {
            Debug.Log($"[NPCAnimator:{gameObject.name}] 🔍 PlaySearching() - Buscando al jugador");
            PlayOneShot(searchingState);
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
    /// Reproduce animación de recibir daño.
    /// Si hay múltiples animaciones configuradas, selecciona una aleatoriamente para variedad.
    /// </summary>
    public void PlayGetHit()
    {
        if (getHitStates == null || getHitStates.Length == 0)
        {
            Debug.LogWarning($"[NPCAnimator:{gameObject.name}] ⚠️ No hay animaciones de daño configuradas");
            return;
        }
        
        // Seleccionar animación aleatoria del array
        string selectedHitAnim = getHitStates[UnityEngine.Random.Range(0, getHitStates.Length)];
        
        if (!string.IsNullOrEmpty(selectedHitAnim))
        {
            Debug.Log($"[NPCAnimator:{gameObject.name}] 💥 PlayGetHit() - Animación seleccionada: '{selectedHitAnim}' ({getHitStates.Length} variantes disponibles)");
            PlayOneShot(selectedHitAnim);
        }
    }
    
    /// <summary>
    /// Reproduce animación de bloqueo con escudo (cuando el escudo absorbe un ataque)
    /// </summary>
    public void PlayDefendHit()
    {
        if (!string.IsNullOrEmpty(defendHitState))
        {
            PlayOneShot(defendHitState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de muerte
    /// </summary>
    public void PlayDeath()
    {
        Debug.Log($"[NPCAnimator:{gameObject.name}] 💀 PlayDeath() llamado - dieState: '{dieState}'");
        
        if (!string.IsNullOrEmpty(dieState))
        {
            _currentState = AnimationState.Dead;
            
            Debug.Log($"[NPCAnimator:{gameObject.name}] 🎬 Reproduciendo animación de muerte: {dieState}");
            
            // Usar Play directamente para reproducir la animación de muerte inmediatamente
            // CrossFade puede causar que la animación no se vea si el NPC muere muy rápido
            if (animator != null)
            {
                // Resetear el parámetro InputMagnitude a 0 para evitar movimiento residual
                animator.SetFloat(InputMagnitudeHash, 0f);
                animator.Play(dieState, 0); // Layer 0, reproducción inmediata
                Debug.Log($"[NPCAnimator:{gameObject.name}] ✅ animator.Play('{dieState}', 0) ejecutado");
            }
            
            // Desactivar el NavMeshAgent si existe
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
                navAgent.updateRotation = false;
                navAgent.updatePosition = false;
                Debug.Log($"[NPCAnimator:{gameObject.name}] NavMeshAgent detenido");
            }
            
            // NO desactivar el componente inmediatamente - dejar que la animación se reproduzca
            // enabled = false;  // ❌ COMENTADO - Esto evitaba que la animación se reprodujera
            
            Debug.Log($"[NPCAnimator:{gameObject.name}] ✅ Animación de muerte iniciada");
        }
        else
        {
            Debug.LogWarning($"[NPCAnimator:{gameObject.name}] ⚠️ dieState está vacío - no se puede reproducir animación de muerte");
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
    
    /// <summary>
    /// Reproduce animación de mareo después de levantarse (post-derrota)
    /// El NPC se levanta aturdido y permanece en estado de mareo
    /// </summary>
    public void PlayDizzy()
    {
        Debug.Log($"[NPCAnimator:{gameObject.name}] 😵 PlayDizzy() llamado - dizzyState: '{dizzyState}'");
        
        if (!string.IsNullOrEmpty(dizzyState))
        {
            // Cambiar a estado normal (no muerto)
            _currentState = AnimationState.Idle;
            
            Debug.Log($"[NPCAnimator:{gameObject.name}] 🎬 Reproduciendo animación de mareo: {dizzyState}");
            
            if (animator != null)
            {
                // Reproducir la animación de mareo
                animator.Play(dizzyState, 0);
                animator.speed = 1f; // Asegurar velocidad normal
                Debug.Log($"[NPCAnimator:{gameObject.name}] ✅ animator.Play('{dizzyState}', 0) ejecutado");
            }
            
            Debug.Log($"[NPCAnimator:{gameObject.name}] ✅ Animación de mareo iniciada");
        }
        else
        {
            Debug.LogWarning($"[NPCAnimator:{gameObject.name}] ⚠️ dizzyState está vacío - no se puede reproducir animación de mareo");
        }
    }
    
    #endregion
    
    #region Public API - Spell Casting
    
    /// <summary>
    /// Reproduce animación de disparo de hechizo con alternancia automática de manos.
    /// Usa UpperBody layer y vuelve automáticamente a locomotion al terminar.
    /// </summary>
    public void PlaySpellCast()
    {
        // Alternar entre mano izquierda y derecha
        if (_lastSpellHand != 0) // Si la última fue derecha o ninguna, usar izquierda
        {
            PlaySpellCastLeft();
        }
        else // Si la última fue izquierda, usar derecha
        {
            PlaySpellCastRight();
        }
    }
    
    /// <summary>
    /// Reproduce animación de disparo con mano izquierda (UpperBody layer)
    /// </summary>
    public void PlaySpellCastLeft()
    {
        if (!string.IsNullOrEmpty(spellCastLeftState))
        {
            _lastSpellHand = 0;
            
            if (debugMode)
                Debug.Log($"[NPCAnimator] PlaySpellCastLeft: {spellCastLeftState} en layer {upperBodyLayer}");
            
            // Reproducir en el UpperBody layer con callback para volver a locomotion
            PlaySpellCastInternal(spellCastLeftState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de disparo con mano derecha (UpperBody layer)
    /// </summary>
    public void PlaySpellCastRight()
    {
        if (!string.IsNullOrEmpty(spellCastRightState))
        {
            _lastSpellHand = 1;
            
            if (debugMode)
                Debug.Log($"[NPCAnimator] PlaySpellCastRight: {spellCastRightState} en layer {upperBodyLayer}");
            
            // Reproducir en el UpperBody layer con callback para volver a locomotion
            PlaySpellCastInternal(spellCastRightState);
        }
    }
    
    /// <summary>
    /// Reproduce animación de disparo especial con ambas manos (UpperBody layer)
    /// </summary>
    public void PlaySpellCastSpecial()
    {
        if (!string.IsNullOrEmpty(spellCastSpecialState))
        {
            _lastSpellHand = 2; // Marcar como especial
            
            if (debugMode)
                Debug.Log($"[NPCAnimator] PlaySpellCastSpecial: {spellCastSpecialState} en layer {upperBodyLayer}");
            
            // Reproducir en el UpperBody layer con callback para volver a locomotion
            PlaySpellCastInternal(spellCastSpecialState);
        }
    }
    
    /// <summary>
    /// Método interno para reproducir animaciones de spell cast en UpperBody layer
    /// </summary>
    private void PlaySpellCastInternal(string stateName)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
            return;
        
        // Asegurar que el UpperBody layer esté activo
        if (upperBodyLayer > 0 && upperBodyLayer < animator.layerCount)
        {
            animator.SetLayerWeight(upperBodyLayer, 1f);
        }
        
        // Reproducir la animación en el UpperBody layer usando PlayOneShot
        // NO forzar transición a locomotion en el callback - dejar que el animator lo maneje naturalmente
        PlayOneShot(stateName, upperBodyLayer, () =>
        {
            if (debugMode)
                Debug.Log($"[NPCAnimator] Spell cast completado en UpperBody layer");
            
            // El UpperBody layer volverá a su estado idle automáticamente
            // El Base Layer (piernas) puede continuar con locomotion si se está moviendo
            // NO hacer nada aquí - dejar que el sistema fluya naturalmente
        });
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
    /// Desactiva la rotación automática (útil durante diálogos cuando otro sistema controla la rotación)
    /// </summary>
    public void DisableAutoRotation()
    {
        _disableAutoRotation = true;
    }
    
    /// <summary>
    /// Reactiva la rotación automática
    /// </summary>
    public void EnableAutoRotation()
    {
        _disableAutoRotation = false;
        
        // ✅ FIX: Sincronizar _targetRotation con la rotación actual del transform
        // Esto evita que al reactivar, el NPC gire hacia una dirección antigua
        _targetRotation = transform.rotation;
    }
    
    #region Dialogue Events
    
    /// <summary>
    /// Callback cuando se inicia un diálogo. Solo responde si este NPC es el involucrado.
    /// </summary>
    private void OnDialogueStarted(Transform npcInvolved)
    {
        // Solo procesar si este NPC es el que está en el diálogo
        if (npcInvolved != transform)
            return;
        
        if (debugMode)
            Debug.Log($"[NPCAnimator:{name}] 📢 OnDialogueStarted recibido");
        
        // Detener cualquier corrutina previa
        if (_dialogueLookAtCoroutine != null)
        {
            StopCoroutine(_dialogueLookAtCoroutine);
        }
        
        // Rotar instantáneamente hacia el jugador y desactivar rotación automática
        FacePlayerInstantly();
        
        // Reproducir animación de interacción
        BeginInteraction();
        
        // Iniciar la corrutina de seguimiento continuo
        _dialogueLookAtCoroutine = StartCoroutine(KeepLookingAtPlayerDuringDialogue());
    }
    
    /// <summary>
    /// Callback cuando se cierra un diálogo. Solo responde si este NPC es el involucrado.
    /// </summary>
    private void OnDialogueClosed(Transform npcInvolved)
    {
        // Solo procesar si este NPC es el que estaba en el diálogo
        if (npcInvolved != transform)
            return;
        
        if (debugMode)
            Debug.Log($"[NPCAnimator:{name}] 📢 OnDialogueClosed recibido");
        
        // Detener la corrutina de seguimiento
        if (_dialogueLookAtCoroutine != null)
        {
            StopCoroutine(_dialogueLookAtCoroutine);
            _dialogueLookAtCoroutine = null;
        }
        
        // Finalizar animación de interacción
        EndInteraction();
        
        // Reactivar la rotación automática después de un frame
        // para asegurar que el diálogo se cerró completamente
        StartCoroutine(ReactivateAutoRotationDelayed());
    }
    
    /// <summary>
    /// Mantiene al NPC mirando al jugador durante todo el diálogo
    /// </summary>
    private IEnumerator KeepLookingAtPlayerDuringDialogue()
    {
        // Obtener referencia al DialogueManager para verificar si el diálogo está abierto
        var dialogueManager = DialogueManager.Instance;
        
        if (_player == null)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                _player = playerGo.transform;
            }
        }
        
        if (_player == null)
        {
            if (debugMode) Debug.LogWarning($"[NPCAnimator:{name}] KeepLookingAtPlayerDuringDialogue - No se encontró el jugador");
            yield break;
        }
        
        if (debugMode)
            Debug.Log($"[NPCAnimator:{name}] 👁️ Seguimiento de rotación iniciado durante diálogo");
        
        // Mantener rotación hacia el jugador mientras el diálogo esté abierto
        while (dialogueManager != null && dialogueManager.IsOpen && _player != null)
        {
            // Calcular dirección hacia el jugador (solo horizontal)
            Vector3 directionToPlayer = _player.position - transform.position;
            directionToPlayer.y = 0f;
            
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                
                // Rotar suavemente hacia el jugador (por si se mueve)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.unscaledDeltaTime
                );
                
                // Sincronizar _targetRotation
                _targetRotation = transform.rotation;
            }
            
            yield return null;
        }
        
        if (debugMode)
            Debug.Log($"[NPCAnimator:{name}] 🔚 Seguimiento de rotación durante diálogo finalizado");
    }
    
    /// <summary>
    /// Reactiva la rotación automática después de un frame
    /// </summary>
    private IEnumerator ReactivateAutoRotationDelayed()
    {
        // Esperar 1 frame para asegurar que el diálogo se cerró completamente
        yield return null;
        
        EnableAutoRotation();
        
        if (debugMode)
            Debug.Log($"[NPCAnimator:{name}] ✅ Rotación automática reactivada");
    }
    
    #endregion
    
    /// <summary>
    /// Gira instantáneamente hacia el jugador (para inicio de diálogos).
    /// Desactiva la rotación automática para evitar conflictos.
    /// </summary>
    /// <returns>True si la rotación fue exitosa</returns>
    public bool FacePlayerInstantly()
    {
        // Buscar el jugador si no tenemos referencia
        if (_player == null)
        {
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                _player = playerGo.transform;
            }
        }
        
        if (_player == null)
        {
            if (debugMode) Debug.LogWarning($"[NPCAnimator:{name}] FacePlayerInstantly: No se encontró jugador");
            return false;
        }
        
        // Desactivar rotación automática para evitar que interfiera
        _disableAutoRotation = true;
        
        // Calcular dirección al jugador (solo horizontal)
        Vector3 directionToPlayer = _player.position - transform.position;
        directionToPlayer.y = 0f;
        
        if (directionToPlayer.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = targetRotation;
            _targetRotation = targetRotation; // Sincronizar para evitar snapping posterior
            
            if (debugMode) 
                Debug.Log($"[NPCAnimator:{name}] 👁️ Rotado INSTANTÁNEAMENTE hacia el jugador (ángulo: {targetRotation.eulerAngles.y:F1}°)");
            
            return true;
        }
        
        return false;
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
        
        // ✅ FIX: No actualizar rotación si está deshabilitada (ej: durante/después de cinemáticas)
        if (_disableAutoRotation || AllowManualRotation)
            return;
        
        // Update rotation based on velocity
        if (agentSpeed > movementThreshold && navAgent.velocity.sqrMagnitude > 0.01f)
        {
            Vector3 direction = navAgent.velocity.normalized;
            
            // ✅ DEBUG: Log rotación
            if (debugMode && Time.frameCount % 30 == 0) // Cada 30 frames
            {
                Vector3 forward = transform.forward;
                float angleDiff = Vector3.Angle(forward, direction);
                Debug.Log($"[NPCAnimator] ROTACIÓN DEBUG:\n" +
                         $"  Transform.forward: {forward}\n" +
                         $"  NavAgent.velocity: {navAgent.velocity}\n" +
                         $"  Direction: {direction}\n" +
                         $"  Angle diff: {angleDiff:F1}°\n" +
                         $"  updateRotation: {navAgent.updateRotation}\n" +
                         $"  _disableAutoRotation: {_disableAutoRotation}");
            }
            
            FaceDirection(direction);
        }
    }
    
    private void ApplySmoothRotation()
    {
        // Skip if auto rotation is disabled (e.g. during dialogue)
        if (_disableAutoRotation || AllowManualRotation)
            return;
            
        // Calculate angle difference
        float angle = Quaternion.Angle(transform.rotation, _targetRotation);
        
        if (angle < minRotationAngle)
            return;
        
        // ✅ DEBUG: Log aplicación de rotación
        if (debugMode && Time.frameCount % 60 == 0) // Cada 60 frames
        {
            Debug.Log($"[NPCAnimator] APLICANDO ROTACIÓN:\n" +
                     $"  Current: {transform.rotation.eulerAngles}\n" +
                     $"  Target: {_targetRotation.eulerAngles}\n" +
                     $"  Angle diff: {angle:F1}°\n" +
                     $"  Speed: {rotationSpeed}°/s");
        }
        
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
    
    /// <summary>
    /// Transiciona al estado Idle. Público para reseteo externo (ej: después de stun)
    /// </summary>
    public void TransitionToIdle()
    {
        _currentState = AnimationState.Idle;
        
        // ✅ FIX: Desactivar root motion en Idle también
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        
        // Elegir el idle correcto según el modo (batalla o normal)
        string targetIdle = _isInBattle ? idleBattleState : idleNormalState;
        CrossFadeToState(targetIdle, 0.2f);
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
        
        // ✅ FIX CRÍTICO: Desactivar root motion durante locomotion
        // para que el NPCSimpleAnimator controle la rotación, no la animación
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        
        if (string.IsNullOrEmpty(locomotionState))
        {
            if (debugMode)
                Debug.LogError($"[NPCAnimator] locomotionState está vacío");
            return;
        }
        
        CrossFadeToState(locomotionState, locomotionBlendTime);
    }
    
    #endregion
    
    #region Private Methods - Animation Helpers
    
    private void CrossFadeToState(string stateName, float transitionTime, int layer = 0)
    {
        if (string.IsNullOrEmpty(stateName) || animator == null)
        {
            Debug.LogWarning($"[NPCAnimator] CrossFadeToState falló - stateName: {stateName}, animator: {animator != null}");
            return;
        }
        
        int stateHash = Animator.StringToHash(stateName);
        
        // Check if state exists in specified layer
        if (animator.HasState(layer, stateHash))
        {
            // Debug.Log($"[NPCAnimator] ✅ CrossFade a estado '{stateName}' en layer {layer}, tiempo: {transitionTime}s");
            animator.CrossFadeInFixedTime(stateHash, transitionTime, layer, 0f);
        }
        else
        {
            Debug.LogError($"[NPCAnimator] ❌ Estado '{stateName}' NO ENCONTRADO en layer {layer}. Verificar Animator Controller.");
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
