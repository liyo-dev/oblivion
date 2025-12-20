﻿using UnityEngine;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Combate - El NPC está en modo combate con el jugador.
    /// Utiliza NPCCombatBrain para la lógica táctica avanzada.
    /// </summary>
    public class CombatState : NPCStateBase
    {
        public override string StateName => "Combat";
        
        private NPCCombatBrain _combatBrain;
        private bool _combatBrainInitialized;
        private float _playerDistanceCheckTimer;
        private const float PlayerDistanceCheckInterval = 0.5f;
        private const float MaxCombatDistance = 25f; // Distancia máxima para mantener combate
        
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            
            context.IsInCombat = true;
            StopMovement(context);
            
            // Obtener o crear NPCCombatBrain
            _combatBrain = context.Transform.GetComponent<NPCCombatBrain>();
            if (_combatBrain == null)
            {
                context.LogWarning("[CombatState] No se encontró NPCCombatBrain, el combate será básico");
                return;
            }
            
            // Inicializar combat brain con configuración
            if (context.Config != null && context.Config.combatConfig != null)
            {
                var combatConfig = context.Config.combatConfig;
                var settings = new NPCCombatBrain.Settings
                {
                    sightRadius = combatConfig.detectionRange,
                    minDistance = combatConfig.meleeRange,
                    maxDistance = combatConfig.combatRange,
                    repathInterval = 0.5f,
                    retreatDistance = 2f,
                    turnSpeed = 5f, // Valor por defecto razonable
                    upperBodyLayer = 1, // Asumiendo que la capa UpperBody es 1
                    battleIdleState = "Battle Idle",
                    
                    // Configurar slots de ataque con valores por defecto
                    leftAttack = new NPCCombatBrain.AttackSlot 
                    { 
                        animationState = "Attack_Left",
                        cooldown = combatConfig.attackCooldown,
                        slotIndex = 0
                    },
                    rightAttack = new NPCCombatBrain.AttackSlot 
                    { 
                        animationState = "Attack_Right",
                        cooldown = combatConfig.attackCooldown * 1.2f,
                        slotIndex = 1
                    },
                    specialAttack = new NPCCombatBrain.AttackSlot 
                    { 
                        animationState = "Attack_Special",
                        cooldown = combatConfig.attackCooldown * 2f,
                        slotIndex = 2
                    },
                    
                    // Configuración táctica
                    aggressiveDistance = combatConfig.meleeRange * 1.5f,
                    retreatHealthPercent = 0.3f,
                    circleDistance = (combatConfig.meleeRange + combatConfig.combatRange) * 0.5f,
                    circleSpeed = 30f,
                    
                    // Proyectiles y timing
                    spawnProjectileViaAnimationEvent = false,
                    fireDelaySeconds = 0.3f,
                    
                    // Línea de visión
                    requireLineOfSight = true,
                    losMask = LayerMask.GetMask("Default"),
                    windupMin = 0.2f,
                    windupMax = 0.6f,
                    strafeFlipMin = 2f,
                    strafeFlipMax = 4f,
                    dodgeDistance = 2f,
                    dodgeCooldown = 3f,
                    
                    // Micro-pausas para ritmo humano
                    microPauseDurationMin = 0.3f,
                    microPauseDurationMax = 0.8f,
                    microPauseIntervalMin = 2f,
                    microPauseIntervalMax = 5f,
                    
                    // Burst & reposition
                    burstRepositionDistance = 3f,
                    burstRepositionCooldown = 5f,
                    burstAttacksMin = 2,
                    burstAttacksMax = 4,
                    
                    // Ventanas de quieto
                    holdDurationMin = 0.5f,
                    holdDurationMax = 1.5f,
                    holdIntervalMin = 3f,
                    holdIntervalMax = 6f,
                    attackHoldSeconds = 0.4f,
                    
                    // Dificultad
                    attackFrequencyMultiplier = 1f,
                    aggressionBias = combatConfig.isAggressive ? 0.7f : 0.5f,
                    dodgeChance = 0.3f
                };
                
                var manager = context.Transform.GetComponent<NPCBehaviourManagerV2>();
                if (manager != null)
                {
                    _combatBrain.Initialize(manager);
                    _combatBrain.BeginCombat(settings);
                    _combatBrainInitialized = true;
                    context.Log("[CombatState] Combat brain initialized successfully");
                }
                else
                {
                    context.LogError("[CombatState] No se encontró NPCBehaviourManagerV2");
                }
            }
            
            _playerDistanceCheckTimer = 0f;
        }
        
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            
            // Si no hay combat brain, hacer combate básico (placeholder)
            if (!_combatBrainInitialized)
            {
                BasicCombatBehavior(context);
                return;
            }
            
            // El combat brain maneja toda la lógica táctica automáticamente
            // Solo necesitamos verificar condiciones de salida
            
            _playerDistanceCheckTimer += Time.deltaTime;
            if (_playerDistanceCheckTimer >= PlayerDistanceCheckInterval)
            {
                _playerDistanceCheckTimer = 0f;
                
                // Verificar si el jugador está demasiado lejos
                if (context.Player != null)
                {
                    float distance = Vector3.Distance(context.Transform.position, context.Player.position);
                    if (distance > MaxCombatDistance)
                    {
                        context.Log($"[CombatState] Jugador demasiado lejos ({distance:F1}m), saliendo de combate");
                        context.IsInCombat = false;
                    }
                }
            }
        }
        
        public override void OnExit(Common.NPCStateContext context)
        {
            base.OnExit(context);
            
            context.IsInCombat = false;
            
            // Detener combat brain
            if (_combatBrain != null && _combatBrainInitialized)
            {
                _combatBrain.StopCombat();
                context.Log("[CombatState] Combat brain stopped");
            }
            
            // Resetear movimiento
            StopMovement(context);
        }
        
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            // Prioridad máxima: Cinemática
            if (context.IsInCinematic)
            {
                return new CinematicState();
            }
            
            // Si ya no está en combate (flag desactivado externamente), volver a idle
            if (!context.IsInCombat)
            {
                context.Log($"[{StateName}] Combate finalizado, volviendo a Idle");
                return new IdleState();
            }
            
            // TODO: Verificar otras condiciones:
            // - NPC derrotado -> DeathState
            // - Jugador derrotado -> VictoryState
            // - Necesita huir -> FleeState
            
            return null; // Continuar en combate
        }
        
        /// <summary>
        /// Comportamiento de combate básico cuando no hay CombatBrain
        /// </summary>
        private void BasicCombatBehavior(Common.NPCStateContext context)
        {
            if (context.Player == null) return;
            
            // Simplemente mirar al jugador y mantener distancia
            Vector3 directionToPlayer = context.Player.position - context.Transform.position;
            directionToPlayer.y = 0f;
            
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                context.Transform.rotation = Quaternion.Slerp(
                    context.Transform.rotation,
                    targetRotation,
                    Time.deltaTime * 5f
                );
            }
            
            // Mantener una distancia mínima
            float distance = directionToPlayer.magnitude;
            const float minDistance = 2f;
            const float maxDistance = 4f;
            
            if (distance < minDistance)
            {
                // Retroceder
                Vector3 retreatDirection = -directionToPlayer.normalized;
                context.Agent.SetDestination(context.Transform.position + retreatDirection * 2f);
            }
            else if (distance > maxDistance)
            {
                // Acercarse
                context.Agent.SetDestination(context.Player.position);
            }
            else
            {
                // En rango óptimo, detenerse
                StopMovement(context);
            }
        }
    }
}

