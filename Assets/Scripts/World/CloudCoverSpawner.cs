using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera un techo de nubes 3D reales (mallas del pack Low Poly Modular Terrain, u otras que se
/// asignen) sobre el jugador cuando el cielo se está nublando, para que se vea un cielo
/// literalmente cubierto de nubes (sin skybox visible entre huecos) en vez de solo un cambio de
/// material de skybox.
///
/// Totalmente desacoplado de DayNightCycle: se limita a escuchar sus eventos
/// (CloudsBuildingUp / RainStopped), tal como pide la arquitectura del proyecto (comunicación
/// entre sistemas por eventos C#, no referencias directas entre managers).
/// </summary>
public class CloudCoverSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Nubes")]
    [Tooltip("Prefabs de malla de nube a repartir por el techo (p.ej. Cloud_01..04 del Low Poly Modular Terrain Pack). Se elige uno al azar por instancia.")]
    [SerializeField] private GameObject[] cloudPrefabs;
    [Tooltip("Altura sobre el jugador a la que se coloca el CENTRO del techo de nubes. Las nubes NO tienen collider (los prefabs Cloud_XX son solo malla+material), así que si el jugador puede volar (PlayerFlyingController) las atraviesa sin más: por debajo se ve el cielo cubierto, por encima el cielo/skybox normal (el skybox nunca se toca, así que el sol sigue ahí arriba). Si minClearanceAboveFollowTarget detecta que esta altura no basta para las mallas ya escaladas, se sube automáticamente.")]
    [SerializeField] private float cloudHeight = 45f;
    [Tooltip("Radio horizontal alrededor del jugador que cubre el techo de nubes. Cuanto más grande, menos se nota el borde del área cubierta, pero más instancias hacen falta.")]
    [SerializeField] private float coverRadius = 150f;
    [Tooltip("Separación aproximada entre nubes de la rejilla. Más bajo = más denso = tapa mejor el cielo, pero más nubes instanciadas.")]
    [SerializeField] private float cellSize = 18f;
    [Tooltip("Variación aleatoria de posición dentro de cada celda de la rejilla, para que no se note el patrón regular.")]
    [SerializeField, Range(0f, 1f)] private float jitter = 0.5f;
    [Tooltip("Escala mínima/máxima aplicada a cada nube instanciada. OJO: los prefabs Cloud_XX del Low Poly Modular Terrain Pack ya son grandes de por sí (en el pack se usan típicamente a escala 3-6); una escala muy alta aquí puede hacer que las nubes sean tan enormes que la cámara termine literalmente dentro de una (pantalla gris plana). minClearanceAboveFollowTarget protege contra eso, pero conviene no pasarse igualmente.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(5f, 9f);
    [Tooltip("Límite de seguridad de instancias, por si coverRadius/cellSize generan una rejilla enorme.")]
    [SerializeField] private int maxCloudInstances = 300;
    [Tooltip("Margen mínimo, en unidades de mundo, entre el punto más bajo de la malla de nubes ya instanciada/escalada y el jugador. Tras construir el techo se mide su altura REAL (no solo cloudHeight) y si no deja este margen, se sube el techo entero lo que haga falta. Es la protección contra 'la cámara se queda dentro de la nube' si cloudHeight/scaleRange quedan mal calibrados para el prefab que uses.")]
    [SerializeField] private float minClearanceAboveFollowTarget = 25f;

    [Header("Aspecto de tormenta")]
    [Tooltip("Color al que se tiñen las nubes al cubrir el cielo (el alfa se anima aparte, de 0 a 1).")]
    [SerializeField] private Color stormCloudColor = new Color(0.12f, 0.12f, 0.14f);

    [Header("Transición")]
    [Tooltip("Si es true, usa DayNightCycle.RainDarkenTransitionDuration para sincronizar la aparición/disipación con el oscurecimiento del cielo. Si es false, usa fadeDuration.")]
    [SerializeField] private bool syncWithRainDarken = true;
    [Tooltip("Segundos que tardan las nubes en aparecer/disiparse cuando syncWithRainDarken es false.")]
    [SerializeField] private float fadeDuration = 6f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Transform _root;
    private Transform _followTransform;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private MaterialPropertyBlock _mpb;
    private Coroutine _fadeCoroutine;
    private float _currentAlpha;
    private float _safetyHeightBonus;
    private bool _built;

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        _mpb = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp += HandleCloudsBuildingUp;
            dayNightCycle.RainStopped += HandleRainStopped;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning("[CloudCoverSpawner] No se encontró ningún DayNightCycle en la escena; el techo de nubes nunca se activará.");
        }
#endif
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.CloudsBuildingUp -= HandleCloudsBuildingUp;
            dayNightCycle.RainStopped -= HandleRainStopped;
        }

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        DestroyCover();
    }

    // Solo lectura de un Transform ya cacheado (_followTransform se resuelve una vez por evento de
    // nubosidad, nunca aquí) — no viola la regla de no buscar Camera.main/GetComponent en Update.
    void LateUpdate()
    {
        if (_root == null || _followTransform == null) return;

        Vector3 pos = _followTransform.position;
        pos.y += cloudHeight + _safetyHeightBonus;
        _root.position = pos;
    }

    void HandleCloudsBuildingUp()
    {
        _followTransform = PlayerService.Player != null ? PlayerService.Player.transform :
                            Camera.main != null ? Camera.main.transform : null;

        BuildCoverIfNeeded();
        StartFade(1f);
    }

    void HandleRainStopped()
    {
        StartFade(0f);
    }

    void BuildCoverIfNeeded()
    {
        if (_built || cloudPrefabs == null || cloudPrefabs.Length == 0) return;

        _root = new GameObject("[CloudCover]").transform;

        // Colocar ya el contenedor en su altura objetivo ANTES de instanciar, para que las nubes
        // nazcan en su posición de mundo real y podamos medir su altura real más abajo (si se
        // instancian en el origen y luego se mueve el root, mediríamos bounds que no representan
        // nada del mundo real todavía).
        _safetyHeightBonus = 0f;
        if (_followTransform != null)
        {
            Vector3 rootPos = _followTransform.position;
            rootPos.y += cloudHeight;
            _root.position = rootPos;
        }

        int half = Mathf.Max(1, Mathf.CeilToInt(coverRadius / Mathf.Max(1f, cellSize)));
        float radiusSqr = coverRadius * coverRadius;
        float jitterRange = cellSize * jitter * 0.5f;
        int spawned = 0;

        for (int gx = -half; gx <= half && spawned < maxCloudInstances; gx++)
        {
            for (int gz = -half; gz <= half && spawned < maxCloudInstances; gz++)
            {
                float baseX = gx * cellSize;
                float baseZ = gz * cellSize;
                if (baseX * baseX + baseZ * baseZ > radiusSqr) continue;

                var prefab = cloudPrefabs[UnityEngine.Random.Range(0, cloudPrefabs.Length)];
                if (prefab == null) continue;

                float x = baseX + UnityEngine.Random.Range(-jitterRange, jitterRange);
                float z = baseZ + UnityEngine.Random.Range(-jitterRange, jitterRange);

                var instance = Instantiate(prefab, _root);
                instance.transform.localPosition = new Vector3(x, 0f, z);
                instance.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                instance.transform.localScale = Vector3.one * UnityEngine.Random.Range(scaleRange.x, scaleRange.y);

                CollectRenderers(instance);
                spawned++;
            }
        }

        ApplySafetyClearance();

        _currentAlpha = 0f;
        ApplyAlpha(0f);
        _built = true;
    }

    /// <summary>
    /// Mide la altura REAL (bounds de los renderers ya instanciados y escalados, no solo
    /// cloudHeight) del techo de nubes recién construido. Si su punto más bajo queda a menos de
    /// minClearanceAboveFollowTarget del jugador, sube todo el techo lo que haga falta. Protege
    /// contra el caso "la cámara se queda literalmente dentro de una nube" (pantalla gris plana)
    /// si scaleRange/cloudHeight no encajan con el tamaño real de los prefabs asignados.
    /// </summary>
    void ApplySafetyClearance()
    {
        if (_followTransform == null || _renderers.Count == 0) return;

        Bounds combined = default;
        bool has = false;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;
            if (!has) { combined = r.bounds; has = true; }
            else combined.Encapsulate(r.bounds);
        }
        if (!has) return;

        float requiredMinY = _followTransform.position.y + minClearanceAboveFollowTarget;
        float deficit = requiredMinY - combined.min.y;
        if (deficit <= 0f) return;

        _safetyHeightBonus = deficit;
        _root.position += Vector3.up * deficit;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[CloudCoverSpawner] El techo de nubes no dejaba suficiente margen sobre el jugador (faltaban {deficit:F1} unidades); se ha subido automáticamente. Considera aumentar cloudHeight o reducir scaleRange para no depender de esta corrección.");
#endif
    }

    void CollectRenderers(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            // Techo de nubes lejano: ni necesita proyectar sombra ni recibirla.
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _renderers.Add(r);
        }
    }

    void StartFade(float target)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        float duration = syncWithRainDarken && dayNightCycle != null
            ? dayNightCycle.RainDarkenTransitionDuration
            : fadeDuration;
        duration = Mathf.Max(0.01f, duration);

        float start = _currentAlpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _currentAlpha = Mathf.Lerp(start, target, elapsed / duration);
            ApplyAlpha(_currentAlpha);
            yield return null;
        }

        _currentAlpha = target;
        ApplyAlpha(_currentAlpha);
        _fadeCoroutine = null;

        if (target <= 0f)
            DestroyCover();
    }

    void ApplyAlpha(float alpha)
    {
        Color c = stormCloudColor;
        c.a = alpha;

        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    void DestroyCover()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _root = null;
        _renderers.Clear();
        _built = false;
        _currentAlpha = 0f;
    }

    [ContextMenu("Activar/Desactivar techo de nubes (debug)")]
    public void DebugToggleCover()
    {
        if (_built && _currentAlpha > 0f)
            HandleRainStopped();
        else
            HandleCloudsBuildingUp();
    }
}
