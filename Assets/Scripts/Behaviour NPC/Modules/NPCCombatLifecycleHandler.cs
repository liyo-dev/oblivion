﻿﻿using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;
using Game.Player;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Maneja el ciclo de vida del combate: Daño, Stun, Muerte, SlowMotion y lógica Post-Batalla.
    /// Última actualización: 28/12/2024 - Agregado HandlePostDefeatInteraction
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    [RequireComponent(typeof(NPCBehaviourManagerV2))]
    public class NPCCombatLifecycleHandler : MonoBehaviour
    {
        #region ⚙️ Configuration
        [Header("💥 Damage Feedback")]
        [SerializeField] private bool playDamageAnimation = true;
        [SerializeField, Range(0f, 2f)] private float damageStunDuration = 0.8f; // Reducido un poco para mayor dinamismo
        [SerializeField, Range(0f, 2f)] private float invulnerabilityDuration = 0.5f;
        
        [Header("🎥 Camera & Time")]
        [SerializeField] private bool enableCameraShake = true;
        [SerializeField] private float cameraShakeIntensity = 0.2f;
        [SerializeField] private float cameraShakeDuration = 0.15f;
        [SerializeField] private bool enableHitStop = true;
        [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.1f; // Más lento para mayor impacto
        [SerializeField] private float hitStopDuration = 0.15f;

        [Header("💀 Death Sequence")]
        [SerializeField] private bool enableDeathEffects = true;
        [SerializeField] private float deathSlowMoScale = 0.2f;
        [SerializeField] private float deathSlowMoDuration = 1.0f;
        [SerializeField] private GameObject deathVFXPrefab;
        [SerializeField] private Vector3 deathVFXOffset = Vector3.up;
        #endregion

        #region 🔌 Dependencies
        private NPCBehaviourManagerV2 _manager;
        private Damageable _damageable;
        private NPCSimpleAnimator _animator;
        private NavMeshAgent _agent;
        private NPCCombatBrain _brain;
        private NPCCombatConfig _config;
        #endregion

        #region 📊 State
        public bool IsDefeatedAndInactive { get; private set; }
        public bool IsStunned { get; private set; }
        
        private bool _isProcessingDefeat;
        private bool _isInvulnerable;
        private bool _isCasting; // Para interrumpir hechizos
        #endregion

        private void Awake()
        {
            _manager = GetComponent<NPCBehaviourManagerV2>();
            _damageable = GetComponent<Damageable>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _agent = GetComponent<NavMeshAgent>();
            _brain = GetComponent<NPCCombatBrain>();
        }

        private void Start()
        {
            if (_manager.Configuration != null) 
                _config = _manager.Configuration.combatConfig;

            _damageable.OnDamaged += OnDamaged;
            _damageable.OnDied += OnDied;
            _damageable.SetDestroyOnDeath(false); // Importante: controlamos la muerte manualmente
        }

        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged -= OnDamaged;
                _damageable.OnDied -= OnDied;
            }
            // Salvaguarda final por si se destruye durante slow-mo
            if (Time.timeScale < 0.99f || Time.timeScale > 1.01f) Time.timeScale = 1f;
        }

        // =================================================================================
        // ⚔️ DAMAGE HANDLING
        // =================================================================================

        public void StartCasting(string animName, int layer) { _isCasting = true; }
        public void EndCasting() { _isCasting = false; }

        private void OnDamaged(float amount)
        {
            if (_isInvulnerable || _isProcessingDefeat) return;

            // 🔍 DEBUG: Log de vida actual
            Debug.Log($"[Lifecycle] ⚔️ {name} recibió {amount} de daño - Vida: {_damageable.Current}/{_damageable.Max} - IsAlive: {_damageable.IsAlive}");

            // ✅ Notificar al CombatBrain (para detectar ataques por la espalda durante búsqueda)
            if (_brain != null && _manager != null && _manager.Context != null && _manager.Context.Player != null)
            {
                _brain.OnTakeDamage(_manager.Context.Player.position);
            }

            // Interrupción de Casting
            if (_isCasting)
            {
                Debug.Log($"[Lifecycle] ⚡ Interrumpiendo hechizo por daño!");
                _isCasting = false;
                if (_brain != null) _brain.StopAllCoroutines(); // Detener lógica del brain
                // No hacemos return aquí, dejamos que el stun normal ocurra
            }

            StartCoroutine(DamageSequence());
        }

        private IEnumerator DamageSequence()
        {
            IsStunned = true;
            _isInvulnerable = true;

            // ✅ Si este golpe es LETAL, aplicar slow-mo AHORA (durante la animación de Hit)
            bool isLethalHit = !_damageable.IsAlive;
            
            if (isLethalHit && enableDeathEffects)
            {
                Debug.Log($"[Lifecycle] 💀 GOLPE LETAL detectado - Aplicando slow motion durante animación de Hit");
                FeedbackService.CameraShake(cameraShakeIntensity * 2f, 0.5f);
                Time.timeScale = deathSlowMoScale;
            }

            // 1. Feedback Visual/Sonoro
            if (playDamageAnimation && _animator) _animator.PlayGetHit();
            if (!isLethalHit && enableCameraShake) FeedbackService.CameraShake(cameraShakeIntensity, cameraShakeDuration);
            if (!isLethalHit && enableHitStop) FeedbackService.HitStop(hitStopTimeScale, hitStopDuration);

            // 2. Detener movimiento físico
            bool wasMoving = _agent != null && _agent.enabled && !_agent.isStopped;
            if (_agent && _agent.enabled) _agent.isStopped = true;

            // 3. Esperar Stun (usar WaitForSecondsRealtime si está en slow-mo)
            if (isLethalHit && enableDeathEffects)
            {
                // Durante slow-mo, esperar un poco para ver la animación de Hit
                yield return new WaitForSecondsRealtime(deathSlowMoDuration);
                // Restaurar time scale
                Time.timeScale = 1f;
                Debug.Log($"[Lifecycle] ⏱️ Slow motion terminado - Time scale restaurado");
            }
            else
            {
                yield return new WaitForSeconds(damageStunDuration);
            }

            // 4. Recuperación (solo si el NPC NO ha muerto)
            if (_damageable.IsAlive)
            {
                if (_animator)
                {
                    _animator.ResetMovement();
                    _animator.TransitionToIdle(); // Forzar vuelta a idle de combate
                }

                if (_agent && _agent.enabled && wasMoving) 
                    _agent.isStopped = false;
            }
            // Si el NPC murió, NO hacer transición a Idle - DeathRoutine se encargará

            IsStunned = false;

            // Invulnerabilidad residual (frames de gracia) - solo si NO murió
            if (_damageable.IsAlive)
            {
                float graceTime = Mathf.Max(0, invulnerabilityDuration - damageStunDuration);
                if (graceTime > 0) yield return new WaitForSeconds(graceTime);
            }
            
            _isInvulnerable = false;
        }

        // =================================================================================
        // 💀 DEATH SEQUENCE
        // =================================================================================

        private void OnDied()
        {
            Debug.Log($"[Lifecycle] 💀💀💀 OnDied() LLAMADO para {name} - _isProcessingDefeat: {_isProcessingDefeat}");
            
            if (_isProcessingDefeat)
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ OnDied() ya en proceso, ignorando duplicado");
                return;
            }
            
            _isProcessingDefeat = true;
            IsDefeatedAndInactive = true;

            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Debug.Log($"[Lifecycle] 💀 Iniciando secuencia de muerte: {name}");

            // 1. DETENER TODO INMEDIATAMENTE
            if (_brain) _brain.StopCombat();
            if (_agent && _agent.enabled) { _agent.isStopped = true; _agent.velocity = Vector3.zero; }
            if (_manager.Context != null)
            {
                _manager.Context.IsInCombat = false;
                _manager.Context.WasDefeatedInCombat = true;
            }

            // 2. VFX inicial
            if (deathVFXPrefab) Instantiate(deathVFXPrefab, transform.position + deathVFXOffset, Quaternion.identity);

            // 3. Rotar hacia el jugador (Último aliento)
            if (PlayerService.TryGetPlayer(out GameObject player))
            {
                Vector3 dir = (player.transform.position - transform.position).normalized;
                dir.y = 0;
                transform.rotation = Quaternion.LookRotation(dir);
            }

            // ✅ 4. Pequeña pausa para que termine la animación de Hit
            // El slow-mo ya se aplicó en DamageSequence durante la animación de Hit
            yield return new WaitForSeconds(0.1f);

            // ✅ 5. INICIAR ANIMACIÓN DE MUERTE INMEDIATAMENTE
            PostDeathBehavior behavior = _config != null ? _config.postDeathBehavior : PostDeathBehavior.GetUpDizzy;
            
            if (behavior == PostDeathBehavior.GetUpDizzy && _animator)
            {
                // Iniciar animación de muerte YA
                _animator.PlayDeath();
                Debug.Log($"[Lifecycle] 💀 Animación de muerte iniciada - transición directa desde Hit");
            }

            // ✅ 6. CELEBRACIÓN DEL JUGADOR (mientras el NPC cae)
            // Llamar directamente al player para que ejecute la victoria
            if (PlayerService.TryGetPlayer(out GameObject playerGo))
            {
                var playerVictory = playerGo.GetComponent<PlayerBattleModeController>();
                if (playerVictory != null)
                {
                    Debug.Log($"[Lifecycle] 🎉 Llamando a PlayVictory() del player");
                    playerVictory.PlayVictory();
                    
                    // Esperar a que termine la animación de victoria (3s) + margen
                    yield return new WaitForSecondsRealtime(4.0f);
                    Debug.Log($"[Lifecycle] ✅ Animación de victoria completada - continuando con secuencia");
                }
                else
                {
                    Debug.LogWarning($"[Lifecycle] ⚠️ PlayerBattleModeController no encontrado en el player");
                }
            }
            else
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ No se pudo obtener el player con PlayerService");
            }
            
            // También disparar evento narrativo si está configurado (para audio, etc.)
            if (_config != null && !string.IsNullOrEmpty(_config.battleMusicId))
            {
                DefaultNarrativeSignals.Instance?.RaiseBattleWon(_config.battleMusicId);
            }

            // ✅ 7. POST-MUERTE (Desaparecer o continuar con Dizzy)
            if (behavior == PostDeathBehavior.Disappear)
            {
                yield return HandleDisappear();
            }
            else
            {
                // HandleGetUpDizzy ahora solo espera el dizzy y muestra el diálogo
                yield return HandleGetUpDizzy();
            }

            _isProcessingDefeat = false;
        }

        // --- Rutinas de Finalización ---

        private IEnumerator HandleDisappear()
        {
            // Diálogo final antes de irse
            if (_config?.dialogueOnDefeat != null)
            {
                yield return RunDialogueRoutine(_config.dialogueOnDefeat);
            }

            // VFX Desaparición
            if (_config?.disappearVFXPrefab)
                Instantiate(_config.disappearVFXPrefab, transform.position + Vector3.up, Quaternion.identity);

            gameObject.SetActive(false);
        }

        private IEnumerator HandleGetUpDizzy()
        {
            Debug.Log($"[Lifecycle] 😵 Esperando transición a animación dizzy para {name}");
            
            // 1. La animación de muerte ya se inició en DeathRoutine()
            // Solo esperamos a que esté en la animación de mareo (dizzy)
            float timeout = 10f; // Timeout de seguridad
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                if (_animator != null && _animator.IsInDizzyAnimation())
                {
                    Debug.Log($"[Lifecycle] ✅ NPC ahora está en animación dizzy - mostrando diálogo");
                    break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ Timeout esperando animación dizzy - continuando de todas formas");
            }
            
            // 2. Mostrar diálogo de mareo (cuando ya está en la animación dizzy)
            DialogueAsset dialogue = _config?.dialogueOnDizzy ?? _config?.dialogueOnDefeat;
            if (dialogue != null)
            {
                Debug.Log($"[Lifecycle] 💬 Iniciando diálogo post-derrota (la victoria del jugador ya debería haber terminado)");
                bool finished = false;
                DialogueManager.Instance.StartDialogue(dialogue, transform, () => finished = true);
                
                // Esperar a que termine el diálogo
                while (!finished) 
                {
                    yield return null;
                }
                
                Debug.Log($"[Lifecycle] 💬 Diálogo de mareo completado");
            }
            
            // 3. Configurar para interacción futura (Hablar con el NPC derrotado)
            SetupPostCombatInteraction();
            
            Debug.Log($"[Lifecycle] ✅ Secuencia GetUpDizzy completada para {name}");
        }

        private IEnumerator RunDialogueRoutine(DialogueAsset dialogue)
        {
            bool finished = false;
            DialogueManager.Instance.StartDialogue(dialogue, transform, () => finished = true);
            while (!finished) yield return null;
        }

        // =================================================================================
        // 🛠️ HELPER: Configurar NPC como interactuable tras combate
        // =================================================================================
        
        private void SetupPostCombatInteraction()
        {
            // 1. Cambiar Layer
            int layer = LayerMask.NameToLayer("Interactable");
            if (layer != -1) gameObject.layer = layer;

            // 2. Activar Trigger
            var col = GetComponent<CapsuleCollider>();
            if (col) { col.enabled = true; col.isTrigger = true; }

            // 3. Configurar Componente Interactable
            var interactable = GetComponent<Interactable>();
            if (!interactable) interactable = gameObject.AddComponent<Interactable>();
            
            interactable.enabled = true;
            
            // Asignar diálogo "Post-Derrota" (ej: "¿Qué quieres ahora?")
            if (_config?.dialogueAfterDefeat != null)
            {
                interactable.SetDialogue(_config.dialogueAfterDefeat);
                interactable.SetMode(InteractableMode.OpenDialogue);
            }
            
            Debug.Log($"[Lifecycle] ✅ NPC {name} configurado como interactuable post-combate.");
        }
        
        // =================================================================================
        // 🎭 POST-DEFEAT INTERACTION HANDLING
        // =================================================================================
        
        /// <summary>
        /// Maneja la interacción con el NPC después de haber sido derrotado.
        /// Retorna true si la interacción fue procesada.
        /// </summary>
        public bool HandlePostDefeatInteraction(GameObject interactor)
        {
            if (!IsDefeatedAndInactive)
                return false;
            
            // Si tiene diálogo post-derrota configurado, el sistema de Interactable lo manejará
            // Este método existe principalmente para validación y lógica adicional
            
            Debug.Log($"[Lifecycle] 💬 Jugador interactúa con NPC derrotado: {name}");
            
            // El Interactable component ya maneja el diálogo automáticamente
            // Solo retornamos true para indicar que la interacción es válida
            return true;
        }
    }
}

