﻿﻿﻿﻿using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de Alerta - El NPC detectó al jugador y se prepara para el combate.
    /// Muestra icono de alerta y camina hacia el jugador.
    /// </summary>
    public class AlertState : NPCStateBase
    {
        public override string StateName => "Alert";
        
        private NPCAlertIconController _alertIconController;
        private float _alertTimer;
        private float _alertDuration;
        private bool _walkTowardsPlayer;
        private float _stopDistance;
        private bool _waitingForDialogue;
        
        public AlertState(float alertDuration = 2f, bool walkTowardsPlayer = true, float stopDistance = 3f)
        {
            _alertDuration = alertDuration;
            _walkTowardsPlayer = walkTowardsPlayer;
            _stopDistance = stopDistance;
        }
        
        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            // ✅ VERIFICAR SI EL NPC YA FUE DERROTADO - NO permitir alerta
            if (context.WasDefeatedInCombat)
            {
                context.Log("[AlertState] ⛔ NPC ya fue derrotado - NO puede entrar en alerta");
                return;
            }
            
            context.Log("[AlertState] Jugador detectado - iniciando alerta");
            
            // Reproducir música de alerta
            TriggerAlertMusic(context);
            
            // Obtener o crear AlertIconController
            _alertIconController = context.Transform.GetComponent<NPCAlertIconController>();
            if (_alertIconController == null)
            {
                _alertIconController = context.Transform.gameObject.AddComponent<NPCAlertIconController>();
                context.Log("[AlertState] NPCAlertIconController creado");
            }
            
            // Mostrar icono de alerta
            ShowAlertIcon(context);
            
            // Detener movimiento inicial ANTES de girar y animar
            StopMovement(context);
            
            // Girar hacia el jugador INMEDIATAMENTE (rotación instantánea al entrar)
            if (context.Player != null)
            {
                Vector3 directionToPlayer = context.Player.position - context.Transform.position;
                directionToPlayer.y = 0f;
                
                if (directionToPlayer.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                    context.Transform.rotation = targetRotation;
                    context.Log("[AlertState] NPC girado hacia el jugador instantáneamente");
                }
            }
            
            // MANTENER el NPCSimpleAnimator activo - él maneja las animaciones correctamente
            if (context.Animator != null)
            {
                // Reproducir secuencia: Challenge → Idle_Battle
                // PlayChallengingForBattle() reproduce Challenge y cuando termina, va a Idle_Battle
                // Esto permite que el exit time de Idle_Battle permita transición natural a Locomotion
                context.Animator.PlayChallengingForBattle();
                context.Log("[AlertState] Reproduciendo Challenge → Idle de batalla");
            }
            
            // Iniciar diálogo de alerta si existe (DESPUÉS de girar)
            StartAlertDialogue(context);
            
            _alertTimer = 0f;
        }
        
        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (context.Player == null)
            {
                context.LogWarning("[AlertState] Jugador perdido durante alerta");
                return;
            }
            
            // SIEMPRE mirar al jugador durante la alerta (especialmente durante el diálogo)
            LookAtPlayer(context);
            
            // Si estamos esperando al diálogo, verificar si ha terminado
            if (_waitingForDialogue)
            {
                var dm = DialogueManager.Instance;
                if (dm != null && !dm.IsOpen)
                {
                    _waitingForDialogue = false;
                    context.Log("[AlertState] Diálogo de alerta completado");
                }
                else
                {
                    // Mientras el diálogo está abierto, solo mirar al jugador
                    LookAtPlayer(context);
                    return;
                }
            }
            
            // Incrementar temporizador
            _alertTimer += Time.deltaTime;
            
            // Mirar hacia el jugador
            LookAtPlayer(context);
            
            // Caminar hacia el jugador si está configurado
            if (_walkTowardsPlayer)
            {
                MoveTowardsPlayer(context);
            }
        }
        
        public override void OnExit(NPCStateContext context)
        {
            base.OnExit(context);
            
            // Ocultar icono de alerta
            if (_alertIconController != null)
            {
                _alertIconController.HideAlertIcon();
            }
            
            context.Log("[AlertState] Saliendo de estado de alerta");
        }
        
        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // Prioridad máxima: Cinemática
            if (context.IsInCinematic)
            {
                return new CinematicState();
            }
            
            // Si el jugador desaparece, volver a idle
            if (context.Player == null)
            {
                context.Log("[AlertState] Jugador perdido, volviendo a Idle");
                return new IdleState();
            }
            
            // Si aún estamos esperando el diálogo, no transicionar
            if (_waitingForDialogue)
            {
                return null;
            }
            
            // Si se completó la alerta, transicionar a combate
            if (_alertTimer >= _alertDuration)
            {
                context.Log("[AlertState] Alerta completada, iniciando combate");
                context.IsInCombat = true;
                return new CombatState();
            }
            
            return null; // Continuar en alerta
        }
        
        private void StartAlertDialogue(NPCStateContext context)
        {
            if (context.Config == null || context.Config.combatConfig == null)
                return;
            
            var combatConfig = context.Config.combatConfig;
            
            // Si hay diálogo de alerta configurado
            if (combatConfig.dialogueOnAlert != null)
            {
                var dm = DialogueManager.Instance;
                if (dm != null)
                {
                    context.Log("[AlertState] Iniciando diálogo de alerta");
                    
                    // Iniciar el diálogo
                    dm.StartDialogue(combatConfig.dialogueOnAlert, context.Transform, () =>
                    {
                        context.Log("[AlertState] Diálogo de alerta finalizado");
                        _waitingForDialogue = false;
                    });
                    
                    // Si está configurado para esperar, activar flag
                    if (combatConfig.waitForAlertDialogue)
                    {
                        _waitingForDialogue = true;
                        context.Log("[AlertState] Esperando a que termine el diálogo antes de continuar");
                    }
                }
            }
        }
        
        private void ShowAlertIcon(NPCStateContext context)
        {
            if (_alertIconController == null || context.Config == null)
                return;
            
            // Intentar mostrar desde combatConfig
            if (context.Config.combatConfig != null)
            {
                var combatConfig = context.Config.combatConfig;
                
                if (combatConfig.alertIconPrefab != null)
                {
                    _alertIconController.ShowAlertIcon(combatConfig.alertIconPrefab, _alertDuration);
                    context.Log("[AlertState] Mostrando icono de alerta (prefab)");
                    return;
                }
            }
            
            context.LogWarning("[AlertState] No hay icono de alerta configurado (debe ser un prefab)");
        }
        
        private void LookAtPlayer(NPCStateContext context)
        {
            if (context.Player == null) return;
            
            Vector3 directionToPlayer = context.Player.position - context.Transform.position;
            directionToPlayer.y = 0f;
            
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                
                // Rotación más rápida durante diálogo para que sea más responsive
                float rotationSpeed = _waitingForDialogue ? 10f : 5f;
                
                context.Transform.rotation = Quaternion.Slerp(
                    context.Transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotationSpeed
                );
            }
        }
        
        private void MoveTowardsPlayer(NPCStateContext context)
        {
            if (context.Player == null || context.Agent == null) return;
            
            float distanceToPlayer = Vector3.Distance(context.Transform.position, context.Player.position);
            
            // Si ya está lo suficientemente cerca, detenerse
            if (distanceToPlayer <= _stopDistance)
            {
                StopMovement(context);
                return;
            }
            
            // Moverse hacia el jugador
            if (context.Agent.isOnNavMesh)
            {
                context.Agent.isStopped = false;
                context.Agent.SetDestination(context.Player.position);
                
                // Actualizar animación de movimiento
                if (context.Animator != null)
                {
                    float speedFactor = context.Agent.velocity.magnitude / context.Agent.speed;
                    context.Animator.SetMovementSpeed(speedFactor);
                }
            }
        }
        
        private void TriggerAlertMusic(NPCStateContext context)
        {
            if (context.Config == null || context.Config.combatConfig == null)
                return;
            
            var combatConfig = context.Config.combatConfig;
            
            // Enviar evento de alerta al sistema de audio
            if (!string.IsNullOrWhiteSpace(combatConfig.alertMusicEvent))
            {
                DefaultNarrativeSignals.Instance?.RaiseCustom(combatConfig.alertMusicEvent);
                AudioService.Instance?.BeginAlertById(combatConfig.alertMusicEvent);
                context.Log($"[AlertState] Música de alerta activada: {combatConfig.alertMusicEvent}");
            }
        }
    }
}

