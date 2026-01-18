using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración para NPCs que pueden unirse al equipo del jugador (Party).
    /// Define cómo se comporta el NPC cuando es compañero.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPartyConfig", menuName = "NPC/Party Config", order = 100)]
    public class NPCPartyConfig : NPCModuleConfigBase
    {
        [Header("Configuración de Seguimiento")]
        [Tooltip("Distancia ideal a mantener del jugador mientras sigue")]
        [Range(1f, 5f)]
        public float followDistance = 2.5f;
        
        [Tooltip("Si el compañero está más lejos que esta distancia, correrá para alcanzar")]
        [Range(5f, 15f)]
        public float runToPlayerDistance = 8f;
        
        [Tooltip("Si el compañero está más lejos que esta distancia, se teletransportará cerca del jugador")]
        [Range(15f, 50f)]
        public float teleportDistance = 25f;
        
        [Tooltip("Distancia mínima para detenerse cerca del jugador")]
        [Range(0.5f, 3f)]
        public float minStopDistance = 1.5f;

        [Header("Comportamiento de Idle")]
        [Tooltip("Tiempo mínimo de espera cuando está cerca del jugador")]
        [Range(0.5f, 5f)]
        public float minIdleTime = 1f;
        
        [Tooltip("Tiempo máximo de espera cuando está cerca del jugador")]
        [Range(1f, 10f)]
        public float maxIdleTime = 3f;
        
        [Tooltip("Si el compañero puede moverse libremente (wander) cuando está cerca del jugador")]
        public bool allowWanderNearPlayer = false;
        
        [Tooltip("Radio de wander cuando está cerca del jugador")]
        [Range(1f, 5f)]
        public float wanderRadiusNearPlayer = 2f;

        [Header("Posicionamiento")]
        [Tooltip("Offset lateral preferido (para que no todos los compañeros estén en línea)")]
        public Vector2 lateralOffsetRange = new Vector2(-1f, 1f);
        
        [Tooltip("Intentar posicionarse detrás del jugador")]
        public bool preferBehindPlayer = true;

        [Header("Combate en Grupo")]
        [Tooltip("El compañero ayudará automáticamente si el jugador entra en combate")]
        public bool autoJoinPlayerCombat = true;
        
        [Tooltip("Distancia de detección para unirse al combate del jugador")]
        [Range(5f, 20f)]
        public float combatAssistRange = 12f;
        
        [Header("Hechizos de Combate")]
        [Tooltip("Hechizo de mano izquierda (ataque rápido)")]
        public MagicSpellSO spellLeft;
        
        [Tooltip("Hechizo de mano derecha (ataque medio)")]
        public MagicSpellSO spellRight;
        
        [Tooltip("Hechizo especial (ataque potente)")]
        public MagicSpellSO spellSpecial;

        [Header("Distancias de Combate")]
        [Tooltip("Distancia mínima para atacar")]
        [Range(1f, 5f)]
        public float minAttackDistance = 2f;
        
        [Tooltip("Distancia máxima para atacar")]
        [Range(5f, 30f)]
        public float maxAttackDistance = 15f;
        
        /// <summary>
        /// Obtiene el hechizo por índice (0=Left, 1=Right, 2=Special)
        /// </summary>
        public MagicSpellSO GetSpell(int index)
        {
            return index switch
            {
                0 => spellLeft,
                1 => spellRight,
                2 => spellSpecial,
                _ => null
            };
        }
        
        /// <summary>
        /// Obtiene el cooldown del hechizo por índice (del propio SO)
        /// </summary>
        public float GetSpellCooldown(int index)
        {
            var spell = GetSpell(index);
            return spell != null ? spell.cooldown : 1f;
        }

        [Header("Visual")]
        [Tooltip("Icono para mostrar en la UI del equipo")]
        public Sprite partyIcon;
        
        [Tooltip("Nombre para mostrar en la UI")]
        public string displayName;

        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            bool isValid = true;

            if (followDistance >= runToPlayerDistance)
            {
                errorMessage += "followDistance debe ser menor que runToPlayerDistance.\n";
                isValid = false;
            }

            if (runToPlayerDistance >= teleportDistance)
            {
                errorMessage += "runToPlayerDistance debe ser menor que teleportDistance.\n";
                isValid = false;
            }

            if (minIdleTime > maxIdleTime)
            {
                errorMessage += "minIdleTime no puede ser mayor que maxIdleTime.\n";
                isValid = false;
            }

            return isValid;
        }
    }
}

