using System;
using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Narrativa condicional: Define una cadena narrativa que se ejecuta solo si se cumple una condición
    /// </summary>
    [Serializable]
    public class ConditionalNarrative
    {
        [Header("Condición")]
        [Tooltip("Condición que debe cumplirse para ejecutar esta narrativa")]
        public NarrativeCondition condition = new NarrativeCondition();
        
        [Header("Narrativa")]
        [Tooltip("Cadena de acciones que se ejecutan si la condición se cumple")]
        public NarrativeChainEntry[] narrativeChain = System.Array.Empty<NarrativeChainEntry>();
        
        [Header("Icono Persistente")]
        [Tooltip("¿Mostrar icono persistente sobre la cabeza cuando esta narrativa esté disponible?")]
        public bool showPersistentIcon = false;
        
        [Tooltip("Prefab del icono persistente (ej: GameObject con Canvas)")]
        public GameObject persistentIconPrefab;
        
        [Tooltip("O usa un sprite simple (alternativa al prefab)")]
        public Sprite persistentIconSprite;
        
        [Header("Evento al Grafo Narrativo")]
        [Tooltip("¿Enviar evento al grafo narrativo al completar esta narrativa?")]
        public bool sendNarrativeEvent = false;
        
        [Tooltip("Clave del evento que se enviará (ej: 'NPC_ItemEntregado')")]
        public string narrativeEventKey = "";
        
        [Header("Configuración")]
        [Tooltip("¿Esta narrativa solo se puede ejecutar una vez?")]
        public bool singleUse = true;
        
        [Tooltip("Prioridad de evaluación (mayor = se evalúa primero)")]
        public int priority = 0;
        
        [Header("Debug")]
        [Tooltip("Nombre descriptivo para identificar esta narrativa en el inspector")]
        public string description = "";
        
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
                if (debugMode)
                    Debug.Log($"[ConditionalNarrative:{description}] Ya ejecutada (singleUse)");
                return false;
            }
            
            // Evaluar la condición
            bool conditionMet = condition.Evaluate();
            
            if (debugMode)
                Debug.Log($"[ConditionalNarrative:{description}] Condición: {condition.GetDescription()} = {conditionMet}");
            
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

