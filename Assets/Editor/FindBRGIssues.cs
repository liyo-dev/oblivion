#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FindBRGIssues
{
    static readonly string[] SuspiciousTypeKeywords = new[]
    {
        "NatureRenderer",
        "GPUInstancer",
        "VegetationSystem",
        "VegetationSystemPro",
        "AltTrees",
        "Indirect",
        "Instanced",
        "BatchRendererGroup",
        "BRG",
        "RendererManager",
        "RenderManager",
        "CityManager"
    };

    [MenuItem("Tools/Diagnostics/Scan scene for BRG / Instancing culprits")]
    public static void Scan()
    {
        int findings = 0;
        Debug.Log("<b>[Scan]</b> Buscando causas típicas de BRG/DOTS instancing en la <b>escena activa</b>…");

        // 1) Cámaras con Occlusion Culling activo
        foreach (var cam in ServiceLocator.GetAll<Camera>(includeInactive: true))
        {
            if (cam.useOcclusionCulling) // <- propiedad correcta
            {
                findings++;
                Debug.LogWarning($"[Scan] Camera con Occlusion Culling activo: <b>{cam.name}</b>", cam);
            }
        }

        // 2) Terrains con Draw Instanced
        foreach (var t in ServiceLocator.GetAll<Terrain>(includeInactive: true))
        {
            if (t.drawInstanced)
            {
                findings++;
                Debug.LogWarning($"[Scan] Terrain con Draw Instanced=ON: <b>{t.name}</b>", t);
            }

            var td = t.terrainData;
            if (td != null)
            {
                try
                {
                    var details = td.detailPrototypes;
                    for (int i = 0; i < details.Length; i++)
                    {
#if UNITY_2021_2_OR_NEWER
                        if (details[i].useInstancing)
                        {
                            findings++;
                            Debug.LogWarning($"[Scan] Detail Prototype instanciado en Terrain <b>{t.name}</b> índice {i} (Use GPU Instancing = ON).", t);
                        }
#endif
                    }
                }
                catch { }
            }
        }

        // 3) LODGroups cuyo último LOD está vacío (culled)
        foreach (var lg in ServiceLocator.GetAll<LODGroup>(includeInactive: true))
        {
            try
            {
                var lods = lg.GetLODs();
                if (lods != null && lods.Length > 0)
                {
                    var last = lods[lods.Length - 1];
                    if (last.renderers == null || last.renderers.Length == 0)
                    {
                        findings++;
                        Debug.LogWarning($"[Scan] LODGroup con último LOD vacío (culled): <b>{lg.name}</b>", lg.gameObject);
                    }
                }
            }
            catch { }
        }

        // 4) Componentes sospechosos por nombre de tipo
        var monos = ServiceLocator.GetAll<MonoBehaviour>(includeInactive: true);
        foreach (var mb in monos)
        {
            if (mb == null) continue;
            var typeName = mb.GetType().FullName ?? mb.GetType().Name;
            if (string.IsNullOrEmpty(typeName)) continue;

            if (SuspiciousTypeKeywords.Any(k => typeName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                findings++;
                Debug.LogWarning($"[Scan] Componente potencial BRG/Instancing: <b>{typeName}</b> en <b>{mb.gameObject.name}</b>", mb.gameObject);
            }
        }

        if (findings == 0)
            Debug.Log("<b>[Scan]</b> No se han encontrado flags/gestores típicos de BRG/instancing en la escena.");
        else
            Debug.Log($"<b>[Scan]</b> He encontrado <b>{findings}</b> elemento(s). Haz clic en cada mensaje para seleccionar el objeto implicado.");
    }

    [MenuItem("Tools/Diagnostics/Quick Fix (disable Instancing/Occlusion in scene)")]
    public static void QuickFix()
    {
        int changes = 0;

        // Cámaras: apagar Occlusion Culling
        foreach (var cam in ServiceLocator.GetAll<Camera>(includeInactive: true))
        {
            if (cam.useOcclusionCulling) // <- propiedad correcta
            {
                cam.useOcclusionCulling = false;
                EditorUtility.SetDirty(cam);
                changes++;
            }
        }

        // Terrains: apagar Draw Instanced y desinstanciar detalles
        foreach (var t in ServiceLocator.GetAll<Terrain>(includeInactive: true))
        {
            if (t.drawInstanced)
            {
                t.drawInstanced = false;
                EditorUtility.SetDirty(t);
                changes++;
            }

            var td = t.terrainData;
            if (td != null)
            {
                try
                {
#if UNITY_2021_2_OR_NEWER
                    var details = td.detailPrototypes;
                    bool touched = false;
                    for (int i = 0; i < details.Length; i++)
                    {
                        if (details[i].useInstancing)
                        {
                            details[i].useInstancing = false;
                            touched = true;
                        }
                    }
                    if (touched)
                    {
                        td.detailPrototypes = details; // aplicar cambios
                        EditorUtility.SetDirty(td);
                        changes++;
                    }
#endif
                }
                catch { }
            }
        }

        // LODGroups: solo aviso (no cambio automático)
        foreach (var lg in ServiceLocator.GetAll<LODGroup>(includeInactive: true))
        {
            var lods = lg.GetLODs();
            if (lods != null && lods.Length > 0)
            {
                var last = lods[lods.Length - 1];
                if (last.renderers == null || last.renderers.Length == 0)
                    Debug.LogWarning($"[QuickFix] Revisa LODGroup <b>{lg.name}</b>: último LOD está vacío (culled).", lg.gameObject);
            }
        }

        Debug.Log($"<b>[QuickFix]</b> Cambios aplicados en la escena: <b>{changes}</b>. Guarda la escena y prueba una build Development.");
    }
}
#endif
