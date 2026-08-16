using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Wander - El NPC camina aleatoriamente dentro de un radio.
    /// Incluye detección de jugador con Raycast, encuentros sociales con otros NPCs
    /// y la posibilidad de dirigirse a un NPCWorldPoint para realizar una actividad.
    /// </summary>
    public class WanderState : NPCStateBase
    {
        public override string StateName => "Wander";

        private Vector3 _targetPosition;
        private float _stuckTimer;
        private Vector3 _lastPosition;
        private bool _hasSetDestination;

        // Detección de jugador
        private float _playerDetectionTimer;
        private const float PLAYER_DETECTION_INTERVAL = 0.2f;

        // Encuentros sociales (la lógica de escaneo vive en NPCStateBase.CheckSocialEncounter,
        // compartida con IdleState)
        private float _socialScanTimer;
        private const float SOCIAL_SCAN_INTERVAL = 3f;
        private readonly Collider[] _socialBuffer = new Collider[8];

        // Punto de actividad elegido al entrar en el estado
        private NPCWorldPoint _pendingWorldPoint;

        // ✅ FIX rendimiento: cacheado en OnEnter, no GetComponent en cada CheckPlayerDetection
        // (cada 0.2s por NPC vagando). Mismo patrón que AlertState (FIX #10) e IdleState.
        private NPCTeamMember _teamMember;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            if (context.IsPinnedByParty)
            {
                context.Brain.ChangeState(new IdleState());
                return;
            }

            _hasSetDestination  = false;
            _stuckTimer         = 0f;
            _lastPosition       = context.Transform.position;
            _socialScanTimer    = 0f;
            _pendingWorldPoint  = null;
            _teamMember         = context.Transform.GetComponent<NPCTeamMember>(); // ✅ FIX rendimiento: una vez por entrada al estado

            float wanderSpeed = 2.0f;
            if (context.Config?.ambientConfig != null)
                wanderSpeed = context.Config.ambientConfig.walkSpeed;
            else if (context.Config != null)
                wanderSpeed = context.Config.walkSpeed;

            if (context.Agent != null) context.Agent.speed = wanderSpeed;

            var socialConfig = context.Config?.socialConfig;
            float energy = socialConfig?.personality.energy ?? 0.5f;
            float searchRange = socialConfig?.worldPointSearchRange ?? 20f;

            // Radar de amistad: los NPCs sociables intentan buscar activamente a un Friend/
            // BestFriend conocido en vez de solo toparse con quien pase cerca por azar (radio
            // ampliado respecto al de detección social pasiva). Ver diseño § B.6.1 — sin esto,
            // dos NPCs con vínculo fuerte pueden no volver a cruzarse nunca por pura geografía
            // aleatoria en un pueblo con pocos NPCs ambientales.
            Game.NPC.NPCBehaviourManagerV2 nearbyFriend = null;
            if (socialConfig != null && UnityEngine.Random.value <= socialConfig.personality.sociability)
            {
                float friendSeekRange = socialConfig.socialDetectionRange * 2.5f;
                nearbyFriend = NPCAmbientRegistry.FindNearbyFriend(
                    context.RelationshipId, context.Transform.position, friendSeekRange);
            }

            // Con cierta probabilidad (energía baja → prefiere actividades), buscar NPCWorldPoint
            if (nearbyFriend != null)
            {
                _targetPosition = nearbyFriend.transform.position;
            }
            else if (UnityEngine.Random.value > energy &&
                NPCWorldPoint.TryFindAny(context.Transform.position, searchRange, out var wp))
            {
                _pendingWorldPoint = wp;
                _targetPosition    = wp.InteractionPosition;
            }
            else if (!TryFindWanderPoint(context, out _targetPosition))
            {
                context.LogWarning($"[{StateName}] No se encontró punto válido. Volviendo a Idle.");
                context.Brain.ChangeState(new IdleState());
                return;
            }

            if (!SetDestination(context, _targetPosition))
            {
                context.Brain.ChangeState(new IdleState());
                return;
            }

            _hasSetDestination = true;
        }
        
        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (!_hasSetDestination) return;

            CheckIfStuck(context);

            _playerDetectionTimer += Time.deltaTime;
            if (_playerDetectionTimer >= PLAYER_DETECTION_INTERVAL)
            {
                _playerDetectionTimer = 0f;
                CheckPlayerDetection(context);
            }

            _socialScanTimer += Time.deltaTime;
            if (_socialScanTimer >= SOCIAL_SCAN_INTERVAL)
            {
                _socialScanTimer = 0f;
                CheckSocialEncounter(context, _socialBuffer);
            }
        }
        
        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            // Refugio de lluvia - interrumpe el vagabundeo normal, pero no combate/cinemática/interacción
            if (context.ShouldSeekShelter && context.CurrentShelter == null &&
                context.Config != null && context.Config.canSeekShelter)
            {
                return new SeekShelterState();
            }

            // Encuentro social iniciado por otro NPC mientras vagamos
            if (context.PendingSocialPartner != null)
                return new NPCSocialEncounterState();

            if (!_hasSetDestination) return null;

            if (IsPathBlocked(context))
            {
                context.Log($"[{StateName}] Camino bloqueado/inválido.");
                _pendingWorldPoint = null;
                return new IdleState();
            }

            if (HasStalled(context))
            {
                context.Log($"[{StateName}] NPC atascado físicamente.");
                _pendingWorldPoint = null;
                return new IdleState();
            }

            if (HasReachedDestination(context))
            {
                // Si el destino era un NPCWorldPoint, ocuparlo y realizar la actividad
                if (_pendingWorldPoint != null && !_pendingWorldPoint.IsOccupied)
                    return new WalkToActivityState(_pendingWorldPoint, alreadyAtPoint: true);

                context.HasReachedDestination = true;
                return new IdleState();
            }

            return null;
        }
        
        // =================================================================================
        // 🧩 LÓGICA INTERNA
        // =================================================================================

        private bool TryFindWanderPoint(NPCStateContext context, out Vector3 point)
        {
            point = Vector3.zero;
            
            // Asegurar que estamos en NavMesh antes de buscar
            float sampleRadius = context.Config?.navMeshSampleRadius ?? 2f;
            if (!NavMeshAgentUtility.EnsureAgentOnNavMesh(context.Agent, context.Transform.position, sampleRadius))
            {
                return false;
            }
            
            // Obtener radio de patrulla
            float radius = context.Config?.wanderRadius ?? 8f;
            
            // Si tiene un punto de anclaje (Anchor), patrullar alrededor de él, no de la posición actual
            // Esto evita que el NPC se vaya alejando infinitamente del spawn.
            Vector3 origin = context.Transform.position;
            // TODO: Si añades un SpawnAnchor al Context en el futuro, úsalo aquí:
            // if (context.SpawnPoint != Vector3.zero) origin = context.SpawnPoint;

            return NavMeshAgentUtility.TryGetRandomPoint(origin, radius, out point);
        }
        
        private void CheckIfStuck(NPCStateContext context)
        {
            // Comprobación simple de movimiento
            float distSqr = (context.Transform.position - _lastPosition).sqrMagnitude;
            float threshold = context.Config?.stuckThreshold ?? 0.05f;
            
            // Si se movió menos del umbral en este frame...
            if (distSqr < (threshold * threshold))
            {
                _stuckTimer += Time.deltaTime;
            }
            else
            {
                _stuckTimer = 0f;
                _lastPosition = context.Transform.position;
            }
        }
        
        private bool HasStalled(NPCStateContext context)
        {
            float maxTime = context.Config?.stuckCheckInterval ?? 2.0f;
            return _stuckTimer > maxTime;
        }
        
        /// <summary>
        /// Sistema de Sentidos: Vista (Distancia + Ángulo + Raycast)
        /// </summary>
        private void CheckPlayerDetection(NPCStateContext context)
        {
            // Pre-requisitos rápidos
            if (context.WasDefeatedInCombat || context.Player == null) return;
            
            // ✅ FIX: Si soy miembro de un equipo y ya notifiqué, no sigo detectando
            // Esto evita el bucle infinito de detección
            var teamMember = _teamMember; // ✅ FIX rendimiento: cacheado en OnEnter, ver campo _teamMember arriba
            if (teamMember != null && teamMember.HasNotifiedTeam)
            {
                return; // Ya se notificó al equipo, el NPCCombatTeam se encarga
            }
            
            var combatConfig = context.Config?.combatConfig;
            if (combatConfig == null || !combatConfig.isAggressive) return;

            // 1. Chequeo de Distancia (Optimizado con sqrMagnitude)
            Vector3 toPlayer = context.Player.position - context.Transform.position;
            float distSqr = toPlayer.sqrMagnitude;
            float detectionRange = combatConfig.detectionRange;
            
            if (distSqr > detectionRange * detectionRange) return;

            // 2. Chequeo de Campo de Visión (FOV)
            Vector3 eyePos = context.Transform.position + Vector3.up * 1.6f;
            Vector3 playerTargetPos = context.Player.position + Vector3.up * 1.0f; // Pecho del jugador
            Vector3 dirToTarget = (playerTargetPos - eyePos).normalized;

            float angle = Vector3.Angle(context.Transform.forward, dirToTarget);
            float fov = combatConfig.fieldOfView > 0 ? combatConfig.fieldOfView : 160f;
            if (angle > fov * 0.5f) return;

            // 3. Chequeo de Línea de Visión (Raycast) - ¡CRÍTICO PARA PAREDES!
            // Usamos una máscara que incluya Default, Obstacles y Player
            int layerMask = ~0; // Todo
            // O mejor, definimos una máscara específica si la tienes en config
            if (context.Config.combatConfig.coverLayerMask != 0) 
                layerMask = context.Config.combatConfig.coverLayerMask | (1 << context.Player.gameObject.layer);

            if (Physics.Raycast(eyePos, dirToTarget, out RaycastHit hit, detectionRange, layerMask))
            {
                // Si golpeamos algo que NO es el jugador, hay una pared
                if (hit.transform != context.Player && !hit.transform.IsChildOf(context.Player))
                {
                    // FIX INC-073: ver mismo comentario en IdleState.CheckPlayerDetection — un
                    // compañero del mismo equipo (dúo Lety/Vicky, etc.) no debe bloquear la
                    // detección solo por estar de pie al lado.
                    var hitTeamMember = hit.transform.GetComponentInParent<NPCTeamMember>();
                    bool isTeammate = hitTeamMember != null && teamMember != null && hitTeamMember.Team == teamMember.Team;
                    if (!isTeammate)
                        return; // Bloqueado por pared
                }
            }

            // --- JUGADOR DETECTADO ---
            context.Log($"[WanderState] 👁️ Jugador detectado visualmente. Iniciando Alerta.");
            
            // ✅ TEAM SUPPORT: Notificar al equipo (reutilizamos teamMember del inicio)
            if (teamMember != null && teamMember.TryNotifyTeamOfPlayer(context.Player))
            {
                // ✅ FIX: Todos los miembros entran en AlertState, pero los no-líderes saltan el diálogo
                var alertState = new AlertState(
                    duration: combatConfig.alertIconDuration,
                    walk: true,
                    stopDist: combatConfig.minAttackDistance + 1f,
                    skipDialogue: !teamMember.IsLeader // Los no-líderes NO inician diálogo
                );
                context.Brain?.ChangeState(alertState);
                return;
            }
            
            // Crear estado de alerta configurado (NPC sin equipo)
            var alertStateDefault = new AlertState(
                duration: combatConfig.alertIconDuration,
                walk: true,
                stopDist: combatConfig.minAttackDistance + 1f
            );
            
            // Forzar cambio de estado inmediato
            context.Brain.ChangeState(alertStateDefault);
        }

    }
}