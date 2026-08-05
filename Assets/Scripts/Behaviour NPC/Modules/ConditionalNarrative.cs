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
        
        [Tooltip("¿Ejecutar automáticamente cuando se cumpla la condición? Aplica a:\n- Quests: cuando se complete/inicie una quest\n- Eventos Custom: cuando se emita el evento especificado (ej: EVT_MOUNTAIN)")]
        public bool autoExecuteOnQuestConditionMet = false;

        [Tooltip("¿Congelar al jugador (modo cinemático) en el momento exacto en que el NPC le detecta? " +
                 "Solo aplica con autoStartOnDetection = true. El movimiento se bloquea antes de que el NPC se acerque.")]
        public bool freezePlayerOnDetection = false;

        [Tooltip("¿Bloquear al jugador al terminar la cadena narrativa hasta que la detección dispare? " +
                 "Usar cuando la cadena acaba (ej: TeleportNearPlayer) y la quest/narrativa siguiente necesita que el jugador no se escape antes de que el NPC le detecte.")]
        public bool lockPlayerAfterChain = false;

        [Tooltip("Segundos máximos de bloqueo post-cadena como failsafe. Si la detección no dispara en este tiempo, el jugador se desbloquea automáticamente.")]
        public float lockPlayerMaxDuration = 8f;

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

        [Header("Bucle Ambiental")]
        [Tooltip("¿Repetir esta cadena automáticamente, sin interacción del jugador, mientras la condición se siga cumpliendo? " +
                 "Pensado para comportamientos de espera (ej: NPC mareado que camina en bucle hasta que se complete una quest). " +
                 "IMPORTANTE: una narrativa con este flag activo queda EXCLUIDA de la interacción manual (un clic del jugador " +
                 "nunca la dispara a ella misma; solo se dispara desde el bucle ambiental interno del executor). Pero NO bloquea " +
                 "el Interactable ni la detección por proximidad: en cuanto el jugador interactúa (o se detecta según la " +
                 "narrativa real que corresponda), el bucle se corta al instante -a medio gesto si hace falta- y da paso al " +
                 "diálogo/quest normal. Requiere singleUse = false — si no, la cadena se ejecutaría una sola vez y dejaría de " +
                 "repetirse.")]
        public bool loopWhileConditionMet = false;

        [Tooltip("Segundos de pausa entre cada repetición del bucle (solo aplica si loopWhileConditionMet = true).")]
        [Min(0f)]
        public float loopPauseDuration = 1f;

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
                    Debug.Log($"[ConditionalNarrative:{description}] ❌ NO puede ejecutarse - ya fue ejecutada (singleUse=true, _hasBeenExecuted=true)");
                return false;
            }
            
            // Evaluar la condición
            bool conditionMet = condition.Evaluate();
            
            if (debugMode)
                Debug.Log($"[ConditionalNarrative:{description}] Evaluación - singleUse:{singleUse}, executed:{_hasBeenExecuted}, conditionMet:{conditionMet} → CanExecute:{conditionMet}");
            
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
        /// Resetea el estado de ejecución (útil para testing o reinicio de partida)
        /// </summary>
        public void ResetExecutionState()
        {
            _hasBeenExecuted = false;
            
            // También resetear el estado del evento custom si aplica
            condition?.ResetCustomEventState();
            
            if (debugMode)
                Debug.Log($"[ConditionalNarrative:{description}] Estado reseteado (incluyendo eventos custom)");
        }
        
        /// <summary>
        /// Verifica si ya fue ejecutada
        /// </summary>
        public bool HasBeenExecuted => _hasBeenExecuted;
    }
}

