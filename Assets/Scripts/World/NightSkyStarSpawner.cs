using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Domo de estrellas doradas hechas de GameObjects reales (no un truco de shader en el skybox ni
/// un ParticleSystem): mallas Quad pequeñas repartidas sobre una esfera alrededor del jugador,
/// tema "Sendero de las Estrellas". Mismo patrón estructural que CloudCoverSpawner (rejilla/reparto
/// + pool con SetActive, sin volver a Instantiate en cada ciclo + fundido de alfa vía
/// MaterialPropertyBlock + recentrado periódico, no por frame) para no introducir un patrón nuevo
/// en el proyecto — ver ese script para el razonamiento completo de cada decisión de rendimiento.
///
/// No depende de ningún asset externo: la textura de cada estrella (un punto de luz con caída
/// suave) se genera una vez en memoria en Awake, así no hace falta importar ni un sprite ni un
/// material a mano para tener algo que enseñar y ajustar en el Inspector.
///
/// Se activa/desactiva por DayNightCycle.TimeOfDayChanged (aparece entrando en Noche, se apaga
/// entrando en Amanecer) en vez de por lluvia — evento distinto, mismo mecanismo de suscripción
/// que ya usa CloudCoverSpawner con CloudsBuildingUp/RainStopped. También se oculta mientras el
/// cielo está cubierto de nubes de tormenta (no tiene sentido ver estrellas a través de un techo
/// de nubes) y mientras el jugador está en un interior.
///
/// 20 ago 2026 — FIX "las estrellas no brillan y el color es muy apagado": antes de este fix cada
/// estrella tenía un brillo FIJO sorteado una vez (brightnessVariance) y no había animación de
/// parpadeo por diseño (el comentario original de brightnessVariance explicaba que sustituía a
/// "una animación de parpadeo por frame" para ahorrar coste) — el domo se veía como un cielo
/// estrellado estático, sin vida. Ahora cada estrella SÍ parpadea de verdad (ver
/// <see cref="twinkleIntensity"/>/<see cref="twinkleSpeedRange"/>, cada una con su propia fase y
/// velocidad para que no parpadeen a la vez), recalculado cada <see cref="twinkleUpdateInterval"/>
/// segundos (no cada frame) para repartir el coste de escribir hasta <see cref="starCount"/>
/// MaterialPropertyBlock. Además, <see cref="starColor"/> ha subido de un dorado plano
/// (1, 0.85, 0.55) a un dorado más saturado y por encima de 1 en sus canales — con el pipeline de
/// este proyecto en HDR y Bloom activo (ver Assets/Settings/PC_RPAsset.asset /
/// Assets/Settings/DefaultVolumeProfile.asset) un color por encima de 1 puede aportar brillo extra
/// vía Bloom sin tocar el shader (que se deja en Sprites/Default, built-in y ya probado en URP);
/// si el Volume de la escena en juego tiene el Bloom con intensidad muy baja, el color simplemente
/// se ve más saturado, sin regresión frente al valor anterior. Se añade también
/// <see cref="starColorAlt"/> (unas pocas estrellas plateadas/azuladas sueltas, ver
/// <see cref="altStarChance"/>) para romper la monotonía del domo completamente dorado.
/// </summary>
public class NightSkyStarSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("DayNightCycle a escuchar. Si es null, se busca uno en la escena en Awake (una sola vez).")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Domo de estrellas")]
    [Tooltip("Número de estrellas del domo. Se construye UNA vez y queda fijo (igual que el techo de nubes), con recentrado periódico para seguir cubriendo el cielo según el jugador explora.")]
    [SerializeField] private int starCount = 220;
    [Tooltip("Radio de la esfera de estrellas alrededor del jugador.")]
    [SerializeField] private float domeRadius = 220f;
    [Tooltip("Elevación mínima sobre el horizonte (grados) a la que se colocan estrellas. Evita estrellas pegadas al suelo/montañas que nunca se llegan a ver bien.")]
    [SerializeField, Range(0f, 40f)] private float minElevationDegrees = 8f;
    [Tooltip("Tamaño mínimo/máximo (unidades de mundo) de cada estrella.")]
    [SerializeField] private Vector2 sizeRange = new Vector2(0.6f, 1.8f);
    [Tooltip("Color base de las estrellas — dorado cálido por defecto, coherente con 'El Sendero de las Estrellas'. Canales por encima de 1 a propósito (ver comentario de clase, fix 20 ago 2026): con Bloom activo en el Volume de la escena da un brillo extra; sin Bloom, se ve como un dorado saturado normal. Cada estrella varía ligeramente su brillo (ver brightnessVariance) para que el domo no se vea uniforme.")]
    [SerializeField] private Color starColor = new Color(1.6f, 1.2f, 0.55f);
    [Tooltip("Color alternativo 'frío' (plateado/azulado) que adoptan algunas estrellas sueltas — ver altStarChance. Rompe la monotonía de un domo enteramente dorado, como en un cielo real con estrellas de distinto tono.")]
    [SerializeField] private Color starColorAlt = new Color(1.3f, 1.4f, 1.8f);
    [Tooltip("Probabilidad (0-1) de que una estrella dada use starColorAlt en vez de starColor. Bajo a propósito (por defecto ~1 de cada 8) para que el domo siga leyéndose como 'dorado' con solo unos pocos acentos fríos.")]
    [SerializeField, Range(0f, 1f)] private float altStarChance = 0.12f;
    [Tooltip("Cuánto varía el brillo BASE de estrella a estrella (0 = todas iguales, 1 = algunas casi blancas y otras muy tenues). Se combina multiplicando con el parpadeo animado (ver twinkleIntensity) — esto solo fija el 'techo' de brillo de cada estrella, ya no sustituye a una animación por frame como antes del fix del 20 ago 2026.")]
    [SerializeField, Range(0f, 1f)] private float brightnessVariance = 0.6f;
    [Tooltip("Resolución (en píxeles) de la textura de estrella generada en memoria. No hace falta subirla mucho: son puntos pequeños en pantalla.")]
    [SerializeField] private int starTextureSize = 32;

    [Header("Parpadeo (fix 20 ago 2026 — antes las estrellas no brillaban, brillo fijo)")]
    [Tooltip("Cuánto varía el brillo de cada estrella con el tiempo. 0 = sin parpadeo (comportamiento antiguo, brillo fijo). 1 = en el valle de su ciclo casi se apaga del todo. Cada estrella tiene su propia fase y velocidad (ver twinkleSpeedRange) para que no parpadeen todas a la vez ni en fase.")]
    [SerializeField, Range(0f, 1f)] private float twinkleIntensity = 0.55f;
    [Tooltip("Rango de velocidad de parpadeo (ciclos por segundo, aprox.) — cada estrella sortea su propia velocidad dentro de este rango UNA vez, en Awake/BuildDomeIfNeeded.")]
    [SerializeField] private Vector2 twinkleSpeedRange = new Vector2(0.4f, 1.6f);
    [Tooltip("Cada cuántos segundos se recalcula el parpadeo de todas las estrellas. El parpadeo es lento (menos de 2 ciclos/seg como mucho), así que no hace falta todos los frames: con este intervalo se reparte mejor el coste de escribir hasta starCount MaterialPropertyBlock por Update.")]
    [SerializeField] private float twinkleUpdateInterval = 0.05f;

    [Header("Cobertura total del mundo")]
    [Tooltip("Igual que CloudCoverSpawner: cada 'recenterCheckInterval' segundos se comprueba la distancia del jugador al centro actual del domo y, si supera la mitad de domeRadius, se recoloca (solo se mueve _root, sin volver a instanciar nada).")]
    [SerializeField] private float recenterCheckInterval = 2f;

    [Header("Transición")]
    [Tooltip("Segundos que tardan las estrellas en aparecer/disiparse.")]
    [SerializeField] private float fadeDuration = 6f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform _root;
    private Transform _followTransform;
    private readonly List<Renderer> _renderers = new List<Renderer>();
    private readonly List<float> _rendererBrightness = new List<float>();
    private readonly List<float> _twinklePhase = new List<float>();
    private readonly List<float> _twinkleSpeed = new List<float>();
    private readonly List<Color> _starTint = new List<Color>();
    private MaterialPropertyBlock _mpb;
    private Material _starMaterial;
    private Texture2D _starTexture;
    private Coroutine _fadeCoroutine;
    private float _currentAlpha;
    private float _recenterTimer;
    private float _twinkleTimer;
    private bool _built;
    private bool _suppressedIndoors;

    void Awake()
    {
        if (dayNightCycle == null)
            dayNightCycle = FindAnyObjectByType<DayNightCycle>();

        _mpb = new MaterialPropertyBlock();
        BuildStarMaterial();
    }

    void OnEnable()
    {
        if (dayNightCycle != null)
            dayNightCycle.TimeOfDayChanged += HandleTimeOfDayChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
            Debug.LogWarning("[NightSkyStarSpawner] No se encontró ningún DayNightCycle en la escena; el domo de estrellas nunca se activará.");
#endif

        EnvironmentController.OnInteriorEntered += HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  += HandleInteriorExited;

        var ec = EnvironmentController.Instance;
        _suppressedIndoors = ec != null && ec.CurrentMode == EnvironmentMode.Interior;

        // Igual que DayNightCycle.InitializeCycleDelayed: un frame de margen para que el orden de
        // Awake/OnEnable entre escenas/scripts no nos deje mirando un CurrentTimeOfDay todavía sin
        // inicializar. Si la escena arranca ya de noche, esto hace aparecer el domo sin esperar al
        // siguiente cambio de franja.
        StartCoroutine(CheckInitialStateDelayed());
    }

    IEnumerator CheckInitialStateDelayed()
    {
        yield return null;
        if (dayNightCycle != null && dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Night)
        {
            BuildDomeIfNeeded();
            StartFade(1f);
        }
    }

    void OnDisable()
    {
        if (dayNightCycle != null)
            dayNightCycle.TimeOfDayChanged -= HandleTimeOfDayChanged;

        EnvironmentController.OnInteriorEntered -= HandleInteriorEntered;
        EnvironmentController.OnInteriorExited  -= HandleInteriorExited;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        DestroyDome();
    }

    void OnDestroy()
    {
        if (_starMaterial != null) Destroy(_starMaterial);
        if (_starTexture != null) Destroy(_starTexture);
    }

    void HandleTimeOfDayChanged(DayNightCycle.TimeOfDay t)
    {
        if (t == DayNightCycle.TimeOfDay.Night)
        {
            BuildDomeIfNeeded();
            StartFade(1f);
        }
        else if (t == DayNightCycle.TimeOfDay.Morning)
        {
            StartFade(0f);
        }
    }

    void HandleInteriorEntered()
    {
        _suppressedIndoors = true;
        if (_root != null) _root.gameObject.SetActive(false);
    }

    void HandleInteriorExited()
    {
        _suppressedIndoors = false;
        if (_root != null && _currentAlpha > 0f) _root.gameObject.SetActive(true);
    }

    void Update()
    {
        if (!_built || _currentAlpha <= 0f || _root == null) return;

        _recenterTimer += Time.deltaTime;
        if (_recenterTimer >= recenterCheckInterval)
        {
            _recenterTimer = 0f;
            CheckRecenter();
        }

        // Parpadeo: no hace falta todos los frames (ver tooltip de twinkleUpdateInterval). Durante
        // un fundido en curso, FadeRoutine ya llama a ApplyAlpha cada frame (el parpadeo se anima
        // ahí también, de propina); esto es lo que lo mantiene vivo en estado estable (alfa=1, sin
        // ningún fundido en marcha).
        _twinkleTimer += Time.deltaTime;
        if (_twinkleTimer >= twinkleUpdateInterval)
        {
            _twinkleTimer = 0f;
            ApplyAlpha(_currentAlpha);
        }
    }

    /// <summary>Mismo mecanismo que CloudCoverSpawner.CheckRecenter (ver ese script): recoloca el
    /// domo YA CONSTRUIDO cuando el jugador se acerca al borde, sin volver a instanciar nada.</summary>
    void CheckRecenter()
    {
        Transform playerT = PlayerService.Player != null ? PlayerService.Player.transform : _followTransform;
        if (playerT == null) return;

        float recenterThreshold = domeRadius * 0.5f;
        if ((playerT.position - _root.position).sqrMagnitude <= recenterThreshold * recenterThreshold) return;

        _root.position = playerT.position;
        _followTransform = playerT;
    }

    void BuildDomeIfNeeded()
    {
        if (_built) return;

        _followTransform = PlayerService.Player != null ? PlayerService.Player.transform :
                            Camera.main != null ? Camera.main.transform : null;

        _root = new GameObject("[NightSkyStars]").transform;
        if (_followTransform != null)
            _root.position = _followTransform.position;

        // Reparto uniforme en la mitad superior de una esfera (espiral de Fibonacci): da una
        // distribución pareja sin el patrón de rejilla que sí tiene sentido para un techo de nubes
        // plano, pero se notaría como "cuadrícula" en un domo celeste.
        float minElevationRad = minElevationDegrees * Mathf.Deg2Rad;
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        int placed = 0;
        int attempts = 0;
        int maxAttempts = starCount * 4;

        while (placed < starCount && attempts < maxAttempts)
        {
            attempts++;
            // y en [0,1]: solo mitad superior de la esfera (cielo, no bajo el horizonte).
            float y = (attempts % starCount) / (float)Mathf.Max(1, starCount - 1);
            float radiusAtY = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float theta = goldenAngle * attempts;
            Vector3 dir = new Vector3(Mathf.Cos(theta) * radiusAtY, y, Mathf.Sin(theta) * radiusAtY);

            if (Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) < minElevationRad) continue;

            SpawnStar(dir);
            placed++;
        }

        _currentAlpha = 0f;
        ApplyAlpha(0f);
        _built = true;

        if (_suppressedIndoors)
            _root.gameObject.SetActive(false);
    }

    void SpawnStar(Vector3 direction)
    {
        var instance = GameObject.CreatePrimitive(PrimitiveType.Quad);
        instance.name = "Star";

        // No hace falta colisión para un punto de luz decorativo lejano — igual que las nubes,
        // que se marcan sin sombra propia ni recibida (ver CollectRenderers de CloudCoverSpawner).
        var collider = instance.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        instance.transform.SetParent(_root, false);
        instance.transform.localPosition = direction * domeRadius;
        // El quad mira hacia el centro del domo (donde está el jugador). El material es de tipo
        // sprite (doble cara, ver BuildStarMaterial), así que no importa si el winding queda "al
        // revés" — se ve igual desde dentro del domo.
        instance.transform.localRotation = Quaternion.LookRotation(-direction);
        float size = UnityEngine.Random.Range(sizeRange.x, sizeRange.y);
        instance.transform.localScale = Vector3.one * size;

        var renderer = instance.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = _starMaterial;

        _renderers.Add(renderer);
        _rendererBrightness.Add(1f - UnityEngine.Random.value * brightnessVariance);
        _twinklePhase.Add(UnityEngine.Random.Range(0f, Mathf.PI * 2f));
        _twinkleSpeed.Add(UnityEngine.Random.Range(twinkleSpeedRange.x, twinkleSpeedRange.y));
        _starTint.Add(UnityEngine.Random.value < altStarChance ? starColorAlt : starColor);
    }

    /// <summary>
    /// Textura de una estrella (punto con caída suave hacia los bordes) generada en memoria, sin
    /// depender de ningún sprite importado. Se construye UNA vez y la comparten todas las estrellas
    /// vía el mismo Material — el brillo/alfa individual de cada una se anima aparte con
    /// MaterialPropertyBlock (igual que CloudCoverSpawner.ApplyAlpha), sin duplicar el material.
    /// </summary>
    void BuildStarMaterial()
    {
        int size = Mathf.Max(8, starTextureSize);
        _starTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = size * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float falloff = Mathf.Clamp01(1f - dist);
                // Cuadrado del falloff: núcleo brillante pequeño con caída suave, se lee como un
                // punto de luz en vez de un círculo plano.
                float alpha = falloff * falloff;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        _starTexture.SetPixels32(pixels);
        _starTexture.Apply(false, true);

        // Sprites/Default: shader built-in, siempre disponible, alpha blend y SIN cull (doble cara)
        // — evita depender de saber el winding exacto del Quad para que se vea desde dentro del domo.
        _starMaterial = new Material(Shader.Find("Sprites/Default"));
        _starMaterial.mainTexture = _starTexture;
        _starMaterial.enableInstancing = true;
    }

    void StartFade(float target)
    {
        if (_fadeCoroutine != null)
            StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(target));
    }

    IEnumerator FadeRoutine(float target)
    {
        if (target > 0f && _root != null && !_suppressedIndoors)
            _root.gameObject.SetActive(true);

        float duration = Mathf.Max(0.01f, fadeDuration);
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

        if (target <= 0f && _root != null)
            _root.gameObject.SetActive(false);
    }

    /// <summary>
    /// Recalcula el color+alfa de TODAS las estrellas para el alfa global dado. Se llama tanto
    /// desde FadeRoutine (cada frame, mientras el domo aparece/desaparece) como desde Update() cada
    /// twinkleUpdateInterval segundos en estado estable — así el parpadeo (fix 20 ago 2026, ver
    /// comentario de clase) sigue vivo aunque no haya ningún fundido en curso.
    /// </summary>
    void ApplyAlpha(float alpha)
    {
        float time = Time.time;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null) continue;

            float brightness = i < _rendererBrightness.Count ? _rendererBrightness[i] : 1f;
            float phase = i < _twinklePhase.Count ? _twinklePhase[i] : 0f;
            float speed = i < _twinkleSpeed.Count ? _twinkleSpeed[i] : 1f;
            // Oscila entre (1 - twinkleIntensity) y 1: nunca más brillante que el "techo" fijado
            // por brightnessVariance, solo se atenúa periódicamente — así twinkleIntensity=0 da
            // exactamente el comportamiento anterior (brillo fijo) sin ramas especiales.
            float twinkle = Mathf.Lerp(1f - twinkleIntensity, 1f, Mathf.Sin(time * speed + phase) * 0.5f + 0.5f);

            Color c = i < _starTint.Count ? _starTint[i] : starColor;
            c.a = alpha * brightness * twinkle;

            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    void DestroyDome()
    {
        if (_root != null)
            Destroy(_root.gameObject);

        _root = null;
        _renderers.Clear();
        _rendererBrightness.Clear();
        _twinklePhase.Clear();
        _twinkleSpeed.Clear();
        _starTint.Clear();
        _built = false;
        _currentAlpha = 0f;
    }

    [ContextMenu("Activar/Desactivar domo de estrellas (debug)")]
    public void DebugToggleDome()
    {
        if (_built && _currentAlpha > 0f)
            StartFade(0f);
        else
        {
            BuildDomeIfNeeded();
            StartFade(1f);
        }
    }
}
