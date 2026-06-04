using UnityEngine;
using System.Linq;

// Updated: 2025-12-23 - Simplified to conditional-only mode
namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración narrativa condicional para NPCs.
    /// Define QUÉ diálogos/narrativas tiene el NPC y bajo qué condiciones se activan.
    /// Para narrativa simple sin condiciones, añade una con condición 'None'.
    /// 
    /// NOTA: Los campos de identidad (persistenceId, dialogueCharacterId), comportamiento
    /// (rotación, layers, detección) se gestionan ahora en NPCBehaviourManagerV2.
    /// Los campos legacy se mantienen [HideInInspector] para deserialización de .asset existentes.
    /// Ejecutar Tools → NPC → Migrar NarrativeConfig para copiarlos al MonoBehaviour.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_InteractiveNarrative_Config", menuName = "NPC/Módulos/Interactive Narrative Config", order = 5)]
    public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
    {
        [Header("Narrativas Condicionales")]
        [Tooltip("Lista de narrativas con condiciones. Se evalúan en orden de prioridad. Para narrativa simple sin condiciones, añade una con condición 'None'.")]
        public ConditionalNarrative[] conditionalNarratives = System.Array.Empty<ConditionalNarrative>();
        
        // Cache para optimización: array pre-ordenado y pre-filtrado
        [System.NonSerialized]
        private ConditionalNarrative[] _sortedNarrativesCache;
        [System.NonSerialized]
        private bool _isCacheValid;

        [Header("Persistencia Narrativa")]
        [Tooltip("¿Guardar el estado de completado de la narrativa en el preset?")]
        public bool persistState = true;

        [Header("Debug")]
        [Tooltip("⚠️ Solo para DEBUG. Habilitar logs detallados de evaluación de narrativas. DESACTIVAR en producción por rendimiento.")]
        public bool enableDetailedLogs;

        // ════════════════════════════════════════════════════════════════
        // CAMPOS LEGACY — migrados a NPCBehaviourManagerV2.
        // Se mantienen [HideInInspector] para que los .asset existentes
        // sigan deserializando. El Editor migration script los copia
        // al MonoBehaviour y luego se pueden eliminar.
        // ════════════════════════════════════════════════════════════════
        [HideInInspector] public string persistenceId;
        [HideInInspector] public string dialogueCharacterId;
        [HideInInspector] public bool rotateToPlayerOnInteract = true;
        [HideInInspector] public float rotationDuration = 0.3f;
        [HideInInspector] public LayerMode initialLayer = LayerMode.Interactable;
        [HideInInspector] public bool switchToEnemyLayerOnCombat = true;
        [HideInInspector] public float detectionRange = 10f;
        [HideInInspector] public GameObject alertIconPrefab;
        [HideInInspector] public float alertIconDuration = 1f;
        [HideInInspector] public float alertIconHeight = 2.5f;
        [HideInInspector] public bool walkTowardsPlayerOnAlert = true;
        [HideInInspector] public float stopDistanceFromPlayer = 2f;


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
                
                // Validar postNarrativeState de cada narrativa
                if (condNarrative.postNarrativeState == PostNarrativeState.SwitchToAmbient && condNarrative.postNarrativeAmbientConfig == null)
                {
                    errorMessage = $"Conditional narrative {i} ('{condNarrative.description}'): PostNarrativeState = SwitchToAmbient requiere postNarrativeAmbientConfig";
                    return false;
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
                    // Si moveToRandomPoint está activo, no necesita destino fijo
                    if (!entry.moveToRandomPoint && string.IsNullOrEmpty(entry.targetAnchorName) && entry.targetTransform == null)
                    {
                        errorMessage = $"Entry {index} tipo Move requiere targetAnchorName, targetTransform, o marcar 'moveToRandomPoint'";
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
            if (conditionalNarratives == null || conditionalNarratives.Length == 0)
            {
                if (enableDetailedLogs)
                    Debug.Log($"[NPCInteractiveNarrativeConfig] ℹ️ conditionalNarratives está vacío");
                return null;
            }
            
            // Usar caché pre-ordenado para evitar LINQ en runtime
            if (!_isCacheValid)
            {
                RebuildNarrativeCache();
            }
            
            if (enableDetailedLogs)
                Debug.Log($"[NPCInteractiveNarrativeConfig:{name}] 🔍 Evaluando {_sortedNarrativesCache.Length} narrativas condicionales");
            
            // Iterar el array ya ordenado (sin LINQ, sin allocaciones)
            for (int i = 0; i < _sortedNarrativesCache.Length; i++)
            {
                var narrative = _sortedNarrativesCache[i];
                if (narrative.CanExecute())
                {
                    if (enableDetailedLogs)
                        Debug.Log($"[NPCInteractiveNarrativeConfig:{name}] ✅ Narrativa seleccionada: '{narrative.description}' (priority={narrative.priority})");
                    return narrative;
                }
            }
            
            if (enableDetailedLogs)
                Debug.Log($"[NPCInteractiveNarrativeConfig:{name}] ❌ No hay narrativas disponibles para ejecutar");
            return null;
        }
        
        /// <summary>
        /// Reconstruye el caché de narrativas ordenadas (llamar cuando cambian las narrativas)
        /// </summary>
        private void RebuildNarrativeCache()
        {
            if (conditionalNarratives == null || conditionalNarratives.Length == 0)
            {
                _sortedNarrativesCache = System.Array.Empty<ConditionalNarrative>();
            }
            else
            {
                // Filtrar nulls y ordenar por prioridad (mayor a menor)
                _sortedNarrativesCache = conditionalNarratives
                    .Where(n => n != null)
                    .OrderByDescending(n => n.priority)
                    .ToArray();
            }
            
            _isCacheValid = true;
        }
        
        /// <summary>
        /// Invalida el caché (llamar si las narrativas cambian en runtime)
        /// </summary>
        public void InvalidateCache()
        {
            _isCacheValid = false;
        }
        
        private void OnEnable()
        {
            // Construir caché al cargar el ScriptableObject
            _isCacheValid = false;
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
            _isCacheValid = false;
        }
    }

    /// <summary>
    /// Estado del NPC después de completar la narrativa.
    /// Cada ConditionalNarrative puede especificar su propio estado post.
    /// </summary>
    public enum PostNarrativeState
    {
        None            = 0, // No hacer nada especial, el NPC continúa como estaba
        Idle            = 1, // Forzar al NPC a estado Idle
        Wander          = 2, // Activar comportamiento Wander
        SwitchToAmbient = 3, // Cambiar a un NPCAmbientConfig específico
        Disable         = 4  // Desactivar el GameObject del NPC
    }

    /// <summary>
    /// Modo de capa para el NPC durante la narrativa interactiva
    /// </summary>
    public enum LayerMode
    {
        Interactable = 0, // Capa "Interactable" - permite interacción con el NPC
        Enemy        = 1, // Capa "Enemy" - necesaria para combate
        Default      = 2, // Capa "Default" - sin función específica
        Custom       = 3  // Usar la capa actual del NPC sin cambiar
    }
}
