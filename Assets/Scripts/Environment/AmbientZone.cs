using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Zona ambiental: controla fog lejano, luz ambiente y niebla de suelo.
/// Requiere un Collider configurado como trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AmbientZone : MonoBehaviour
{
    [Header("Preset (Opcional)")]
    [Tooltip("Si se asigna, sus valores anulan toda configuración manual de fog y luz.")]
    [FormerlySerializedAs("ambientPreset")]
    [SerializeField] private AmbientPreset ambientPreset;

    [Header("Fog Lejano")]
    [SerializeField] private bool enableFog = true;
    [SerializeField] private Color fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    [Range(0f, 1f)]
    [SerializeField] private float fogDensity = 0.02f;
    [SerializeField] private float fogStartDistance = 0f;
    [SerializeField] private float fogEndDistance = 100f;
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;

    [Header("Luz Ambiente")]
    [Tooltip("Si está activo sobreescribe el color e intensidad de la luz ambiente de la escena.")]
    [SerializeField] private bool controlAmbientLight = false;
    [SerializeField] private Color ambientLightColor = Color.white;
    [Range(0f, 2f)]
    [SerializeField] private float ambientLightIntensity = 1f;

    [Header("Niebla de Suelo")]
    [Tooltip("Particle System de niebla volumétrica a ras de suelo. Debe estar configurado para hacer loop.")]
    [SerializeField] private bool enableGroundMist = false;
    [SerializeField] private ParticleSystem groundMistPS;

    [Header("Niebla de Cámara (Overlay)")]
    [Tooltip("Overlay de pantalla completa. Úsalo para simular estar dentro de niebla densa: " +
             "no depende de la distancia a cámara, tapa el entorno inmediato del jugador.")]
    [SerializeField] private bool enableCameraOverlay = false;
    [SerializeField] private Color overlayColor = new Color(0.85f, 0.88f, 0.92f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Opacidad máxima del overlay (0 = transparente, 1 = completamente opaco).")]
    [SerializeField] private float overlayMaxAlpha = 0.85f;

    [Header("Música")]
    [SerializeField] private bool changeMusic = true;
    [Tooltip("Debe coincidir con el 'Zone Id' en AudioGraphProfile → Ambient Zones.")]
    [SerializeField] private string musicZoneId = "";

    [Header("Transición")]
    [SerializeField] private float transitionDuration = 1.5f;
    [SerializeField] private Ease transitionEase = Ease.InOutSine;

    [Header("Prioridad")]
    [SerializeField] private int priority = 0;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // --- Defaults globales ---
    private static bool _defaultFogEnabled;
    private static Color _defaultFogColor;
    private static float _defaultFogDensity;
    private static float _defaultFogStart;
    private static float _defaultFogEnd;
    private static FogMode _defaultFogMode;
    private static Color _defaultAmbientColor;
    private static float _defaultAmbientIntensity;
    private static bool _defaultsCaptured;

    // --- Estado activo ---
    private static AmbientZone _currentActiveZone;
    private static Tween _currentTween;
    private static Tween _overlayTween;
    private static AudioClip _previousMusic;
    private static bool _wasMusicPlaying;

    // --- Camera overlay ---
    private static CanvasGroup _fogCanvasGroup;
    private static Image _fogImage;

    private Transform _playerTransform;
    private Transform _mistOriginalParent;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _defaultsCaptured = false;
        _currentActiveZone = null;
        _currentTween = null;
        _overlayTween = null;
        _previousMusic = null;
        _wasMusicPlaying = false;
        _fogCanvasGroup = null;
        _fogImage = null;
    }
#endif

    public static AmbientZone CurrentActiveZone => _currentActiveZone;
    public string MusicZoneId => musicZoneId;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[AmbientZone] Collider en '{gameObject.name}' configurado como trigger automáticamente");
        }

        CaptureDefaults();
    }

    private static void CaptureDefaults()
    {
        if (_defaultsCaptured) return;

        _defaultFogEnabled     = RenderSettings.fog;
        _defaultFogColor       = RenderSettings.fogColor;
        _defaultFogDensity     = RenderSettings.fogDensity;
        _defaultFogStart       = RenderSettings.fogStartDistance;
        _defaultFogEnd         = RenderSettings.fogEndDistance;
        _defaultFogMode        = RenderSettings.fogMode;
        _defaultAmbientColor   = RenderSettings.ambientLight;
        _defaultAmbientIntensity = RenderSettings.ambientIntensity;
        _defaultsCaptured = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerTransform = other.transform;

        if (_currentActiveZone != null && _currentActiveZone.priority > priority)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showDebugLogs)
                Debug.Log($"[AmbientZone] '{gameObject.name}' ignorada — '{_currentActiveZone.gameObject.name}' tiene mayor prioridad");
