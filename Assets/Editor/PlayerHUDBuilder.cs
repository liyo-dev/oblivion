#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class PlayerHUDBuilder
{
    [MenuItem("Tools/UI/Create Player HUD (Binder + Panel)")]
    public static void CreatePlayerHUD()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // 1) Canvas global
        var canvas = EnsureCanvas();
        // 2) EventSystem
        EnsureEventSystem();

        // 3) Panel del HUD (si existe uno con el mismo nombre, lo usamos)
        var panelRoot = GameObject.Find("HUD_PlayerPanel");
        if (!panelRoot)
        {
            panelRoot = BuildHudPanel(canvas.transform);
            Undo.RegisterCreatedObjectUndo(panelRoot, "Create HUD Panel");
        }

        // 4) Binder con PlayerHUDComplete
        var binder = Object.FindFirstObjectByType<PlayerHUDComplete>();
        if (!binder)
        {
            var binderGO = new GameObject("HUD_PlayerBinder");
            binder = Undo.AddComponent<PlayerHUDComplete>(binderGO);
            Undo.RegisterCreatedObjectUndo(binderGO, "Create HUD Binder");
            // Por limpieza, deja el binder en raíz de escena
            binderGO.transform.SetParent(null, false);
        }

        // 5) Autowire referencias
        AutoWire(binder, panelRoot);

        // 6) Selecciona el binder
        Selection.activeObject = binder.gameObject;

        Undo.CollapseUndoOperations(group);
        EditorUtility.DisplayDialog("Player HUD", "HUD creado y referencias enlazadas.", "OK");
    }

    // ---------- Infra ----------
    private static Canvas EnsureCanvas()
    {
        // Busca un canvas global existente
        var existing = GameObject.Find("HUD_Canvas_Global");
        if (existing)
        {
            var c = existing.GetComponent<Canvas>();
            if (c != null) return c;
        }

        var anyCanvas = Object.FindFirstObjectByType<Canvas>();
        if (anyCanvas && !IsUnderPlayer(anyCanvas.transform))
            return anyCanvas;

        // Crea uno nuevo global
        var canvasGO = new GameObject("HUD_Canvas_Global");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create HUD Canvas");

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(null, false);

        return canvas;
    }

    private static bool IsUnderPlayer(Transform t)
    {
        if (!t) return false;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (!player) return false;
        return t.IsChildOf(player.transform);
    }

    private static void EnsureEventSystem()
    {
        if (!Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }
    }

    // ---------- Construcción del Panel ----------
    private static GameObject BuildHudPanel(Transform parent)
    {
        // Panel raíz
        var panel = CreateGO("HUD_PlayerPanel", parent);
        var rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(30, -30);
        rect.sizeDelta = new Vector2(260, 180);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.25f);
        bg.raycastTarget = false;

        // Borde
        var border = CreateGO("MainBorder", panel.transform);
        var borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        var borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        borderImg.raycastTarget = false;
        border.transform.SetAsFirstSibling();

        // Contenedor vertical
        var vertical = CreateGO("VerticalContainer", panel.transform);
        var vRect = vertical.AddComponent<RectTransform>();
        vRect.anchorMin = Vector2.zero;
        vRect.anchorMax = Vector2.one;
        vRect.offsetMin = new Vector2(12, 12);
        vRect.offsetMax = new Vector2(-12, -12);

        var vLayout = vertical.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 8;
        vLayout.padding = new RectOffset(0, 0, 0, 0);
        vLayout.childControlHeight = false;
        vLayout.childControlWidth = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childAlignment = TextAnchor.UpperCenter;

        // Salud
        BuildHealth(vertical.transform);
        // Maná
        BuildMana(vertical.transform);
        // Slots
        BuildSlots(vertical.transform);

        return panel;
    }

    private static void BuildHealth(Transform parent)
    {
        var container = CreateGO("HealthContainer", parent);
        var cRect = container.AddComponent<RectTransform>();
        cRect.sizeDelta = new Vector2(0, 38);

        var sliderGO = CreateGO("HealthSlider", container.transform);
        var sRect = sliderGO.AddComponent<RectTransform>();
        sRect.anchorMin = Vector2.zero; sRect.anchorMax = Vector2.one;
        sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();
        slider.interactable = false;

        var bgGO = CreateGO("Background", sliderGO.transform);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        bgImg.raycastTarget = false;
        slider.targetGraphic = bgImg;

        var fillArea = CreateGO("Fill Area", sliderGO.transform);
        var faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(2, 2); faRect.offsetMax = new Vector2(-2, -2);

        var fill = CreateGO("Fill", fillArea.transform);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 0.2f, 1f); // high
        fillImg.raycastTarget = false;
        slider.fillRect = fillRect;

        var textGO = CreateGO("HealthText", sliderGO.transform);
        var tRect = textGO.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "100 / 100";
        tmp.fontSize = 16; tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,0.8f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        var hpGO = CreateGO("HPLabel", sliderGO.transform);
        var hpRect = hpGO.AddComponent<RectTransform>();
        hpRect.anchorMin = new Vector2(0,1); hpRect.anchorMax = new Vector2(0,1);
        hpRect.pivot = new Vector2(0,1); hpRect.anchoredPosition = new Vector2(5,0);
        hpRect.sizeDelta = new Vector2(30,15);
        var hp = hpGO.AddComponent<TextMeshProUGUI>();
        hp.text = "HP";
        hp.fontSize = 11; hp.fontStyle = FontStyles.Bold;
        hp.color = new Color(1f,1f,1f,0.7f);
        hp.alignment = TextAlignmentOptions.MidlineLeft;
        hp.raycastTarget = false;
    }

    private static void BuildMana(Transform parent)
    {
        var container = CreateGO("ManaContainer", parent);
        var cRect = container.AddComponent<RectTransform>();
        cRect.sizeDelta = new Vector2(0, 38);

        var sliderGO = CreateGO("ManaSlider", container.transform);
        var sRect = sliderGO.AddComponent<RectTransform>();
        sRect.anchorMin = Vector2.zero; sRect.anchorMax = Vector2.one;
        sRect.offsetMin = Vector2.zero; sRect.offsetMax = Vector2.zero;

        var slider = sliderGO.AddComponent<Slider>();
        slider.interactable = false;

        var bgGO = CreateGO("Background", sliderGO.transform);
        var bgRect = bgGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        bgImg.raycastTarget = false;
        slider.targetGraphic = bgImg;

        var fillArea = CreateGO("Fill Area", sliderGO.transform);
        var faRect = fillArea.AddComponent<RectTransform>();
        faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one;
        faRect.offsetMin = new Vector2(2, 2); faRect.offsetMax = new Vector2(-2, -2);

        var fill = CreateGO("Fill", fillArea.transform);
        var fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.4f, 0.9f, 1f); // mana
        fillImg.raycastTarget = false;
        slider.fillRect = fillRect;

        var textGO = CreateGO("ManaText", sliderGO.transform);
        var tRect = textGO.AddComponent<RectTransform>();
        tRect.anchorMin = Vector2.zero; tRect.anchorMax = Vector2.one;
        tRect.offsetMin = Vector2.zero; tRect.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "50 / 50";
        tmp.fontSize = 16; tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,0.8f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);

        var mpGO = CreateGO("MPLabel", sliderGO.transform);
        var mpRect = mpGO.AddComponent<RectTransform>();
        mpRect.anchorMin = new Vector2(0,1); mpRect.anchorMax = new Vector2(0,1);
        mpRect.pivot = new Vector2(0,1); mpRect.anchoredPosition = new Vector2(5,0);
        mpRect.sizeDelta = new Vector2(30,15);
        var mp = mpGO.AddComponent<TextMeshProUGUI>();
        mp.text = "MP";
        mp.fontSize = 11; mp.fontStyle = FontStyles.Bold;
        mp.color = new Color(1f,1f,1f,0.7f);
        mp.alignment = TextAlignmentOptions.MidlineLeft;
        mp.raycastTarget = false;
    }

    private static void BuildSlots(Transform parent)
    {
        var row = CreateGO("SlotsRow", parent);
        var rRect = row.AddComponent<RectTransform>();
        rRect.sizeDelta = new Vector2(0, 60);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8;
        h.padding = new RectOffset(0, 0, 5, 5);
        h.childControlHeight = true;
        h.childControlWidth = true;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;
        h.childAlignment = TextAnchor.MiddleCenter;

        BuildOneSlot(row.transform, "Slot_Left",    new Color(0.3f, 0.5f, 0.9f, 1f)); // X
        BuildOneSlot(row.transform, "Slot_Special", new Color(0.9f, 0.8f, 0.2f, 1f)); // Y
        BuildOneSlot(row.transform, "Slot_Right",   new Color(0.9f, 0.3f, 0.3f, 1f)); // B
    }

    private static GameObject BuildOneSlot(Transform parent, string name, Color buttonColor)
    {
        var slot = CreateGO(name, parent);
        var rect = slot.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(50, 50);

        var layout = slot.AddComponent<LayoutElement>();
        layout.preferredWidth = 50;
        layout.preferredHeight = 50;
        layout.flexibleWidth = 0;
        layout.flexibleHeight = 0;

        var borderGO = CreateGO("Border", slot.transform);
        var borderRect = borderGO.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = Vector2.zero; borderRect.offsetMax = Vector2.zero;
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = buttonColor;
        borderImg.raycastTarget = false;

        var bg = slot.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.2f, 0.6f);
        bg.raycastTarget = false;

        var iconGO = CreateGO("Icon", slot.transform);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero; iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(5, 5); iconRect.offsetMax = new Vector2(-5, -5);
        var icon = iconGO.AddComponent<Image>();
        icon.color = Color.white;
        icon.raycastTarget = false;

        var cdGO = CreateGO("CooldownOverlay", slot.transform);
        var cdRect = cdGO.AddComponent<RectTransform>();
        cdRect.anchorMin = Vector2.zero; cdRect.anchorMax = Vector2.one;
        cdRect.offsetMin = new Vector2(2, 2); cdRect.offsetMax = new Vector2(-2, -2);
        var cdImg = cdGO.AddComponent<Image>();
        cdImg.color = new Color(0f, 0f, 0f, 0.8f);
        cdImg.raycastTarget = false;
        cdImg.type = Image.Type.Filled;
        cdImg.fillMethod = Image.FillMethod.Radial360;
        cdImg.fillOrigin = 2;
        cdGO.SetActive(false);

        var cdTextGO = CreateGO("CooldownText", slot.transform);
        var cdTextRect = cdTextGO.AddComponent<RectTransform>();
        cdTextRect.anchorMin = Vector2.zero; cdTextRect.anchorMax = Vector2.one;
        cdTextRect.offsetMin = Vector2.zero; cdTextRect.offsetMax = Vector2.zero;
        var cdText = cdTextGO.AddComponent<TextMeshProUGUI>();
        cdText.text = "";
        cdText.fontSize = 18; cdText.fontStyle = FontStyles.Bold;
        cdText.color = Color.white; cdText.alignment = TextAlignmentOptions.Center;
        cdText.raycastTarget = false;
        var sh = cdTextGO.AddComponent<Shadow>();
        sh.effectColor = new Color(0,0,0,0.9f);
        sh.effectDistance = new Vector2(1.5f, -1.5f);
        cdTextGO.SetActive(false);

        return slot;
    }

    private static GameObject CreateGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // ---------- Auto-wire ----------
    private static void AutoWire(PlayerHUDComplete binder, GameObject panelRoot)
    {
        // Campos privados → usa SerializedObject para asignar
        var so = new SerializedObject(binder);

        // panelRoot
        SetProp(so, "panelRoot", panelRoot);

        // Salud
        var healthSlider   = panelRoot.transform.Find("VerticalContainer/HealthContainer/HealthSlider")?.GetComponent<Slider>();
        var healthFill     = panelRoot.transform.Find("VerticalContainer/HealthContainer/HealthSlider/Fill Area/Fill")?.GetComponent<Image>();
        var healthText     = panelRoot.transform.Find("VerticalContainer/HealthContainer/HealthSlider/HealthText")?.GetComponent<TextMeshProUGUI>();

        SetProp(so, "healthSlider", healthSlider);
        SetProp(so, "healthFill",   healthFill);
        SetProp(so, "healthText",   healthText);

        // Maná
        var manaSlider   = panelRoot.transform.Find("VerticalContainer/ManaContainer/ManaSlider")?.GetComponent<Slider>();
        var manaFill     = panelRoot.transform.Find("VerticalContainer/ManaContainer/ManaSlider/Fill Area/Fill")?.GetComponent<Image>();
        var manaText     = panelRoot.transform.Find("VerticalContainer/ManaContainer/ManaSlider/ManaText")?.GetComponent<TextMeshProUGUI>();

        SetProp(so, "manaSlider", manaSlider);
        SetProp(so, "manaFill",   manaFill);
        SetProp(so, "manaText",   manaText);

        // Slots
        var slotsRoot = panelRoot.transform.Find("VerticalContainer/SlotsRow");
        AutoWireSlot(so, "leftSlot",  slotsRoot, "Slot_Left");
        AutoWireSlot(so, "upSlot",    slotsRoot, "Slot_Special");
        AutoWireSlot(so, "rightSlot", slotsRoot, "Slot_Right");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(binder);
    }

    private static void AutoWireSlot(SerializedObject so, string fieldName, Transform slotsRoot, string slotName)
    {
        if (!slotsRoot) return;
        var slot = slotsRoot.Find(slotName);
        if (!slot) return;

        var root = slot.gameObject;
        var bg   = slot.GetComponent<Image>();
        var icon = slot.Find("Icon")?.GetComponent<Image>();
        var cdOv = slot.Find("CooldownOverlay")?.GetComponent<Image>();
        var cdTx = slot.Find("CooldownText")?.GetComponent<TextMeshProUGUI>();

        var slotProp = so.FindProperty(fieldName);
        if (slotProp == null) return;

        // slotProp es una clase serializable (SlotRefs). Asignamos sus campos.
        SetChildProp(slotProp, "root", root);
        SetChildProp(slotProp, "background", bg);
        SetChildProp(slotProp, "icon", icon);
        SetChildProp(slotProp, "cooldownOverlay", cdOv);
        SetChildProp(slotProp, "cooldown", cdTx);

        // slotType: define según nombre
        var slotTypeProp = slotProp.FindPropertyRelative("slotType");
        if (slotName.Contains("Left"))      slotTypeProp.enumValueIndex = (int)MagicSlot.Left;
        else if (slotName.Contains("Right"))slotTypeProp.enumValueIndex = (int)MagicSlot.Right;
        else                                 slotTypeProp.enumValueIndex = (int)MagicSlot.Special;
    }

    // Helpers SerializedObject
    private static void SetProp(SerializedObject so, string name, Object obj)
    {
        var prop = so.FindProperty(name);
        if (prop != null) prop.objectReferenceValue = obj;
    }

    private static void SetChildProp(SerializedProperty parent, string name, Object obj)
    {
        var prop = parent.FindPropertyRelative(name);
        if (prop != null) prop.objectReferenceValue = obj;
    }
}
#endif
