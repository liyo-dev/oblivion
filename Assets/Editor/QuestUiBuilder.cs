// Assets/Editor/QuestUiBuilder.cs
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public static class QuestUiBuilder
{
    const string RootFolder = "Assets/QuestsUI";

    [MenuItem("Tools/Quests/Create Quest Log UI")]
    public static void CreateQuestLogUI()
    {
        EnsureFolder(RootFolder);

        // 1) Buscar o crear Canvas destino
        var canvas = GameObject.Find("CanvasHUD") ?? Object.FindFirstObjectByType<Canvas>()?.gameObject;
        if (canvas == null)
        {
            canvas = CreateCanvas();
        }

        // 2) Crear panel principal con mejor diseño
        var panel = new GameObject("QuestLogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create QuestLogPanel");
        panel.transform.SetParent(canvas.transform, false);

        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(1, 1);
        panelRT.anchorMax = new Vector2(1, 1);
        panelRT.pivot = new Vector2(1, 1);
        panelRT.sizeDelta = new Vector2(450, 600);
        panelRT.anchoredPosition = new Vector2(-30, -30);
        
        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.12f, 0f); // Transparente para que no se vea al ocultar
        bg.raycastTarget = false;

        // 3) Barra de título con degradado
        var titleBar = new GameObject("TitleBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        titleBar.transform.SetParent(panel.transform, false);
        var titleBarRT = titleBar.GetComponent<RectTransform>();
        titleBarRT.anchorMin = new Vector2(0, 1);
        titleBarRT.anchorMax = new Vector2(1, 1);
        titleBarRT.pivot = new Vector2(0.5f, 1);
        titleBarRT.sizeDelta = new Vector2(0, 60);
        titleBarRT.anchoredPosition = Vector2.zero;
        var titleBarImg = titleBar.GetComponent<Image>();
        titleBarImg.color = new Color(0.15f, 0.25f, 0.45f, 1f); // Azul oscuro

        // Icono de misiones
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        icon.transform.SetParent(titleBar.transform, false);
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0, 0.5f);
        iconRT.anchorMax = new Vector2(0, 0.5f);
        iconRT.pivot = new Vector2(0, 0.5f);
        iconRT.anchoredPosition = new Vector2(20, 0);
        iconRT.sizeDelta = new Vector2(40, 40);
        var iconTMP = icon.GetComponent<TextMeshProUGUI>();
        iconTMP.text = "📋";
        iconTMP.fontSize = 32;
        iconTMP.alignment = TextAlignmentOptions.Center;
        iconTMP.raycastTarget = false;

        // Título mejorado
        var title = CreateTMP(titleBar.transform, "MISIONES", 28, TextAlignmentOptions.MidlineLeft, out var _);
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(1f, 1f, 1f, 1f);
        var titleRT2 = title.GetComponent<RectTransform>();
        titleRT2.anchorMin = new Vector2(0, 0.5f);
        titleRT2.anchorMax = new Vector2(1, 0.5f);
        titleRT2.pivot = new Vector2(0, 0.5f);
        titleRT2.anchoredPosition = new Vector2(70, 0);
        titleRT2.sizeDelta = new Vector2(-80, 40);

        // Indicador de ayuda
        var helpText = CreateTMP(titleBar.transform, "[D-Pad ▲] Ocultar", 14, TextAlignmentOptions.MidlineRight, out var _);
        helpText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        var helpRT = helpText.GetComponent<RectTransform>();
        helpRT.anchorMin = new Vector2(1, 0.5f);
        helpRT.anchorMax = new Vector2(1, 0.5f);
        helpRT.pivot = new Vector2(1, 0.5f);
        helpRT.anchoredPosition = new Vector2(-20, 0);
        helpRT.sizeDelta = new Vector2(150, 30);

        // 4) ScrollView mejorado
        var scroll = CreateScrollView(panel.transform, out var contentRT);
        var scrollRT = scroll.GetComponent<RectTransform>();
        scrollRT.offsetMin = new Vector2(15, 15);
        scrollRT.offsetMax = new Vector2(-15, -75);
        
        // Añadir fondo oscuro al ScrollView
        var scrollImg = scroll.GetComponent<Image>();
        scrollImg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f); // Fondo oscuro aquí

        // Configurar Content (layout vertical)
        var vlg = contentRT.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var csf = contentRT.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 5) Crear/guardar prefabs
        var itemGO = BuildQuestItemPrefab(out var itemUI);
        var itemPath = Path.Combine(RootFolder, "QuestLogItem.prefab").Replace("\\", "/");
        var itemPrefab = PrefabUtility.SaveAsPrefabAsset(itemGO, itemPath);
        Object.DestroyImmediate(itemGO);

        var stepGO = BuildStepItemPrefab();
        var stepPath = Path.Combine(RootFolder, "QuestStepItem.prefab").Replace("\\", "/");
        var stepPrefab = PrefabUtility.SaveAsPrefabAsset(stepGO, stepPath);
        Object.DestroyImmediate(stepGO);

        // Conectar stepPrefab en el item (aunque la fila es compacta, queda listo si expandes en el futuro)
        var loadedItem  = PrefabUtility.LoadPrefabContents(itemPath);
        var loadedItemUI = loadedItem.GetComponent<QuestLogItemUI>();
        if (loadedItemUI != null)
        {
            var soItem = new SerializedObject(loadedItemUI);

            // stepsRoot
            var stepsTr = loadedItem.transform.Find("StepsContainer");
            soItem.FindProperty("stepsRoot").objectReferenceValue = stepsTr ? stepsTr : null;

            // stepPrefab
            var stepUIPref = AssetDatabase.LoadAssetAtPath<QuestStepItemUI>(stepPath);
            soItem.FindProperty("stepPrefab").objectReferenceValue = stepUIPref;

            soItem.ApplyModifiedPropertiesWithoutUndo();
        }
        PrefabUtility.SaveAsPrefabAsset(loadedItem, itemPath);
        PrefabUtility.UnloadPrefabContents(loadedItem);


        // 6) Añadir QuestLogListUI al panel y cablear
        var list = panel.AddComponent<QuestLogListUI>();
        list.GetType().GetField("showInactive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(list, true); // visible en debug

        // Asignar referencias via SerializedObject (para campos privados serializados)
        var so = new SerializedObject(list);
        so.FindProperty("contentRoot").objectReferenceValue = contentRT;
        so.FindProperty("itemPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<QuestLogItemUI>(itemPath);
        so.FindProperty("headerText").objectReferenceValue = title;
        so.FindProperty("panelRoot").objectReferenceValue = panel; // Para control con mando
        so.FindProperty("scrollView").objectReferenceValue = scroll; // Para ocultar solo el contenido
        so.FindProperty("helpText").objectReferenceValue = helpText; // Para cambiar el texto de ayuda
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = panel;
        EditorGUIUtility.PingObject(panel);
        Debug.Log("Quest Log UI creada y cableada ✔");
    }

    // ---------- helpers ----------
    static GameObject CreateCanvas()
    {
        var go = new GameObject("CanvasHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        Undo.RegisterCreatedObjectUndo(go, "Create CanvasHUD");
        return go;
    }

    static TextMeshProUGUI CreateTMP(Transform parent, string text, int size, TextAlignmentOptions align, out GameObject go)
    {
        go = new GameObject("TMP", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 40);
        return tmp;
    }

    static GameObject CreateScrollView(Transform parent, out RectTransform contentRT)
    {
        // ScrollView base
        var scroll = new GameObject("Scroll View", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scroll.transform.SetParent(parent, false);
        var img = scroll.GetComponent<Image>(); img.color = new Color(1,1,1,0.06f);
        scroll.GetComponent<Mask>().showMaskGraphic = false;

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Mask), typeof(Image));
        viewport.transform.SetParent(scroll.transform, false);
        var vpImg = viewport.GetComponent<Image>(); vpImg.color = new Color(1,1,1,0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        var srect = scroll.GetComponent<ScrollRect>();
        srect.viewport = viewport.GetComponent<RectTransform>();
        srect.content = content.GetComponent<RectTransform>();
        srect.horizontal = false;
        srect.vertical = true;

        // anchors
        var srt = scroll.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0,0);
        srt.anchorMax = new Vector2(1,1);
        srt.offsetMin = new Vector2(0,0);
        srt.offsetMax = new Vector2(0,0);

        var vprt = viewport.GetComponent<RectTransform>();
        vprt.anchorMin = new Vector2(0,0);
        vprt.anchorMax = new Vector2(1,1);
        vprt.offsetMin = new Vector2(0,0);
        vprt.offsetMax = new Vector2(0,0);

        contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0,1);
        contentRT.anchorMax = new Vector2(1,1);
        contentRT.pivot = new Vector2(0.5f,1);

        return scroll;
    }
    
    // REEMPLAZA COMPLETO este método en QuestUiBuilder.cs
