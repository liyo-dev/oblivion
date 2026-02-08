using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado especial para posicionar a un party member durante un diálogo.
    /// El NPC se mueve a una posición específica (al lado del player) y se queda ahí quieto.
    /// </summary>
    public class DialoguePositionState : NPCStateBase
    {
        public override string StateName => "DialoguePosition";

        private readonly NPCPartyMember _partyMember;
        private readonly Vector3 _targetPosition;
        private readonly Transform _npcTarget;
        private readonly float _maxTime;
        
        private float _elapsedTime;
        private bool _hasReachedPosition;
        private bool _hasTeleported;
        
        private const float ARRIVAL_THRESHOLD = 0.5f;  // Distancia para considerar que llegó

        public DialoguePositionState(NPCPartyMember partyMember, Vector3 targetPosition, float maxTime, Transform npcTarget = null)
        {
            _partyMember = partyMember;
            _targetPosition = targetPosition;
            _maxTime = maxTime;
            _npcTarget = npcTarget;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            _elapsedTime = 0f;
            _hasReachedPosition = false;
            _hasTeleported = false;
            
            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                // Activar movimiento
                context.Agent.isStopped = false;
                context.Agent.updatePosition = true;
                context.Agent.updateRotation = true;
                context.Agent.speed = _partyMember.PartyConfig?.walkSpeed ?? 3.5f;
                
                // Establecer destino
                context.Agent.SetDestination(_targetPosition);
                
                Debug.Log($"[DialoguePositionState:{context.Transform.name}] 🎯 Moviéndose a posición de diálogo: {_targetPosition}");
            }
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (context.Agent == null || !context.Agent.isOnNavMesh)
                return;
            
            _elapsedTime += Time.deltaTime;
            
            // Verificar si llegó a la posición
            float distanceToTarget = Vector3.Distance(context.Transform.position, _targetPosition);
            
            if (!_hasReachedPosition && distanceToTarget <= ARRIVAL_THRESHOLD)
            {
                // ¡Llegó!
                _hasReachedPosition = true;
                
                // Detener movimiento
                context.Agent.isStopped = true;
                context.Agent.updatePosition = false;
                context.Agent.ResetPath();
                context.Animator?.SetMovementSpeed(0f);
                
                // ✅ Girar hacia el NPC con quien el player está hablando
                if (_npcTarget != null)
                {
                    Vector3 dirToNpc = (_npcTarget.position - context.Transform.position).normalized;
                    dirToNpc.y = 0;
                    
                    if (dirToNpc.sqrMagnitude > 0.01f)
                    {
                        context.Transform.rotation = Quaternion.LookRotation(dirToNpc);
                        Debug.Log($"[DialoguePositionState:{context.Transform.name}] 👁️ Mirando hacia {_npcTarget.name}");
                    }
                }
                // Fallback: mirar al player si no hay NPC target
                else if (PlayerService.TryGetPlayer(out var playerGo))
                {
                    Vector3 dirToPlayer = (playerGo.transform.position - context.Transform.position).normalized;
                    dirToPlayer.y = 0;
                    
                    if (dirToPlayer.sqrMagnitude > 0.01f)
                    {
                        context.Transform.rotation = Quaternion.LookRotation(dirToPlayer);
                    }
                }
                
                Debug.Log($"[DialoguePositionState:{context.Transform.name}] ✅ Llegó a posición de diálogo");
            }
            // Si tarda demasiado, teletransportar
            else if (!_hasTeleported && _elapsedTime > _maxTime && distanceToTarget > ARRIVAL_THRESHOLD)
            {
                _hasTeleported = true;
                
                // Teletransportar
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.Warp(_targetPosition);
                }
                else
                {
                    context.Transform.position = _targetPosition;
                }
                
                // Detener movimiento
                context.Agent.isStopped = true;
                context.Agent.updatePosition = false;
                context.Agent.ResetPath();
                context.Animator?.SetMovementSpeed(0f);
                
                _hasReachedPosition = true;
                
                Debug.Log($"[DialoguePositionState:{context.Transform.name}] ⚡ Teletransportado a posición de diálogo (tardó {_elapsedTime:F1}s)");
            }
            // Actualizar animación mientras se mueve
            else if (!_hasReachedPosition)
            {
                UpdateMovementAnimation(context);
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            // Restaurar valores por defecto
            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                context.Agent.updatePosition = true;
                context.Agent.updateRotation = true;
            }
            
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // Prioridad 1: Cinemática
            if (context.IsInCinematic) return new CinematicState();
            
            // Prioridad 2: Combate
            if (context.IsInCombat) return new AllyCombatState();
            
            // Prioridad 3: Ya no está en el party
            if (_partyMember == null || !_partyMember.IsInParty)
            {
                return new IdleState();
            }
            
            // Mantenerse en este estado hasta que se libere manualmente
            return null;
        }
    }
}
