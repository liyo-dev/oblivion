﻿using UnityEngine;
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
        
        // Estado Interno
        private NPCAlertIconController _iconController;
        private float _timer;
        private bool _waitingForDialogue;
        private bool _hasPlayedChallenge;
        private bool _dialogueCompleted; 
        
        // Timers de Animación
        private float _senseTimer;
        private const float SENSE_DURATION = 1.2f;

        public AlertState(float duration = 2f, bool walk = true, float stopDist = 3f)
        {
            _alertDuration = duration;
            _walkTowardsPlayer = walk;
            _stopDistance = stopDist;
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

            // 1. Audio & Icono
            TriggerAlertMusic(context);
            ShowAlertIcon(context);

            // 2. Detenerse y Girar
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

            // 4. Iniciar Diálogo (si existe)
            StartAlertDialogue(context);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            if (context.Player == null) return;

            // A. Gestionar Secuencia de Animación
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
                    // ✅ Usar NPCSimpleAnimator.FaceDirection en lugar de rotar el transform directamente
                    Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
                    directionToPlayer.y = 0;
                    if (directionToPlayer.sqrMagnitude > 0.01f && context.Animator != null)
                    {
                        context.Animator.FaceDirection(directionToPlayer);
                    }
                    return;
                }
            }

            // C. Lógica de Alerta Activa
            _timer += Time.deltaTime;

            if (_walkTowardsPlayer)
            {
                MoveAndRotate(context);
            }
            else
            {
                // Si no camina, solo rota hacia el jugador
                // ✅ Usar NPCSimpleAnimator.FaceDirection en lugar de rotar el transform directamente
                Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
                directionToPlayer.y = 0;
                if (directionToPlayer.sqrMagnitude > 0.01f && context.Animator != null)
                {
                    context.Animator.FaceDirection(directionToPlayer);
                }
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_iconController) _iconController.HideAlertIcon();
            context.Log("[AlertState] Fin de alerta.");
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.WasDefeatedInCombat) return new DeadState();

            // Si el jugador se esfuma
            if (context.Player == null) return new IdleState();

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
                
                // ✅ FIX: Usar NPCSimpleAnimator.FaceDirection en lugar de rotar directamente
                // Esto evita conflictos con ApplySmoothRotation en LateUpdate
                Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
                directionToPlayer.y = 0;
                
                if (directionToPlayer.sqrMagnitude > 0.01f && context.Animator != null)
                {
                    context.Animator.FaceDirection(directionToPlayer);
                }
            }
        }


        private void ShowAlertIcon(NPCStateContext context)
        {
            _iconController = context.Transform.GetComponent<NPCAlertIconController>();
            if (!_iconController) _iconController = context.Transform.gameObject.AddComponent<NPCAlertIconController>();

            var prefab = context.Config?.combatConfig?.alertIconPrefab;
            if (prefab) _iconController.ShowAlertIcon(prefab, _alertDuration);
        }

        private void TriggerAlertMusic(NPCStateContext context)
        {
            string eventId = context.Config?.combatConfig?.alertMusicEvent;
            if (!string.IsNullOrEmpty(eventId))
            {
                AudioService.Instance?.BeginAlertById(eventId);
            }
        }

        private void StartAlertDialogue(NPCStateContext context)
        {
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