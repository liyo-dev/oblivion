using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;
using Game.Player;
using Game.NPC;

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
        
        // ✅ CAMBIO: Obtener _config dinámicamente para asegurar que siempre tenga los valores actualizados
        // Esto es necesario porque NPCInteractiveNarrativeExecutor puede actualizar combatConfig después de Start()
        private NPCCombatConfig _config => _manager?.Configuration?.combatConfig;
        #endregion

        #region 📊 State
        public bool IsDefeatedAndInactive { get; private set; }
        public bool IsStunned { get; private set; }
        
        private bool _isProcessingDefeat;
        private bool _isInvulnerable;
        private bool _isCasting; // Para interrumpir hechizos
        private bool _shouldCancelDizzySequence; // Para interrumpir dizzy cuando inicia movimiento narrativo
        #endregion

        private void Awake()
        {
            _manager = GetComponent<NPCBehaviourManagerV2>();
            _damageable = GetComponent<Damageable>();
            _animator = GetComponent<NPCSimpleAnimator>();
            _agent = GetComponent<NavMeshAgent>();
            _brain = GetComponent<NPCCombatBrain>();
            
            // ✅ CRÍTICO: Configurar destroyOnDeath=false INMEDIATAMENTE en Awake
            // Esto se aplica tanto si el componente ya existía como si se acaba de añadir
            if (_damageable != null)
            {
                _damageable.SetDestroyOnDeath(false);
                // Debug.Log($"[Lifecycle] ✅ destroyOnDeath establecido a FALSE en Awake para {name} (actual valor: {_damageable.GetComponent<Damageable>() != null})");
            }
            else
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ Damageable no encontrado en Awake para {name} - esperando que se añada después");
            }
        }

        private void Start()
        {
            // ✅ _config ahora es una propiedad que obtiene el valor dinámicamente
            // No necesita asignación aquí

            // ✅ Verificación de respaldo: Si Damageable no estaba en Awake, obtenerlo ahora
            if (_damageable == null)
            {
                _damageable = GetComponent<Damageable>();
                if (_damageable != null)
                {
                    Debug.LogWarning($"[Lifecycle] ⚠️ Damageable se añadió después de Awake - configurando destroyOnDeath=false ahora");
                    _damageable.SetDestroyOnDeath(false);
                }
                else
                {
                    Debug.LogError($"[Lifecycle] ❌ Damageable SIGUE siendo null en Start() para {name}");
                    return;
                }
            }
            
            // Verificación final: Asegurar que destroyOnDeath esté en false
            _damageable.SetDestroyOnDeath(false);
            // Debug.Log($"[Lifecycle] 🔒 Verificación final en Start: destroyOnDeath=false para {name}");

            // Suscribirse a eventos
            _damageable.OnDamaged += OnDamaged;
            _damageable.OnDied += OnDied;
            
            // Debug.Log($"[Lifecycle] ✅ Suscrito a eventos OnDamaged y OnDied para {name}");
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
            
            // ✅ Notificar al equipo si pertenece a uno
            var teamMember = GetComponent<Game.NPC.NPCTeamMember>();
            if (teamMember != null)
            {
                teamMember.NotifyDefeated();
            }

            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            Debug.Log($"[Lifecycle] 💀 Iniciando secuencia de muerte: {name}");
            
            // ✅ DEBUG: Verificar configuración de eventos de derrota
            Debug.Log($"[Lifecycle] 🔍 Config de derrota para {name}:");
            Debug.Log($"    - _config es null: {_config == null}");
            if (_config != null)
            {
                Debug.Log($"    - sendEventOnDefeat: {_config.sendEventOnDefeat}");
                Debug.Log($"    - defeatEventKey: '{_config.defeatEventKey}'");
                Debug.Log($"    - sendDefeatEventBeforeDeath: {_config.sendDefeatEventBeforeDeath}");
            }

            // 1. DETENER TODO INMEDIATAMENTE
            if (_brain) _brain.StopCombat();
            if (_agent && _agent.enabled) { _agent.isStopped = true; _agent.velocity = Vector3.zero; }
            if (_manager.Context != null)
            {
                _manager.Context.IsInCombat = false;
                _manager.Context.WasDefeatedInCombat = true;
            }
            
            // ✅ NUEVO: Enviar evento de derrota ANTES de la muerte si está configurado así
            if (_config != null && _config.sendEventOnDefeat && _config.sendDefeatEventBeforeDeath && !string.IsNullOrEmpty(_config.defeatEventKey))
            {
                Debug.Log($"[Lifecycle] 📤 Enviando evento de derrota ANTES de muerte: '{_config.defeatEventKey}'");
                DefaultNarrativeSignals.Instance?.RaiseCustom(_config.defeatEventKey);
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

            // ✅ VERIFICAR SI PERTENECE A UN EQUIPO
            var teamMember = GetComponent<NPCTeamMember>();
            bool isInTeam = teamMember != null && teamMember.HasTeam;
            bool isLastTeamMember = isInTeam && teamMember.Team.IsTeamDefeated;
            
            // ✅ 6. CELEBRACIÓN DEL JUGADOR
            // Solo celebrar si: NO es equipo, O es el último miembro del equipo
            bool shouldCelebrate = !isInTeam || isLastTeamMember;
            
            if (shouldCelebrate)
            {
                if (PlayerService.TryGetPlayer(out GameObject playerGo))
                {
                    var playerVictory = playerGo.GetComponent<PlayerBattleModeController>();
                    if (playerVictory != null)
                    {
                        string battleId = _config?.battleMusicId;
                        Debug.Log($"[Lifecycle] 🎉 Llamando a PlayVictory() del player con battleId: {battleId}");
                        playerVictory.PlayVictory(battleId);
                        
                        // Esperar a que termine la animación de victoria
                        yield return new WaitForSecondsRealtime(4.0f);
                        Debug.Log($"[Lifecycle] ✅ Animación de victoria completada");
                    }
                }
                
                // Disparar evento narrativo si está configurado
                if (_config != null && !string.IsNullOrEmpty(_config.battleMusicId))
                {
                    DefaultNarrativeSignals.Instance?.RaiseBattleWon(_config.battleMusicId);
                }
            }
            else
            {
                Debug.Log($"[Lifecycle] 👥 {name} derrotado pero quedan miembros del equipo - sin celebración aún");
                // Pequeña pausa para la animación de muerte
                yield return new WaitForSeconds(1f);
            }
            
            // ✅ Enviar evento de derrota al grafo narrativo DESPUÉS de la muerte (solo si no se envió antes)
            if (_config != null && _config.sendEventOnDefeat && !_config.sendDefeatEventBeforeDeath && !string.IsNullOrEmpty(_config.defeatEventKey))
            {
                Debug.Log($"[Lifecycle] 📤 Enviando evento de derrota al grafo narrativo: '{_config.defeatEventKey}'");
                DefaultNarrativeSignals.Instance?.RaiseCustom(_config.defeatEventKey);
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

        public void CancelDizzySequence()
        {
            _shouldCancelDizzySequence = true;
            Debug.Log($"[Lifecycle] 🛑 Secuencia dizzy cancelada para {name} - movimiento narrativo iniciado");
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
                    Debug.Log($"[Lifecycle] ✅ NPC ahora está en animación dizzy");
                    break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ Timeout esperando animación dizzy - continuando de todas formas");
            }
            
            // ✅ Verificar si pertenece a un equipo
            var teamMember = GetComponent<NPCTeamMember>();
            bool isInTeam = teamMember != null && teamMember.HasTeam;
            
            if (isInTeam)
            {
                var team = teamMember.Team;
                
                Debug.Log($"[Lifecycle] 👥 {name} es parte de un equipo - IsLeader: {teamMember.IsLeader}, Derrotados: {team.DefeatedCount}/{team.TeamSize}");
                
                // Esperar a que todo el equipo sea derrotado
                if (!team.IsTeamDefeated)
                {
                    Debug.Log($"[Lifecycle] 👥 {name} esperando a que caiga todo el equipo...");
                    
                    while (!team.IsTeamDefeated)
                    {
                        // ✅ Verificar si se canceló la secuencia
                        if (_shouldCancelDizzySequence)
                        {
                            Debug.Log($"[Lifecycle] 🛑 Espera de equipo interrumpida para {name}");
                            yield break;
                        }
                        
                        yield return null;
                    }
                    
                    Debug.Log($"[Lifecycle] 👥 ¡Todo el equipo derrotado!");
                }
                
                // IMPORTANTE: Solo el líder muestra el diálogo, sin importar quién cayó primero
                if (!teamMember.IsLeader)
                {
                    Debug.Log($"[Lifecycle] ✅ {name} (NO es líder) - configurando post-combate sin diálogo");
                    SetupPostCombatInteraction();
                    yield break;
                }
                
                Debug.Log($"[Lifecycle] 👑 {name} ES EL LÍDER - mostrará el diálogo del equipo");
            }
            
            // 2. Mostrar diálogo de mareo (solo si no es equipo, o si es el líder del equipo)
            DialogueAsset dialogue = _config?.dialogueOnDizzy ?? _config?.dialogueOnDefeat;
            if (dialogue != null)
            {
                // ✅ Verificar antes de mostrar diálogo
                if (_shouldCancelDizzySequence)
                {
                    Debug.Log($"[Lifecycle] 🛑 Diálogo omitido para {name} - movimiento iniciado");
                    yield break;
                }
                
                Debug.Log($"[Lifecycle] 💬 Iniciando diálogo post-derrota para {name}");
                bool finished = false;
                DialogueManager.Instance.StartDialogue(dialogue, transform, () => finished = true);
                
                // Esperar a que termine el diálogo
                while (!finished) 
                {
                    // ✅ Verificar durante el diálogo también
                    if (_shouldCancelDizzySequence)
                    {
                        Debug.Log($"[Lifecycle] 🛑 Diálogo interrumpido para {name}");
                        // Cerrar el diálogo si está abierto
                        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
                        {
                            DialogueManager.Instance.Close();
                        }
                        yield break;
                    }
                    
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
        
        // =================================================================================
        // 🔄 RESURRECTION (Para sistema de equipos)
        // =================================================================================
        
        /// <summary>
        /// Resucita al NPC, restaurando su vida y estado.
        /// Usado por el sistema de equipos de combate (NPCCombatTeam).
        /// </summary>
        public void Resurrect()
        {
            if (!IsDefeatedAndInactive)
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ {name} no está derrotado, no se puede resucitar");
                return;
            }
            
            Debug.Log($"[Lifecycle] 🔄 Resucitando {name}...");
            
            // 1. Restaurar vida
            if (_damageable != null)
            {
                _damageable.Heal(_damageable.Max); // Curar al máximo
            }
            
            // 2. Restaurar estado
            IsDefeatedAndInactive = false;
            _isProcessingDefeat = false;
            IsStunned = false;
            _isInvulnerable = false;
            
            // 3. Reactivar NavMeshAgent
            if (_agent != null)
            {
                _agent.enabled = true;
                if (_agent.isOnNavMesh)
                {
                    _agent.isStopped = false;
                }
            }
            
            // 4. Reproducir animación de levantarse (usar Victory como "levantarse feliz")
            if (_animator != null)
            {
                _animator.PlayVictory(); // Usamos Victory como animación de levantarse
            }
            
            // 5. Restaurar layer y collider
            int defaultLayer = LayerMask.NameToLayer("Default");
            if (defaultLayer != -1) gameObject.layer = defaultLayer;
            
            var col = GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.isTrigger = false;
            }
            
            // 6. Notificar al manager
            if (_manager != null && _manager.Context != null)
            {
                _manager.Context.WasDefeatedInCombat = false;
            }
            
            Debug.Log($"[Lifecycle] ✅ {name} resucitado exitosamente");
        }
    }
}

