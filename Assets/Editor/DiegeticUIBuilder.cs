using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Genera los prefabs de UI diegética desde el menú El Sendero > Crear Prefabs UI.
/// Los prefabs se guardan en Assets/Prefabs/UI/ listos para arrastrar al Canvas de Start.unity.
/// </summary>
public static class DiegeticUIBuilder
{
    const string PrefabPath = "Assets/Prefabs/UI";

    // ── Menú ──────────────────────────────────────────────────────────────

    [MenuItem("El Sendero/Crear Prefabs UI/TutorialPromptUI")]
    static void BuildTutorialPrompt() => CreateTutorialPromptUI();

    [MenuItem("El Sendero/Crear Prefabs UI/SpeechBubbleUI")]
    static void BuildSpeechBubble() => CreateSpeechBubbleUI();

    [MenuItem("El Sendero/Crear Prefabs UI/PanicInputUI")]
    static void BuildPanicInput() => CreatePanicInputUI();

    [MenuItem("El Sendero/Crear Prefabs UI/Todos")]
    static void BuildAll()
    {
        CreateTutorialPromptUI();
        CreateSpeechBubbleUI();
        CreatePanicInputUI();
    }

    // ── TutorialPromptUI ──────────────────────────────────────────────────

    static void CreateTutorialPromptUI()
    {
        // Raíz — anclada al centro-inferior de la pantalla
        var root = MakeRT("TutorialPromptUI");
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0f);
        rootRT.anchorMax = new Vector2(0.5f, 0f);
        rootRT.pivot     = new Vector2(0.5f, 0f);
        rootRT.anchoredPosition = new Vector2(0f, 70f);
        rootRT.sizeDelta = new Vector2(700f, 70f);

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Fondo semitransparente
        var bg = Child(root, "Background");
        Stretch(bg);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false;

        // Fila horizontal: icono + texto
        var row = Child(root, "Row");
        Stretch(row);
        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment        = TextAnchor.MiddleCenter;
        hlg.spacing               = 14f;
        hlg.padding               = new RectOffset(24, 24, 8, 8);
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight= true;
        hlg.childControlWidth     = true;
        hlg.childControlHeight    = true;

        // Contenedor del icono (botón A, joystick…)
        var iconContainer = Child(row, "IconContainer");
        var icLE = iconContainer.AddComponent<LayoutElement>();
        icLE.minWidth       = 52f;
        icLE.preferredWidth = 52f;
        icLE.flexibleWidth  = 0f;
        var icImg = iconContainer.AddComponent<Image>();
        icImg.color = Color.white;
        icImg.raycastTarget = false;

        // Icono dentro del contenedor
        var icon = Child(iconContainer, "Icon");
        Stretch(icon);
        var iconImg = icon.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Texto del tutorial
        var labelGO = Child(row, "Label");
        var labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1f;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text      = "Pulsa A para continuar";
        label.fontSize  = 26f;
        label.color     = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget  = false;
        label.enableWordWrapping = false;

        // Conectar referencias al script
        var script = root.AddComponent<TutorialPromptUI>();
        var so = new SerializedObject(script);
        so.FindProperty("_rootGroup")     .objectReferenceValue = cg;
        so.FindProperty("_iconContainer") .objectReferenceValue = iconContainer;
        so.FindProperty("_icon")          .objectReferenceValue = iconImg;
        so.FindProperty("_label")         .objectReferenceValue = label;
        so.ApplyModifiedProperties();

