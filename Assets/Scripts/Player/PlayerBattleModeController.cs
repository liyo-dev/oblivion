using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

namespace Game.Player
{
    /// <summary>
    /// Gestiona el estado de batalla del jugador (animaciones idle)
    /// Detecta cuando hay NPCs enemigos cerca y activa Battle Idle
    /// </summary>
    public class PlayerBattleModeController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator animator;
        [SerializeField] private vThirdPersonController controller;
        [SerializeField] private Rigidbody playerRigidbody;
        
        [Header("Configuración")]
        [Tooltip("Nombre del estado Battle Idle en el Animator del player")]
        [SerializeField] private string battleIdleStateName = "Idle_Battle_NoWeapon";
        
        [Tooltip("Nombre del estado Normal Idle en el Animator del player")]
        [SerializeField] private string normalIdleStateName = "Idle_Normal_NoWeapon";
        
        [Tooltip("Nombre del estado de Victoria en el Animator del player")]
        [SerializeField] private string victoryStateName = "Victory_NoWeapon";
        
        [Tooltip("Radio de detección de enemigos para activar Battle Mode")]
        [SerializeField] private float enemyDetectionRadius = 15f;
        
        [Tooltip("Layer de enemigos (Enemy)")]
        [SerializeField] private LayerMask enemyLayer = ~0;
        
        [Tooltip("Tiempo sin enemigos cerca para desactivar Battle Mode")]
        [SerializeField] private float exitBattleDelay = 3f;
        
        [Tooltip("Umbral de velocidad para considerar al player quieto")]
        [SerializeField] private float idleSpeedThreshold = 0.1f;
        
        [Tooltip("Duración de la animación de victoria en segundos")]
        [SerializeField] private float victoryAnimationDuration = 3f;
        
        [Header("Audio")]
        [Tooltip("Clave del evento de audio para victoria (configurado en AudioGraphProfile)")]
        [SerializeField] private string victorySfxKey = "Player_Victory";
        
        [Header("Debug")]
        [SerializeField] private bool debugMode;
        
        private bool _isInBattleMode;
        private bool _isPlayingVictory;
        private float _timeSinceLastEnemyDetected;
        private float _timeSinceStoppedMoving; // Tiempo desde que el jugador dejó de moverse
        private bool _wasMovingLastFrame; // Para detectar cambio de movimiento a idle
        private int _battleIdleHash;
        private int _normalIdleHash;
        private int _victoryHash;
        
        // Colliders buffer para OverlapSphereNonAlloc (evitar allocation)
        private readonly Collider[] _hitColliders = new Collider[20];
        
        void Awake()
        {
            // Auto-encontrar referencias
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            
            if (controller == null)
                controller = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
            
            if (playerRigidbody == null)
                playerRigidbody = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();
            
            // Cachear hashes de estados
            _battleIdleHash = Animator.StringToHash(battleIdleStateName);
            _normalIdleHash = Animator.StringToHash(normalIdleStateName);
            _victoryHash = Animator.StringToHash(victoryStateName);
            
            // Inicializar variables de estado
            _wasMovingLastFrame = false;
            _timeSinceStoppedMoving = 0f;
        }
        
        void OnEnable()
        {
            // Suscribirse al evento de victoria de batalla
            if (DefaultNarrativeSignals.Instance != null)
            {
                DefaultNarrativeSignals.Instance.OnBattleWon("*", OnBattleVictory);
            }
        }
        
        void OnDisable()
        {
            // Desuscribirse del evento
            if (DefaultNarrativeSignals.Instance != null)
            {
                DefaultNarrativeSignals.Instance.OffBattleWon("*", OnBattleVictory);
            }
        }
        
        /// <summary>
        /// Callback cuando se gana una batalla
        /// </summary>
        void OnBattleVictory()
        {
            if (_isPlayingVictory) return;
            
            // Solo reproducir victoria si el jugador está en batalla
            if (_isInBattleMode)
            {
                StartCoroutine(PlayVictorySequence());
            }
        }
        
