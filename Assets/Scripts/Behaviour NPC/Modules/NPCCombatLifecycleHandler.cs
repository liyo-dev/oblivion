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
                    Debug.Log($"[Lifecycle] 👥 {name} (NO es líder) - esperando a que el líder termine el diálogo...");
                    
                    // Esperar a que el líder termine el diálogo
                    while (!team.IsPostDefeatDialogueFinished)
                    {
                        if (_shouldCancelDizzySequence) yield break;
                        yield return null;
                    }
                    
                    Debug.Log($"[Lifecycle] ✅ Líder terminó diálogo - {name} procede a post-acción");
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
                    
                    if (!isInTeam || (isInTeam && teamMember.IsLeader && isLastTeamMember))
                    {
                        if (!string.IsNullOrEmpty(_config?.battleMusicId) && AudioService.Instance != null)
                        {
                            Debug.Log($"[Lifecycle] 🎵 Restaurando música de batalla después del diálogo: {_config.battleMusicId}");
                            AudioService.Instance.EndBattleById(_config.battleMusicId);
                        }
                        else
                        {
                            Debug.LogWarning($"[Lifecycle] ⚠️ No se puede restaurar música - battleMusicId es '{_config?.battleMusicId}', AudioService existe: {AudioService.Instance != null}");
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
                    if (!isInTeam || (isInTeam && teamMember.IsLeader && isLastTeamMember))
                    {
                        if (!string.IsNullOrEmpty(_config?.battleMusicId) && AudioService.Instance != null)
                        {
                            Debug.Log($"[Lifecycle] 🎵 Restaurando música de batalla (sin diálogo): {_config.battleMusicId}");
                            AudioService.Instance.EndBattleById(_config.battleMusicId);
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
                    // Huir del jugador
                    if (PlayerService.TryGetPlayer(out GameObject player))
                    {
                        Vector3 fleeDir = (transform.position - player.transform.position).normalized;
                        Vector3 fleePos = transform.position + fleeDir * 10f;
                        
                        // Usar NavMesh para encontrar punto válido
                        if (NavMesh.SamplePosition(fleePos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                        {
                            fleePos = hit.position;
                        }
                        
                        // Activar movimiento
                        if (_agent != null)
                        {
                            _agent.enabled = true;
                            _agent.isStopped = false;
                            _agent.SetDestination(fleePos);
                            
                            // Animar
                            if (_animator != null)
                            {
                                _animator.SetMovementSpeed(1f);
                            }
                            
                            // Esperar un poco mientras huye
                            yield return new WaitForSeconds(2f);
                        }
                    }
                    
                    // Desaparecer con VFX
                    if (_config?.disappearVFXPrefab)
                        Instantiate(_config.disappearVFXPrefab, transform.position + Vector3.up, Quaternion.identity);
                        
                    gameObject.SetActive(false);
                    break;
                    
                case PostDefeatAction.ReturnToIdle:
                    // Volver a estado idle/interactuable
                    SetupPostCombatInteraction();
                    
                    // Si tiene animator, volver a idle
                    if (_animator != null)
                    {
                        _animator.TransitionToIdle();
                    }
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
            
            // Buscar el anchor de destino
            var anchor = GameObject.Find(_config.postDefeatMoveAnchor);
            if (anchor == null)
            {
                Debug.LogWarning($"[Lifecycle] ⚠️ No se encontró el anchor '{_config.postDefeatMoveAnchor}' para {name}");
                SetupPostCombatInteraction();
                yield break;
            }
            
            Vector3 targetPos = anchor.transform.position;
            Debug.Log($"[Lifecycle] 🚶 {name} moviéndose a anchor '{_config.postDefeatMoveAnchor}'");
            
            // Cancelar la secuencia dizzy para que no interfiera
            _shouldCancelDizzySequence = true;
            
            // Transicionar a animación de caminar
            if (_animator != null)
            {
                _animator.TransitionToIdle();
                yield return new WaitForSeconds(0.3f);
                _animator.SetMovementSpeed(0.5f); // Caminar lento (derrotado)
                
                // ✅ FIX: Habilitar rotación automática y rotar hacia el destino
                _animator.AllowManualRotation = false;
                _animator.EnableAutoRotation();
                Vector3 directionToTarget = targetPos - transform.position;
                directionToTarget.y = 0f;
                if (directionToTarget.sqrMagnitude > 0.01f)
                {
                    _animator.FaceDirection(directionToTarget.normalized);
                }
            }
            
            // Activar NavMeshAgent y mover
            if (_agent != null)
            {
                _agent.enabled = true;
                _agent.isStopped = false;
                _agent.speed = _agent.speed * 0.5f; // Más lento de lo normal
                _agent.SetDestination(targetPos);
                
                // Esperar a que llegue
                float timeout = 30f;
                float elapsed = 0f;
                
                while (elapsed < timeout)
                {
                    if (_agent == null || !_agent.isOnNavMesh) break;
                    
                    // Actualizar animación de movimiento
                    if (_animator != null)
                    {
                        float speed = _agent.velocity.magnitude / _agent.speed;
                        _animator.SetMovementSpeed(speed * 0.5f);
                        
                        // ✅ FIX: Rotar hacia la dirección del movimiento
                        if (_agent.velocity.sqrMagnitude > 0.01f)
                        {
                            _animator.FaceDirection(_agent.velocity.normalized);
                        }
                    }
                    
                    // Verificar si llegó
                    if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.1f)
                    {
                        Debug.Log($"[Lifecycle] ✅ {name} llegó a su destino");
                        break;
                    }
                    
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                
                // Detener movimiento
                _agent.isStopped = true;
                if (_animator != null)
                {
                    _animator.SetMovementSpeed(0f);
                    _animator.TransitionToIdle();
                }
            }
            
            // ¿Desaparecer al llegar?
            if (_config.disappearOnArrival)
            {
                if (_config.disappearOnArrivalVFX != null)
                {
                    Instantiate(_config.disappearOnArrivalVFX, transform.position + Vector3.up, Quaternion.identity);
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
            
            // Si es líder de equipo y debe mover a los compañeros
            var teamMember = GetComponent<NPCTeamMember>();
            if (teamMember != null && teamMember.IsLeader && _config.moveTeamMembersOnDefeat)
            {
                yield return MoveTeamMembersToRandomPoints();
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
                
                if (memberAgent != null && memberAgent.isOnNavMesh)
                {
                    memberAgent.enabled = true;
                    memberAgent.isStopped = false;
                    memberAgent.speed = memberAgent.speed * 0.5f;
                    memberAgent.SetDestination(targetPos);
                    
                    if (memberAnimator != null)
                    {
                        memberAnimator.TransitionToIdle();
                        memberAnimator.SetMovementSpeed(0.5f);
                        
                        // ✅ FIX: Habilitar rotación y rotar hacia el destino
                        memberAnimator.AllowManualRotation = false;
                        memberAnimator.EnableAutoRotation();
                        Vector3 directionToTarget = targetPos - member.transform.position;
                        directionToTarget.y = 0f;
                        if (directionToTarget.sqrMagnitude > 0.01f)
                        {
                            memberAnimator.FaceDirection(directionToTarget.normalized);
                        }
                    }
                    
                    Debug.Log($"[Lifecycle] 🚶 Miembro de equipo {member.name} moviéndose a destino: {targetPos}");
                    
                    // ✅ FIX: Iniciar corrutina para esperar llegada y manejar post-acción
                    member.StartCoroutine(WaitForMemberArrivalAndHandle(member, memberConfig, hasIndividualAnchor));
                }
            }
            
            // Pequeña espera para que empiecen a moverse
            yield return new WaitForSeconds(0.5f);
        }
        
        /// <summary>
        /// Espera a que un miembro llegue a su destino y maneja la post-acción
        /// </summary>
        private IEnumerator WaitForMemberArrivalAndHandle(NPCBehaviourManagerV2 member, NPCCombatConfig config, bool hasIndividualAnchor)
        {
            if (member == null || member.Agent == null) yield break;
            
            var agent = member.Agent;
            float timeout = 30f;
            float elapsed = 0f;
            
            while (elapsed < timeout && member != null && agent != null && agent.isOnNavMesh)
            {
                // Actualizar animación
                var animator = member.GetComponent<NPCSimpleAnimator>();
                if (animator != null)
                {
                    float speed = agent.velocity.magnitude / Mathf.Max(agent.speed, 0.1f);
                    animator.SetMovementSpeed(speed * 0.5f);
                    
                    // Rotar hacia la dirección del movimiento
                    if (agent.velocity.sqrMagnitude > 0.01f)
                    {
                        animator.FaceDirection(agent.velocity.normalized);
                    }
                }
                
                // Verificar si llegó
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
                {
                    Debug.Log($"[Lifecycle] ✅ Miembro {member.name} llegó a su destino");
                    break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Detener movimiento
            if (member != null && agent != null)
            {
                agent.isStopped = true;
                var animator = member.GetComponent<NPCSimpleAnimator>();
                if (animator != null)
                {
                    animator.SetMovementSpeed(0f);
                    animator.TransitionToIdle();
                }
                
                // ¿Desaparecer al llegar?
                if (config != null && config.disappearOnArrival)
                {
                    if (config.disappearOnArrivalVFX != null)
                    {
                        Instantiate(config.disappearOnArrivalVFX, member.transform.position + Vector3.up, Quaternion.identity);
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
