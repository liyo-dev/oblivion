using UnityEngine;
using Game.NPC.Modules;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.NPC.Tools
{
    /// <summary>
    /// Script de utilidades para testear y debuggear el NPCInteractiveNarrativeRegistry.
    /// Attach a cualquier GameObject en la escena para acceder a funciones de debug.
    /// </summary>
    public class NPCNarrativeRegistryDebugger : MonoBehaviour
    {
        [Header("Test Controls")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showGUI = true;
        
        [Header("Manual Tests")]
        [SerializeField] private string npcNameToSearch = "";
        [SerializeField] private string persistenceIdToSearch = "";
        
        private void OnGUI()
        {
            if (!showGUI) return;
            
            GUILayout.BeginArea(new Rect(10, 10, 400, 600));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== NPC Narrative Registry Debugger ===", GUI.skin.GetStyle("label"));
            
            var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
            GUILayout.Label($"Total Executors Registrados: {allExecutors.Count}");
            
            GUILayout.Space(10);
            
            if (GUILayout.Button("🔄 Reset All NPCs"))
            {
                NPCNarrativeStateManager.ResetAllNPCs();
            }
            
            if (GUILayout.Button("🗑️ Clear All Saved States"))
            {
                NPCNarrativeStateManager.ClearAllSavedStates();
            }
            
            if (GUILayout.Button("📊 Print Debug Info to Console"))
            {
                Debug.Log(NPCNarrativeStateManager.GetDebugInfo());
                Debug.Log(NPCInteractiveNarrativeRegistry.GetDebugInfo());
            }
            
            GUILayout.Space(10);
            GUILayout.Label("--- Manual Search ---");
            
            npcNameToSearch = GUILayout.TextField(npcNameToSearch);
            if (GUILayout.Button($"Search by Name: '{npcNameToSearch}'"))
            {
                var executor = NPCInteractiveNarrativeRegistry.GetByName(npcNameToSearch);
                if (executor != null)
                {
                    Debug.Log($"✅ Found: {executor.name} at position {executor.transform.position}");
                    // Highlight en escena
                    Debug.DrawLine(executor.transform.position, executor.transform.position + Vector3.up * 5f, Color.green, 5f);
                    
                    #if UNITY_EDITOR
                    Selection.activeGameObject = executor.gameObject;
                    #endif
                }
                else
                {
                    Debug.LogWarning($"❌ Not found: '{npcNameToSearch}'");
                }
            }
            
            GUILayout.Space(5);
            persistenceIdToSearch = GUILayout.TextField(persistenceIdToSearch);
            if (GUILayout.Button($"Search by ID: '{persistenceIdToSearch}'"))
            {
                var executor = NPCInteractiveNarrativeRegistry.GetById(persistenceIdToSearch);
                if (executor != null)
                {
                    Debug.Log($"✅ Found: {executor.name} (ID: {persistenceIdToSearch})");
                    var config = executor.GetConfiguration();
                    if (config != null)
                    {
                        Debug.Log($"   Config: SingleUse={config.singleUse}, Persist={config.persistState}");
                    }
                    
                    #if UNITY_EDITOR
                    Selection.activeGameObject = executor.gameObject;
                    #endif
                }
                else
                {
                    Debug.LogWarning($"❌ Not found with ID: '{persistenceIdToSearch}'");
                }
            }
            
            GUILayout.Space(10);
            GUILayout.Label("--- Registered Executors ---");
            
            foreach (var executor in allExecutors)
            {
                if (executor != null)
                {
                    var config = executor.GetConfiguration();
                    string id = config?.persistenceId ?? "N/A";
                    if (GUILayout.Button($"{executor.name} (ID: {id})"))
                    {
                        Debug.Log($"Selected: {executor.name}");
                        #if UNITY_EDITOR
                        Selection.activeGameObject = executor.gameObject;
                        #endif
                    }
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
        
        [ContextMenu("Print Registry Info")]
        private void PrintRegistryInfo()
        {
            Debug.Log(NPCInteractiveNarrativeRegistry.GetDebugInfo());
        }
        
        [ContextMenu("Print NPC State Info")]
        private void PrintNPCStateInfo()
        {
            Debug.Log(NPCNarrativeStateManager.GetDebugInfo());
        }
        
        [ContextMenu("Reset All NPCs")]
        private void ResetAllNPCs()
        {
            NPCNarrativeStateManager.ResetAllNPCs();
        }
        
        [ContextMenu("Clear Registry")]
        private void ClearRegistry()
        {
            NPCInteractiveNarrativeRegistry.Clear();
            Debug.Log("Registry cleared manually");
        }
        
        private void Update()
        {
            if (showDebugInfo)
            {
                // Dibujar rayos visuales desde cada executor registrado
                var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
                foreach (var executor in allExecutors)
                {
                    if (executor != null)
                    {
                        // Ray hacia arriba para visualizar en escena
                        Debug.DrawRay(executor.transform.position, Vector3.up * 3f, Color.cyan);
                        
                        var config = executor.GetConfiguration();
                        if (config != null && config.autoStartOnPlayerDetection)
                        {
                            // Dibujar el rango de detección
                            Debug.DrawRay(executor.transform.position, Vector3.forward * config.detectionRange, Color.yellow);
                        }
                    }
                }
            }
        }
    }
}

