using UnityEngine;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado Dead - El NPC ha sido derrotado y está muerto.
    /// Este estado no permite transiciones y mantiene al NPC en su animación de muerte.
    /// </summary>
    public class DeadState : NPCStateBase
    {
        public override string StateName => "Dead";
        
        public override void OnEnter(Common.NPCStateContext context)
        {
            base.OnEnter(context);
            
            // NO llamar a StopMovement() aquí porque resetea el Animator y cancela la animación de muerte
            // En su lugar, solo detener el NavMeshAgent manualmente
            if (context.Agent != null)
            {
                Common.NavMeshAgentUtility.HardStop(context.Agent);
                context.Log("[DeadState] NavMeshAgent detenido");
            }
            
            // La animación de muerte ya fue reproducida por NPCCombatLifecycleHandler.DeathSequence()
            // NO tocar el Animator aquí para no interrumpir la animación
            
            // Desactivar el combate
            context.IsInCombat = false;
            
            // Marcar como derrotado
            context.WasDefeatedInCombat = true;
            
            context.Log("[DeadState] NPC muerto - Estado final alcanzado - Animación de muerte en progreso");
        }
        
        public override void OnUpdate(Common.NPCStateContext context)
        {
            base.OnUpdate(context);
            // El NPC está muerto, no hace nada
        }
        
        public override Common.INPCState CheckTransitions(Common.NPCStateContext context)
        {
            // No hay transiciones desde el estado Dead
            // El NPC permanece muerto permanentemente
            return null;
        }
        
        public override void OnExit(Common.NPCStateContext context)
        {
            base.OnExit(context);
            // Este estado no debería tener exit, pero por si acaso
            context.Log("[DeadState] ⚠️ Saliendo del estado Dead (esto no debería ocurrir)");
        }
    }
}

