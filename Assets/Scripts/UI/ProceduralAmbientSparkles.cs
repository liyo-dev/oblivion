using UnityEngine;

/// <summary>
/// Chispas ambientales sutiles para paneles de UI "vivos" (menú principal, ventana de misiones/inventario,
/// tienda...). Es un envoltorio fino sobre <see cref="DreamSparkleOverlay"/> — el mismo generador que ya usan
/// las secuencias de sueño — así que reutiliza el mismo look en vez de inventar uno nuevo. No requiere cablear
/// nada en el Inspector: crea su propio hijo con el overlay en <see cref="Awake"/>.
///
/// Uso: añadir a cualquier panel (Screen Space) que deba sentirse "mágico"/vivo. Se activa/desactiva solo con
/// <see cref="OnEnable"/>/<see cref="OnDisable"/> del propio panel — perfecto para pantallas de menú que se
/// abren y cierran con <c>SetActive</c>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ProceduralAmbientSparkles : MonoBehaviour
{
    [Tooltip("Nº de chispas simultáneas. Para un panel pequeño de HUD, 4-6 basta; para un menú grande, 10-14.")]
    [SerializeField, Range(2, 20)] int _poolSize = 6;

    [Tooltip("Chispas más lentas y espaciadas que en una secuencia de sueño a pantalla completa (esto es ambiente, no protagonista).")]
    [SerializeField] float _spawnMin = 0.6f;
    [SerializeField] float _spawnMax = 1.8f;

    DreamSparkleOverlay _overlay;

    void Awake()
    {
        var go = new GameObject("AmbientSparkles") { hideFlags = HideFlags.DontSave };
        go.SetActive(false); // evita que DreamSparkleOverlay.Awake() se dispare antes de configurarlo

        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _overlay = go.AddComponent<DreamSparkleOverlay>();
        _overlay.ConfigurePace(_poolSize, _spawnMin, _spawnMax);

        go.SetActive(true); // ahora sí corre Awake(), ya con el ritmo de "ambiente" en vez del de "sueño"
    }

    void OnEnable()
    {
        if (_overlay != null) _overlay.StartSparkles();
    }

    void OnDisable()
    {
        if (_overlay != null) _overlay.StopSparkles();
    }
}
