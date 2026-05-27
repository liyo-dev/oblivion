using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Puente entre un NPC y el grafo narrativo.
    /// Al interactuar con el NPC, emite un evento custom al DefaultNarrativeSignals
    /// que los nodos WaitNPCInteractionNode pueden escuchar.
    /// También recibe comandos desde NPCCommandNode para ejecutar acciones.
    /// </summary>
    [DisallowMultipleComponent]
    public class NPCGraphBridge : MonoBehaviour
    {
        [Header("Identificación")]
        [Tooltip("ID narrativo del NPC. Debe coincidir con el npcId en los nodos del grafo.")]
        [SerializeField] string npcId;

        [Header("Configuración")]
        [Tooltip("Si está activo, al interactuar con el NPC se emite el evento de interacción al grafo.")]
        [SerializeField] bool emitInteractionEvent = true;

        NPCBehaviourManagerV2 _npcManager;
        DefaultNarrativeSignals _signals;

        /// <summary>ID narrativo del NPC.</summary>
        public string NpcId => npcId;

        /// <summary>Manager del NPC asociado (puede ser null si no tiene NPCBehaviourManagerV2).</summary>
        public NPCBehaviourManagerV2 NpcManager => _npcManager;

        /// <summary>Transform del NPC para cámara de diálogos.</summary>
        public Transform NpcTransform => transform;

        /// <summary>Clave del evento de interacción que emite este NPC.</summary>
        public string InteractionEventKey => $"NPC_INTERACT_{npcId}";

        void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();

            if (string.IsNullOrWhiteSpace(npcId))
            {
                Debug.LogWarning($"[NPCGraphBridge] NPC '{name}' no tiene npcId asignado. " +
                    "Asígnalo en el Inspector para que los nodos del grafo lo encuentren.");
            }
        }

        void OnEnable()
        {
            NPCGraphBridgeRegistry.Register(this);
        }

        void OnDisable()
        {
            NPCGraphBridgeRegistry.Unregister(this);
        }

        void ResolveSignals()
        {
            if (_signals != null) return;
            _signals = DefaultNarrativeSignals.EnsureInstance();
        }

        /// <summary>
        /// Emite el evento de interacción al grafo narrativo.
        /// Llamado desde Interactable cuando el jugador interactúa con este NPC.
        /// </summary>
        public void EmitInteraction()
        {
            if (!emitInteractionEvent) return;
            if (string.IsNullOrWhiteSpace(npcId))
            {
                Debug.LogWarning($"[NPCGraphBridge] No se puede emitir interacción: npcId vacío en '{name}'.");
                return;
            }

            ResolveSignals();
            if (_signals == null)
            {
                Debug.LogError($"[NPCGraphBridge] No hay DefaultNarrativeSignals disponible.");
                return;
            }

            var key = InteractionEventKey;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCGraphBridge] Emitiendo interacción: '{key}' desde NPC '{npcId}'");
#endif
            _signals.RaiseCustom(key, $"NPCGraphBridge:{npcId}");
        }

        /// <summary>
        /// Emite un evento custom arbitrario al grafo narrativo.
        /// </summary>
        public void EmitCustomEvent(string eventKey)
        {
            if (string.IsNullOrWhiteSpace(eventKey)) return;

            ResolveSignals();
            if (_signals == null) return;

            _signals.RaiseCustom(eventKey, $"NPCGraphBridge:{npcId}");
        }

    }
}
