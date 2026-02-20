using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    public class FollowPlayerState : NPCStateBase
    {
        public override string StateName => "FollowPlayer";

        private readonly NPCPartyMember _partyMember;
        private NPCPartyConfig _config;
        
        private float _pathUpdateTimer;
        private float _stateTimer;
        private float _idleTimer;
        
        private bool _isInitialized;
        private Vector3 _lastPlayerPosition;
        
        private const float PATH_UPDATE_INTERVAL = 0.1f;
        private const float DEFAULT_STOP_DISTANCE = 1.2f;
        private const float DEFAULT_RUN_DISTANCE = 3f;
        private const float DEFAULT_WALK_SPEED = 3.5f;
        private const float DEFAULT_RUN_SPEED = 7.5f;
        private const float PLAYER_MOVE_THRESHOLD = 0.05f; // Reducido para mayor sensibilidad
        private const float INITIAL_DELAY = 0.3f;

        public FollowPlayerState(NPCPartyMember partyMember)
        {
            _partyMember = partyMember;
            _config = partyMember?.PartyConfig;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            if (context.Animator != null)
            {
                // Permitir que NPCSimpleAnimator controle la rotación
                context.Animator.EnableAutoRotation(); 
            }

            if (context.Agent != null)
            {
                context.Agent.isStopped = true;
                context.Agent.updateRotation = false; // NPCSimpleAnimator se encarga de la rotación
                context.Agent.speed = GetWalkSpeed();
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.ResetPath();
                }
            }
            
            context.Animator?.SetMovementSpeed(0f);
            
            if (context.Player != null)
            {
                _lastPlayerPosition = context.Player.position;
            }
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (context.Player == null || context.Agent == null || !context.Agent.isOnNavMesh) return;
            
            _stateTimer += Time.deltaTime;
            
            if (_stateTimer < INITIAL_DELAY)
            {
                context.Agent.isStopped = true;
                context.Animator?.SetMovementSpeed(0f);
                // Mirar al jugador incluso durante el delay inicial
                RotateTowardsPlayer(context);
                return;
            }
            
            if (!_isInitialized)
            {
                _isInitialized = true;
                _lastPlayerPosition = context.Player.position;
            }
            
            float distance = Vector3.Distance(context.Transform.position, context.Player.position);
            
            float stopDist = _config?.distanciaParaPararse ?? DEFAULT_STOP_DISTANCE;
            float runDist = _config?.distanciaParaCorrer ?? DEFAULT_RUN_DISTANCE;
            float walkSpeed = _config?.velocidadCaminando ?? DEFAULT_WALK_SPEED;
            float runSpeed = _config?.velocidadCorriendo ?? DEFAULT_RUN_SPEED;
            
            bool playerIsMoving = Vector3.Distance(_lastPlayerPosition, context.Player.position) > PLAYER_MOVE_THRESHOLD;
            if (playerIsMoving)
            {
                _lastPlayerPosition = context.Player.position;
                _idleTimer = 0f;
            }
            else
            {
                _idleTimer += Time.deltaTime;
            }

            // Si está muy cerca o el jugador está quieto por un momento, parar y mirar.
            if (distance <= stopDist || (!playerIsMoving && _idleTimer > 0.2f))
            {
                if (!context.Agent.isStopped)
                {
                    context.Agent.isStopped = true;
                    context.Agent.ResetPath();
                }
                context.Animator?.SetMovementSpeed(0f);
                
                // Siempre mirar al jugador cuando se está quieto.
                RotateTowardsPlayer(context);
                return;
            }
            
            // Si el jugador se mueve o el NPC está demasiado lejos, seguir.
            if (playerIsMoving || distance > stopDist * 1.2f)
            {
                context.Agent.isStopped = false;
                
                context.Agent.speed = distance > runDist ? runSpeed : walkSpeed;
                
                _pathUpdateTimer += Time.deltaTime;
                if (_pathUpdateTimer >= PATH_UPDATE_INTERVAL)
                {
                    _pathUpdateTimer = 0f;
                    UpdateDestination(context, stopDist);
                }
            }
            
            // NPCSimpleAnimator se encargará de la rotación basándose en la velocidad del NavMeshAgent.
            // No es necesario hacer nada más aquí para la rotación en movimiento.
        }
        
        private void RotateTowardsPlayer(NPCStateContext context)
        {
            if (context.Player == null || context.Animator == null) return;
            
            // Delegar la rotación a NPCSimpleAnimator
            context.Animator.FaceTarget(context.Player.position);
        }
        
        private void UpdateDestination(NPCStateContext context, float followDist)
        {
            bool preferBehind = _config?.quedarseDetras ?? true;
            
            Vector3 targetPos;
            if (preferBehind)
            {
                Vector3 behindOffset = -context.Player.forward * (followDist * 0.7f);
                targetPos = context.Player.position + behindOffset;
            }
            else
            {
                Vector3 direction = (context.Transform.position - context.Player.position).normalized;
                targetPos = context.Player.position + direction * followDist;
            }
            
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                if (Vector3.Distance(context.Agent.destination, hit.position) > 0.5f)
                {
                    context.Agent.SetDestination(hit.position);
                }
            }
            else
            {
                context.Agent.SetDestination(context.Player.position);
            }
        }
        
        private float GetWalkSpeed()
        {
            return _config?.velocidadCaminando ?? DEFAULT_WALK_SPEED;
        }

        public override void OnExit(NPCStateContext context)
        {
            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                context.Agent.isStopped = true;
            }
            
            if (context.Animator != null)
            {
                // Restaurar rotación automática por si otro estado la necesita
                context.Animator.EnableAutoRotation();
            }
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.IsInCombat) return new AllyCombatState();
            if (_partyMember == null || !_partyMember.IsInParty)
            {
                return new IdleState();
            }
            return null;
        }
    }
}
