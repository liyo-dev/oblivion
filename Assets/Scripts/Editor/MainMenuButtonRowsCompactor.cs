using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Encoge las filas de botón del ButtonPanel del MainMenu — pedido por Raúl el 24 ago 2026 tras ver
/// una captura con 8 filas (Continuar, Nueva Partida, Configuración, Controles, Créditos, Salir,
/// Notas del Parche, Reportar un Fallo): con el tamaño/espaciado que tenía cada fila (65.6 de alto,
/// 40 de espaciado, fijados por MainMenuStylingBuilder.StyleButtonRows() en su momento, para un menú
/// de solo 6 filas), la lista no cabía entera en pantalla — la fila de Reportar un Fallo quedaba
/// cortada por debajo del borde inferior.
///
/// Reduce el espaciado del VerticalLayoutGroup y el alto de cada fila (LayoutElement.preferredHeight/
/// minHeight) a un valor más compacto, y activa auto-sizing en el texto de cada fila para que no se
/// recorte al encoger la altura. Idempotente por construcción: usa `Mathf.Min(valorActual, objetivo)`
/// en vez de multiplicar por un factor, así que ejecutarlo varias veces no sigue encogiendo cada vez
/// (a diferencia de StyleButtonRows(), que sí evita re-tocar botones ya encogidos comprobando
/// `preferredWidth > 0`).
///
/// Nota (24 ago 2026): esto es una primera pasada calculada, no confirmada visualmente en el Editor
/// todavía (esta sesión en la nube no tiene acceso al Editor de Unity) — si tras probarlo sigue sin
/// caber del todo o el texto se ve demasiado pequeño, dilo con una captura y se afina el valor.
///
/// Uso: menú "El Sendero → Controles → Compactar Filas del Main Menu (para que quepan todas)".
/// </summary>
public static class MainMenuButtonRowsCompactor
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";

    const float TargetSpacing = 10f;       // antes: 40
    const float TargetRowHeight = 42f;     // antes: 65.6
    const float MinFontSize = 15f;         // suelo de auto-sizing, para que el texto siga siendo legible

    [MenuItem("El Sendero/Controles/Compactar Filas del Main Menu (para que quepan todas)")]
    public static void CompactRows()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuButtonRowsCompactor] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuButtonRowsCompactor] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuButtonRowsCompactor] No se pudo abrir {ScenePath}.");
            return;
        }

        var buttonPanel = FindByNameIncludingInactive("ButtonPanel");
        if (buttonPanel == null)
        {
            Debug.LogError("[MainMenuButtonRowsCompactor] No se encontró 'ButtonPanel'.");
            return;
        }

        var layoutGroup = buttonPanel.GetComponent<VerticalLayoutGroup>()
                        ?? buttonPanel.GetComponentInChildren<VerticalLayoutGroup>(true);

        int spacingChanged = 0;
        if (layoutGroup != null)
        {
            var so = new SerializedObject(layoutGroup);
            var spacingProp = so.FindProperty("m_Spacing");
            float before = spacingProp.floatValue;
            spacingProp.floatValue = Mathf.Min(before, TargetSpacing);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!Mathf.Approximately(before, spacingProp.floatValue)) spacingChanged++;
        }
        else
        {
            Debug.LogWarning("[MainMenuButtonRowsCompactor] No se encontró VerticalLayoutGroup dentro de 'ButtonPanel'.");
        }

        var buttons = buttonPanel.GetComponentsInChildren<Button>(true);
        int rowsShrunk = 0, textsAdjusted = 0;

        foreach (var b in buttons)
        {
            var le = b.GetComponent<LayoutElement>();
            if (le != null)
            {
                float beforeH = le.preferredHeight;
                float newH = beforeH > 0f ? Mathf.Min(beforeH, TargetRowHeight) : TargetRowHeight;
                if (!Mathf.Approximately(beforeH, newH))
                {
                    le.preferredHeight = newH;
                    le.minHeight = newH;
                    rowsShrunk++;
                }
            }

            var tmp = b.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null)
            {
                if (!tmp.enableAutoSizing)
                {
                    tmp.fontSizeMax = tmp.fontSize > 0f ? tmp.fontSize : 32f;
                    tmp.fontSizeMin = MinFontSize;
                    tmp.enableAutoSizing = true;
                    textsAdjusted++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MainMenuButtonRowsCompactor] ✅ Espaciado {(spacingChanged > 0 ? $"reducido a {TargetSpacing}" : "ya estaba igual o más pequeño")}, " +
                  $"{rowsShrunk} fila(s) encogida(s) a {TargetRowHeight} de alto, {textsAdjusted} texto(s) con auto-sizing activado " +
                  $"(mín {MinFontSize}pt). Dale a Play y comprueba que las {buttons.Length} filas caben en pantalla.");
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
