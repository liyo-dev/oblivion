using UnityEngine;

namespace Game.Interfaces
{
    /// <summary>
    /// Interfaz para cualquier entidad que pueda ser un objetivo en combate.
    /// Proporciona una forma unificada de identificar y obtener el punto de mira.
    /// </summary>
    public interface ICombatTarget
    {
        /// <summary>
        /// El Transform de la entidad.
        /// </summary>
        Transform TargetTransform { get; }

        /// <summary>
        /// El punto específico donde la cámara y los proyectiles deben apuntar.
        /// (ej: el centro de masa, la cabeza, etc.)
        /// </summary>
        Vector3 AimPoint { get; }

        /// <summary>
        /// ¿Está la entidad actualmente "viva" y puede ser un objetivo válido?
        /// </summary>
        bool IsTargetable { get; }
    }
}
