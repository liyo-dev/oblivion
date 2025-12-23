﻿﻿﻿using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;
using Game.NPC;

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
        
        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            // ✅ VERIFICAR SI EL NPC YA FUE DERROTADO - NO permitir que vuelva a entrar en combate
            if (context.WasDefeatedInCombat)
            {
                context.Log("[CombatState] ⛔ NPC ya fue derrotado - NO puede volver a entrar en combate");
                context.IsInCombat = false;
                return;
            }
            
            context.Log("[CombatState] Entrando en combate");
            context.IsInCombat = true;
            
            // IMPORTANTE: El GameObject NPC debe estar en la layer "Enemy" para que:
            // - El script Targetable funcione correctamente
            // - Los hechizos del jugador puedan apuntar automáticamente al NPC
            
            // Reproducir música de batalla
            TriggerBattleMusic(context);
            
            // Asegurar que el NPC tenga Damageable y CombatLifecycleHandler
            InitializeCombatComponents(context);
            
            // Obtener o crear NPCCombatBrain
            _combatBrain = context.Transform.GetComponent<NPCCombatBrain>();
            if (_combatBrain == null)
            {
                _combatBrain = context.Transform.gameObject.AddComponent<NPCCombatBrain>();
                context.Log("[CombatState] ✅ NPCCombatBrain añadido automáticamente al NPC");
            }
            else
            {
                context.Log("[CombatState] NPCCombatBrain encontrado en el NPC");
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
                    turnSpeed = 5f,
                    upperBodyLayer = 1,
                    battleIdleState = "Battle Idle",
                    
                    // Configurar slots de ataque con nombres correctos del Animator
                    leftAttack = new NPCCombatBrain.AttackSlot 
                    { 
                        animationState = "MagicLeft",  // UpperBody/Magic/MagicLeft
                        cooldown = combatConfig.attackCooldown,
                        slotIndex = 0
                    },
                    rightAttack = new NPCCombatBrain.AttackSlot 
                    { 
                        animationState = "MagicRight",  // UpperBody/Magic/MagicRight
                        cooldown = combatConfig.attackCooldown * 1.2f,
                        slotIndex = 1
                    },
                    specialAttack = new NPCCombatBrain.AttackSlot 
                    { 
                        animationState = "MagicSpecial",  // UpperBody/Magic/MagicSpecial
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
                    windupMin = 0.05f,  // MUY rápido
                    windupMax = 0.25f,  // Variabilidad mínima
                    strafeFlipMin = 0.8f,  // Cambia dirección más rápido
                    strafeFlipMax = 2f,
                    dodgeDistance = 2f,
                    dodgeCooldown = 3f,
                    
                    // Micro-pausas MUCHO más variadas y aleatorias
                    microPauseDurationMin = 0.1f,  // Pausas muy cortas
                    microPauseDurationMax = 0.6f,  // A veces más largas
                    microPauseIntervalMin = 0.5f,  // Pausas MUY frecuentes
                    microPauseIntervalMax = 2f,    // Máxima variabilidad
                    
                    // Burst EXTREMADAMENTE variable (1-4 ataques)
                    burstRepositionDistance = 2.5f,
                    burstRepositionCooldown = 1.5f,  // Reposiciona MUY frecuentemente
                    burstAttacksMin = 1,  // A veces solo 1 ataque
                    burstAttacksMax = 4,  // A veces hasta 4 ataques
                    
                    // Ventanas de quieto MUY cortas y frecuentes
                    holdDurationMin = 0.2f,
                    holdDurationMax = 0.8f,
                    holdIntervalMin = 1f,
                    holdIntervalMax = 3f,
                    attackHoldSeconds = 0.05f,  // MUY corto - casi no se queda quieto después de atacar
                    
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
        
        /// <summary>
        /// Inicializa los componentes necesarios para el combate
        /// </summary>
        private void InitializeCombatComponents(Game.NPC.Common.NPCStateContext context)
        {
            if (context.Config == null || context.Config.combatConfig == null)
                return;
            
            var combatConfig = context.Config.combatConfig;
            
            // ✅ MANTENER Targetable ACTIVADO durante el combate (necesario para auto-targeting de hechizos del player)
            var targetable = context.Transform.GetComponent<Targetable>();
            if (targetable == null)
            {
                targetable = context.Transform.gameObject.AddComponent<Targetable>();
                context.Log("[CombatState] Targetable añadido al NPC para auto-targeting");
            }
            
            // Asegurar que Targetable esté SIEMPRE activado en combate
            if (!targetable.enabled)
            {
                targetable.enabled = true;
                context.Log("[CombatState] Targetable activado para permitir auto-targeting de hechizos del jugador");
            }
            
            // OCULTAR botón de interactuar durante el combate
            var interactable = context.Transform.GetComponent<Interactable>();
            if (interactable != null)
            {
                // Temporalmente deshabilitar la interacción durante el combate
                interactable.enabled = false;
                context.Log("[CombatState] Interactable deshabilitado durante el combate (el botón A no se mostrará)");
            }
            
            // Asegurar que tiene Damageable
            var damageable = context.Transform.GetComponent<Damageable>();
            if (damageable == null)
            {
                damageable = context.Transform.gameObject.AddComponent<Damageable>();
                context.Log("[CombatState] Damageable añadido al NPC");
            }
            
            // Configurar vida del Damageable SOLO si el NPC NO ha sido derrotado antes
            if (!context.WasDefeatedInCombat)
            {
                damageable.SetMaxAndCurrent(combatConfig.health, combatConfig.health);
            }
            else
            {
                context.Log("[CombatState] ⚠️ NPC ya fue derrotado - NO se reinicia la vida ni se permite combate");
                // Salir del estado de combate inmediatamente
                context.IsInCombat = false;
                return;
            }
            
            // Asegurar que tiene NPCCombatLifecycleHandler
            var lifecycleHandler = context.Transform.GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycleHandler == null)
            {
                lifecycleHandler = context.Transform.gameObject.AddComponent<NPCCombatLifecycleHandler>();
                lifecycleHandler.Initialize(); // ✅ IMPORTANTE: Inicializar manualmente cuando se añade en runtime
                context.Log("[CombatState] NPCCombatLifecycleHandler añadido al NPC");
            }
            
            // Asegurar que tiene NPCHealthBarSpawner
            var healthBarSpawner = context.Transform.GetComponent<NPCHealthBarSpawner>();
            if (healthBarSpawner == null)
            {
                healthBarSpawner = context.Transform.gameObject.AddComponent<NPCHealthBarSpawner>();
                context.Log("[CombatState] NPCHealthBarSpawner añadido al NPC");
            }
            
            // Asignar el prefab desde combatConfig si está configurado
            if (combatConfig.healthBarPrefab != null)
            {
                healthBarSpawner.SetHealthBarPrefab(combatConfig.healthBarPrefab);
                context.Log("[CombatState] Prefab de barra de vida asignado desde combatConfig");
            }
            
            // Spawnear la barra de vida al entrar en combate
            healthBarSpawner.SpawnHealthBar();
            context.Log("[CombatState] ✅ Solicitado spawn de barra de vida");
        }
        
        public override void OnUpdate(Game.NPC.Common.NPCStateContext context)
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
        
        public override void OnExit(Game.NPC.Common.NPCStateContext context)
        {
            base.OnExit(context);
            
            context.IsInCombat = false;
            
            // Detener combat brain
            if (_combatBrain != null && _combatBrainInitialized)
            {
                _combatBrain.StopCombat();
                context.Log("[CombatState] Combat brain stopped");
            }
            
            // ✅ ASEGURAR QUE EL COLLIDER SE ACTIVE DESPUÉS DEL COMBATE
            // Buscar TODOS los CapsuleColliders y activar el que es trigger (Interactable)
            var colliders = context.Transform.GetComponentsInChildren<CapsuleCollider>(true);
            foreach (var col in colliders)
            {
                // Solo activar si es trigger (el Interactable)
                if (col.isTrigger && !col.enabled)
                {
                    col.enabled = true;
                    context.Log($"[CombatState] ✅ CapsuleCollider trigger activado en {col.gameObject.name}");
                }
            }
            
            // Fallback: Si no hay colliders trigger, buscar en el propio Transform
            var capsuleCollider = context.Transform.GetComponent<CapsuleCollider>();
            if (capsuleCollider != null && !capsuleCollider.enabled)
            {
                // Si fue derrotado, debe ser trigger para la interacción
                if (context.WasDefeatedInCombat)
                {
                    capsuleCollider.isTrigger = true;
                    context.Log("[CombatState] ✅ CapsuleCollider activado como trigger (NPC derrotado)");
                }
                capsuleCollider.enabled = true;
            }
            
            // RE-HABILITAR Interactable después del combate (para mostrar el botón A de nuevo)
            var interactable = context.Transform.GetComponent<Interactable>();
            if (interactable != null && !interactable.enabled)
            {
                interactable.enabled = true;
                context.Log("[CombatState] Interactable re-habilitado después del combate");
            }
            
            // Resetear movimiento
            StopMovement(context);
        }
        
        public override Game.NPC.Common.INPCState CheckTransitions(Game.NPC.Common.NPCStateContext context)
        {
            // Prioridad máxima: Cinemática
            if (context.IsInCinematic)
            {
                return new CinematicState();
            }
            
            // Si el NPC fue derrotado, salir del combate inmediatamente
            if (context.WasDefeatedInCombat)
            {
                context.Log($"[{StateName}] NPC derrotado, saliendo de combate a Idle");
                return new IdleState();
            }
            
            // Si ya no está en combate (flag desactivado externamente), volver a idle
            if (!context.IsInCombat)
            {
                context.Log($"[{StateName}] Combate finalizado, volviendo a Idle");
                return new IdleState();
            }
            
            // TODO: Verificar otras condiciones:
            // - Jugador derrotado -> VictoryState
            // - Necesita huir -> FleeState
            
            return null; // Continuar en combate
        }
        
        /// <summary>
        /// Comportamiento de combate básico cuando no hay CombatBrain
        /// </summary>
        private void BasicCombatBehavior(Game.NPC.Common.NPCStateContext context)
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
        
        private void TriggerBattleMusic(Game.NPC.Common.NPCStateContext context)
        {
            if (context.Config == null || context.Config.combatConfig == null)
                return;
            
            var combatConfig = context.Config.combatConfig;
            
            // Enviar evento de batalla al sistema de audio
            if (!string.IsNullOrWhiteSpace(combatConfig.battleMusicId))
            {
                DefaultNarrativeSignals.Instance?.RaiseCustom($"BATTLE_START:{combatConfig.battleMusicId}");
                AudioService.Instance?.BeginBattleById(combatConfig.battleMusicId);
                context.Log($"[CombatState] Música de batalla activada: {combatConfig.battleMusicId}");
            }
        }
    }
}

