using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado Idle - El NPC está quieto.
    /// Incluye detección visual realista (Raycast) para iniciar combate si es agresivo.
    /// </summary>
    public class IdleState : NPCStateBase
    {
        public override string StateName => "Idle";

        private float _idleTimer;
        private float _idleDuration;

        // Detección
        private float _playerDetectionTimer;
        private const float PLAYER_DETECTION_INTERVAL = 0.2f; // 5 veces por segundo es suficiente

        // Encuentros sociales (compartido con WanderState vía NPCStateBase.CheckSocialEncounter).
        // Antes solo se escaneaba en WanderState: un NPC parado o recién llegado nunca conversaba.
        private float _socialScanTimer;
        private const float SOCIAL_SCAN_INTERVAL = 3f;
        private readonly Collider[] _socialBuffer = new Collider[8];

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            StopMovement(context);

            // Seguridad: Si viene de combate/muerte, limpiar flags
            if (context.WasDefeatedInCombat && context.Animator != null)
                context.Animator.SetBattleMode(false);

            var ambientActivity = context.Config?.ambientConfig?.ambientActivity ?? NPCAmbientActivity.None;

            if (ambientActivity != NPCAmbientActivity.None)
            {
                // NPC con actividad fija: no vaga, permanece en la actividad indefinidamente
                context.Animator?.PlayAmbientActivity(ambientActivity);
                _idleDuration = float.MaxValue;
            }
            else
            {
                // Resetear el estado de animación a Idle para que el próximo WanderState
                // siempre dispare TransitionToLocomotion() correctamente.
                context.Animator?.TransitionToIdle();

                _idleDuration = context.Config != null
                    ? Random.Range(context.Config.minIdleTime, context.Config.maxIdleTime)
                    : 2f;
            }

            _idleTimer = 0f;
            _playerDetectionTimer = 0f;
            _socialScanTimer = 0f;
        }

        public override void OnExit(NPCStateContext context)
        {
            var ambientActivity = context.Config?.ambientConfig?.ambientActivity ?? NPCAmbientActivity.None;
            if (ambientActivity != NPCAmbientActivity.None)
                context.Animator?.StopAmbientActivity(ambientActivity);

            base.OnExit(context);
        }
        
        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            _idleTimer += Time.deltaTime;
            
            // ✅ FIX CRÍTICO: Asegurar que el NavMeshAgent permanezca detenido
            // Esto previene que paths residuales o velocidad acumulada cause movimiento
            if (context.Agent != null && context.Agent.enabled && context.Agent.isOnNavMesh)
            {
                // ⚠️ DETENCIÓN MUY AGRESIVA: Forzar cada frame
                if (!context.Agent.isStopped)
                {
                    context.Log("[IdleState] ⚠️ Agent NO ESTABA DETENIDO - forzando detención");
                    context.Agent.isStopped = true;
                }
                
                if (context.Agent.hasPath)
                {
                    context.Log("[IdleState] ⚠️ Agent TIENE PATH ACTIVO - reseteando");
                    context.Agent.ResetPath();
                }
                
                if (context.Agent.velocity.sqrMagnitude > 0.01f)
                {
                    context.Log($"[IdleState] ⚠️ Agent TIENE VELOCIDAD ({context.Agent.velocity.magnitude:F1}) - limpiando");
                    context.Agent.velocity = Vector3.zero;
                }
            }
            
            // Detección visual periódica
            _playerDetectionTimer += Time.deltaTime;
            if (_playerDetectionTimer >= PLAYER_DETECTION_INTERVAL)
            {
                _playerDetectionTimer = 0f;
                CheckPlayerDetection(context);
            }

            // Encuentros sociales: un NPC parado también puede charlar con quien pase cerca,
            // no solo mientras vaga (ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § B.6.2)
            _socialScanTimer += Time.deltaTime;
            if (_socialScanTimer >= SOCIAL_SCAN_INTERVAL)
            {
                _socialScanTimer = 0f;
                CheckSocialEncounter(context, _socialBuffer);
            }
        }
        
        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Prioridad Máxima
            if (context.IsInCinematic) return new CinematicState();
            if (context.WasDefeatedInCombat) return new DeadState(); // Cambio directo a muerte si procede
            
            // 2. Combate externo (ej: golpeado por detrás)
            if (context.IsInCombat) return new CombatState();
            
            // 3. Interacción
            if (context.IsInteracting) return null; // Quedarse en Idle mientras habla

            // 3b. Refugio de lluvia - interrumpe el idle normal, pero no combate/cinemática/interacción
            if (context.ShouldSeekShelter && context.CurrentShelter == null &&
                context.Config != null && context.Config.canSeekShelter)
            {
                return new SeekShelterState();
            }

            // 4. Lógica de Patrulla (Wander) - bloqueada si el NPC está anclado por el sistema de party
            if (context.Config != null && context.Config.enableWander && !context.IsPinnedByParty)
            {
                if (_idleTimer >= _idleDuration)
                {
                    return new WanderState();
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Sistema de Detección Visual (Igual que WanderState)
        /// </summary>
        private void CheckPlayerDetection(NPCStateContext context)
        {
            // 0. Filtros básicos
            if (context.WasDefeatedInCombat) return;
            if (context.Player == null) return;
            
            // Bloqueo global: si ya hay otro combate activo, este NPC no debe iniciar uno nuevo.
            if (ActiveCombatRegistry.HasActiveCombatExcluding(context.Transform.gameObject))
            {
                return;
            }
            
            // ✅ FIX: Si soy miembro de un equipo y ya notifiqué, no sigo detectando
            // Esto evita el bucle infinito de detección
            var teamMember = context.Transform.GetComponent<NPCTeamMember>();
            if (teamMember != null && teamMember.HasNotifiedTeam)
            {
                return; // Ya se notificó al equipo, el NPCCombatTeam se encarga
            }
            
            var combatConfig = context.Config?.combatConfig;
            if (combatConfig == null || !combatConfig.isAggressive) return;
            
            // 1. Distancia (sqrMagnitude es más rápido)
            Vector3 toPlayer = context.Player.position - context.Transform.position;
            float distSqr = toPlayer.sqrMagnitude;
            float detectionRange = combatConfig.detectionRange;
            
            if (distSqr > detectionRange * detectionRange) return;
            
            // 2. Campo de Visión (FOV)
            Vector3 eyePos = context.Transform.position + Vector3.up * 1.6f;
            Vector3 playerTarget = context.Player.position + Vector3.up * 1.0f;
            Vector3 dirToTarget = (playerTarget - eyePos).normalized;
            
            float angle = Vector3.Angle(context.Transform.forward, dirToTarget);
            float fov = combatConfig.fieldOfView > 0 ? combatConfig.fieldOfView : 160f;
            if (angle > fov * 0.5f) return;
            
            // 3. Línea de Visión (Raycast) - Evita ver a través de paredes
            int layerMask = ~0; // Todo
            if (combatConfig.coverLayerMask != 0) 
                layerMask = combatConfig.coverLayerMask | (1 << context.Player.gameObject.layer);

            if (Physics.Raycast(eyePos, dirToTarget, out RaycastHit hit, detectionRange, layerMask))
            {
                // Si chocamos con algo que NO es el jugador, estamos bloqueados
                if (hit.transform != context.Player && !hit.transform.IsChildOf(context.Player))
                {
                    // FIX INC-073: el layerMask usado aquí incluye "Everything" salvo que se
                    // configure coverLayerMask explícitamente, así que el propio compañero de
                    // equipo (p.ej. Lety y Vicky, colocados muy cerca el uno del otro) podía tapar
                    // el raycast del otro y ninguno de los dos llegaba a "ver" al jugador
                    // acercarse — había que atacarles para forzar el combate. Un compañero del
                    // mismo equipo no debe bloquear la detección.
                    var hitTeamMember = hit.transform.GetComponentInParent<NPCTeamMember>();
                    bool isTeammate = hitTeamMember != null && teamMember != null && hitTeamMember.Team == teamMember.Team;
                    if (!isTeammate)
                        return;
                }
            }
            
            // --- JUGADOR DETECTADO ---
            context.Log($"[IdleState] 👁️ Jugador visto. ¡Alerta!");
            
            // ✅ TEAM SUPPORT: Notificar al equipo (reutilizamos teamMember del inicio)
            if (teamMember != null && teamMember.TryNotifyTeamOfPlayer(context.Player))
            {
                // Crear AlertState para este miembro
                var alertState = new AlertState(
                    duration: combatConfig.alertIconDuration,
                    walk: true,
                    stopDist: combatConfig.minAttackDistance + 1f,
                    skipDialogue: !teamMember.IsLeader // ✅ FIX: Los no-líderes NO inician diálogo
                );
                context.Brain?.ChangeState(alertState);
                return;
            }
            
            var alertStateDefault = new AlertState(
                duration: combatConfig.alertIconDuration,
                walk: true,
                stopDist: combatConfig.minAttackDistance + 1f
            );
            
            context.Brain?.ChangeState(alertStateDefault);
        }
    }
}
