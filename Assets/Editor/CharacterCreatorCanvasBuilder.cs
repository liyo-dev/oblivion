#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class CharacterCreatorCanvasBuilder
{
    static readonly string[] LeftCats = {
        "Body","Cloak","Head","Hair","Eyes","Mouth","Hat","Eyebrow","Accessory"
    };
    static readonly string[] RightCats = { "OHS","Shield","Bow" };

    [MenuItem("El Sendero/Personajes/Create Character Creator Canvas (Designer Only)")]
    public static void CreateCanvas()
    {
        var builder = Object.FindFirstObjectByType<ModularAutoBuilder>();
        if (!builder)
        {
            EditorUtility.DisplayDialog("Character Creator",
                "Coloca en la escena un personaje con 'ModularAutoBuilder' antes de crear la UI.",
                "Ok");
            return;
        }

        if (!Object.FindFirstObjectByType<EventSystem>())
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // Canvas base
        var canvasGO = new GameObject("UI_Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Character Creator Canvas");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Highlighter ÚNICO (para ambas columnas)
        var hi = canvasGO.AddComponent<RowSelectionHighlighter>();
        hi.normalColor   = new Color(1, 1, 1, 0.00f);
        hi.selectedColor = new Color(1f, 0.92f, 0.16f, 0.85f);

        // UI Controller
        var ui = canvasGO.AddComponent<CharacterCreatorUI>();
        ui.builder = builder;
        ui.highlighter = hi;

        // Actions Controller (para conectar botones Random y Bake en runtime)
        var actions = canvasGO.AddComponent<CharacterCreatorActions>();
        actions.ui = ui;
        actions.builder = builder;

        // ===== Panel izquierdo =====
        var left = NewPanel("Panel_Left", canvasGO.transform);
        var vLeft = left.GetComponent<VerticalLayoutGroup>();
        vLeft.spacing = 8; vLeft.padding = new RectOffset(16,16,16,16);
        vLeft.childForceExpandHeight = false; vLeft.childForceExpandWidth = false;

        var rtL = left.GetComponent<RectTransform>();
        rtL.anchorMin = new Vector2(0f, 1f);
        rtL.anchorMax = new Vector2(0f, 1f);
        rtL.pivot     = new Vector2(0f, 1f);
        rtL.sizeDelta = new Vector2(600f, 1200f);
        rtL.anchoredPosition = new Vector2(20f, -20f);

        foreach (var c in LeftCats)
            MakeRow(left.transform, c, builder, hi);

        // Acciones en el panel izquierdo
        var actionsPanel = New("Row_Actions", left.transform, typeof(HorizontalLayoutGroup));
        actionsPanel.GetComponent<HorizontalLayoutGroup>().spacing = 10;

        var random = MakeButton(actionsPanel.transform, "Random", new Vector2(140, 54));
        random.GetComponent<Button>().onClick.AddListener(ui.RandomizeAll);

        var bake = MakeButton(actionsPanel.transform, "Bake NPC (Editor)", new Vector2(220, 54));
        bake.GetComponent<Button>().onClick.AddListener(() =>
        {
            Selection.activeGameObject = builder.gameObject;
            ModularCharacterBaker.BakeSelected();
        });

        // ===== Panel derecho (armas) =====
        var right = NewPanel("Panel_Right", canvasGO.transform);
        var vRight = right.GetComponent<VerticalLayoutGroup>();
        vRight.spacing = 8; vRight.padding = new RectOffset(16,16,16,16);
        vRight.childForceExpandHeight = false; vRight.childForceExpandWidth = false;

        var rtR = right.GetComponent<RectTransform>();
        rtR.anchorMin = new Vector2(1f, 1f);
        rtR.anchorMax = new Vector2(1f, 1f);
        rtR.pivot     = new Vector2(1f, 1f);
        rtR.sizeDelta = new Vector2(600f, 1200f);
        rtR.anchoredPosition = new Vector2(-20f, -20f);

        foreach (var c in RightCats)
            MakeRow(right.transform, c, builder, hi);

        Selection.activeGameObject = canvasGO;
    }

    // ---------- helpers ----------
    static Font BuiltinUIFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static GameObject New(string n, Transform p, params System.Type[] t)
    {
        var go = new GameObject(n, t);
        go.transform.SetParent(p, false);
        return go;
    }

    static GameObject NewPanel(string name, Transform parent)
    {
        var p = New(name, parent, typeof(Image), typeof(VerticalLayoutGroup));
        p.GetComponent<Image>().color = new Color(0.07f, 0.13f, 0.23f, 0.95f);
        return p;
    }

    static Text MakeText(Transform p, string txt, int size, TextAnchor a, Vector2 dim)
    {
        var go = New("Text", p, typeof(Text));
        var t = go.GetComponent<Text>();
        t.text = txt; t.font = BuiltinUIFont();
        t.alignment = a; t.fontSize = size; t.color = Color.white;
        go.GetComponent<RectTransform>().sizeDelta = dim;
        return t;
    }

    static GameObject MakeButton(Transform p, string label, Vector2 dim)
    {
        var go = New("Button", p, typeof(Image), typeof(Button));
        go.GetComponent<Image>().color = new Color(1, 1, 1, 0.15f);
        go.GetComponent<RectTransform>().sizeDelta = dim;

        var txt = New("Text", go.transform, typeof(Text)).GetComponent<Text>();
        txt.text = label; txt.font = BuiltinUIFont();
        txt.alignment = TextAnchor.MiddleCenter; txt.fontSize = 22; txt.color = Color.white;

        var tr = txt.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;

        return go;
    }

    static void MakeRow(Transform parent, string category, ModularAutoBuilder builder,
                        RowSelectionHighlighter hi)
    {
        var row = New($"Row_{category}", parent, typeof(HorizontalLayoutGroup));
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 6; h.childForceExpandWidth = false; h.childForceExpandHeight = false;

        MakeText(row.transform, category, 22, TextAnchor.MiddleLeft, new Vector2(160, 48));

        var prev = MakeButton(row.transform, "◄", new Vector2(60, 48));
        var prevStep = prev.AddComponent<UIButtonStep>();
        prevStep.ui = Object.FindFirstObjectByType<CharacterCreatorUI>();
        prevStep.category = category; prevStep.step = -1;

        var next = MakeButton(row.transform, "►", new Vector2(60, 48));
        var nextStep = next.AddComponent<UIButtonStep>();
        nextStep.ui = Object.FindFirstObjectByType<CharacterCreatorUI>();
        nextStep.category = category; nextStep.step = +1;

        var none = MakeButton(row.transform, "None", new Vector2(90, 48));
        var noneComp = none.AddComponent<UIButtonSetNone>();
        noneComp.ui = Object.FindFirstObjectByType<CharacterCreatorUI>();
        noneComp.category = category;

        var active = MakeText(row.transform, "-", 18, TextAnchor.MiddleLeft, new Vector2(320, 48));
        var watch = active.gameObject.AddComponent<UITextCurrentPart>();
        watch.builder = builder; watch.category = category;

        // registrar fila en el highlighter global
        if (hi) hi.RegisterRow(row.transform as RectTransform);
    }
}
#endif
