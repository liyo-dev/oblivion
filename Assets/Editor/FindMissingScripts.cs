using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;

/// <summary>
/// Utilidad de diagnóstico: localiza GameObjects con componentes de script perdido
/// ("The referenced script (Unknown) on this Behaviour is missing!").
/// Escanea tanto las escenas actualmente abiertas como todos los prefabs del proyecto.
/// Uso: menú Tools/Debug/Find Missing Scripts.
/// </summary>
public static class FindMissingScripts
{
    [MenuItem("Tools/Debug/Find Missing Scripts (Escenas Abiertas)")]
    public static void FindInOpenScenes()
    {
        var sb = new StringBuilder();
        int total = 0;

        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                total += ScanTransform(root.transform, scene.name, sb);
            }
        }

        Report(sb, total, "escenas abiertas");
    }

    [MenuItem("Tools/Debug/Find Missing Scripts (Todos los Prefabs)")]
    public static void FindInAllPrefabs()
    {
        var sb = new StringBuilder();
        int total = 0;

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            int count = ScanTransform(prefab.transform, path, sb);
            total += count;
        }

        Report(sb, total, "prefabs del proyecto");
    }

    private static int ScanTransform(Transform t, string ownerLabel, StringBuilder sb)
    {
        int count = 0;
        var components = t.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                sb.AppendLine($"[{ownerLabel}] Ruta: {GetHierarchyPath(t)}  (componente #{i} perdido)");
                count++;
            }
        }

        foreach (Transform child in t)
        {
            count += ScanTransform(child, ownerLabel, sb);
        }

        return count;
    }

    private static string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static void Report(StringBuilder sb, int total, string scope)
    {
        if (total == 0)
        {
            Debug.Log($"[FindMissingScripts] No se encontraron scripts perdidos en {scope}.");
        }
        else
        {
            Debug.LogWarning($"[FindMissingScripts] {total} script(s) perdido(s) en {scope}:\n{sb}");
        }
    }
}
