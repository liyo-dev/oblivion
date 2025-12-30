using System;
using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Narrativa condicional: Define una cadena narrativa que se ejecuta solo si se cumple una condición.
    /// Cada narrativa tiene su propia configuración de ejecución (singleUse, autoStart, postState).
    /// </summary>
    [Serializable]
    public class ConditionalNarrative
    {
        [Header("Identificación")]
        [Tooltip("Nombre descriptivo para identificar esta narrativa en el inspector")]
        public string description = "";
        
        [Tooltip("Prioridad de evaluación (mayor = se evalúa primero)")]
        public int priority = 0;
        
        [Header("Condición")]
        [Tooltip("Condición que debe cumplirse para ejecutar esta narrativa")]
        public NarrativeCondition condition = new NarrativeCondition();
        
        [Header("Narrativa")]
        [Tooltip("Cadena de acciones que se ejecutan si la condición se cumple")]
        public NarrativeChainEntry[] narrativeChain = Array.Empty<NarrativeChainEntry>();
        
        [Header("Comportamiento de Ejecución")]
        [Tooltip("¿Esta narrativa solo se puede ejecutar una vez? Si es true, después de completarse no volverá a ejecutarse.")]
        public bool singleUse = true;
        
        [Tooltip("¿Iniciar automáticamente al detectar al jugador? Si es false, el jugador debe interactuar manualmente.")]
        public bool autoStartOnDetection = false;
        
        [Header("Estado Post-Narrativa")]
        [Tooltip("¿Qué hace el NPC después de completar ESTA narrativa? Solo aplica si es singleUse=true.")]
        public PostNarrativeState postNarrativeState = PostNarrativeState.None;
        
        [Tooltip("Config ambient si postNarrativeState = SwitchToAmbient")]
        public NPCAmbientConfig postNarrativeAmbientConfig;
        
        [Header("Icono Persistente")]
        [Tooltip("¿Mostrar icono persistente sobre la cabeza cuando esta narrativa esté disponible?")]
        public bool showPersistentIcon = false;
        
        [Tooltip("Prefab del icono persistente (ej: GameObject con Canvas). Si es null, usa el del Config general.")]
        public GameObject persistentIconPrefab;
        
        [Header("Evento al Grafo Narrativo")]
        [Tooltip("¿Enviar evento al grafo narrativo al completar esta narrativa?")]
        public bool sendNarrativeEvent = false;
        
        [Tooltip("Clave del evento que se enviará (ej: 'NPC_ItemEntregado')")]
        public string narrativeEventKey = "";
        
        [Header("Debug")]
        [Tooltip("Mostrar logs de debug para esta narrativa")]
        public bool debugMode = false;
        
        // Estado runtime
        [NonSerialized]
        private bool _hasBeenExecuted = false;
        
        /// <summary>
        /// Verifica si esta narrativa se puede ejecutar
        /// </summary>
        public bool CanExecute()
        {
            // Si ya se ejecutó y es single use, no se puede ejecutar de nuevo
            if (singleUse && _hasBeenExecuted)
            {
                return false;
            }
            
            // Evaluar la condición
            bool conditionMet = condition.Evaluate();
            
            return conditionMet;
        }
        
        /// <summary>
        /// Marca esta narrativa como ejecutada
        /// </summary>
        public void MarkAsExecuted()
        {
            _hasBeenExecuted = true;
            
            if (debugMode)
                Debug.Log($"[ConditionalNarrative:{description}] Marcada como ejecutada");
        }
        
        /// <summary>
        /// Resetea el estado de ejecución (útil para testing)
        /// </summary>
        public void ResetExecutionState()
        {
            _hasBeenExecuted = false;
            
            if (debugMode)
                Debug.Log($"[ConditionalNarrative:{description}] Estado reseteado");
        }
        
        /// <summary>
        /// Verifica si ya fue ejecutada
        /// </summary>
        public bool HasBeenExecuted => _hasBeenExecuted;
    }
}

