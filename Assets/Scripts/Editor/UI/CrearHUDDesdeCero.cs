using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Sendero.UI;

/// <summary>
/// Construye un HUD nuevo DESDE CERO (no reutiliza ningún GameObject del HUD viejo) con el lenguaje
/// visual procedural — el que se repetirá luego en misiones, inventario y tienda. Todo el arte sale de
/// <see cref="ProceduralUIKit"/>: ningún sprite a mano.
///
/// Crea, bajo el mismo Canvas donde vive <c>PlayerHUDV2</c>:
/// - "BarraVida" / "BarraMana": panel procedural (<see cref="ProceduralPanelSkin"/>) + relleno plano
///   (<c>Image.Type.Filled</c>) con un margen interior para que las esquinas redondeadas del panel
///   enmarquen los extremos rectos del relleno.
/// - "SlotIzquierdo" / "SlotEspecial" / "SlotDerecho": disco de fondo + icono (vacío, lo rellena
///   PlayerHUDV2 en runtime) + aro brillante (<see cref="ProceduralSlotFrameSkin"/>) + overlay de
///   cooldown radial.
///
/// Y reenlaza los campos de <c>PlayerHUDV2</c> (healthFillImage, manaFillImage, los tres
/// *MagicSlotImage y sus *CooldownOverlay) para que apunten a estos elementos nuevos.
///
/// NO borra el HUD viejo (PanelHP/PanelMP/ImageSlot.../ImageHPBG/ImageMPBG) — lo deja al lado, ya sin
/// ninguna referencia de PlayerHUDV2 apuntándolo, para que se pueda borrar a mano desde la Hierarchy en
/// cuanto se compruebe que el nuevo funciona. Todo pasa por un único grupo de Undo (Ctrl+Z lo deshace
/// de una vez, incluyendo el reenlazado de PlayerHUDV2).
///
/// Uso: con Start.unity abierto, "El Sendero → UI → Crear HUD nuevo desde cero".
/// </summary>
static class CrearHUDDesdeCero
{
    // Color de acento por barra — el resto (fondo, borde, halo) sale de la paleta común del kit.
    static readonly Color ColorVida = new Color(0.86f, 0.24f, 0.22f, 1f);
    static readonly Color ColorMana = new Color(0.32f, 0.48f, 0.95f, 1f);

    const string NombreDeshacer = "Crear HUD nuevo desde cero";

