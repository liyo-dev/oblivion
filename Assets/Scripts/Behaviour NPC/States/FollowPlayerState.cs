using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado FSM para NPCs que siguen al jugador como compañeros de equipo.
    /// Gestiona el movimiento inteligente, idle cerca del jugador, y transiciones.
    /// </summary>
    public class FollowPlayerState : NPCStateBase
    {
        public override string StateName => "FollowPlayer";

        private readonly NPCPartyMember _partyMember;
        private NPCPartyConfig _config;
        
        // Timers
        private float _pathUpdateTimer;
        private float _idleTimer;
        private float _idleDuration;
        
        // State tracking
        private bool _isIdle;
        private bool _isRunning;
        private Vector3 _lastPlayerPosition;
        private bool _hasRotatedToPlayer; // Para no rotar constantemente
        
        // Constants
        private const float PATH_UPDATE_INTERVAL = 0.3f;
        private const float PLAYER_MOVE_THRESHOLD = 0.5f;
        private const float ROTATION_SPEED_MULTIPLIER = 2f;
        private const float IDLE_EXIT_HYSTERESIS = 0.5f; // Umbral extra para salir de idle

        public FollowPlayerState(NPCPartyMember partyMember)
        {
            _partyMember = partyMember;
            _config = partyMember?.PartyConfig;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            // Fallback config si no hay una específica
            if (_config == null)
            {
                context.LogWarning("[FollowPlayer] No hay PartyConfig, usando valores por defecto");
            }
            
            _pathUpdateTimer = 0f;
            _idleTimer = 0f;
            _isIdle = false;
            _isRunning = false;
            _hasRotatedToPlayer = false;
            _lastPlayerPosition = context.Player?.position ?? Vector3.zero;
            
            // Configurar velocidad inicial
            SetWalkSpeed(context);
            
            // Recalcular destino inmediatamente
            UpdateDestination(context);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (context.Player == null) return;
            
            float distanceToPlayer = Vector3.Distance(context.Transform.position, context.Player.position);
            float followDist = _config?.followDistance ?? 2.5f;
            float runDist = _config?.runToPlayerDistance ?? 8f;
            float minStopDist = _config?.minStopDistance ?? 1.5f;
            
            // Aplicar histéresis: si ya está en idle, necesita alejarse más para salir
            float exitIdleDist = _isIdle ? (followDist + IDLE_EXIT_HYSTERESIS) : followDist;
            
            // 1. Decidir si correr o caminar (solo si no está idle)
            if (!_isIdle)
            {
                bool shouldRun = distanceToPlayer > runDist;
                if (shouldRun != _isRunning)
                {
                    _isRunning = shouldRun;
                    if (shouldRun) SetRunSpeed(context);
                    else SetWalkSpeed(context);
                }
            }
            
            // 2. Decidir si quedarse idle o moverse
            bool closeEnough = distanceToPlayer <= followDist;
            bool tooClose = distanceToPlayer <= minStopDist;
            bool playerMovedAway = distanceToPlayer > exitIdleDist;
            bool playerMovedSignificantly = PlayerMovedSignificantly(context);
            
            if (_isIdle)
            {
                // Ya está en idle - solo salir si el jugador se aleja lo suficiente o se mueve
                if (playerMovedAway || playerMovedSignificantly)
                {
                    ExitIdleMode(context);
                    _hasRotatedToPlayer = false;
                }
                else
                {
                    // Rotar hacia el jugador solo una vez al entrar en idle
                    if (!_hasRotatedToPlayer)
                    {
                        RotateTowardsPlayerSmooth(context);
                        // Marcar como rotado cuando está casi mirando al jugador
                        if (IsLookingAtPlayer(context))
                        {
                            _hasRotatedToPlayer = true;
                        }
                    }
                    _idleTimer += Time.deltaTime;
                }
            }
            else
            {
                // No está en idle - decidir si entrar
                if (tooClose || (closeEnough && !playerMovedSignificantly))
                {
                    EnterIdleMode(context);
                }
                else
                {
                    // Necesita moverse
                    _pathUpdateTimer += Time.deltaTime;
                    if (_pathUpdateTimer >= PATH_UPDATE_INTERVAL)
                    {
                        _pathUpdateTimer = 0f;
                        UpdateDestination(context);
                    }
                    
                    UpdateMovementAnimation(context);
                }
            }
            
            _lastPlayerPosition = context.Player.position;
        }

        public override void OnExit(NPCStateContext context)
        {
            StopMovement(context);
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Prioridad máxima: Cinemática
            if (context.IsInCinematic) return new CinematicState();
            
            // 2. Combate
            if (context.IsInCombat) return new CombatState();
            
            // 3. Interacción (quedarse en FollowPlayer pero pausar)
            // La interacción se maneja en el mismo estado
            
            // 4. Si ya no está en el equipo, volver a Idle
            if (_partyMember == null || !_partyMember.IsInParty)
            {
                return new IdleState();
            }
            
            return null;
        }

        #region Movement Helpers
        private void UpdateDestination(NPCStateContext context)
        {
            if (context.Player == null || !IsAgentValid(context)) return;
            
            // Obtener posición de formación
            Vector3 targetPos = _partyMember?.GetFormationPosition() ?? 
                CalculateFallbackPosition(context);
            
            // Validar en NavMesh
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                targetPos = hit.position;
            }
            
            SetDestination(context, targetPos);
        }

        private Vector3 CalculateFallbackPosition(NPCStateContext context)
        {
            if (context.Player == null) return context.Transform.position;
            
            float followDist = _config?.followDistance ?? 2.5f;
            bool preferBehind = _config?.preferBehindPlayer ?? true;
            
            Vector3 direction = preferBehind ? -context.Player.forward : 
                (context.Transform.position - context.Player.position).normalized;
            
            // Añadir offset lateral para no estar exactamente detrás
            float lateralOffset = Random.Range(
                _config?.lateralOffsetRange.x ?? -1f, 
                _config?.lateralOffsetRange.y ?? 1f
            );
            direction += context.Player.right * lateralOffset * 0.3f;
            
            return context.Player.position + direction.normalized * followDist;
        }

        private bool PlayerMovedSignificantly(NPCStateContext context)
        {
            if (context.Player == null) return false;
            return Vector3.Distance(context.Player.position, _lastPlayerPosition) > PLAYER_MOVE_THRESHOLD;
        }

        private void SetWalkSpeed(NPCStateContext context)
        {
            if (context.Agent != null && context.Config != null)
            {
                context.Agent.speed = context.Config.walkSpeed;
            }
        }

        private void SetRunSpeed(NPCStateContext context)
        {
            if (context.Agent != null && context.Config != null)
            {
                context.Agent.speed = context.Config.runSpeed;
            }
        }

        private void RotateTowardsPlayer(NPCStateContext context)
        {
            if (context.Player == null || context.Animator == null) return;
            
            Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
            directionToPlayer.y = 0;
            
            if (directionToPlayer != Vector3.zero)
            {
                float rotSpeed = (context.Config?.rotationSpeed ?? 180f) * ROTATION_SPEED_MULTIPLIER;
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                context.Transform.rotation = Quaternion.RotateTowards(
                    context.Transform.rotation, 
                    targetRotation, 
                    rotSpeed * Time.deltaTime
                );
            }
        }
        
        private void RotateTowardsPlayerSmooth(NPCStateContext context)
        {
            if (context.Player == null) return;
            
            Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
            directionToPlayer.y = 0;
            
            if (directionToPlayer.sqrMagnitude > 0.001f)
            {
                // Rotación más suave para evitar temblor
                float rotSpeed = (context.Config?.rotationSpeed ?? 180f) * 0.5f;
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                context.Transform.rotation = Quaternion.Slerp(
                    context.Transform.rotation, 
                    targetRotation, 
                    rotSpeed * Time.deltaTime * 0.1f
                );
            }
        }
        
        private bool IsLookingAtPlayer(NPCStateContext context)
        {
            if (context.Player == null) return true;
            
            Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
            directionToPlayer.y = 0;
            
            if (directionToPlayer.sqrMagnitude < 0.001f) return true;
            
            float angle = Vector3.Angle(context.Transform.forward, directionToPlayer);
            return angle < 10f; // Consideramos que mira al jugador si está dentro de 10 grados
        }
        #endregion

        #region Idle Mode
        private void EnterIdleMode(NPCStateContext context)
        {
            _isIdle = true;
            _idleTimer = 0f;
            _idleDuration = Random.Range(
                _config?.minIdleTime ?? 1f, 
                _config?.maxIdleTime ?? 3f
            );
            
            StopMovement(context);
        }

        private void ExitIdleMode(NPCStateContext context)
        {
            _isIdle = false;
            _idleTimer = 0f;
        }
        #endregion
    }
}

