using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Sendero.UI; // PlayerHUDV2 vive en este namespace (a diferencia de la mayoría de scripts de Assets/Scripts/UI)

/// <summary>
/// Herramienta de editor: aplica el kit visual procedural (<see cref="ProceduralUIKit"/>) al HUD del
/// jugador en la escena abierta, sin tocar ninguna lógica de <c>PlayerHUDV2</c> ni ningún sprite propio.
///
/// Qué hace exactamente (localiza los GameObjects a partir de las referencias que ya tiene
/// <c>PlayerHUDV2</c> en el Inspector, vía <see cref="SerializedObject"/> — no hace falta seleccionar
/// nada a mano):
/// - <see cref="ProceduralPanelSkin"/> en el panel padre de <c>healthFillImage</c>/<c>manaFillImage</c>
///   (el fondo translúcido detrás de la barra — NO el sprite de relleno ni el frame ImageHPBG/ImageMPBG,
///   que son arte hecho a mano y no se tocan).
/// - <see cref="ProceduralSlotFrameSkin"/> en el marco padre de cada slot de magia (izquierdo, derecho,
///   especial), dejando el icono del hechizo intacto.
///
/// Es idempotente: si un GameObject ya tiene el componente correspondiente, no lo duplica — se puede
/// ejecutar tantas veces como haga falta sin acumular nada. Todo pasa por <see cref="Undo"/>, así que
/// Ctrl+Z deshace el resultado completo de una sola pasada.
///
/// Uso: con Start.unity abierto (ahí vive el HUD), disponible en dos sitios (el mismo comando):
/// - Menú superior "El Sendero" → UI → Crear nuevo HUD (mismo menú raíz que "El Sendero → Render → ...").
/// - Menú "Assets" → El Sendero → UI → Crear nuevo HUD (y también clic derecho en la ventana Project).
/// </summary>
static class CrearNuevoHUD
{
    [MenuItem("El Sendero/UI/Crear nuevo HUD (procedural)")]
    [MenuItem("Assets/El Sendero/UI/Crear nuevo HUD (procedural)")]
    static void Ejecutar()
    {
        var hud = Object.FindFirstObjectByType<PlayerHUDV2>(FindObjectsInactive.Include);
        if (hud == null)
        {
            EditorUtility.DisplayDialog(
                "Crear nuevo HUD",
                "No se encontró ningún PlayerHUDV2 en la escena abierta.\n\n" +
                "Abre (o carga aditivamente) Start.unity, que es donde vive el HUD, y vuelve a intentarlo.",
                "Vale");
            return;
        }

        var so = new SerializedObject(hud);
        int aplicados = 0;
        int yaEstaban = 0;
        int noEncontrados = 0;

        AplicarPanelAlFondoDe(so, "healthFillImage", ref aplicados, ref yaEstaban, ref noEncontrados);
        AplicarPanelAlFondoDe(so, "manaFillImage", ref aplicados, ref yaEstaban, ref noEncontrados);
        AplicarAroA(so, "leftMagicSlotImage", ref aplicados, ref yaEstaban, ref noEncontrados);
        AplicarAroA(so, "rightMagicSlotImage", ref aplicados, ref yaEstaban, ref noEncontrados);
        AplicarAroA(so, "specialMagicSlotImage", ref aplicados, ref yaEstaban, ref noEncontrados);

        string resumen = $"HUD procedural: {aplicados} componente(s) añadidos, {yaEstaban} ya estaban" +
                          (noEncontrados > 0 ? $", {noEncontrados} no se pudieron localizar (revisa la consola)." : ".");

        Debug.Log($"[CrearNuevoHUD] {resumen}");
        EditorUtility.DisplayDialog("Crear nuevo HUD", resumen, "Vale");
    }

    // El panel de fondo es el GameObject padre directo del Image de relleno en la jerarquía actual
    // del HUD (p. ej. "PanelHP" es padre de "ImageHPFILL").
    static void AplicarPanelAlFondoDe(SerializedObject so, string campo, ref int aplicados, ref int yaEstaban, ref int noEncontrados)
    {
        var frameGO = ResolverPadreConImage(so, campo);
        if (frameGO == null) { noEncontrados++; return; }
        if (AddIfMissing<ProceduralPanelSkin>(frameGO)) aplicados++; else yaEstaban++;
    }

    // El marco del slot es el GameObject "ImageSlot" que contiene al icono (el propio campo) como hijo.
    static void AplicarAroA(SerializedObject so, string campo, ref int aplicados, ref int yaEstaban, ref int noEncontrados)
    {
        var frameGO = ResolverPadreConImage(so, campo);
        if (frameGO == null) { noEncontrados++; return; }
        if (AddIfMissing<ProceduralSlotFrameSkin>(frameGO)) aplicados++; else yaEstaban++;
    }

    static GameObject ResolverPadreConImage(SerializedObject so, string campo)
    {
        var prop = so.FindProperty(campo);
        if (prop == null || prop.objectReferenceValue == null)
        {
            Debug.LogWarning($"[CrearNuevoHUD] Campo '{campo}' no encontrado o vacío en PlayerHUDV2.");
            return null;
        }
        var img = prop.objectReferenceValue as Image;
        var parent = img != null ? img.transform.parent : null;
        if (parent == null || parent.GetComponent<Image>() == null)
        {
            Debug.LogWarning($"[CrearNuevoHUD] '{campo}' no tiene un padre con Image — no se puede aplicar el skin ahí.");
            return null;
        }
        return parent.gameObject;
    }

    static bool AddIfMissing<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() != null) return false;
        Undo.AddComponent<T>(go);
        EditorUtility.SetDirty(go);
        return true;
    }
}
