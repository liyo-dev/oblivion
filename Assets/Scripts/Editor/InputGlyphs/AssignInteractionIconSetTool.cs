using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.InputGlyphs.EditorTools
{
    /// <summary>
    /// Asigna los assets InteractionHintIconSet compartidos (Assets/_UI/*.asset) al campo
    /// correspondiente de cada sitio del proyecto que muestra un icono de botón según el mando/
    /// teclado activo: Interactable.iconSet, TeleportHintUI.teleportIconSet,
    /// StarAwakeningSequencer.interactIconSet. Solo toca los campos que todavía estén vacíos — si
    /// alguna vez se asigna un set distinto a mano en una instancia concreta, esta herramienta lo
    /// deja intacto.
    ///
    /// FIX (2026-08-11): la primera versión recorría TODOS los prefabs del proyecto con
    /// PrefabUtility.LoadPrefabContents antes de mirar si de verdad contenían alguno de estos
    /// componentes — en un proyecto con miles de prefabs de arte comprado (personajes, props...)
    /// eso es carísimo y parecía "colgarse". Ahora se hace primero un filtro rápido por texto
    /// (buscar el GUID del script en el archivo, sin deserializarlo) y solo se abre de verdad el
    /// prefab/escena si ese filtro encuentra algo — por eso ahora debería tardar segundos, no
    /// minutos. También hay barra de progreso, así que si parece que no avanza, mira ahí antes de
    /// asumir que se ha colgado.
    ///
    /// GUARDA UN COMMIT ANTES DE CORRER ESTO: recorre y guarda TODAS las escenas del proyecto (abre
    /// cada una en modo Single) y los prefabs que hagan falta, así que conviene tener el árbol de
    /// trabajo limpio para poder revisar el diff después con "git status"/"git diff" y deshacer si
    /// algo no cuadra.
    /// </summary>
    public static class AssignInteractionIconSetTool
    {
        struct Target
        {
            public Type ComponentType;
            public string FieldName;
            public string AssetPath;
            public string ScriptGuid;
            public string Label;
        }

        static readonly Target[] Targets =
        {
            new Target
            {
                ComponentType = typeof(Interactable),
                FieldName = "iconSet",
                AssetPath = "Assets/_UI/InteractionHintIconSet.asset",
                ScriptGuid = "b687a38d9c603ef4ea2c981103c66173",
                Label = "Interactable.iconSet",
            },
            new Target
            {
                ComponentType = typeof(TeleportHintUI),
                FieldName = "teleportIconSet",
                AssetPath = "Assets/_UI/TeleportHintIconSet.asset",
                ScriptGuid = "28a0f694c80a490196628203b693cf88",
                Label = "TeleportHintUI.teleportIconSet",
            },
            new Target
            {
                ComponentType = typeof(StarAwakeningSequencer),
                FieldName = "interactIconSet",
                AssetPath = "Assets/_UI/InteractionHintIconSet.asset",
                ScriptGuid = "9972e554871cac546b34f166e14d7d04",
                Label = "StarAwakeningSequencer.interactIconSet",
            },
        };

        [MenuItem("Tools/Input Glyphs/Asignar Icon Sets a todos los consumidores")]
        public static void AssignToAll()
        {
            var resolved = new List<(Target target, InteractionHintIconSet asset)>();
            foreach (var target in Targets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<InteractionHintIconSet>(target.AssetPath);
                if (asset == null)
                {
                    Debug.LogError($"[AssignInteractionIconSetTool] No se encontró el asset en {target.AssetPath} (para {target.Label}). Se omite este campo.");
                    continue;
                }
                resolved.Add((target, asset));
            }

            if (resolved.Count == 0)
            {
                Debug.LogError("[AssignInteractionIconSetTool] No hay ningún asset resuelto — nada que asignar.");
                return;
            }

            var labels = new List<string>();
            foreach (var r in resolved) labels.Add("- " + r.target.Label);

            if (!EditorUtility.DisplayDialog(
                    "Asignar Icon Sets",
                    "Esto va a abrir y guardar las escenas del proyecto que contengan alguno de estos " +
                    "componentes, y los prefabs equivalentes, con el campo de icon set vacío:\n\n" +
                    string.Join("\n", labels) +
                    "\n\nAsegúrate de tener el árbol de trabajo limpio (commit o stash) antes de " +
                    "continuar para poder revisar el diff después.\n\n¿Continuar?",
                    "Sí, continuar", "Cancelar"))
            {
                return;
            }

            int prefabsChanged = AssignInPrefabs(resolved);
            int scenesChanged = AssignInScenes(resolved);

            Debug.Log($"[AssignInteractionIconSetTool] Hecho. Prefabs modificados: {prefabsChanged}. " +
                      $"Escenas modificadas: {scenesChanged}.");
        }

        static int AssignInPrefabs(List<(Target target, InteractionHintIconSet asset)> resolved)
        {
            int changed = 0;
            var guids = AssetDatabase.FindAssets("t:Prefab");

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);

                    if ((i & 127) == 0)
                        EditorUtility.DisplayProgressBar("Asignar Icon Sets — Prefabs",
                            path, (float)i / Mathf.Max(1, guids.Length));

                    if (!MightContainAnyTarget(path, resolved))
                        continue;

                    var root = PrefabUtility.LoadPrefabContents(path);
                    bool dirty = false;

                    try
                    {
                        foreach (var entry in resolved)
                        {
                            var components = root.GetComponentsInChildren(entry.target.ComponentType, true);
                            foreach (var component in components)
                            {
                                if (TryAssignIfEmpty(component, entry.target.FieldName, entry.asset))
                                    dirty = true;
                            }
                        }

                        if (dirty)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            changed++;
                            Debug.Log($"[AssignInteractionIconSetTool] Prefab actualizado: {path}");
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return changed;
        }

        static int AssignInScenes(List<(Target target, InteractionHintIconSet asset)> resolved)
        {
            int changed = 0;
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var originalScenePath = SceneManager.GetActiveScene().path;

            try
            {
                for (int i = 0; i < sceneGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);

                    EditorUtility.DisplayProgressBar("Asignar Icon Sets — Escenas",
                        path, (float)i / Mathf.Max(1, sceneGuids.Length));

                    if (!MightContainAnyTarget(path, resolved))
                        continue;

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool dirty = false;

                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var entry in resolved)
                        {
                            var components = root.GetComponentsInChildren(entry.target.ComponentType, true);
                            foreach (var component in components)
                            {
                                if (TryAssignIfEmpty(component, entry.target.FieldName, entry.asset))
                                    dirty = true;
                            }
                        }
                    }

                    if (dirty)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                        changed++;
                        Debug.Log($"[AssignInteractionIconSetTool] Escena actualizada: {path}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(originalScenePath))
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            return changed;
        }

        /// <summary>
        /// Filtro rápido: lee el archivo como texto plano y busca el GUID del script de cada
        /// componente que nos interesa, SIN deserializar YAML ni instanciar nada. Un prefab/escena
        /// que no mencione ninguno de estos GUIDs no puede tener ese componente, así que se
        /// descarta sin pagar el coste de LoadPrefabContents/OpenScene.
        /// </summary>
        static bool MightContainAnyTarget(string path, List<(Target target, InteractionHintIconSet asset)> resolved)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch
            {
                return true; // si no se puede leer como texto, mejor no descartarlo a ciegas
            }

            foreach (var entry in resolved)
            {
                if (text.Contains(entry.target.ScriptGuid))
                    return true;
            }
            return false;
        }

        static bool TryAssignIfEmpty(Component component, string fieldName, InteractionHintIconSet asset)
        {
            var so = new SerializedObject(component);
            var prop = so.FindProperty(fieldName);
            if (prop == null || prop.objectReferenceValue != null)
                return false;

            prop.objectReferenceValue = asset;
            so.ApplyModifiedProperties();
            return true;
        }
    }
}
