using System;
using Core.InputGlyphs;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor que construye y cablea la pantalla "Controles" del menú principal en
/// MainMenu.unity: crea Assets/_UI/ControlsSchemeConfig.asset (si no existe), crea
/// Assets/Prefabs/UI/ControlRow.prefab (si no existe), clona el botón de Ajustes para el nuevo
/// botón CONTROLES, clona el panel de Ajustes como base visual del panel de Controles (mismo
/// fondo/tamaño, sin sus hijos específicos), añade un ScrollRect+Content con ControlsMenuController,
/// y cablea MainMenuController.controlsButton/controlsMenu — todo con las APIs propias del Editor
/// de Unity, para no tocar el YAML de la escena a mano.
///
/// Uso: Assets → menú "El Sendero → Controles → Construir pantalla de Controles en MainMenu".
/// Requiere que ControlsSchemeConfig.cs, ControlRowWidget.cs, ControlsMenuController.cs y el
/// MainMenuController.cs actualizado (con los campos controlsButton/controlsMenu) ya estén en el
/// proyecto y compilando sin errores antes de ejecutar esto.
///
/// Es (razonablemente) seguro de re-ejecutar: si el asset, el prefab, el botón o el panel ya
/// existen, los reutiliza en vez de duplicarlos. No guarda la escena hasta el final, así que si
/// algo falla a mitad de camino no se pierde nada en disco (solo queda algún asset/prefab suelto,
/// inofensivo, que puedes borrar a mano si quieres empezar de cero).
/// </summary>
public static class ControlsMenuSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string SchemeAssetPath = "Assets/_UI/ControlsSchemeConfig.asset";
    const string RowPrefabFolder = "Assets/Prefabs/UI";
    const string RowPrefabPath = RowPrefabFolder + "/ControlRow.prefab";
    const string RowGlassSpritePath = "Assets/Art/UI/Menu/menu_row_glass.png"; // mismo sprite que usan los botones del menú principal

    [MenuItem("El Sendero/Controles/Construir pantalla de Controles en MainMenu")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[ControlsMenuBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        // No pisar trabajo sin guardar: este proceso cambia la escena activa a MainMenu.unity.
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[ControlsMenuBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[ControlsMenuBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        try
        {
            var scheme = BuildOrLoadScheme();
            var rowPrefab = BuildOrLoadRowPrefab();
            var controlsPanelGo = BuildOrLoadControlsPanel(rowPrefab, scheme);
            WireMainMenuController(controlsPanelGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ControlsMenuBuilder] ✅ Listo. Pantalla de Controles creada y cableada en MainMenu.unity. " +
                      "Revisa con 'git diff' qué se ha tocado, y ajusta colores/tamaños a mano si quieres afinar el estilo.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ControlsMenuBuilder] Error durante la construcción (la escena NO se ha guardado): {e}");
        }
    }

    // ── Datos ────────────────────────────────────────────────────────────

    static ControlsSchemeConfig BuildOrLoadScheme()
    {
        var existing = AssetDatabase.LoadAssetAtPath<ControlsSchemeConfig>(SchemeAssetPath);
        if (existing != null)
        {
            Debug.Log("[ControlsMenuBuilder] Ya existe ControlsSchemeConfig.asset — se reutiliza tal cual (no se pisa contenido editado a mano).");
            return existing;
        }

        EnsureFolder("Assets/_UI");

        var scheme = ScriptableObject.CreateInstance<ControlsSchemeConfig>();
        scheme.entries = ControlsSchemeConfig.BuildDefaultEntries();
        AssetDatabase.CreateAsset(scheme, SchemeAssetPath);

        Debug.Log($"[ControlsMenuBuilder] Creado {SchemeAssetPath} con las {scheme.entries.Count} acciones por defecto.");
        return scheme;
    }

    // ── Prefab de fila ───────────────────────────────────────────────────

    static ControlRowWidget BuildOrLoadRowPrefab()
    {
        var existingGo = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath);
        if (existingGo != null)
        {
            var existingComp = existingGo.GetComponent<ControlRowWidget>();
            if (existingComp != null)
            {
                Debug.Log("[ControlsMenuBuilder] Ya existe ControlRow.prefab — se reutiliza.");
                return existingComp;
            }
        }

        // Reutilizar la fuente TMP que ya usa el menú, para que la fila encaje visualmente sin
        // tener que adivinar qué font asset custom usa el proyecto.
        var referenceText = UnityEngine.Object.FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        TMP_FontAsset font = referenceText != null ? referenceText.font : null;

        var row = new GameObject("ControlRow", typeof(RectTransform));
        var rowRt = (RectTransform)row.transform;
        rowRt.sizeDelta = new Vector2(0f, 104f);

        // Fondo de cristal, mismo estilo que los botones del menú principal, para que la fila no
        // sea solo texto suelto sobre el panel — más legible y consistente con el resto del menú.
        var rowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RowGlassSpritePath);
        if (rowSprite != null)
        {
            var rowBg = new GameObject("RowGlassBG", typeof(RectTransform), typeof(Image));
            rowBg.transform.SetParent(row.transform, false);
            var rowBgRt = (RectTransform)rowBg.transform;
            rowBgRt.anchorMin = Vector2.zero;
            rowBgRt.anchorMax = Vector2.one;
            rowBgRt.offsetMin = Vector2.zero;
            rowBgRt.offsetMax = Vector2.zero;
            var rowBgImg = rowBg.GetComponent<Image>();
            rowBgImg.sprite = rowSprite;
            rowBgImg.type = Image.Type.Sliced;
            rowBgImg.raycastTarget = false;

            // ignoreLayout=true es imprescindible: el fondo se parentea bajo "row", que tiene (o
            // tendrá, se añade justo debajo) un HorizontalLayoutGroup con childControlWidth=true —
            // sin este LayoutElement, el grupo trata el fondo como un elemento más de la fila (el
            // primero, por orden de creación) y le IMPONE su propio ancho "preferido" (el nativo del
            // Image, muy estrecho), encogiéndolo a un cuadrado/óvalo minúsculo pegado al borde
            // izquierdo en vez de dejarlo cubrir toda la fila como marcan sus anchors (0,0)-(1,1).
            // Con ignoreLayout=true el grupo lo salta por completo y el fondo conserva el stretch
            // manual de arriba.
            var rowBgLayoutElement = rowBg.AddComponent<LayoutElement>();
            rowBgLayoutElement.ignoreLayout = true;
        }

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        // Margen izquierdo mayor que el derecho a propósito: a la izquierda hay ARTE (el sprite de
        // la tecla/botón), que con 14px quedaba prácticamente lamiendo el borde redondeado del
        // fondo de la fila; a la derecha solo hay texto flexible, que casi nunca llega al borde.
        layout.padding = new RectOffset(32, 24, 10, 10);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        // childControlWidth TIENE que ser true: es lo que hace que HorizontalLayoutGroup aplique de
        // verdad minWidth/preferredWidth/flexibleWidth de cada LayoutElement de abajo. En false (como
        // estaba antes), el grupo solo usa esos valores para CALCULAR dónde colocar cada hijo, pero
        // nunca les cambia el tamaño real — cada hijo se queda con el tamaño en bruto que le puso
        // Unity al crear el componente (p.ej. 100x100 en el Icon, 200x50 en los dos TMP_Text), así
        // que el icono salía más grande de lo pensado y la descripción quedaba fija en 200px en vez
        // de estirarse con flexibleWidth para ocupar el resto de la fila — de ahí que el texto se
        // cortara pronto y la fila se viera apretada/pegada a la izquierda en vez de aprovechar el
        // ancho real de la celda.
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var rowLayoutElement = row.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = 104f;
        rowLayoutElement.preferredHeight = 104f;

        // Icono — algo más pequeño que en la primera versión (columna única, ancho completo): en
        // dos columnas cada fila tiene aprox. la mitad de ancho, así que el icono/tecla dejan
        // menos hueco para forzar que la descripción no se corte.
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(row.transform, false);
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.preserveAspect = true;
        var iconLayoutElement = iconGo.AddComponent<LayoutElement>();
        iconLayoutElement.minWidth = 52f;
        iconLayoutElement.preferredWidth = 52f;

        // Tecla/botón
        var keyGo = new GameObject("KeyLabel", typeof(RectTransform));
        keyGo.transform.SetParent(row.transform, false);
        var keyText = keyGo.AddComponent<TextMeshProUGUI>();
        if (font != null) keyText.font = font;
        keyText.fontSize = 24f;
        keyText.alignment = TextAlignmentOptions.MidlineLeft;
        keyText.color = new Color(1f, 0.92f, 0.16f); // dorado — mismo tono que activeStateColor en SettingsMenuController
        keyText.textWrappingMode = TextWrappingModes.NoWrap;
        keyText.overflowMode = TextOverflowModes.Ellipsis;
        // Auto-tamaño: etiquetas cortas ("Q", "E", "T"...) se ven grandes a 24pt, pero combos como
        // "clic izquierdo"/"rueda arriba" no caben ahí ni de lejos — sin esto se cortaban con "…" y
        // quedaban ilegibles. Con auto-sizing, TMP encoge el texto hasta que quepa (mínimo 14pt)
        // antes de recurrir a la elipsis, así que solo se corta si ni siquiera a 14pt entra.
        keyText.enableAutoSizing = true;
        keyText.fontSizeMin = 14f;
        keyText.fontSizeMax = 24f;
        var keyLayoutElement = keyGo.AddComponent<LayoutElement>();
        keyLayoutElement.minWidth = 130f;
        keyLayoutElement.preferredWidth = 130f;

        // Descripción
        var descGo = new GameObject("DescriptionLabel", typeof(RectTransform));
        descGo.transform.SetParent(row.transform, false);
        var descText = descGo.AddComponent<TextMeshProUGUI>();
        if (font != null) descText.font = font;
        descText.fontSize = 21f;
        descText.alignment = TextAlignmentOptions.MidlineLeft;
        descText.color = Color.white;
        descText.textWrappingMode = TextWrappingModes.NoWrap;
        descText.overflowMode = TextOverflowModes.Ellipsis; // corta con "…" en vez de desbordar fuera de la celda
        var descLayoutElement = descGo.AddComponent<LayoutElement>();
        descLayoutElement.flexibleWidth = 1f;

        var widget = row.AddComponent<ControlRowWidget>();
        var so = new SerializedObject(widget);
        so.FindProperty("icon").objectReferenceValue = iconImg;
        so.FindProperty("keyLabel").objectReferenceValue = keyText;
        so.FindProperty("descriptionLabel").objectReferenceValue = descText;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(RowPrefabFolder);
        var prefabGo = PrefabUtility.SaveAsPrefabAsset(row, RowPrefabPath);
        UnityEngine.Object.DestroyImmediate(row);

        Debug.Log($"[ControlsMenuBuilder] Creado {RowPrefabPath}.");
        return prefabGo.GetComponent<ControlRowWidget>();
    }

    // ── Panel de Controles ───────────────────────────────────────────────

    static GameObject BuildOrLoadControlsPanel(ControlRowWidget rowPrefab, ControlsSchemeConfig scheme)
    {
        var settingsMenu = UnityEngine.Object.FindAnyObjectByType<SettingsMenuController>(FindObjectsInactive.Include);
        if (settingsMenu == null)
            throw new Exception("No se encontró SettingsMenuController en MainMenu.unity — no se puede clonar su estilo de panel.");

        var canvasParent = settingsMenu.transform.parent;
        var existingPanel = canvasParent != null ? canvasParent.Find("PanelControles") : null;
        if (existingPanel != null && existingPanel.GetComponent<ControlsMenuController>() != null)
        {
            Debug.Log("[ControlsMenuBuilder] Ya existe 'PanelControles' con ControlsMenuController — se reutiliza. " +
                      "Bórralo a mano en la Hierarchy si quieres que se regenere desde cero.");
            return existingPanel.gameObject;
        }

        // Clonar el panel de Ajustes como base: mismo fondo/tamaño que el resto de sub-paneles del menú.
        var panelGo = UnityEngine.Object.Instantiate(settingsMenu.gameObject, canvasParent);
        panelGo.name = "PanelControles";

        // Fuera el componente y los hijos específicos de Ajustes (idioma, sliders, invert-look...):
        // el panel de Controles construye su propio contenido desde cero. OJO: si el fondo visual
        // del panel de Ajustes es un HIJO (patrón habitual: "Background"/"BG"/"Fondo" como primer
        // hijo, en vez de un Image en la propia raíz), borrar todos los hijos sin distinguir lo deja
        // completamente transparente — el panel se activa y las filas se construyen bien (por eso
        // no salía ningún error), pero no hay nada detrás y queda invisible sobre el mundo 3D. Así
        // que primero se intenta conservar cualquier hijo que parezca fondo por nombre; si no se
        // encuentra ninguno, se añade uno propio para que el panel NUNCA se quede sin fondo,
        // independientemente de cómo esté montado el de Ajustes.
        string[] backgroundNameHints = { "background", "bg", "backdrop", "fondo", "panel_bg", "glass", "frame", "marco" };
        Transform preservedBackground = null;
        for (int i = panelGo.transform.childCount - 1; i >= 0; i--)
        {
            var child = panelGo.transform.GetChild(i);
            var childNameLower = child.name.ToLowerInvariant();
            bool looksLikeBackground = preservedBackground == null && Array.Exists(backgroundNameHints, h => childNameLower.Contains(h));
            if (looksLikeBackground)
            {
                preservedBackground = child;
                continue; // se conserva tal cual, incluida su posición como primer hijo (detrás del resto)
            }
            UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
        if (preservedBackground != null)
        {
            preservedBackground.SetAsFirstSibling();
            Debug.Log($"[ControlsMenuBuilder] Conservado '{preservedBackground.name}' como fondo del panel de Controles.");
        }
        else if (panelGo.GetComponent<Image>() == null)
        {
            // Ni la raíz ni ningún hijo tenían un Image de fondo reconocible: se añade uno propio,
            // discreto (oscuro semitransparente), para que el panel sea visible sí o sí.
            var fallbackBg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            fallbackBg.transform.SetParent(panelGo.transform, false);
            fallbackBg.transform.SetAsFirstSibling();
            var bgRt = (RectTransform)fallbackBg.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            fallbackBg.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 0.92f);
            Debug.LogWarning("[ControlsMenuBuilder] No se encontró ningún fondo reconocible al clonar el panel de Ajustes " +
                              "— se ha añadido un fondo oscuro genérico a 'PanelControles'. Ajusta el color/sprite a mano si quieres que combine mejor.");
        }

        // ScrollRect + Viewport + Content en dos columnas (GridLayoutGroup), para que la lista de
        // controles no quede como una única columna pegada a la izquierda y sea más fácil de leer.
        // Se construye con el panel TODAVÍA ACTIVO (SetActive(false) se hace al final): medir el
        // ancho real del Viewport con LayoutRebuilder para repartir las dos columnas requiere que
        // la jerarquía esté activa, si no el cálculo de tamaños no es fiable.
        var scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(panelGo.transform, false);
        var scrollRt = (RectTransform)scrollGo.transform;
        scrollRt.anchorMin = new Vector2(0.08f, 0.08f);
        scrollRt.anchorMax = new Vector2(0.92f, 0.9f);
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = Vector2.zero;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // RectMask2D en vez de Image+Mask: el combo Image (transparente) + Mask necesita esa Image
        // presente y HABILITADA para recortar nada (si se desactiva, sale el aviso "Masking
        // disabled due to Graphic component being disabled" y dejas de ver todo lo de dentro) — es
        // decir, depende de un componente cuyo único trabajo es "existir pero no pintarse", lo cual
        // es frágil (y en esta escena, por lo que sea, la Image acababa deshabilitada). RectMask2D
        // recorta directamente por los límites del RectTransform, sin necesitar ningún Graphic de
        // por medio, así que no hay ninguna casilla "enabled" que pueda desactivarse por error.
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(scrollGo.transform, false);
        var viewportRt = (RectTransform)viewportGo.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewportGo.transform, false);
        var contentRt = (RectTransform)contentGo.transform;
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;

        const int columns = 2;
        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.spacing = new Vector2(18f, 20f);
        grid.padding = new RectOffset(4, 4, 8, 8);

        // El ancho de celda se calcula a partir del ancho REAL del Viewport ya resuelto por Unity
        // (dentro del Canvas Scaler del proyecto), no un valor en píxeles inventado a mano que
        // podría no encajar con la resolución/escala real.
        LayoutRebuilder.ForceRebuildLayoutImmediate(viewportRt);
        float viewportWidth = viewportRt.rect.width;
        if (viewportWidth <= 1f) viewportWidth = 900f; // red de seguridad si el layout aún no se resolvió
        float cellWidth = (viewportWidth - grid.padding.left - grid.padding.right - grid.spacing.x * (columns - 1)) / columns;
        grid.cellSize = new Vector2(Mathf.Max(120f, cellWidth), 104f);

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRt;
        scrollRect.content = contentRt;

        var controller = panelGo.AddComponent<ControlsMenuController>();
        var so = new SerializedObject(controller);
        so.FindProperty("root").objectReferenceValue = panelGo;
        so.FindProperty("scheme").objectReferenceValue = scheme;
        so.FindProperty("rowsContainer").objectReferenceValue = contentRt;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        panelGo.SetActive(false); // igual que el resto de sub-paneles: arranca oculto, MainMenuController lo abre

        Debug.Log($"[ControlsMenuBuilder] Creado GameObject 'PanelControles' con ScrollRect a {columns} columnas " +
                  "(celda " + grid.cellSize.x.ToString("F0") + "x" + grid.cellSize.y.ToString("F0") + "px) y ControlsMenuController cableado.");
        return panelGo;
    }

    // ── Botón CONTROLES + cableado en MainMenuController ────────────────

    static void WireMainMenuController(GameObject controlsPanelGo)
    {
        var mainMenu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (mainMenu == null)
            throw new Exception("No se encontró MainMenuController en MainMenu.unity.");

        var controlsMenu = controlsPanelGo.GetComponent<ControlsMenuController>();
        var mainMenuSo = new SerializedObject(mainMenu);

        // Vía más fiable: leer directamente la referencia YA asignada en el Inspector al botón de
        // Ajustes — evita adivinar nombres/jerarquía (MainMenuController puede vivir en un
        // GameObject aparte del que contiene el Canvas/ButtonPanel, así que buscar por
        // GetComponentsInChildren desde MainMenuController no siempre encuentra nada).
        var settingsButtonProp = mainMenuSo.FindProperty("settingsButton");
        var settingsButton = settingsButtonProp != null ? settingsButtonProp.objectReferenceValue as Button : null;

        // Fallback: buscar por nombre de GameObject o por el texto visible del botón, en TODA la
        // escena.
        if (settingsButton == null)
            settingsButton = FindButtonByNameOrLabel("setting", "ajuste", "config");

        if (settingsButton == null)
        {
            var allButtons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            var names = string.Join(", ", Array.ConvertAll(allButtons, b => b.gameObject.name));
            throw new Exception("No se encontró el botón de Ajustes (ni por el campo 'settingsButton' del Inspector, ni por " +
                                 $"nombre/texto). Botones en la escena: [{names}]. Asigna 'Settings Button' a mano en el " +
                                 "componente MainMenuController (Inspector) y vuelve a ejecutar el comando.");
        }

        var existingControlsBtn = FindButtonByNameOrLabel("control");
        Button controlsButton;
        GameObject controlsButtonGo;
        if (existingControlsBtn != null)
        {
            Debug.Log("[ControlsMenuBuilder] Ya existe un botón con 'control' en el nombre — se reutiliza en vez de clonar uno nuevo.");
            controlsButton = existingControlsBtn;
            controlsButtonGo = existingControlsBtn.gameObject;
        }
        else
        {
            var clonedGo = UnityEngine.Object.Instantiate(settingsButton.gameObject, settingsButton.transform.parent);
            clonedGo.name = "BotonControles";
            clonedGo.transform.SetSiblingIndex(settingsButton.transform.GetSiblingIndex() + 1);
            controlsButtonGo = clonedGo;

            var btn = clonedGo.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent(); // limpiar el listener heredado (OnClickSettings del clon)
            controlsButton = btn;

            Debug.Log("[ControlsMenuBuilder] Creado botón 'BotonControles' (clonado del de Ajustes) en el ButtonPanel.");
        }

        // Se aplica SIEMPRE (tanto si el botón se acaba de clonar como si ya existía de una
        // ejecución anterior): la primera versión de esta herramienta escribía "CONTROLES" a pelo
        // sin tocar la clave de LocalizedText, así que un botón creado con esa versión antigua se
        // queda pegado a la clave heredada de Ajustes ("MainMenu_Settings") si no lo corregimos
        // aquí. El proyecto NO usa literales sueltos para los textos del menú, todo pasa por
        // LocalizationManager (ver Assets/Resources/Localization/ui_es.json y ui_en.json, clave
        // "MainMenu_Controls" ya añadida ahí con "Controles"/"Controls"). Re-apuntamos esa clave en
        // vez de escribir el texto a pelo: si no lo hiciéramos, en cuanto LocalizedText.Refresh()
        // corra en Play (Awake, o un cambio de idioma) pisaría lo que escribiéramos aquí con
        // "Configuración"/"Settings" otra vez, porque seguiría apuntando a la clave original.
        var localized = controlsButtonGo.GetComponentInChildren<LocalizedText>(true);
        var label = controlsButtonGo.GetComponentInChildren<TMP_Text>(true);
        if (localized != null)
        {
            localized.key = "MainMenu_Controls";
            // Refresco solo visual en el Editor (fuera de Play, LocalizedText.Refresh() no corre
            // porque depende de LocalizationManager.Instance, que no existe en edit mode): así el
            // botón ya se ve bien en la Scene view sin esperar a pulsar Play.
            if (label != null) label.text = "Controles";
        }
        else if (label != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[ControlsMenuBuilder] El botón CONTROLES no tiene LocalizedText — se escribe " +
                              "'Controles' como texto suelto (revisa la localización a mano).");
#endif
            label.text = "Controles";
        }

        mainMenuSo.FindProperty("controlsButton").objectReferenceValue = controlsButton;
        mainMenuSo.FindProperty("controlsMenu").objectReferenceValue = controlsMenu;
        mainMenuSo.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("[ControlsMenuBuilder] MainMenuController.controlsButton / controlsMenu cableados.");
    }

    /// <summary>
    /// Busca en TODA la escena (no solo bajo un GameObject concreto) un Button cuyo nombre de
    /// GameObject o cuyo texto visible (TMP o Text legacy) contenga alguno de los "needles"
    /// (comparación en minúsculas). Deliberadamente no restringido a los hijos de
    /// MainMenuController: en esta escena el script de menú puede vivir en un GameObject separado
    /// del que contiene el Canvas/ButtonPanel real.
    /// </summary>
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

        // Segunda pasada por el texto visible, por si el nombre del GameObject no es descriptivo
        // (p.ej. "Button (1)") pero la etiqueta sí lo es ("AJUSTES").
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

    // ── Utilidades ───────────────────────────────────────────────────────

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parent = path.Substring(0, path.LastIndexOf('/'));
        var name = path.Substring(path.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
