using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class EquipmentMenuCreator
{
    private const string MenuPath = "Tools/Create/Equipment Menu";
    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const string InventoryRowPrefabPath = PrefabFolder + "/InventoryRowWidget.prefab";
    private const string SpellRowPrefabPath = PrefabFolder + "/SpellRowWidget.prefab";
    private const string DefaultFontPath = "Assets/Plugins/Fonts/Nunito-Regular.ttf";

    [MenuItem(MenuPath, priority = 200)]
    public static void CreateMenu()
    {
        EnsureEventSystem();

        var inventoryRowPrefab = EnsureInventoryRowPrefab();
        var spellRowPrefab = EnsureSpellRowPrefab();

        var canvasGO = new GameObject("EquipmentMenu");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Equipment Menu");
        var rect = canvasGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        var canvasGroup = canvasGO.AddComponent<CanvasGroup>();

        var controller = canvasGO.AddComponent<PlayerEquipmentMenuController>();

        // Window root
        var window = CreateUIObject("Window", canvasGO.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = Vector2.one;
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        var windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0f, 0f, 0f, 0.6f);

        // Player info panel
        var infoPanel = CreateUIObject("PlayerInfo", window.transform);
        var infoRect = infoPanel.GetComponent<RectTransform>();
        infoRect.anchorMin = new Vector2(0f, 0.85f);
        infoRect.anchorMax = new Vector2(1f, 1f);
        infoRect.offsetMin = new Vector2(240f, -80f);
        infoRect.offsetMax = new Vector2(-240f, -20f);
        var infoLayout = infoPanel.AddComponent<HorizontalLayoutGroup>();
        infoLayout.childAlignment = TextAnchor.MiddleCenter;
        infoLayout.spacing = 40f;

        Text levelText = CreateInfoText("LevelLabel", infoPanel.transform, "Nivel: --");
        Text hpText = CreateInfoText("HPLabel", infoPanel.transform, "HP: --/--");
        Text mpText = CreateInfoText("MPLabel", infoPanel.transform, "MP: --/--");

        // Main content container
        var contentRoot = CreateUIObject("Content", window.transform);
        var contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = Vector2.zero;
        contentRect.anchorMax = new Vector2(1f, 0.85f);
        contentRect.offsetMin = new Vector2(40f, 40f);
        contentRect.offsetMax = new Vector2(-40f, -20f);

        // Left tabs panel
        var tabsPanel = CreateUIObject("TabsPanel", contentRoot.transform);
        var tabsRect = tabsPanel.GetComponent<RectTransform>();
        tabsRect.anchorMin = new Vector2(0f, 0f);
        tabsRect.anchorMax = new Vector2(0f, 1f);
        tabsRect.pivot = new Vector2(0f, 1f);
        tabsRect.sizeDelta = new Vector2(280f, 0f);
        var tabsImage = tabsPanel.AddComponent<Image>();
        tabsImage.color = new Color(0f, 0f, 0f, 0.4f);
        var tabsLayout = tabsPanel.AddComponent<VerticalLayoutGroup>();
        tabsLayout.padding = new RectOffset(20, 20, 20, 20);
        tabsLayout.spacing = 15f;
        tabsLayout.childAlignment = TextAnchor.UpperCenter;

        var resources = new DefaultControls.Resources();
        Button inventoryTabButton = CreateButton("InventarioButton", tabsPanel.transform, "Inventario", resources);
        Button spellsTabButton = CreateButton("HechizosButton", tabsPanel.transform, "Hechizos", resources);
        Button equipmentTabButton = CreateButton("EquipoButton", tabsPanel.transform, "Equipamiento", resources);

        // Content panels container
        var panelsRoot = CreateUIObject("Panels", contentRoot.transform);
        var panelsRect = panelsRoot.GetComponent<RectTransform>();
        panelsRect.anchorMin = new Vector2(0f, 0f);
        panelsRect.anchorMax = new Vector2(1f, 1f);
        panelsRect.offsetMin = new Vector2(320f, 0f);
        panelsRect.offsetMax = Vector2.zero;

        var inventoryPanel = BuildInventoryPanel(panelsRoot.transform, resources, inventoryRowPrefab);
        var spellsPanel = BuildSpellsPanel(panelsRoot.transform, resources, spellRowPrefab);
        var equipmentPanel = BuildEquipmentPanel(panelsRoot.transform, resources);

        spellsPanel.root.SetActive(false);
        equipmentPanel.root.SetActive(false);

        // Populate serialized fields
        var so = new SerializedObject(controller);
        so.FindProperty("canvas").objectReferenceValue = canvas;
        so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("windowRoot").objectReferenceValue = window;
        so.FindProperty("inventoryTabButton").objectReferenceValue = inventoryTabButton;
        so.FindProperty("spellsTabButton").objectReferenceValue = spellsTabButton;
        so.FindProperty("equipmentTabButton").objectReferenceValue = equipmentTabButton;
        so.FindProperty("levelText").objectReferenceValue = levelText;
        so.FindProperty("hpText").objectReferenceValue = hpText;
        so.FindProperty("mpText").objectReferenceValue = mpText;
        so.FindProperty("initialSelectionOverride").objectReferenceValue = inventoryTabButton.gameObject;

        var inventoryProp = so.FindProperty("inventoryUI");
        inventoryProp.FindPropertyRelative("root").objectReferenceValue = inventoryPanel.root;
        inventoryProp.FindPropertyRelative("rowsParent").objectReferenceValue = inventoryPanel.rowsParent;
        inventoryProp.FindPropertyRelative("rowPrefab").objectReferenceValue = inventoryRowPrefab.GetComponent<InventoryRowWidget>();
        inventoryProp.FindPropertyRelative("itemName").objectReferenceValue = inventoryPanel.itemName;
        inventoryProp.FindPropertyRelative("itemDescription").objectReferenceValue = inventoryPanel.itemDescription;
        inventoryProp.FindPropertyRelative("itemCount").objectReferenceValue = inventoryPanel.itemCount;
        inventoryProp.FindPropertyRelative("feedbackText").objectReferenceValue = inventoryPanel.feedbackText;
        inventoryProp.FindPropertyRelative("useButton").objectReferenceValue = inventoryPanel.useButton;

        var spellProp = so.FindProperty("spellUI");
        spellProp.FindPropertyRelative("root").objectReferenceValue = spellsPanel.root;
        spellProp.FindPropertyRelative("leftSlotButton").objectReferenceValue = spellsPanel.leftButton;
        spellProp.FindPropertyRelative("leftSlotLabel").objectReferenceValue = spellsPanel.leftLabel;
        spellProp.FindPropertyRelative("rightSlotButton").objectReferenceValue = spellsPanel.rightButton;
        spellProp.FindPropertyRelative("rightSlotLabel").objectReferenceValue = spellsPanel.rightLabel;
        spellProp.FindPropertyRelative("specialSlotButton").objectReferenceValue = spellsPanel.specialButton;
        spellProp.FindPropertyRelative("specialSlotLabel").objectReferenceValue = spellsPanel.specialLabel;
        spellProp.FindPropertyRelative("rowsParent").objectReferenceValue = spellsPanel.rowsParent;
        spellProp.FindPropertyRelative("rowPrefab").objectReferenceValue = spellRowPrefab.GetComponent<SpellRowWidget>();
        spellProp.FindPropertyRelative("detailsText").objectReferenceValue = spellsPanel.detailsText;

        var equipmentProp = so.FindProperty("equipmentUI");
        equipmentProp.FindPropertyRelative("root").objectReferenceValue = equipmentPanel.root;
        var rowsProp = equipmentProp.FindPropertyRelative("rows");
        rowsProp.ClearArray();
        foreach (var row in equipmentPanel.rows)
        {
            int index = rowsProp.arraySize;
            rowsProp.InsertArrayElementAtIndex(index);
            var element = rowsProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("category").enumValueIndex = (int)row.category;
            element.FindPropertyRelative("label").objectReferenceValue = row.label;
            element.FindPropertyRelative("previousButton").objectReferenceValue = row.previous;
            element.FindPropertyRelative("nextButton").objectReferenceValue = row.next;
            element.FindPropertyRelative("clearButton").objectReferenceValue = row.clear;
        }

        so.ApplyModifiedProperties();

        Selection.activeObject = canvasGO;
        EditorUtility.SetDirty(canvasGO);
        AssetDatabase.SaveAssets();
    }

    #region Builders

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    private static GameObject EnsureInventoryRowPrefab()
    {
        Directory.CreateDirectory(PrefabFolder);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryRowPrefabPath);
        if (existing != null) return existing;

        var resources = new DefaultControls.Resources();
        var go = DefaultControls.CreateButton(resources);
        go.name = "InventoryRowWidget";
        go.GetComponentInChildren<Text>().text = "Objeto x0";
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 48f);
        go.AddComponent<InventoryRowWidget>();
        PrefabUtility.SaveAsPrefabAsset(go, InventoryRowPrefabPath);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<GameObject>(InventoryRowPrefabPath);
    }

    private static GameObject EnsureSpellRowPrefab()
    {
        Directory.CreateDirectory(PrefabFolder);
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(SpellRowPrefabPath);
        if (existing != null) return existing;

        var resources = new DefaultControls.Resources();
        var go = DefaultControls.CreateButton(resources);
        go.name = "SpellRowWidget";
        go.GetComponentInChildren<Text>().text = "Hechizo";
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 48f);
        go.AddComponent<SpellRowWidget>();
        PrefabUtility.SaveAsPrefabAsset(go, SpellRowPrefabPath);
        Object.DestroyImmediate(go);
        return AssetDatabase.LoadAssetAtPath<GameObject>(SpellRowPrefabPath);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    private static Button CreateButton(string name, Transform parent, string label, DefaultControls.Resources resources)
    {
        var buttonGO = DefaultControls.CreateButton(resources);
        buttonGO.name = name;
        Undo.RegisterCreatedObjectUndo(buttonGO, $"Create {name}");
        GameObjectUtility.SetParentAndAlign(buttonGO, parent.gameObject);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 60f);
        buttonGO.GetComponentInChildren<Text>().text = label;
        return buttonGO.GetComponent<Button>();
    }

    private static Text CreateInfoText(string name, Transform parent, string initial)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        GameObjectUtility.SetParentAndAlign(go, parent.gameObject);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, 40f);
        var text = go.AddComponent<Text>();
        text.font = LoadFont();
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = initial;
        return text;
    }

    private static InventoryPanelBindings BuildInventoryPanel(Transform parent, DefaultControls.Resources resources, GameObject rowPrefab)
    {
        var panelRoot = DefaultControls.CreatePanel(resources);
        panelRoot.name = "InventoryPanel";
        Undo.RegisterCreatedObjectUndo(panelRoot, "Create Inventory Panel");
        GameObjectUtility.SetParentAndAlign(panelRoot, parent.gameObject);
        var rootImage = panelRoot.GetComponent<Image>();
        rootImage.color = new Color(0f, 0f, 0f, 0.2f);
        var rect = panelRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layout = panelRoot.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        var header = CreateInfoText("ItemName", panelRoot.transform, "Selecciona un objeto");
        header.alignment = TextAnchor.MiddleLeft;
        header.fontSize = 28;

        var countText = CreateInfoText("ItemCount", panelRoot.transform, "Cantidad: --");
        countText.alignment = TextAnchor.MiddleLeft;

        var descriptionGO = new GameObject("ItemDescription", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(descriptionGO, "Create ItemDescription");
        GameObjectUtility.SetParentAndAlign(descriptionGO, panelRoot);
        var descRect = descriptionGO.GetComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(0f, 120f);
        var descriptionText = descriptionGO.AddComponent<Text>();
        descriptionText.font = LoadFont();
        descriptionText.fontSize = 20;
        descriptionText.alignment = TextAnchor.UpperLeft;
        descriptionText.color = Color.white;
        descriptionText.text = "Descripción del objeto.";

        var feedback = CreateInfoText("Feedback", panelRoot.transform, "");
        feedback.alignment = TextAnchor.MiddleLeft;
        feedback.fontSize = 18;
        feedback.color = new Color(0.7f, 0.9f, 1f, 1f);

        var scroll = DefaultControls.CreateScrollView(resources);
        scroll.name = "InventoryScroll";
        Undo.RegisterCreatedObjectUndo(scroll, "Create Inventory Scroll");
        GameObjectUtility.SetParentAndAlign(scroll, panelRoot);
        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        var viewport = scroll.transform.Find("Viewport").GetComponent<RectTransform>();
        var content = viewport.transform.Find("Content").GetComponent<RectTransform>();
        content.gameObject.name = "Rows";
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

        var useButton = CreateButton("UseButton", panelRoot.transform, "Usar", resources);

        return new InventoryPanelBindings
        {
            root = panelRoot,
            rowsParent = content,
            itemName = header,
            itemCount = countText,
            itemDescription = descriptionText,
            feedbackText = feedback,
            useButton = useButton,
            rowPrefab = rowPrefab
        };
    }

    private static SpellPanelBindings BuildSpellsPanel(Transform parent, DefaultControls.Resources resources, GameObject rowPrefab)
    {
        var root = DefaultControls.CreatePanel(resources);
        root.name = "SpellsPanel";
        Undo.RegisterCreatedObjectUndo(root, "Create Spells Panel");
        GameObjectUtility.SetParentAndAlign(root, parent.gameObject);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.SetActive(false);

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 10f;

        var slotsPanel = CreateUIObject("SlotsPanel", root.transform);
        var slotsRect = slotsPanel.GetComponent<RectTransform>();
        slotsRect.anchorMin = new Vector2(0f, 1f);
        slotsRect.anchorMax = new Vector2(1f, 1f);
        slotsRect.sizeDelta = new Vector2(0f, 80f);
        var slotsLayout = slotsPanel.AddComponent<HorizontalLayoutGroup>();
        slotsLayout.spacing = 20f;
        slotsLayout.childAlignment = TextAnchor.MiddleCenter;

        Button leftButton = CreateButton("LeftSlotButton", slotsPanel.transform, "Ranura Izquierda", resources);
        Button rightButton = CreateButton("RightSlotButton", slotsPanel.transform, "Ranura Derecha", resources);
        Button specialButton = CreateButton("SpecialSlotButton", slotsPanel.transform, "Ranura Especial", resources);

        Text leftLabel = CreateInfoText("LeftSlotLabel", root.transform, "Izquierda: --");
        leftLabel.alignment = TextAnchor.MiddleLeft;
        Text rightLabel = CreateInfoText("RightSlotLabel", root.transform, "Derecha: --");
        rightLabel.alignment = TextAnchor.MiddleLeft;
        Text specialLabel = CreateInfoText("SpecialSlotLabel", root.transform, "Especial: --");
        specialLabel.alignment = TextAnchor.MiddleLeft;

        var scroll = DefaultControls.CreateScrollView(resources);
        scroll.name = "SpellsScroll";
        Undo.RegisterCreatedObjectUndo(scroll, "Create Spells Scroll");
        GameObjectUtility.SetParentAndAlign(scroll, root);
        var scrollRect = scroll.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        var viewport = scroll.transform.Find("Viewport").GetComponent<RectTransform>();
        var content = viewport.transform.Find("Content").GetComponent<RectTransform>();
        content.gameObject.name = "Rows";
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);

        var details = CreateInfoText("DetailsText", root.transform, "Selecciona un hechizo para ver detalles.");
        details.alignment = TextAnchor.UpperLeft;
        details.fontSize = 20;

        return new SpellPanelBindings
        {
            root = root,
            leftButton = leftButton,
            rightButton = rightButton,
            specialButton = specialButton,
            leftLabel = leftLabel,
            rightLabel = rightLabel,
            specialLabel = specialLabel,
            rowsParent = content,
            detailsText = details,
            rowPrefab = rowPrefab
        };
    }

    private static EquipmentPanelBindings BuildEquipmentPanel(Transform parent, DefaultControls.Resources resources)
    {
        var root = DefaultControls.CreatePanel(resources);
        root.name = "EquipmentPanel";
        Undo.RegisterCreatedObjectUndo(root, "Create Equipment Panel");
        GameObjectUtility.SetParentAndAlign(root, parent.gameObject);
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.2f);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.SetActive(false);

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;

        var rows = new[]
        {
            CreateEquipmentRow(root.transform, resources, PartCategory.Body, "Cuerpo"),
            CreateEquipmentRow(root.transform, resources, PartCategory.Cloak, "Capa"),
            CreateEquipmentRow(root.transform, resources, PartCategory.Head, "Cabeza"),
            CreateEquipmentRow(root.transform, resources, PartCategory.Hair, "Cabello"),
            CreateEquipmentRow(root.transform, resources, PartCategory.Accessory, "Accesorio")
        };

        return new EquipmentPanelBindings
        {
            root = root,
            rows = rows
        };
    }

    private static EquipmentPanelBindings.Row CreateEquipmentRow(Transform parent, DefaultControls.Resources resources, PartCategory category, string labelText)
    {
        var rowGO = CreateUIObject($"{category}Row", parent);
        var rowRect = rowGO.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 60f);
        var bg = rowGO.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.05f);

        var layout = rowGO.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 15f;
        layout.childAlignment = TextAnchor.MiddleLeft;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        GameObjectUtility.SetParentAndAlign(labelGO, rowGO);
        var labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.sizeDelta = new Vector2(0f, 40f);
        var label = labelGO.AddComponent<Text>();
        label.font = LoadFont();
        label.fontSize = 22;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.text = $"{labelText}: --";

        var buttonsHolder = CreateUIObject("Buttons", rowGO.transform);
        var buttonsRect = buttonsHolder.GetComponent<RectTransform>();
        buttonsRect.sizeDelta = new Vector2(0f, 40f);
        var buttonsLayout = buttonsHolder.AddComponent<HorizontalLayoutGroup>();
        buttonsLayout.spacing = 10f;
        buttonsLayout.childAlignment = TextAnchor.MiddleRight;

        Button prev = CreateButton("Previous", buttonsHolder.transform, "<", resources);
        Button next = CreateButton("Next", buttonsHolder.transform, ">", resources);
        Button clear = CreateButton("Clear", buttonsHolder.transform, "X", resources);

        return new EquipmentPanelBindings.Row
        {
            category = category,
            label = label,
            previous = prev,
            next = next,
            clear = clear
        };
    }

    #endregion

    private static Font LoadFont()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
        if (font == null)
        {
            font = AssetDatabase.FindAssets("t:Font")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<Font>(path))
                .FirstOrDefault(f => f != null);
        }
        if (font == null)
        {
            Debug.LogWarning("[EquipmentMenuCreator] No se encontró fuente Nunito. Usando LegacyRuntime.ttf.");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return font;
    }

    #region Helper structs

    private struct InventoryPanelBindings
    {
        public GameObject root;
        public Transform rowsParent;
        public Text itemName;
        public Text itemDescription;
        public Text itemCount;
        public Text feedbackText;
        public Button useButton;
        public GameObject rowPrefab;
    }

    private struct SpellPanelBindings
    {
        public GameObject root;
        public Button leftButton;
        public Button rightButton;
        public Button specialButton;
        public Text leftLabel;
        public Text rightLabel;
        public Text specialLabel;
        public Transform rowsParent;
        public Text detailsText;
        public GameObject rowPrefab;
    }

    private struct EquipmentPanelBindings
    {
        public GameObject root;
        public Row[] rows;

        public struct Row
        {
            public PartCategory category;
            public Text label;
            public Button previous;
            public Button next;
            public Button clear;
        }
    }

    #endregion
}
