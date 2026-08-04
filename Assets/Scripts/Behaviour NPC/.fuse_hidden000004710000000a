using System.Collections.Generic;
using UnityEngine;

namespace Game.NPC
{
    public class NPCRegistry : MonoBehaviour
    {
        private static NPCRegistry _instance;
        private static bool _applicationQuitting;

        /// <summary>
        /// Verifica si existe una instancia de NPCRegistry sin crear una nueva.
        /// Útil para evitar NullReferenceException en OnDestroy.
        /// </summary>
        public static bool HasInstance => _instance != null && !_applicationQuitting;

        public static NPCRegistry Instance
        {
            get
            {
                // No crear instancias si la aplicación se está cerrando
                if (_applicationQuitting)
                {
                    return null;
                }

                if (_instance == null)
                {
                    // Buscar si ya existe una instancia en la escena
                    _instance = FindFirstObjectByType<NPCRegistry>();

                    if (_instance == null)
                    {
                        // Crear la instancia si no existe
                        var go = new GameObject("[NPCRegistry]");
                        _instance = go.AddComponent<NPCRegistry>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, NPCBehaviourManagerV2> _npcsByID = new Dictionary<string, NPCBehaviourManagerV2>();
        private readonly Dictionary<string, List<NPCBehaviourManagerV2>> _npcsByTag = new Dictionary<string, List<NPCBehaviourManagerV2>>();

        void Awake()
        {
            // Patrón singleton estándar
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        void OnDestroy()
        {
            if (_instance == this)
            {
                // Limpiar referencias
                _npcsByID.Clear();
                _npcsByTag.Clear();
                _instance = null;
            }
        }

#if UNITY_EDITOR
        // Limpiar al salir de Play Mode en el Editor
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _applicationQuitting = false;
        }
#endif
        public void RegisterNPC(string narrativeID, string narrativeTag, NPCBehaviourManagerV2 npc)
        {
            if (npc == null)
            {
                Debug.LogWarning("[NPCRegistry] Intento de registrar NPC null");
                return;
            }
            if (!string.IsNullOrWhiteSpace(narrativeID))
            {
                if (_npcsByID.ContainsKey(narrativeID))
                {
                    Debug.LogWarning($"[NPCRegistry] NPC con ID {narrativeID} ya está registrado. Se sobrescribe.");
                }
                _npcsByID[narrativeID] = npc;
                Debug.Log($"[NPCRegistry] NPC registrado con ID: {narrativeID}");
            }
            if (!string.IsNullOrWhiteSpace(narrativeTag))
            {
                if (!_npcsByTag.ContainsKey(narrativeTag))
                {
                    _npcsByTag[narrativeTag] = new List<NPCBehaviourManagerV2>();
                }
                if (!_npcsByTag[narrativeTag].Contains(npc))
                {
                    _npcsByTag[narrativeTag].Add(npc);
                    Debug.Log($"[NPCRegistry] NPC registrado con Tag: {narrativeTag}");
                }
            }
        }
        public void UnregisterNPC(string narrativeID, string narrativeTag)
        {
            if (!string.IsNullOrWhiteSpace(narrativeID) && _npcsByID.ContainsKey(narrativeID))
            {
                _npcsByID.Remove(narrativeID);
                Debug.Log($"[NPCRegistry] NPC des-registrado con ID: {narrativeID}");
            }
            if (!string.IsNullOrWhiteSpace(narrativeTag) && _npcsByTag.ContainsKey(narrativeTag))
            {
                _npcsByTag[narrativeTag].RemoveAll(n => n == null);
                Debug.Log($"[NPCRegistry] NPC des-registrado con Tag: {narrativeTag}");
            }
        }
        public NPCBehaviourManagerV2 GetNPCByID(string narrativeID)
        {
            if (string.IsNullOrWhiteSpace(narrativeID))
                return null;
            if (_npcsByID.TryGetValue(narrativeID, out var npc))
            {
                return npc;
            }
            Debug.LogWarning($"[NPCRegistry] No se encontró NPC con ID: {narrativeID}");
            return null;
        }
        public List<NPCBehaviourManagerV2> GetNPCsByTag(string narrativeTag)
        {
            if (string.IsNullOrWhiteSpace(narrativeTag))
                return new List<NPCBehaviourManagerV2>();
            if (_npcsByTag.TryGetValue(narrativeTag, out var npcs))
            {
                npcs.RemoveAll(n => n == null);
                return new List<NPCBehaviourManagerV2>(npcs);
            }
            return new List<NPCBehaviourManagerV2>();
        }
        public NPCBehaviourManagerV2 GetFirstNPCByTag(string narrativeTag)
        {
            var npcs = GetNPCsByTag(narrativeTag);
            return npcs.Count > 0 ? npcs[0] : null;
        }
        public void Clear()
        {
            _npcsByID.Clear();
            _npcsByTag.Clear();
            Debug.Log("[NPCRegistry] Registro limpiado");
        }
        public string[] GetAllRegisteredIDs()
        {
            var ids = new string[_npcsByID.Count];
            _npcsByID.Keys.CopyTo(ids, 0);
            return ids;
        }
    }
}
