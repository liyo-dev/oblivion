﻿﻿﻿﻿using UnityEngine;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado Idle - El NPC está quieto esperando.
    /// Detecta al jugador para iniciar combate si es agresivo.
    /// </summary>
    public class IdleState : NPCStateBase
    {
        private float _idleTimer;
        private float _idleDuration;
        private float _playerDetectionTimer;
        private const float PlayerDetectionInterval = 0.3f;
        
        public override string StateName => "Idle";
        
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            StopMovement(context);
            
            // Si el NPC fue derrotado, asegurar que use animaciones normales (no de batalla)
            if (context.WasDefeatedInCombat && context.Animator != null)
            {
                context.Animator.SetBattleMode(false);
                context.Log($"[IdleState] NPC derrotado - Desactivando modo batalla, animación normal");
            }
            
            if (context.Config != null)
            {
                _idleDuration = Random.Range(context.Config.minIdleTime, context.Config.maxIdleTime);
            }
            else
            {
                _idleDuration = 2f;
            }
            
            _idleTimer = 0f;
            _playerDetectionTimer = 0f;
        }
        
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            _idleTimer += Time.deltaTime;
            _playerDetectionTimer += Time.deltaTime;
            
            // Detección periódica del jugador para combate
            if (_playerDetectionTimer >= PlayerDetectionInterval)
            {
                _playerDetectionTimer = 0f;
                CheckPlayerDetection(context);
            }
        }
        
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            // Prioridad máxima: Cinemática
            if (context.IsInCinematic)
            {
                return new CinematicState();
            }
            
            // Si se activó combate externamente
            if (context.IsInCombat)
            {
                return new CombatState();
            }
            
            // No cambiar de estado mientras interactúa
            if (context.IsInteracting)
            {
                return null;
            }
            
            // Wander después de idle
            if (context.Config != null && context.Config.enableWander && _idleTimer >= _idleDuration)
            {
                return new WanderState();
            }
            
            return null;
        }
        
        /// <summary>
        /// Detecta al jugador y transite a AlertState si es agresivo
        /// </summary>
        private void CheckPlayerDetection(Common.NPCStateContext context)
        {
            // ✅ NO DETECTAR SI EL NPC YA FUE DERROTADO - ESTO PREVIENE EL BUCLE INFINITO
            if (context.WasDefeatedInCombat)
            {
                // Silencioso: No loguear cada frame, solo la primera vez
                return;
            }
            
            // Solo detectar si tiene configuración de combate agresiva
            if (context.Config == null || context.Config.combatConfig == null)
                return;
            
            var combatConfig = context.Config.combatConfig;
            if (!combatConfig.isAggressive)
                return;
            
            // Verificar si el jugador está en rango de detección
            if (context.Player == null)
                return;
            
            float distanceToPlayer = Vector3.Distance(context.Transform.position, context.Player.position);
            if (distanceToPlayer > combatConfig.detectionRange)
                return;
            
            // Verificar si está en el campo de visión (opcional, podría ser 360°)
            Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
            float angleToPlayer = Vector3.Angle(context.Transform.forward, directionToPlayer);
            
            // Campo de visión amplio (180° por defecto)
            const float detectionAngle = 180f;
            if (angleToPlayer > detectionAngle / 2f)
                return;
            
            // Jugador detectado - activar alerta
            context.Log($"[IdleState] Jugador detectado a {distanceToPlayer:F1}m, activando alerta");
            
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
