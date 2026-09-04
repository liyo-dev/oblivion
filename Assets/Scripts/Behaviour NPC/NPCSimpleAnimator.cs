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
    
    [Tooltip("Ángulo mínimo para considerar que debe rotar")]
    [SerializeField, Range(1f, 45f)] private float minRotationAngle = 5f;
    
    [Header("Interaction")]
    [SerializeField] private string interactState = "InteractWithPeople_NoWeapon";
    [SerializeField] private string greetingState = "Greeting01_NoWeapon";

    [Header("Saludo al inicio de diálogo")]
    [Tooltip("Si está activo, reproduce una animación de saludo al iniciar un diálogo")]
    [SerializeField] private bool playGreetingOnDialogueStart = false;
    [Tooltip("Animación de saludo (ej: HandWave01, Reverence01, HeadNod01)")]
    [SerializeField] private string greetingDialogueState = "HandWave01";

    [Header("Idle Variations")]
    [Tooltip("Activar variaciones de idle aleatorias cuando el NPC está parado")]
    [SerializeField] private bool enableIdleVariations = true;
    [Tooltip("Estados de idle variante (deben existir en el Animator Controller)")]
    [SerializeField] private string[] idleVariationStates = { "Idle02_NoWeapon", "Idle03_NoWeapon" };
    [Tooltip("Tiempo mínimo en segundos entre variaciones")]
    [SerializeField, Range(3f, 60f)] private float minIdleVariationInterval = 8f;
    [Tooltip("Tiempo máximo en segundos entre variaciones")]
    [SerializeField, Range(5f, 120f)] private float maxIdleVariationInterval = 20f;
    
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

    // ✅ FIX: parámetros de suelo/vuelo del controller compartido de personajes jugables
    // (Will/Liam/Estela usan el Animator Controller de Invector, que trae un Any State →
    // "Falling" condicionado a IsGrounded=false / GroundDistance>0.25 / isFlying=false).
    // El controller genérico de NPCs (Eldran) no tiene ese Any State, así que esto es
    // inofensivo para él. Ver ForceGroundedForSit().
    private static readonly int HashIsGrounded    = Animator.StringToHash("IsGrounded");
    private static readonly int HashGroundDistance = Animator.StringToHash("GroundDistance");
    private static readonly int HashIsFlying      = Animator.StringToHash("isFlying");
    private HashSet<int> _animatorParamHashes;

    private EmotionProfile _emotionProfile;
    
    // References
    private Transform _player;
    private Transform _playerCam;
    private Interactable _interactable;
    
    // Coroutines
    private Coroutine _oneShotCoroutine;
    private Coroutine _rotationCoroutine;
    private Coroutine _idleVariationCoroutine;
    private Coroutine _activityCoroutine;
    private Coroutine _victoryCelebrationCoroutine;

    // Índice de ciclo de Talk para variedad (Neutral)
    private int _lastTalkIndex = -1;
    
    // Caches
    private AnimatorStateCache _stateCache;
    private Dictionary<string, float> _clipLengthCache = new Dictionary<string, float>();
    
    // Smooth rotation
    private Quaternion _targetRotation;
    private float _rotationVelocity;

    // Velocidades del NPC para normalizar InputMagnitude con mapa de dos segmentos:
    //   agentSpeed == walkSpeed → 0.5 (zona walk del blend tree)
    //   agentSpeed == runSpeed  → 1.0 (zona run del blend tree)
    private float _walkSpeed = 1.5f;
    private float _runSpeed  = 4f;

    // ✅ Anti-spam para animaciones de idle
    private float _lastBattleIdleTime = -999f;
    private const float BattleIdleCooldown = 0.5f; // Mínimo 0.5s entre llamadas (antes 0.3s)
    private bool _disableAutoRotation; // Flag para desactivar rotación automática (usado durante diálogos)

    // ✅ FIX: recuerda si el NPC está sentado (PlayAmbientActivity con un Sit*) para que un
    // PlaySocialGesture/PlayOneShot lanzado durante un diálogo (p.ej. TabernaSequencer) no lo
    // deje de pie al terminar. Antes TransitionToIdle() siempre iba a idleNormalState, ignorando
    // que el NPC seguía sentado en la silla.
    private bool _isSeated;
    private NPCAmbientActivity _seatedActivity;
    
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Solo para diagnóstico en Editor/Development Build: valor actual del InputMagnitude
    /// que este NPCSimpleAnimator está aplicando al Animator.
    /// </summary>
    public float DebugCurrentMovementSpeed => _currentMovementSpeed;

    /// <summary>
    /// Solo para diagnóstico: el NavMeshAgent que este NPCSimpleAnimator está usando
    /// para sincronizar la animación de locomoción (SyncWithNavMeshAgent). Si no coincide
    /// con el agente que un sistema externo está moviendo (p.ej. un sequencer cinemático),
    /// la animación nunca reflejará el movimiento real.
    /// </summary>
    public NavMeshAgent DebugSyncedAgent => navAgent;
