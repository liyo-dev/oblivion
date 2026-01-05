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
        [SerializeField] private string victorySfxKey = "Npc_Battle_Victory";
        
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
        
        // ⭐ Flag para suprimir temporalmente Battle Idle (tras diálogo de combate)
        private bool _suppressBattleIdle;
        
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
            // Ya no nos suscribimos a eventos narrativos
            // El NPCCombatLifecycleHandler llamará directamente a PlayVictory()
        }
        
        void OnDisable()
        {
            // Nada que limpiar
        }
        
        // Guarda el battleId del último combate para reproducir la música correcta
        private string _currentBattleId;
        
        /// <summary>
        /// Método público para que el NPC llame cuando el player gana
        /// </summary>
        /// <param name="battleId">ID del combate para restaurar la música después de la victoria</param>
        public void PlayVictory(string battleId = null)
        {
            _currentBattleId = battleId;
            
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🎯 PlayVictory() LLAMADO - _isPlayingVictory: {_isPlayingVictory}, battleId: {battleId}");
            
            if (_isPlayingVictory)
            {
                if (debugMode)
                    Debug.Log($"[PlayerBattleMode] ⚠️ Victoria ya en reproducción - ignorando");
                return;
            }
            
            StartCoroutine(PlayVictorySequence());
        }
        
        /// <summary>
        /// Suprime temporalmente el Battle Idle para permitir que el jugador se mueva libremente
        /// tras un diálogo de combate. Se desactiva automáticamente cuando el jugador empieza a moverse.
        /// ⭐ DESACTIVADO - Ya no se usa Battle Idle
        /// </summary>
        public void SuppressBattleIdle()
        {
            // No hace nada - Battle Idle desactivado
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] SuppressBattleIdle() llamado pero Battle Idle está desactivado");
        }
        
        void Update()
        {
            // ⭐ SISTEMA DE BATTLE IDLE DESACTIVADO
            // El player usará siempre su idle normal, sin forzar animaciones de batalla
            // Solo se mantiene la detección de enemigos para tracking interno si se necesita en el futuro
            
            if (animator == null) return;
            
            // ✅ No hacer nada si está reproduciendo victoria
            if (_isPlayingVictory) return;
            
            // // Detectar enemigos cercanos (comentado - no se usa actualmente)
            // bool enemiesNearby = DetectEnemiesNearby();
            // if (enemiesNearby)
            // {
            //     _timeSinceLastEnemyDetected = 0f;
            // }
            // else
            // {
            //     _timeSinceLastEnemyDetected += Time.deltaTime;
            // }
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
            
            // ⭐ Respetar flag de supresión (tras diálogo de combate)
            if (_suppressBattleIdle) return;
            
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
                        // ✅ IMPORTANTE: Usar CrossFade con tiempo corto para permitir override rápido
                        // Si el player empieza a moverse, Invector podrá interrumpir esta transición
                        animator.CrossFade(_battleIdleHash, 0.15f, 0);
                        
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
                    string currentStateName = "Locomoción u Otro";
                    Debug.Log($"[PlayerBattleMode] ⏳ Esperando a Idle normal (actual: {currentStateName})");
                }
            }
        }
        
        /// <summary>
        /// Libera el Animator de Battle Idle para permitir que Invector controle la locomoción
        /// </summary>
        void ReleaseFromBattleIdle()
        {
            if (!_isInBattleMode) return;
            
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            
            // Si estamos en Battle Idle, NO hacer nada especial
            // Invector automáticamente hará la transición a Walk/Run basándose en InputMagnitude
            // Solo necesitamos NO forzar más el Battle Idle mientras se mueve
            
            // Opcional: Puedes forzar una transición a un estado base de locomoción si Invector no responde
            // animator.CrossFade("Locomotion", 0.1f, 0);
            
            if (debugMode && currentState.shortNameHash == _battleIdleHash)
            {
                Debug.Log($"[PlayerBattleMode] 🔓 Liberando de Battle Idle - Invector tomará control");
            }
        }
        
        /// <summary>
        /// Secuencia de victoria con animación y música
        /// </summary>
        IEnumerator PlayVictorySequence()
        {
            _isPlayingVictory = true;
            
            Debug.Log($"[PlayerBattleMode] 🎉 ✅ INICIANDO ANIMACIÓN DE VICTORIA");
            
            // Deshabilitar control del jugador temporalmente usando campos públicos de Invector
            if (controller != null)
            {
                controller.enabled = false; // Deshabilitar completamente el controlador
                Debug.Log($"[PlayerBattleMode] 🎮 Controlador del jugador deshabilitado");
            }
            else
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Controller es NULL - no se puede deshabilitar");
            }
            
            // Reproducir animación de victoria
            if (animator != null)
            {
                if (animator.HasState(0, _victoryHash))
                {
                    animator.CrossFadeInFixedTime(_victoryHash, 0.2f, 0);
                    Debug.Log($"[PlayerBattleMode] 🎬 ✅ Reproduciendo animación de victoria: {victoryStateName}");
                }
                else
                {
                    Debug.LogWarning($"[PlayerBattleMode] ⚠️ Estado '{victoryStateName}' NO encontrado en Animator");
                }
            }
            else
            {
                Debug.LogError($"[PlayerBattleMode] ❌ Animator es NULL");
            }
            
            // Reproducir música de victoria usando el sistema de audio centralizado
            if (!string.IsNullOrEmpty(victorySfxKey) && AudioService.Instance != null)
            {
                // Usar PlayVictoryForBattle para reproducir la música de victoria correctamente
                // El primer parámetro es el battleId actual (para restaurar música después)
                // El segundo es la clave de victoria
                // El tercer parámetro es el tiempo que se mantiene la música de victoria
                if (!string.IsNullOrEmpty(_currentBattleId))
                {
                    AudioService.Instance.PlayVictoryForBattle(_currentBattleId, victorySfxKey, victoryAnimationDuration + 2f);
                    Debug.Log($"[PlayerBattleMode] 🎵 ✅ Reproduciendo música de victoria: {victorySfxKey} (battleId: {_currentBattleId})");
                }
                else
                {
                    // Sin battleId específico, solo reproducir el SFX de victoria sin restaurar música después
                    AudioService.Instance.PlaySFX(victorySfxKey);
                    Debug.Log($"[PlayerBattleMode] 🎵 Reproduciendo SFX de victoria: {victorySfxKey} (sin battleId)");
                }
            }
            else if (string.IsNullOrEmpty(victorySfxKey))
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ victorySfxKey está vacío - no se reproduce audio");
            }
            else
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ AudioService.Instance es NULL - no se puede reproducir música");
            }
            
            // Esperar duración de la animación
            Debug.Log($"[PlayerBattleMode] ⏱️ Esperando {victoryAnimationDuration}s (duración de animación de victoria)");
            yield return new WaitForSeconds(victoryAnimationDuration);
            
            Debug.Log($"[PlayerBattleMode] 🔄 Terminando animación de victoria - restaurando control del jugador");
            
            // IMPORTANTE: Resetear el flag ANTES de re-habilitar el control
            // Esto permite que el Update() vuelva a funcionar normalmente
            _isPlayingVictory = false;
            
            // Volver a idle normal ANTES de re-habilitar el controlador
            // Esto asegura que el animator esté en un estado válido
            if (animator != null)
            {
                if (animator.HasState(0, _normalIdleHash))
                {
                    // Usar Play() en lugar de CrossFade para forzar la transición inmediata
                    animator.Play(_normalIdleHash, 0, 0f);
                    Debug.Log($"[PlayerBattleMode] 🔄 FORZANDO transición a Idle Normal: {normalIdleStateName}");
                }
                else
                {
                    Debug.LogWarning($"[PlayerBattleMode] ⚠️ Estado '{normalIdleStateName}' no encontrado en Animator");
                }
                
                // Resetear cualquier parámetro del animator que pueda estar bloqueando transiciones
                // (esto depende de tu setup, ajusta según sea necesario)
                // animator.SetFloat("InputMagnitude", 0f);
                // animator.SetBool("IsGrounded", true);
            }
            
            // Re-habilitar control del jugador
            if (controller != null)
            {
                controller.enabled = true; // Re-habilitar completamente el controlador
                Debug.Log($"[PlayerBattleMode] 🎮 Controlador del jugador RE-HABILITADO");
            }
            else
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Controller es NULL - no se pudo re-habilitar");
            }
            
            // Pequeña espera para asegurar que todo se estabilice
            yield return null;
            
            Debug.Log($"[PlayerBattleMode] ✅ Secuencia de victoria COMPLETADA - jugador debe estar en idle normal");
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

