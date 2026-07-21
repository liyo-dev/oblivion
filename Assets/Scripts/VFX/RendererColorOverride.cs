using UnityEngine;

/// Tiñe el/los renderers indicados con un color usando MaterialPropertyBlock.
/// El override vive SOLO en esta instancia: no crea materiales nuevos ni
/// modifica el material/shader compartido, así los demás personajes y mallas
/// que usen el mismo material (ej: PBR_Default del pack Tiny Hero Duo) no
/// se ven afectados.
///
/// Uso: añadir al GameObject del pelo (ej: Hair08 de _ESTELA) y elegir el
/// color en el Inspector. Con [ExecuteAlways] el cambio se previsualiza en
/// el editor sin darle a Play.
///
/// Nota: el tinte MULTIPLICA la textura. Sobre una textura clara el color
/// sale fiel; sobre una oscura queda apagado (subir el brillo del color).
[ExecuteAlways]
[DisallowMultipleComponent]
public class RendererColorOverride : MonoBehaviour
{
    [Tooltip("Color de tinte (naranja por defecto, referencia Lina Inverse)")]
    [SerializeField] private Color _color = new Color(1f, 0.45f, 0.15f, 1f);

    [Tooltip("Renderers a teñir. Vacío = el Renderer de este mismo GameObject.")]
    [SerializeField] private Renderer[] _renderers;

    // Propiedades de color del Unity Toon Shader. Se tiñen también los colores
    // de sombra toon para que las zonas sombreadas mantengan el mismo tono.
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId     = Shader.PropertyToID("_Color");
    private static readonly int Shade1Id    = Shader.PropertyToID("_1st_ShadeColor");
    private static readonly int Shade2Id    = Shader.PropertyToID("_2nd_ShadeColor");

    private MaterialPropertyBlock _mpb;

    void OnEnable()   => Apply();
    void OnValidate() => Apply();
    void OnDisable()  => Clear();

    private void Apply()
    {
        _mpb ??= new MaterialPropertyBlock();
        var targets = ResolveTargets();
        for (int i = 0; i < targets.Length; i++)
        {
            var r = targets[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, _color);
            _mpb.SetColor(ColorId,     _color);
            _mpb.SetColor(Shade1Id,    _color);
            _mpb.SetColor(Shade2Id,    _color);
            r.SetPropertyBlock(_mpb);
        }
    }

    private void Clear()
    {
        var targets = ResolveTargets();
        for (int i = 0; i < targets.Length; i++)
            if (targets[i] != null) targets[i].SetPropertyBlock(null);
    }

    private Renderer[] ResolveTargets()
    {
        if (_renderers != null && _renderers.Length > 0) return _renderers;
        var own = GetComponent<Renderer>();
        return own != null ? new[] { own } : System.Array.Empty<Renderer>();
    }
}
