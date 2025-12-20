using UnityEngine;
namespace Game.NPC.States
{
    public class IdleState : NPCStateBase
    {
        private float _idleTimer;
        private float _idleDuration;
        public override string StateName => "Idle";
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            StopMovement(context);
            if (context.Config != null)
            {
                _idleDuration = Random.Range(context.Config.minIdleTime, context.Config.maxIdleTime);
            }
            else
            {
                _idleDuration = 2f;
            }
            _idleTimer = 0f;
        }
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            _idleTimer += Time.deltaTime;
        }
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            if (context.IsInCinematic)
            {
                return new CinematicState();
            }
            if (context.IsInCombat)
            {
                return new CombatState();
            }
            if (context.IsInteracting)
            {
                return null;
            }
            if (context.Config != null && context.Config.enableWander && _idleTimer >= _idleDuration)
            {
                return new WanderState();
            }
            return null;
        }
    }
}
