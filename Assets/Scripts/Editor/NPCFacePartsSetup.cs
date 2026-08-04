#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de configuración de un solo uso: completa las partes de cara (GameObjects
/// Eye0X / Mouth0X bajo el hueso "head") en los NPCs que las tienen incompletas, usando
/// Eldran como referencia (tiene las 24 variantes: Eye01-12, Mouth01-12).
///
/// Contexto: al principio del proyecto se quitaron las partes de cara que un NPC no usaba
/// para aligerar el prefab, sin contar con que ahora hace falta poder cambiar de expresión
/// (NPCEmotionController / EmotionProfile). Esta herramienta clona los GameObjects que faltan
/// (mismo mesh, mismo material universal de cara, misma transform local que en Eldran) bajo
/// el hueso "head" del NPC de destino, dejándolos desactivados (igual que en Eldran, donde solo
/// hay una pareja ojo/boca activa a la vez).
///
/// Detecta automáticamente qué NPCs necesitan el arreglo: recorre todos los prefabs con
/// NPCEmotionController y compara sus hijos bajo "head" contra el set de referencia de Eldran.
/// Como usa la jerarquía ya resuelta del prefab (PrefabUtility.LoadPrefabContents), esto
/// funciona igual tanto si el NPC tiene las partes "aplanadas" en el propio prefab (como Eldran)
/// como si las tiene como prefabs anidados de HeadParts (como Ladron1/Ladron2, que no las
/// necesitan porque ya están completas).
///
/// Menú: El Sendero > NPCs > Setup > Completar partes de cara (Eye/Mouth) desde Eldran
/// </summary>
public static class NPCFacePartsSetup
{
    private const string EldranPath = "Assets/_NPCs/Eldran.prefab";
    private const string HeadBoneName = "head";
    private static readonly string[] PartPrefixes = { "Eye", "Mouth" };

    private struct ReferencePart
    {
        public GameObject source;
        public string name;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [MenuItem("El Sendero/NPCs/Setup/Completar partes de cara (Eye/Mouth) desde Eldran")]
    public static void Run()
    {
        var eldran = AssetDatabase.LoadAssetAtPath<GameObject>(EldranPath);
        if (eldran == null)
        {
            EditorUtility.DisplayDialog("Error", $"No se encontró el prefab de referencia en '{EldranPath}'.", "OK");
            return;
        }

        var eldranHead = FindByNameRecursive(eldran.transform, HeadBoneName);
        if (eldranHead == null)
        {
            EditorUtility.DisplayDialog("Error", $"Eldran no tiene un hueso/objeto llamado '{HeadBoneName}'.", "OK");
            return;
        }

        var referenceParts = CollectFaceParts(eldranHead);
        if (referenceParts.Count == 0)
        {
            EditorUtility.DisplayDialog("Error", "Eldran no tiene partes Eye/Mouth bajo 'head'. Nada que copiar.", "OK");
            return;
        }

        var logLines = new List<string> { $"Referencia: {referenceParts.Count} partes bajo '{HeadBoneName}' en Eldran." };
        var fixedPrefabs = new List<string>();
        var skippedNoHead = new List<string>();

        var guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == EldranPath) continue;

            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (mainAsset == null) continue;
            if (mainAsset.GetComponentInChildren<NPCEmotionController>(true) == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var head = FindByNameRecursive(root.transform, HeadBoneName);
                if (head == null)
                {
                    skippedNoHead.Add(path);
                    continue;
                }

                var existingNames = new HashSet<string>();
                foreach (Transform child in head)
                    existingNames.Add(child.name);

                var missing = referenceParts.Where(p => !existingNames.Contains(p.name)).ToList();
                if (missing.Count == 0)
                {
                    logLines.Add($"ℹ {path}: ya tiene las {referenceParts.Count} partes, no se toca.");
                    continue;
                }

                foreach (var part in missing)
                {
                    var clone = Object.Instantiate(part.source, head);
                    clone.name = part.name;
                    clone.transform.localPosition = part.localPosition;
                    clone.transform.localRotation = part.localRotation;
                    clone.transform.localScale = part.localScale;
                    clone.SetActive(false);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                fixedPrefabs.Add(path);
                logLines.Add($"✅ {path}: añadidas {missing.Count} partes ({string.Join(", ", missing.Select(m => m.name))}).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (var path in skippedNoHead)
            logLines.Add($"⚠ {path}: no tiene un objeto '{HeadBoneName}', revisar a mano.");

        string summary = string.Join("\n", logLines);
        Debug.Log($"[NPCFacePartsSetup] Resultado:\n{summary}");

        EditorUtility.DisplayDialog(
            "Completar partes de cara",
            $"{fixedPrefabs.Count} prefab(s) completados con las partes de cara que faltaban.\n" +
            (skippedNoHead.Count > 0 ? $"{skippedNoHead.Count} prefab(s) sin hueso '{HeadBoneName}' (revisar a mano).\n" : "") +
            "Detalles completos en la Console.",
            "OK");
    }

    private static List<ReferencePart> CollectFaceParts(Transform head)
    {
        var result = new List<ReferencePart>();
        foreach (Transform child in head)
        {
            if (!PartPrefixes.Any(prefix => child.name.StartsWith(prefix)))
                continue;

            result.Add(new ReferencePart
            {
                source = child.gameObject,
                name = child.name,
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });
        }

        return result;
    }

    private static Transform FindByNameRecursive(Transform root, string name)
    {
        if (root.name == name)
            return root;

        foreach (Transform child in root)
        {
            var found = FindByNameRecursive(child, name);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
