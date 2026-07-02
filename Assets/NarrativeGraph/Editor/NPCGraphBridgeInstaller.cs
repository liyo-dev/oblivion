using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.NPC;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Herramienta de editor que añade NPCGraphBridge a todos los prefabs de NPCs
    /// que tengan NPCBehaviourManagerV2 pero no tengan ya NPCGraphBridge.
    /// Se ejecuta desde el menú Tools > Narrative Graph > Instalar NPCGraphBridge en Prefabs.
    /// </summary>
    public static class NPCGraphBridgeInstaller
    {
        // Mapeo de nombre de prefab → npcId para el grafo narrativo
        static readonly Dictionary<string, string> NpcIdMap = new()
        {
            { "Oliver",  "OLIVER" },
            { "Erika",   "ERIKA" },
            { "_ESTELA", "ESTELA" },
            { "Guard",   "GUARD" },
            { "WoodsGuard", "GUARD" },
            { "King",    "KING" },
            { "_LIAM",   "LIAM" },
            { "Lety",    "LETY" },
            { "Vicky",   "VICKY" },
            { "Victoria","VICTORIA" },
            { "Eldran",  "ELDRAN" },
            { "Nora",    "NORA" },
            { "Leonardo","LEONARDO" },
            { "Manuel",  "MANUEL" },
            { "Patricia","PATRICIA" },
            { "Roberto", "ROBERTO" },
            { "Rudolfo", "RUDOLFO" },
            { "Sara",    "SARA" },
        };

        [MenuItem("Tools/Narrative Graph/Instalar NPCGraphBridge en Prefabs")]
        public static void InstallBridges()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_NPCs", "Assets/Prefabs" });
            int installed = 0;
            int skipped = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Solo procesar prefabs con NPCBehaviourManagerV2
                var npcManager = prefab.GetComponent<NPCBehaviourManagerV2>();
                if (npcManager == null) continue;

                // Saltar si ya tiene NPCGraphBridge
                if (prefab.GetComponent<NPCGraphBridge>() != null)
                {
                    skipped++;
                    continue;
                }

                // Determinar el npcId
                string npcId = DetermineNpcId(prefab.name);
                if (string.IsNullOrEmpty(npcId))
                {
                    Debug.LogWarning($"[NPCGraphBridgeInstaller] No hay npcId mapeado para '{prefab.name}' → saltando");
                    continue;
                }

                // Abrir prefab para editar
                var prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                try
                {
                    var bridge = prefabRoot.AddComponent<NPCGraphBridge>();
                    // Asignar npcId vía SerializedObject
                    var so = new SerializedObject(bridge);
                    var npcIdProp = so.FindProperty("npcId");
                    if (npcIdProp != null)
                    {
                        npcIdProp.stringValue = npcId;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    installed++;
                    Debug.Log($"[NPCGraphBridgeInstaller] ✓ Añadido NPCGraphBridge a '{prefab.name}' con npcId='{npcId}'");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            EditorUtility.DisplayDialog("NPCGraphBridge Installer",
                $"Instalación completada:\n\n" +
                $"• {installed} prefabs actualizados\n" +
                $"• {skipped} prefabs ya tenían NPCGraphBridge",
                "OK");

            Debug.Log($"[NPCGraphBridgeInstaller] Completado: {installed} instalados, {skipped} ya existentes.");
        }

        static string DetermineNpcId(string prefabName)
        {
            // Buscar en el mapa
            if (NpcIdMap.TryGetValue(prefabName, out string id))
                return id;

            // Fallback: usar el nombre en mayúsculas
            return prefabName.ToUpperInvariant();
        }
    }
}
