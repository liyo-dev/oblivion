using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Editor
{
    // Editor utility para generar en escena la jerarquía del HUD que usa PlayerHUDComplete
    public static class CreatePlayerHUDEditor
    {
        [MenuItem("Tools/Create Player HUD (PlayerHUDComplete)")]
        public static void CreatePlayerHUD()
        {
            // Buscar o crear Canvas
            GameObject canvasGO = GameObject.Find("PlayerHUD_Canvas");
            Canvas canvas;
            if (canvasGO == null)
            {
                canvasGO = new GameObject("PlayerHUD_Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
                canvasGO.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create PlayerHUD_Canvas");
            }
            else
            {
                canvas = canvasGO.GetComponent<Canvas>();
                if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
            }

            // Evitar crear duplicados
            var existingMain = GameObject.Find("PlayerHUD_Main");
            if (existingMain != null)
            {
                if (!EditorUtility.DisplayDialog("Player HUD ya existe",
                    "Ya existe un objeto llamado 'PlayerHUD_Main' en la escena. Seleccionar existente o crear otro? (Si eliges 'Cancelar' la operación se aborta)",
                    "Seleccionar existente", "Cancelar"))
                {
                    Selection.activeGameObject = existingMain;
                    EditorGUIUtility.PingObject(existingMain);
                    return;
                }
                else
                {
                    Selection.activeGameObject = existingMain;
                    EditorGUIUtility.PingObject(existingMain);
                    return;
                }
            }

            // Crear la jerarquía del HUD
            var mainPanel = new GameObject("PlayerHUD_Main");
            Undo.RegisterCreatedObjectUndo(mainPanel, "Create PlayerHUD_Main");
            mainPanel.transform.SetParent(canvasGO.transform, false);
            var mainRect = mainPanel.AddComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0, 0);
            mainRect.anchorMax = new Vector2(0, 0);
            mainRect.pivot = new Vector2(0, 0);
            mainRect.anchoredPosition = new Vector2(30, 30);
            mainRect.sizeDelta = new Vector2(260, 180);
            var mainImg = mainPanel.AddComponent<Image>();
            mainImg.color = new Color(0f, 0f, 0f, 0.25f);

            // Border
            var borderGO = new GameObject("MainBorder");
            Undo.RegisterCreatedObjectUndo(borderGO, "Create MainBorder");
            borderGO.transform.SetParent(mainPanel.transform, false);
            var borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2, -2);
            borderRect.offsetMax = new Vector2(2, 2);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            // Vertical Container
            var verticalContainer = new GameObject("VerticalContainer");
            Undo.RegisterCreatedObjectUndo(verticalContainer, "Create VerticalContainer");
            verticalContainer.transform.SetParent(mainPanel.transform, false);
            var containerRect = verticalContainer.AddComponent<RectTransform>();
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.offsetMin = new Vector2(12, 12);
            containerRect.offsetMax = new Vector2(-12, -12);
            verticalContainer.AddComponent<VerticalLayoutGroup>();

            // HealthContainer + HealthSlider
            var healthContainer = new GameObject("HealthContainer");
            Undo.RegisterCreatedObjectUndo(healthContainer, "Create HealthContainer");
            healthContainer.transform.SetParent(verticalContainer.transform, false);
            var healthRect = healthContainer.AddComponent<RectTransform>();
            healthRect.sizeDelta = new Vector2(0, 38);

            var healthSliderGO = new GameObject("HealthSlider");
            Undo.RegisterCreatedObjectUndo(healthSliderGO, "Create HealthSlider");
            healthSliderGO.transform.SetParent(healthContainer.transform, false);
            var hsRect = healthSliderGO.AddComponent<RectTransform>();
            hsRect.anchorMin = Vector2.zero; hsRect.anchorMax = Vector2.one; hsRect.offsetMin = Vector2.zero; hsRect.offsetMax = Vector2.zero;
            var slider = healthSliderGO.AddComponent<Slider>();
            slider.interactable = false;
            slider.direction = Slider.Direction.LeftToRight;

            var bg = new GameObject("Background"); bg.transform.SetParent(healthSliderGO.transform, false);
            var bgRect = bg.AddComponent<RectTransform>(); bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
            var bgImg = bg.AddComponent<Image>(); bgImg.color = new Color(0.12f,0.12f,0.12f,0.95f);
            slider.targetGraphic = bgImg;

            var fillArea = new GameObject("Fill Area"); fillArea.transform.SetParent(healthSliderGO.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>(); faRect.anchorMin = Vector2.zero; faRect.anchorMax = Vector2.one; faRect.offsetMin = new Vector2(2,2); faRect.offsetMax = new Vector2(-2,-2);
            var fill = new GameObject("Fill"); fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>(); fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.offsetMin = Vector2.zero; fillRect.offsetMax = Vector2.zero;
            var fillImg = fill.AddComponent<Image>(); fillImg.color = new Color(0.2f,0.8f,0.2f,1f);
            slider.fillRect = fillRect;

            var healthTextGO = new GameObject("HealthText"); healthTextGO.transform.SetParent(healthSliderGO.transform, false);
            var htRect = healthTextGO.AddComponent<RectTransform>(); htRect.anchorMin = Vector2.zero; htRect.anchorMax = Vector2.one; htRect.offsetMin = Vector2.zero; htRect.offsetMax = Vector2.zero;
            if (healthTextGO.GetComponent<CanvasRenderer>() == null) healthTextGO.AddComponent<CanvasRenderer>();
            var ht = healthTextGO.AddComponent<TextMeshProUGUI>(); ht.text = "100 / 100"; ht.fontSize = 16; ht.alignment = TextAlignmentOptions.Center;

            // MP
            var manaContainer = new GameObject("ManaContainer"); Undo.RegisterCreatedObjectUndo(manaContainer, "Create ManaContainer"); manaContainer.transform.SetParent(verticalContainer.transform, false);
            var manaRect = manaContainer.AddComponent<RectTransform>(); manaRect.sizeDelta = new Vector2(0, 38);

            var manaSliderGO = new GameObject("ManaSlider"); Undo.RegisterCreatedObjectUndo(manaSliderGO, "Create ManaSlider"); manaSliderGO.transform.SetParent(manaContainer.transform, false);
            var msRect = manaSliderGO.AddComponent<RectTransform>(); msRect.anchorMin = Vector2.zero; msRect.anchorMax = Vector2.one; msRect.offsetMin = Vector2.zero; msRect.offsetMax = Vector2.zero;
            var mslider = manaSliderGO.AddComponent<Slider>(); mslider.interactable = false; mslider.direction = Slider.Direction.LeftToRight;

            var mbg = new GameObject("Background"); mbg.transform.SetParent(manaSliderGO.transform, false); var mbgRect = mbg.AddComponent<RectTransform>(); mbgRect.anchorMin = Vector2.zero; mbgRect.anchorMax = Vector2.one; mbgRect.offsetMin = Vector2.zero; mbgRect.offsetMax = Vector2.zero; var mbgImg = mbg.AddComponent<Image>(); mbgImg.color = new Color(0.12f,0.12f,0.12f,0.95f); mslider.targetGraphic = mbgImg;
            var mfillArea = new GameObject("Fill Area"); mfillArea.transform.SetParent(manaSliderGO.transform, false); var mfaRect = mfillArea.AddComponent<RectTransform>(); mfaRect.anchorMin = Vector2.zero; mfaRect.anchorMax = Vector2.one; mfaRect.offsetMin = new Vector2(2,2); mfaRect.offsetMax = new Vector2(-2,-2);
            var mfill = new GameObject("Fill"); mfill.transform.SetParent(mfillArea.transform, false); var mfillRect = mfill.AddComponent<RectTransform>(); mfillRect.anchorMin = Vector2.zero; mfillRect.anchorMax = Vector2.one; mfillRect.offsetMin = Vector2.zero; mfillRect.offsetMax = Vector2.zero; var mfillImg = mfill.AddComponent<Image>(); mfillImg.color = new Color(0.2f,0.4f,0.9f,1f); mslider.fillRect = mfillRect;
            var manaTextGO = new GameObject("ManaText"); manaTextGO.transform.SetParent(manaSliderGO.transform, false); var mtRect = manaTextGO.AddComponent<RectTransform>(); mtRect.anchorMin = Vector2.zero; mtRect.anchorMax = Vector2.one; mtRect.offsetMin = Vector2.zero; mtRect.offsetMax = Vector2.zero; if (manaTextGO.GetComponent<CanvasRenderer>() == null) manaTextGO.AddComponent<CanvasRenderer>(); var mt = manaTextGO.AddComponent<TextMeshProUGUI>(); mt.text = "50 / 50"; mt.fontSize = 16; mt.alignment = TextAlignmentOptions.Center;

            // Slots row
            var slotsRow = new GameObject("SlotsRow"); Undo.RegisterCreatedObjectUndo(slotsRow, "Create SlotsRow"); slotsRow.transform.SetParent(verticalContainer.transform, false); var slotsRect = slotsRow.AddComponent<RectTransform>(); slotsRect.sizeDelta = new Vector2(0,60); var h = slotsRow.AddComponent<HorizontalLayoutGroup>(); h.spacing = 8;

            // Create three slots helper
            System.Action<string> CreateSlot = (name) =>
            {
                var slot = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(slot, "Create Slot");
                slot.transform.SetParent(slotsRow.transform, false);
                var sRect = slot.AddComponent<RectTransform>(); sRect.sizeDelta = new Vector2(50,50);
                slot.AddComponent<LayoutElement>().preferredWidth = 50;
                var border = new GameObject("Border"); border.transform.SetParent(slot.transform, false); var bRect = border.AddComponent<RectTransform>(); bRect.anchorMin = Vector2.zero; bRect.anchorMax = Vector2.one; bRect.offsetMin = Vector2.zero; bRect.offsetMax = Vector2.zero; var bImg = border.AddComponent<Image>(); bImg.color = new Color(0.9f,0.3f,0.3f,1f);
                var bg = slot.AddComponent<Image>(); bg.color = new Color(0.2f,0.2f,0.2f,0.6f);
                var icon = new GameObject("Icon"); icon.transform.SetParent(slot.transform, false); var iRect = icon.AddComponent<RectTransform>(); iRect.anchorMin = Vector2.zero; iRect.anchorMax = Vector2.one; iRect.offsetMin = new Vector2(5,5); iRect.offsetMax = new Vector2(-5,-5); var iImg = icon.AddComponent<Image>(); iImg.color = Color.white;
                var cooldown = new GameObject("CooldownOverlay"); cooldown.transform.SetParent(slot.transform, false); var cRect = cooldown.AddComponent<RectTransform>(); cRect.anchorMin = Vector2.zero; cRect.anchorMax = Vector2.one; cRect.offsetMin = new Vector2(2,2); cRect.offsetMax = new Vector2(-2,-2); var cImg = cooldown.AddComponent<Image>(); cImg.color = new Color(0,0,0,0.8f); cImg.type = Image.Type.Filled; cImg.fillMethod = Image.FillMethod.Radial360; cooldown.SetActive(false);
                var buttonBadge = new GameObject("ButtonBadge"); buttonBadge.transform.SetParent(slot.transform, false); var bbRect = buttonBadge.AddComponent<RectTransform>(); bbRect.anchorMin = new Vector2(1,1); bbRect.anchorMax = new Vector2(1,1); bbRect.pivot = new Vector2(1,1); bbRect.anchoredPosition = new Vector2(2,2); bbRect.sizeDelta = new Vector2(18,18); var bbImg = buttonBadge.AddComponent<Image>(); bbImg.color = new Color(0.3f,0.5f,0.9f,1f);
                var buttonText = new GameObject("ButtonText"); buttonText.transform.SetParent(buttonBadge.transform, false); var btr = buttonText.AddComponent<RectTransform>(); btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one; btr.offsetMin = Vector2.zero; btr.offsetMax = Vector2.zero; if (buttonText.GetComponent<CanvasRenderer>() == null) buttonText.AddComponent<CanvasRenderer>(); var btxt = buttonText.AddComponent<TextMeshProUGUI>(); btxt.text = "X"; btxt.fontSize = 12; btxt.alignment = TextAlignmentOptions.Center;
                var cooldownText = new GameObject("CooldownText"); cooldownText.transform.SetParent(slot.transform, false); var cdRect = cooldownText.AddComponent<RectTransform>(); cdRect.anchorMin = Vector2.zero; cdRect.anchorMax = Vector2.one; cdRect.offsetMin = Vector2.zero; cdRect.offsetMax = Vector2.zero; if (cooldownText.GetComponent<CanvasRenderer>() == null) cooldownText.AddComponent<CanvasRenderer>(); var cdtxt = cooldownText.AddComponent<TextMeshProUGUI>(); cdtxt.text = ""; cdtxt.fontSize = 18; cdtxt.alignment = TextAlignmentOptions.Center; cooldownText.SetActive(false);
            };

            CreateSlot("Slot_Left");
            CreateSlot("Slot_Special");
            CreateSlot("Slot_Right");

            // Seleccionar y mostrar en editor
            Selection.activeGameObject = mainPanel;
            EditorGUIUtility.PingObject(mainPanel);

            // Si el usuario tiene un GameObject seleccionado con PlayerHUDComplete, asignar referencias
            // Buscar componente PlayerHUDComplete en la escena y asignárselo si existe
            // Usar API moderna FindObjectsByType para evitar advertencias de obsolescencia
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
             PlayerHUDComplete comp = null;
             foreach (var g in all)
             {
                 var c = g.GetComponent<PlayerHUDComplete>();
                 if (c != null)
                 {
                     comp = c;
                     break;
                 }
             }

            if (comp != null)
            {
                if (EditorUtility.DisplayDialog("Asignar HUD al PlayerHUDComplete",
                    "Se encontró un objeto con el componente PlayerHUDComplete en la escena. ¿Deseas asignar el HUD recién creado a su campo 'editorRootPanel'?",
                    "Sí, asignar", "No"))
                {
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("editorRootPanel");
                    if (prop != null) { prop.objectReferenceValue = mainPanel; }
                    var prop2 = so.FindProperty("editorCanvas");
                    if (prop2 != null) { prop2.objectReferenceValue = canvasGO.GetComponent<Canvas>(); }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(comp);
                    Debug.Log("PlayerHUDComplete: referencias asignadas desde el editor.");
                }
            }
            else
            {
                // No se encontró componente: mensaje informativo
                Debug.Log("HUD creado. Selecciona tu GameObject con PlayerHUDComplete en la escena y asigna 'editorRootPanel' manualmente o usa el botón de asignación.");
            }
        }
    }
}
