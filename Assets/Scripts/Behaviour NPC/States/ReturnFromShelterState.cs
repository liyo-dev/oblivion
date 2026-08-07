using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Tras dejar de llover, el NPC que se había refugiado (SeekShelterState) camina de vuelta al
    /// punto exacto donde estaba antes de que empezara la tormenta
    /// (context.ShelterOriginPosition, capturado por SeekShelterState.OnEnter). Al llegar, o si el
    /// camino resulta bloqueado/inválido, pasa a IdleState con normalidad.
    ///
    /// Si vuelve a llover mientras el NPC todavía está de camino de vuelta, da media vuelta hacia
    /// SeekShelterState otra vez sin completar el regreso.
    ///
    /// Nota: si el regreso se interrumpe por combate/cinemática/interacción, al terminar ese estado
    /// externo el NPC vuelve a IdleState normal, no a este estado — no reanuda automáticamente el
    /// camino de vuelta. Es un caso borde poco frecuente (aceptado, no se resuelve aquí).
    /// </summary>
    public class ReturnFromShelterState : NPCStateBase
    {
        public override string StateName => "ReturnFromShelter";

        private bool _destinationSet;
        private bool _manualExitActive;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            _manualExitActive = false;

            if (context.HasShelterEdgePosition)
            {
                // El NPC quedó posicionado fuera del área caminable del NavMesh (bajo la copa de
                // un árbol, ver SeekShelterState / NPCStateBase.BeginManualApproach). Antes de
                // poder usar el NavMeshAgent para el camino de vuelta hay que sacarlo primero,
                // caminando a mano hasta el borde del NavMesh por el que entró.
                BeginManualApproach(context);
                _manualExitActive = true;
                return;
            }

            StartNormalReturn(context);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (!_manualExitActive) return;

            float speed = context.Config?.walkSpeed ?? 1.5f;
            if (ManualApproachStep(context, context.ShelterEdgePosition, speed))
            {
                EndManualApproach(context);
                context.HasShelterEdgePosition = false;
                _manualExitActive = false;
                StartNormalReturn(context);
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_manualExitActive)
            {
                EndManualApproach(context);
                _manualExitActive = false;
            }
            base.OnExit(context);
        }

        private void StartNormalReturn(NPCStateContext context)
        {
            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                float speed = context.Config?.walkSpeed ?? 1.5f;
                context.Agent.speed     = speed;
                context.Agent.isStopped = false;
            }

            _destinationSet = context.HasShelterOrigin &&
                SetDestination(context, context.ShelterOriginPosition);

            if (!_destinationSet)
            {
                // Sin origen válido guardado (o agente no disponible): no hay a dónde volver,
                // dar el regreso por completado aquí mismo.
                context.HasShelterOrigin = false;
            }
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            // Ha vuelto a llover a mitad de camino: dar media vuelta hacia el refugio otra vez.
            if (context.ShouldSeekShelter)
                return new SeekShelterState();

            if (_manualExitActive)
                return null; // todavía saliendo a mano de bajo el árbol, ver OnUpdate

            if (!_destinationSet)
            {
                context.HasShelterOrigin = false;
                return new IdleState();
            }

            if (IsPathBlocked(context))
            {
                context.LogWarning($"[{StateName}] Camino de vuelta bloqueado/inválido. Quedándose aquí.");
                context.HasShelterOrigin = false;
                return new IdleState();
            }

            if (HasReachedDestination(context))
            {
                context.HasShelterOrigin = false;
                return new IdleState();
            }

            return null;
        }
    }
}
