using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// NOTA: este archivo usa siempre "UnityEditor.PlayerSettings" completamente cualificado (nunca
// "PlayerSettings" a secas) a propósito — el proyecto ya tiene su propia clase global
// "PlayerSettings" en Assets/Scripts/Core/PlayerSettings.cs (sin namespace), que si no se
// cualifica gana la resolución sobre UnityEditor.PlayerSettings y da error de compilación
// (CS0117: 'PlayerSettings' does not contain a definition for 'bundleVersion').

/// <summary>
/// Sube automáticamente el número de parche de <c>PlayerSettings.bundleVersion</c>
/// (PlayerSettings → Other Settings → Version, formato "MAYOR.MENOR.PARCHE", ej. "0.1.3" → "0.1.4")
/// cada vez que se genera un build de verdad — así ya no hace falta subirlo a mano antes de cada
/// build para itch.io.
///
/// Se engancha a <see cref="IPreprocessBuildWithReport"/>, que Unity llama justo antes de arrancar
/// <c>BuildPipeline.BuildPlayer</c> (botón "Build" / "Build and Run" del Build Settings, o un build
/// lanzado por línea de comandos/CI). NO se dispara al entrar en Play Mode, al guardar una escena ni
/// al hacer una importación — solo cuando de verdad se está generando un ejecutable.
///
/// Si <c>bundleVersion</c> no tiene el formato "X.Y.Z" esperado (o está vacío), no se toca nada y se
/// avisa por consola como warning — mejor no tocar un valor con forma rara que corromperlo en
/// silencio.
///
/// Para saltarte el autoincremento en un build puntual (por ejemplo una build de pruebas que no vas
/// a subir a ningún sitio), usa el menú "El Sendero → Build → Saltar autoincremento de versión (solo
/// el próximo build)" antes de compilar — se desactiva una sola vez y vuelve a activarse solo después,
/// sin tocar nada en el Inspector.
/// </summary>
public class BuildVersionIncrementer : IPreprocessBuildWithReport
{
    // Que corra pronto, antes que otros pasos de preprocess que pudieran depender ya del número
    // de versión nuevo (por ejemplo, algo que escriba la versión en un archivo de metadata del build).
    public int callbackOrder => -1000;

    const string SkipNextBuildKey = "ElSendero_SkipVersionIncrementOnce";

    // Recuerda, solo para el resto de este mismo build, si el autoincremento se saltó — lo lee
    // PatchNotesBuildGuard (que corre después, callbackOrder -900) para no validar ni archivar las
    // Notas del Parche en un build de pruebas que tampoco sube de versión. Se sobrescribe siempre al
    // principio de cada build, así que no hace falta borrarlo a mano.
    const string LastBuildSkippedKey = "ElSendero_LastBuildWasVersionSkipped";

    /// <summary>True si el build que se está generando ahora mismo saltó el autoincremento de versión.</summary>
    public static bool WasLastBuildVersionSkipped => SessionState.GetBool(LastBuildSkippedKey, false);

    public void OnPreprocessBuild(BuildReport report)
    {
        if (SessionState.GetBool(SkipNextBuildKey, false))
        {
            SessionState.EraseBool(SkipNextBuildKey);
            SessionState.SetBool(LastBuildSkippedKey, true);
            Debug.Log("[BuildVersionIncrementer] Autoincremento saltado para este build (una sola vez). " +
                      $"La versión se queda en {UnityEditor.PlayerSettings.bundleVersion}.");
            return;
        }

        SessionState.SetBool(LastBuildSkippedKey, false);

        string current = UnityEditor.PlayerSettings.bundleVersion;
        string[] parts = current.Split('.');

        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out int major) ||
            !int.TryParse(parts[1], out int minor) ||
            !int.TryParse(parts[2], out int patch))
        {
            Debug.LogWarning($"[BuildVersionIncrementer] PlayerSettings.bundleVersion ('{current}') no " +
                              "tiene el formato MAYOR.MENOR.PARCHE esperado — no se ha tocado. Súbela a " +
                              "mano en Player Settings > Other Settings > Version, o corrige el formato " +
                              "para que el autoincremento pueda hacerse cargo a partir de ahora.");
            return;
        }

        patch++;
        string next = $"{major}.{minor}.{patch}";
        UnityEditor.PlayerSettings.bundleVersion = next;
        AssetDatabase.SaveAssets(); // fuerza a que quede escrito en ProjectSettings.asset ya mismo

        Debug.Log($"[BuildVersionIncrementer] Versión subida automáticamente: {current} → {next}.");
    }

    [MenuItem("El Sendero/Build/Saltar autoincremento de versión (solo el próximo build)")]
    static void SkipNextBuild()
    {
        SessionState.SetBool(SkipNextBuildKey, true);
        Debug.Log("[BuildVersionIncrementer] El próximo build NO subirá la versión automáticamente " +
                  "(solo esta vez — el build siguiente a ese ya vuelve a incrementar normal).");
    }
}
