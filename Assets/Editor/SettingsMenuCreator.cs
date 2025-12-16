using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsMenuCreator
{
    private const string MenuPath = "Tools/Create/Settings Menu";
    private static Font NunitoRegular => AssetDatabase.LoadAssetAtPath<Font>("Assets/Plugins/Fonts/Nunito-Regular.ttf");
    private static Font NunitoBold => AssetDatabase.LoadAssetAtPath<Font>("Assets/Plugins/Fonts/Nunito-Bold.ttf");

    [MenuItem(MenuPath, priority = 210)]
    public static void CreateMenu()
    {

        var resources = new DefaultControls.Resources();
        var canvasGO = new GameObject("SettingsMenu");
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Settings Menu");
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

        var controller = canvasGO.AddComponent<SettingsMenuController>();

        // Window root
        var window = CreateUIObject("Window", canvasGO.transform);
        var windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = Vector2.one;
        windowRect.offsetMin = Vector2.zero;
        windowRect.offsetMax = Vector2.zero;
        var windowImage = window.AddComponent<Image>();
        windowImage.color = new Color(0f, 0f, 0f, 0.55f);

        // Content box
        var content = CreateUIObject("Content", window.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(560f, 640f);
        contentRect.anchoredPosition = Vector2.zero;
        var contentImage = content.AddComponent<Image>();
        contentImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(24, 24, 24, 24);
        contentLayout.spacing = 16f;
        contentLayout.childAlignment = TextAnchor.UpperCenter;
        contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateTitle(content.transform, "Ajustes");

        // Language section
        CreateSectionLabel(content.transform, "Idioma");
        var languageRow = CreateRow(content.transform, spacing: 8f, childAlignment: TextAnchor.MiddleCenter);
        var spanishButton = CreateButton("SpanishButton", languageRow.transform, "Español", resources);
        var englishButton = CreateButton("EnglishButton", languageRow.transform, "English", resources);

        // Audio section
        CreateSectionLabel(content.transform, "Audio");
        CreateSliderRow(content.transform, "Volumen general", resources, out Slider masterSlider);
        masterSlider.minValue = 0f;
        masterSlider.maxValue = 1f;
        masterSlider.value = 1f;

        CreateSliderRow(content.transform, "Volumen SFX", resources, out Slider sfxSlider);
        sfxSlider.minValue = 0f;
        sfxSlider.maxValue = 1f;
        sfxSlider.value = 1f;

        CreateSliderRow(content.transform, "Volumen música", resources, out Slider musicSlider);
        musicSlider.minValue = 0f;
        musicSlider.maxValue = 1f;
        musicSlider.value = 1f;

        // Camera section
        CreateSectionLabel(content.transform, "Cámara / Controles");
        CreateToggleRow(content.transform, "Invertir eje Y (suelo)", resources, out Toggle invertLook);
        CreateToggleRow(content.transform, "Invertir eje Y (vuelo)", resources, out Toggle invertFlight);
        invertLook.isOn = false;
        invertFlight.isOn = false;
        CreateSliderRow(content.transform, "Sensibilidad cámara", resources, out Slider lookSensitivity);
        lookSensitivity.minValue = 0.1f;
        lookSensitivity.maxValue = 5f;
        lookSensitivity.value = 1f;

        // General / accesibilidad
        CreateSectionLabel(content.transform, "General");
        CreateToggleRow(content.transform, "Subtítulos", resources, out Toggle subtitlesToggle);
        subtitlesToggle.isOn = true;
        CreateToggleRow(content.transform, "Vibración", resources, out Toggle vibrationToggle);
        vibrationToggle.isOn = true;
        CreateToggleRow(content.transform, "Pantalla completa", resources, out Toggle fullscreenToggle);
        fullscreenToggle.isOn = true;

        // Back button
        var backRow = CreateRow(content.transform, spacing: 0f, childAlignment: TextAnchor.MiddleCenter);
        var backButton = CreateButton("CloseButton", backRow.transform, "Cerrar", resources);
        var backLayout = backRow.GetComponent<HorizontalLayoutGroup>();
        backLayout.childForceExpandWidth = false;

        // Populate serialized fields
        var so = new SerializedObject(controller);
        so.FindProperty("root").objectReferenceValue = window;
        so.FindProperty("firstSelection").objectReferenceValue = spanishButton;
        so.FindProperty("backButton").objectReferenceValue = backButton;
        so.FindProperty("spanishButton").objectReferenceValue = spanishButton;
        so.FindProperty("englishButton").objectReferenceValue = englishButton;
        so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
        so.FindProperty("invertLookToggle").objectReferenceValue = invertLook;
        so.FindProperty("invertFlightToggle").objectReferenceValue = invertFlight;
        so.FindProperty("lookSensitivitySlider").objectReferenceValue = lookSensitivity;
        so.FindProperty("subtitlesToggle").objectReferenceValue = subtitlesToggle;
        so.FindProperty("vibrationToggle").objectReferenceValue = vibrationToggle;
        so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
        so.ApplyModifiedProperties();

        Selection.activeObject = canvasGO;
        EditorUtility.SetDirty(canvasGO);
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Settings Menu");
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }

    private static void CreateTitle(Transform parent, string text)
    {
        var label = CreateLabel(parent, text, 28, FontStyle.Bold);
        var layout = label.GetComponent<LayoutElement>();
        layout.preferredHeight = 48f;
    }

    private static void CreateSectionLabel(Transform parent, string text)
    {
        var label = CreateLabel(parent, text, 20, FontStyle.Bold);
        var layout = label.GetComponent<LayoutElement>();
        layout.preferredHeight = 32f;
    }

    private static Text CreateLabel(Transform parent, string text, int fontSize, FontStyle style)
    {
        var go = CreateUIObject(text.Replace(" ", "") + "Label", parent);
        var txt = go.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleLeft;
        txt.font = style == FontStyle.Bold && NunitoBold != null ? NunitoBold : NunitoRegular != null ? NunitoRegular : Resources.GetBuiltinResource<Font>("Arial.ttf");
        go.AddComponent<LayoutElement>();
        return txt;
    }

    private static GameObject CreateRow(Transform parent, float spacing, TextAnchor childAlignment)
    {
        var row = CreateUIObject("Row", parent);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = childAlignment;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        var fitter = row.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return row;
    }

    private static Button CreateButton(string name, Transform parent, string text, DefaultControls.Resources resources)
    {
        var buttonGO = DefaultControls.CreateButton(resources);
        buttonGO.name = name;
        Undo.RegisterCreatedObjectUndo(buttonGO, "Create Settings Menu Button");
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(0f, 50f);
        var btnText = buttonGO.GetComponentInChildren<Text>();
        btnText.text = text;
        btnText.font = NunitoBold != null ? NunitoBold : btnText.font;
        var layout = buttonGO.AddComponent<LayoutElement>();
        layout.preferredHeight = 50f;
        layout.flexibleWidth = 1f;
        return buttonGO.GetComponent<Button>();
    }

    private static GameObject CreateSliderRow(Transform parent, string label, DefaultControls.Resources resources, out Slider slider)
    {
        var row = CreateRow(parent, spacing: 12f, childAlignment: TextAnchor.MiddleLeft);
        var labelText = CreateLabel(row.transform, label, 16, FontStyle.Normal);
        labelText.alignment = TextAnchor.MiddleLeft;
        var labelLayout = labelText.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 160f;
        labelLayout.flexibleWidth = 0f;

        var sliderGO = DefaultControls.CreateSlider(resources);
        sliderGO.name = label.Replace(" ", "") + "Slider";
        Undo.RegisterCreatedObjectUndo(sliderGO, "Create Settings Slider");
        var rect = sliderGO.GetComponent<RectTransform>();
        rect.SetParent(row.transform, false);
        rect.sizeDelta = new Vector2(0f, 30f);
        slider = sliderGO.GetComponent<Slider>();
        slider.wholeNumbers = false;
        var layout = sliderGO.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.preferredHeight = 30f;
        var sliderLabel = sliderGO.GetComponentInChildren<Text>();
        if (sliderLabel != null && NunitoRegular != null)
            sliderLabel.font = NunitoRegular;
        return row;
    }

    private static GameObject CreateToggleRow(Transform parent, string label, DefaultControls.Resources resources, out Toggle toggle)
    {
        var row = CreateRow(parent, spacing: 12f, childAlignment: TextAnchor.MiddleLeft);
        var toggleGO = DefaultControls.CreateToggle(resources);
        toggleGO.name = label.Replace(" ", "") + "Toggle";
        Undo.RegisterCreatedObjectUndo(toggleGO, "Create Settings Toggle");
        var rect = toggleGO.GetComponent<RectTransform>();
        rect.SetParent(row.transform, false);
        rect.sizeDelta = new Vector2(30f, 30f);
        toggle = toggleGO.GetComponent<Toggle>();
        var toggleLayout = toggleGO.AddComponent<LayoutElement>();
        toggleLayout.preferredWidth = 30f;
        toggleLayout.preferredHeight = 30f;
        toggleLayout.flexibleWidth = 0f;

        var labelText = CreateLabel(row.transform, label, 16, FontStyle.Normal);
        labelText.alignment = TextAnchor.MiddleLeft;
        var labelLayout = labelText.GetComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        var toggleText = toggleGO.GetComponentInChildren<Text>();
        if (toggleText != null && NunitoRegular != null) toggleText.font = NunitoRegular;
        return row;
    }
}
