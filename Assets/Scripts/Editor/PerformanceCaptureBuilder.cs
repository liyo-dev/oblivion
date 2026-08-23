using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Añade (o repara) el capturador de rendimiento (ver PerformanceCapture.cs) en MainMenu.unity.
///
/// Al vivir en MainMenu (la escena de arranque) y usar DontDestroyOnLoad, el mismo GameObject
/// sobrevive a todos los cambios de escena durante la partida, así que basta con colocarlo una vez.
///
/// Uso en juego: pulsar F9 en cualquier momento empieza a grabar rendimiento; F9 otra vez lo para
/// y vuelca un .json con fps, ms/frame, CPU/GPU (si la plataforma lo soporta) y memoria/GC. Se
/// guarda en ProfilerCaptures/ (repo) al jugar desde el Editor, o en la carpeta de datos
/// persistente del juego en una build — la ruta exacta se muestra en pantalla al guardar.
///
/// Reparador, no solo creador: si el GameObject ya existe, no lo duplica.
///
/// Uso: menú "El Sendero → Diagnóstico → Añadir Captura de Rendimiento al Main Menu".
/// </summary>
public static class PerformanceCaptureBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string GoName = "PerformanceCaptureController";

    [MenuItem("El Sendero/Diagnóstico/Añadir Captura de Rendimiento al Main Menu")]
    public static void AddPerformanceCapture()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[PerformanceCaptureBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[PerformanceCaptureBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[PerformanceCaptureBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        bool created = SetUp();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PerformanceCaptureBuilder] ✅ '{GoName}' {(created ? "creado" : "ya existía, sin cambios")} en {ScenePath}. " +
                  "En Play, pulsa F9 para empezar/parar una grabación de rendimiento.");
    }

    static bool SetUp()
    {
        var existing = FindByNameIncludingInactive(GoName);
        if (existing != null)
        {
            if (existing.GetComponent<PerformanceCapture>() == null)
                existing.AddComponent<PerformanceCapture>();
            Debug.Log($"[PerformanceCaptureBuilder] '{GoName}' ya existe — no se duplica.");
            return false;
        }

        new GameObject(GoName, typeof(PerformanceCapture));
        return true;
    }

    static GameObject FindByNameIncludingInactive(string name)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
            if (t.name == name)
                return t.gameObject;
        return null;
    }
}
