using UnityEngine;
using System.Linq;

// Updated: 2025-12-23 - Simplified to conditional-only mode
namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de cadena narrativa interactiva para NPCs.
    /// Permite encadenar diálogos, movimientos, animaciones, quests, combat, etc.
    /// Solo usa modo condicional - para narrativa simple sin condiciones, añade una con condición 'None'.
    /// 
    /// COMPORTAMIENTO DE PERSISTENCIA:
    /// - singleUse = true: La narrativa se ejecuta UNA VEZ por partida. Después de completarse, no se repetirá.
    /// - persistState = true: El estado 'completado' se guarda en el preset cuando el jugador hace SAVE.
    /// 
    /// NUEVA PARTIDA vs CARGAR PARTIDA:
    /// - Al crear una NUEVA PARTIDA: El preset es limpio, todas las narrativas están disponibles de nuevo.
    /// - Al CARGAR una partida guardada: Se restaura el estado guardado (narrativas completadas siguen completadas).
    /// 
    /// GESTIÓN DE CAPAS (Layer Management):
    /// - initialLayer: Define la capa del NPC al iniciar. Usa 'Interactable' si autoStartOnPlayerDetection está desactivado.
    /// - switchToEnemyLayerOnCombat: Cambia automáticamente a la capa 'Enemy' al ejecutar una acción StartCombat.
    /// - Después de ser derrotado en combate, el NPC vuelve automáticamente a la capa 'Interactable'.
    /// 
    /// FLUJO TÍPICO CON COMBATE:
    /// 1. NPC en capa "Interactable" → Jugador puede interactuar
    /// 2. Se ejecuta la narrativa (diálogos, animaciones, etc.)
    /// 3. Al llegar a StartCombat → NPC cambia a capa "Enemy" automáticamente
    /// 4. Combate ocurre normalmente
    /// 5. Al ser derrotado → NPC vuelve a capa "Interactable" (diálogo post-derrota)
    /// 
    /// EJEMPLO:
    /// 1. Nueva partida → Narrativa disponible
    /// 2. Ejecutas la narrativa → Se marca como completada (si singleUse=true)
    /// 3. Guardas la partida → El estado se guarda en el preset (si persistState=true)
    /// 4. Cargas esa partida → La narrativa sigue completada
    /// 5. NUEVA PARTIDA (diferente) → La narrativa vuelve a estar disponible ✅
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_InteractiveNarrative_Config", menuName = "NPC/Módulos/Interactive Narrative Config", order = 5)]
    public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
    {
        [Header("Narrativas Condicionales")]
        [Tooltip("Lista de narrativas con condiciones. Se evalúan en orden de prioridad. Para narrativa simple sin condiciones, añade una con condición 'None'.")]
        public ConditionalNarrative[] conditionalNarratives = System.Array.Empty<ConditionalNarrative>();

        [Header("Configuración")]
        [Tooltip("¿Esta narrativa solo se puede ejecutar una vez POR PARTIDA? Si es true, después de completarse no volverá a ejecutarse en esa partida. Al crear una NUEVA PARTIDA se resetea.")]
        public bool singleUse = true;

        [Tooltip("¿Guardar el estado en el preset de la partida? Si es true, el estado de 'completado' se guardará cuando el jugador haga SAVE. Al cargar esa partida guardada, se restaurará. NOTA: Al crear una nueva partida, el preset es limpio y todas las narrativas vuelven a estar disponibles.")]
        public bool persistState = true;

        [Tooltip("ID único para persistencia (generado automáticamente). Usado para identificar esta narrativa en el sistema de guardado.")]
        public string persistenceId;

        [Header("Behavior")]
        [Tooltip("¿El NPC gira hacia el jugador al interactuar?")]
        public bool rotateToPlayerOnInteract = true;

        [Min(0f)]
        [Tooltip("Duración de la rotación")]
        public float rotationDuration = 0.3f;
        
        [Header("Layer Management")]
        [Tooltip("Capa inicial del NPC. Si autoStartOnPlayerDetection está desactivado, se recomienda 'Interactable' para poder interactuar con el NPC. Cambiará automáticamente a 'Enemy' al iniciar combate si hay una acción StartCombat.")]
        public LayerMode initialLayer = LayerMode.Interactable;
        
        [Tooltip("¿Cambiar automáticamente a la capa 'Enemy' cuando se inicie un combate (acción StartCombat)?")]
        public bool switchToEnemyLayerOnCombat = true;

        [Header("Auto-Inicio (Alerta)")]
        [Tooltip("¿El NPC detecta al jugador y comienza la narrativa automáticamente?")]
        public bool autoStartOnPlayerDetection;

        [Tooltip("Rango de detección del jugador (solo si autoStartOnPlayerDetection = true)")]
        [Min(1f)]
        public float detectionRange = 10f;

        [Tooltip("Prefab del icono que aparece sobre la cabeza al detectar al jugador (GameObject con Canvas configurado)")]
        public GameObject alertIconPrefab;

        [Tooltip("Duración del icono de alerta antes de comenzar la narrativa (segundos)")]
        [Min(0.1f)]
        public float alertIconDuration = 1f;

        [Tooltip("¿El NPC camina hacia el jugador durante la alerta?")]
        public bool walkTowardsPlayerOnAlert = true;

        [Tooltip("Distancia mínima para detenerse al acercarse al jugador")]
        [Min(0.5f)]
        public float stopDistanceFromPlayer = 2f;

        [Header("Estado Post-Narrativa")]
        [Tooltip("¿Qué hace el NPC después de completar toda la cadena?")]
        public PostNarrativeState postNarrativeState = PostNarrativeState.Idle;

        [Tooltip("Config ambient si postNarrativeState = SwitchToAmbient")]
        public NPCAmbientConfig postNarrativeAmbientConfig;

        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";

            if (conditionalNarratives == null || conditionalNarratives.Length == 0)
            {
                errorMessage = "Debes añadir al menos una narrativa condicional. Para una narrativa simple sin condiciones, añade una con condición 'None'.";
                return false;
            }

            for (int i = 0; i < conditionalNarratives.Length; i++)
            {
                var condNarrative = conditionalNarratives[i];
                
                if (condNarrative == null)
                {
                    errorMessage = $"Conditional narrative {i} es null";
                    return false;
                }

                if (condNarrative.narrativeChain == null || condNarrative.narrativeChain.Length == 0)
                {
                    errorMessage = $"Conditional narrative {i} ('{condNarrative.description}') no tiene acciones";
                    return false;
                }

                for (int j = 0; j < condNarrative.narrativeChain.Length; j++)
                {
                    if (!ValidateChainEntry(condNarrative.narrativeChain[j], j, out errorMessage))
                    {
                        errorMessage = $"Conditional narrative {i} ('{condNarrative.description}'): {errorMessage}";
                        return false;
                    }
                }
                
                // Validar evento narrativo
                if (condNarrative.sendNarrativeEvent && string.IsNullOrEmpty(condNarrative.narrativeEventKey))
                {
                    errorMessage = $"Conditional narrative {i} ('{condNarrative.description}'): sendNarrativeEvent activado pero narrativeEventKey vacío";
                    return false;
                }
            }

            if (postNarrativeState == PostNarrativeState.SwitchToAmbient && postNarrativeAmbientConfig == null)
            {
                errorMessage = "PostNarrativeState = SwitchToAmbient requiere postNarrativeAmbientConfig";
                return false;
            }

            // Validar configuración de auto-inicio
            if (autoStartOnPlayerDetection)
            {
                if (detectionRange <= 0f)
                {
                    errorMessage = "Auto-inicio requiere detectionRange mayor a 0";
                    return false;
                }

                if (alertIconPrefab == null)
                {
                    Debug.LogWarning("[NPCInteractiveNarrativeConfig] Auto-inicio configurado pero no hay alertIconPrefab asignado");
                }
            }

            return true;
        }
        
        private bool ValidateChainEntry(NarrativeChainEntry entry, int index, out string errorMessage)
        {
            errorMessage = "";
            
            if (entry == null)
            {
                errorMessage = $"Entry {index} es null";
                return false;
            }

            // Validar según tipo de acción
            switch (entry.actionType)
            {
                case NarrativeActionType.Dialogue:
                    if (entry.dialogue == null)
                    {
                        errorMessage = $"Entry {index} tipo Dialogue requiere un DialogueAsset";
                        return false;
                    }
                    break;

                case NarrativeActionType.Move:
                    if (string.IsNullOrEmpty(entry.targetAnchorName) && entry.targetTransform == null)
                    {
                        errorMessage = $"Entry {index} tipo Move requiere targetAnchorName o targetTransform";
                        return false;
                    }
                    break;

                case NarrativeActionType.PlayAnimation:
                    if (string.IsNullOrEmpty(entry.animationTrigger))
                    {
                        errorMessage = $"Entry {index} tipo PlayAnimation requiere animationTrigger";
                        return false;
                    }
                    break;

                case NarrativeActionType.StartQuest:
                    if (entry.questToStart == null)
                    {
                        errorMessage = $"Entry {index} tipo StartQuest requiere questToStart";
                        return false;
                    }
                    break;

                case NarrativeActionType.StartCombat:
                    if (entry.combatConfig == null)
                    {
                        errorMessage = $"Entry {index} tipo StartCombat requiere combatConfig (NPCCombatConfig)";
                        return false;
                    }
                    break;
            }
            
            return true;
        }
        
        /// <summary>
        /// Obtiene la narrativa condicional que debe ejecutarse según las condiciones actuales
        /// </summary>
        public ConditionalNarrative GetActiveNarrative()
        {
            if (conditionalNarratives == null)
                return null;
            
            // Ordenar por prioridad (mayor a menor) y evaluar
            var sortedNarratives = conditionalNarratives
                .Where(n => n != null)
                .OrderByDescending(n => n.priority)
                .ToArray();
            
            foreach (var narrative in sortedNarratives)
            {
                if (narrative.CanExecute())
                {
                    return narrative;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Verifica si hay alguna narrativa disponible para ejecutar
        /// </summary>
        public bool HasAvailableNarrative()
        {
            return GetActiveNarrative() != null;
        }
        
        private void OnValidate()
        {
            // Auto-generar ID de persistencia si está vacío y se requiere persistencia
            if (persistState && string.IsNullOrEmpty(persistenceId))
            {
                persistenceId = System.Guid.NewGuid().ToString();
            }
        }
    }

    /// <summary>
    /// Estado del NPC después de completar la narrativa
    /// </summary>
    public enum PostNarrativeState
    {
        Idle,               // Se queda en Idle
        Wander,            // Activa comportamiento Wander
        SwitchToAmbient,   // Cambia a un NPCAmbientConfig específico
        Disable            // Se desactiva el GameObject
    }
    
    /// <summary>
    /// Modo de capa para el NPC durante la narrativa interactiva
    /// </summary>
    public enum LayerMode
    {
        Interactable,      // Capa "Interactable" - permite interacción con el NPC
        Enemy,             // Capa "Enemy" - necesaria para combate
        Default,           // Capa "Default" - sin función específica
        Custom             // Usar la capa actual del NPC sin cambiar
    }
}

