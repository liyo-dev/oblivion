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

        // =================================================================================
        // 🗣️ ENCUENTROS SOCIALES (compartido entre WanderState e IdleState)
        // =================================================================================

        protected static readonly int SocialNpcLayerMask = LayerMask.GetMask("Interactable", "Default");

        /// <summary>
        /// Busca NPCs cercanos con los que iniciar un encuentro social, ponderado por personalidad,
        /// y resuelve la relación combinando el valor forjado en runtime (NPCRelationshipRegistry)
        /// con el valor autor del NPCSocialConfig. Compartido entre WanderState e IdleState para
        /// que los NPCs también puedan socializar mientras están parados, no solo mientras caminan
        /// (ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § B.6.2).
        /// </summary>
        protected void CheckSocialEncounter(Common.NPCStateContext context, Collider[] socialBuffer)
        {
            var socialConfig = context.Config?.socialConfig;
            if (socialConfig == null) return;

            // Comprobar cooldown
            if (Time.time - context.LastSocialEncounterTime < socialConfig.socialCooldown) return;

            // El dado de sociabilidad: alta sociabilidad → mayor probabilidad de iniciar
            if (UnityEngine.Random.value > socialConfig.personality.sociability) return;

            float range = socialConfig.socialDetectionRange;
            int count = Physics.OverlapSphereNonAlloc(
                context.Transform.position, range, socialBuffer, SocialNpcLayerMask);

            for (int i = 0; i < count; i++)
            {
                var col = socialBuffer[i];
                if (col == null) continue;

                Transform root = col.transform.root;
                if (root == context.Transform.root) continue; // ignorar a sí mismo

                var partnerManager = root.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
                if (partnerManager == null) continue;

                // Relación "autor" (relationships[] del SO), igual que antes
                string partnerAuthoredId = partnerManager.Configuration?.socialConfig?.npcId;
                NPCRelationType authoredRelation = socialConfig.GetRelationshipWith(partnerAuthoredId);

                // Los enemigos no tienen encuentros sociales amistosos
                if (authoredRelation == NPCRelationType.Enemy) continue;

                // Relación "efectiva": prioriza el vínculo forjado en runtime sobre el autor
                string myId = context.RelationshipId;
                string partnerId = partnerManager.Context?.RelationshipId;
                NPCRelationType relation = NPCRelationshipRegistry.Resolve(myId, partnerId, authoredRelation);

                // Intentar que el otro NPC acepte el encuentro
                if (partnerManager.TryAcceptSocialEncounter(context.Transform, relation))
                {
                    // El partner aceptó: yo también entro en el encuentro
                    context.PendingSocialPartner  = root;
                    context.PendingSocialRelation = relation;
                    context.Brain?.ChangeState(new NPCSocialEncounterState());
                    return;
                }
            }
        }
    }
}