        void Update()
        {
            if (animator == null) return;
            
            // ✅ No hacer nada si está reproduciendo victoria
            if (_isPlayingVictory) return;
            
            // Detectar si el player se está moviendo
            bool isMoving = !IsPlayerIdle();
            
            if (debugMode && isMoving)
                Debug.Log($"[PlayerBattleMode] 🏃 Jugador moviéndose - velocidad: {(playerRigidbody != null ? playerRigidbody.linearVelocity.magnitude : 0f)}");
            
            // Detectar enemigos cercanos
            bool enemiesNearby = DetectEnemiesNearby();
            
            if (enemiesNearby)
            {
                _timeSinceLastEnemyDetected = 0f;
                
                // Activar Battle Mode si no está activo
                if (!_isInBattleMode)
                {
                    EnterBattleMode();
                }
                
                // Detectar transición de movimiento a idle
                if (isMoving)
                {
                    _timeSinceStoppedMoving = 0f;
                    _wasMovingLastFrame = true;
                }
                else
                {
                    // El jugador está quieto
                    if (_wasMovingLastFrame)
                    {
                        // Acaba de dejar de moverse
                        _timeSinceStoppedMoving = 0f;
                        _wasMovingLastFrame = false;
                        
                        if (debugMode)
                            Debug.Log($"[PlayerBattleMode] ⏸️ Jugador detuvo movimiento");
                    }
                    else
                    {
                        // Sigue quieto
                        _timeSinceStoppedMoving += Time.deltaTime;
                    }
                    
                    // Solo forzar Battle Idle después de un pequeño delay (0.3s)
                    // Esto permite que Invector complete las transiciones de desaceleración
                    if (_timeSinceStoppedMoving >= 0.3f)
                    {
                        EnsureBattleIdle();
                    }
                }
            }
            else
            {
                // Incrementar tiempo sin enemigos
                _timeSinceLastEnemyDetected += Time.deltaTime;
                
                // Salir de Battle Mode después del delay
                if (_isInBattleMode && _timeSinceLastEnemyDetected >= exitBattleDelay)
                {
                    ExitBattleMode();
                }
            }
        }
        
        /// <summary>
        /// Detecta si hay enemigos cerca
        /// </summary>
        bool DetectEnemiesNearby()
        {
            // Usar NonAlloc para evitar allocations
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, enemyDetectionRadius, _hitColliders, enemyLayer);
            
