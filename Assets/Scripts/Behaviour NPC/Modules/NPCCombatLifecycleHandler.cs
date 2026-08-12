﻿using System.Collections;
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

        [Header("💀 Death Sequence - Cámara cinematográfica")]
        [Tooltip("Factor de zoom del FOV hacia el enemigo derrotado (1 = sin zoom, <1 = acercar). Comunica que la pausa es intencional en vez de sentirse como un cuelgue.")]
        [SerializeField, Range(0.5f, 1f)] private float deathZoomFactor = 0.85f;
        [Tooltip("Duración del zoom de entrada hacia el enemigo")]
        [SerializeField, Range(0f, 0.5f)] private float deathZoomDuration = 0.15f;
        [Tooltip("Duración de la vuelta a la normalidad (FOV y ritmo) al terminar el slow-mo")]
        [SerializeField, Range(0f, 1f)] private float deathReturnDuration = 0.35f;
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

        /// <summary>
        /// FIX INC-045: un proyectil ya lanzado puede matar a este NPC después de que el
        /// jugador haya muerto (game over en curso, ej: ambos se golpean casi a la vez).
        /// En ese caso el combate terminó en derrota, no en victoria: ni la música/animación
        /// de victoria ni la restauración de música ambiente deben sonar por encima del game over.
        /// </summary>
        private static bool IsPlayerDeadOrGameOverActive()
        {
            if (GameOverManager.Instance != null && GameOverManager.Instance.IsShown) return true;
            if (PlayerService.TryGetPlayer(out GameObject playerGo))
            {
                var health = playerGo.GetComponent<PlayerHealthSystem>();
                if (health != null && !health.IsAlive) return true;
            }
            return false;
        }

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
            // Salvaguarda final por si se destruye durante slow-mo: cancelamos el efecto cinematográfico
            // (restaura FOV y libera _isEffectActive, si no quedaría bloqueado para la próxima muerte).
            // FIX C6 (auditoría 2026-08-07): antes forzaba Time.timeScale=1 "por si acaso", pisando
            // cualquier otro efecto activo (hitstop, cinemática) que no tuviera relación con esta
            // muerte. CancelDeathEffect() ya libera la petición de este NPC en TimeScaleArbiterService
            // (vía DeathCameraEffect.CancelEffect); no hace falta tocar Time.timeScale aquí.
            FeedbackService.CancelDeathEffect();
        }

        // =================================================================================
        // ⚔️ DAMAGE HANDLING
        // =================================================================================

        public void StartCasting(string animName, int layer) { _isCasting = true; }
        public void EndCasting() { _isCasting = false; }

        private void OnDamaged(float amount)
        {
            if (_isInvulnerable || _isProcessingDefeat) return;

            // Si el NPC no está en combate y recibe daño, entrar en combate inmediatamente.
            // Cubre ataques por la espalda y cualquier caso donde no haya detectado al jugador.
            if (_manager != null && _manager.Context != null && !_manager.Context.IsInCombat && !IsDefeatedAndInactive)
            {
                var attacker = _manager.Player;
                if (attacker != null)
                    _manager.ForceEnterCombat(attacker);
            }

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
                Debug.Log($"[Lifecycle] 💀 GOLPE LETAL detectado - Aplicando slow motion cinematográfico durante animación de Hit");
                FeedbackService.CameraShake(cameraShakeIntensity * 2f, 0.5f);
                // ✅ Usamos el efecto cinematográfico (zoom + easing de entrada/salida) en vez de un
                // Time.timeScale seco: el corte instantáneo se sentía como un cuelgue en vez de un
                // golpe deliberado. El zoom hacia el enemigo comunica que la pausa es intencional.
                FeedbackService.TriggerDeathEffect(transform, deathSlowMoScale, deathSlowMoDuration, deathZoomFactor, deathZoomDuration, deathReturnDuration);
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
                // El propio FeedbackService.TriggerDeathEffect ya restaura Time.timeScale de forma
                // suave (con zoom-out) al terminar su hold; aquí solo esperamos en tiempo real la
                // misma duración para mantener sincronizado el stun del NPC con el efecto visual.
                yield return new WaitForSecondsRealtime(deathSlowMoDuration);
                Debug.Log($"[Lifecycle] ⏱️ Slow motion cinematográfico terminado");
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
                DefaultNarrativeSignals.Instance?.RaiseCustom(_config.defeatEventKey, name);
            }

            // 2. VFX inicial
            // ✅ FIX: VfxPoolService en vez de Instantiate suelto — el prefab no se autodestruía
            // y quedaba flotando en el suelo indefinidamente tras terminar el combate.
            if (deathVFXPrefab) VfxPoolService.Instance.Play(deathVFXPrefab, transform.position + deathVFXOffset, Quaternion.identity, 3f);

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
                    if (IsPlayerDeadOrGameOverActive())
                    {
                        Debug.Log("[Lifecycle] ⚠️ Jugador ya murió (game over en curso) — se omite la celebración de victoria.");
                    }
                    else
                    {
                        var playerVictory = playerGo.GetComponent<PlayerBattleModeController>();
                        if (playerVictory != null)
                        {
                            string battleId = _config?.battleMusicId;
                            Debug.Log($"[Lifecycle] 🎉 Llamando a PlayVictory() del player con battleId: {battleId}");
                            playerVictory.PlayVictory(battleId);

                            // ✅ Esperar a que termine la animación de victoria del jugador
                            float victoryTimeout = 15f; // Timeout de seguridad
                            float elapsed = 0f;

                            // Esperar a que comience la victoria (puede tomar un frame)
                            yield return null;

                            // Esperar mientras el jugador está reproduciendo la victoria
                            while (playerVictory.IsPlayingVictory && elapsed < victoryTimeout)
                            {
                                elapsed += Time.unscaledDeltaTime;
                                yield return null;
                            }

                            Debug.Log($"[Lifecycle] ✅ Animación de victoria completada - la música se restaurará después del diálogo");
                        }
                    }
                }

                // NOTA: La restauración de música ahora se maneja manualmente después del diálogo post-derrota
                // en HandleGetUpDizzy(), permitiendo que la música de victoria suene durante todo el diálogo.
                // El evento RaiseBattleWon solo se usa para BossArenaController que no usa PlayVictoryForBattle.
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
                DefaultNarrativeSignals.Instance?.RaiseCustom(_config.defeatEventKey, name);
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
                VfxPoolService.Instance.Play(_config.disappearVFXPrefab, transform.position + Vector3.up, Quaternion.identity, 3f);

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
            
            // ✅ Calcular si estamos en equipo y si somos el último miembro
            var teamMember = GetComponent<NPCTeamMember>();
            bool isInTeam = teamMember != null && teamMember.Team != null;
            bool isLastTeamMember = isInTeam && teamMember.Team.IsTeamDefeated;
            
            Debug.Log($"[Lifecycle] 🔍 DEBUG HandleGetUpDizzy INICIO: isInTeam={isInTeam}, isLastTeamMember={isLastTeamMember}, battleMusicId='{_config?.battleMusicId}'");
            
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
            
            // ✅ Ya tenemos teamMember e isInTeam declarados al inicio del método
            if (isInTeam)
            {
                var team = teamMember.Team;
                
                Debug.Log($"[Lifecycle] 👥 {name} es parte de un equipo - IsLeader: {teamMember.IsLeader}, Derrotados: {team.DefeatedCount}/{team.TeamSize}");
                
                // Esperar a que todo el equipo sea derrotado
                if (!team.IsTeamDefeated)
                {
                    Debug.Log($"[Lifecycle] 👥 {name} esperando a que caiga todo el equipo...");
                    
                    // ✅ Timeout de seguridad para evitar espera infinita
                    float teamWaitTimeout = 120f; // 2 minutos de timeout
                    float teamWaitElapsed = 0f;
                    
                    while (!team.IsTeamDefeated && teamWaitElapsed < teamWaitTimeout)
                    {
                        // ✅ Verificar si se canceló la secuencia
                        if (_shouldCancelDizzySequence)
                        {
                            Debug.Log($"[Lifecycle] 🛑 Espera de equipo interrumpida para {name}");
                            yield break;
                        }
                        
                        teamWaitElapsed += Time.deltaTime;
                        yield return null;
                    }
                    
                    if (teamWaitElapsed >= teamWaitTimeout)
                    {
                        Debug.LogWarning($"[Lifecycle] ⏱️ TIMEOUT esperando caída del equipo para {name} - procediendo de todas formas");
                    }
                    else
                    {
                        Debug.Log($"[Lifecycle] 👥 ¡Todo el equipo derrotado!");
                    }
                }
                
                // IMPORTANTE: Solo el líder muestra el diálogo, sin importar quién cayó primero
                if (!teamMember.IsLeader)
                {
                    Debug.Log($"[Lifecycle] 👥 {name} (NO es líder) - esperando a que el líder termine el diálogo...");
                    
                    // ✅ Esperar a que el líder termine el diálogo CON TIMEOUT
                    float dialogueTimeout = 60f; // 60 segundos de timeout
                    float dialogueElapsed = 0f;
                    
                    while (!team.IsPostDefeatDialogueFinished && dialogueElapsed < dialogueTimeout)
                    {
                        if (_shouldCancelDizzySequence) 
                        {
                            Debug.Log($"[Lifecycle] 🛑 Espera de diálogo cancelada para {name}");
                            yield break;
                        }
                        
                        dialogueElapsed += Time.deltaTime;
                        yield return null;
                    }
                    
                    if (dialogueElapsed >= dialogueTimeout)
                    {
                        Debug.LogWarning($"[Lifecycle] ⏱️ TIMEOUT esperando diálogo del líder para {name} - procediendo de todas formas");
                    }
                    else
                    {
                        Debug.Log($"[Lifecycle] ✅ Líder terminó diálogo - {name} procede a post-acción");
                    }
                }
                else
                {
                    Debug.Log($"[Lifecycle] 👑 {name} ES EL LÍDER - mostrará el diálogo del equipo");
                }
            }
            
            // 2. Mostrar diálogo de mareo (solo si no es equipo, o si es el líder del equipo)
            // Si es miembro de equipo (no líder), saltamos este paso porque ya esperamos arriba
            bool shouldShowDialogue = !isInTeam || (isInTeam && teamMember.IsLeader);
            
            if (shouldShowDialogue)
            {
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
                    
                    // ✅ Restaurar música de batalla DESPUÉS del diálogo
                    // Solo si es el último enemigo derrotado (líder o sin equipo)
                    Debug.Log($"[Lifecycle] 🔍 DEBUG Restauración música: isInTeam={isInTeam}, IsLeader={teamMember?.IsLeader}, isLastTeamMember={isLastTeamMember}");
                    Debug.Log($"[Lifecycle] 🔍 DEBUG Config: battleMusicId='{_config?.battleMusicId}', AudioService={AudioService.Instance != null}");
                    
                    if (IsPlayerDeadOrGameOverActive())
                    {
                        Debug.Log("[Lifecycle] ⚠️ Jugador ya murió (game over en curso) — se omite la restauración de música tras el diálogo.");
                    }
                    else if (!isInTeam || (isInTeam && teamMember.IsLeader && isLastTeamMember))
                    {
                        if (!string.IsNullOrEmpty(_config?.battleMusicId) && AudioService.Instance != null)
                        {
                            Debug.Log($"[Lifecycle] 🎵 Restaurando música de batalla después del diálogo: {_config.battleMusicId}");
                            AudioService.Instance.EndBattleById(_config.battleMusicId);
                        }
                        else if (AudioService.Instance != null)
                        {
                            Debug.Log($"[Lifecycle] 🎵 Restaurando música después del diálogo (sin battleMusicId)");
                            AudioService.Instance.RestoreAfterBattle();
                        }
                    }
                    else
                    {
                        Debug.Log($"[Lifecycle] ℹ️ No se restaura música aún - esperando a que líder maneje la restauración");
                    }
                    
                    // ✅ Si es líder de equipo, notificar que terminó el diálogo
                    if (isInTeam && teamMember.IsLeader)
                    {
                        teamMember.Team.NotifyPostDefeatDialogueFinished();
                    }
                }
                else
                {
                    // Si no hay diálogo pero es líder, notificar inmediatamente
                    if (isInTeam && teamMember.IsLeader)
                    {
                        teamMember.Team.NotifyPostDefeatDialogueFinished();
                    }
                    
                    // ✅ Restaurar música si no hay diálogo
                    if (IsPlayerDeadOrGameOverActive())
                    {
                        Debug.Log("[Lifecycle] ⚠️ Jugador ya murió (game over en curso) — se omite la restauración de música (sin diálogo).");
                    }
                    else if (!isInTeam || (isInTeam && teamMember.IsLeader && isLastTeamMember))
                    {
                        if (!string.IsNullOrEmpty(_config?.battleMusicId) && AudioService.Instance != null)
                        {
                            Debug.Log($"[Lifecycle] 🎵 Restaurando música de batalla (sin diálogo): {_config.battleMusicId}");
                            AudioService.Instance.EndBattleById(_config.battleMusicId);
                        }
                        else if (AudioService.Instance != null)
                        {
                            Debug.Log($"[Lifecycle] 🎵 Restaurando música (sin diálogo, sin battleMusicId)");
                            AudioService.Instance.RestoreAfterBattle();
                        }
                    }
                }
            }
            
            // 3. Ejecutar acción post-derrota (si está configurada)
            Debug.Log($"[Lifecycle] 🔍 Verificando postDefeatAction para {name}: config={(_config != null ? "OK" : "NULL")}, action={_config?.postDefeatAction}");
            
            // ✅ Si es miembro de equipo (no líder) y el líder tiene moveTeamMembersOnDefeat activo,
            // NO ejecutar postDefeatAction individual porque el líder nos moverá
            bool skipPostDefeatBecauseLeaderWillMove = false;
            if (isInTeam && !teamMember.IsLeader)
            {
                var leaderConfig = teamMember.Team.Leader?.Configuration?.combatConfig;
                if (leaderConfig != null && 
                    leaderConfig.postDefeatAction == PostDefeatAction.MoveToAnchor && 
                    leaderConfig.moveTeamMembersOnDefeat)
                {
                    skipPostDefeatBecauseLeaderWillMove = true;
                    Debug.Log($"[Lifecycle] ℹ️ {name} omitirá postDefeatAction individual - el líder moverá al equipo");
                }
            }
            
            if (!skipPostDefeatBecauseLeaderWillMove && _config != null && _config.postDefeatAction != PostDefeatAction.None)
            {
                Debug.Log($"[Lifecycle] 🎬 {name} ejecutará acción post-derrota: {_config.postDefeatAction}");
                yield return HandlePostDefeatAction(_config.postDefeatAction);
            }
            else
            {
                Debug.Log($"[Lifecycle] ℹ️ {name} no tiene postDefeatAction configurada - configurando como interactuable");
                // 4. Configurar para interacción futura (Hablar con el NPC derrotado)
                SetupPostCombatInteraction();
            }
            
            Debug.Log($"[Lifecycle] ✅ Secuencia GetUpDizzy completada para {name}");
        }

        private IEnumerator HandlePostDefeatAction(PostDefeatAction action)
        {
            Debug.Log($"[Lifecycle] 🎬 Ejecutando acción post-derrota: {action}");
            
            switch (action)
            {
                case PostDefeatAction.FleeAndDisappear:
                    bool fleeMovementStarted = false;
                    // Huir del jugador
                    if (PlayerService.TryGetPlayer(out GameObject player))
                    {
                        Vector3 fleeDir = (transform.position - player.transform.position).normalized;
                        Vector3 fleePos = transform.position + fleeDir * 10f;

                        // Usar NavMesh para encontrar punto válido inicial
                        if (NavMesh.SamplePosition(fleePos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                        {
                            fleePos = hit.position;
                        }

                        // Activar movimiento
                        if (_agent != null)
                        {
                            _agent.enabled = true;

                            // ✅ VALIDACIÓN 1: Verificar que está en NavMesh
                            if (!_agent.isOnNavMesh)
                            {
                                Debug.LogWarning($"[Lifecycle] ⚠️ {name} NO está en NavMesh! No puede huir. Posición: {transform.position}");
                                if (NavMesh.SamplePosition(transform.position, out NavMeshHit repoHit, 2f, NavMesh.AllAreas))
                                {
                                    transform.position = repoHit.position;
                                    _agent.Warp(repoHit.position);
                                    Debug.Log($"[Lifecycle] 🔄 {name} recolocado en NavMesh: {repoHit.position}");
                                }
                                else
                                {
                                    Debug.LogWarning($"[Lifecycle] ⚠️ No se pudo recolocar {name} en NavMesh - se aplicará fallback de desaparición");
                                }
                            }

                            if (_agent.isOnNavMesh)
                            {
                                // Guardar configuración original
                                bool originalUpdateRotation = _agent.updateRotation;
                                float originalSpeed = _agent.speed;

                                _agent.isStopped = false;
                                _agent.updatePosition = true; // PlayDeath lo desactiva
                                _agent.updateRotation = false; // NPCSimpleAnimator maneja la rotación

                                if (!_agent.Warp(transform.position))
                                {
                                    Debug.LogWarning($"[Lifecycle] ⚠️ {name} no pudo hacer Warp a {transform.position} - se aplicará fallback de desaparición");
                                }
                                else if (!TryResolveReachableDestination(_agent, fleePos, out Vector3 resolvedFleePos))
                                {
                                    Debug.LogWarning($"[Lifecycle] ⚠️ {name} no encontró destino alcanzable para huida cerca de {fleePos}");
                                }
                                else
                                {
                                    fleePos = resolvedFleePos;
                                    _agent.SetDestination(fleePos);

                                    float pathWaitTime = 0f;
                                    while (_agent.pathPending && pathWaitTime < 1f)
                                    {
                                        pathWaitTime += Time.deltaTime;
                                        yield return null;
                                    }

                                    if (_agent.hasPath && _agent.path.status == NavMeshPathStatus.PathComplete)
                                    {
                                        if (_animator != null)
                                        {
                                            _animator.AllowManualRotation = false;
                                            _animator.EnableAutoRotation();
                                            _animator.TransitionToLocomotion();
                                            _animator.SetMovementSpeed(1f, 0.1f);
                                        }

                                        // ✅ EXTRA: Dar 1 frame para que el agent procese el destino
                                        yield return null;

                                        float fleeTime = 0f;
                                        float maxFleeTime = 2f;
                                        Vector3 lastPosition = transform.position;
                                        float stuckCheckInterval = 1f;
                                        float timeSinceLastCheck = 0f;

                                        while (fleeTime < maxFleeTime && _agent != null && _agent.isOnNavMesh)
                                        {
                                            if (_animator != null && _agent.velocity.sqrMagnitude > 0.01f)
                                            {
                                                float speed = _agent.velocity.magnitude / Mathf.Max(_agent.speed, 0.1f);
                                                _animator.SetMovementSpeed(speed, 0.1f);

                                                Vector3 moveDir = _agent.velocity.normalized;
                                                moveDir.y = 0f;
                                                if (moveDir.sqrMagnitude > 0.01f)
                                                {
                                                    _animator.FaceDirection(moveDir);
                                                }
                                            }

                                            timeSinceLastCheck += Time.deltaTime;
                                            if (timeSinceLastCheck >= stuckCheckInterval)
                                            {
                                                float distanceMoved = Vector3.Distance(transform.position, lastPosition);
                                                if (distanceMoved < 0.1f && _agent.remainingDistance > 0.5f)
                                                {
                                                    _agent.isStopped = true;
                                                    yield return null;
                                                    _agent.isStopped = false;
                                                    _agent.SetDestination(fleePos);
                                                }

                                                lastPosition = transform.position;
                                                timeSinceLastCheck = 0f;
                                            }

                                            fleeTime += Time.deltaTime;
                                            yield return null;
                                        }

                                        fleeMovementStarted = true;
                                    }
                                    else
                                    {
                                        Debug.LogWarning($"[Lifecycle] ⚠️ {name} no puede calcular path hacia {fleePos}! Status: {(_agent.hasPath ? _agent.path.status.ToString() : "NO PATH")}");
                                    }
                                }

                                // Detener y restaurar configuración
                                if (_agent != null && _agent.isOnNavMesh)
                                {
                                    _agent.isStopped = true;
                                    _agent.updateRotation = originalUpdateRotation;
                                    _agent.speed = originalSpeed;
                                }
                            }
                        }
                    }

                    if (!fleeMovementStarted)
                    {
                        Debug.LogWarning($"[Lifecycle] ⚠️ {name} no pudo ejecutar huida - aplicando desaparición directa");
                    }
                    
                    // ✅ Desaparecer con TransitionManager si está configurado
                    if (_config?.disappearTransition != null)
                    {
                        var transitionManager = EasyTransition.TransitionManager.Instance();
                        if (transitionManager != null)
                        {
                            Debug.Log($"[Lifecycle] 🎭 Iniciando transición de desaparición para {name}");
                            
                            bool transitionCompleted = false;
                            
                            // Callback para desactivar el NPC en el punto de corte
                            void OnCutPoint()
                            {
                                // Reproducir VFX si existe
                                if (_config?.disappearVFXPrefab != null)
                                {
                                    VfxPoolService.Instance.Play(_config.disappearVFXPrefab, transform.position + Vector3.up, Quaternion.identity, 3f);
                                }
                                
                                // Desactivar el GameObject
                                gameObject.SetActive(false);
                                transitionCompleted = true;
                                Debug.Log($"[Lifecycle] ✅ {name} desactivado durante transición");
                            }
                            
                            // Suscribirse al evento de punto de corte
                            transitionManager.onTransitionCutPointReached += OnCutPoint;
                            
                            // Iniciar transición sin cambio de escena
                            transitionManager.Transition(_config.disappearTransition, 0f);
                            
                            // Esperar a que complete
                            float timeout = 5f;
                            while (!transitionCompleted && timeout > 0f)
                            {
                                timeout -= Time.deltaTime;
                                yield return null;
                            }
                            
                            // Limpiar callback
                            transitionManager.onTransitionCutPointReached -= OnCutPoint;
                            
                            if (!transitionCompleted)
                            {
                                Debug.LogWarning($"[Lifecycle] ⚠️ Timeout esperando transición, desactivando {name} directamente");
                                gameObject.SetActive(false);
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"[Lifecycle] ⚠️ No se encontró TransitionManager, desapareciendo sin transición");
                            // Fallback: desaparecer sin transición
                            if (_config?.disappearVFXPrefab)
                                VfxPoolService.Instance.Play(_config.disappearVFXPrefab, transform.position + Vector3.up, Quaternion.identity, 3f);
                            gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        Debug.Log($"[Lifecycle] ℹ️ No hay TransitionSettings configurado, desapareciendo sin transición");
                        // Desaparecer sin transición (comportamiento original)
                        if (_config?.disappearVFXPrefab)
                            VfxPoolService.Instance.Play(_config.disappearVFXPrefab, transform.position + Vector3.up, Quaternion.identity, 3f);
                        gameObject.SetActive(false);
                    }
                    break;
                    
                case PostDefeatAction.ReturnToIdle:
                    // ✅ Transicionar de dizzy a idle
                    Debug.Log($"[Lifecycle] 🔄 {name} volviendo a idle desde dizzy");
                    
                    if (_animator != null)
                    {
                        _animator.TransitionToIdle();
                        yield return new WaitForSeconds(0.3f); // Esperar transición
                    }
                    
                    // Configurar como interactuable
                    SetupPostCombatInteraction();
                    break;
                    
                case PostDefeatAction.MoveToAnchor:
                    yield return HandleMoveToAnchor();
                    break;
            }
        }
        
        /// <summary>
        /// Maneja el movimiento del NPC (y su equipo si es líder) a un anchor después de la derrota
        /// </summary>
        private IEnumerator HandleMoveToAnchor()
        {
            if (string.IsNullOrEmpty(_config?.postDefeatMoveAnchor))
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ {name} tiene MoveToAnchor pero no hay anchor configurado");
                SetupPostCombatInteraction();
                yield break;
            }
            
            // Buscar el anchor de destino (primero por SpawnAnchor ID, luego por nombre de GameObject)
            var anchorTransform = ResolveAnchorTransform(_config.postDefeatMoveAnchor);
            if (anchorTransform == null)
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ No se encontró el anchor '{_config.postDefeatMoveAnchor}' para {name}");
                SetupPostCombatInteraction();
                yield break;
            }
            
            Vector3 targetPos = anchorTransform.position;
            Debug.Log($"[Lifecycle] 🚶 {name} moviéndose a anchor '{_config.postDefeatMoveAnchor}'");
            
            // Cancelar la secuencia dizzy para que no interfiera
            _shouldCancelDizzySequence = true;

            bool externalMovementOverridePushed = false;
            if (_manager != null)
            {
                _manager.PushExternalMovementOverride();
                externalMovementOverridePushed = true;
            }
            
            // ✅ Transicionar a animación de caminar
            if (_animator != null)
            {
                _animator.SetTalking(false);
                _animator.EndInteraction();
                _animator.TransitionToIdle();
                yield return new WaitForSeconds(0.3f);
                
                // ✅ Habilitar rotación automática ANTES de empezar a mover
                _animator.AllowManualRotation = false;
                _animator.EnableAutoRotation();
            }
            
            try
            {
                // Activar NavMeshAgent y mover
                if (_agent != null)
                {
                    // ✅ Validación exhaustiva del NavMeshAgent
                    if (!_agent.isOnNavMesh)
                    {
                        Debug.LogError($"[Lifecycle] ❌ {name} NO está en NavMesh! Posición: {transform.position}");
                        SetupPostCombatInteraction();
                        yield break;
                    }
                    
                    _agent.enabled = true;
                    _agent.isStopped = false;
                    
                    // ✅ Guardar configuración original para restaurar después
                    float originalSpeed = _agent.speed;
                    bool originalUpdateRotation = _agent.updateRotation;
                    
                    _agent.speed = originalSpeed * 0.5f; // Más lento de lo normal
                    _agent.updateRotation = false; // NPCSimpleAnimator maneja la rotación
                    
                    // ✅ CRÍTICO: Establecer destino y verificar que el path es válido
                    _agent.SetDestination(targetPos);
                    
                    // Esperar un frame para que Unity calcule el path
                    yield return null;
                    
                    // ✅ Verificar que el path es válido
                    if (_agent.pathPending)
                    {
                        float waitTime = 0f;
                        while (_agent.pathPending && waitTime < 1f)
                        {
                            waitTime += Time.deltaTime;
                            yield return null;
                        }
                    }
                    
                    if (_agent.path.status != NavMeshPathStatus.PathComplete)
                    {
                        Debug.LogError($"[Lifecycle] ❌ {name} no puede calcular path hacia {targetPos}! Status: {_agent.path.status}");
                        SetupPostCombatInteraction();
                        yield break;
                    }
                    
                    Debug.Log($"[Lifecycle] ✅ {name} iniciando movimiento:\n" +
                             $"  Destino: {targetPos}\n" +
                             $"  Distancia: {Vector3.Distance(transform.position, targetPos):F2}m\n" +
                             $"  Speed: {_agent.speed}\n" +
                             $"  Path Status: {_agent.path.status}\n" +
                             $"  UpdateRotation: {_agent.updateRotation}");
                    
                    // Loop de movimiento con verificación continua
                    float timeout = 30f;
                    float elapsed = 0f;
                    float stuckCheckInterval = 1f;
                    float lastStuckCheck = 0f;
                    Vector3 lastCheckPos = transform.position;
                    
                    while (elapsed < timeout)
                    {
                        if (_agent == null || !_agent.isOnNavMesh) break;
                        
                        // ✅ Verificar si está "atascado" (no se ha movido en 1 segundo)
                        if (elapsed - lastStuckCheck >= stuckCheckInterval)
                        {
                            float movedDistance = Vector3.Distance(transform.position, lastCheckPos);
                            if (movedDistance < 0.1f && !_agent.pathPending)
                            {
                                Debug.LogWarning($"[Lifecycle] ⚠️ {name} parece estar atascado (movido {movedDistance:F3}m en {stuckCheckInterval}s)");
                                
                                // Intentar reconfigurar el agent
                                _agent.isStopped = true;
                                yield return new WaitForSeconds(0.1f);
                                _agent.isStopped = false;
                                _agent.SetDestination(targetPos);
                                
                                Debug.Log($"[Lifecycle] 🔄 {name} reconfigurado - Velocity: {_agent.velocity.magnitude:F2}, IsOnNavMesh: {_agent.isOnNavMesh}");
                            }
                            
                            lastStuckCheck = elapsed;
                            lastCheckPos = transform.position;
                        }
                        
                        // ✅ CRÍTICO: Actualizar animación CADA FRAME
                        if (_animator != null)
                        {
                            float agentSpeed = _agent.velocity.magnitude;
                            
                            // ✅ Log si velocity es 0 pero debería moverse
                            if (agentSpeed < 0.01f && !_agent.pathPending && _agent.remainingDistance > _agent.stoppingDistance + 0.5f)
                            {
                                if (Time.frameCount % 60 == 0) // Cada 60 frames (~1 segundo)
                                {
                                    Debug.LogWarning($"[Lifecycle] ⚠️ {name} velocity = 0 pero debería moverse!\n" +
                                                   $"  remainingDistance: {_agent.remainingDistance:F2}\n" +
                                                   $"  stoppingDistance: {_agent.stoppingDistance:F2}\n" +
                                                   $"  isStopped: {_agent.isStopped}\n" +
                                                   $"  pathPending: {_agent.pathPending}\n" +
                                                   $"  path.status: {_agent.path.status}");
                                }
                            }
                            
                            if (agentSpeed > 0.01f)
                            {
                                // Calcular velocidad normalizada (0 = parado, 1 = velocidad máxima)
                                float maxSpeed = Mathf.Max(_agent.speed, 0.1f);
                                float normalizedSpeed = Mathf.Clamp01(agentSpeed / maxSpeed);
                                
                                // Aplicar velocidad más lenta (derrotado)
                                _animator.SetMovementSpeed(normalizedSpeed * 0.5f, 0.1f);
                                
                                // ✅ Rotar hacia la dirección del movimiento
                                Vector3 moveDir = _agent.velocity.normalized;
                                moveDir.y = 0f;
                                if (moveDir.sqrMagnitude > 0.01f)
                                {
                                    _animator.FaceDirection(moveDir);
                                }
                            }
                            else
                            {
                                // Si no se está moviendo, velocidad 0
                                _animator.SetMovementSpeed(0f, 0.1f);
                            }
                        }
                        
                        // Verificar si llegó
                        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                        {
                            Debug.Log($"[Lifecycle] ✅ {name} llegó a su destino (elapsed: {elapsed:F1}s)");
                            break;
                        }
                        
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                    
                    if (elapsed >= timeout)
                    {
                        Debug.LogWarning($"[Lifecycle] ⏱️ {name} timeout alcanzado ({timeout}s) - Forzando parada");
                    }
                    
                    // Detener movimiento y restaurar configuración
                    _agent.isStopped = true;
                    _agent.speed = originalSpeed;
                    _agent.updateRotation = originalUpdateRotation;
                    
                    if (_animator != null)
                    {
                        _animator.SetMovementSpeed(0f, 0.1f);
                        _animator.TransitionToIdle();
                    }
                }
            }
            finally
            {
                if (externalMovementOverridePushed && _manager != null)
                {
                    _manager.PopExternalMovementOverride();
                }
            }

            // Si es líder de equipo y debe mover a los compañeros, hacerlo ANTES de desaparecer.
            var teamMember = GetComponent<NPCTeamMember>();
            if (teamMember != null && teamMember.IsLeader && _config.moveTeamMembersOnDefeat)
            {
                yield return MoveTeamMembersToRandomPoints();
            }
            
            // ¿Desaparecer al llegar?
            if (_config.disappearOnArrival)
            {
                if (_config.disappearOnArrivalVFX != null)
                {
                    VfxPoolService.Instance.Play(_config.disappearOnArrivalVFX, transform.position + Vector3.up, Quaternion.identity, 3f);
                }
                
                yield return new WaitForSeconds(0.5f);
                gameObject.SetActive(false);
                Debug.Log($"[Lifecycle] 👋 {name} desapareció al llegar a destino");
            }
            else
            {
                // Configurar como interactuable
                SetupPostCombatInteraction();
            }
        }
        
        /// <summary>
        /// Mueve a los miembros del equipo a sus anchors configurados o puntos aleatorios si no tienen
        /// </summary>
        private IEnumerator MoveTeamMembersToRandomPoints()
        {
            var teamMember = GetComponent<NPCTeamMember>();
            if (teamMember == null || teamMember.Team == null) yield break;
            
            var team = teamMember.Team;
            var members = team.AllMembers;
            
            foreach (var member in members)
            {
                if (member == null || member.gameObject == gameObject) continue;
                if (!member.gameObject.activeInHierarchy) continue;
                
                var memberHandler = member.GetComponent<NPCCombatLifecycleHandler>();
                if (memberHandler != null)
                {
                    memberHandler.CancelDizzySequence();
                }
                
                // ✅ FIX: Primero intentar usar el anchor individual del miembro
                Vector3 targetPos;
                var memberConfig = member.Configuration?.combatConfig;
                bool hasIndividualAnchor = false;
                
                if (memberConfig != null && 
                    memberConfig.postDefeatAction == PostDefeatAction.MoveToAnchor && 
                    !string.IsNullOrEmpty(memberConfig.postDefeatMoveAnchor))
                {
                    // Buscar el anchor individual del miembro
                    var memberAnchor = SpawnAnchor.FindById(memberConfig.postDefeatMoveAnchor);
                    if (memberAnchor != null)
                    {
                        targetPos = memberAnchor.transform.position;
                        hasIndividualAnchor = true;
                        Debug.Log($"[Lifecycle] 🚶 Miembro {member.name} moviéndose a su SpawnAnchor: '{memberConfig.postDefeatMoveAnchor}'");
                    }
                    else
                    {
                        // Fallback: buscar por nombre
                        var anchorGo = GameObject.Find(memberConfig.postDefeatMoveAnchor);
                        if (anchorGo != null)
                        {
                            targetPos = anchorGo.transform.position;
                            hasIndividualAnchor = true;
                            Debug.Log($"[Lifecycle] 🚶 Miembro {member.name} moviéndose a su anchor (GameObject): '{memberConfig.postDefeatMoveAnchor}'");
                        }
                        else
                        {
                            // No se encontró el anchor, usar punto aleatorio
                            Vector3 randomOffset = Random.insideUnitSphere * 5f;
                            randomOffset.y = 0;
                            targetPos = transform.position + randomOffset;
                            Debug.LogWarning($"[Lifecycle] ⚠️ No se encontró anchor '{memberConfig.postDefeatMoveAnchor}' para {member.name}, usando punto aleatorio");
                        }
                    }
                }
                else
                {
                    // Sin anchor configurado, mover a punto aleatorio cerca del líder
                    Vector3 randomOffset = Random.insideUnitSphere * 5f;
                    randomOffset.y = 0;
                    targetPos = transform.position + randomOffset;
                    Debug.Log($"[Lifecycle] 🚶 Miembro {member.name} sin anchor configurado, moviendo a punto aleatorio");
                }
                
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    targetPos = hit.position;
                }
                
                var memberAgent = member.Agent;
                var memberAnimator = member.GetComponent<NPCSimpleAnimator>();
                
                if (memberAgent != null)
                {
                    // ✅ Validación exhaustiva del NavMeshAgent
                    if (!memberAgent.isOnNavMesh)
                    {
                        Debug.LogError($"[Lifecycle] ❌ Miembro {member.name} NO está en NavMesh! Posición: {member.transform.position}");
                        continue; // Saltar este miembro
                    }
                    
                    memberAgent.enabled = true;
                    memberAgent.isStopped = false;
                    
                    // ✅ Guardar configuración original
                    float originalSpeed = memberAgent.speed;
                    bool originalUpdateRotation = memberAgent.updateRotation;
                    
                    memberAgent.speed = originalSpeed * 0.5f;
                    memberAgent.updateRotation = false; // NPCSimpleAnimator maneja la rotación
                    member.PushExternalMovementOverride();
                    
                    // ✅ Establecer destino y esperar cálculo de path
                    memberAgent.SetDestination(targetPos);
                    
                    if (memberAnimator != null)
                    {
                        memberAnimator.SetTalking(false);
                        memberAnimator.EndInteraction();
                        memberAnimator.TransitionToIdle();
                        yield return new WaitForSeconds(0.1f); // Pequeña espera para transición
                        
                        // ✅ Habilitar rotación automática
                        memberAnimator.AllowManualRotation = false;
                        memberAnimator.EnableAutoRotation();
                    }
                    
                    // Esperar un frame para que Unity calcule el path
                    yield return null;
                    
                    // ✅ Verificar que el path es válido
                    if (memberAgent.pathPending)
                    {
                        float waitTime = 0f;
                        while (memberAgent.pathPending && waitTime < 1f)
                        {
                            waitTime += Time.deltaTime;
                            yield return null;
                        }
                    }
                    
                    if (memberAgent.path.status != NavMeshPathStatus.PathComplete)
                    {
                        Debug.LogError($"[Lifecycle] ❌ Miembro {member.name} no puede calcular path hacia {targetPos}! Status: {memberAgent.path.status}");
                        member.PopExternalMovementOverride();
                        continue; // Saltar este miembro
                    }
                    
                    Debug.Log($"[Lifecycle] ✅ Miembro {member.name} iniciando movimiento:\n" +
                             $"  Destino: {targetPos}\n" +
                             $"  Distancia: {Vector3.Distance(member.transform.position, targetPos):F2}m\n" +
                             $"  Speed: {memberAgent.speed}\n" +
                             $"  Path Status: {memberAgent.path.status}\n" +
                             $"  UpdateRotation: {memberAgent.updateRotation}");
                    
                    // ✅ Iniciar corrutina para esperar llegada y manejar post-acción
                    member.StartCoroutine(WaitForMemberArrivalAndHandle(member, memberConfig, hasIndividualAnchor, originalSpeed, originalUpdateRotation, true));
                }
            }
            
            // Pequeña espera para que empiecen a moverse
            yield return new WaitForSeconds(0.5f);
        }
        
        /// <summary>
        /// Espera a que un miembro llegue a su destino y maneja la post-acción
        /// </summary>
        private IEnumerator WaitForMemberArrivalAndHandle(NPCBehaviourManagerV2 member, NPCCombatConfig config, bool hasIndividualAnchor, float originalSpeed, bool originalUpdateRotation, bool releaseExternalMovementOverride)
        {
            if (member == null || member.Agent == null) yield break;
            
            var agent = member.Agent;
            float timeout = 30f;
            float elapsed = 0f;
            float stuckCheckInterval = 1f;
            float lastStuckCheck = 0f;
            Vector3 lastCheckPos = member.transform.position;
            
            while (elapsed < timeout && member != null && agent != null && agent.isOnNavMesh)
            {
                // ✅ Verificar si está "atascado" (no se ha movido en 1 segundo)
                if (elapsed - lastStuckCheck >= stuckCheckInterval)
                {
                    float movedDistance = Vector3.Distance(member.transform.position, lastCheckPos);
                    if (movedDistance < 0.1f && !agent.pathPending)
                    {
                        Debug.LogWarning($"[Lifecycle] ⚠️ Miembro {member.name} parece estar atascado (movido {movedDistance:F3}m en {stuckCheckInterval}s)");
                        
                        // Intentar reconfigurar el agent
                        Vector3 currentDest = agent.destination;
                        agent.isStopped = true;
                        yield return new WaitForSeconds(0.1f);
                        agent.isStopped = false;
                        agent.SetDestination(currentDest);
                        
                        Debug.Log($"[Lifecycle] 🔄 Miembro {member.name} reconfigurado - Velocity: {agent.velocity.magnitude:F2}");
                    }
                    
                    lastStuckCheck = elapsed;
                    lastCheckPos = member.transform.position;
                }
                
                // ✅ Actualizar animación CADA FRAME
                var animator = member.GetComponent<NPCSimpleAnimator>();
                if (animator != null)
                {
                    float agentSpeed = agent.velocity.magnitude;
                    
                    // ✅ Log si velocity es 0 pero debería moverse
                    if (agentSpeed < 0.01f && !agent.pathPending && agent.remainingDistance > agent.stoppingDistance + 0.5f)
                    {
                        if (Time.frameCount % 60 == 0) // Cada 60 frames
                        {
                            Debug.LogWarning($"[Lifecycle] ⚠️ Miembro {member.name} velocity = 0 pero debería moverse!\n" +
                                           $"  remainingDistance: {agent.remainingDistance:F2}\n" +
                                           $"  stoppingDistance: {agent.stoppingDistance:F2}\n" +
                                           $"  isStopped: {agent.isStopped}\n" +
                                           $"  pathPending: {agent.pathPending}\n" +
                                           $"  path.status: {agent.path.status}");
                        }
                    }
                    
                    if (agentSpeed > 0.01f)
                    {
                        // Calcular velocidad normalizada
                        float maxSpeed = Mathf.Max(agent.speed, 0.1f);
                        float normalizedSpeed = Mathf.Clamp01(agentSpeed / maxSpeed);
                        
                        // Aplicar velocidad más lenta (derrotado)
                        animator.SetMovementSpeed(normalizedSpeed * 0.5f, 0.1f);
                        
                        // Rotar hacia la dirección del movimiento
                        Vector3 moveDir = agent.velocity.normalized;
                        moveDir.y = 0f;
                        if (moveDir.sqrMagnitude > 0.01f)
                        {
                            animator.FaceDirection(moveDir);
                        }
                    }
                    else
                    {
                        // Si no se está moviendo, velocidad 0
                        animator.SetMovementSpeed(0f, 0.1f);
                    }
                }
                
                // Verificar si llegó
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                {
                    Debug.Log($"[Lifecycle] ✅ Miembro {member.name} llegó a su destino (elapsed: {elapsed:F1}s)");
                    break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[Lifecycle] ⏱️ Miembro {member.name} timeout alcanzado ({timeout}s)");
            }
            
            // Detener movimiento y restaurar configuración
            if (member != null && agent != null)
            {
                agent.isStopped = true;
                agent.speed = originalSpeed;
                agent.updateRotation = originalUpdateRotation;
                
                var animator = member.GetComponent<NPCSimpleAnimator>();
                if (animator != null)
                {
                    animator.SetMovementSpeed(0f, 0.1f);
                    animator.TransitionToIdle();
                }
                
                // ¿Desaparecer al llegar?
                if (config != null && config.disappearOnArrival)
                {
                    if (releaseExternalMovementOverride)
                    {
                        member.PopExternalMovementOverride();
                        releaseExternalMovementOverride = false;
                    }

                    if (config.disappearOnArrivalVFX != null)
                    {
                        VfxPoolService.Instance.Play(config.disappearOnArrivalVFX, member.transform.position + Vector3.up, Quaternion.identity, 3f);
                    }
                    
                    yield return new WaitForSeconds(0.5f);
                    member.gameObject.SetActive(false);
                    Debug.Log($"[Lifecycle] 👋 Miembro {member.name} desapareció al llegar a destino");
                }
                else
                {
                    // Configurar como interactuable
                    var memberHandler = member.GetComponent<NPCCombatLifecycleHandler>();
                    if (memberHandler != null)
                    {
                        memberHandler.SetupPostCombatInteraction();
                    }
                    Debug.Log($"[Lifecycle] ✅ Miembro {member.name} configurado como interactuable en destino");
                }
            }

            if (releaseExternalMovementOverride && member != null)
            {
                member.PopExternalMovementOverride();
            }
        }

        private bool TryResolveReachableDestination(NavMeshAgent agent, Vector3 targetPos, out Vector3 resolvedPos)
        {
            resolvedPos = targetPos;
            NavMeshPath path = new NavMeshPath();
            
            if (agent.CalculatePath(targetPos, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    resolvedPos = targetPos;
                    return true;
                }
                
                if (path.status == NavMeshPathStatus.PathPartial && path.corners.Length > 0)
                {
                    resolvedPos = path.corners[path.corners.Length - 1];
                    return true;
                }
            }
            
            return false;
        }

        private Transform ResolveAnchorTransform(string anchorIdOrName)
        {
            if (string.IsNullOrWhiteSpace(anchorIdOrName))
                return null;

            var spawnAnchor = SpawnAnchor.FindById(anchorIdOrName);
            if (spawnAnchor != null)
                return spawnAnchor.transform;

            var anchorGo = GameObject.Find(anchorIdOrName);
            return anchorGo != null ? anchorGo.transform : null;
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
        
        public void SetupPostCombatInteraction()
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