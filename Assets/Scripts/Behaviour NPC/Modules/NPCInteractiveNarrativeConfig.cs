using UnityEngine;
using System.Linq;

// Updated: 2025-12-23 - Simplified to conditional-only mode
namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de cadena narrativa interactiva para NPCs.
    /// Permite encadenar diálogos, movimientos, animaciones, quests, combat, etc.
    /// Solo usa modo condicional - para narrativa simple sin condiciones, añade una con condición 'None'.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_InteractiveNarrative_Config", menuName = "NPC/Módulos/Interactive Narrative Config", order = 5)]
    public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
    {
        [Header("Narrativas Condicionales")]
        [Tooltip("Lista de narrativas con condiciones. Se evalúan en orden de prioridad. Para narrativa simple sin condiciones, añade una con condición 'None'.")]
        public ConditionalNarrative[] conditionalNarratives = System.Array.Empty<ConditionalNarrative>();

        [Header("Configuración")]
        [Tooltip("¿Esta narrativa solo se puede ejecutar una vez?")]
        public bool singleUse = true;

        [Tooltip("¿Persistir el estado de uso entre sesiones?")]
        public bool persistState = true;

        [Tooltip("ID único para persistencia (generado automáticamente si está vacío)")]
        public string persistenceId;

        [Header("Behavior")]
        [Tooltip("¿El NPC gira hacia el jugador al interactuar?")]
        public bool rotateToPlayerOnInteract = true;

        [Min(0f)]
        [Tooltip("Duración de la rotación")]
        public float rotationDuration = 0.3f;

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
                    if (entry.combatTarget == null)
                    {
                        errorMessage = $"Entry {index} tipo StartCombat requiere combatTarget";
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
}

