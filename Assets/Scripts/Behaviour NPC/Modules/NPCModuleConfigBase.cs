using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Clase base abstracta para todos los módulos de configuración de NPC
    /// </summary>
    public abstract class NPCModuleConfigBase : ScriptableObject
    {
        [Header("Información del Módulo")]
        [Tooltip("Nombre descriptivo de este módulo")]
        public string moduleName;
        
        [TextArea(2, 4)]
        [Tooltip("Descripción opcional de lo que hace este módulo")]
        public string description;
        
        /// <summary>
        /// Valida que la configuración sea correcta
        /// </summary>
        public abstract bool ValidateConfig(out string errorMessage);
    }
}

