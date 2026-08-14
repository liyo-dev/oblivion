// Editor/ForceProductionBootStateOnBuild.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Antes de CUALQUIER build (Build, Build And Run, o pipeline por script), fuerza el
/// estado de arranque de producción para no publicar builds en modo test por descuido:
///
///   - Escena 'Start': GameObject 'START_BootLoader' → desactivado.
///   - Escena 'Start': GameObject 'START_Dedication' → activado.
///   - GameBootProfile.asset: 'usePresetInsteadOfSave' → false.
///
/// Motivo: en modo testeo se activa BootLoader (carga rápida a una escena de test) y/o
/// usePresetInsteadOfSave (arranca desde un bootPreset en vez del save real). Si eso se
/// cuela en una build, el juego no arranca desde Dedication/MainMenu con el flujo real
/// de guardado. Este hook corrige el estado automáticamente para que nunca haya que
/// acordarse a mano (ver CLAUDE.md § 1 y TDD.md § 10 sobre invariantes de arranque).
///
/// También expone un ítem de menú para aplicar la misma corrección manualmente sin
/// lanzar una build, por si se quiere dejar el proyecto "listo para build" de antemano.
/// </summary>
public class ForceProductionBootStateOnBuild : IPreprocessBuildWithReport
{
    // Ejecutar muy pronto, antes que otros preprocesadores que puedan depender
    // del estado de la escena Start o del GameBootProfile.
    public int callbackOrder => -10000;

    private const string StartScenePath = "Assets/Scenes/Systems/Start.unity";
    private const string BootProfilePath = "Assets/_BootProfile/GameBootProfile.asset";
    private const string BootLoaderObjectName = "START_BootLoader";
    private const string DedicationObjectName = "START_Dedication";

    public void OnPreprocessBuild(BuildReport report)
    {
        ApplyProductionBootState();
    }

    [MenuItem("El Sendero/Build/Aplicar estado de Build (BootLoader OFF / Dedication ON)")]
    public static void ApplyProductionBootStateMenuItem()
    {
        ApplyProductionBootState();
        Debug.Log("[ForceProductionBootStateOnBuild] Corrección manual aplicada.");
    }

    private static void ApplyProductionBootState()
    {
        FixBootProfileAsset();
        FixStartScene();
    }

    private static void FixBootProfileAsset()
    {
        var profile = AssetDatabase.LoadAssetAtPath<GameBootProfile>(BootProfilePath);
        if (profile == null)
        {
            Debug.LogWarning($"[ForceProductionBootStateOnBuild] No se encontró GameBootProfile en '{BootProfilePath}'. No se pudo comprobar 'usePresetInsteadOfSave'.");
            return;
        }

        if (profile.usePresetInsteadOfSave)
        {
            profile.usePresetInsteadOfSave = false;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[ForceProductionBootStateOnBuild] ✅ GameBootProfile.usePresetInsteadOfSave forzado a FALSE para build.");
        }
    }

    private static void FixStartScene()
    {
        var scene = SceneManager.GetSceneByPath(StartScenePath);
        bool openedByUs = false;

        if (!scene.IsValid() || !scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Additive);
            openedByUs = true;
        }

        bool changed = false;
        changed |= SetObjectActiveIfNeeded(scene, BootLoaderObjectName, false);
        changed |= SetObjectActiveIfNeeded(scene, DedicationObjectName, true);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ForceProductionBootStateOnBuild] ✅ Estado de build forzado en 'Start': START_BootLoader OFF / START_Dedication ON.");
        }

        if (openedByUs)
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool SetObjectActiveIfNeeded(Scene scene, string objectName, bool active)
    {
        var target = FindInScene(scene, objectName);
        if (target == null)
        {
            Debug.LogWarning($"[ForceProductionBootStateOnBuild] No se encontró el GameObject '{objectName}' en '{StartScenePath}'.");
            return false;
        }

        if (target.activeSelf == active) return false;

        target.SetActive(active);
        return true;
    }

    // Búsqueda recursiva manual: GameObject.Find/scene.Find ignoran objetos inactivos,
    // y START_BootLoader/START_Dedication están precisamente uno de los dos desactivado.
    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindRecursive(root.transform, objectName);
            if (found != null) return found.gameObject;
        }
        return null;
    }

    private static Transform FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
#endif
