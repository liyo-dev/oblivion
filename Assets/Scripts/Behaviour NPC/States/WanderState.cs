﻿﻿﻿using UnityEngine;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Wander - El NPC camina aleatoriamente dentro de un radio
    /// </summary>
    public class WanderState : NPCStateBase
    {
        private Vector3 _targetPosition;
        private float _stuckTimer;
        private Vector3 _lastPosition;
        private bool _hasSetDestination;
        private float _playerDetectionTimer;
        private const float PlayerDetectionInterval = 0.3f;
        
        public override string StateName => "Wander";
        
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            
            _hasSetDestination = false;
            _stuckTimer = 0f;
            _lastPosition = context.Transform.position;
            
            // Intentar encontrar un punto aleatorio
            if (!TryFindWanderPoint(context, out _targetPosition))
            {
                context.LogWarning($"[{StateName}] No se pudo encontrar punto de wander, volviendo a Idle");
                // Si no se puede encontrar punto, volver a idle inmediatamente
                context.Brain.ChangeState(new IdleState());
                return;
            }
            
            // Establecer destino
            if (!SetDestination(context, _targetPosition))
            {
                context.LogWarning($"[{StateName}] No se pudo establecer destino, volviendo a Idle");
                context.Brain.ChangeState(new IdleState());
                return;
            }
            
            _hasSetDestination = true;
        }
        
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (!_hasSetDestination)
                return;
            
            // Actualizar animación de movimiento
            UpdateMovementAnimation(context);
            
            // Verificar si se ha atascado
            CheckIfStuck(context);
            
            // Detección periódica del jugador para combate
            _playerDetectionTimer += Time.deltaTime;
            if (_playerDetectionTimer >= PlayerDetectionInterval)
            {
                _playerDetectionTimer = 0f;
                CheckPlayerDetection(context);
            }
        }
        
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            // Prioridad: Cinemática
            if (context.IsInCinematic)
            {
                return new CinematicState();
            }
            
            // Prioridad: Combate
            if (context.IsInCombat)
            {
                return new CombatState();
            }
            
            // Si está interactuando, volver a idle
            if (context.IsInteracting)
            {
                return new IdleState();
            }
            
            // Si no se estableció destino (falló en OnEnter), ya habremos cambiado de estado
            if (!_hasSetDestination)
            {
                return null;
            }
            
            // Si el camino está bloqueado, volver a idle
            if (IsPathBlocked(context))
            {
                context.Log($"[{StateName}] Camino bloqueado, volviendo a Idle");
                return new IdleState();
            }
            
            // Si se ha atascado, volver a idle
            if (HasStalled(context))
            {
                context.Log($"[{StateName}] NPC atascado, volviendo a Idle");
                return new IdleState();
            }
            
            // Si ha llegado al destino, volver a idle
            if (HasReachedDestination(context))
            {
                context.Log($"[{StateName}] Destino alcanzado, volviendo a Idle");
                context.HasReachedDestination = true;
                return new IdleState();
            }
            
            return null; // Continuar caminando
        }
        
        private bool TryFindWanderPoint(Common.NPCStateContext context, out Vector3 point)
        {
            point = Vector3.zero;
            
            // Asegurar que el agent está en NavMesh
            float radius = context.Config != null ? context.Config.navMeshSampleRadius : 2f;
            if (!Common.NavMeshAgentUtility.EnsureAgentOnNavMesh(context.Agent, context.Transform.position, radius))
            {
                return false;
            }
            
            // Buscar punto aleatorio
            float wanderRadius = context.Config != null ? context.Config.wanderRadius : 6f;
            return Common.NavMeshAgentUtility.TryGetRandomPoint(context.Transform.position, wanderRadius, out point);
        }
        
        private void CheckIfStuck(Common.NPCStateContext context)
        {
            var currentPos = context.Transform.position;
            float sqrDistance = (currentPos - _lastPosition).sqrMagnitude;
            
            float threshold = context.Config != null ? context.Config.stuckThreshold : 0.02f;
            threshold *= threshold; // sqrMagnitude
            
            if (sqrDistance <= threshold)
            {
                _stuckTimer += Time.deltaTime;
            }
            else
            {
                _stuckTimer = 0f;
                _lastPosition = currentPos;
            }
        }
        
        private bool HasStalled(Common.NPCStateContext context)
        {
            float interval = context.Config != null ? context.Config.stuckCheckInterval : 1.5f;
            return _stuckTimer > interval;
        }
        
        /// <summary>
        /// Detecta al jugador y transite a AlertState si es agresivo
        /// </summary>
        private void CheckPlayerDetection(Common.NPCStateContext context)
        {
            // No detectar si el NPC ya fue derrotado
            if (context.WasDefeatedInCombat)
                return;
            
            // Solo detectar si tiene configuración de combate agresiva
            if (context.Config == null || context.Config.combatConfig == null)
                return;
            
            var combatConfig = context.Config.combatConfig;
            if (!combatConfig.isAggressive)
                return;
            
            // ...existing code...
            // Verificar si el jugador está en rango de detección
            if (context.Player == null)
                return;
            
            float distanceToPlayer = Vector3.Distance(context.Transform.position, context.Player.position);
            if (distanceToPlayer > combatConfig.detectionRange)
                return;
            
            // Verificar si está en el campo de visión
            Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
            float angleToPlayer = Vector3.Angle(context.Transform.forward, directionToPlayer);
            
            // Campo de visión amplio (180° por defecto)
            const float detectionAngle = 180f;
            if (angleToPlayer > detectionAngle / 2f)
                return;
            
            // Jugador detectado - activar alerta
            context.Log($"[WanderState] Jugador detectado a {distanceToPlayer:F1}m, activando alerta");
            
            // Transitar manualmente a AlertState
            var alertState = new AlertState(
                combatConfig.alertIconDuration,
                walkTowardsPlayer: true,
                stopDistance: combatConfig.minAttackDistance + 1f
            );
            
            context.Brain?.ChangeState(alertState);
        }
    }
}

