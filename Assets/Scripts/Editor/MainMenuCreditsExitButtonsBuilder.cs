using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Añade (o repara) dos filas del ButtonPanel del MainMenu: CRÉDITOS y SALIR.
///
/// Por qué hacía falta esto: CreditsFlyoutPanel (Assets/Scripts/UI/CreditsFlyoutPanel.cs) ya estaba
/// integrado en MainMenu.unity (vía MenuUIIntegrationBuilder), pero en Start() busca un Button YA
/// EXISTENTE en la escena cuyo nombre/texto contenga "credit" para engancharle su propio listener
/// (ver WireCreditsButton) — si ese botón no existe físicamente en la jerarquía, el panel no tiene
/// nada que enganchar y nunca se muestra, aunque el componente CreditsFlyoutPanel esté presente y
/// funcionando. Eso es justo lo que pasaba: "hemos añadido los créditos y no está saliendo" — el
/// botón CRÉDITOS en sí nunca llegó a crearse en MainMenu.unity.
///
/// De forma parecida, MainMenuController.OnClickExit() ya existía (hace Application.Quit() /
/// EditorApplication.isPlaying = false) pero no había ningún botón "Salir" en el menú que lo
/// disparase.
///
/// Este builder clona el botón CONTROLES (ya estilizado por MainMenuStylingBuilder: fondo de
/// cristal RowGlassBG + LayoutElement con el tamaño/spacing correctos) para heredar exactamente su
/// estilo, lo renombra y le cambia el texto. No hace falta cablear el OnClick a mano en el
/// Inspector:
/// - CRÉDITOS lo engancha automáticamente CreditsFlyoutPanel.WireCreditsButton() al entrar en Play
///   (busca por nombre/texto que contenga "credit", sin/con tilde).
/// - SALIR lo engancha automáticamente MainMenuController.Awake() vía TryFindExitButton() (busca
///   "salir"/"exit"/"quit" en el nombre del GameObject) — ver MainMenuController.cs.
///
/// FIX (primera versión de este builder): el texto se ponía bien en el editor, pero cada fila del
/// menú lleva también un componente LocalizedText (mismo GameObject que el TextMeshProUGUI) que en
/// Play sobreescribe el texto según una clave de localización — al clonar CONTROLES, ese componente
/// venía con key="MainMenu_Controls" y pisaba "CRÉDITOS"/"SALIR" de vuelta a "Controles" en tiempo
/// de ejecución (por eso en el editor se veía bien pero en Play salían dos filas "Controles"). Ahora
/// también se reapunta esa clave a "MainMenu_Credits"/"MainMenu_Exit" (ya traducida en
/// Assets/Resources/Localization/ui_es.json y ui_en.json — MainMenu_Exit ya existía, MainMenu_Credits
/// se ha añadido junto con este fix).
///
/// Reparador, no solo creador: si BotonCreditos/BotonSalir YA existen (p. ej. de una ejecución
/// anterior de este mismo builder antes del fix de arriba), no los duplica — les corrige el texto y
/// la clave de localización in-place. Así basta con volver a ejecutar el menú tras actualizar este
/// script para arreglar filas que ya se hubieran creado mal.
///
/// Uso: menú "El Sendero → Controles → Añadir Botones Créditos + Salir al Main Menu".
/// </summary>
public static class MainMenuCreditsExitButtonsBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";

    // Botón plantilla del que se clona estilo/tamaño (fila ya estilizada por MainMenuStylingBuilder).
    const string TemplateButtonName = "BotonControles";

    static readonly (string goName, string label, string locKey)[] Rows =
    {
        ("BotonCreditos", "CRÉDITOS", "MainMenu_Credits"),
        ("BotonSalir", "SALIR", "MainMenu_Exit"),
    };

    [MenuItem("El Sendero/Controles/Añadir Botones Créditos + Salir al Main Menu")]
    public static void AddButtons()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuCreditsExitButtonsBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuCreditsExitButtonsBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuCreditsExitButtonsBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        var template = FindButtonByNameIncludingInactive(TemplateButtonName);
        if (template == null)
        {
            Debug.LogError($"[MainMenuCreditsExitButtonsBuilder] No se encontró el botón plantilla '{TemplateButtonName}' — no se puede clonar el estilo. ¿Se ha renombrado el botón de Controles?");
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
                Debug.Log($"[MainMenuCreditsExitButtonsBuilder] '{goName}' ya existe — reaplicando texto/clave de localización por si venían mal (ver comentario del fix en este script).");
            }
            else
            {
                var clone = Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = goName;
                clone.transform.SetAsLastSibling();
                targetGo = clone;
                created++;
                Debug.Log($"[MainMenuCreditsExitButtonsBuilder] '{goName}' creado (clonado de '{TemplateButtonName}').");
            }

            SetLabel(targetGo, label);
            SetLocalizationKey(targetGo, locKey);
        }

        if (created == 0 && repaired == 0)
        {
            Debug.Log("[MainMenuCreditsExitButtonsBuilder] Nada que hacer.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MainMenuCreditsExitButtonsBuilder] ✅ {created} botón(es) creado(s), {repaired} reparado(s), guardado en {ScenePath}. " +
                  "Dale a Play: CRÉDITOS lo engancha CreditsFlyoutPanel y SALIR lo engancha MainMenuController automáticamente.");
    }

    static void SetLabel(GameObject buttonGo, string label)
    {
        var tmp = buttonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) { tmp.text = label; return; }

        var legacy = buttonGo.GetComponentInChildren<Text>(true);
        if (legacy != null) { legacy.text = label; return; }

        Debug.LogWarning($"[MainMenuCreditsExitButtonsBuilder] '{buttonGo.name}': no se encontró ni TextMeshProUGUI ni Text para poner la etiqueta '{label}'.");
    }

    // Las filas del menú se localizan en runtime vía LocalizedText.Refresh(), que pisa el texto que
    // se vea en el editor con la traducción de la clave configurada. Sin reapuntar esto, la fila
    // clonada de CONTROLES seguiría mostrando "Controles" en Play pese a tener el texto correcto en
    // el editor (justo el bug que motivó este fix).
    static void SetLocalizationKey(GameObject buttonGo, string key)
    {
        var loc = buttonGo.GetComponentInChildren<LocalizedText>(true);
        if (loc != null) { loc.key = key; return; }

        Debug.LogWarning($"[MainMenuCreditsExitButtonsBuilder] '{buttonGo.name}': no se encontró LocalizedText — el texto podría no traducirse, pero el literal puesto por SetLabel se mostrará igualmente.");
    }

    static Button FindButtonByNameIncludingInactive(string name)
    {
        var all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
        foreach (var b in all)
            if (b.gameObject.name == name)
                return b;
        return null;
    }
}
