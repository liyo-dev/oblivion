using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Añade (o repara) al MainMenu dos filas de botón nuevas — NOTAS DEL PARCHE y REPORTAR UN FALLO —
/// e integra los componentes que les dan función (PatchNotesFlyoutPanel.cs y
/// BugReportFlyoutPanel.cs). Idea inspirada en el menú de otro juego en Kickstarter (Billie Bust
/// Up), que muestra Patch Notes y Bug Report junto a su fase de desarrollo — ver también
/// MainMenuPhaseLabelBuilder.cs (PRE-ALPHA).
///
/// Mismo patrón que MainMenuCreditsExitButtonsBuilder.cs para las filas de botón: clona el botón
/// CONTROLES (ya estilizado por MainMenuStylingBuilder) para heredar exactamente su estilo, lo
/// renombra y le cambia el texto + la clave de LocalizedText (sin reapuntar esa clave, el texto
/// clonado se pisaría con la traducción de "Controles" en Play — bug ya documentado y corregido dos
/// veces en este proyecto, ver comentario de MainMenuCreditsExitButtonsBuilder). Las filas se añaden
/// al final de la columna de botones (mismo padre que CONTROLES/CRÉDITOS/SALIR). Justo después,
/// <see cref="ReorderExitButtonToEnd"/> mueve SALIR al final de todo (convención habitual de
/// Salir/Quit al fondo — pedido por Raúl el 24 ago 2026), así que el orden final queda: ...,
/// Créditos, Notas del Parche, Reportar un Fallo, Salir.
///
/// Mismo patrón que MenuUIIntegrationBuilder.IntegrateCreditsPanel() para los componentes: crea un
/// GameObject vacío por componente si no existe todavía. Ninguno de los dos necesita cablearse a mano
/// en el Inspector — ambos se auto-enganchan al botón correspondiente en runtime por nombre/texto.
///
/// Reparador, no solo creador: si algo de esto ya existe (de una ejecución anterior), no lo duplica.
///
/// REPORTAR UN FALLO abre un panel de formulario nativo DENTRO del juego (BugReportFlyoutPanel.cs,
/// pedido por Raúl el 24 ago 2026 — antes abría el Google Form en el navegador del sistema vía
/// BugReportButton.cs, ya no se usa). Si la escena tiene el GameObject 'BugReportButton' de esa
/// versión anterior, <see cref="IntegrateBugReportPanel"/> lo elimina para que no compita por el
/// mismo botón.
///
/// FIX (24 ago 2026 — texto en mayúsculas en Play): igual que en MainMenuCreditsExitButtonsBuilder,
/// el placeholder de este array estaba en mayúsculas ("NOTAS DEL PARCHE"/"REPORTAR UN FALLO")
/// confiando en que LocalizedText.Refresh() lo pisaría con la traducción real en caso título — pero
/// la clave asignada aquí con `loc.key = ...` no sobrevivía al guardado de la escena (bug de raíz en
/// LocalizedText, ya corregido — ver LocalizedText.cs), así que el placeholder en mayúsculas se
/// quedaba fijo para siempre en Play. Corregido también aquí el propio placeholder, por si acaso.
///
/// Uso: menú "El Sendero → Controles → Añadir Patch Notes + Bug Report al Main Menu".
/// </summary>
public static class MainMenuPatchNotesBugReportBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string TemplateButtonName = "BotonControles";
    const string ExitButtonName = "BotonSalir";

    static readonly (string goName, string label, string locKey)[] Rows =
    {
        ("BotonPatchNotes", "Notas del Parche", "MainMenu_PatchNotes"),
        ("BotonBugReport", "Reportar un Fallo", "MainMenu_BugReport"),
    };

    [MenuItem("El Sendero/Controles/Añadir Patch Notes + Bug Report al Main Menu")]
    public static void AddButtonsAndComponents()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuPatchNotesBugReportBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuPatchNotesBugReportBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[MainMenuPatchNotesBugReportBuilder] No se pudo abrir {ScenePath}.");
                return;
            }

            AddButtonRows(scene);
            ReorderExitButtonToEnd();
            IntegratePatchNotesPanel(scene);
            IntegrateBugReportPanel(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MainMenuPatchNotesBugReportBuilder] ✅ Filas de botón + componentes listos. " +
                      "Dale a Play: NOTAS DEL PARCHE abre el panel de texto, REPORTAR UN FALLO abre el " +
                      "formulario nativo dentro del juego y lo manda directo al Google Form al pulsar ENVIAR.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainMenuPatchNotesBugReportBuilder] Error durante la integración: {e}");
        }
    }

    // ── Filas de botón (clonadas de BotonControles) ─────────────────────────

    static void AddButtonRows(Scene scene)
    {
        var template = FindButtonByNameIncludingInactive(TemplateButtonName);
        if (template == null)
        {
            Debug.LogError($"[MainMenuPatchNotesBugReportBuilder] No se encontró el botón plantilla '{TemplateButtonName}' — no se puede clonar el estilo. ¿Se ha renombrado el botón de Controles?");
            return;
        }

        int created = 0, repaired = 0;
        foreach (var (goName, label, locKey) in Rows)
        {
            var existing = FindButtonByNameIncludingInactive(goName);
            GameObject targetGo;

            if (existing != null)
            {
                targetGo = existing.gameObject;
                repaired++;
            }
            else
            {
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = goName;
                clone.transform.SetAsLastSibling();
                targetGo = clone;
                created++;
                Debug.Log($"[MainMenuPatchNotesBugReportBuilder] '{goName}' creado (clonado de '{TemplateButtonName}').");
            }

            SetLabel(targetGo, label);
            SetLocalizationKey(targetGo, locKey);
        }

        Debug.Log($"[MainMenuPatchNotesBugReportBuilder] Filas de botón: {created} creada(s), {repaired} reparada(s).");
    }

    static void SetLabel(GameObject buttonGo, string label)
    {
        var tmp = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) { tmp.text = label; return; }

        var legacy = buttonGo.GetComponentInChildren<Text>(true);
        if (legacy != null) { legacy.text = label; return; }

        Debug.LogWarning($"[MainMenuPatchNotesBugReportBuilder] '{buttonGo.name}': no se encontró ni TextMeshProUGUI ni Text para poner la etiqueta '{label}'.");
    }

    static void SetLocalizationKey(GameObject buttonGo, string key)
    {
        var loc = buttonGo.GetComponentInChildren<LocalizedText>(true);
        if (loc != null) { loc.key = key; return; }

        Debug.LogWarning($"[MainMenuPatchNotesBugReportBuilder] '{buttonGo.name}': no se encontró LocalizedText — el texto podría no traducirse, pero el literal puesto por SetLabel se mostrará igualmente.");
    }

    // Salir se creó originalmente como la última fila (era la última entonces), pero ahora que hay
    // dos filas nuevas detrás conviene que vuelva a ser la última — es la convención habitual
    // (Salir/Quit al fondo del todo). Reparador: si ya está al final, no hace nada ni loguea.
    static void ReorderExitButtonToEnd()
    {
        var exitButton = FindButtonByNameIncludingInactive(ExitButtonName);
        if (exitButton == null)
        {
            Debug.LogWarning($"[MainMenuPatchNotesBugReportBuilder] No se encontró el botón '{ExitButtonName}' para moverlo al final — ¿se ha renombrado?");
            return;
        }

        int lastIndex = exitButton.transform.parent.childCount - 1;
        if (exitButton.transform.GetSiblingIndex() != lastIndex)
        {
            exitButton.transform.SetAsLastSibling();
            Debug.Log($"[MainMenuPatchNotesBugReportBuilder] '{ExitButtonName}' movido al final de la columna (después de Notas del Parche y Reportar un Fallo).");
        }
    }

    static Button FindButtonByNameIncludingInactive(string name)
    {
        var all = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (var b in all)
            if (b.gameObject.name == name)
                return b;
        return null;
    }

    // ── Componentes (mismo patrón que MenuUIIntegrationBuilder.IntegrateCreditsPanel) ──────

    static void IntegratePatchNotesPanel(Scene scene)
    {
        if (FindInScene<PatchNotesFlyoutPanel>(scene) != null)
        {
            Debug.Log("[MainMenuPatchNotesBugReportBuilder] PatchNotesFlyoutPanel ya existe en MainMenu.unity — no se duplica.");
            return;
        }

        var go = new GameObject("PatchNotesFlyoutPanel");
        go.AddComponent<PatchNotesFlyoutPanel>();
        Debug.Log("[MainMenuPatchNotesBugReportBuilder] PatchNotesFlyoutPanel añadido.");
    }

    static void IntegrateBugReportPanel(Scene scene)
    {
        // Limpieza: la versión anterior (BugReportButton, ya no se usa) abría el Google Form en el
        // navegador del sistema — sustituida por BugReportFlyoutPanel, un formulario nativo dentro
        // del propio juego (pedido por Raúl el 24 ago 2026). Si la escena tiene el GameObject viejo
        // de una ejecución anterior, se elimina: si se dejaran los dos, ambos intentarían
        // engancharse al mismo botón REPORTAR UN FALLO y pisarían el listener del otro.
        var legacy = FindInScene<BugReportButton>(scene);
        if (legacy != null)
        {
            Debug.Log("[MainMenuPatchNotesBugReportBuilder] Eliminando el GameObject 'BugReportButton' antiguo (sustituido por BugReportFlyoutPanel).");
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }

        if (FindInScene<BugReportFlyoutPanel>(scene) != null)
        {
            Debug.Log("[MainMenuPatchNotesBugReportBuilder] BugReportFlyoutPanel ya existe en MainMenu.unity — no se duplica.");
            return;
        }

        var go = new GameObject("BugReportFlyoutPanel");
        go.AddComponent<BugReportFlyoutPanel>();
        Debug.Log("[MainMenuPatchNotesBugReportBuilder] BugReportFlyoutPanel añadido.");
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
