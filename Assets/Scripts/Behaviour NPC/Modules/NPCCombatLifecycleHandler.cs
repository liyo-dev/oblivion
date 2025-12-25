using UnityEngine;
using Game.NPC.Common;
using Game.NPC.States;
using Sendero.Core.Feedback;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Componente que maneja el ciclo de vida del combate del NPC:
    /// - Suscripción a eventos de Damageable
    /// - Reproducción de diálogos al ser derrotado
    /// - Cambio de estado post-derrota
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public class NPCCombatLifecycleHandler : MonoBehaviour
    {
        [Header("Feedbacks de Daño")]
        [SerializeField, Tooltip("Reproducir animación de daño al recibir golpes")]
        private bool playDamageAnimation = true;
        
        [SerializeField, Tooltip("Tiempo de pausa al recibir daño (el NPC se detiene)")]
        [Range(0f, 2f)]
        private float damageStunDuration = 1f;
        
        [SerializeField, Tooltip("Tiempo de invulnerabilidad después de recibir daño")]
        [Range(0f, 2f)]
        private float invulnerabilityDuration = 0.5f;
        
        [SerializeField, Tooltip("Shake de cámara al recibir daño")]
        private bool enableCameraShake = true;
        
        [SerializeField, Range(0f, 1f), Tooltip("Intensidad del shake de cámara")]
        private float cameraShakeIntensity = 0.2f;
        
        [SerializeField, Range(0f, 0.5f), Tooltip("Duración del shake de cámara")]
        private float cameraShakeDuration = 0.15f;
        
        [SerializeField, Tooltip("Activar hitstop (slowmotion) al recibir daño")]
        private bool enableHitStop = true;
        
        [SerializeField, Range(0f, 1f), Tooltip("TimeScale durante hitstop (0 = pausa total)")]
        private float hitStopTimeScale = 0.3f;
        
        [SerializeField, Range(0f, 0.5f), Tooltip("Duración del hitstop")]
        private float hitStopDuration = 0.1f;
        
        [Header("Feedbacks de Muerte (Golpe Letal)")]
        [SerializeField, Tooltip("Activar efectos especiales al morir")]
        private bool enableDeathEffects = true;
        
        [SerializeField, Range(0f, 2f), Tooltip("Intensidad del shake en golpe letal")]
        private float deathShakeIntensity = 1.2f;
        
        [SerializeField, Range(0f, 1f), Tooltip("Duración del shake en golpe letal")]
        private float deathShakeDuration = 0.5f;
        
        [SerializeField, Range(0f, 1f), Tooltip("TimeScale durante slowmo de muerte")]
        private float deathSlowMotionScale = 0.1f;
        
        [SerializeField, Range(0f, 2f), Tooltip("Duración del slowmo en golpe letal")]
        private float deathSlowMotionDuration = 0.8f;
        
        [Header("VFX de Muerte")]
        [SerializeField, Tooltip("Prefab del VFX que se reproduce al morir")]
        private GameObject deathVFXPrefab;
        
        [SerializeField, Tooltip("Offset del VFX respecto al NPC")]
        private Vector3 deathVFXOffset = Vector3.up;
        
        [SerializeField, Range(0f, 10f), Tooltip("Tiempo de vida del VFX")]
        private float deathVFXLifetime = 3f;
        
        private NPCBehaviourManagerV2 _npcManager;
        private Damageable _damageable;
        private NPCSimpleAnimator _animator;
        private NPCCombatConfig _combatConfig;
        private UnityEngine.AI.NavMeshAgent _navAgent;
        private bool _hasBeenDefeated;
        private bool _isProcessingDefeat;
        private bool _isInvulnerable;
        
#pragma warning disable CS0414 // Reservado para sistema de stun futuro
        private bool _isStunned;
#pragma warning restore CS0414
        
        // Sistema de interrupción de casting
        private bool _isCasting;
        private string _currentCastAnimation;
        private int _currentCastLayer;
        
        /// <summary>
        /// Indica si el NPC ha sido derrotado y NO debe volver a entrar en combate
        /// </summary>
        public bool IsDefeatedAndInactive => _hasBeenDefeated;
        
        /// <summary>
        /// Marca que el NPC está casteando un hechizo (puede ser interrumpido por daño)
        /// </summary>
        public void StartCasting(string animationName, int layer)
        {
            _isCasting = true;
            _currentCastAnimation = animationName;
            _currentCastLayer = layer;
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🎭 Casting iniciado: {animationName} (layer {layer})");
        }
        
        /// <summary>
        /// Marca que el casting terminó normalmente
        /// </summary>
        public void EndCasting()
        {
            if (_isCasting)
            {
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ Casting completado: {_currentCastAnimation}");
            }
            _isCasting = false;
            _currentCastAnimation = null;
        }
        
        /// <summary>
        /// Interrumpe el casting actual y reproduce la animación de TakeDamage
        /// </summary>
        private void InterruptCasting()
        {
            if (!_isCasting) return;
            
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⚠️ CASTING INTERRUMPIDO por daño: {_currentCastAnimation}");
            
            // Reproducir animación de TakeDamage para interrumpir visualmente el casting
            if (_animator != null)
            {
                _animator.PlayGetHit();
            }
            
            // Limpiar estado de casting
            _isCasting = false;
            _currentCastAnimation = null;
            
            // Detener cualquier coroutine de MonitorSpellCastEnd activo
            var combatBrain = GetComponent<NPCCombatBrain>();
            if (combatBrain != null)
            {
                combatBrain.StopAllCoroutines(); // Esto detendrá MonitorSpellCastEnd
            }
        }
        
        private void Awake()
        {
            Initialize();
        }
        
        /// <summary>
        /// Inicializa las referencias. Puede llamarse manualmente si el componente se añade en runtime.
        /// </summary>
        public void Initialize()
        {
            if (_npcManager != null && _damageable != null)
                return; // Ya inicializado
            
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _damageable = GetComponent<Damageable>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _navAgent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⚙️ Inicializando - NPCManager: {_npcManager != null}, Damageable: {_damageable != null}, Animator: {_animator != null}, NavAgent: {_navAgent != null}");
            
            if (_npcManager == null || _damageable == null)
            {
                enabled = false;
                return;
            }
        }
        
        private void Start()
        {
            // Obtener configuración de combate
            if (_npcManager.Configuration != null)
            {
                _combatConfig = _npcManager.Configuration.combatConfig;
            }
            
            // ✅ SUSCRIBIRSE A EVENTOS DE DAÑO Y MUERTE
            _damageable.OnDamaged += HandleNPCDamaged;
            _damageable.OnDied += HandleNPCDeath;
            
            // Configurar Damageable para que no se destruya automáticamente
            _damageable.SetDestroyOnDeath(false);
        }
        
        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged -= HandleNPCDamaged;
                _damageable.OnDied -= HandleNPCDeath;
            }
            
            // ✅ SALVAGUARDA FINAL: Si este componente se destruye mientras hay slowmo activo,
            // restaurar el timeScale a 1 para evitar que el juego quede lento permanentemente
            if (_isProcessingDefeat && Time.timeScale != 1f)
            {
                Debug.LogWarning($"[NPCCombatLifecycleHandler:{name}] ⚠️ Componente destruido durante slowmo - Restaurando Time.timeScale a 1");
                Time.timeScale = 1f;
            }
        }
        
        /// <summary>
        /// Maneja el feedback cuando el NPC recibe daño
        /// </summary>
        private void HandleNPCDamaged(float damageAmount)
        {
            // No procesar daño si ya está derrotado o es invulnerable
            if (_hasBeenDefeated || _isProcessingDefeat || _isInvulnerable)
                return;
            
            // ✅ INTERRUMPIR CASTING SI ESTÁ ACTIVO
            if (_isCasting)
            {
                InterruptCasting();
                return; // No hacer el stun normal, la interrupción ya reproduce TakeDamage
            }
            
            // Iniciar coroutine de daño
            StartCoroutine(DamageStunSequence());
        }
        
        /// <summary>
        /// Secuencia de stun al recibir daño
        /// </summary>
        private System.Collections.IEnumerator DamageStunSequence()
        {
            // Activar invulnerabilidad
            _isInvulnerable = true;
            _isStunned = true;
            
            // 1. Reproducir animación de daño
            if (playDamageAnimation && _animator != null)
            {
                _animator.PlayGetHit();
            }
            
            // 2. Detener el NavMeshAgent (pausa/stun)
            bool wasAgentActive = false;
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                wasAgentActive = true;
                _navAgent.isStopped = true;
                _navAgent.velocity = Vector3.zero;
            }
            
            // 3. Camera shake
            if (enableCameraShake)
            {
                FeedbackService.CameraShake(cameraShakeIntensity, cameraShakeDuration);
            }
            
            // 4. Hitstop (slowmotion breve)
            if (enableHitStop)
            {
                FeedbackService.HitStop(hitStopTimeScale, hitStopDuration);
            }
            
            // 5. Esperar duración del stun
            yield return new WaitForSeconds(damageStunDuration);
            
            // 6. Reactivar movimiento
            _isStunned = false;
            if (wasAgentActive && _navAgent != null && _navAgent.enabled)
            {
                _navAgent.isStopped = false;
            }
            
            // 7. Mantener invulnerabilidad un poco más
            float remainingInvulnerability = Mathf.Max(0f, invulnerabilityDuration - damageStunDuration);
            if (remainingInvulnerability > 0f)
            {
                yield return new WaitForSeconds(remainingInvulnerability);
            }
            
            // 8. Desactivar invulnerabilidad
            _isInvulnerable = false;
        }
        
        /// <summary>
        /// Maneja la interacción post-derrota
        /// </summary>
        public bool HandlePostDefeatInteraction(GameObject interactor)
        {
            if (!_hasBeenDefeated)
                return false;
            
            // Si hay diálogo después de la derrota, reproducirlo
            if (_combatConfig != null && _combatConfig.dialogueAfterDefeat != null)
            {
                var dm = DialogueManager.Instance;
                if (dm != null)
                {
                    dm.StartDialogue(_combatConfig.dialogueAfterDefeat, transform, null);
                    return true;
                }
            }
            
            return false;
        }
        
        private void HandleNPCDeath()
        {
            if (_isProcessingDefeat)
                return;
            
            _isProcessingDefeat = true;
            _hasBeenDefeated = true;
            
            // Iniciar secuencia de muerte
            StartCoroutine(DeathSequence());
        }
        
        /// <summary>
        /// Secuencia de muerte con efectos de golpe letal
        /// </summary>
        private System.Collections.IEnumerator DeathSequence()
        {
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] 💀 DeathSequence iniciada");
            
            // ✅ SALVAGUARDA: Guardar el timeScale original antes de cualquier cosa
            float savedTimeScale = Time.timeScale;
            
            // 1. Reproducir animación de muerte inmediatamente
            if (_animator != null)
            {
                Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🎬 Llamando a _animator.PlayDeath()");
                _animator.PlayDeath();
            }
            else
            {
                Debug.LogError($"[NPCCombatLifecycleHandler:{name}] ❌ _animator es NULL - no se puede reproducir animación de muerte");
            }
            
            // 2. Spawn VFX de muerte
            if (deathVFXPrefab != null)
            {
                Vector3 vfxPosition = transform.position + deathVFXOffset;
                FeedbackService.PlayVFX(deathVFXPrefab, vfxPosition, Quaternion.identity, deathVFXLifetime);
            }
            
            // 3. EFECTOS DE GOLPE LETAL - Slowmo + Shake intenso
            bool slowmoWasApplied = false;
            if (enableDeathEffects)
            {
                try
                {
                    // Activar slowmotion
                    Time.timeScale = deathSlowMotionScale;
                    slowmoWasApplied = true;
                    Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⏱️ Slowmo activado - Time.timeScale: {Time.timeScale}");
                    
                    // Camera shake intenso sincronizado con slowmo
                    FeedbackService.CameraShake(deathShakeIntensity, deathShakeDuration);
                    
                    // Esperar duración del slowmo (usando unscaledDeltaTime)
                    float elapsed = 0f;
                    while (elapsed < deathSlowMotionDuration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                finally
                {
                    // ✅ GARANTIZADO: Restaurar timeScale SIEMPRE, incluso si hay excepción
                    if (slowmoWasApplied)
                    {
                        Time.timeScale = savedTimeScale;
                        Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⏱️ Slowmo restaurado - Time.timeScale: {Time.timeScale}");
                    }
                }
            }
            
            // ✅ VERIFICACIÓN EXTRA #1: Asegurar que Time.timeScale está en 1 ANTES de continuar
            if (Time.timeScale != 1f)
            {
                Debug.LogWarning($"[NPCCombatLifecycleHandler:{name}] ⚠️ Time.timeScale todavía no es 1 (actual: {Time.timeScale}), forzando restauración");
                Time.timeScale = 1f;
            }
            
            // Esperar un frame extra para asegurar que el cambio de timeScale se aplique
            yield return null;
            
            // ✅ VERIFICACIÓN EXTRA #2: Doble check después del yield
            if (Time.timeScale != 1f)
            {
                Debug.LogError($"[NPCCombatLifecycleHandler:{name}] ❌ CRÍTICO: Time.timeScale AÚN no es 1 después del yield (actual: {Time.timeScale}), forzando OTRA VEZ");
                Time.timeScale = 1f;
            }
            
            // 4. Marcar como derrotado y detener todo movimiento INMEDIATAMENTE
            if (_npcManager != null && _npcManager.Context != null)
            {
                _npcManager.Context.IsInCombat = false;
                _npcManager.Context.WasDefeatedInCombat = true;
                
                // ✅ DETENER EL COMBAT BRAIN INMEDIATAMENTE para que no siga moviéndose
                var combatBrain = _npcManager.GetComponent<NPCCombatBrain>();
                if (combatBrain != null)
                {
                    combatBrain.StopCombat();
                    Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🛑 Combat brain detenido inmediatamente");
                }
                
                // ✅ DETENER EL NAVMESH AGENT INMEDIATAMENTE
                if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
                {
                    _navAgent.isStopped = true;
                    _navAgent.velocity = Vector3.zero;
                    _navAgent.updateRotation = false;
                    _navAgent.updatePosition = false;
                    Debug.Log($"[NPCCombatLifecycleHandler:{name}] 🛑 NavMeshAgent detenido y bloqueado");
                }
            }
            
            // 5. ROTAR HACIA EL JUGADOR INMEDIATAMENTE (antes del delay)
            if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            {
                Vector3 directionToPlayer = playerGo.transform.position - transform.position;
                directionToPlayer.y = 0f; // Mantener rotación en el plano horizontal
                
                if (directionToPlayer.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    transform.rotation = targetRotation;
                    Debug.Log($"[NPCCombatLifecycleHandler:{name}] 👁️ NPC girado hacia el jugador ANTES del delay");
                }
            }
            
            // 6. ESPERAR 2 SEGUNDOS para que se vea la animación de muerte
            // USAR WaitForSecondsRealtime para que NO se vea afectado por Time.timeScale
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ⏳ Esperando 2 segundos (tiempo real) para que se complete la animación de muerte...");
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] 📊 Time.timeScale actual: {Time.timeScale}");
            yield return new WaitForSecondsRealtime(2f);
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] ✅ Animación de muerte completada, procediendo con el diálogo");
            Debug.Log($"[NPCCombatLifecycleHandler:{name}] 📊 Time.timeScale después del delay: {Time.timeScale}");
            
            // ✅ VERIFICACIÓN FINAL: Asegurar una última vez que Time.timeScale está en 1 antes del diálogo
            if (Time.timeScale != 1f)
            {
                Debug.LogError($"[NPCCombatLifecycleHandler:{name}] ❌ CRÍTICO: Time.timeScale TODAVÍA no es 1 antes del diálogo (actual: {Time.timeScale}), forzando");
                Time.timeScale = 1f;
            }
            
            // 7. Reproducir diálogo de derrota si existe
            if (_combatConfig != null && _combatConfig.dialogueOnDefeat != null)
            {
                var dm = DialogueManager.Instance;
                if (dm != null)
                {
                    // ✅ El NPC ya está mirando al jugador (rotado antes del delay)
                    // ✅ Usar StartDialogue normal (NO StartBattleDialogue) para evitar efectos de pre-batalla
                    dm.StartDialogue(_combatConfig.dialogueOnDefeat, transform, OnDefeatDialogueComplete);
                    yield break;
                }
            }
            
            // Si no hay diálogo, completar inmediatamente
            OnDefeatDialogueComplete();
        }
        
        private void OnDefeatDialogueComplete()
        {
            // Cambiar el GameObject a la layer "Interactable"
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer != -1)
            {
                gameObject.layer = interactableLayer;
            }
            
            // Asegurar que el collider esté activo y configurado como trigger
            var capsuleCollider = GetComponent<CapsuleCollider>();
            if (capsuleCollider != null)
            {
                capsuleCollider.enabled = true;
                capsuleCollider.isTrigger = true;
            }
            
            // Asegurar que existe el componente Interactable
            var interactable = GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = gameObject.AddComponent<Interactable>();
            }
            
            // Habilitar el componente
            interactable.enabled = true;
            
            // Configurar el diálogo de "after defeat" si existe
            if (_combatConfig != null && _combatConfig.dialogueAfterDefeat != null)
            {
                interactable.SetDialogue(_combatConfig.dialogueAfterDefeat);
                interactable.SetMode(InteractableMode.OpenDialogue);
            }
            
            // El NPC ya está en DeadState desde DeathSequence(), no necesitamos cambiar estado aquí
            
            _isProcessingDefeat = false;
        }
        
        public bool HasBeenDefeated => _hasBeenDefeated;
    }
}

