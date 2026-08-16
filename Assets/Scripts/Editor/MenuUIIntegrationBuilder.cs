using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Integra en las escenas los dos componentes nuevos del menú (CursorManager y CreditsFlyoutPanel)
/// sin tener que montarlos a mano en el Editor: crea los GameObjects, añade los componentes, asigna
/// las texturas del cursor y configura su Import Settings, y guarda las escenas. Mismo patrón que
/// MainMenuStylingBuilder / ControlsMenuSceneBuilder ya usan en este proyecto (abrir la escena,
/// construir por código, guardar) — nada nuevo que aprender si ya conoces esas herramientas.
///
/// Es idempotente: si el componente correspondiente ya existe en la escena, no lo duplica. Se puede
/// ejecutar tantas veces como haga falta (por ejemplo, tras borrar el GameObject a mano para
/// reconstruirlo desde cero).
///
/// Se ejecuta solo, sin pedir nada: en cuanto el Editor termina de compilar estos scripts,
/// <see cref="AutoIntegrateOnLoad"/> se dispara y llama a <see cref="Integrate"/> automáticamente
/// (si no hay escenas con cambios sin guardar ni Play Mode activo — si los hay, no toca nada y basta
/// con lanzar manualmente el menú de abajo cuando venga bien).
///
/// Uso manual: menú "El Sendero → Controles → Integrar Cursor + Panel de Créditos".
/// </summary>
[InitializeOnLoad]
public static class MenuUIIntegrationBuilder
{
    const string StartScenePath = "Assets/Scenes/Systems/Start.unity";
    const string MainMenuScenePath = "Assets/Scenes/Systems/MainMenu.unity";

    const string DefaultCursorTexturePath = "Assets/Art/UI/Cursor/cursor_default_star.png";
    const string InteractCursorTexturePath = "Assets/Art/UI/Cursor/cursor_interact_hand.png";

    const string CursorManagerScriptPath = "Assets/Scripts/UI/CursorManager.cs";
    const string CreditsFlyoutPanelScriptPath = "Assets/Scripts/UI/CreditsFlyoutPanel.cs";

    const string AutoRanSessionKey = "MenuUIIntegrationBuilder_AutoRan";

    static MenuUIIntegrationBuilder()
    {
        // Disparo automático de una sola vez por sesión de Editor. Se pospone con delayCall para no
        // tocar escenas en mitad del propio evento de recarga de dominio (mala práctica en Unity).
        if (SessionState.GetBool(AutoRanSessionKey, false)) return;
        EditorApplication.delayCall += AutoIntegrateOnLoad;
    }

    static void AutoIntegrateOnLoad()
    {
        if (SessionState.GetBool(AutoRanSessionKey, false)) return;
        SessionState.SetBool(AutoRanSessionKey, true);

        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AnySceneDirty()) return; // no tocar nada si hay trabajo sin guardar — se puede lanzar a mano luego

        if (AlreadyIntegrated()) return;

        Debug.Log("[MenuUIIntegrationBuilder] Integración automática al abrir el proyecto...");
        Integrate();
    }

    [MenuItem("El Sendero/Controles/Integrar Cursor + Panel de Créditos")]
    public static void Integrate()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MenuUIIntegrationBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        if (AnySceneDirty())
        {
            Debug.LogError("[MenuUIIntegrationBuilder] Hay una escena abierta con cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
            return;
        }

        try
        {
            IntegrateCursor();
            IntegrateCreditsPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MenuUIIntegrationBuilder] ✅ Cursor personalizado y panel de créditos integrados y guardados en las escenas.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MenuUIIntegrationBuilder] Error durante la integración: {e}");
        }
    }

    static bool AnySceneDirty()
    {
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            if (EditorSceneManager.GetSceneAt(i).isDirty) return true;
        }
        return false;
    }

    // Comprueba por texto (sin abrir las escenas en el Editor) si el .cs ya está referenciado en
    // ellas, buscando el GUID del script en el .unity — evita el efecto secundario de cambiar la
    // escena abierta del usuario solo para comprobar si hace falta hacer algo.
    static bool AlreadyIntegrated()
    {
        return SceneReferencesScript(StartScenePath, CursorManagerScriptPath)
            && SceneReferencesScript(MainMenuScenePath, CreditsFlyoutPanelScriptPath);
    }

    static bool SceneReferencesScript(string scenePath, string scriptPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(scriptPath);
        if (string.IsNullOrEmpty(guid)) return false;
        if (!System.IO.File.Exists(scenePath)) return false;

        string sceneText = System.IO.File.ReadAllText(scenePath);
        return sceneText.Contains(guid);
    }

    static void IntegrateCursor()
    {
        ConfigureCursorTexture(DefaultCursorTexturePath);
        ConfigureCursorTexture(InteractCursorTexturePath);

        var scene = EditorSceneManager.OpenScene(StartScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MenuUIIntegrationBuilder] No se pudo abrir {StartScenePath}.");
            return;
        }

        if (FindInScene<CursorManager>(scene) != null)
        {
            Debug.Log("[MenuUIIntegrationBuilder] CursorManager ya existe en Start.unity — no se duplica.");
            return;
        }

        var go = new GameObject("CursorManager");
        var cm = go.AddComponent<CursorManager>();

        var starTex = AssetDatabase.LoadAssetAtPath<Texture2D>(DefaultCursorTexturePath);
        var handTex = AssetDatabase.LoadAssetAtPath<Texture2D>(InteractCursorTexturePath);

        if (starTex == null || handTex == null)
        {
            Debug.LogWarning("[MenuUIIntegrationBuilder] No se encontraron las texturas del cursor en " +
                              $"'{DefaultCursorTexturePath}' / '{InteractCursorTexturePath}'. El componente se añade igualmente, pero sin texturas asignadas.");
        }

        var so = new SerializedObject(cm);
        so.FindProperty("defaultCursorTexture").objectReferenceValue = starTex;
        so.FindProperty("interactCursorTexture").objectReferenceValue = handTex;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MenuUIIntegrationBuilder] ✅ CursorManager añadido a Start.unity con las texturas asignadas.");
    }

    static void IntegrateCreditsPanel()
    {
        var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MenuUIIntegrationBuilder] No se pudo abrir {MainMenuScenePath}.");
            return;
        }

        if (FindInScene<CreditsFlyoutPanel>(scene) != null)
        {
            Debug.Log("[MenuUIIntegrationBuilder] CreditsFlyoutPanel ya existe en MainMenu.unity — no se duplica.");
            return;
        }

        var go = new GameObject("CreditsFlyoutPanel");
        go.AddComponent<CreditsFlyoutPanel>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[MenuUIIntegrationBuilder] ✅ CreditsFlyoutPanel añadido a MainMenu.unity (sustituirá el listener del botón CRÉDITOS en cuanto le des a Play).");
    }

    static void ConfigureCursorTexture(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[MenuUIIntegrationBuilder] No se encontró TextureImporter para {path}.");
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Cursor)
        {
            importer.textureType = TextureImporterType.Cursor;
            changed = true;
        }
        if (!importer.isReadable)
        {
            importer.isReadable = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }
}
