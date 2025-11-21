using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Herramienta de editor para crear automáticamente la UI completa de una tienda.
/// Abre desde Tools > Create Shop UI
/// </summary>
public class ShopUICreator : EditorWindow
{
    private string shopName = "Shop";
    private ShopController shopController;

    [MenuItem("Tools/Create Shop UI")]
    public static void ShowWindow()
    {
        var window = GetWindow<ShopUICreator>("Shop UI Creator");
        window.minSize = new Vector2(400, 200);
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Crear UI de Tienda", EditorStyles.boldLabel);
        GUILayout.Space(10);

        shopName = EditorGUILayout.TextField("Nombre de la tienda:", shopName);
        shopController = (ShopController)EditorGUILayout.ObjectField("Shop Controller:", shopController, typeof(ShopController), true);

        GUILayout.Space(20);

        if (GUILayout.Button("Crear Shop UI Completa", GUILayout.Height(40)))
        {
            CreateShopUI();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("Esto creará:\n" +
            "• Canvas con ShopUI\n" +
            "• Window Root\n" +
            "• Item List (scroll view)\n" +
            "• Detail Panel\n" +
            "• Currency Display\n" +
            "• Prefab de ItemCard\n" +
            "• EventSystem si no existe", MessageType.Info);
    }

    void CreateShopUI()
    {
        // Crear Canvas principal
        GameObject canvasObj = new GameObject($"{shopName}_Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Window Root (panel oscuro de fondo)
        GameObject windowRoot = CreateUIObject("WindowRoot", canvasObj.transform);
        RectTransform windowRect = windowRoot.GetComponent<RectTransform>();
        windowRect.anchorMin = Vector2.zero;
        windowRect.anchorMax = Vector2.one;
        windowRect.sizeDelta = Vector2.zero;

        Image windowBg = windowRoot.AddComponent<Image>();
        windowBg.color = new Color(0, 0, 0, 0.8f);

        // Panel principal centrado
        GameObject mainPanel = CreateUIObject("MainPanel", windowRoot.transform);
        RectTransform mainRect = mainPanel.GetComponent<RectTransform>();
        mainRect.anchorMin = new Vector2(0.5f, 0.5f);
        mainRect.anchorMax = new Vector2(0.5f, 0.5f);
        mainRect.sizeDelta = new Vector2(1400, 800);
        mainRect.anchoredPosition = Vector2.zero;

        Image mainBg = mainPanel.AddComponent<Image>();
        mainBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Header con título y monedas
        GameObject header = CreateUIObject("Header", mainPanel.transform);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.sizeDelta = new Vector2(-40, 80);
        headerRect.anchoredPosition = new Vector2(0, -40);

        // Título
        GameObject titleObj = CreateText("Title", header.transform, $"Tienda - {shopName}", 32, TextAnchor.MiddleLeft);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 0);
        titleRect.anchorMax = new Vector2(0.6f, 1);
        titleRect.sizeDelta = Vector2.zero;
        titleRect.anchoredPosition = new Vector2(20, 0);

        // Currency Display
        GameObject currencyObj = CreateText("CurrencyText", header.transform, "💰 0", 28, TextAnchor.MiddleRight);
        RectTransform currencyRect = currencyObj.GetComponent<RectTransform>();
        currencyRect.anchorMin = new Vector2(0.6f, 0);
        currencyRect.anchorMax = new Vector2(0.9f, 1);
        currencyRect.sizeDelta = Vector2.zero;
        Text currencyText = currencyObj.GetComponent<Text>();
        currencyText.color = Color.yellow;

        // Close Button
        GameObject closeBtn = CreateButton("CloseButton", header.transform, "X");
        RectTransform closeBtnRect = closeBtn.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0.92f, 0.2f);
        closeBtnRect.anchorMax = new Vector2(0.98f, 0.8f);
        closeBtnRect.sizeDelta = Vector2.zero;
        Button closeBtnComp = closeBtn.GetComponent<Button>();
        ColorBlock closeColors = closeBtnComp.colors;
        closeColors.normalColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        closeBtnComp.colors = closeColors;

        // Content Area (izquierda: lista, derecha: detalles)
        GameObject content = CreateUIObject("Content", mainPanel.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.sizeDelta = new Vector2(-40, -140);
        contentRect.anchoredPosition = new Vector2(0, -70);

        // LEFT: Item List con Scroll View
        GameObject scrollView = CreateScrollView("ItemScrollView", content.transform);
        RectTransform scrollRect = scrollView.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(0.45f, 1);
        scrollRect.sizeDelta = Vector2.zero;
        scrollRect.anchoredPosition = Vector2.zero;

        Transform itemListContainer = scrollView.transform.Find("Viewport/Content");

        // RIGHT: Detail Panel
        GameObject detailPanel = CreateUIObject("DetailPanel", content.transform);
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0.47f, 0);
        detailRect.anchorMax = new Vector2(1, 1);
        detailRect.sizeDelta = Vector2.zero;

        Image detailBg = detailPanel.AddComponent<Image>();
        detailBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Detail: Icon
        GameObject detailIcon = CreateUIObject("DetailIcon", detailPanel.transform);
        RectTransform iconRect = detailIcon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 0.7f);
        iconRect.anchorMax = new Vector2(0.5f, 0.7f);
        iconRect.sizeDelta = new Vector2(150, 150);
        iconRect.anchoredPosition = new Vector2(0, 50);
        Image iconImg = detailIcon.AddComponent<Image>();
        iconImg.color = Color.white;

