using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Alerta: Detecta al jugador, reproduce animaciones de aviso (Sense/Challenge) y transiciona a Combate.
    /// </summary>
    public class AlertState : NPCStateBase
    {
        public override string StateName => "Alert";

        // Configuración
        private readonly float _alertDuration;
        private readonly bool _walkTowardsPlayer;
        private readonly float _stopDistance;
        private readonly bool _skipDialogue; // ✅ FIX: Para miembros no-líderes de equipos

        // Estado Interno
        private float _timer;
        private bool _waitingForDialogue;
        private bool _hasPlayedChallenge;
        private bool _dialogueCompleted;
        private NPCTeamMember _teamMember; // ✅ FIX #10: cacheado en OnEnter, no GetComponent cada frame en OnUpdate
        // Congelado del jugador al detectar (ver NPCCombatConfig.freezePlayerOnAlert). Solo se
        // aplica cuando este AlertState es el que lleva la voz (skipDialogue=false) - los
        // miembros no-líderes de un equipo (skipDialogue=true) nunca congelan por su cuenta, eso
        // lo gestiona NPCCombatTeam.Co_DetectAndEngage() una sola vez para todo el equipo.
        private bool _playerFrozenByAlert;

        // Timers de Animación
        private float _senseTimer;
        private const float SENSE_DURATION = 1.2f;

        // ✅ MEJORA (1 sep 2026, petición de Raúl): "cuando Lety y Vicky me ven, el jugador se
        // congela, las dos caminan hacia mí y ENTONCES empieza el diálogo — ese es el
        // comportamiento correcto y debe ser igual para cualquier NPC de combate". Antes, un NPC
        // en solitario con walk=true (p.ej. Boy_Pirate) arrancaba el diálogo con un simple
        // temporizador (_dialogueStartDelay) sin importar la distancia real al jugador — podía
        // soltar su frase desde lejos, quieto, y solo caminaba DESPUÉS si es que llegaba a
        // hacerlo. Ahora hay una fase de aproximación explícita (ver _hasArrivedForDialogue /
        // MAX_APPROACH_TIME más abajo) que camina hacia el jugador hasta llegar a _stopDistance
        // (o hasta un límite de seguridad) ANTES de dejar correr el temporizador de inicio de
        // diálogo. Para NPCs estáticos (walk=false) no cambia nada: siguen hablando en el sitio.
        private bool _hasArrivedForDialogue;
        private float _approachTimer;
        private const float MAX_APPROACH_TIME = 8f; // salvaguarda: pathing bloqueado, jugador huye, etc. — no caminar para siempre antes de hablar

        // ✅ MEJORA (1 sep 2026, petición de Raúl): el diálogo de alerta (y por tanto el corte a
        // la cámara de diálogo) arrancaba en el mismo frame que el icono de detección y la
        // animación SenseSomethingStart_NoWeapon, cortándolos a medias. Se retrasó el arranque
        // del diálogo hasta que ambos hubieran tenido tiempo de verse.
        //
        // ✅ FIX 4 sep 2026 (petición de Raúl: "tarda muchísimo en empezar el diálogo, hay que
        // acortarlo"): aquel retraso se nivelaba con combatConfig.alertIconDuration (2s por
        // defecto), pero eso ya NO hace falta — desde que existe la fase de aproximación
        // (_hasArrivedForDialogue arriba), el icono y la animación de sobresalto tienen de sobra
        // los 1.2s de SENSE_DURATION iniciales MÁS todo lo que dura la caminata hacia el jugador
        // para completarse, antes de que este contador siquiera empiece a correr. Reutilizar
        // alertIconDuration aquí sumaba hasta 2s más de espera, quieto, innecesarios. Se sustituye
        // por una pausa fija breve (POST_ARRIVAL_DIALOGUE_DELAY) — un respiro para que se note la
        // llegada antes de hablar, sin la espera larga.
        private const float POST_ARRIVAL_DIALOGUE_DELAY = 0.4f;
        private float _dialogueStartDelay;
        private bool _dialogueStarted;

        public AlertState(float duration = 2f, bool walk = true, float stopDist = 3f, bool skipDialogue = false)
        {
            _alertDuration = duration;
            _walkTowardsPlayer = walk;
            _stopDistance = stopDist;
            _skipDialogue = skipDialogue;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            if (context.WasDefeatedInCombat)
            {
                context.Log("[AlertState] ⛔ NPC derrotado. Cancelando alerta.");
                return;
            }

            context.Log("[AlertState] ⚠️ INICIANDO ALERTA");

            // Congelar al jugador en el instante de la detección (si procede), igual que ya
            // hace el Rey vía el módulo narrativo. Solo para el AlertState "principal" (no para
            // miembros de equipo con skipDialogue=true, ver comentario del campo arriba) - evita
            // que dos sistemas de detección compitan por el mismo NPC.
            _playerFrozenByAlert = false;
            if (!_skipDialogue)
            {
                var freezeConfig = context.Config?.combatConfig;
                if (freezeConfig != null && freezeConfig.freezePlayerOnAlert && global::Core.PlayerInputManager.Instance != null)
                {
                    global::Core.PlayerInputManager.Instance.PushUIMode();
                    _playerFrozenByAlert = true;
                }
            }

            // ✅ FIX #10: cachear una vez, en vez de GetComponent en cada frame de OnUpdate
            _teamMember = context.Transform.GetComponent<NPCTeamMember>();

            // FIX INC-019: el icono de detección (❗) estaba definido en NPCCombatConfig
            // (alertIconPrefab/questionIconPrefab/exclamationIconPrefab) y el sistema que lo
            // dibuja (NPCAlertIconController) ya existía, pero solo se usaba dentro del
            // minigame de Tag — ningún NPC normal lo mostraba al detectar al jugador porque
            // WanderState/IdleState solo reutilizaban alertIconDuration como duración de estado,
            // sin nunca invocar ShowExclamation(). Se aplica aquí para que TODOS los NPCs con
            // combatConfig.isAggressive muestren el icono al entrar en alerta.
            ShowDetectionIcon(context);

            // 1. Audio
            TriggerAlertMusic(context);

            // 2. Detenerse y Girar (reacción inicial en el sitio, antes de aproximarse)
            StopMovement(context);
            if (context.Player != null)
            {
                Vector3 dir = (context.Player.position - context.Transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                    context.Transform.rotation = Quaternion.LookRotation(dir);
            }

            // 3. Secuencia de Animación Inicial: "Sense Something"
            if (context.Animator != null)
            {
                context.Animator.PlaySenseSomething();
                _senseTimer = SENSE_DURATION;
                // Aún NO activamos BattleMode para dejar que la animación de alerta se reproduzca limpia
            }

            // 4. Fase de aproximación (ver comentario del campo _hasArrivedForDialogue arriba):
            // arranca en OnUpdate en cuanto termina el Sense. Los NPCs estáticos (walk=false)
            // pasan directo al diálogo, igual que antes.
            _hasArrivedForDialogue = false;
            _approachTimer = 0f;

            // 5. Iniciar Diálogo (si existe) — retrasado hasta llegar a distancia de diálogo (o de
            // inmediato si no camina), ver _dialogueStartDelay más arriba. No se llama a
            // StartAlertDialogue aquí directamente: se dispara desde OnUpdate.
            _dialogueStartDelay = POST_ARRIVAL_DIALOGUE_DELAY;
            _dialogueStarted = false;
        }

        public override void OnUpdate(NPCStateContext context)
        {
            if (context.Player == null) return;

            // A. Gestionar Secuencia de Animación (reacción inicial en el sitio)
            if (_senseTimer > 0)
            {
                _senseTimer -= Time.deltaTime;
                if (_senseTimer <= 0 && !_hasPlayedChallenge)
                {
                    // Al terminar Sense, reproducir Challenge y activar modo batalla
                    if (context.Animator != null)
                    {
                        context.Animator.PlayChallengingForBattle();
                        context.Animator.SetBattleMode(true); // AHORA sí entramos en pose de combate
                    }
                    _hasPlayedChallenge = true;
                }
                // Mientras dura la reacción inicial, ni caminar ni empezar diálogo todavía.
                return;
            }

            // A.1. Fase de aproximación: caminar hacia el jugador ANTES de hablar (mismo
            // comportamiento que ya se sentía bien en Lety/Vicky, generalizado aquí a cualquier
            // NPC con walk=true). Los NPCs estáticos (walk=false, p.ej. miembros de equipo no
            // líderes o guardias fijos) no entran aquí y pasan directo al punto A.2.
            if (_walkTowardsPlayer && !_hasArrivedForDialogue)
            {
                _approachTimer += Time.deltaTime;
                float distToPlayer = Vector3.Distance(context.Transform.position, context.Player.position);

                if (distToPlayer <= _stopDistance || _approachTimer >= MAX_APPROACH_TIME)
                {
                    _hasArrivedForDialogue = true;
                    if (context.Agent != null && context.Agent.isOnNavMesh)
                        context.Agent.isStopped = true;
                    context.Animator?.SetMovementSpeed(0);
                }
                else
                {
                    MoveAndRotate(context);
                    return;
                }
            }

            // A.2. Retraso previo al inicio del diálogo/cámara de batalla (ver _dialogueStartDelay,
            // calculado en OnEnter). Se cuenta desde que se llega a distancia de diálogo (punto
            // A.1) o desde el principio si el NPC no camina. Mientras no transcurra, el NPC solo
            // mira al jugador: no acumula _timer y no arranca el diálogo.
            if (!_dialogueStarted)
            {
                _dialogueStartDelay -= Time.deltaTime;
                if (_dialogueStartDelay <= 0f)
                {
                    _dialogueStarted = true;
                    StartAlertDialogue(context);
                }
                else
                {
                    FacePlayer(context);
                    return;
                }
            }

            // ✅ FIX: Si el NPC pertenece a un equipo que está reagrupándose,
            // dejar que NPCCombatTeam maneje el movimiento
            // ✅ FIX #10: _teamMember ya viene cacheado desde OnEnter (antes: GetComponent cada frame)
            if (_teamMember != null && _teamMember.Team != null && _teamMember.Team.IsRegrouping)
            {
                // Solo mirar al jugador, el movimiento lo maneja NPCCombatTeam
                FacePlayer(context);
                return;
            }

            // B. Esperar Diálogo
            if (_waitingForDialogue)
            {
                // Si el diálogo se cerró, liberamos
                if (DialogueManager.Instance != null && !DialogueManager.Instance.IsOpen)
                {
                    _waitingForDialogue = false;
                    context.Log("[AlertState] Diálogo finalizado.");
                }
                else
                {
                    // Mientras habla, solo mira al jugador (sin caminar)
                    FacePlayer(context);
                    return;
                }
            }

            // C. Ya en distancia de diálogo (la aproximación ocurrió en A.1) — solo queda esperar
            // a que se cumpla _alertDuration (caso sin diálogo configurado) mirando al jugador.
            _timer += Time.deltaTime;
            FacePlayer(context);
        }

        public override void OnExit(NPCStateContext context)
        {
            context.Log("[AlertState] Fin de alerta.");

            if (_playerFrozenByAlert && global::Core.PlayerInputManager.Instance != null)
            {
                global::Core.PlayerInputManager.Instance.PopUIMode();
                _playerFrozenByAlert = false;
            }
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.WasDefeatedInCombat) return new DeadState();

            // Si el jugador se esfuma
            if (context.Player == null) return new IdleState();

            // ✅ FIX: Si es miembro no-líder, solo transicionar cuando context.IsInCombat sea true
            // Esto ocurre cuando ForceTeamCombat lo establece
            if (_skipDialogue)
            {
                if (context.IsInCombat)
                {
                    return new CombatState();
                }
                // Mantener el miembro mirando al jugador mientras espera
                return null;
            }

            // Bloqueo por diálogo
            if (_waitingForDialogue) return null;

            // ⭐ MEJORA: Si el diálogo completó, transicionar inmediatamente a combate
            // No esperar _alertDuration completo
            if (_dialogueCompleted)
            {
                context.IsInCombat = true;
                return new CombatState();
            }

            // Tiempo cumplido (solo si no había diálogo) -> COMBATE
            if (_timer >= _alertDuration)
            {
                context.IsInCombat = true;
                return new CombatState();
            }

            return null;
        }

        // =================================================================================
        // 🛠️ HELPERS
        // =================================================================================

        private void FacePlayer(NPCStateContext context)
        {
            Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
            directionToPlayer.y = 0;
            if (directionToPlayer.sqrMagnitude > 0.01f && context.Animator != null)
            {
                context.Animator.FaceDirection(directionToPlayer);
            }
        }

        private void MoveAndRotate(NPCStateContext context)
        {
            float dist = Vector3.Distance(context.Transform.position, context.Player.position);

            if (dist > _stopDistance)
            {
                // Caminar hacia el jugador
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.isStopped = false;
                    context.Agent.SetDestination(context.Player.position);

                    // ✅ FIX: Desactivar updateRotation del NavMeshAgent
                    // Dejar que NPCSimpleAnimator.SyncWithNavMeshAgent() maneje la rotación
                    // para evitar conflictos entre sistemas
                    context.Agent.updateRotation = false;

                    // Sync Animación - NPCSimpleAnimator se encargará de rotar correctamente
                    if (context.Animator != null)
                        context.Animator.SetMovementSpeed(context.Agent.velocity.magnitude / context.Agent.speed);
                }
            }
            else
            {
                // Llegamos -> Parar y mirar hacia el jugador
                context.Agent.isStopped = true;
                context.Agent.updateRotation = false;
                context.Animator.SetMovementSpeed(0);
                FacePlayer(context);
            }
        }


        private void TriggerAlertMusic(NPCStateContext context)
        {
            string eventId = context.Config?.combatConfig?.alertMusicEvent;
            if (!string.IsNullOrEmpty(eventId))
            {
                AudioService.Instance?.BeginAlertById(eventId);
            }
        }

        /// <summary>
        /// FIX INC-019: muestra el icono "❗" sobre la cabeza del NPC al entrar en alerta,
        /// usando el prefab configurado en NPCCombatConfig.exclamationIconPrefab.
        /// Añade NPCAlertIconController de forma perezosa (igual que hace TagMinigameController)
        /// para no requerir configuración manual por NPC.
        /// </summary>
        private void ShowDetectionIcon(NPCStateContext context)
        {
            var combatConfig = context.Config?.combatConfig;
            if (combatConfig == null || combatConfig.exclamationIconPrefab == null) return;
            if (context.Transform == null) return;

            var iconController = context.Transform.GetComponent<NPCAlertIconController>();
            if (iconController == null)
                iconController = context.Transform.gameObject.AddComponent<NPCAlertIconController>();

            iconController.ShowExclamation(combatConfig.exclamationIconPrefab, combatConfig.alertIconDuration);
        }

        private void StartAlertDialogue(NPCStateContext context)
        {
            // ✅ FIX: Si skipDialogue está activo (miembros no-líderes), no iniciar diálogo
            if (_skipDialogue)
            {
                context.Log("[AlertState] ⏭️ Saltando diálogo (miembro de equipo no-líder)");
                return;
            }

            var config = context.Config?.combatConfig;
            if (config != null && config.dialogueOnAlert != null)
            {
                if (config.waitForAlertDialogue) _waitingForDialogue = true;

                DialogueManager.Instance?.StartBattleDialogue(config.dialogueOnAlert, context.Transform, () =>
                {
                    _waitingForDialogue = false;
                    _dialogueCompleted = true; // ⭐ Marcar que el diálogo completó para transición inmediata

                    // ⭐ NO manipular animaciones del player - Invector maneja todo automáticamente
                    // El player usará siempre su idle normal sin forzar poses de batalla
                });
            }
        }
    }
}