    [MenuItem("El Sendero/UI/Crear HUD nuevo desde cero")]
    [MenuItem("Assets/El Sendero/UI/Crear HUD nuevo desde cero")]
    static void Ejecutar()
    {
        var hud = Object.FindFirstObjectByType<PlayerHUDV2>(FindObjectsInactive.Include);
        if (hud == null)
        {
            EditorUtility.DisplayDialog(
                "Crear HUD nuevo",
                "No se encontró ningún PlayerHUDV2 en la escena abierta.\n\n" +
                "Abre Start.unity, que es donde vive el HUD, y vuelve a intentarlo.",
                "Vale");
            return;
        }

        var canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Crear HUD nuevo",
                "PlayerHUDV2 no está bajo ningún Canvas — no sé dónde colgar la UI nueva.",
                "Vale");
            return;
        }

        Undo.SetCurrentGroupName(NombreDeshacer);
        int grupo = Undo.GetCurrentGroup();

        var root = NuevoRect("HUD (Procedural)", canvas.transform,
            ancla: new Vector2(0f, 1f), pivote: new Vector2(0f, 1f),
            tamano: new Vector2(420f, 210f), posicion: new Vector2(40f, -40f));

        var (_, fillVida) = CrearBarra(root, "BarraVida", new Vector2(0f, -6f), new Vector2(400f, 40f), ColorVida);
        var (_, fillMana) = CrearBarra(root, "BarraMana", new Vector2(0f, -52f), new Vector2(400f, 26f), ColorMana);

        var slotIzq = CrearSlot(root, "SlotIzquierdo", new Vector2(-140f, -145f));
        var slotEsp = CrearSlot(root, "SlotEspecial", new Vector2(0f, -155f));
        var slotDer = CrearSlot(root, "SlotDerecho", new Vector2(140f, -145f));

        Undo.RecordObject(hud, NombreDeshacer);
        var so = new SerializedObject(hud);
        Asignar(so, "healthFillImage", fillVida);
        Asignar(so, "manaFillImage", fillMana);
        Asignar(so, "leftMagicSlotImage", slotIzq.icono);
        Asignar(so, "rightMagicSlotImage", slotDer.icono);
        Asignar(so, "specialMagicSlotImage", slotEsp.icono);
        Asignar(so, "leftCooldownOverlay", slotIzq.cooldown);
        Asignar(so, "rightCooldownOverlay", slotDer.cooldown);
        Asignar(so, "specialCooldownOverlay", slotEsp.cooldown);
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(grupo);

        Selection.activeGameObject = root.gameObject;
        EditorGUIUtility.PingObject(root.gameObject);

        Debug.Log(
            "[CrearHUDDesdeCero] HUD nuevo creado bajo '" + canvas.name + "' y enlazado a PlayerHUDV2 " +
            "(vida, maná, 3 slots de magia con cooldown). El HUD antiguo (PanelHP/PanelMP/ImageSlot...) " +
            "sigue en la escena pero ya no lo referencia ningún campo — bórralo a mano desde la Hierarchy " +
            "en cuanto compruebes que el nuevo funciona. emptySlotSprite (el icono de slot vacío) no se " +
            "ha tocado, sigue siendo el sprite original.");
    }

    static void Asignar(SerializedObject so, string campo, Object valor)
    {
        var prop = so.FindProperty(campo);
        if (prop == null)
        {
            Debug.LogWarning($"[CrearHUDDesdeCero] Campo '{campo}' no existe en PlayerHUDV2 — revisa si se ha renombrado.");
            return;
        }
        prop.objectReferenceValue = valor;
    }

    // ── Construcción de piezas ──────────────────────────────────────────────────

    static (RectTransform panel, Image fill) CrearBarra(RectTransform padre, string nombre, Vector2 posicion, Vector2 tamano, Color colorAcento)
    {
        var panel = NuevoRect(nombre, padre, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), tamano, posicion);
        var panelImg = Undo.AddComponent<Image>(panel.gameObject);
        panelImg.raycastTarget = false;
        Undo.AddComponent<ProceduralPanelSkin>(panel.gameObject);

        // Margen respecto al borde del panel: así las esquinas redondeadas del panel enmarcan los
        // extremos rectos del relleno, en vez de que se vea una esquina cuadrada asomando por fuera.
        float margen = 6f;
        var fillRT = NuevoRect("Relleno", panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(tamano.x - margen * 2f, tamano.y - margen * 2f), new Vector2(margen, 0f));
        var fillImg = Undo.AddComponent<Image>(fillRT.gameObject);
        fillImg.raycastTarget = false;
        fillImg.sprite = ProceduralUIKit.BuildFlatFillSprite(colorAcento);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;

        return (panel, fillImg);
    }

    struct Slot
    {
        public Image icono;
        public Image cooldown;
    }

    static Slot CrearSlot(RectTransform padre, string nombre, Vector2 posicion)
    {
        const float lado = 100f;
        var root = NuevoRect(nombre, padre, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.one * lado, posicion);

        var fondo = Undo.AddComponent<Image>(root.gameObject);
        fondo.raycastTarget = false;
        fondo.sprite = ProceduralUIKit.BuildFilledCircleSprite(size: 96, glowRange: 6f);

        var iconoRT = NuevoRect("Icono", root, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * (lado * 0.68f), Vector2.zero);
        var icono = Undo.AddComponent<Image>(iconoRT.gameObject);
        icono.raycastTarget = false;
        icono.color = Color.white; // PlayerHUDV2 asigna el sprite del hechizo (o emptySlotSprite) en runtime

        var aroRT = NuevoRect("Aro", root, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * lado, Vector2.zero);
        Undo.AddComponent<Image>(aroRT.gameObject);
        Undo.AddComponent<ProceduralSlotFrameSkin>(aroRT.gameObject);

        var cooldownRT = NuevoRect("Cooldown", root, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * (lado * 0.68f), Vector2.zero);
        var cooldown = Undo.AddComponent<Image>(cooldownRT.gameObject);
        cooldown.raycastTarget = false;
        cooldown.sprite = ProceduralUIKit.BuildFilledCircleSprite(
            size: 64, glowRange: 0f, fill: new Color(0f, 0f, 0f, 0.75f), rimHighlight: false);
        cooldown.type = Image.Type.Filled;
        cooldown.fillMethod = Image.FillMethod.Radial360;
        cooldown.fillOrigin = (int)Image.Origin360.Top;
        cooldown.fillClockwise = false;
        cooldown.fillAmount = 0f;

        return new Slot { icono = icono, cooldown = cooldown };
    }

    static RectTransform NuevoRect(string nombre, Transform padre, Vector2 ancla, Vector2 pivote, Vector2 tamano, Vector2 posicion)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, NombreDeshacer);
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = ancla;
        rt.anchorMax = ancla;
        rt.pivot = pivote;
        rt.sizeDelta = tamano;
        rt.anchoredPosition = posicion;
        return rt;
    }
}
