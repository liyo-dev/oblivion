// Editor/MeasurePrefabFootprints.cs
using System.Linq;
using UnityEditor;
using UnityEngine;

public class MeasurePrefabFootprints : EditorWindow
{
    Object[] prefabs;
    float padding = 1.1f; // 10% margen de seguridad

    [MenuItem("El Sendero/Mundo/Medir Footprints (XZ)")]
    static void Open() => GetWindow<MeasurePrefabFootprints>("Medir Footprints");

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Selecciona prefabs y pulsa Medir.", MessageType.Info);
        if (GUILayout.Button("Usar selección actual")) prefabs = Selection.objects;
        padding = EditorGUILayout.Slider("Padding sugerido", padding, 1f, 1.5f);

        if (GUILayout.Button("Medir"))
        {
            if (prefabs == null || prefabs.Length == 0)
            {
                Debug.LogWarning("No hay prefabs seleccionados.");
                return;
            }

            var sizes = prefabs
                .Select(p => AssetDatabase.GetAssetPath(p))
                .Where(path => path.EndsWith(".prefab"))
                .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path))
                .Select(GetXZFootprint)
                .Where(s => s > 0f)
                .ToList();

            if (sizes.Count == 0) { Debug.LogWarning("No se pudieron medir bounds."); return; }

            float min = sizes.Min();
            float max = sizes.Max();
            float avg = sizes.Average();
            float p50 = Percentile(sizes, 0.5f);
            float p80 = Percentile(sizes, 0.8f);

            Debug.Log($"Medidas XZ (diámetro aprox.) en metros -> Min:{min:F2}  Avg:{avg:F2}  P50:{p50:F2}  P80:{p80:F2}  Max:{max:F2}");
            Debug.Log($"Sugerencias de tileSize (con padding {padding*100f - 100f:F0}%):  " +
                      $"P50→{(p50*padding):F2}   P80→{(p80*padding):F2}   Max→{(max*padding):F2}");
        }
    }

    static float GetXZFootprint(GameObject prefab)
    {
        // instancia temporal “fantasma” en memoria
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.hideFlags = HideFlags.HideAndDontSave;
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Object.DestroyImmediate(go); return 0f; }

        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        Object.DestroyImmediate(go);

        // diámetro en XZ (usamos el mayor de los dos ejes para no solapar)
        float dx = bounds.size.x;
        float dz = bounds.size.z;
        return Mathf.Max(dx, dz);
    }

    static float Percentile(System.Collections.Generic.List<float> list, double p)
    {
        list.Sort();
        double idx = (list.Count - 1) * p;
        int lo = Mathf.FloorToInt((float)idx);
        int hi = Mathf.CeilToInt((float)idx);
        if (lo == hi) return list[lo];
        return Mathf.Lerp(list[lo], list[hi], (float)(idx - lo));
    }
}
