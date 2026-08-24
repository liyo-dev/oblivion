using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Añade (o repara) la etiqueta de fase de desarrollo ("PRE-ALPHA") justo debajo del logo del juego
/// en MainMenu.unity — decisión tomada con Raúl el 24 ago 2026 tras ver que otros juegos en Kickstarter
/// (p. ej. Billie Bust Up) muestran su fase bajo el logo. "Demo" (el tipo de build que se descarga en
/// itch.io) y "Pre-Alpha" (la fase de desarrollo del juego completo) son cosas distintas y no se
/// excluyen — esta etiqueta es la segunda, no sustituye a la palabra "demo" que ya se usa en itch.io/la
/// web.
///
/// Se cuelga como HIJO del propio 'LogoTitulo' (no de un Canvas aparte, a diferencia de
/// VersionLabelUI/MainMenuVersionLabelBuilder): así hereda su posición sin tener que duplicar el
/// anchoring del logo (anchor top-right, offset 150,100, tamaño 768x512).
///
/// FIX (24 ago 2026, tras ver una captura de Raúl): la primera versión anclaba el label al BORDE
/// INFERIOR del rect de 'LogoTitulo' (768x512), asumiendo que el arte del logo llenaba todo ese rect.
/// En la práctica el sprite del logo solo pinta el texto en la franja superior del rect (el resto es
/// relleno transparente, típico de un PNG de logo con margen para el resplandor) — así que "PRE-ALPHA"
/// aparecía flotando muy por debajo del texto visible, sobre el mundo 3D del fondo, sin relación visual
/// con el logo. Corregido: ahora se ancla al BORDE SUPERIOR del rect (que sí coincide con el borde
/// superior real del texto del logo) con un desplazamiento hacia abajo.
///
/// FIX 2 (24 ago 2026, "no sale lo de pre alfa"): el offset de -195 de arriba seguía mal — calibrado
/// "a ojo" contra una captura, sin medir de verdad el sprite. Se midió el canal alfa real de
/// Assets/Art/UI/Menu/logo sendero 4.png (1536x1024 px, textura NO Read/Write así que se analizó el
/// PNG fuente directamente, no en runtime): el texto "EL SENDERO DE LAS ESTRELLAS" ocupa de la fila
/// ~286 a la ~677 (umbral de alfa >30, ignorando el resplandor tenue de fondo que da falsos positivos
/// en casi toda la imagen). Con y=-195 (equivalente a la fila de píxel 390 de esa imagen, escala
/// 0.5 = 768/1536), el label caía JUSTO ENCIMA de "EL SENDERO", superpuesto y camuflado en el propio
/// arte dorado del logo — por eso "no salía": técnicamente se dibujaba, pero era ilegible sobre el
/// logo. Confirmado renderizando una línea guía sobre el PNG en la posición antigua (atraviesa el
/// texto) y en la nueva (queda justo debajo de "ESTRELLAS", con margen). Nuevo valor: -355 (fila de
/// píxel ~710, bottom del texto ~677 + margen), deducido de la medición real, no a ojo. Sigue cabiendo
/// dentro de la caja de 512 de alto (-355-48=-403, no se sale por abajo). Si el logo cambia de arte en
/// el futuro, hay que remedir en vez de reutilizar este número a ciegas.
///
/// Reparador, no solo creador: si 'PhaseLabel' ya existe, no lo duplica — reaplica texto/posición/estilo
/// por si algo se desconfiguró a mano.
///
/// Uso: menú "El Sendero → Controles → Añadir Etiqueta de Fase (PRE-ALPHA) al Main Menu".
/// </summary>
public static class MainMenuPhaseLabelBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string LogoGoName = "LogoTitulo";
    const string LabelGoName = "PhaseLabel";

    const string PhaseText = "PRE-ALPHA";
    const float FontSize = 30f;
    static readonly Color LabelColor = new Color(0.97f, 0.71f, 0.22f, 0.9f); // mismo dorado de acento que Créditos/Notas del Parche

    // Debajo del borde SUPERIOR del rect del logo (no del inferior — ver FIX de arriba), bajando lo
    // suficiente para despejar el texto visible del logo sin quedar demasiado lejos de él. Valor
    // medido de verdad contra el canal alfa del PNG del logo (ver FIX 2 de arriba) — no es un valor
    // a ojo.
    static readonly Vector2 AnchoredPosition = new Vector2(0f, -355f);
    static readonly Vector2 SizeDelta = new Vector2(0f, 48f);

    [MenuItem("El Sendero/Controles/Añadir Etiqueta de Fase (PRE-ALPHA) al Main Menu")]
    public static void AddPhaseLabel()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuPhaseLabelBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuPhaseLabelBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuPhaseLabelBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        var logo = FindByNameIncludingInactive(LogoGoName);
        if (logo == null)
        {
            Debug.LogError($"[MainMenuPhaseLabelBuilder] No se encontró '{LogoGoName}' en la escena — ¿se ha renombrado el logo del menú?");
            return;
        }

        bool created = SetUpPhaseLabel(logo.transform, out _);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MainMenuPhaseLabelBuilder] ✅ '{LabelGoName}' {(created ? "creado" : "reparado")} bajo '{LogoGoName}', guardado en {ScenePath}.");
    }

    static bool SetUpPhaseLabel(Transform logoTransform, out GameObject labelGo)
    {
        bool created;
        var existing = logoTransform.Find(LabelGoName);

        if (existing != null)
        {
            labelGo = existing.gameObject;
            created = false;
            Debug.Log($"[MainMenuPhaseLabelBuilder] '{LabelGoName}' ya existe — reaplicando posición/estilo por si se tocó a mano.");
        }
        else
        {
            labelGo = new GameObject(LabelGoName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(logoTransform, false);
            created = true;
        }

        var rt = (RectTransform)labelGo.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = AnchoredPosition;
        rt.sizeDelta = SizeDelta;

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        ApplyFontFromExistingMenuLabel(tmp);

        tmp.text = PhaseText;
        tmp.fontSize = FontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = false;
        tmp.color = LabelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false; // puramente informativo, no debe robar clicks

        return created;
    }

    // Mismo helper que MainMenuVersionLabelBuilder: copia fuente/material de un TMP_Text ya presente
    // en el menú para no depender de apuntar a mano a un asset de fuente concreto.
    static void ApplyFontFromExistingMenuLabel(TextMeshProUGUI target)
    {
        TextMeshProUGUI reference = null;
        foreach (var tmp in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include))
        {
            if (tmp.gameObject != target.gameObject) { reference = tmp; break; }
        }

        if (reference == null)
        {
            Debug.LogWarning("[MainMenuPhaseLabelBuilder] No se encontró ningún otro TextMeshProUGUI en la escena para copiar la tipografía — se deja la fuente TMP por defecto.");
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
