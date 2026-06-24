#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Crea el ConfirmationPopupUI en la escena activa (debe ser Start.unity).
/// El popup es DontDestroyOnLoad y persiste entre escenas.
/// </summary>
public static class ConfirmationPopupBuilder
{
    [MenuItem("El Sendero/UI/Crear ConfirmationPopupUI en escena activa")]
    public static void Create()
    {
        if (Object.FindFirstObjectByType<ConfirmationPopupUI>() != null)
        {
            EditorUtility.DisplayDialog("ConfirmationPopupUI",
                "Ya existe un ConfirmationPopupUI en la escena.", "Ok");
            return;
        }

        // ── Raíz (el DontDestroyOnLoad actúa sobre este GO)
        var root = new GameObject("ConfirmationPopupUI");
        var popup = root.AddComponent<ConfirmationPopupUI>();
        Undo.RegisterCreatedObjectUndo(root, "Crear ConfirmationPopupUI");

        // ── Canvas (Sort Order alto para estar por encima de todo)
        var canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(root.transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ── Panel de fondo oscuro (inicia inactivo — el script lo activa/desactiva)
        var panelGO = new GameObject("Panel", typeof(Image));
        panelGO.transform.SetParent(canvasGO.transform, false);
        Stretch(panelGO);
        panelGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);
        panelGO.SetActive(false);

        // ── Caja de diálogo centrada
        var boxGO = new GameObject("DialogBox", typeof(Image));
        boxGO.transform.SetParent(panelGO.transform, false);
        var boxRT = boxGO.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.25f, 0.35f);
        boxRT.anchorMax = new Vector2(0.75f, 0.65f);
        boxRT.sizeDelta = Vector2.zero;
        boxGO.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.97f);

        // ── Texto del mensaje
        var msgGO = new GameObject("MessageText", typeof(TextMeshProUGUI));
        msgGO.transform.SetParent(boxGO.transform, false);
        var msgRT = msgGO.GetComponent<RectTransform>();
        msgRT.anchorMin = new Vector2(0.05f, 0.48f);
        msgRT.anchorMax = new Vector2(0.95f, 0.92f);
        msgRT.sizeDelta = Vector2.zero;
        var msgTMP = msgGO.GetComponent<TextMeshProUGUI>();
        msgTMP.text = "¿Salir del minijuego?";
        msgTMP.fontSize = 28;
        msgTMP.alignment = TextAlignmentOptions.Center;
        msgTMP.color = Color.white;

        // ── Fila de botones
        var buttonsGO = new GameObject("Buttons", typeof(HorizontalLayoutGroup));
        buttonsGO.transform.SetParent(boxGO.transform, false);
        var buttonsRT = buttonsGO.GetComponent<RectTransform>();
        buttonsRT.anchorMin = new Vector2(0.05f, 0.06f);
        buttonsRT.anchorMax = new Vector2(0.95f, 0.44f);
        buttonsRT.sizeDelta = Vector2.zero;
        var hlg = buttonsGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        // ── Botón Confirmar (verde)
        var (confirmBtn, confirmLabel) = MakeButton(
            "ConfirmButton", buttonsGO.transform, "Sí", new Color(0.12f, 0.52f, 0.12f));

        // ── Botón Cancelar (rojo)
        var (cancelBtn, cancelLabel) = MakeButton(
            "CancelButton", buttonsGO.transform, "No", new Color(0.52f, 0.12f, 0.12f));

        // ── Conectar referencias del componente via SerializedObject
        var so = new SerializedObject(popup);
        so.FindProperty("panel").objectReferenceValue        = panelGO;
        so.FindProperty("messageText").objectReferenceValue  = msgTMP;
        so.FindProperty("confirmButton").objectReferenceValue = confirmBtn;
        so.FindProperty("confirmLabel").objectReferenceValue  = confirmLabel;
        so.FindProperty("cancelButton").objectReferenceValue  = cancelBtn;
        so.FindProperty("cancelLabel").objectReferenceValue   = cancelLabel;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log("[ConfirmationPopupBuilder] ✅ ConfirmationPopupUI creado. Guarda Start.unity (Ctrl+S) para persistir.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static (Button btn, TextMeshProUGUI label) MakeButton(
        string name, Transform parent, string text, Color bgColor)
    {
        var go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(180, 60);
        go.GetComponent<Image>().color = bgColor;

        var labelGO = new GameObject("Label", typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.sizeDelta = Vector2.zero;
        var tmp = labelGO.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 26;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return (go.GetComponent<Button>(), tmp);
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
#endif
