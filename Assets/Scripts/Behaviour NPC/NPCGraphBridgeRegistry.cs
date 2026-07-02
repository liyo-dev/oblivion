using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Registro estático de NPCGraphBridge para lookup rápido por npcId.
    /// Los nodos del grafo narrativo usan este registro para encontrar NPCs.
    /// </summary>
    public static class NPCGraphBridgeRegistry
    {
        static readonly Dictionary<string, NPCGraphBridge> _bridges = new();

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _bridges.Clear();
        }
#endif

        public static void Register(NPCGraphBridge bridge)
        {
            if (bridge == null || string.IsNullOrWhiteSpace(bridge.NpcId)) return;

            if (_bridges.ContainsKey(bridge.NpcId))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[NPCGraphBridgeRegistry] NPC '{bridge.NpcId}' ya registrado. Se sobrescribe.");
#endif
            }
            _bridges[bridge.NpcId] = bridge;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCGraphBridgeRegistry] Registrado: '{bridge.NpcId}'");
#endif
        }

        public static void Unregister(NPCGraphBridge bridge)
        {
            if (bridge == null || string.IsNullOrWhiteSpace(bridge.NpcId)) return;

            if (_bridges.TryGetValue(bridge.NpcId, out var existing) && existing == bridge)
            {
                _bridges.Remove(bridge.NpcId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[NPCGraphBridgeRegistry] Des-registrado: '{bridge.NpcId}'");
#endif
            }
        }

        /// <summary>Busca un NPCGraphBridge por su npcId.</summary>
        public static NPCGraphBridge Get(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)) return null;
            _bridges.TryGetValue(npcId, out var bridge);
            return bridge;
        }

        /// <summary>Comprueba si existe un NPCGraphBridge con el npcId dado.</summary>
        public static bool Has(string npcId)
        {
            return !string.IsNullOrWhiteSpace(npcId) && _bridges.ContainsKey(npcId);
        }
    }
}
