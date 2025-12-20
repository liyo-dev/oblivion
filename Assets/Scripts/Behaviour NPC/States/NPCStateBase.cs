using UnityEngine;
namespace Game.NPC.States
{
    public abstract class NPCStateBase : Common.INPCState
    {
        public abstract string StateName { get; }
        public virtual void OnEnter(Common.NPCStateContext context)
        {
            context.Log($"[{StateName}] OnEnter");
        }
        public virtual void OnUpdate(Common.NPCStateContext context) { }
        public virtual void OnExit(Common.NPCStateContext context)
        {
            context.Log($"[{StateName}] OnExit");
            if (context.Config != null && context.Config.resetAnimationOnStateExit)
            {
                context.Animator?.ResetMovement();
            }
        }
        public abstract Common.INPCState CheckTransitions(Common.NPCStateContext context);
        protected bool IsAgentValid(Common.NPCStateContext context)
        {
            return context.Agent != null && 
                   context.Agent.enabled && 
                   context.Agent.isOnNavMesh;
        }
        protected bool HasReachedDestination(Common.NPCStateContext context)
        {
            if (!IsAgentValid(context))
                return false;
            var agent = context.Agent;
            var config = context.Config;
            if (agent.pathPending)
                return false;
            float stoppingDist = config != null ? config.stoppingDistance : 0.5f;
            return agent.remainingDistance <= stoppingDist + 0.1f;
        }
        protected bool IsPathBlocked(Common.NPCStateContext context)
        {
            if (!IsAgentValid(context))
                return true;
            var agent = context.Agent;
            return agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathPartial ||
                   agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathInvalid;
        }
        protected void UpdateMovementAnimation(Common.NPCStateContext context)
        {
            if (!IsAgentValid(context) || context.Animator == null)
                return;
            float speedFactor = Common.NavMeshAgentUtility.ComputeSpeedFactor(context.Agent);
            if (context.Config != null && speedFactor > 0f)
            {
                speedFactor = Mathf.Max(speedFactor, context.Config.minAnimSpeed);
            }
            context.Animator.SetMovementSpeed(speedFactor);
        }
        protected bool SetDestination(Common.NPCStateContext context, Vector3 destination)
        {
            if (!IsAgentValid(context))
            {
                context.LogWarning($"[{StateName}] Agent no válido, no se puede establecer destino");
                return false;
            }
            context.TargetDestination = destination;
            context.HasReachedDestination = false;
            Common.NavMeshAgentUtility.SetDestination(context.Agent, destination);
            return true;
        }
        protected void StopMovement(Common.NPCStateContext context)
        {
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
            }
            if (context.Animator != null)
            {
                context.Animator.ResetMovement();
            }
        }
    }
}
