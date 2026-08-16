using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ElSendero.Editor.Rendimiento
{
    /// <summary>
    /// Genera un ShaderVariantCollection a partir de TODOS los materiales del proyecto y lo
    /// registra en Graphics Settings > Preloaded Shaders (auditoría 15 ago 2026).
    ///
    /// Por qué existe: el Profiler mostró coste real en Shader.CreateGPUProgram durante el juego
    /// — eso es Unity compilando una variante de shader la PRIMERA vez que se usa, en mitad de
    /// una escena, en vez de tenerla ya lista. Confirmado en ProjectSettings/GraphicsSettings.asset:
    /// m_PreloadedShaders estaba vacío. Con Quibli metiendo shaders nuevos (Cloud3D, StylizedLit...)
    /// que antes no existían, cada uno de esos tirones/hitches es exactamente esto.
    ///
    /// Qué hace un clic en el menú de abajo:
    ///  1. Recorre todos los Material del proyecto (AssetDatabase).
    ///  2. Para cada uno, apunta su shader + las keywords que ese material tiene activas.
    ///  3. Guarda todo eso en un único ShaderVariantCollection en Assets/Settings/.
    ///  4. Lo registra en Graphics Settings > Preloaded Shaders — Unity las compila todas de
    ///     golpe al arrancar (en la pantalla de carga), no una a una mientras juegas.
    ///
    /// Limitación honesta, para que quede documentada: esto cubre las variantes que existen
    /// "horneadas" en algún Material real del proyecto — que es la inmensa mayoría de los casos.
    /// No captura una keyword que SOLO se active por código en tiempo de ejecución y nunca esté
    /// guardada en ningún .mat. Para ese caso residual, la vía oficial de Unity es jugar con
    /// "Track" activado en Graphics Settings y pulsar "Save to asset" — pero no hace falta para
    /// cubrir el caso común, que es este.
    ///
    /// Se puede volver a ejecutar cuando se añadan materiales/shaders nuevos (Quibli u otros):
    /// vacía y reconstruye el collection existente en vez de duplicar.
    /// </summary>
    public static class ShaderVariantWarmupBuilder
    {
        private const string OutputDir = "Assets/Settings";
        private const string OutputPath = OutputDir + "/ElSendero.shadervariants";

        [MenuItem("El Sendero/Rendimiento/Generar Shader Variant Collection (desde Materiales)")]
        public static void Generate()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(OutputPath);
            if (collection == null)
            {
                collection = new ShaderVariantCollection();
                if (!AssetDatabase.IsValidFolder(OutputDir))
                {
                    Directory.CreateDirectory(OutputDir);
                    AssetDatabase.Refresh();
                }
                AssetDatabase.CreateAsset(collection, OutputPath);
            }
            else
            {
                collection.Clear();
            }

            var materialGuids = AssetDatabase.FindAssets("t:Material");
            int added = 0, materialsScanned = 0, sinShader = 0;
            var seen = new HashSet<string>();

            foreach (var guid in materialGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null)
                {
                    sinShader++;
                    continue;
                }
                materialsScanned++;

                var shader = mat.shader;
                var keywords = mat.shaderKeywords; // keywords locales habilitados en ESTE material

                // Los pases propios de un Scriptable Render Pipeline (URP/HDRP) se registran bajo
                // PassType.ScriptableRenderPipeline — es el mismo valor que usa la propia ventana
                // de Graphics Settings de Unity al grabar variantes de shaders URP.
                var key = shader.name + "|" + string.Join(",", keywords);
                if (seen.Contains(key)) continue;
                seen.Add(key);

                var variant = new ShaderVariantCollection.ShaderVariant
                {
                    shader = shader,
                    passType = PassType.ScriptableRenderPipeline,
                    keywords = keywords
                };
                collection.Add(variant);
                added++;
            }

            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            bool registrado = RegisterInGraphicsSettings(collection);

            Debug.Log(
                $"[ShaderVariantWarmupBuilder] Listo — {materialsScanned} materiales escaneados " +
                $"({sinShader} sin shader, omitidos), {added} variantes únicas guardadas en '{OutputPath}'. " +
                $"Registrado en Graphics Settings > Preloaded Shaders: {(registrado ? "sí" : "NO — revisa el Warning de arriba")}."
            );

            EditorUtility.DisplayDialog(
                "Shader Variant Collection generado",
                $"{materialsScanned} materiales escaneados.\n{added} variantes guardadas en:\n{OutputPath}\n\n" +
                (registrado
                    ? "Registrado en Graphics Settings > Preloaded Shaders — se precompilarán al arrancar el juego."
                    : "No se pudo registrar automáticamente — mira la consola y añádelo a mano en Edit > Project Settings > Graphics > Preloaded Shaders."),
                "OK");
        }

        /// <summary>Añade el collection a GraphicsSettings.m_PreloadedShaders si no estaba ya.</summary>
        private static bool RegisterInGraphicsSettings(ShaderVariantCollection collection)
        {
            var graphicsSettingsObj = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettingsObj == null)
            {
                Debug.LogWarning("[ShaderVariantWarmupBuilder] No se pudo abrir GraphicsSettings.asset por código.");
                return false;
            }

            var so = new SerializedObject(graphicsSettingsObj);
            var prop = so.FindProperty("m_PreloadedShaders");
            if (prop == null)
            {
                Debug.LogWarning("[ShaderVariantWarmupBuilder] GraphicsSettings.asset no expone 'm_PreloadedShaders' en esta versión de Unity.");
                return false;
            }

            for (int i = 0; i < prop.arraySize; i++)
            {
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == collection)
                    return true; // ya estaba registrado de una ejecución anterior
            }

            prop.InsertArrayElementAtIndex(prop.arraySize);
            prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = collection;
            so.ApplyModifiedProperties();
            return true;
        }
    }
}
