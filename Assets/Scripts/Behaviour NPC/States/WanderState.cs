using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Wander - El NPC camina aleatoriamente dentro de un radio.
    /// Incluye detección de jugador con Raycast (Línea de visión).
    /// </summary>
    public class WanderState : NPCStateBase
    {
        public override string StateName => "Wander";

        private Vector3 _targetPosition;
        private float _stuckTimer;
        private Vector3 _lastPosition;
        private bool _hasSetDestination;
        
        // Detección
        private float _playerDetectionTimer;
        private const float PLAYER_DETECTION_INTERVAL = 0.2f;
        
        // Cache para optimización
        private Collider[] _collidersBuffer = new Collider[1]; 

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            _hasSetDestination = false;
            _stuckTimer = 0f;
            _lastPosition = context.Transform.position;
            
            // 1. Configurar velocidad de paseo (Walk Speed)
            // Si hay configuración ambiental, usar su velocidad, si no, reducir la velocidad base
            float wanderSpeed = 2.0f; // Valor por defecto seguro
            if (context.Config != null && context.Config.ambientConfig != null)
            {
                wanderSpeed = context.Config.ambientConfig.walkSpeed;
            }
            else if (context.Agent != null)
            {
                wanderSpeed = context.Agent.speed * 0.5f; // 50% de la velocidad máxima si no hay config
            }
            
            if (context.Agent != null) context.Agent.speed = wanderSpeed;

            // 2. Buscar punto
            if (!TryFindWanderPoint(context, out _targetPosition))
            {
                // Si falla (ej: NavMesh no bakeado o área inaccesible), volver a idle
                context.LogWarning($"[{StateName}] No se encontró punto válido. Volviendo a Idle.");
                context.Brain.ChangeState(new IdleState());
                return;
            }
            
            // 3. Moverse
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
            
            // Actualizar animación (blend tree de locomoción)
            UpdateMovementAnimation(context);
            
            // Verificar si se atascó contra una pared
            CheckIfStuck(context);
            
            // Detección de jugador (Sensor visual)
            _playerDetectionTimer += Time.deltaTime;
            if (_playerDetectionTimer >= PLAYER_DETECTION_INTERVAL)
            {
                _playerDetectionTimer = 0f;
                CheckPlayerDetection(context);
            }
        }
        
        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Prioridades Altas (Cinemática / Combate Forzado / Muerte)
            if (context.IsInCinematic) return new CinematicState();
            if (context.IsInCombat) return new CombatState();
            if (context.WasDefeatedInCombat) return new DeadState();
            
            // 2. Interacción
            if (context.IsInteracting) return new IdleState();
            
            // 3. Fallos de Navegación
            if (!_hasSetDestination) return null; // Ya se manejó en OnEnter
            
            if (IsPathBlocked(context))
            {
                context.Log($"[{StateName}] Camino bloqueado/inválido.");
                return new IdleState();
            }
            
            if (HasStalled(context))
            {
                context.Log($"[{StateName}] NPC atascado físicamente.");
                return new IdleState();
            }
            
            // 4. Éxito
            if (HasReachedDestination(context))
            {
                // Al llegar, activamos el flag para que el IdleState sepa que acabamos de llegar
                // y decida cuánto tiempo esperar antes de volver a Wander.
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
            var teamMember = context.Transform.GetComponent<NPCTeamMember>();
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
            // Asumimos que los ojos están un poco arriba del pivote
            Vector3 eyePos = context.Transform.position + Vector3.up * 1.6f;
            Vector3 playerTargetPos = context.Player.position + Vector3.up * 1.0f; // Pecho del jugador
            Vector3 dirToTarget = (playerTargetPos - eyePos).normalized;

            float angle = Vector3.Angle(context.Transform.forward, dirToTarget);
            float fov = combatConfig.fieldOfView > 0 ? combatConfig.fieldOfView : 160f; // 160 grados por defecto

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