#endif

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
        var emotionController = GetComponent<NPCEmotionController>();
        if (emotionController != null)
            _emotionProfile = emotionController.EmotionProfile;
        
        // Initialize
        if (animator != null)
        {
            animator.applyRootMotion = useRootMotionForSpecialAnims;
            _stateCache = new AnimatorStateCache(animator);
            CacheAnimationClips();

            // ✅ FIX: cachear qué parámetros existen realmente en ESTE Animator Controller.
            // Eldran (NPC_NoWeapon.controller) no tiene IsGrounded/GroundDistance/isFlying;
            // Will/Liam/Estela sí (Invector@BasicLocomotion.controller). Sin esta guarda,
            // SetBool/SetFloat sobre un parámetro inexistente sería un no-op silencioso pero
            // costoso (y con warnings en editor); con ella, ForceGroundedForSit() es seguro
            // de llamar en cualquier NPC sin comprobar antes qué controller tiene.
            _animatorParamHashes = new HashSet<int>();
            var animParams = animator.parameters;
            for (int i = 0; i < animParams.Length; i++)
                _animatorParamHashes.Add(animParams[i].nameHash);
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

        var behaviourMgr = GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (behaviourMgr != null && behaviourMgr.Configuration != null)
        {
            // FIX (1 sep 2026) — INC-096/INC-097 seguían reproduciéndose en juego pese al fix del
            // 25 ago: el blend walk/run se normalizaba contra Configuration.walkSpeed/runSpeed
            // (1.5/4 en los prefabs de party — Estela, Liam), pensados para NPCs ambientales, y
            // nunca se sincronizó con las velocidades reales que FollowPlayerState usa para mover
            // al NavMeshAgent de un compañero (NPCPartyConfig.velocidadCaminando/velocidadCorriendo
            // = 5/10, y hasta 25 durante el catch-up dinámico de sprint). Con el techo del blend en
            // 4, cualquier velocidad real por encima de eso — que es casi siempre que el compañero
            // se mueve, incluso andando — se clampaba (Mathf.Clamp01 en SyncWithNavMeshAgent) a
            // normalizedSpeed=1.0: las piernas animan al ritmo fijo de "correr al máximo" mientras
            // el cuerpo se desliza por el NavMesh a una velocidad mucho más alta y variable — el
            // patinazo/trompicón reportado tanto andando despacio como esprintando. Si el NPC tiene
            // partyConfig asignado, usamos sus velocidades reales de seguimiento en vez de las
            // genéricas de Configuration.
            var partyCfg = behaviourMgr.Configuration.partyConfig;
            float walkSrc = partyCfg != null ? partyCfg.walkSpeed : behaviourMgr.Configuration.walkSpeed;
            float runSrc  = partyCfg != null ? partyCfg.runSpeed  : behaviourMgr.Configuration.runSpeed;
            _walkSpeed = Mathf.Max(0.1f, walkSrc);
            _runSpeed  = Mathf.Max(_walkSpeed + 0.1f, runSrc);
        }

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

        if (enable)
            StopIdleVariations();

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
            _isSeated = false; // fuerza de pie explícitamente (usado al liberar un asiento)
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
        else if (_isInteracting)
        {
            // FIX (30/08/2026, Raul: "hace la animacion de enfado, el bocadillo continua... se
            // queda [congelado]"; ronda siguiente: "no hace las anumaciones de hablar tras la de
            // enfado"): sin esto, al terminar el one-shot con _isInteracting=true (ver
            // BeginInteraction/EndInteraction en ShowBubblePaged, CinematicSequencerBase.cs) este
            // metodo no hacia NADA -- ni transicion a Idle (correctamente evitada, eso ya estaba
            // bien) ni ningun otro estado -- dejando el Animator congelado en el ultimo frame del
            // gesto (p.ej. "Angry01") indefinidamente. Con paginas de dialogo (ShowBubblePaged) mas
            // largas que el clip del gesto, y sobre todo con loopAnim repitiendo el MISMO trigger en
            // varias paginas seguidas, volver a cruzar a un estado en el que el Animator ya esta
            // parado (congelado en su ultimo frame) no siempre reinicia el clip de forma visible.
            // Se vuelve al interactState entre gestos -- mismo patron que PlayBodyEmotion() ya usa
            // con exito mas abajo en este archivo -- asi el personaje queda con una pose neutra de
            // "hablando" hasta el siguiente trigger, y cada retrigger es un crossfade limpio desde
            // un estado distinto, siempre visible.
            CrossFadeToState(interactState, 0.15f);
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

        StopIdleVariations();

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
    /// Reproduce el saludo configurado para ESTE NPC (greetingState, editable en el Inspector —
    /// cada NPC puede tener su propio gesto: Greeting01_NoWeapon, HandWave02, Reverence01...).
    /// </summary>
    /// <param name="onComplete">
    /// ✅ NUEVO (1 sep 2026, petición de Raúl): callback opcional invocado cuando el gesto termina
    /// de verdad (mismo mecanismo que PlayOneShot). Si el guard de abajo impide reproducirlo
    /// (_isInteracting activo o greetingState vacío), se invoca igualmente al momento para no
    /// dejar colgado a quien esté esperando el callback — ver
    /// NPCInteractiveNarrativeExecutor.ExecuteNarrativeChain.
    /// </param>
    public void PlayGreeting(Action onComplete = null)
    {
        if (!_isInteracting && !string.IsNullOrEmpty(greetingState))
        {
            // El gesto configurado vive en UpperBody layer (no debe congelar las piernas)
            PlayOneShot(greetingState, upperBodyLayer, onComplete);
        }
        else
        {
            onComplete?.Invoke();
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
            // Challenging_NoWeapon vive en UpperBody layer (no debe congelar las piernas)
            PlayOneShot(challengingState, upperBodyLayer);
        }
    }
    
    /// <summary>
    /// Reproduce animación de Challenge y luego va a Idle Battle
    /// </summary>
    public void PlayChallengingForBattle()
    {
        if (!string.IsNullOrEmpty(challengingState))
        {
            // Challenging_NoWeapon vive en UpperBody layer (no debe congelar las piernas)
            PlayOneShot(challengingState, upperBodyLayer, () =>
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
    /// Reproduce la animación de alerta configurada para ESTE NPC (senseSomethingState, editable
    /// en el Inspector).
    /// </summary>
    /// <param name="onComplete">
    /// ✅ NUEVO (1 sep 2026): callback opcional invocado cuando la animación termina de verdad
    /// (mismo mecanismo que PlayGreeting/PlayOneShot). Si senseSomethingState está vacío se
    /// invoca igualmente al momento.
    /// </param>
    public void PlaySenseSomething(Action onComplete = null)
    {
        if (!string.IsNullOrEmpty(senseSomethingState))
        {
            // El gesto configurado vive en UpperBody layer (no debe congelar las piernas)
            PlayOneShot(senseSomethingState, upperBodyLayer, onComplete);
        }
        else
        {
            onComplete?.Invoke();
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
            // SenseSomethingSearching_NoWeapon vive en UpperBody layer (no debe congelar las piernas)
            PlayOneShot(searchingState, upperBodyLayer);
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
            StopIdleVariations();
            
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

    /// <summary>
    /// FIX INC-028: sincroniza únicamente el objetivo de rotación suave (_targetRotation) con la
    /// rotación actual del transform, SIN tocar _disableAutoRotation (a diferencia de
    /// EnableAutoRotation()). _targetRotation se cachea una sola vez en Awake() con la rotación
    /// que el NPC tiene en la escena en ese momento; si después algo reposiciona/reorienta al NPC
    /// externamente (p.ej. GameBootProfile aplicando la rotación persistida al cargar partida) sin
    /// llamar a esto, ApplySmoothRotation() lo arrastra en los siguientes frames de vuelta hacia esa
    /// rotación cacheada — SyncWithNavMeshAgent() no la actualiza mientras el agente esté parado/sin
    /// path, así que un NPC quieto (p.ej. Eldran esperando fuera de la taberna) se queda girando
    /// hacia la rotación por defecto de la escena en vez de mantener la que se acaba de restaurar.
    /// </summary>
    public void SyncTargetRotation()
    {
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

        // Saludo opcional antes de que BeginInteraction fije la animación
        if (playGreetingOnDialogueStart && !string.IsNullOrEmpty(greetingDialogueState))
        {
            PlayOneShot(greetingDialogueState, 0, () =>
            {
                if (_isInteracting) CrossFadeToState(interactState, 0.15f);
            });
        }

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
    
    #region Public API - Social & Emotions

    /// <summary>
    /// Reproduce una animación corporal acorde a la emoción del NPC durante el diálogo.
    /// Las emociones Neutral/None rotan entre Talk01/02/03 para variedad.
    /// Si la emoción tiene el campo "Anim Corporal" vacío en el EmotionProfile, NO se reproduce
    /// ninguna animación: el NPC se queda con el gesto/pose que tuviera en ese momento (permite
    /// emociones que solo cambian la cara, sin tocar el cuerpo).
    /// Solo actúa si el NPC está en estado Interacting.
    /// </summary>
    public void PlayBodyEmotion(NPCEmotion emotion)
    {
        if (!_isInteracting || _currentState == AnimationState.Dead)
            return;

        string stateName = ResolveBodyAnimStateName(emotion);
        if (string.IsNullOrEmpty(stateName))
            return; // Emoción sin animación corporal asignada: se mantiene la pose actual

        // Los gestos corporales de diálogo (Talk01-03, Angry01-02, Cry01, Laugh01, Fear01, etc.)
        // viven en UpperBody layer para no congelar las piernas del NPC mientras gesticula.
        PlayOneShot(stateName, upperBodyLayer, () =>
        {
            if (_isInteracting && _currentState != AnimationState.Dead)
                CrossFadeToState(interactState, 0.15f);
        });
    }

    /// <summary>
    /// Resuelve el estado de animación corporal para una emoción.
    /// Neutral/None siempre rotan entre las animaciones neutrales (para dar variedad al hablar).
    /// Para el resto de emociones, devuelve tal cual el bodyAnimStateName configurado en el
    /// EmotionProfile: si está vacío, el llamador debe interpretarlo como "sin cambio de animación"
    /// (no hay fallback a Talk01, para no forzar un gesto en emociones que solo cambian la cara).
    /// Solo si no hay EmotionProfile asignado se usa un fallback de seguridad.
    /// </summary>
    private string ResolveBodyAnimStateName(NPCEmotion emotion)
    {
        string[] neutralAnims = (_emotionProfile != null && _emotionProfile.neutralBodyAnims is { Length: > 0 })
            ? _emotionProfile.neutralBodyAnims
            : new[] { "Talk01", "Talk02", "Talk03" };

        if (emotion == NPCEmotion.None || emotion == NPCEmotion.Neutral)
        {
            _lastTalkIndex = (_lastTalkIndex + 1) % neutralAnims.Length;
            return neutralAnims[_lastTalkIndex];
        }

        if (_emotionProfile != null)
        {
            var data = _emotionProfile.GetEmotionData(emotion);
            return data.bodyAnimStateName; // puede venir vacío a propósito: "sin cambio"
        }

        return neutralAnims[0];
    }

    /// <summary>
    /// Reproduce un gesto social como one-shot (para uso desde sistemas narrativos/quest).
    /// Disponible siempre, no requiere que el NPC esté en interacción.
    /// </summary>
    public void PlaySocialGesture(string stateName, Action onComplete = null)
    {
        if (string.IsNullOrEmpty(stateName) || _currentState == AnimationState.Dead)
            return;

        // Los gestos sociales pueden vivir en el Base Layer (locomoción/pose completa) o en el
        // UpperBody layer (gestos que no deben congelar las piernas, p.ej. Greeting01/HandWave01).
        // AnimatorLayerUtil resuelve en qué layer existe realmente el estado antes de reproducirlo
        // (misma utilidad compartida que usan PromoVideo01Sequencer y PlayerDialogueAnimator).
        int resolvedLayer = AnimatorLayerUtil.ResolveLayer(animator, stateName, upperBodyLayer);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnóstico siempre visible (no depende de debugMode): si el estado no existe en NINGÚN
        // layer del Animator Controller de este NPC en concreto, PlayOneShot fallará en silencio
        // (CrossFadeToState solo loguea con debugMode activo). Esto confirma o descarta rápidamente
        // si "no hacen animación al socializar" es un problema de contenido (estado/clip faltante
        // en ESTE controller) y no de la lógica del encuentro.
        if (animator != null && resolvedLayer < 0)
        {
            Debug.LogWarning($"[NPCAnimator:{gameObject.name}] ⚠️ PlaySocialGesture('{stateName}'): " +
                $"ese estado no existe en ningún layer del Animator Controller " +
                $"'{(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}'. " +
                "El NPC se quedará callado en este gesto.");
        }
#endif

        PlayOneShot(stateName, resolvedLayer >= 0 ? resolvedLayer : 0, onComplete);
    }

    /// <summary>
    /// Celebración de victoria aleatoria (Cheer01, Cheer02, HandClap01).
    /// Permite delay para escalonar la celebración entre varios aliados.
    /// </summary>
    public void PlayVictoryCelebration(float delay = 0f)
    {
        if (_currentState == AnimationState.Dead)
            return;

        if (_victoryCelebrationCoroutine != null)
            StopCoroutine(_victoryCelebrationCoroutine);

        _victoryCelebrationCoroutine = StartCoroutine(VictoryCelebrationRoutine(delay));
    }

    private IEnumerator VictoryCelebrationRoutine(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (_currentState == AnimationState.Dead || _isInBattle)
            yield break;

        string[] options = { "Cheer01", "Cheer02", "HandClap01" };
        string chosen = options[UnityEngine.Random.Range(0, options.Length)];

        // Cheer01/Cheer02/HandClap01 viven en UpperBody layer (no deben congelar las piernas)
        PlayOneShot(chosen, upperBodyLayer, () =>
        {
            if (!_isInBattle && !_isInteracting && _currentState != AnimationState.Dead)
                TransitionToIdle();
        });

        _victoryCelebrationCoroutine = null;
    }

    #endregion

    #region Public API - Ambient Activity

    /// <summary>
    /// Inicia una actividad ambiental (sentarse, comer, beber, dormir).
    /// Reproduce Begin → Loop. El NPC permanece en el Loop hasta que se llame StopAmbientActivity.
    /// </summary>
    public void PlayAmbientActivity(NPCAmbientActivity activity, NPCWorldPoint worldPoint = null)
    {
        if (activity == NPCAmbientActivity.None || _currentState == AnimationState.Dead)
            return;

        StopIdleVariations();

        _isSeated       = IsSitActivity(activity);
        _seatedActivity = activity;

        // ✅ FIX: si el NPC viene de un modo especial (vuelo/salto/plataforma replicado del
        // jugador vía FollowPlayerState) puede quedar con IsGrounded=false, GroundDistance
        // alto o isFlying=true en el Animator. En el controller compartido de personajes
        // jugables (Will/Liam/Estela) eso dispara un Any State → "Falling" que interrumpe
        // CUALQUIER estado activo, incluido el loop de sentado — el NPC se teletransporta
        // bien a la silla pero la animación salta a caída/de pie ("se sienta mal"). Eldran
        // usa un controller sin ese Any State, así que para él esto es inofensivo.
        if (_isSeated)
            ForceGroundedForSit();

        // Comer/beber viven en la UpperBody layer (torso/brazos, sin piernas) para no pisar la
        // locomoción de la Base Layer — el NPC puede seguir de pie/caminando con normalidad
        // mientras el gesto se reproduce encima. Ver GetActivityLayer().
        int activityLayer = GetActivityLayer(activity);
        if (activityLayer > 0 && animator != null && activityLayer < animator.layerCount)
            animator.SetLayerWeight(activityLayer, 1f);

        if (_activityCoroutine != null)
            StopCoroutine(_activityCoroutine);

        _activityCoroutine = StartCoroutine(AmbientActivityRoutine(activity, worldPoint));
    }

    private void ForceGroundedForSit()
    {
        TrySetBool(HashIsGrounded, true);
        TrySetFloat(HashGroundDistance, 0f);
        TrySetBool(HashIsFlying, false);
    }

    private void TrySetBool(int paramHash, bool value)
    {
        if (animator == null) return;
        if (_animatorParamHashes != null && !_animatorParamHashes.Contains(paramHash)) return;
        try { animator.SetBool(paramHash, value); } catch { }
    }

    private void TrySetFloat(int paramHash, float value)
    {
        if (animator == null) return;
        if (_animatorParamHashes != null && !_animatorParamHashes.Contains(paramHash)) return;
        try { animator.SetFloat(paramHash, value); } catch { }
    }

    private static bool IsSitActivity(NPCAmbientActivity activity)
    {
        return activity == NPCAmbientActivity.SitGround
            || activity == NPCAmbientActivity.SitLow
            || activity == NPCAmbientActivity.SitMedium
            || activity == NPCAmbientActivity.SitHigh;
    }

    /// <summary>
    /// Para la actividad ambiental activa. La animación de salida (Exit) se aplica si existe;
    /// luego el animator vuelve a idle normal.
    /// </summary>
    public void StopAmbientActivity(NPCAmbientActivity activity, NPCWorldPoint worldPoint = null)
    {
        _isSeated = false;

        if (_activityCoroutine != null)
        {
            StopCoroutine(_activityCoroutine);
            _activityCoroutine = null;
        }

        // Devolver el prop a su posición original antes de volver al idle
        worldPoint?.DetachProp();

        // Si la actividad usaba la UpperBody layer (comer/beber), soltarla al terminar para no
        // dejar el torso "congelado" en la última pose sobre la locomoción normal.
        int activityLayer = GetActivityLayer(activity);
        if (activityLayer > 0 && animator != null && activityLayer < animator.layerCount)
            animator.SetLayerWeight(activityLayer, 0f);

        string exitState = GetActivityExitState(activity);
        if (!string.IsNullOrEmpty(exitState) && animator != null && animator.HasState(0, Animator.StringToHash(exitState)))
        {
            PlayOneShot(exitState, 0, () => TransitionToIdle());
        }
        else
        {
            TransitionToIdle();
        }
    }

    private IEnumerator AmbientActivityRoutine(NPCAmbientActivity activity, NPCWorldPoint worldPoint)
    {
        // Adjuntar prop a la mano derecha si el worldPoint tiene uno
        worldPoint?.AttachPropToOccupant(animator);

        string beginState = GetActivityBeginState(activity);
        string loopState  = GetActivityLoopState(activity);
        int layer = GetActivityLayer(activity);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnóstico (15 ago 2026): a diferencia de PlaySocialGesture (que sí avisa si el estado
        // no existe), este método no dejaba ningún rastro en consola cuando el Animator Controller
        // no tenía el estado begin/loop (p.ej. los clips de SitGround están "pendientes de
        // importar", ver comentario en GetActivityBeginState más abajo) — el NPC simplemente se
        // quedaba de pie sin ningún indicio de por qué (reportado por Raúl: los NPCs bajo el árbol
        // "se quedan quietos" en vez de sentarse o tiritar de frío). Este log confirma en la
        // práctica si es un problema de CONTENIDO (clip/estado faltante en ESTE controller en
        // concreto) y no de la lógica que llama a PlayAmbientActivity — que si llega hasta aquí,
        // ya está haciendo su parte correctamente.
        bool hasBeginState = !string.IsNullOrEmpty(beginState) && animator != null && animator.HasState(layer, Animator.StringToHash(beginState));
        bool hasLoopState  = !string.IsNullOrEmpty(loopState)  && animator != null && animator.HasState(layer, Animator.StringToHash(loopState));
        if (!hasBeginState && !hasLoopState && (!string.IsNullOrEmpty(beginState) || !string.IsNullOrEmpty(loopState)))
        {
            Debug.LogWarning($"[NPCAnimator:{gameObject.name}] ⚠️ PlayAmbientActivity({activity}): ni '{beginState}' ni '{loopState}' " +
                $"existen en la layer {layer} del Animator Controller " +
                $"'{(animator != null && animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "null")}'. " +
                "El NPC se quedará de pie en vez de sentarse — revisar si los clips están importados y wireados en ESE controller.");
        }
#endif

        // Animación de inicio (one-shot)
        if (!string.IsNullOrEmpty(beginState) && animator != null && animator.HasState(layer, Animator.StringToHash(beginState)))
        {
            CrossFadeToState(beginState, 0.2f, layer);
            float clipLen = GetClipLength(beginState);
            yield return new WaitForSeconds(Mathf.Max(0.1f, clipLen));
        }

        // Loop hasta que se interrumpa externamente
        if (!string.IsNullOrEmpty(loopState) && animator != null && animator.HasState(layer, Animator.StringToHash(loopState)))
        {
            CrossFadeToState(loopState, 0.15f, layer);
        }

        _activityCoroutine = null;
    }

    /// <summary>
    /// Capa del Animator en la que vive cada actividad ambiental. Comer y beber viven en la
    /// UpperBody layer (torso/brazos, AvatarMask sin piernas) para que el NPC pueda seguir de pie
    /// o caminando con su locomoción normal de la Base Layer mientras el gesto se reproduce
    /// encima; el resto de actividades (sentarse, dormir) siguen siendo poses de cuerpo completo
    /// en la Base Layer (capa 0).
    /// </summary>
    private int GetActivityLayer(NPCAmbientActivity activity)
    {
        return activity switch
        {
            NPCAmbientActivity.Eat   => upperBodyLayer,
            NPCAmbientActivity.Drink => upperBodyLayer,
            _                        => 0
        };
    }

    private static string GetActivityBeginState(NPCAmbientActivity activity)
    {
        return activity switch
        {
            // Sentarse — pendiente de importar los clips (nombres reservados)
            NPCAmbientActivity.SitGround  => "SitGround_Begin",
            NPCAmbientActivity.SitLow     => "SitLow_Begin",
            NPCAmbientActivity.SitMedium  => "SitMedium_Begin",
            NPCAmbientActivity.SitHigh    => "SitHigh_Begin",
            // Comer — pendiente de importar los clips
            NPCAmbientActivity.Eat        => "Eat_Begin",
            // Beber: usa DrinkPotion_NoWeapon como one-shot y luego idle
            NPCAmbientActivity.Drink      => "DrinkPotion_NoWeapon",
            // Dormir: sin transición de entrada, va directo al loop
            NPCAmbientActivity.Sleep      => string.Empty,
            _                             => string.Empty
        };
    }

    private static string GetActivityLoopState(NPCAmbientActivity activity)
    {
        return activity switch
        {
            // Sentarse — pendiente de importar los clips
            NPCAmbientActivity.SitGround  => "SitGround_Loop",
            NPCAmbientActivity.SitLow     => "SitLow_Loop",
            NPCAmbientActivity.SitMedium  => "SitMedium_Loop",
            NPCAmbientActivity.SitHigh    => "SitHigh_Loop",
            // Comer — pendiente
            NPCAmbientActivity.Eat        => "Eat_Loop",
            // Beber: el one-shot ya es la animación completa, sin loop
            NPCAmbientActivity.Drink      => string.Empty,
            // Dormir: loop real del personaje durmiendo
            NPCAmbientActivity.Sleep      => "Sleeping_NoWeapon",
            _                             => string.Empty
        };
    }

    private static string GetActivityExitState(NPCAmbientActivity activity)
    {
        return activity switch
        {
            // Sentarse — pendiente de importar los clips
            NPCAmbientActivity.SitGround  => "SitGround_Exit",
            NPCAmbientActivity.SitLow     => "SitLow_Exit",
            NPCAmbientActivity.SitMedium  => "SitMedium_Exit",
            NPCAmbientActivity.SitHigh    => "SitHigh_Exit",
            // Dormir, comer y beber: sin animación de salida, vuelven directo a idle
            _                             => string.Empty
        };
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
    
    #region Private Methods - Idle Variations

    private void StartIdleVariations()
    {
        // FIX 4 sep 2026 (petición de Raúl: "vamos a quitar que no cambie de idle porque no me
        // gustan las animaciones, que se quede con el idle normal"): se desactiva la
        // funcionalidad entera aquí, en vez de solo cambiar el valor por defecto de
        // enableIdleVariations — ese campo ya viene serializado a `true` en el Inspector de
        // muchos NPCs existentes, así que cambiar el default en el script no les habría afectado.
        // Con este return incondicional NINGÚN NPC vuelve a cambiar de idle, sea cual sea el
        // valor guardado en su prefab/instancia; se deja el resto del sistema (campos, coroutine)
        // intacto por si se quiere reactivar más adelante.
        return;

#pragma warning disable CS0162 // código inalcanzable a propósito, ver comentario arriba
        if (!enableIdleVariations || idleVariationStates == null || idleVariationStates.Length == 0)
            return;
        if (_isInBattle || _isInteracting || _currentState == AnimationState.Dead)
            return;

        StopIdleVariations();
        _idleVariationCoroutine = StartCoroutine(IdleVariationLoop());
#pragma warning restore CS0162
    }

    private void StopIdleVariations()
    {
        if (_idleVariationCoroutine == null)
            return;

        StopCoroutine(_idleVariationCoroutine);
        _idleVariationCoroutine = null;
    }

    private IEnumerator IdleVariationLoop()
    {
        while (true)
        {
            float wait = UnityEngine.Random.Range(minIdleVariationInterval, maxIdleVariationInterval);
            yield return new WaitForSeconds(wait);

            // Guardas: solo si seguimos en Idle puro
            if (_currentState != AnimationState.Idle || _isInBattle || _isInteracting)
                continue;

            if (idleVariationStates.Length == 0)
                continue;

            string variation = idleVariationStates[UnityEngine.Random.Range(0, idleVariationStates.Length)];

            if (string.IsNullOrEmpty(variation) || animator == null || !animator.HasState(0, Animator.StringToHash(variation)))
                continue;

            PlayOneShot(variation, 0, () =>
            {
                if (_currentState != AnimationState.Dead && !_isInBattle && !_isInteracting)
                    CrossFadeToState(idleNormalState, 0.2f);
            });
        }
    }

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
        
        // ✅ FIX CRÍTICO: Solo sincronizar si el agente tiene un path activo y no está detenido
        // Esto previene que velocidad residual cause movimiento constante
        if (navAgent.isStopped || !navAgent.hasPath)
        {
            // Si está detenido, asegurar que la animación también esté en 0
            SetMovementSpeed(0f);
            return;
        }
        
        // Get agent velocity
        float agentSpeed = navAgent.velocity.magnitude;

        // ✅ Threshold más estricto: Si la velocidad es muy baja, considerarlo como parado
        if (agentSpeed < movementThreshold * 0.5f)
        {
            SetMovementSpeed(0f);
            return;
        }

        // Mapa de dos segmentos para que el blend tree reciba valores correctos:
        //   0 m/s        → 0.0  (idle)
        //   walkSpeed    → 0.5  (zona walk del blend tree)
        //   runSpeed     → 1.0  (zona run del blend tree)
        float normalizedSpeed;
        if (agentSpeed <= _walkSpeed)
            normalizedSpeed = (agentSpeed / _walkSpeed) * 0.5f;
        else
            normalizedSpeed = 0.5f + ((agentSpeed - _walkSpeed) / (_runSpeed - _walkSpeed)) * 0.5f;
        normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
        
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

        // ✅ FIX: si el NPC sigue sentado (p.ej. este TransitionToIdle llega desde el callback
        // de PlayOneShot al terminar un PlaySocialGesture lanzado mientras estaba en la mesa),
        // volver al loop de sentado en vez de ponerlo de pie con idleNormalState.
        if (_isSeated && !_isInBattle)
        {
            string seatLoop = GetActivityLoopState(_seatedActivity);
            if (!string.IsNullOrEmpty(seatLoop) && animator != null && animator.HasState(0, Animator.StringToHash(seatLoop)))
            {
                CrossFadeToState(seatLoop, 0.2f);
                return; // no arrancar variaciones de idle de pie mientras sigue sentado
            }
        }

        // Elegir el idle correcto según el modo (batalla o normal)
        string targetIdle = _isInBattle ? idleBattleState : idleNormalState;

        // ✅ Seguridad: Si el estado no existe, no intentar CrossFade para evitar errores en consola
        if (!string.IsNullOrEmpty(targetIdle) && animator != null && animator.HasState(0, Animator.StringToHash(targetIdle)))
        {
            CrossFadeToState(targetIdle, 0.2f);
        }

        StartIdleVariations();
    }
    
    /// <summary>
    /// Fuerza transición a estado de locomotion (caminar/correr).
    /// Útil cuando necesitas mover el NPC inmediatamente desde dizzy u otro estado.
    /// </summary>
    public void TransitionToLocomotion()
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
            // Solo loguear si no es un string vacío intencional
            if (!string.IsNullOrEmpty(stateName) && debugMode)
                Debug.LogWarning($"[NPCAnimator] CrossFadeToState falló - stateName: {stateName}, animator: {animator != null}");
            return;
        }
        
        int stateHash = Animator.StringToHash(stateName);
        
        // Check if state exists in specified layer
        if (animator.HasState(layer, stateHash))
        {
            animator.CrossFadeInFixedTime(stateHash, transitionTime, layer, 0f);
        }
        else
        {
            // ✅ Cambiado a Warning para no ensuciar la consola si un NPC específico no tiene una animación opcional
            if (debugMode)
                Debug.LogWarning($"[NPCAnimator] ⚠️ Estado '{stateName}' no encontrado en layer {layer} para {gameObject.name}.");
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