        // Detail: Name
        GameObject detailName = CreateText("DetailName", detailPanel.transform, "Nombre del Item", 28, TextAnchor.MiddleCenter);
        RectTransform nameRect = detailName.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.1f, 0.55f);
        nameRect.anchorMax = new Vector2(0.9f, 0.65f);
        nameRect.sizeDelta = Vector2.zero;
        detailName.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // Detail: Description
        GameObject detailDesc = CreateText("DetailDescription", detailPanel.transform, "Descripción del item...", 20, TextAnchor.UpperLeft);
        RectTransform descRect = detailDesc.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0.1f, 0.35f);
        descRect.anchorMax = new Vector2(0.9f, 0.52f);
        descRect.sizeDelta = Vector2.zero;

        // Detail: Price
        GameObject detailPrice = CreateText("DetailPrice", detailPanel.transform, "Precio: 0 💰", 24, TextAnchor.MiddleLeft);
        RectTransform priceRect = detailPrice.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0.1f, 0.27f);
        priceRect.anchorMax = new Vector2(0.9f, 0.32f);
        priceRect.sizeDelta = Vector2.zero;

        // Detail: Stock
        GameObject detailStock = CreateText("DetailStock", detailPanel.transform, "En stock", 20, TextAnchor.MiddleLeft);
        RectTransform stockRect = detailStock.GetComponent<RectTransform>();
        stockRect.anchorMin = new Vector2(0.1f, 0.21f);
        stockRect.anchorMax = new Vector2(0.9f, 0.26f);
        stockRect.sizeDelta = Vector2.zero;
        detailStock.GetComponent<Text>().color = Color.green;

        // Buy Button
        GameObject buyBtn = CreateButton("BuyButton", detailPanel.transform, "COMPRAR");
        RectTransform buyRect = buyBtn.GetComponent<RectTransform>();
        buyRect.anchorMin = new Vector2(0.1f, 0.08f);
        buyRect.anchorMax = new Vector2(0.9f, 0.16f);
        buyRect.sizeDelta = Vector2.zero;
        Button buyBtnComp = buyBtn.GetComponent<Button>();
        ColorBlock buyColors = buyBtnComp.colors;
        buyColors.normalColor = new Color(0.2f, 0.7f, 0.2f, 1f);
        buyBtnComp.colors = buyColors;

        // Sell Button (oculto por defecto)
        GameObject sellBtn = CreateButton("SellButton", detailPanel.transform, "VENDER");
        RectTransform sellRect = sellBtn.GetComponent<RectTransform>();
        sellRect.anchorMin = new Vector2(0.1f, 0.0f);
        sellRect.anchorMax = new Vector2(0.9f, 0.06f);
        sellRect.sizeDelta = Vector2.zero;

        // Message Text
        GameObject messageObj = CreateText("MessageText", detailPanel.transform, "", 18, TextAnchor.MiddleCenter);
        RectTransform msgRect = messageObj.GetComponent<RectTransform>();
        msgRect.anchorMin = new Vector2(0.1f, 0.0f);
        msgRect.anchorMax = new Vector2(0.9f, 0.05f);
        msgRect.sizeDelta = Vector2.zero;

        // Crear prefab de ItemCard
        GameObject itemCardPrefab = CreateItemCardPrefab();

        // Añadir componente ShopUI
        ShopUI shopUI = canvasObj.AddComponent<ShopUI>();
        
        // Asignar referencias usando reflexión (porque son SerializeField privados)
        var type = typeof(ShopUI);
        
        SetPrivateField(shopUI, "shopController", shopController);
        SetPrivateField(shopUI, "windowRoot", windowRoot);
        SetPrivateField(shopUI, "itemListContainer", itemListContainer);
        SetPrivateField(shopUI, "itemCardPrefab", itemCardPrefab);
        SetPrivateField(shopUI, "currencyText", currencyText);
        SetPrivateField(shopUI, "closeButton", closeBtnComp);
        SetPrivateField(shopUI, "detailPanel", detailPanel);
        SetPrivateField(shopUI, "detailIcon", iconImg);
        SetPrivateField(shopUI, "detailName", detailName.GetComponent<Text>());
        SetPrivateField(shopUI, "detailDescription", detailDesc.GetComponent<Text>());
        SetPrivateField(shopUI, "detailPrice", detailPrice.GetComponent<Text>());
        SetPrivateField(shopUI, "detailStock", detailStock.GetComponent<Text>());
        SetPrivateField(shopUI, "buyButton", buyBtnComp);
        SetPrivateField(shopUI, "sellButton", sellBtn.GetComponent<Button>());
        SetPrivateField(shopUI, "messageText", messageObj.GetComponent<Text>());

        // Crear EventSystem si no existe
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        // Guardar prefab
        string prefabPath = $"Assets/Prefabs/UI/{shopName}_ItemCard.prefab";
        System.IO.Directory.CreateDirectory("Assets/Prefabs/UI");
        PrefabUtility.SaveAsPrefabAsset(itemCardPrefab, prefabPath);
        DestroyImmediate(itemCardPrefab);

        // Recargar el prefab guardado
        GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        SetPrivateField(shopUI, "itemCardPrefab", savedPrefab);

        Selection.activeGameObject = canvasObj;
        EditorUtility.SetDirty(canvasObj);

        Debug.Log($"✅ Shop UI '{shopName}' creada exitosamente!");
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.AddComponent<RectTransform>();
        return obj;
    }

    GameObject CreateText(string name, Transform parent, string text, int fontSize, TextAnchor alignment)
    {
        GameObject obj = CreateUIObject(name, parent);
        Text textComp = obj.AddComponent<Text>();
        textComp.text = text;
        textComp.fontSize = fontSize;
        textComp.alignment = alignment;
        textComp.color = Color.white;
        textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return obj;
    }

    GameObject CreateButton(string name, Transform parent, string label)
    {
        GameObject obj = CreateUIObject(name, parent);
        Image img = obj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        Button btn = obj.AddComponent<Button>();

        GameObject textObj = CreateText("Text", obj.transform, label, 22, TextAnchor.MiddleCenter);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        return obj;
    }

    GameObject CreateScrollView(string name, Transform parent)
    {
        GameObject scrollView = CreateUIObject(name, parent);
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        ScrollRect scroll = scrollView.AddComponent<ScrollRect>();

        GameObject viewport = CreateUIObject("Viewport", scrollView.transform);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;

        GameObject content = CreateUIObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = viewportRect;
        scroll.vertical = true;
        scroll.horizontal = false;

        return scrollView;
    }

    GameObject CreateItemCardPrefab()
    {
        GameObject card = CreateUIObject("ItemCard", null);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(0, 100);

        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        Button cardBtn = card.AddComponent<Button>();

        LayoutElement layout = card.AddComponent<LayoutElement>();
        layout.preferredHeight = 100;

        // Icon
        GameObject icon = CreateUIObject("Icon", card.transform);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.sizeDelta = new Vector2(80, 80);
        iconRect.anchoredPosition = new Vector2(50, 0);
        Image iconImg = icon.AddComponent<Image>();
        iconImg.color = Color.white;

        // Name
        GameObject nameText = CreateText("NameText", card.transform, "Item Name", 20, TextAnchor.MiddleLeft);
        RectTransform nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.25f, 0.5f);
        nameRect.anchorMax = new Vector2(0.7f, 0.8f);
        nameRect.sizeDelta = Vector2.zero;

        // Price
        GameObject priceText = CreateText("PriceText", card.transform, "0 💰", 18, TextAnchor.MiddleLeft);
        RectTransform priceRect = priceText.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0.25f, 0.2f);
        priceRect.anchorMax = new Vector2(0.7f, 0.5f);
        priceRect.sizeDelta = Vector2.zero;
        priceText.GetComponent<Text>().color = Color.yellow;

        // Stock
        GameObject stockText = CreateText("StockText", card.transform, "Disponible", 16, TextAnchor.MiddleRight);
        RectTransform stockRect = stockText.GetComponent<RectTransform>();
        stockRect.anchorMin = new Vector2(0.7f, 0.2f);
        stockRect.anchorMax = new Vector2(0.95f, 0.5f);
        stockRect.sizeDelta = Vector2.zero;
        stockText.GetComponent<Text>().color = Color.green;

        // Añadir componente ShopItemCard
        ShopItemCard itemCard = card.AddComponent<ShopItemCard>();
        SetPrivateField(itemCard, "iconImage", iconImg);
        SetPrivateField(itemCard, "nameText", nameText.GetComponent<Text>());
        SetPrivateField(itemCard, "priceText", priceText.GetComponent<Text>());
        SetPrivateField(itemCard, "stockText", stockText.GetComponent<Text>());
        SetPrivateField(itemCard, "button", cardBtn);
        SetPrivateField(itemCard, "background", cardBg);

        return card;
    }

    void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, 
            System.Reflection.BindingFlags.NonPublic | 
            System.Reflection.BindingFlags.Instance);
        
        if (field != null)
            field.SetValue(obj, value);
    }
}
