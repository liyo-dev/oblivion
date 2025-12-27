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
        [SerializeField] private string battleIdleStateName = "Idle_Battle";
        
        [Tooltip("Nombre del estado Normal Idle en el Animator del player")]
        [SerializeField] private string normalIdleStateName = "Idle";
        
        [Tooltip("Nombre del estado de Victoria en el Animator del player")]
        [SerializeField] private string victoryStateName = "Victory";
        
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
        [Tooltip("AudioSource para reproducir música de victoria (debe estar configurado en el Inspector)")]
        [SerializeField] private AudioSource victoryAudioSource;
        
        [Tooltip("Clip de audio de música de victoria (opcional)")]
        [SerializeField] private AudioClip victoryMusicClip;
        
        [Tooltip("Volumen de la música de victoria")]
        [Range(0f, 1f)]
        [SerializeField] private float victoryMusicVolume = 0.7f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode;
        
        private bool _isInBattleMode;
        private bool _isPlayingVictory;
        private float _timeSinceLastEnemyDetected;
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
                
                // ✅ Si está quieto Y en Battle Mode, asegurar Battle Idle
                // ✅ Si se mueve, NO hacer nada - Invector maneja la locomoción
                if (!isMoving)
                {
                    EnsureBattleIdle();
                }
                // Si se mueve, no hacer nada - dejar que Invector maneje las animaciones
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
        /// </summary>
        void EnsureBattleIdle()
        {
            if (!_isInBattleMode) return;
            
            // Verificar el estado actual del animator
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            
            // Solo cambiar si NO está ya en Battle Idle
            if (currentState.shortNameHash != _battleIdleHash)
            {
                // Verificar si el estado existe antes de intentar cambiar
                if (animator.HasState(0, _battleIdleHash))
                {
                    animator.CrossFadeInFixedTime(_battleIdleHash, 0.2f, 0);
                    
                    if (debugMode)
                        Debug.Log($"[PlayerBattleMode] ✅ Cambiado a Battle Idle");
                }
                else if (debugMode)
                {
                    Debug.LogWarning($"[PlayerBattleMode] ⚠️ Estado '{battleIdleStateName}' no encontrado en Animator");
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
            
            // Reproducir música de victoria si está configurada
            if (victoryMusicClip != null)
            {
                if (victoryAudioSource != null)
                {
                    victoryAudioSource.clip = victoryMusicClip;
                    victoryAudioSource.volume = victoryMusicVolume;
                    victoryAudioSource.Play();
                    
                    if (debugMode)
                        Debug.Log($"[PlayerBattleMode] 🎵 Reproduciendo música de victoria");
                }
                else
                {
                    Debug.LogWarning($"[PlayerBattleMode] ⚠️ victoryAudioSource no está asignado en el Inspector - no se reproducirá música de victoria");
                }
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

