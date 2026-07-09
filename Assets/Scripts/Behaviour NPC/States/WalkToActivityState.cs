using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// El NPC camina hacia un NPCWorldPoint, ocupa el punto y realiza la actividad ambiental
    /// hasta que se cumpla el tiempo o llegue una interrupción.
    /// </summary>
    public class WalkToActivityState : NPCStateBase
    {
        public override string StateName => "WalkToActivity";

        private readonly NPCWorldPoint _worldPoint;
        private readonly bool _alreadyAtPoint;

        private bool _occupied;
        private float _occupationTimer;
        private float _occupationDuration;

        public WalkToActivityState(NPCWorldPoint worldPoint, bool alreadyAtPoint = false)
        {
            _worldPoint     = worldPoint;
            _alreadyAtPoint = alreadyAtPoint;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            _occupied        = false;
            _occupationTimer = 0f;

            if (_worldPoint == null || _worldPoint.IsOccupied)
            {
                context.Brain?.ChangeState(new IdleState());
                return;
            }

            if (_alreadyAtPoint)
            {
                Arrive(context);
                return;
            }

            // Caminar hacia el punto
            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                float speed = context.Config?.walkSpeed ?? 1.5f;
                context.Agent.speed     = speed;
                context.Agent.isStopped = false;
            }

            SetDestination(context, _worldPoint.InteractionPosition);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (_occupied)
            {
                _occupationTimer += Time.deltaTime;
                return;
            }

            // Comprobar si llegamos
            if (!_alreadyAtPoint && HasReachedDestination(context))
                Arrive(context);
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_occupied)
            {
                _worldPoint?.Release(context.Transform);
                if (_worldPoint != null)
                    context.Animator?.StopAmbientActivity(_worldPoint.activityType, _worldPoint);
            }

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            // Otro NPC quiere socializar: levantarse para el encuentro
            if (context.PendingSocialPartner != null)
                return new NPCSocialEncounterState();

            // Actividad completada: volver a vagar
            if (_occupied && _occupationTimer >= _occupationDuration)
                return new WanderState();

            // Camino inaccesible antes de llegar
            if (!_occupied && !_alreadyAtPoint && IsPathBlocked(context))
                return new IdleState();

            return null;
        }

        private void Arrive(NPCStateContext context)
        {
            // Intentar ocupar (otro NPC pudo llegar antes)
            if (!_worldPoint.TryOccupy(context.Transform))
            {
                context.Brain?.ChangeState(new WanderState());
                return;
            }

            _occupied         = true;
            _occupationDuration = Random.Range(_worldPoint.minOccupationDuration, _worldPoint.maxOccupationDuration);

            StopMovement(context);

            if (_worldPoint.overrideFacing)
                context.Transform.rotation = _worldPoint.InteractionRotation;

            context.Animator?.PlayAmbientActivity(_worldPoint.activityType, _worldPoint);
        }
    }
}
