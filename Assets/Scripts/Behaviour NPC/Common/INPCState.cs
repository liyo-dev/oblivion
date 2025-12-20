using UnityEngine;
namespace Game.NPC.Common
{
    /// <summary>
    /// Interfaz base para todos los estados de un NPC.
    /// Cada estado representa un comportamiento específico (Idle, Walking, Combat, Cinematic, etc.)
    /// </summary>
    public interface INPCState
    {
        /// <summary>
        /// Se llama una vez cuando el estado se activa
        /// </summary>
        void OnEnter(NPCStateContext context);
        /// <summary>
        /// Se llama cada frame mientras el estado está activo
        /// </summary>
        void OnUpdate(NPCStateContext context);
        /// <summary>
        /// Se llama una vez cuando el estado se desactiva
        /// </summary>
        void OnExit(NPCStateContext context);
        /// <summary>
        /// Verifica si este estado debe transicionar a otro
        /// </summary>
        /// <returns>El siguiente estado, o null si debe permanecer en el actual</returns>
        INPCState CheckTransitions(NPCStateContext context);
        /// <summary>
        /// Nombre del estado para debugging
        /// </summary>
        string StateName { get; }
    }
}
