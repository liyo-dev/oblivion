using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Genera un techo de nubes estilizadas (mallas con el shader Quibli/Cloud3D — recomendado, ver
/// prefabs QuibliRainCloud3D_1..4 en Assets/Prefabs/VFX/ — quads con Quibli/Cloud2D, o cualquier
/// otro prefab de nube que se asigne) alrededor del jugador la PRIMERA vez que el cielo se está
/// nublando, para que se vea un cielo literalmente cubierto de nubes (sin skybox visible entre
/// huecos) en vez de solo un cambio de material de skybox.
///
/// El techo se construye UNA sola vez y luego queda FIJO en el mundo (no sigue al jugador frame
/// a frame). En lluvias posteriores se reutiliza el mismo conjunto de mallas ya instanciadas
/// (pool manual vía SetActive, sin volver a golpear Instantiate/Destroy) y solo se anima el
/// fundido de alfa al aparecer/disiparse.
///
/// Totalmente desacoplado de DayNightCycle: se limita a escuchar sus eventos
/// (CloudsBuildingUp / RainStopped), tal como pide la arquitectura del proyecto (comunicación
/// entre sistemas por eventos C#, no referencias directas entre managers).
///
/// 13 ago 2026: restaurado el modo QuibliCloud3D (había existido antes, en el commit
/// cf6ca2002/bfb27c983, y se revirtió en 81d65a9cc por daño colateral en OTROS materiales del
/// proyecto — no por un problema de este sistema en sí). El alcance de ESTE componente sigue
/// siendo el mismo que ya existía: el techo completo como aviso previo a la lluvia/tormenta real.
///
/// 15 ago 2026: la nubosidad ambiental independiente (TDD.md §16 Parte B, antes aparcada) ya está
/// implementada, pero en un componente aparte: <see cref="AmbientCloudDirector"/> +
/// <see cref="AmbientCloudDrifter"/>. Ese sistema se ocupa de las nubes sueltas que van y vienen
/// sin relación con la lluvia (y de dejar el cielo nublado un rato tras una tormenta, aclarando
/// poco a poco); este sigue encargándose solo del techo denso de tormenta real.
/// </summary>
public class CloudCoverSpawner : MonoBehaviour
{
    public enum CloudShaderMode { QuibliCloud3D, QuibliCloud2D, LegacyBaseColor }

    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Nubes")]
    [Tooltip("Prefabs de nube a repartir por el techo. Recomendado: QuibliRainCloud3D_1..4 (Assets/Prefabs/VFX/), mallas Cloud3D de Quibli como las del [Demo] SampleSceneWithQuibli. Se elige uno al azar por instancia.")]
    [SerializeField] private GameObject[] cloudPrefabs;
    [Tooltip("Altura sobre el jugador a la que se coloca el CENTRO del techo de nubes la PRIMERA vez que se construye (después el techo queda fijo en el mundo, no vuelve a recalcularse aunque el jugador se mueva). Las nubes NO tienen collider, así que si el jugador puede volar (PlayerFlyingController) las atraviesa sin más: por debajo se ve el cielo cubierto, por encima el cielo/skybox normal (el skybox nunca se toca, así que el sol sigue ahí arriba). Si minClearanceAboveFollowTarget detecta que esta altura no basta para las mallas ya escaladas, se sube automáticamente.")]
    [SerializeField] private float cloudHeight = 60f;
    [Tooltip("Radio horizontal alrededor del jugador que cubre el techo de nubes. Cuanto más grande, menos se nota el borde del área cubierta, pero más instancias hacen falta.")]
    [SerializeField] private float coverRadius = 150f;
    [Tooltip("Separación aproximada entre nubes de la rejilla. Más bajo = más denso = tapa mejor el cielo, pero más nubes instanciadas (y más overdraw con quads transparentes).")]
    [SerializeField] private float cellSize = 30f;
    [Tooltip("Variación aleatoria de posición dentro de cada celda de la rejilla, para que no se note el patrón regular.")]
    [SerializeField, Range(0f, 1f)] private float jitter = 0.5f;
    [Tooltip("Escala mínima/máxima aplicada a cada nube, MULTIPLICANDO la escala base del prefab (los QuibliRainCloud3D_X ya vienen normalizados a ~25-33 unidades de ancho a escala 1). Con 0.8-1.5 y cellSize 30 las nubes se tocan/solapan lo justo para leerse como un techo de tormenta sin dejar huecos grandes. minClearanceAboveFollowTarget protege contra el caso de que la cámara acabe dentro de una nube.")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.8f, 1.5f);
    [Tooltip("Límite de seguridad de instancias, por si coverRadius/cellSize generan una rejilla enorme.")]
    [SerializeField] private int maxCloudInstances = 300;
    [Tooltip("Variación aleatoria de altura (±) de cada nube respecto al plano del techo. Rompe el plano perfecto (más natural) y evita que todos los quads transparentes queden coplanares, lo que provoca artefactos de ordenación al mirarlos desde abajo.")]
    [SerializeField] private float heightJitter = 12f;
    [Tooltip("Margen mínimo, en unidades de mundo, entre el punto más bajo de la malla de nubes ya instanciada/escalada y el jugador. Tras construir el techo se mide su altura REAL (no solo cloudHeight) y si no deja este margen, se sube el techo entero lo que haga falta. Es la protección contra 'la cámara se queda dentro de la nube' si cloudHeight/scaleRange quedan mal calibrados para el prefab que uses.")]
    [SerializeField] private float minClearanceAboveFollowTarget = 25f;

    [Header("Cobertura total del mundo (FIX INC-074)")]
    [Tooltip("El techo se construye UNA vez y queda fijo (ver comentario de clase), así que solo cubre 'coverRadius' unidades alrededor del punto donde se activó por primera vez. Si el jugador se aleja lo bastante, sale por el borde y ve el skybox despejado en vez de nubes. Cada 'recenterCheckInterval' segundos se comprueba la distancia del jugador al centro actual y, si supera la mitad de coverRadius, se recoloca el techo YA CONSTRUIDO (solo se mueve _root, sin volver a instanciar nada) centrado en la nueva posición — así las nubes acaban cubriendo todo el mundo según se explora, sin volver a seguir al jugador cada frame (que era lo que causaba el temblor que motivó fijarlo originalmente).")]
    [SerializeField] private float recenterCheckInterval = 2f;

    private float _recenterTimer;

    [Header("Aspecto de tormenta")]
    [Tooltip("Color de sombreado de tormenta. Solo se usa en modo QuibliCloud2D (_ShadowColor) y LegacyBaseColor (_BaseColor). En QuibliCloud3D no hace falta: el tono tormentoso lo pone la propia luz de la escena al oscurecerse con la lluvia.")]
    [SerializeField] private Color stormCloudColor = new Color(0.42f, 0.43f, 0.47f);
    [Tooltip("Cuánto sombreado de tormenta (_ShadowAmount de Quibli/Cloud2D) tienen las nubes una vez formadas del todo. 0 = nubes blancas de buen tiempo, 1 = panza de tormenta muy marcada. Se anima junto al alfa: mientras la nube aparece también se va oscureciendo.")]
    [SerializeField, Range(0f, 1f)] private float stormShadowAmount = 0.55f;
    [Tooltip("Shader de los prefabs de nube. QuibliCloud3D (por defecto y recomendado): mallas del Foliage Generator con Quibli/Cloud3D; el fundido 'erosiona' el recorte de alfa (_AlphaThreshold), con efecto de materializarse/disiparse — así las nubes 'vienen y se van' en vez de aparecer/desaparecer de golpe. QuibliCloud2D: quads con Quibli/Cloud2D (_Opacity + _ShadowColor/_ShadowAmount). LegacyBaseColor: comportamiento antiguo (_BaseColor con alfa) para mallas tipo Low Poly.")]
    [SerializeField] private CloudShaderMode cloudShaderMode = CloudShaderMode.QuibliCloud3D;
    [Tooltip("Solo con QuibliCloud3D: valor de _AlphaThreshold cuando la nube está formada del todo (0.5 en el material del demo de Quibli, SampleScene_Cloud3D.mat). El fundido anima desde 1 (invisible) hasta este valor.")]
    [SerializeField, Range(0.05f, 1f)] private float visibleAlphaThreshold = 0.5f;
    [Tooltip("Solo con QuibliCloud2D: activa el billboard del shader (_Billboard) para que cada quad mire siempre a cámara. Si se desactiva, los quads se tumban mirando al suelo con giro aleatorio (aspecto de 'techo' plano).")]
    [SerializeField] private bool billboard = true;

    [Header("Transición")]
    [Tooltip("Si es true, usa DayNightCycle.RainDarkenTransitionDuration para sincronizar la aparición/disipación con el oscurecimiento del cielo. Si es false, usa fadeDuration.")]
    [SerializeField] private bool syncWithRainDarken = true;
    [Tooltip("Segundos que tardan las nubes en aparecer/disiparse cuando syncWithRainDarken es false.")]
    [SerializeField] private float fadeDuration = 6f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // Propiedades del shader Quibli/Cloud2D (ver Assets/Plugins/Quibli/Shaders/Cloud2D.shadergraph).
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
    private static readonly int ShadowColorId = Shader.PropertyToID("_ShadowColor");
    private static readonly int ShadowAmountId = Shader.PropertyToID("_ShadowAmount");
    private static readonly int BillboardId = Shader.PropertyToID("_Billboard");
    // Propiedad del shader Quibli/Cloud3D (recorte de alfa que anima la formación/disipación).
    private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");

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

    void HandleCloudsBuildingUp()
    {
        if (!_built)
        {
            // _followTransform solo se usa aquí, para anclar el techo la primera vez que se
            // construye. Una vez construido queda fijo en el mundo: no hay LateUpdate que lo
            // reposicione por frame (eso era lo que hacía que las nubes "acompañaran" al jugador).
            _followTransform = PlayerService.Player != null ? PlayerService.Player.transform :
                                Camera.main != null ? Camera.main.transform : null;
            BuildCoverIfNeeded();
        }
        else if (_root != null)
        {
            // Pool: reactivar las mallas ya instanciadas en vez de Instantiate de nuevo.
            _root.gameObject.SetActive(true);
        }

        StartFade(1f);
    }

    void HandleRainStopped()
    {
        StartFade(0f);
    }

    void Update()
    {
        // Solo merece la pena comprobar el recentrado mientras el techo existe y es visible.
        if (!_built || _currentAlpha <= 0f || _root == null) return;

        _recenterTimer += Time.deltaTime;
        if (_recenterTimer < recenterCheckInterval) return;
        _recenterTimer = 0f;

        CheckRecenter();
    }

    /// <summary>
    /// FIX INC-074: ver tooltip de recenterCheckInterval. Recoloca el techo YA CONSTRUIDO (mover
    /// _root, sin Instantiate) cuando el jugador se acerca al borde de la cobertura actual, para
    /// que las nubes terminen cubriendo todo el mundo explorado en vez de quedarse ancladas al
    /// punto donde empezó a llover la primera vez.
    /// </summary>
    void CheckRecenter()
    {
        Transform playerT = PlayerService.Player != null ? PlayerService.Player.transform : _followTransform;
        if (playerT == null) return;

        Vector3 rootPos = _root.position;
        float dx = playerT.position.x - rootPos.x;
        float dz = playerT.position.z - rootPos.z;
        float distSqr = dx * dx + dz * dz;

        float recenterThreshold = coverRadius * 0.5f;
        if (distSqr <= recenterThreshold * recenterThreshold) return;

        Vector3 newPos = playerT.position;
        newPos.y = rootPos.y; // mantiene la altura ya calculada (incluye _safetyHeightBonus)
        _root.position = newPos;
        _followTransform = playerT;
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
                float y = UnityEngine.Random.Range(-heightJitter, heightJitter);

                var instance = Instantiate(prefab, _root);
                instance.transform.localPosition = new Vector3(x, y, z);
                instance.transform.localRotation = CloudRotation();
                instance.transform.localScale = prefab.transform.localScale * UnityEngine.Random.Range(scaleRange.x, scaleRange.y);

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
    /// Rotación inicial de cada nube. Con el shader Quibli en modo billboard la orientación real
    /// la decide el shader (siempre de cara a cámara), así que se deja identidad. Sin billboard,
    /// los quads se tumban mirando al suelo (90º en X) con giro aleatorio para variar. En modo
    /// Cloud3D/legacy (mallas 3D) se mantiene el giro aleatorio en Y de siempre.
    /// </summary>
    Quaternion CloudRotation()
    {
        if (cloudShaderMode == CloudShaderMode.QuibliCloud2D)
            return billboard
                ? Quaternion.identity
                : Quaternion.Euler(90f, UnityEngine.Random.Range(0f, 360f), 0f);

        // Mallas 3D (QuibliCloud3D o LegacyBaseColor): giro aleatorio en Y para variar siluetas.
        return Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
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
            DeactivateCover();
    }

    void ApplyAlpha(float alpha)
    {
        Color legacyColor = stormCloudColor;
        legacyColor.a = alpha;

        // Con Cloud2D, el sombreado de tormenta se anima junto al alfa: la nube aparece clara y
        // se va oscureciendo a medida que se hace opaca ("se carga de lluvia").
        float shadowAmount = stormShadowAmount * alpha;
        float billboardValue = billboard ? 1f : 0f;
        // Con Cloud3D, bajar el umbral desde >1 hasta el valor visible hace que la nube se
        // 'materialice' pixel a pixel (y se erosione al disiparse) — así "vienen y se van" en vez
        // de aparecer/desaparecer de golpe.
        float alphaThreshold = Mathf.Lerp(1.01f, visibleAlphaThreshold, alpha);

        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(_mpb);
            switch (cloudShaderMode)
            {
                case CloudShaderMode.QuibliCloud3D:
                    _mpb.SetFloat(AlphaThresholdId, alphaThreshold);
                    break;
                case CloudShaderMode.QuibliCloud2D:
                    _mpb.SetFloat(OpacityId, alpha);
                    _mpb.SetColor(ShadowColorId, stormCloudColor);
                    _mpb.SetFloat(ShadowAmountId, shadowAmount);
                    _mpb.SetFloat(BillboardId, billboardValue);
                    break;
                default:
                    _mpb.SetColor(BaseColorId, legacyColor);
                    break;
            }
            r.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>
    /// Oculta el techo de nubes sin destruir las mallas (pool): las deja desactivadas y fijas en
    /// su posición de mundo, listas para reaparecer en la próxima lluvia con solo un SetActive +
    /// fundido de alfa, sin volver a pagar el coste de Instantiate.
    /// </summary>
    void DeactivateCover()
    {
        if (_root != null)
            _root.gameObject.SetActive(false);

        _currentAlpha = 0f;
    }

    /// <summary>
    /// Limpieza real (destruye las mallas). Solo se usa cuando el propio componente se
    /// desactiva/destruye (p.ej. descarga de escena), no en cada ciclo de lluvia.
    /// </summary>
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

    /// <summary>
    /// Activa/desactiva SOLO los renderers del techo ya construido, sin tocar _currentAlpha, el
    /// fundido en curso ni el estado de la lluvia/tormenta real. Pensado para ocultar el techo un
    /// momento durante un enfoque de cámara puntual (ver FocusCameraNode) sin interferir con
    /// CloudsBuildingUp/RainStopped: al restaurar (visible=true) el techo vuelve exactamente al
    /// alfa que tenía antes de ocultarlo.
    /// </summary>
    public void SetRenderersVisible(bool visible)
    {
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r != null) r.enabled = visible;
        }
    }
}
