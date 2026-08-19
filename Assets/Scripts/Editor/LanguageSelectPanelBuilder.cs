using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor que construye y cablea el selector de idioma de primer arranque
/// (LanguageSelectPanel.cs) en MainMenu.unity: clona el panel de Ajustes como base visual (mismo
/// fondo/tamaño que el resto de sub-paneles del menú), clona el botón CONTROLES dos veces para
/// "Español"/"English" (mismo fondo de cristal RowGlassBG y tamaño que ya tiene, ver
/// MainMenuCreditsExitButtonsBuilder), y cablea MainMenuController.languageSelectPanel — todo con
/// las APIs propias del Editor de Unity, mismo patrón que ControlsMenuSceneBuilder /
/// MainMenuCreditsExitButtonsBuilder, para no tocar el YAML de la escena a mano.
///
/// A diferencia de los botones clonados en MainMenuCreditsExitButtonsBuilder, aquí se ELIMINA el
/// componente LocalizedText de cada botón clonado en vez de reapuntar su clave: este panel se
/// enseña ANTES de que el jugador haya elegido idioma, así que cada botón debe mostrar su propio
/// idioma siempre ("Español"/"English"), nunca traducirse según el locale activo — si se dejara el
/// LocalizedText puesto, en cuanto LocalizationManager terminase de cargar pisaría el texto con la
/// traducción de la clave heredada del botón de origen (mismo tipo de bug que documenta el fix de
/// MainMenuCreditsExitButtonsBuilder, aquí evitado de raíz quitando el componente).
///
/// Es (razonablemente) seguro de re-ejecutar: si 'PanelSelectorIdioma' ya existe con un
/// LanguageSelectPanel, se reutiliza en vez de duplicarlo. No guarda la escena hasta el final, así
/// que si algo falla a mitad de camino no se pierde nada en disco.
///
/// Uso: menú "El Sendero → Controles → Construir Selector de Idioma en MainMenu".
/// </summary>
public static class LanguageSelectPanelBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string PanelName = "PanelSelectorIdioma";

    static readonly (string goName, string locale, string label)[] Buttons =
    {
        ("BotonEspanol", "es", "Español"),
        ("BotonIngles", "en", "English"),
    };

    [MenuItem("El Sendero/Controles/Construir Selector de Idioma en MainMenu")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[LanguageSelectPanelBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        // No pisar trabajo sin guardar: este proceso cambia la escena activa a MainMenu.unity.
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[LanguageSelectPanelBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[LanguageSelectPanelBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        try
        {
            var panelGo = BuildOrLoadPanel();
            WireMainMenuController(panelGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LanguageSelectPanelBuilder] ✅ Listo. Selector de idioma creado y cableado en MainMenu.unity. " +
                      "Revisa con 'git diff' qué se ha tocado, y ajusta colores/tamaños a mano si quieres afinar el estilo.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LanguageSelectPanelBuilder] Error durante la construcción (la escena NO se ha guardado): {e}");
        }
    }

    // ── Panel ────────────────────────────────────────────────────────────

    static GameObject BuildOrLoadPanel()
    {
        var settingsMenu = UnityEngine.Object.FindAnyObjectByType<SettingsMenuController>(FindObjectsInactive.Include);
        if (settingsMenu == null)
            throw new Exception("No se encontró SettingsMenuController en MainMenu.unity — no se puede clonar su estilo de panel.");

        // El panel de Ajustes puede colgar de un Canvas compartido (parent != null) o ser él mismo
        // un GameObject raíz con su propio Canvas (parent == null, caso real en este proyecto) —
        // se soportan los dos: si no hay padre, el nuevo panel se crea también como raíz, igual
        // que su plantilla.
        var canvasParent = settingsMenu.transform.parent;

        var existingPanel = FindExistingPanel(canvasParent, settingsMenu.gameObject.scene);
        if (existingPanel != null && existingPanel.GetComponent<LanguageSelectPanel>() != null)
        {
            Debug.Log($"[LanguageSelectPanelBuilder] Ya existe '{PanelName}' con LanguageSelectPanel — se reutiliza. " +
                      "Bórralo a mano en la Hierarchy si quieres que se regenere desde cero.");
            return existingPanel.gameObject;
        }

        // Clonar el panel de Ajustes como base: mismo fondo/tamaño que el resto de sub-paneles del
        // menú (mismo recurso que ya usó ControlsMenuSceneBuilder para 'PanelControles').
        var panelGo = UnityEngine.Object.Instantiate(settingsMenu.gameObject, canvasParent);
        panelGo.name = PanelName;

        // Fuera los componentes de menú de Ajustes: este panel construye su propio contenido.
        var staleSettings = panelGo.GetComponent<SettingsMenuController>();
        if (staleSettings != null) UnityEngine.Object.DestroyImmediate(staleSettings);

        // Igual que ControlsMenuSceneBuilder: conservar cualquier hijo que parezca fondo por
        // nombre (patrón habitual: "Background"/"BG"/"Fondo" como primer hijo) y borrar el resto
        // (sliders, botones de idioma de Ajustes, etc.). Si no se reconoce ningún fondo, se añade
        // uno propio para que el panel nunca se quede transparente sobre el mundo 3D.
        string[] backgroundNameHints = { "background", "bg", "backdrop", "fondo", "panel_bg", "glass", "frame", "marco" };
        Transform preservedBackground = null;
        for (int i = panelGo.transform.childCount - 1; i >= 0; i--)
        {
            var child = panelGo.transform.GetChild(i);
            var childNameLower = child.name.ToLowerInvariant();
            bool looksLikeBackground = preservedBackground == null && Array.Exists(backgroundNameHints, h => childNameLower.Contains(h));
            if (looksLikeBackground) { preservedBackground = child; continue; }
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        if (preservedBackground != null)
        {
            preservedBackground.SetAsFirstSibling();
            Debug.Log($"[LanguageSelectPanelBuilder] Conservado '{preservedBackground.name}' como fondo de '{PanelName}'.");
        }
        else if (panelGo.GetComponent<Image>() == null)
        {
            var fallbackBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            fallbackBg.transform.SetParent(panelGo.transform, false);
            fallbackBg.transform.SetAsFirstSibling();
            var bgRt = (RectTransform)fallbackBg.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            fallbackBg.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.92f);
            Debug.LogWarning($"[LanguageSelectPanelBuilder] No se encontró ningún fondo reconocible al clonar el panel de Ajustes " +
                              $"— se ha añadido un fondo oscuro genérico a '{PanelName}'. Ajusta el color/sprite a mano si quieres que combine mejor.");
        }

        // ── Contenedor central: título bilingüe + los dos botones de idioma ──
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(panelGo.transform, false);
        var contentRect = (RectTransform)contentGo.transform;
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(640f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 28f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildTitle(contentGo.transform);

        var templateButton = FindButtonByNameOrLabel("control") ?? UnityEngine.Object.FindAnyObjectByType<Button>(FindObjectsInactive.Include);
        if (templateButton == null)
            throw new Exception("No se encontró ningún botón existente en MainMenu.unity para clonar su estilo.");

        Button esButton = null, enButton = null;
        foreach (var (goName, locale, label) in Buttons)
        {
            var clone = CloneStyledButton(templateButton, contentGo.transform, goName, label);
            if (locale == "es") esButton = clone; else enButton = clone;
        }

        // ── Componente LanguageSelectPanel ──
        // AddComponent dispara Awake() de inmediato (también en Editor, fuera de Play Mode): con
        // root/languageOptions aún sin asignar, Awake() se limita a autoasignar root=gameObject y
        // a crear el CanvasGroup (root.AddComponent<CanvasGroup>() si no existe ya) — el resto de
        // Awake() (cablear los botones con listeners/UISelectVisual/UIButtonAudio) se salta porque
        // languageOptions todavía es null en este instante. No pasa nada: al entrar en Play Mode de
        // verdad, Unity vuelve a llamar Awake() desde cero leyendo YA los valores guardados en la
        // escena (root/canvasGroup/languageOptions de abajo), así que el cableado real ocurre ahí.
        var languageSelectPanel = panelGo.AddComponent<LanguageSelectPanel>();
        var canvasGroup = panelGo.GetComponent<CanvasGroup>() ?? panelGo.AddComponent<CanvasGroup>();

        var panelSo = new SerializedObject(languageSelectPanel);
        panelSo.FindProperty("root").objectReferenceValue = panelGo;
        panelSo.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;

        var optionsProp = panelSo.FindProperty("languageOptions");
        optionsProp.arraySize = Buttons.Length;
        for (int i = 0; i < Buttons.Length; i++)
        {
            var element = optionsProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("locale").stringValue = Buttons[i].locale;
            element.FindPropertyRelative("button").objectReferenceValue = Buttons[i].locale == "es" ? esButton : enButton;
        }
        panelSo.ApplyModifiedPropertiesWithoutUndo();

        panelGo.SetActive(false); // igual que el resto de sub-paneles: arranca oculto, MainMenuController lo abre

        Debug.Log($"[LanguageSelectPanelBuilder] Creado GameObject '{PanelName}' con LanguageSelectPanel cableado " +
                  $"({Buttons.Length} idiomas: {string.Join(", ", Array.ConvertAll(Buttons, b => b.locale))}).");
        return panelGo;
    }

    static void BuildTitle(Transform parent)
    {
        var referenceText = UnityEngine.Object.FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        TMP_FontAsset font = referenceText != null ? referenceText.font : null;

        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(parent, false);

        var title = titleGo.AddComponent<TextMeshProUGUI>();
        if (font != null) title.font = font;
        title.text = "Selecciona tu idioma  /  Select your language";
        title.fontSize = 40f;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        title.enableAutoSizing = true;
        title.fontSizeMin = 24f;
        title.fontSizeMax = 40f;

        var titleLayoutElement = titleGo.AddComponent<LayoutElement>();
        titleLayoutElement.minHeight = 70f;
        titleLayoutElement.preferredHeight = 70f;
    }

    /// <summary>
    /// Clona un botón ya estilizado (fondo de cristal RowGlassBG + LayoutElement de tamaño, ver
    /// MainMenuStylingBuilder/MainMenuCreditsExitButtonsBuilder) para heredar su estilo, le cambia
    /// el texto y ELIMINA su LocalizedText (ver comentario de cabecera: este botón no debe
    /// traducirse nunca, su idioma es fijo).
    /// </summary>
    static Button CloneStyledButton(Button template, Transform parent, string goName, string label)
    {
        var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
        clone.name = goName;
        clone.transform.localScale = Vector3.one;

        var button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // limpiar cualquier listener heredado del clon

        var localized = clone.GetComponentInChildren<LocalizedText>(true);
        if (localized != null)
            UnityEngine.Object.DestroyImmediate(localized);

        var tmp = clone.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = label;
        else
        {
            var legacy = clone.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
            else Debug.LogWarning($"[LanguageSelectPanelBuilder] '{goName}': no se encontró ni TextMeshProUGUI ni Text para poner la etiqueta '{label}'.");
        }

        return button;
    }

    // ── Enganche en MainMenuController ──────────────────────────────────

    static void WireMainMenuController(GameObject panelGo)
    {
        var mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (mainMenu == null)
            throw new Exception("No se encontró MainMenuController en MainMenu.unity.");

        var languageSelectPanel = panelGo.GetComponent<LanguageSelectPanel>();
        var mainMenuSo = new SerializedObject(mainMenu);
        mainMenuSo.FindProperty("languageSelectPanel").objectReferenceValue = languageSelectPanel;
        mainMenuSo.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[LanguageSelectPanelBuilder] MainMenuController.languageSelectPanel cableado.");
    }

    /// <summary>Busca el panel ya construido en una ejecución anterior. Si el panel de Ajustes
    /// cuelga de un Canvas compartido, basta con buscar entre sus hermanos; si es él mismo un
    /// GameObject raíz (caso real de este proyecto: cada sub-panel es su propio Canvas), hay que
    /// mirar entre los objetos raíz de la escena en su lugar.</summary>
    static Transform FindExistingPanel(Transform canvasParent, Scene scene)
    {
        if (canvasParent != null)
            return canvasParent.Find(PanelName);

        foreach (var root in scene.GetRootGameObjects())
            if (root.name == PanelName)
                return root.transform;

        return null;
    }

    /// <summary>Mismo patrón que ControlsMenuSceneBuilder.FindButtonByNameOrLabel: busca en TODA la
    /// escena un Button cuyo nombre de GameObject o texto visible contenga alguno de los "needles".</summary>
    static Button FindButtonByNameOrLabel(params string[] needles)
    {
        var all = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);

        foreach (var b in all)
        {
            var n = b.gameObject.name.ToLowerInvariant();
            foreach (var needle in needles)
                if (n.Contains(needle))
                    return b;
        }

        foreach (var b in all)
        {
            var tmp = b.GetComponentInChildren<TMP_Text>(true);
            var text = tmp != null ? tmp.text : b.GetComponentInChildren<Text>(true)?.text;
            if (string.IsNullOrEmpty(text)) continue;

            var t = text.ToLowerInvariant();
            foreach (var needle in needles)
                if (t.Contains(needle))
                    return b;
        }

        return null;
    }
}