static GameObject BuildQuestItemPrefab(out QuestLogItemUI itemUI)
{
    // --- ROOT con borde y sombra ---------------------------------------------------------
    var root = new GameObject("QuestLogItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Shadow));
    var rt = root.GetComponent<RectTransform>();
    rt.sizeDelta = new Vector2(420, 100); // ALTURA 100px como pediste

    var bg = root.GetComponent<Image>();
    if (bg) 
    { 
        bg.color = new Color(0.15f, 0.18f, 0.25f, 0.95f);
        bg.raycastTarget = true; 
    }

    var shadow = root.GetComponent<Shadow>();
    shadow.effectColor = new Color(0, 0, 0, 0.5f);
    shadow.effectDistance = new Vector2(2, -2);

    // Borde izquierdo de color
    var leftBorder = new GameObject("LeftBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
    leftBorder.transform.SetParent(root.transform, false);
    var lbRT = leftBorder.GetComponent<RectTransform>();
    lbRT.anchorMin = new Vector2(0, 0);
    lbRT.anchorMax = new Vector2(0, 1);
    lbRT.pivot = new Vector2(0, 0.5f);
    lbRT.sizeDelta = new Vector2(4, 0);
    lbRT.anchoredPosition = Vector2.zero;
    var lbImg = leftBorder.GetComponent<Image>();
    lbImg.color = new Color(0.2f, 0.6f, 1f, 1f);
    lbImg.raycastTarget = false;

    // Layout principal horizontal para: (Pill + Contenido + Progreso)
    var mainRow = new GameObject("MainRow", typeof(RectTransform), typeof(CanvasRenderer));
    mainRow.transform.SetParent(root.transform, false);
    var mainHL = mainRow.AddComponent<HorizontalLayoutGroup>();
    mainHL.padding = new RectOffset(15, 15, 15, 15); // Padding uniforme
    mainHL.spacing = 12;
    mainHL.childAlignment = TextAnchor.MiddleLeft; // CENTRADO VERTICAL
    mainHL.childControlWidth = false;
    mainHL.childControlHeight = false;
    mainHL.childForceExpandWidth = false;
    mainHL.childForceExpandHeight = false;
    
    var mainRowRT = mainRow.GetComponent<RectTransform>();
    mainRowRT.anchorMin = Vector2.zero;
    mainRowRT.anchorMax = Vector2.one;
    mainRowRT.offsetMin = Vector2.zero;
    mainRowRT.offsetMax = Vector2.zero;

    // StatePill (izquierda)
    var pill = new GameObject("StatePill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
    pill.transform.SetParent(mainRow.transform, false);
    var pillImg = pill.GetComponent<Image>(); 
    if (pillImg) pillImg.color = new Color(0.25f, 0.65f, 1f, 1f);
    var pillLE = pill.GetComponent<LayoutElement>(); 
    pillLE.preferredWidth = 75; 
    pillLE.preferredHeight = 32;
    pillLE.minWidth = 75;

    TextMeshProUGUI pillText = null;
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(pill.transform, false);
        var rtTxt = go.GetComponent<RectTransform>();
        rtTxt.anchorMin = Vector2.zero;
        rtTxt.anchorMax = Vector2.one;
        rtTxt.offsetMin = Vector2.zero;
        rtTxt.offsetMax = Vector2.zero;
        pillText = go.GetComponent<TextMeshProUGUI>();
        pillText.text = "Activa";
        pillText.fontSize = 15;
        pillText.fontStyle = FontStyles.Bold;
        pillText.alignment = TextAlignmentOptions.Center;
        pillText.raycastTarget = false;
        pillText.color = Color.white;
    }

    // Contenedor vertical para Título + Descripción - WIDTH FIJO 230px
    var contentColumn = new GameObject("ContentColumn", typeof(RectTransform), typeof(CanvasRenderer));
    contentColumn.transform.SetParent(mainRow.transform, false);
    var contentVLG = contentColumn.AddComponent<VerticalLayoutGroup>();
    contentVLG.spacing = 4;
    contentVLG.childAlignment = TextAnchor.UpperLeft;
    contentVLG.childControlWidth = false;
    contentVLG.childControlHeight = false;
    contentVLG.childForceExpandWidth = false;
    contentVLG.childForceExpandHeight = false;
    
    var contentLE = contentColumn.AddComponent<LayoutElement>();
    contentLE.preferredWidth = 230; // WIDTH FIJO 230px
    contentLE.minWidth = 230;
    contentLE.preferredHeight = 70; // Altura suficiente para título + descripción

    // Título de la misión
    TextMeshProUGUI qnTMP = null;
    {
        var qnGO = new GameObject("QuestName", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        qnGO.transform.SetParent(contentColumn.transform, false);
        var qnRT = qnGO.GetComponent<RectTransform>();
        qnRT.sizeDelta = new Vector2(230, 25);
        qnTMP = qnGO.GetComponent<TextMeshProUGUI>();
        qnTMP.text = "La carta de Eldran";
        qnTMP.fontSize = 18;
        qnTMP.fontStyle = FontStyles.Bold;
        qnTMP.alignment = TextAlignmentOptions.TopLeft;
        qnTMP.raycastTarget = false;
        qnTMP.color = new Color(0.95f, 0.95f, 1f, 1f);
        qnTMP.overflowMode = TextOverflowModes.Ellipsis;
    }

    // Descripción del primer paso - SIEMPRE VISIBLE
    TextMeshProUGUI firstStepTMP = null;
    {
        var fsGO = new GameObject("FirstStepDesc", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        fsGO.transform.SetParent(contentColumn.transform, false);
        var fsRT = fsGO.GetComponent<RectTransform>();
        fsRT.sizeDelta = new Vector2(230, 20);
        firstStepTMP = fsGO.GetComponent<TextMeshProUGUI>();
        firstStepTMP.text = "Habla con Eldran";
        firstStepTMP.fontSize = 14;
        firstStepTMP.alignment = TextAlignmentOptions.TopLeft;
        firstStepTMP.raycastTarget = false;
        firstStepTMP.color = new Color(0.65f, 0.7f, 0.8f, 1f);
        firstStepTMP.overflowMode = TextOverflowModes.Ellipsis;
    }

    // SPACER FLEXIBLE - empuja el progreso a la derecha
    var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
    spacer.transform.SetParent(mainRow.transform, false);
    var spacerLE = spacer.GetComponent<LayoutElement>();
    spacerLE.flexibleWidth = 1; // Se expande para llenar el espacio disponible

    // Contador de progreso (derecha)
    var progContainer = new GameObject("ProgressContainer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
    progContainer.transform.SetParent(mainRow.transform, false);
    var progContImg = progContainer.GetComponent<Image>();
    progContImg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
    var progContLE = progContainer.GetComponent<LayoutElement>();
    progContLE.preferredWidth = 70;
    progContLE.preferredHeight = 40;
    progContLE.minWidth = 70;

    TextMeshProUGUI progTMP = null;
    {
        var progGO = new GameObject("ProgressText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        progGO.transform.SetParent(progContainer.transform, false);
        var progRT = progGO.GetComponent<RectTransform>();
        progRT.anchorMin = Vector2.zero;
        progRT.anchorMax = Vector2.one;
        progRT.offsetMin = Vector2.zero;
        progRT.offsetMax = Vector2.zero;
        
        progTMP = progGO.GetComponent<TextMeshProUGUI>();
        progTMP.text = "0/1";
        progTMP.fontSize = 22;
        progTMP.fontStyle = FontStyles.Bold;
        progTMP.alignment = TextAlignmentOptions.Center;
        progTMP.raycastTarget = false;
        progTMP.color = new Color(0.4f, 0.85f, 1f, 1f);
    }

    // ProgressBar oculta (compatibilidad con binding)
    var progressBar = new GameObject("ProgressBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
    progressBar.transform.SetParent(root.transform, false);
    progressBar.SetActive(false);
    var pbg = progressBar.GetComponent<Image>(); 
    if (pbg) { pbg.color = new Color(0, 0, 0, 0.15f); pbg.raycastTarget = false; }
    var pbrt = progressBar.GetComponent<RectTransform>(); 
    pbrt.sizeDelta = new Vector2(0, 6);

    var fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
    fill.transform.SetParent(progressBar.transform, false);
    var fillImg = fill.GetComponent<Image>();
    if (fillImg)
    {
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 0f;
        fillImg.color = new Color(0.3f, 0.85f, 0.5f, 1f);
        fillImg.raycastTarget = false;
    }
    var frt = fill.GetComponent<RectTransform>();
    frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; 
    frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

    // Componente runtime
    itemUI = root.AddComponent<QuestLogItemUI>();
    if (itemUI == null)
    {
        Debug.LogError("No se pudo añadir QuestLogItemUI. ¿Compila el script?");
        return root;
    }

    var so = new SerializedObject(itemUI);
    so.FindProperty("questName").objectReferenceValue = qnTMP;
    so.FindProperty("firstStepDesc").objectReferenceValue = firstStepTMP; // NUEVO: conectar descripción del paso
    so.FindProperty("statePillText").objectReferenceValue = pillText;
    so.FindProperty("statePillBg").objectReferenceValue = pillImg;
    so.FindProperty("progressText").objectReferenceValue = progTMP;
    so.FindProperty("progressFill").objectReferenceValue = fillImg;
    so.FindProperty("stepsRoot").objectReferenceValue = null; // No hay StepsContainer
    so.FindProperty("stepsContainer").objectReferenceValue = null; // Eliminado
    so.ApplyModifiedPropertiesWithoutUndo();

    return root;
}

    static GameObject BuildStepItemPrefab()
    {
        var root = new GameObject("QuestStepItem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rootRT = root.GetComponent<RectTransform>();
        
        // Fondo sutil para cada paso
        var rootImg = root.GetComponent<Image>();
        rootImg.color = new Color(0.12f, 0.15f, 0.2f, 0.5f);
        rootImg.raycastTarget = false;
        
        var hl = root.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(10, 10, 8, 8);
        hl.spacing = 10;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = false;
        hl.childControlHeight = false;
        hl.childForceExpandWidth = false;
        hl.childForceExpandHeight = false;
        
        var csf = root.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        
        var rootLE = root.AddComponent<LayoutElement>();
        rootLE.minHeight = 32;

        // Icono con fondo circular
        var iconContainer = new GameObject("IconContainer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconContainer.transform.SetParent(root.transform, false);
        var iconContImg = iconContainer.GetComponent<Image>();
        iconContImg.color = new Color(0.2f, 0.25f, 0.35f, 0.8f); // Fondo del círculo
        iconContImg.raycastTarget = false;
        var iconContLE = iconContainer.GetComponent<LayoutElement>();
        iconContLE.preferredWidth = 24;
        iconContLE.preferredHeight = 24;
        iconContLE.minWidth = 24;
        iconContLE.minHeight = 24;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(iconContainer.transform, false);
        var iconRT = icon.GetComponent<RectTransform>();
        iconRT.anchorMin = Vector2.zero;
        iconRT.anchorMax = Vector2.one;
        iconRT.offsetMin = new Vector2(4, 4);
        iconRT.offsetMax = new Vector2(-4, -4);
        var iconImg = icon.GetComponent<Image>();
        iconImg.raycastTarget = false;
        iconImg.color = new Color(0.6f, 0.8f, 1f, 1f); // Color por defecto

        // Label con mejor tipografía
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(root.transform, false);
        var label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "Descripción del paso";
        label.fontSize = 17;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.color = new Color(0.85f, 0.88f, 0.95f, 1f); // Color de texto claro
        label.textWrappingMode = TextWrappingModes.Normal;
         
         var labelLE = labelGO.AddComponent<LayoutElement>();
         labelLE.flexibleWidth = 1;
         labelLE.minWidth = 150;

        var ui = root.AddComponent<QuestStepItemUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("icon").objectReferenceValue = iconImg;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
