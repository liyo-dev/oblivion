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
        [Header("=== INFORMACIÓN DEL COMPAÑERO ===")]
        
        [Tooltip("Nombre que se muestra en la UI (si está vacío, usa el narrativeID o nombre del GameObject)")]
        public string displayName;
        
        [Header("=== SEGUIMIENTO DEL JUGADOR ===")]
        
        [Tooltip("Distancia a la que se PARA cuando alcanza al jugador (mientras más bajo, más cerca se queda)")]
        [Range(0.8f, 4f)]
        public float distanciaParaPararse = 1.2f; // CAMBIADO: Más cerca (era 2f)
        
        [Tooltip("Si está MÁS LEJOS de esta distancia, empezará a CORRER para alcanzarte")]
        [Range(2f, 10f)]
        public float distanciaParaCorrer = 3f; // CAMBIADO: Correr antes (era 4f)
        
        [Tooltip("Si está MÁS LEJOS de esta distancia, se TELETRANSPORTA a tu lado")]
        [Range(10f, 50f)]
        public float distanciaParaTeletransporte = 15f; // CAMBIADO: Teleport antes (era 20f)

        [Header("=== VELOCIDAD DE MOVIMIENTO ===")]
        
        [Tooltip("Velocidad al CAMINAR (cuando está cerca del jugador)")]
        [Range(1f, 5f)]
        public float velocidadCaminando = 3.5f;
        
        [Tooltip("Velocidad al CORRER (cuando el jugador corre o está lejos)")]
        [Range(4f, 10f)]
        public float velocidadCorriendo = 7.5f; // CAMBIADO: Un poco más rápido (era 7f)

        [Header("=== POSICIONAMIENTO ===")]
        
        [Tooltip("Intentar quedarse DETRÁS del jugador")]
        public bool quedarseDetras = true;
        
        [Tooltip("Offset lateral (para que no esté exactamente detrás)")]
        public Vector2 offsetLateral = new Vector2(-1f, 1f);

        [Header("=== COMPORTAMIENTO IDLE ===")]
        
        [Tooltip("Tiempo mínimo que espera quieto cuando está cerca del jugador")]
        [Range(0.5f, 5f)]
        public float tiempoIdleMinimo = 1f;
        
        [Tooltip("Tiempo máximo que espera quieto")]
        [Range(1f, 10f)]
        public float tiempoIdleMaximo = 3f;
        
        [Tooltip("Puede moverse un poco mientras espera cerca del jugador")]
        public bool puedeVagarCerca = false;
        
        [Tooltip("Radio de vagabundeo cerca del jugador")]
        [Range(1f, 5f)]
        public float radioVagabundeo = 2f;

        [Header("=== COMBATE EN GRUPO ===")]
        
        [Tooltip("Entra en combate automáticamente cuando el jugador ataca")]
        public bool autoUnirseACombate = true;
        
        [Tooltip("Distancia máxima para detectar enemigos y unirse al combate")]
        [Range(5f, 50f)]
        public float rangoAsistenciaCombate = 30f;
        
        [Header("=== HECHIZOS DE COMBATE ===")]
        
        [Tooltip("Hechizo principal (mano izquierda)")]
        public MagicSpellSO spellLeft;
        
        [Tooltip("Hechizo secundario (mano derecha)")]
        public MagicSpellSO spellRight;
        
        [Tooltip("Hechizo especial (más potente)")]
        public MagicSpellSO spellSpecial;

        [Header("=== DISTANCIAS DE ATAQUE ===")]
        
        [Tooltip("Distancia mínima para atacar enemigos")]
        [Range(1f, 5f)]
        public float distanciaMinimaAtaque = 2f;
        
        [Tooltip("Distancia máxima para atacar enemigos")]
        [Range(5f, 30f)]
        public float distanciaMaximaAtaque = 15f;

        // ============ PROPIEDADES DE COMPATIBILIDAD (para que el código existente siga funcionando) ============
        
        // Seguimiento
        public float followDistance => distanciaParaPararse;
        public float runToPlayerDistance => distanciaParaCorrer;
        public float teleportDistance => distanciaParaTeletransporte;
        public float minStopDistance => distanciaParaPararse * 0.8f; // Un poco más cerca que followDistance
        
        // Velocidades
        public float walkSpeed => velocidadCaminando;
        public float runSpeed => velocidadCorriendo;
        
        // Posicionamiento
        public bool preferBehindPlayer => quedarseDetras;
        public Vector2 lateralOffsetRange => offsetLateral;
        
        // Idle
        public float minIdleTime => tiempoIdleMinimo;
        public float maxIdleTime => tiempoIdleMaximo;
        public bool allowWanderNearPlayer => puedeVagarCerca;
        public float wanderRadiusNearPlayer => radioVagabundeo;
        
        // Combate
        public bool autoJoinPlayerCombat => autoUnirseACombate;
        public float combatAssistRange => rangoAsistenciaCombate;
        public float minAttackDistance => distanciaMinimaAtaque;
        public float maxAttackDistance => distanciaMaximaAtaque;

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
        /// Valida que la configuración sea correcta
        /// </summary>
        public override bool ValidateConfig(out string errorMessage)
        {
            // Validar que las distancias tengan sentido
            if (distanciaParaCorrer <= distanciaParaPararse)
            {
                errorMessage = "La distancia para correr debe ser mayor que la distancia para pararse";
                return false;
            }
            
            if (distanciaParaTeletransporte <= distanciaParaCorrer)
            {
                errorMessage = "La distancia para teletransporte debe ser mayor que la distancia para correr";
                return false;
            }
            
            if (velocidadCorriendo <= velocidadCaminando)
            {
                errorMessage = "La velocidad corriendo debe ser mayor que la velocidad caminando";
                return false;
            }
            
            if (tiempoIdleMaximo <= tiempoIdleMinimo)
            {
                errorMessage = "El tiempo idle máximo debe ser mayor que el mínimo";
                return false;
            }
            
            if (distanciaMaximaAtaque <= distanciaMinimaAtaque)
            {
                errorMessage = "La distancia máxima de ataque debe ser mayor que la mínima";
                return false;
            }
            
            errorMessage = string.Empty;
            return true;
        }
    }
}