#endif
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugLogs)
            Debug.Log($"[AmbientZone] Jugador entró en '{gameObject.name}'");
#endif

        _currentActiveZone = this;
        ApplyZoneTransition();
        StartCoroutine(DeferredMusicTransition());
    }

    private System.Collections.IEnumerator DeferredMusicTransition()
    {
        yield return null;
        TransitionToZoneMusic();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_currentActiveZone != this) return;
        _playerTransform = null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugLogs)
            Debug.Log($"[AmbientZone] Jugador salió de '{gameObject.name}'");
#endif

        _currentActiveZone = null;
        StopGroundMist();
        HideCameraOverlay();
        TransitionToDefaultFog();
        RestorePreviousMusic();
    }

    // -------------------------------------------------------------------------
    //  Fog lejano + luz ambiente
    // -------------------------------------------------------------------------

    private void ApplyZoneTransition()
    {
        _currentTween?.Kill();

        // Resolver valores: preset > manual
        bool   targetEnableFog      = ambientPreset != null ? ambientPreset.enableFog          : enableFog;
        Color  targetFogColor       = ambientPreset != null ? ambientPreset.fogColor            : fogColor;
        float  targetDensity        = ambientPreset != null ? ambientPreset.fogDensity          : fogDensity;
        FogMode targetFogMode       = ambientPreset != null ? ambientPreset.fogMode             : fogMode;
        float  targetFogStart       = ambientPreset != null ? ambientPreset.fogStartDistance    : fogStartDistance;
        float  targetFogEnd         = ambientPreset != null ? ambientPreset.fogEndDistance      : fogEndDistance;
        float  duration             = ambientPreset != null ? ambientPreset.transitionDuration  : transitionDuration;
        Ease   ease                 = ambientPreset != null ? ambientPreset.transitionEase      : transitionEase;
        bool   targetControlLight   = ambientPreset != null ? ambientPreset.controlAmbientLight : controlAmbientLight;

        // Si la zona no controla la luz, el target es el default de la escena
        // → al entrar en zona B sin luz, la luz de zona A se restaura automáticamente
        Color targetAmbient  = targetControlLight
            ? (ambientPreset != null ? ambientPreset.ambientLightColor     : ambientLightColor)
            : _defaultAmbientColor;
        float targetAmbientI = targetControlLight
            ? (ambientPreset != null ? ambientPreset.ambientLightIntensity : ambientLightIntensity)
            : _defaultAmbientIntensity;

        RenderSettings.fog     = targetEnableFog;
        RenderSettings.fogMode = targetFogMode;

        float startDensity   = RenderSettings.fogDensity;
        Color startFogColor  = RenderSettings.fogColor;
        float startFogStart  = RenderSettings.fogStartDistance;
        float startFogEnd    = RenderSettings.fogEndDistance;
        Color startAmbient   = RenderSettings.ambientLight;
        float startAmbientI  = RenderSettings.ambientIntensity;

        _currentTween = DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity        = Mathf.Lerp(startDensity,  targetDensity,  t);
                RenderSettings.fogColor          = Color.Lerp(startFogColor, targetFogColor, t);
                RenderSettings.fogStartDistance  = Mathf.Lerp(startFogStart, targetFogStart, t);
                RenderSettings.fogEndDistance    = Mathf.Lerp(startFogEnd,   targetFogEnd,   t);
                RenderSettings.ambientLight      = Color.Lerp(startAmbient,  targetAmbient,  t);
                RenderSettings.ambientIntensity  = Mathf.Lerp(startAmbientI, targetAmbientI, t);
            },
            1f,
            duration
        ).SetEase(ease).SetUpdate(true);

        PlayGroundMist();
        ShowCameraOverlay();
    }

    private void TransitionToDefaultFog()
    {
        _currentTween?.Kill();

        float startDensity  = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd   = RenderSettings.fogEndDistance;
        Color startAmbient  = RenderSettings.ambientLight;
        float startAmbientI = RenderSettings.ambientIntensity;

        _currentTween = DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity       = Mathf.Lerp(startDensity,  _defaultFogDensity,       t);
                RenderSettings.fogColor         = Color.Lerp(startFogColor, _defaultFogColor,         t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, _defaultFogStart,         t);
                RenderSettings.fogEndDistance   = Mathf.Lerp(startFogEnd,   _defaultFogEnd,           t);
                RenderSettings.ambientLight     = Color.Lerp(startAmbient,  _defaultAmbientColor,     t);
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientI, _defaultAmbientIntensity, t);
            },
            1f,
            transitionDuration
        ).SetEase(transitionEase).SetUpdate(true).OnComplete(() => {
            RenderSettings.fog     = _defaultFogEnabled;
            RenderSettings.fogMode = _defaultFogMode;
        });
    }

    // -------------------------------------------------------------------------
    //  Niebla de suelo
    // -------------------------------------------------------------------------

    private void PlayGroundMist()
    {
        if (!enableGroundMist || groundMistPS == null) return;

        if (!groundMistPS.gameObject.activeSelf)
            groundMistPS.gameObject.SetActive(true);

        if (_playerTransform != null)
        {
            _mistOriginalParent = groundMistPS.transform.parent;
            groundMistPS.transform.SetParent(_playerTransform, false);
            groundMistPS.transform.localPosition = Vector3.zero;
        }

        if (!groundMistPS.isPlaying)
        {
            // prewarm: llena el volumen de partículas inmediatamente en lugar de acumular durante ~8s
            var main = groundMistPS.main;
            main.prewarm = true;
            groundMistPS.Play(true);
        }
    }

    private void StopGroundMist()
    {
        if (!enableGroundMist || groundMistPS == null) return;

        var restoreParent = _mistOriginalParent != null ? _mistOriginalParent : transform;
        groundMistPS.transform.SetParent(restoreParent, true);
        _mistOriginalParent = null;

        // StopEmitting: las partículas ya emitidas se desvanecen solas por su lifetime
        groundMistPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // -------------------------------------------------------------------------
    //  Camera overlay
    // -------------------------------------------------------------------------

    private static void EnsureFogCanvas()
    {
        if (_fogCanvasGroup != null) return;

        var go = new GameObject("[AmbientZone_CameraFog]");
        Object.DontDestroyOnLoad(go);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        go.AddComponent<CanvasScaler>();

        _fogCanvasGroup = go.AddComponent<CanvasGroup>();
        _fogCanvasGroup.alpha = 0f;
        _fogCanvasGroup.blocksRaycasts = false;
        _fogCanvasGroup.interactable = false;

        var imgGo = new GameObject("FogImage");
        imgGo.transform.SetParent(go.transform, false);
        _fogImage = imgGo.AddComponent<Image>();
        _fogImage.raycastTarget = false;

        var rect = _fogImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void ShowCameraOverlay()
    {
        if (!enableCameraOverlay) return;

        EnsureFogCanvas();
        _fogImage.color = overlayColor;

        float dur  = ambientPreset != null ? ambientPreset.transitionDuration : transitionDuration;
        Ease  ease = ambientPreset != null ? ambientPreset.transitionEase      : transitionEase;

        _overlayTween?.Kill();
        _overlayTween = _fogCanvasGroup.DOFade(overlayMaxAlpha, dur).SetEase(ease).SetUpdate(true);
    }

    private void HideCameraOverlay()
    {
        if (_fogCanvasGroup == null) return;

        _overlayTween?.Kill();
        _overlayTween = _fogCanvasGroup.DOFade(0f, transitionDuration).SetEase(transitionEase).SetUpdate(true);
    }

    // -------------------------------------------------------------------------
    //  Música
    // -------------------------------------------------------------------------

    private void TransitionToZoneMusic()
    {
        if (!changeMusic || string.IsNullOrEmpty(musicZoneId)) return;

        var audioService = AudioService.Instance;
        if (audioService == null) return;

        _previousMusic    = audioService.CurrentMusicClip;
        _wasMusicPlaying  = _previousMusic != null;

        if (audioService.profile == null) return;

        var rule = audioService.profile.GetAmbientZoneRule(musicZoneId);
        if (rule?.music != null)
        {
            audioService.PlayMusic(rule.music, rule.fade);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else if (showDebugLogs)
        {
            Debug.LogWarning($"[AmbientZone] No se encontró música para musicZoneId '{musicZoneId}'");
        }
#endif
    }

    private void RestorePreviousMusic()
    {
        if (!changeMusic) return;

        var audioService = AudioService.Instance;
        if (audioService == null) return;

        float fade = audioService.profile?.GetAmbientZoneRule(musicZoneId)?.fade ?? 1.5f;

        if (_wasMusicPlaying && _previousMusic != null)
            audioService.PlayMusic(_previousMusic, fade);
        else
            audioService.RestoreSceneMusic(fade);

        _previousMusic   = null;
        _wasMusicPlaying = false;
    }

    // -------------------------------------------------------------------------
    //  Ciclo de vida
    // -------------------------------------------------------------------------

    private void OnDestroy()
    {
        if (_currentActiveZone == this)
        {
            _currentActiveZone = null;
            _playerTransform = null;
            StopGroundMist();
            HideCameraOverlay();
            RestoreDefaultsImmediate();
        }
    }

    public static void RestoreDefaultsImmediate()
    {
        _currentTween?.Kill();
        _overlayTween?.Kill();
        if (_fogCanvasGroup != null) _fogCanvasGroup.alpha = 0f;
        if (!_defaultsCaptured) return;

        RenderSettings.fog              = _defaultFogEnabled;
        RenderSettings.fogColor         = _defaultFogColor;
        RenderSettings.fogDensity       = _defaultFogDensity;
        RenderSettings.fogStartDistance = _defaultFogStart;
        RenderSettings.fogEndDistance   = _defaultFogEnd;
        RenderSettings.fogMode          = _defaultFogMode;
        RenderSettings.ambientLight     = _defaultAmbientColor;
        RenderSettings.ambientIntensity = _defaultAmbientIntensity;
    }

    public static void RecaptureDefaults()
    {
        _defaultsCaptured = false;
        CaptureDefaults();
    }

    // -------------------------------------------------------------------------
    //  Editor
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        Color gizmoColor = enableFog ? fogColor : (controlAmbientLight ? ambientLightColor : Color.gray);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.25f);

        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
            Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
        }
    }

    private void OnDrawGizmosSelected()
    {
        string fogInfo   = enableFog ? $"Fog density:{fogDensity}" : "Sin fog";
        string lightInfo = controlAmbientLight ? $"Luz amb. ×{ambientLightIntensity}" : "";
        string mistInfo  = enableGroundMist && groundMistPS != null ? "Niebla suelo" : "";

        string[] parts = System.Array.FindAll(
            new[] { fogInfo, lightInfo, mistInfo },
            s => !string.IsNullOrEmpty(s)
        );

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 2f,
            $"AmbientZone: {gameObject.name}\n{string.Join(" | ", parts)}\nPrioridad: {priority}"
        );
    }

    [ContextMenu("Crear Niebla de Suelo")]
    private void CreateGroundMistPS()
    {
        if (groundMistPS != null)
        {
            UnityEditor.EditorUtility.DisplayDialog("AmbientZone",
                "Ya hay un Ground Mist PS asignado.\nDesasígnalo primero si quieres crear uno nuevo.", "OK");
            return;
        }

        var go = new GameObject("GroundMist_PS");
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Crear Niebla de Suelo");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();

        var boxCol = GetComponent<BoxCollider>();
        float areaX = boxCol != null ? boxCol.size.x : 12f;
        float areaZ = boxCol != null ? boxCol.size.z : 12f;

        // Main — partículas grandes, quietas, alpha bajo individual (se suma al solaparse)
        var main = ps.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed      = 0f;      // completamente inmóviles
        main.startSize       = new ParticleSystem.MinMaxCurve(areaX * 0.4f, areaX * 0.7f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(0.88f, 0.9f, 0.95f, 1f)
        );
        main.gravityModifier = 0f;
        main.maxParticles    = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission — alta tasa para que cubra toda el área sin huecos
        var emission = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = 40f;

        // Shape — caja que cubre desde el suelo hasta la altura del jugador
        // La emisión a distintas alturas hace que la niebla sea visible de cerca
        var shapeModule = ps.shape;
        shapeModule.enabled   = true;
        shapeModule.shapeType = ParticleSystemShapeType.Box;
        shapeModule.position  = new Vector3(0f, 0.75f, 0f);
        shapeModule.scale     = new Vector3(areaX, 1.5f, areaZ);

        // Color over lifetime — alpha bajo (0.18 máx): se acumula en capas y da sensación de densidad
        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        var fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.18f, 0.12f),
                new GradientAlphaKey(0.18f, 0.88f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        colorLife.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        // Size over lifetime — crecen muy levemente para disimular el reciclaje
        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.1f));

        // Noise — movimiento imperceptible para que no parezca estático
        var noise = ps.noise;
        noise.enabled   = true;
        noise.strength  = 0.08f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.05f;
        noise.quality   = ParticleSystemNoiseQuality.Medium;

        // Renderer — asigna tu material aquí
        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode   = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingOrder = 0;
        // Material: asígnalo manualmente desde el Inspector

        // Enlazar con el componente
        groundMistPS     = ps;
        enableGroundMist = true;

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"[AmbientZone] GroundMist_PS creado en '{gameObject.name}'. " +
                  "Asigna tu material al Renderer del hijo GroundMist_PS.");
    }
#endif
}
