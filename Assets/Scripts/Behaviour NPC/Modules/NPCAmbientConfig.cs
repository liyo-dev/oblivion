using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de comportamiento ambiental (Idle/Wander)
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Ambient_Config", menuName = "NPC/Módulos/Ambient Config", order = 1)]
    public class NPCAmbientConfig : NPCModuleConfigBase
    {
        [Header("Wander Settings")]
        [Tooltip("Si está activo, el NPC vagará dentro del radio indicado")]
        public bool enableWander = true;
        
        [Min(0f)]
        [Tooltip("Radio en el que el NPC puede caminar aleatoriamente")]
        public float wanderRadius = 6f;
        
        [Min(0f)]
        [Tooltip("Tiempo mínimo en estado Idle antes de volver a caminar")]
        public float minIdleTime = 1.2f;
        
        [Min(0f)]
        [Tooltip("Tiempo máximo en estado Idle antes de volver a caminar")]
        public float maxIdleTime = 3.0f;
        
        [Header("Movement")]
        [Min(0f)]
        [Tooltip("Velocidad de caminata")]
        public float walkSpeed = 1.5f;
        
        [Range(0f, 1f)]
        [Tooltip("Velocidad mínima de animación")]
        public float minAnimSpeed = 0.25f;
        
        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            
            if (enableWander && wanderRadius <= 0f)
            {
                errorMessage = "Wander radius debe ser mayor a 0 si enableWander está activo";
                return false;
            }
            
            if (maxIdleTime < minIdleTime)
            {
                errorMessage = "maxIdleTime debe ser mayor o igual a minIdleTime";
                return false;
            }
            
            return true;
        }
    }
}

