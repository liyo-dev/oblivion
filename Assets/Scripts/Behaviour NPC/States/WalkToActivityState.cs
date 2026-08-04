using UnityEngine;
using UnityEngine.AI;
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
        private bool _autoRotationDisabled;
        private bool _agentPositionDisabled;
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

            _occupied              = false;
            _autoRotationDisabled  = false;
            _agentPositionDisabled = false;
            _occupationTimer       = 0f;

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

            // Restaurar control de posición del NavMeshAgent
            if (_agentPositionDisabled && context.Agent != null && context.Agent.enabled)
            {
                // FIX INC-046: al sentarse, Arrive() coloca el transform EXACTAMENTE en la altura
                // del asiento (InteractionPosition), que suele estar por encima del NavMesh real
                // (bancos, sillas...), y desactiva updatePosition para no desviar esa altura.
                // Si al levantarse restaurábamos el control del agente tomando esa misma posición
                // elevada como "nextPosition", el NavMeshAgent quedaba pensando que esa altura de
                // asiento ES el suelo: no hay gravedad que lo baje (los agentes no simulan
                // físicas), así que el NPC se quedaba flotando ahí hasta el próximo Warp/teleport.
                // Encajamos primero la posición real sobre el NavMesh antes de devolver el control.
                Vector3 groundedPos = context.Transform.position;
                if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                {
                    groundedPos = navHit.position;
                    context.Transform.position = groundedPos;
                }

                context.Agent.Warp(groundedPos);
                context.Agent.nextPosition   = groundedPos;
                context.Agent.updatePosition = true;
                _agentPositionDisabled = false;
            }

            // Restaurar rotación automática si fue desactivada al ocupar el punto
            if (_autoRotationDisabled)
            {
                context.Animator?.EnableAutoRotation();
                _autoRotationDisabled = false;
            }

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            // Refugio de lluvia - levantarse/interrumpir la actividad para ir a resguardarse
            if (context.ShouldSeekShelter && context.CurrentShelter == null &&
                context.Config != null && context.Config.canSeekShelter)
            {
                return new SeekShelterState();
            }

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
            {
                context.Transform.rotation = _worldPoint.InteractionRotation;
                // Desactivar la rotación automática de NPCSimpleAnimator para que ApplySmoothRotation
                // (LateUpdate) no sobreescriba la orientación del punto con el _targetRotation antiguo
                context.Animator?.DisableAutoRotation();
                _autoRotationDisabled = true;
            }

            // Asignación directa de posición, igual que PlayerAmbientActivityHandler.SnapToSeat.
            // NO usar Warp(): proyecta al NavMesh más cercano y puede desviar la altura del asiento.
            // Desactivar updatePosition para que el agente no sobreescriba el transform cada frame.
            if (context.Agent != null && context.Agent.enabled)
            {
                context.Agent.updatePosition = false;
                context.Agent.nextPosition   = _worldPoint.InteractionPosition;
                _agentPositionDisabled = true;
            }
            context.Transform.position = _worldPoint.InteractionPosition;

            context.Animator?.PlayAmbientActivity(_worldPoint.activityType, _worldPoint);
        }
    }
}
