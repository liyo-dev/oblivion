using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de combate para NPCs
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Combat_Config", menuName = "NPC/Módulos/Combat Config", order = 2)]
    public class NPCCombatConfig : NPCModuleConfigBase
    {
        [Header("Combat Stats")]
        [Min(0f)]
        [Tooltip("Puntos de vida del NPC")]
        public float health = 100f;
        
        [Min(0f)]
        [Tooltip("Daño base de ataque")]
        public float attackDamage = 10f;
        
        [Min(0f)]
        [Tooltip("Tiempo entre ataques")]
        public float attackCooldown = 1.5f;
        
        [Header("Ranges")]
        [Min(0f)]
        [Tooltip("Rango de detección del jugador")]
        public float detectionRange = 10f;
        
        [Min(0f)]
        [Tooltip("Rango para ataques a distancia")]
        public float combatRange = 8f;
        
        [Min(0f)]
        [Tooltip("Rango para ataques cuerpo a cuerpo")]
        public float meleeRange = 2f;
        
        [Header("Behavior")]
        [Tooltip("¿El NPC es agresivo automáticamente o espera ser atacado?")]
        public bool isAggressive = true;
        
        [Tooltip("¿Puede el NPC perseguir al jugador fuera de su área inicial?")]
        public bool canChaseOutOfBounds = false;
        
        [Min(0f)]
        [Tooltip("Distancia máxima de persecución (si canChaseOutOfBounds = true)")]
        public float maxChaseDistance = 20f;
        
        [Header("Projectiles (Opcional)")]
        [Tooltip("Prefab de proyectil para ataques a distancia")]
        public GameObject projectilePrefab;
        
        [Tooltip("Punto de spawn del proyectil")]
        public Transform projectileSpawnPoint;
        
        [Min(0f)]
        [Tooltip("Velocidad del proyectil")]
        public float projectileSpeed = 15f;
        
        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            
            if (health <= 0f)
            {
                errorMessage = "Health debe ser mayor a 0";
                return false;
            }
            
            if (attackDamage < 0f)
            {
                errorMessage = "Attack damage no puede ser negativo";
                return false;
            }
            
            if (detectionRange < combatRange)
            {
                errorMessage = "Detection range debe ser mayor o igual a combat range";
                return false;
            }
            
            if (projectilePrefab != null && projectileSpeed <= 0f)
            {
                errorMessage = "Projectile speed debe ser mayor a 0 si hay projectile prefab";
                return false;
            }
            
            return true;
        }
    }
}

