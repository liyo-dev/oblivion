using System.IO;
using Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// NOTA: igual que BuildVersionIncrementer.cs, este archivo cualifica siempre "UnityEditor.PlayerSettings"
// (nunca "PlayerSettings" a secas) porque el proyecto tiene su propia clase global "PlayerSettings"
// en Assets/Scripts/Core/PlayerSettings.cs que si no se cualifica gana la resolución y rompe la
// compilación (CS0117).

/// <summary>
/// Mantiene las Notas del Parche in-game (<see cref="PatchNotesFlyoutPanel"/>) sincronizadas con
/// cada build real, igual que <see cref="BuildVersionIncrementer"/> hace con el número de versión —
/// para no volver a tener un panel de Notas del Parche que muestre una versión distinta a la que
/// aparece en pantalla, ni texto de marcador de posición visible para los jugadores (el motivo
/// original de este script, 24 ago 2026: el panel se quedó anunciando "v0.1.4" con
/// PlayerSettings.bundleVersion ya en 0.1.5, y su texto de ejemplo/instrucciones para el propio
/// desarrollador seguía visible al final).
///
/// Trabaja sobre tres archivos de texto plano en Assets/Resources/PatchNotes/ (cargados en runtime
/// por PatchNotesFlyoutPanel vía Resources.Load&lt;TextAsset&gt;):
/// - CurrentEntryBullets.txt: los cambios de la build en curso, SIN cabecera ni número de versión —
///   se edita a mano durante el desarrollo, el mismo flujo de siempre, solo que ahora es un archivo
///   de texto en vez de un campo del Inspector.
/// - HistoryEntries.txt: el histórico de builds ya publicadas, con su cabecera "vX.Y.Z — Pre-Alpha
///   (fecha)" ya fijada para siempre. No se edita a mano — lo escribe este script.
/// - BuildDate.txt: la fecha del build más reciente, mismo formato que las cabeceras ("24 ago
///   2026"). Se sobrescribe automáticamente en cada build.
///
/// Se engancha después de BuildVersionIncrementer (callbackOrder -1000) para ver ya la versión
/// nueva:
/// 1. OnPreprocessBuild (callbackOrder -900): si CurrentEntryBullets.txt no existe, está vacío, o
///    sigue con el marcador de "pendiente", CANCELA el build (BuildFailedException) — mejor que un
///    build no salga a que salga con notas de parche en blanco o con texto interno visible para los
///    jugadores. Si todo está bien, escribe la fecha de hoy en BuildDate.txt.
/// 2. OnPostprocessBuild: solo si el build terminó en éxito y no se saltó el autoincremento de
///    versión (mismo flag que consulta BuildVersionIncrementer.WasLastBuildVersionSkipped), archiva
///    la entrada actual —ya con versión y fecha definitivas— al principio de HistoryEntries.txt, y
///    resetea CurrentEntryBullets.txt al marcador de "pendiente" para la próxima sesión de trabajo.
///
/// Para saltarte esto en un build de pruebas que no vas a publicar: usa el mismo menú que para la
/// versión ("El Sendero → Build → Saltar autoincremento de versión (solo el próximo build)") — al
/// saltarse el autoincremento, este script tampoco valida ni archiva nada en ese build.
/// </summary>
public class PatchNotesBuildGuard : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -900;

    const string PendingPlaceholder =
        "(Pendiente: añade aquí los cambios de esta build antes de compilar.)";

    const string ResourcesRoot = "Assets/Resources/PatchNotes";
    const string CurrentEntryFile = ResourcesRoot + "/CurrentEntryBullets.txt";
    const string HistoryFile = ResourcesRoot + "/HistoryEntries.txt";
    const string BuildDateFile = ResourcesRoot + "/BuildDate.txt";

    public void OnPreprocessBuild(BuildReport report)
    {
        if (BuildVersionIncrementer.WasLastBuildVersionSkipped)
        {
            Debug.Log("[PatchNotesBuildGuard] Autoincremento de versión saltado para este build — " +
                      "no se validan ni tocan las Notas del Parche.");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string currentEntryPath = Path.Combine(projectRoot, CurrentEntryFile);

        if (!File.Exists(currentEntryPath))
        {
            throw new BuildFailedException(
                $"[PatchNotesBuildGuard] Build cancelado: no existe '{CurrentEntryFile}'. Crea el " +
                "archivo con los cambios de esta build (solo los bullets, sin cabecera de versión) " +
                "antes de compilar.");
        }

        string bullets = File.ReadAllText(currentEntryPath).Trim();

        if (string.IsNullOrEmpty(bullets) || bullets == PendingPlaceholder)
        {
            throw new BuildFailedException(
                $"[PatchNotesBuildGuard] Build cancelado: '{CurrentEntryFile}' está vacío o sigue con " +
                "el marcador de pendiente. Añade los cambios reales de esta build antes de compilar " +
                "(o usa 'El Sendero → Build → Saltar autoincremento de versión' si es un build de " +
                "pruebas que no vas a publicar).");
        }

        string datePath = Path.Combine(projectRoot, BuildDateFile);
        File.WriteAllText(datePath, PatchNotesDateFormatter.FormatSpanishDate(System.DateTime.Now));
        AssetDatabase.ImportAsset(BuildDateFile);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (BuildVersionIncrementer.WasLastBuildVersionSkipped)
            return;

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogWarning("[PatchNotesBuildGuard] El build no terminó en éxito — no se archivan " +
                              "las Notas del Parche (se dejan tal cual para reintentar).");
            return;
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string currentEntryPath = Path.Combine(projectRoot, CurrentEntryFile);
        string historyPath = Path.Combine(projectRoot, HistoryFile);
        string dateText = ReadOrEmpty(Path.Combine(projectRoot, BuildDateFile)).Trim();

        if (string.IsNullOrEmpty(dateText))
            dateText = PatchNotesDateFormatter.FormatSpanishDate(System.DateTime.Now);

        string bullets = ReadOrEmpty(currentEntryPath).Trim();
        string existingHistory = ReadOrEmpty(historyPath);

        string header = $"v{UnityEditor.PlayerSettings.bundleVersion} — Pre-Alpha ({dateText})";
        string archivedEntry = $"{header}\n\n{bullets}";

        string newHistory = string.IsNullOrEmpty(existingHistory)
            ? archivedEntry
            : $"{archivedEntry}\n\n{existingHistory.TrimStart()}";

        File.WriteAllText(historyPath, newHistory);
        File.WriteAllText(currentEntryPath, PendingPlaceholder);

        AssetDatabase.ImportAsset(HistoryFile);
        AssetDatabase.ImportAsset(CurrentEntryFile);

        Debug.Log("[PatchNotesBuildGuard] Notas del Parche archivadas para " +
                  $"v{UnityEditor.PlayerSettings.bundleVersion} y CurrentEntryBullets.txt reseteado " +
                  "para la próxima build.");
    }

    static string ReadOrEmpty(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;
}
