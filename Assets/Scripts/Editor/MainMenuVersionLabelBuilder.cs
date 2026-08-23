using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Añade (o repara) la etiqueta de versión del juego en MainMenu.unity — ver INC-083 del Tracker de
/// Incidencias.
///
/// Vive en su propio Canvas ('VersionCanvas', GameObject raíz independiente, no hijo de 'Canvas' ni
/// de ningún panel) para que no dependa del CanvasGroup/rootGroup del intro de MainMenuController, ni
/// de ningún otro sistema que pueda desactivar o hacer fade al Canvas principal. Así es visible desde
/// el primer frame del menú y no desaparece cuando Ajustes/Controles ocultan el ButtonPanel.
///
/// El texto en sí (número de versión real) lo pinta en runtime VersionLabelUI.cs, leyendo
/// Application.version (PlayerSettings > Other Settings > Version) — este builder solo coloca y
/// estiliza los GameObjects, no escribe el número a mano.
///
/// Reparador, no solo creador: si 'VersionCanvas'/'VersionLabel' ya existen, no los duplica — reaplica
/// configuración/posición/estilo por si algo se desconfiguró a mano.
///
/// Uso: menú "El Sendero → Controles → Añadir Etiqueta de Versión al Main Menu".
/// </summary>
public static class MainMenuVersionLabelBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string CanvasGoName = "VersionCanvas";
    const string LabelGoName = "VersionLabel";

    // Mismo Reference Resolution que el Canvas principal del menú, para que el tamaño de fuente se
    // escale igual en cualquier resolución.
    static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // Por debajo de los paneles de Ajustes/Controles/Créditos (Sorting Order 0): si el jugador abre
    // alguno de esos paneles a pantalla completa, lo tapa con normalidad, igual que taparía al
    // Canvas principal. Por encima del fondo 3D del menú (Canvas principal, Sorting Order -10).
    const int SortingOrder = -5;

    // Esquina inferior izquierda, con un pequeño margen respecto al borde de pantalla.
    static readonly Vector2 AnchorMinMax = new Vector2(0f, 0f);
    static readonly Vector2 Pivot = new Vector2(0f, 0f);
    static readonly Vector2 AnchoredPosition = new Vector2(24f, 16f);
    static readonly Vector2 SizeDelta = new Vector2(320f, 40f);

    const float FontSize = 22f;
    static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.55f);

    [MenuItem("El Sendero/Controles/Añadir Etiqueta de Versión al Main Menu")]
    public static void AddVersionLabel()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuVersionLabelBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuVersionLabelBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuVersionLabelBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        bool canvasCreated = SetUpVersionCanvas(out var versionCanvasGo);
        bool labelCreated = SetUpVersionLabel(versionCanvasGo, out _);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MainMenuVersionLabelBuilder] ✅ '{CanvasGoName}' {(canvasCreated ? "creado" : "reparado")} y " +
                  $"'{LabelGoName}' {(labelCreated ? "creado" : "reparado")}, guardado en {ScenePath}. " +
                  $"En Play mostrará la versión real (Application.version = '{Application.version}', tomada de PlayerSettings > Other Settings > Version).");
    }

    // ── Canvas propio ────────────────────────────────────────────────────

    static bool SetUpVersionCanvas(out GameObject canvasGo)
    {
        bool created;
        var existing = FindByNameIncludingInactive(CanvasGoName);

        if (existing != null)
        {
            canvasGo = existing;
            created = false;
            Debug.Log($"[MainMenuVersionLabelBuilder] '{CanvasGoName}' ya existe — reaplicando configuración por si se tocó a mano.");
        }
        else
        {
            canvasGo = new GameObject(CanvasGoName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            created = true;
        }

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Nada de GraphicRaycaster: la etiqueta es puramente informativa (raycastTarget = false más
        // abajo), no necesita recibir clicks ni competir por ellos con el menú.

        return created;
    }

    // ── Texto de la versión ─────────────────────────────────────────────

    static bool SetUpVersionLabel(GameObject versionCanvasGo, out GameObject labelGo)
    {
        bool created;
        var existing = versionCanvasGo.transform.Find(LabelGoName);

        if (existing != null)
        {
            labelGo = existing.gameObject;
            created = false;
            Debug.Log($"[MainMenuVersionLabelBuilder] '{LabelGoName}' ya existe — reaplicando posición/estilo por si se tocó a mano.");
        }
        else
        {
            labelGo = new GameObject(LabelGoName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(versionCanvasGo.transform, false);
            created = true;
        }

        var rt = (RectTransform)labelGo.transform;
        rt.anchorMin = AnchorMinMax;
        rt.anchorMax = AnchorMinMax;
        rt.pivot = Pivot;
        rt.anchoredPosition = AnchoredPosition;
        rt.sizeDelta = SizeDelta;

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        ApplyFontFromExistingMenuLabel(tmp);

        tmp.fontSize = FontSize;
        tmp.enableAutoSizing = false;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false; // solo informativo, no debe robar clicks al menú
        tmp.text = "v0.0.0"; // placeholder en editor — VersionLabelUI lo sobreescribe en runtime

        if (labelGo.GetComponent<VersionLabelUI>() == null)
            labelGo.AddComponent<VersionLabelUI>();

        return created;
    }

    // Copia fuente/material de un TMP_Text ya presente en el menú principal (p.ej. el de un botón),
    // para que la etiqueta de versión use la misma tipografía que el resto del menú sin tener que
    // apuntar a un asset de fuente a mano ni arriesgarse a que ese GUID cambie entre proyectos.
    // Busca en toda la escena (no solo dentro de VersionCanvas, que ahora vive aparte de 'Canvas').
    static void ApplyFontFromExistingMenuLabel(TextMeshProUGUI target)
    {
        TextMeshProUGUI reference = null;
        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
        {
            if (tmp.gameObject != target.gameObject) { reference = tmp; break; }
        }

        if (reference == null)
        {
            Debug.LogWarning("[MainMenuVersionLabelBuilder] No se encontró ningún otro TextMeshProUGUI en la escena para copiar la tipografía — se deja la fuente TMP por defecto.");
            return;
        }

        if (reference.font != null)
            target.font = reference.font;
        if (reference.fontSharedMaterial != null)
            target.fontSharedMaterial = reference.fontSharedMaterial;
    }

    static GameObject FindByNameIncludingInactive(string name)
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
            if (t.name == name)
                return t.gameObject;
        return null;
    }
}