            for (int i = 0; i < hitCount; i++)
            {
                var hitCollider = _hitColliders[i];
                
                // Verificar si es un NPC enemigo en combate
                var npcManager = hitCollider.GetComponent<NPC.NPCBehaviourManagerV2>();
                if (npcManager != null)
                {
                    // ✅ Acceder al Brain a través de la propiedad pública
                    var brain = npcManager.Brain;
                    if (brain != null && brain.CurrentState != null)
                    {
                        string stateName = brain.CurrentState.GetType().Name;
                        if (stateName == "CombatState")
                        {
                            if (debugMode)
                                Debug.Log($"[PlayerBattleMode] Enemigo detectado: {npcManager.name}");
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Verifica si el player está quieto (idle)
        /// </summary>
        bool IsPlayerIdle()
        {
            // Usar velocity del Rigidbody
            if (playerRigidbody != null)
            {
                // Unity 2023+ usa linearVelocity en lugar de velocity
                Vector3 velocity = playerRigidbody.linearVelocity;
                velocity.y = 0f; // Ignorar velocidad vertical
                float sqrSpeed = velocity.sqrMagnitude;
                return sqrSpeed < (idleSpeedThreshold * idleSpeedThreshold);
            }
            
            // Si no hay Rigidbody, asumir que está quieto si no hay controller
            if (debugMode)
                Debug.LogWarning("[PlayerBattleMode] ⚠️ No se encontró Rigidbody - no se puede detectar si está quieto");
            
            return false;
        }
        
        /// <summary>
        /// Asegura que el player esté en Battle Idle (sin spam)
        /// Solo hace la transición si está en Idle normal, NO desde animaciones de locomoción
        /// </summary>
        void EnsureBattleIdle()
        {
            if (!_isInBattleMode) return;
            
            // Verificar el estado actual del animator
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            
            // Solo cambiar si NO está ya en Battle Idle
            if (currentState.shortNameHash != _battleIdleHash)
            {
                // ✅ CLAVE: Solo hacer transición si está en Idle normal
                // Esto evita interrumpir animaciones de locomoción que Invector está reproduciendo
                if (currentState.shortNameHash == _normalIdleHash)
                {
                    // Verificar si el estado existe antes de intentar cambiar
                    if (animator.HasState(0, _battleIdleHash))
                    {
                        animator.CrossFadeInFixedTime(_battleIdleHash, 0.2f, 0);
                        
                        if (debugMode)
                            Debug.Log($"[PlayerBattleMode] ✅ Cambiado a Battle Idle desde Idle normal");
                    }
                    else if (debugMode)
                    {
                        Debug.LogWarning($"[PlayerBattleMode] ⚠️ Estado '{battleIdleStateName}' no encontrado en Animator");
                    }
                }
                else if (debugMode)
                {
                    // No está en Idle normal, esperar
                    string currentStateName = currentState.IsName(normalIdleStateName) ? normalIdleStateName : "Otro";
                    Debug.Log($"[PlayerBattleMode] ⏳ Esperando a Idle normal (actual: {currentStateName})");
                }
            }
        }
        
        /// <summary>
        /// Secuencia de victoria con animación y música
        /// </summary>
        IEnumerator PlayVictorySequence()
        {
            _isPlayingVictory = true;
            
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🎉 Iniciando animación de victoria");
            
            // Deshabilitar control del jugador temporalmente usando campos públicos de Invector
            if (controller != null)
            {
                controller.enabled = false; // Deshabilitar completamente el controlador
            }
            
            // Reproducir animación de victoria
            if (animator.HasState(0, _victoryHash))
            {
                animator.CrossFadeInFixedTime(_victoryHash, 0.2f, 0);
                
                if (debugMode)
                    Debug.Log($"[PlayerBattleMode] 🎬 Reproduciendo animación de victoria");
            }
            else if (debugMode)
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Estado '{victoryStateName}' no encontrado en Animator");
            }
            
            // Reproducir SFX de victoria usando el sistema de audio centralizado
            if (!string.IsNullOrEmpty(victorySfxKey) && AudioService.Instance != null)
            {
                AudioService.Instance.PlaySFX(victorySfxKey, volume: 1f);
                
                if (debugMode)
                    Debug.Log($"[PlayerBattleMode] 🎵 Reproduciendo SFX de victoria: {victorySfxKey}");
            }
            
            // Esperar duración de la animación
            yield return new WaitForSeconds(victoryAnimationDuration);
            
            // Re-habilitar control del jugador
            if (controller != null)
            {
                controller.enabled = true; // Re-habilitar completamente el controlador
            }
            
            // Volver a idle normal
            if (animator.HasState(0, _normalIdleHash))
            {
                animator.CrossFadeInFixedTime(_normalIdleHash, 0.3f, 0);
            }
            
            _isPlayingVictory = false;
            
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] ✅ Secuencia de victoria completada");
        }
        
        /// <summary>
        /// Entra en modo batalla
        /// </summary>
        void EnterBattleMode()
        {
            _isInBattleMode = true;
            
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🗡️ ENTRANDO en Battle Mode");
        }
        
        /// <summary>
        /// Sale del modo batalla
        /// </summary>
        void ExitBattleMode()
        {
            _isInBattleMode = false;
            
            // Volver a idle normal si está quieto
            if (IsPlayerIdle())
            {
                AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
                
                if (currentState.shortNameHash != _normalIdleHash)
                {
                    if (animator.HasState(0, _normalIdleHash))
                    {
                        animator.CrossFadeInFixedTime(_normalIdleHash, 0.3f, 0);
                    }
                }
            }
            
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🏡 SALIENDO de Battle Mode");
        }
        
        // Debug Gizmos
        void OnDrawGizmosSelected()
        {
            if (!debugMode) return;
            
            Gizmos.color = _isInBattleMode ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, enemyDetectionRadius);
        }
    }
}

