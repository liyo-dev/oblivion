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
        
        // Cache para optimización: array pre-ordenado y pre-filtrado
        [System.NonSerialized]
        private ConditionalNarrative[] _sortedNarrativesCache;
        [System.NonSerialized]
        private bool _isCacheValid;

        [Header("Persistencia")]
        [Tooltip("¿Guardar el estado en el preset de la partida? Si es true, el estado de 'completado' se guardará cuando el jugador haga SAVE.")]
        public bool persistState = true;

        [Tooltip("ID único para persistencia (generado automáticamente basado en el nombre del asset).")]
        public string persistenceId;

        [Header("Comportamiento General")]
        [Tooltip("¿El NPC gira hacia el jugador al interactuar?")]
        public bool rotateToPlayerOnInteract = true;

        [Min(0f)]
        [Tooltip("Duración de la rotación")]
        public float rotationDuration = 0.3f;
        
        [Header("Layer Management")]
        [Tooltip("Capa inicial del NPC. Cambiará automáticamente a 'Enemy' al iniciar combate si switchToEnemyLayerOnCombat está activo.")]
        public LayerMode initialLayer = LayerMode.Interactable;
        
        [Tooltip("¿Cambiar automáticamente a la capa 'Enemy' cuando se inicie un combate (acción StartCombat)?")]
        public bool switchToEnemyLayerOnCombat = true;

        [Header("Detección del Jugador")]
        [Tooltip("Rango de detección del jugador para narrativas con autoStartOnDetection=true")]
        [Min(1f)]
        public float detectionRange = 10f;

        [Tooltip("Prefab del icono de alerta que aparece sobre la cabeza (!) al detectar al jugador")]
        public GameObject alertIconPrefab;

        [Tooltip("Duración del icono de alerta antes de comenzar la narrativa (segundos)")]
        [Min(0.1f)]
        public float alertIconDuration = 1f;
        
        [Tooltip("Altura del icono de alerta sobre el NPC (Y offset). Ajustar según el tamaño del NPC.\n• NPCs pequeños: 1.5-2.0\n• NPCs normales: 2.5\n• NPCs grandes: 3.0-4.0")]
        [Range(0.5f, 5f)]
        public float alertIconHeight = 2.5f;

        [Tooltip("¿El NPC camina hacia el jugador durante la alerta?")]
        public bool walkTowardsPlayerOnAlert = true;

        [Tooltip("Distancia mínima para detenerse al acercarse al jugador")]
        [Min(0.5f)]
        public float stopDistanceFromPlayer = 2f;

        [Header("Speech Bubbles (Bocadillos)")]
        [Tooltip("Prefab por defecto para bocadillos de diálogo (tipo cómic)")]
        public GameObject defaultSpeechBubblePrefab;

        [Tooltip("Prefab por defecto para bocadillos de pensamiento (tipo cómic)")]
        public GameObject defaultThoughtBubblePrefab;

        [Tooltip("Altura del bocadillo sobre el NPC (Y offset)")]
        [Range(0.5f, 5f)]
        public float speechBubbleHeight = 2.8f;
        
        [Header("Debug")]
        [Tooltip("⚠️ Solo para DEBUG. Habilitar logs detallados de evaluación de narrativas. DESACTIVAR en producción por rendimiento.")]
        public bool enableDetailedLogs;


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

            // Validar configuración de detección (rango compartido)
            if (detectionRange <= 0f)
            {
                errorMessage = "detectionRange debe ser mayor a 0";
                return false;
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

                case NarrativeActionType.ShowSpeechBubble:
                    if (string.IsNullOrEmpty(entry.speechBubbleText))
                    {
                        errorMessage = $"Entry {index} tipo ShowSpeechBubble requiere speechBubbleText";
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
            // Invalidar caché cuando se modifica en el Inspector
            _isCacheValid = false;
            
            // Auto-generar ID de persistencia basado en el nombre del asset si está vacío
            if (persistState && string.IsNullOrEmpty(persistenceId))
            {
                // Usar el nombre del asset como base para el ID único
                string baseName = name;
                if (string.IsNullOrEmpty(baseName))
                {
                    baseName = "NPC_Narrative";
                }
                
                // Generar un hash corto para garantizar unicidad
                string shortHash = System.Guid.NewGuid().ToString().Substring(0, 8);
                persistenceId = $"{baseName}_{shortHash}";
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"[NPCInteractiveNarrativeConfig] 🆔 Auto-generado persistenceId: '{persistenceId}' para asset '{name}'");
                #endif
            }
        }
        
        /// <summary>
        /// Regenera el ID de persistencia basado en el nombre actual del asset.
        /// Útil si el asset fue renombrado y quieres sincronizar el ID.
        /// </summary>
        public void RegenerateIdFromName()
        {
            string baseName = name;
            if (string.IsNullOrEmpty(baseName))
            {
                baseName = "NPC_Narrative";
            }
            
            string shortHash = System.Guid.NewGuid().ToString().Substring(0, 8);
            persistenceId = $"{baseName}_{shortHash}";
            
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[NPCInteractiveNarrativeConfig] 🔄 Regenerado persistenceId: '{persistenceId}' para asset '{name}'");
            #endif
        }
    }

    /// <summary>
    /// Estado del NPC después de completar la narrativa.
    /// Cada ConditionalNarrative puede especificar su propio estado post.
    /// </summary>
    public enum PostNarrativeState
    {
        None,              // No hacer nada especial, el NPC continúa como estaba
        Idle,              // Forzar al NPC a estado Idle
        Wander,            // Activar comportamiento Wander
        SwitchToAmbient,   // Cambiar a un NPCAmbientConfig específico
        Disable            // Desactivar el GameObject del NPC
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
