using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

namespace Game.Player
{
    /// <summary>
    /// Gestiona el estado de batalla del jugador.
    /// Detecta cuando hay NPCs enemigos cerca y activa la pose de batalla en la parte superior del cuerpo.
    /// Usa Layer 1 (UpperBody) con Avatar Mask para que los brazos estén en pose de combate
    /// mientras las piernas siguen la locomoción normal.
    /// </summary>
    public class PlayerBattleModeController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator animator;
        [SerializeField] private vThirdPersonController controller;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private PlayerActionManager actionManager;
        
        [Header("Configuración de Capas del Animator")]
        [Tooltip("Índice de la capa UpperBody en el Animator (normalmente 1)")]
        [SerializeField] private int upperBodyLayerIndex = 1;
        
        [Tooltip("Nombre del estado Battle Idle en la capa UpperBody")]
        [SerializeField] private string battleIdleStateName = "Idle_Battle_NoWeapon";
        
        [Tooltip("Path completo del estado Battle Idle (ej: UpperBody.Idle_Battle_NoWeapon). Dejar vacío para usar solo el nombre.")]
        [SerializeField] private string battleIdleFullPath = "UpperBody.Idle_Battle_NoWeapon";
        
        [Tooltip("Nombre del estado de Victoria en el Animator del player")]
        [SerializeField] private string victoryStateName = "Victory_NoWeapon";
        
        [Header("Detección de Combate")]
        [Tooltip("Radio de detección de enemigos para activar Battle Mode")]
        [SerializeField] private float enemyDetectionRadius = 15f;
        
        [Tooltip("Layer de enemigos (Enemy)")]
        [SerializeField] private LayerMask enemyLayer = ~0;
        
        [Tooltip("Tiempo sin enemigos cerca para desactivar Battle Mode")]
        [SerializeField] private float exitBattleDelay = 3f;
        
        [Header("Transiciones")]
        [Tooltip("Duración del fade para activar/desactivar la capa UpperBody")]
        [SerializeField] private float layerFadeDuration = 0.3f;
        
        [Tooltip("Duración de la animación de victoria en segundos")]
        [SerializeField] private float victoryAnimationDuration = 3f;
        
        [Header("Audio")]
        [Tooltip("Clave del evento de audio para victoria (configurado en AudioGraphProfile)")]
        [SerializeField] private string victorySfxKey = "Npc_Battle_Victory";
        
#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private bool debugMode;
#endif
        
        private bool _isInBattleMode;
        private bool _isPlayingVictory;
        private float _timeSinceLastEnemyDetected;
        private int _battleIdleHash;
        private int _victoryHash;
        
        // Estado de la capa
        private float _currentLayerWeight;
        private float _targetLayerWeight;
        
        // Colliders buffer para OverlapSphereNonAlloc (evitar allocation)
        private readonly Collider[] _hitColliders = new Collider[20];
        
        /// <summary>
        /// Indica si actualmente se está reproduciendo la secuencia de victoria
        /// </summary>
        public bool IsPlayingVictory => _isPlayingVictory;
        
        void Awake()
        {
            // Auto-encontrar referencias
            if (animator == null)
                animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            
            if (controller == null)
                controller = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
            
            if (playerRigidbody == null)
                playerRigidbody = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();

            if (actionManager == null)
                actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();
            
            // Cachear hashes de estados
            _battleIdleHash = Animator.StringToHash(battleIdleStateName);
            _victoryHash = Animator.StringToHash(victoryStateName);
            
            // Asegurar que la capa empieza desactivada
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                animator.SetLayerWeight(upperBodyLayerIndex, 0f);
            }
            
