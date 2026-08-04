using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Se activa cuando empieza a llover (NPCWeatherAwareness.RainStarted, propagado a
    /// context.ShouldSeekShelter por NPCBehaviourManagerV2). El NPC camina hasta el
    /// NPCShelterPoint más cercano:
    ///   - TreeCanopy: se queda ahí parado hasta que deje de llover.
    ///   - HouseDoor: tras un breve delay ("abrir la puerta"), se desactiva por completo
    ///     simulando que ha entrado. NPCBehaviourManagerV2.HandleRainStopped() lo reactiva
    ///     directamente en la puerta cuando deja de llover — funciona incluso con el GameObject
    ///     desactivado, porque es una suscripción a un evento C#, no un callback del ciclo de
    ///     vida de Unity (Update no se llama en objetos inactivos).
    /// Si no hay ningún punto de refugio libre en rango, el NPC se queda en IdleState bajo la
    /// lluvia en vez de forzar un comportamiento sin sentido.
    /// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § A.2-A.4.
    /// </summary>
    public class SeekShelterState : NPCStateBase
    {
        public override string StateName => "SeekShelter";

        private const float MaxSearchDistance = 25f;
        private const float DoorOpenDelay = 0.7f;

        private NPCShelterPoint _shelterPoint;
        private bool _arrived;
        private bool _hidden;
        private float _doorTimer;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            _arrived   = false;
            _hidden    = false;
            _doorTimer = 0f;
            context.CurrentShelter = null;

            if (!NPCShelterPoint.TryFindNearest(context.Transform.position, null, MaxSearchDistance, out _shelterPoint))
            {
                // No hay refugio libre en rango: quedarse en Idle bajo la lluvia en vez de forzar
                // un comportamiento sin sentido (vagar buscando algo que no existe).
                context.Brain?.ChangeState(new IdleState());
                return;
            }

            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                float speed = context.Config?.walkSpeed ?? 1.5f;
                context.Agent.speed     = speed;
                context.Agent.isStopped = false;
            }

            SetDestination(context, _shelterPoint.InteractionPosition);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (_hidden) return; // GameObject desactivado: en la práctica esto ni se ejecuta

            if (!_arrived)
            {
                if (HasReachedDestination(context))
                    Arrive(context);
                return;
            }

            // Ya hemos llegado y ocupado el punto. Si es una puerta, esperar el delay de "abrir"
            // antes de desaparecer, para que no sea un desactivado instantáneo y brusco.
            if (_shelterPoint != null && _shelterPoint.shelterType == NPCShelterType.HouseDoor)
            {
                _doorTimer += Time.deltaTime;
                if (_doorTimer >= DoorOpenDelay)
                {
                    _hidden = true;
                    context.IsHiddenForShelter = true;
                    context.Transform.gameObject.SetActive(false);
                }
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_shelterPoint != null)
                _shelterPoint.Release(context.Transform);

            context.CurrentShelter     = null;
            context.IsHiddenForShelter = false;

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // Mientras el GameObject está desactivado (refugiado en una casa) esto nunca se
            // evalúa: Unity no llama Update en objetos inactivos. La reactivación la dispara
            // directamente NPCBehaviourManagerV2.HandleRainStopped().
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            if (!context.ShouldSeekShelter)
                return new WanderState();

            if (!_arrived && IsPathBlocked(context))
                return new IdleState();

            return null;
        }

        private void Arrive(NPCStateContext context)
        {
            if (!_shelterPoint.TryOccupy(context.Transform))
            {
                // Otro NPC ocupó el punto justo antes de que llegáramos: buscar otro desde aquí.
                context.Brain?.ChangeState(new SeekShelterState());
                return;
            }

            _arrived = true;
            context.CurrentShelter = _shelterPoint;

            StopMovement(context);

            if (_shelterPoint.overrideFacing)
                context.Transform.rotation = _shelterPoint.InteractionRotation;
        }
    }
}