        SavePrefab(root, $"{PrefabPath}/TutorialPromptUI.prefab");
    }

    // ── SpeechBubbleUI ────────────────────────────────────────────────────

    static void CreateSpeechBubbleUI()
    {
        // Raíz — posición controlada por código en LateUpdate, anclada en centro
        var root = MakeRT("SpeechBubbleUI");
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot     = new Vector2(0.5f, 0f);
        rootRT.sizeDelta = Vector2.zero;

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // Bocadillo — se posiciona encima del personaje
        var bubble = Child(root, "Bubble");
        var bubbleRT = bubble.GetComponent<RectTransform>();
        bubbleRT.anchorMin = bubbleRT.anchorMax = new Vector2(0.5f, 0f);
        bubbleRT.pivot     = new Vector2(0.5f, 0f);
        bubbleRT.sizeDelta = new Vector2(280f, 0f); // alto lo controla ContentSizeFitter

        var bubbleImg = bubble.AddComponent<Image>();
        bubbleImg.color = new Color(0.97f, 0.97f, 0.97f, 0.95f);
        bubbleImg.raycastTarget = false;

        // Auto-altura según el texto
        var vlg = bubble.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment       = TextAnchor.MiddleCenter;
        vlg.padding              = new RectOffset(14, 14, 10, 10);
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth    = true;
        vlg.childControlHeight   = true;

        var csf = bubble.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Texto del bocadillo
        var labelGO = Child(bubble, "Label");
        var le = labelGO.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text       = "Otra vez ese sueño...";
        label.fontSize   = 22f;
        label.color      = new Color(0.12f, 0.12f, 0.12f, 1f);
        label.alignment  = TextAlignmentOptions.Center;
        label.enableWordWrapping = true;
        label.raycastTarget = false;

        // Conectar referencias al script
        var script = root.AddComponent<SpeechBubbleUI>();
        var so = new SerializedObject(script);
        so.FindProperty("_rootGroup") .objectReferenceValue = cg;
        so.FindProperty("_bubbleRect").objectReferenceValue = bubbleRT;
        so.FindProperty("_label")     .objectReferenceValue = label;
        // _parentCanvasRect: se autodetecta en Awake con GetComponentInParent<Canvas>
        so.ApplyModifiedProperties();

        SavePrefab(root, $"{PrefabPath}/SpeechBubbleUI.prefab");
    }

    // ── PanicInputUI ──────────────────────────────────────────────────────

    static void CreatePanicInputUI()
    {
        // Raíz — centrada horizontalmente, a 1/3 de altura desde abajo
        var root = MakeRT("PanicInputUI");
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin        = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax        = new Vector2(0.5f, 0.5f);
        rootRT.pivot            = new Vector2(0.5f, 0.5f);
        rootRT.anchoredPosition = new Vector2(0f, -180f);
        rootRT.sizeDelta        = new Vector2(160f, 190f);

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;
        cg.blocksRaycasts = false;
        cg.interactable   = false;

        // Fondo semitransparente redondeado
        var bg = Child(root, "Background");
        Stretch(bg);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color         = new Color(0f, 0f, 0f, 0.55f);
        bgImg.raycastTarget = false;

        // Layout vertical: icono arriba, barra abajo
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment        = TextAnchor.MiddleCenter;
        vlg.padding               = new RectOffset(16, 16, 20, 20);
        vlg.spacing               = 14f;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = true;

        // ── Icono del botón (X / Square / A según plataforma) ────────────
        var iconGO = Child(root, "ButtonIcon");
        var iconLE = iconGO.AddComponent<LayoutElement>();
        iconLE.preferredHeight = 100f;
        iconLE.flexibleWidth   = 1f;
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.color           = Color.white;
        iconImg.preserveAspect  = true;
        iconImg.raycastTarget   = false;
        // Sprite asignar manualmente en Inspector después de generar el prefab

        // ── Barra de progreso (mash counter) ─────────────────────────────
        var sliderGO = Child(root, "ProgressBar");
        var sliderLE = sliderGO.AddComponent<LayoutElement>();
        sliderLE.preferredHeight = 18f;
        sliderLE.flexibleWidth   = 1f;
        var slider = sliderGO.AddComponent<Slider>();
        slider.direction  = Slider.Direction.LeftToRight;
        slider.minValue   = 0f;
        slider.maxValue   = 1f;
        slider.value      = 0f;
        slider.interactable = false;

        // Fondo de la barra
        var sliderBgGO = Child(sliderGO, "Background");
        Stretch(sliderBgGO);
        var sliderBgImg = sliderBgGO.AddComponent<Image>();
        sliderBgImg.color         = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        sliderBgImg.raycastTarget = false;

        // Fill area + fill
        var fillAreaGO = Child(sliderGO, "Fill Area");
        var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin  = new Vector2(0f, 0f);
        fillAreaRT.anchorMax  = new Vector2(1f, 1f);
        fillAreaRT.offsetMin  = Vector2.zero;
        fillAreaRT.offsetMax  = Vector2.zero;

        var fillGO = Child(fillAreaGO, "Fill");
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin  = new Vector2(0f, 0f);
        fillRT.anchorMax  = new Vector2(0f, 1f);
        fillRT.offsetMin  = Vector2.zero;
        fillRT.offsetMax  = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color         = new Color(1f, 0.45f, 0.08f, 1f); // naranja cálido
        fillImg.raycastTarget = false;

        slider.fillRect     = fillRT;
        slider.targetGraphic = sliderBgImg;

        // ── Conectar el componente PanicInputUI ───────────────────────────
        var script = root.AddComponent<PanicInputUI>();
        var so = new SerializedObject(script);
        so.FindProperty("_rootGroup")  .objectReferenceValue = cg;
        so.FindProperty("_buttonIcon") .objectReferenceValue = iconImg;
        so.FindProperty("_progressBar").objectReferenceValue = slider;
        so.ApplyModifiedProperties();

        SavePrefab(root, $"{PrefabPath}/PanicInputUI.prefab");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    static GameObject MakeRT(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        return go;
    }

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void SavePrefab(GameObject go, string path)
    {
        EnsureFolder(PrefabPath);
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[DiegeticUIBuilder] ✅ Prefab creado: {path}");
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }
}