            _currentLayerWeight = 0f;
            _targetLayerWeight = 0f;
        }
        
        void OnEnable()
        {
            // El NPCCombatLifecycleHandler llamará directamente a PlayVictory()
            
            // Suscribirse al evento de fin de animación de magia para restaurar battle idle
            if (controller != null)
            {
                controller.OnMagicCastAnimationEnded += OnMagicAnimationEnded;
            }
        }
        
        void OnDisable()
        {
            // Desactivar la capa al deshabilitarse
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                animator.SetLayerWeight(upperBodyLayerIndex, 0f);
            }

            if (_isInBattleMode && actionManager != null)
            {
                actionManager.PopMode(ActionMode.Combat);
            }
            _isInBattleMode = false;
            _targetLayerWeight = 0f;
            _currentLayerWeight = 0f;
            
            // Desuscribirse del evento
            if (controller != null)
            {
                controller.OnMagicCastAnimationEnded -= OnMagicAnimationEnded;
            }
        }
        
        /// <summary>
        /// Callback cuando termina una animación de magia.
        /// Si estamos en modo batalla, restauramos el battle idle.
        /// </summary>
        private void OnMagicAnimationEnded()
        {
            if (_isInBattleMode && !_isPlayingVictory)
            {
                RestoreBattleIdle();
            }
        }
        
        /// <summary>
        /// Restaura el battle idle en la capa UpperBody
        /// </summary>
        private void RestoreBattleIdle()
        {
            if (animator == null || animator.layerCount <= upperBodyLayerIndex) return;
            
            // IMPORTANTE: Sincronizar _currentLayerWeight con el valor real del animator
            // ya que vThirdPersonController puede haber modificado el peso directamente
            _currentLayerWeight = animator.GetLayerWeight(upperBodyLayerIndex);
            
            // Establecer el objetivo para que la transición suave funcione
            _targetLayerWeight = 1f;
            
            if (animator.HasState(upperBodyLayerIndex, _battleIdleHash))
            {
                // Usar el full path si está definido, sino el nombre simple
                string statePath = !string.IsNullOrEmpty(battleIdleFullPath) ? battleIdleFullPath : battleIdleStateName;
                
                // Forzar la animación de battle idle
                animator.CrossFadeInFixedTime(statePath, 0.2f, upperBodyLayerIndex);
                
#if UNITY_EDITOR
                if (debugMode)
                    Debug.Log($"[PlayerBattleMode] 🗡️ Battle Idle RESTAURADO después de animación de magia (weight actual: {_currentLayerWeight:F2} → 1.0)");
#endif
            }
        }
        
        // Guarda el battleId del último combate para reproducir la música correcta
        private string _currentBattleId;
        
        /// <summary>
        /// Método público para que el NPC llame cuando el player gana
        /// </summary>
        /// <param name="battleId">ID del combate para restaurar la música después de la victoria</param>
        public void PlayVictory(string battleId = null)
        {
            Debug.Log($"[PlayerBattleMode] 🎯 PlayVictory() LLAMADO - _isPlayingVictory: {_isPlayingVictory}, battleId: {battleId ?? "null"}");
            
            if (_isPlayingVictory)
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Victoria ya en reproducción - ignorando llamada duplicada (battleId: {battleId ?? "null"})");
                return;
            }
            
            _currentBattleId = battleId;
            StartCoroutine(PlayVictorySequence());
        }
        
        /// <summary>
        /// Suprime temporalmente el Battle Mode (tras diálogos de combate, etc.)
        /// </summary>
        public void SuppressBattleMode(float duration = 2f)
        {
            StartCoroutine(SuppressBattleModeRoutine(duration));
        }
        
        private IEnumerator SuppressBattleModeRoutine(float duration)
        {
            _targetLayerWeight = 0f;
            yield return new WaitForSeconds(duration);
        }
        
        void Update()
        {
            if (animator == null) return;
            
            // No hacer nada si está reproduciendo victoria
            if (_isPlayingVictory) return;
            
            // Detectar enemigos cercanos
            bool enemiesNearby = DetectEnemiesNearby();
            
            if (enemiesNearby)
            {
                _timeSinceLastEnemyDetected = 0f;
                
                if (!_isInBattleMode)
                {
                    EnterBattleMode();
                }
            }
            else
            {
                _timeSinceLastEnemyDetected += Time.deltaTime;
                
                // Salir del modo batalla después del delay
                if (_isInBattleMode && _timeSinceLastEnemyDetected >= exitBattleDelay)
                {
                    ExitBattleMode();
                }
            }
            
            // Actualizar peso de la capa con transición suave
            UpdateLayerWeight();
        }
        
        /// <summary>
        /// Actualiza el peso de la capa UpperBody con transición suave
        /// </summary>
        void UpdateLayerWeight()
        {
            if (animator == null || animator.layerCount <= upperBodyLayerIndex) return;
            
            // Interpolar hacia el peso objetivo
            if (!Mathf.Approximately(_currentLayerWeight, _targetLayerWeight))
            {
                float speed = 1f / Mathf.Max(0.01f, layerFadeDuration);
                _currentLayerWeight = Mathf.MoveTowards(_currentLayerWeight, _targetLayerWeight, speed * Time.deltaTime);
                animator.SetLayerWeight(upperBodyLayerIndex, _currentLayerWeight);
                
#if UNITY_EDITOR
                if (debugMode && Mathf.Approximately(_currentLayerWeight, _targetLayerWeight))
                {
                    Debug.Log($"[PlayerBattleMode] Capa UpperBody peso = {_currentLayerWeight:F2}");
                }
#endif
            }
        }
        
        /// <summary>
        /// Detecta si hay enemigos cerca (NPCs en CombatState o enemigos puros)
        /// </summary>
        bool DetectEnemiesNearby()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, enemyDetectionRadius, _hitColliders, enemyLayer);
            
            for (int i = 0; i < hitCount; i++)
            {
                var hitCollider = _hitColliders[i];
                if (hitCollider == null) continue;
                
                var root = hitCollider.transform.root; // Get the root of the hierarchy
                
                // Verificar si es un NPC enemigo en combate
                var npcManager = root.GetComponentInChildren<NPC.NPCBehaviourManagerV2>();
                if (npcManager != null)
                {
                    var brain = npcManager.Brain;
                    if (brain != null && brain.CurrentState != null)
                    {
                        string stateName = brain.CurrentState.GetType().Name;
                        if (stateName == "CombatState")
                        {
                            return true;
                        }
                    }
                }
                
                // También detectar enemigos puros (sin NPCBehaviourManagerV2) que tengan Damageable
                var damageable = root.GetComponentInChildren<Damageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    // Si tiene Targetable y está en combate activo
                    var targetable = root.GetComponentInChildren<Targetable>();
                    if (targetable != null && targetable.isInActiveCombat)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Entra en modo batalla - Activa la capa UpperBody con pose de combate
        /// </summary>
        void EnterBattleMode()
        {
            if (_isInBattleMode) return;
            
            _isInBattleMode = true;
            _targetLayerWeight = 1f;

            if (actionManager != null)
                actionManager.PushMode(ActionMode.Combat);
            
            // Asegurar que la animación de batalla esté reproduciéndose en la capa
            if (animator != null && animator.layerCount > upperBodyLayerIndex)
            {
                // Verificar si el estado existe en la capa
                if (animator.HasState(upperBodyLayerIndex, _battleIdleHash))
                {
                    // Usar el full path si está definido, sino el nombre simple
                    string statePath = !string.IsNullOrEmpty(battleIdleFullPath) ? battleIdleFullPath : battleIdleStateName;
                    animator.CrossFadeInFixedTime(statePath, 0.2f, upperBodyLayerIndex);
                }
            }
            
#if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🗡️ ENTRANDO en Battle Mode - UpperBody Layer activándose");
#endif
        }
        
        /// <summary>
        /// Sale del modo batalla - Desactiva la capa UpperBody
        /// </summary>
        void ExitBattleMode()
        {
            if (!_isInBattleMode) return;
            
            _isInBattleMode = false;
            _targetLayerWeight = 0f;

            if (actionManager != null)
                actionManager.PopMode(ActionMode.Combat);
            
#if UNITY_EDITOR
            if (debugMode)
                Debug.Log($"[PlayerBattleMode] 🏡 SALIENDO de Battle Mode - UpperBody Layer desactivándose");
#endif
        }
        
        /// <summary>
        /// Fuerza la entrada/salida del modo batalla (para uso externo)
        /// </summary>
        public void SetBattleMode(bool active)
        {
            if (active)
                EnterBattleMode();
            else
                ExitBattleMode();
        }
        
        /// <summary>
        /// Verifica si está en modo batalla
        /// </summary>
        public bool IsInBattleMode => _isInBattleMode;
        
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
                // IMPORTANTE: holdSeconds = 0 significa que NO se restaura automáticamente
                // El NPCCombatLifecycleHandler se encargará de restaurar la música después del diálogo post-derrota
                AudioService.Instance.PlayVictoryForBattle(_currentBattleId ?? "", victorySfxKey, holdSeconds: 0f);
                Debug.Log($"[PlayerBattleMode] 🎵 ✅ Reproduciendo música de victoria: {victorySfxKey} (battleId: {_currentBattleId ?? "null"}) - Restauración manual por lifecycle handler");
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
            
            // Re-habilitar control del jugador
            // La animación de victoria tiene exit time configurado en el Animator
            // que automáticamente transiciona a locomotion, por lo que NO necesitamos
            // forzar ninguna transición manualmente
            if (controller != null)
            {
                controller.enabled = true; // Re-habilitar completamente el controlador
                Debug.Log($"[PlayerBattleMode] 🎮 Controlador del jugador RE-HABILITADO - Animator manejará transición automática");
            }
            else
            {
                Debug.LogWarning($"[PlayerBattleMode] ⚠️ Controller es NULL - no se pudo re-habilitar");
            }
            
            Debug.Log($"[PlayerBattleMode] ✅ Secuencia de victoria COMPLETADA - Animator transicionará automáticamente a locomotion");
        }
        
        // Debug Gizmos
        void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (!debugMode) return;
            
            Gizmos.color = _isInBattleMode ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, enemyDetectionRadius);
#endif
        }
    }
}
