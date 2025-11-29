using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class QuestMenuCreator
{
    [MenuItem("GameObject/UI/Crear Menus de Misiones", priority = 20)]
    public static void CreateQuestMenus(MenuCommand command)
    {
        var canvasGo = new GameObject("QuestMenusCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();

        var eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            var ev = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(ev, "Create EventSystem");
        }

        // Quick menu
        var quickRoot = new GameObject("QuickQuestMenu", typeof(RectTransform));
        quickRoot.transform.SetParent(canvasGo.transform, false);
        var quickLayout = quickRoot.AddComponent<VerticalLayoutGroup>();
        quickLayout.childForceExpandHeight = false;
        quickLayout.childForceExpandWidth = true;
        quickLayout.padding = new RectOffset(12, 12, 12, 12);
        quickLayout.spacing = 8;

        var quickCanvasGroup = quickRoot.AddComponent<CanvasGroup>();
        var quickRect = quickRoot.GetComponent<RectTransform>();
        quickRect.anchorMin = new Vector2(0f, 1f);
        quickRect.anchorMax = new Vector2(0f, 1f);
        quickRect.pivot = new Vector2(0f, 1f);
        quickRect.anchoredPosition = new Vector2(32f, -32f);
        quickRect.sizeDelta = new Vector2(520f, 620f);

        var headerObj = CreateText("Header", quickRoot.transform, "Misiones", 28, FontStyles.Bold);
        var helpObj = CreateText("Help", quickRoot.transform, "[D-Pad ▲] Mostrar", 18, FontStyles.Italic);

        var scrollGo = CreateScroll(quickRoot.transform, out var contentRoot);

        var questList = quickRoot.AddComponent<QuestLogListUI>();
        questList.GetType().GetField("panelRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, quickRoot);
        questList.GetType().GetField("scrollView", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, scrollGo);
        questList.GetType().GetField("panelGroup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, quickCanvasGroup);
        questList.GetType().GetField("animatedRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, quickRect);
        questList.GetType().GetField("contentRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, contentRoot);
        questList.GetType().GetField("headerText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, headerObj.GetComponent<TextMeshProUGUI>());
        questList.GetType().GetField("helpText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, helpObj.GetComponent<TextMeshProUGUI>());

        var questItem = AssetDatabase.LoadAssetAtPath<QuestLogItemUI>("Assets/Prefabs/QuestsUI/QuestLogItem.prefab");
        if (questItem != null)
        {
            questList.GetType().GetField("itemPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(questList, questItem);
        }

        // Main menu
        var mainRoot = new GameObject("QuestMainMenu", typeof(RectTransform));
        mainRoot.transform.SetParent(canvasGo.transform, false);
        var mainGroup = mainRoot.AddComponent<CanvasGroup>();
        var mainRect = mainRoot.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.pivot = new Vector2(0.5f, 0.5f);
        mainRect.sizeDelta = new Vector2(760f, 640f);
        mainRect.anchoredPosition = Vector2.zero;
        mainGroup.alpha = 0f;
        mainGroup.blocksRaycasts = false;
        mainGroup.interactable = false;
        mainRoot.SetActive(false);

        var mainLayout = mainRoot.AddComponent<VerticalLayoutGroup>();
        mainLayout.padding = new RectOffset(24, 24, 24, 24);
        mainLayout.spacing = 12f;

        var mainHeader = CreateText("MainHeader", mainRoot.transform, "Misiones (principal)", 30, FontStyles.Bold);
        var mainScroll = CreateScroll(mainRoot.transform, out var mainContent);

        var visibleHeader = CreateText("VisibleHeader", mainContent, "Misiones visibles", 24, FontStyles.Bold);
        var visibleContent = new GameObject("VisibleContent", typeof(RectTransform));
        visibleContent.transform.SetParent(mainContent, false);
        var visibleLayout = visibleContent.AddComponent<VerticalLayoutGroup>();
        visibleLayout.childForceExpandHeight = false;
        visibleLayout.childForceExpandWidth = true;
        visibleLayout.spacing = 8f;
        visibleContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var hiddenHeader = CreateText("HiddenHeader", mainContent, "Misiones ocultas", 24, FontStyles.Bold);
        var hiddenContent = new GameObject("HiddenContent", typeof(RectTransform));
        hiddenContent.transform.SetParent(mainContent, false);
        var hiddenLayout = hiddenContent.AddComponent<VerticalLayoutGroup>();
        hiddenLayout.childForceExpandHeight = false;
        hiddenLayout.childForceExpandWidth = true;
        hiddenLayout.spacing = 8f;
        hiddenContent.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var mainMenu = mainRoot.AddComponent<QuestMainMenuUI>();
        mainMenu.GetType().GetField("panelRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, mainRoot);
        mainMenu.GetType().GetField("panelGroup", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, mainGroup);
        mainMenu.GetType().GetField("contentRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, mainContent);
        mainMenu.GetType().GetField("visibleContentRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, visibleContent.transform);
        mainMenu.GetType().GetField("hiddenContentRoot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, hiddenContent.transform);
        mainMenu.GetType().GetField("headerText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, mainHeader.GetComponent<TextMeshProUGUI>());
        mainMenu.GetType().GetField("visibleHeaderText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, visibleHeader.GetComponent<TextMeshProUGUI>());
        mainMenu.GetType().GetField("hiddenHeaderText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mainMenu, hiddenHeader.GetComponent<TextMeshProUGUI>());

        questList.GetType()
            .GetField("mainMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(questList, mainMenu);

        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Quest Menus");
        Selection.activeObject = canvasGo;
    }

    static GameObject CreateText(string name, Transform parent, string text, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        return go;
    }

    static GameObject CreateScroll(Transform parent, out Transform content)
    {
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform));
        scrollGo.transform.SetParent(parent, false);
        var image = scrollGo.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.3f);
        var mask = scrollGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpMask = viewport.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1f, 1f, 1f, 0.05f);

        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewport.transform, false);
        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.spacing = 8f;
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var rect = scrollGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(520f, 520f);

        var vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;

        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        scroll.viewport = vpRect;
        scroll.content = contentRect;

        content = contentGo.transform;
        return scrollGo;
    }
}
