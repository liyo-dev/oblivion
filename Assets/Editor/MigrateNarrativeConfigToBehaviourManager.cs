using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace Game.NPC.EditorTools
{
    /// <summary>
    /// Migra los campos legacy de NPCInteractiveNarrativeConfig (ScriptableObject)
    /// a NPCBehaviourManagerV2 (MonoBehaviour) en todas las escenas y prefabs.
    /// 
    /// Ejecutar desde: Tools → NPC → Migrar NarrativeConfig → BehaviourManager
    /// </summary>
    public static class MigrateNarrativeConfigToBehaviourManager
    {
        [MenuItem("Tools/NPC/Migrar NarrativeConfig → BehaviourManager")]
        public static void RunMigration()
        {
            if (!EditorUtility.DisplayDialog(
                "Migración NarrativeConfig → BehaviourManager",
                "Este script copiará los campos legacy (persistenceId, dialogueCharacterId, rotación, layers, detección) " +
                "desde cada NPCInteractiveNarrativeConfig SO hacia el NPCBehaviourManagerV2 correspondiente.\n\n" +
                "Solo copia si el campo destino está vacío/default.\n\n" +
                "¿Continuar?",
                "Migrar", "Cancelar"))
            {
                return;
            }

            int totalMigrated = 0;
            int totalSkipped = 0;
            var log = new List<string>();

            // 1. Migrar en todas las escenas del build
            string currentScene = EditorSceneManager.GetActiveScene().path;
            var scenePaths = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                    scenePaths.Add(scene.path);
            }

            // Incluir escena actual si no está en build settings
            if (!string.IsNullOrEmpty(currentScene) && !scenePaths.Contains(currentScene))
                scenePaths.Insert(0, currentScene);

            foreach (var scenePath in scenePaths)
            {
                EditorUtility.DisplayProgressBar("Migrando...", $"Escena: {scenePath}", 
                    (float)scenePaths.IndexOf(scenePath) / scenePaths.Count);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var managers = Object.FindObjectsByType<NPCBehaviourManagerV2>(FindObjectsSortMode.None);

                foreach (var mgr in managers)
                {
                    int result = MigrateManager(mgr, log);
                    if (result > 0) totalMigrated += result;
                    else totalSkipped++;
                }

                if (totalMigrated > 0)
                    EditorSceneManager.SaveScene(scene);
            }

            // 2. Migrar en prefabs
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                EditorUtility.DisplayProgressBar("Migrando prefabs...", path,
                    (float)i / prefabGuids.Length);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var managers = prefab.GetComponentsInChildren<NPCBehaviourManagerV2>(true);
                if (managers.Length == 0) continue;

                bool prefabModified = false;
                foreach (var mgr in managers)
                {
                    int result = MigrateManager(mgr, log);
                    if (result > 0)
                    {
                        totalMigrated += result;
                        prefabModified = true;
                    }
                    else totalSkipped++;
                }

                if (prefabModified)
                {
                    EditorUtility.SetDirty(prefab);
                    PrefabUtility.SavePrefabAsset(prefab);
                }
            }

            EditorUtility.ClearProgressBar();

            // Restaurar escena original
            if (!string.IsNullOrEmpty(currentScene))
                EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);

            string summary = $"Migración completada:\n" +
                $"  • {totalMigrated} campo(s) migrados\n" +
                $"  • {totalSkipped} NPC(s) sin cambios (ya migrados o sin config)\n\n";
            
            if (log.Count > 0)
                summary += "Detalle:\n" + string.Join("\n", log);
            else
                summary += "No se encontraron campos pendientes de migrar.";

            Debug.Log($"[MigrateNarrativeConfig] {summary}");
            EditorUtility.DisplayDialog("Migración completada", summary, "OK");
        }

        /// <summary>
        /// Migra campos de un SO a un manager. Devuelve número de campos copiados.
        /// </summary>
        private static int MigrateManager(NPCBehaviourManagerV2 mgr, List<string> log)
        {
            if (mgr == null) return 0;

            var config = mgr.Configuration?.interactiveNarrativeConfig;
            if (config == null) return 0;

            var so = new SerializedObject(mgr);
            int fieldsCopied = 0;

            // persistenceId
            if (string.IsNullOrEmpty(GetString(so, "persistenceId")) && !string.IsNullOrEmpty(config.persistenceId))
            {
                SetString(so, "persistenceId", config.persistenceId);
                fieldsCopied++;
            }

            // dialogueCharacterId
            if (string.IsNullOrEmpty(GetString(so, "dialogueCharacterId")) && !string.IsNullOrEmpty(config.dialogueCharacterId))
            {
                SetString(so, "dialogueCharacterId", config.dialogueCharacterId);
                fieldsCopied++;
            }

            // rotateToPlayerOnInteract (solo migrar si el SO lo tiene en false, ya que true es el default)
            var rotateProp = so.FindProperty("rotateToPlayerOnInteract");
            if (rotateProp != null && !config.rotateToPlayerOnInteract && rotateProp.boolValue)
            {
                rotateProp.boolValue = false;
                fieldsCopied++;
            }

            // rotationDuration (solo si difiere del default 0.3)
            var rotDurProp = so.FindProperty("rotationDuration");
            if (rotDurProp != null && Mathf.Abs(config.rotationDuration - 0.3f) > 0.001f 
                && Mathf.Abs(rotDurProp.floatValue - 0.3f) < 0.001f)
            {
                rotDurProp.floatValue = config.rotationDuration;
                fieldsCopied++;
            }

            // initialLayer
            var layerProp = so.FindProperty("initialLayer");
            if (layerProp != null && config.initialLayer != Modules.LayerMode.Interactable 
                && layerProp.enumValueIndex == (int)Modules.LayerMode.Interactable)
            {
                layerProp.enumValueIndex = (int)config.initialLayer;
                fieldsCopied++;
            }

            // switchToEnemyLayerOnCombat
            var switchProp = so.FindProperty("switchToEnemyLayerOnCombat");
            if (switchProp != null && !config.switchToEnemyLayerOnCombat && switchProp.boolValue)
            {
                switchProp.boolValue = false;
                fieldsCopied++;
            }

            // detectionRange → narrativeDetectionRange
            var detRangeProp = so.FindProperty("narrativeDetectionRange");
            if (detRangeProp != null && Mathf.Abs(config.detectionRange - 10f) > 0.001f 
                && Mathf.Abs(detRangeProp.floatValue - 10f) < 0.001f)
            {
                detRangeProp.floatValue = config.detectionRange;
                fieldsCopied++;
            }

            // walkTowardsPlayerOnAlert
            var walkProp = so.FindProperty("walkTowardsPlayerOnAlert");
            if (walkProp != null && !config.walkTowardsPlayerOnAlert && walkProp.boolValue)
            {
                walkProp.boolValue = false;
                fieldsCopied++;
            }

            // stopDistanceFromPlayer
            var stopProp = so.FindProperty("stopDistanceFromPlayer");
            if (stopProp != null && Mathf.Abs(config.stopDistanceFromPlayer - 2f) > 0.001f 
                && Mathf.Abs(stopProp.floatValue - 2f) < 0.001f)
            {
                stopProp.floatValue = config.stopDistanceFromPlayer;
                fieldsCopied++;
            }

            if (fieldsCopied > 0)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mgr);
                log.Add($"  ✔ '{mgr.name}': {fieldsCopied} campo(s) migrados");
            }

            return fieldsCopied;
        }

        private static string GetString(SerializedObject so, string propName)
        {
            var prop = so.FindProperty(propName);
            return prop?.stringValue;
        }

        private static void SetString(SerializedObject so, string propName, string value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null)
                prop.stringValue = value;
        }
    }
}
