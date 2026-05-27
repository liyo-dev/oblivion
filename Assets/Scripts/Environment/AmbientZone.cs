using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
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
    [Range(0f, 0.1f)]
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
    private static AudioClip _previousMusic;
    private static bool _wasMusicPlaying;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _defaultsCaptured = false;
        _currentActiveZone = null;
        _currentTween = null;
        _previousMusic = null;
        _wasMusicPlaying = false;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugLogs)
            Debug.Log($"[AmbientZone] Jugador salió de '{gameObject.name}'");
#endif

        _currentActiveZone = null;
        StopGroundMist();
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

        if (!groundMistPS.isPlaying)
            groundMistPS.Play(true);
    }

    private void StopGroundMist()
    {
        if (!enableGroundMist || groundMistPS == null) return;

        // StopEmitting: las partículas ya emitidas se desvanecen solos por su lifetime
        groundMistPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
            StopGroundMist();
            RestoreDefaultsImmediate();
        }
    }

    public static void RestoreDefaultsImmediate()
    {
        _currentTween?.Kill();
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

        // Main
        var main = ps.main;
        main.loop           = true;
        main.playOnAwake    = false; // AmbientZone lo gestiona
        main.startLifetime  = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed     = new ParticleSystem.MinMaxCurve(0f, 0.12f);
        main.startSize      = new ParticleSystem.MinMaxCurve(3f, 6f);
        main.startColor     = new ParticleSystem.MinMaxGradient(
            new Color(1f,    1f,    1f,    1f),
            new Color(0.85f, 0.88f, 0.93f, 1f)
        );
        main.gravityModifier  = -0.005f; // flota levemente hacia arriba
        main.maxParticles     = 100;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;

        // Emission
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = 10f;

        // Shape — caja plana al ras del suelo, escala del BoxCollider si existe
        var shapeModule = ps.shape;
        shapeModule.enabled   = true;
        shapeModule.shapeType = ParticleSystemShapeType.Box;
        shapeModule.position  = new Vector3(0f, 0.2f, 0f);
        var boxCol = GetComponent<BoxCollider>();
        shapeModule.scale = boxCol != null
            ? new Vector3(boxCol.size.x * 0.85f, 0.3f, boxCol.size.z * 0.85f)
            : new Vector3(12f, 0.3f, 12f);

        // Color over lifetime — fade in/out suave con opacidad máx ~35%
        var colorLife = ps.colorOverLifetime;
        colorLife.enabled = true;
        var fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.35f, 0.15f),
                new GradientAlphaKey(0.35f, 0.85f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        colorLife.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        // Size over lifetime — expansión lenta (hace que la niebla se "disuelva")
        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.85f, 1f, 1.25f));

        // Renderer
        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode  = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingOrder = 0;

        // Material — buscar uno ya creado, si no crearlo
        const string matPath = "Assets/Settings/AmbientPresets/Mat_GroundMist.mat";
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit");

            mat = new Material(shader != null ? shader : Shader.Find("Standard"));

            if (shader != null && shader.name.StartsWith("Universal"))
            {
                // Configurar como transparente en URP
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend",   0f);
                mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",    0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }

            UnityEditor.AssetDatabase.CreateAsset(mat, matPath);
            UnityEditor.AssetDatabase.SaveAssets();
        }
        psRenderer.material = mat;

        // Enlazar con el componente
        groundMistPS    = ps;
        enableGroundMist = true;

        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"[AmbientZone] GroundMist_PS creado en '{gameObject.name}'.\n" +
                  "Para mejor aspecto: asigna una textura circular y suave (tipo nube/humo) " +
                  "al material Mat_GroundMist en Assets/Settings/AmbientPresets/.");
    }
#endif
}
