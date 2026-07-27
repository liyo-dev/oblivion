using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;
using DG.Tweening;

public enum ZoneCameraMode
{
    SoloDistancia,
    Plataformas2D,
    TopDown,
    Isometrico,
    CorredorEstrecho,
    PanoramicoLejano,
}

/// <summary>
/// Zona ambiental: aplica un AmbientPreset al entrar y lo revierte al salir.
/// Requiere un Collider configurado como trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AmbientZone : MonoBehaviour
{
    [Header("Preset")]
    [Tooltip("Preset con toda la configuración ambiental de la zona.")]
    [FormerlySerializedAs("ambientPreset")]
    [SerializeField] private AmbientPreset ambientPreset;

    [Header("Niebla de Suelo")]
    [Tooltip("Particle System de niebla volumétrica a ras de suelo. Debe estar configurado para hacer loop.")]
    [SerializeField] private ParticleSystem groundMistPS;

    [Header("Niebla de Pies")]
    [Tooltip("GameObject (plano con shader) que cubre los pies del jugador. Se activa al entrar en la zona.")]
    [SerializeField] private GameObject footFogObject;

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

    // --- Cámara ---
    private struct CameraState { public float distance, height, yMin, yMax; }
    private static CameraState _defaultCameraState;
    private static bool _cameraDefaultsCaptured;
    private static Tween _cameraTween;
    private static vThirdPersonCamera _cachedCamera;

    private Transform _playerTransform;
    private Transform _mistOriginalParent;
    private Transform _footFogOriginalParent;

    // FIX INC-057: rotación de mundo original del plano de niebla de pies, fijada cada frame
    // mientras está parentado al jugador (ver PlayFootFog/LateUpdate).
    private Quaternion _footFogLockedRotation = Quaternion.identity;
    private bool _footFogRotationLocked;

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
        _cameraDefaultsCaptured = false;
        _cameraTween = null;
        _cachedCamera = null;
    }
#endif

    public static AmbientZone CurrentActiveZone => _currentActiveZone;
    public string MusicZoneId => ambientPreset != null ? ambientPreset.musicZoneId : "";

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

        _defaultFogEnabled       = RenderSettings.fog;
        _defaultFogColor         = RenderSettings.fogColor;
        _defaultFogDensity       = RenderSettings.fogDensity;
        _defaultFogStart         = RenderSettings.fogStartDistance;
        _defaultFogEnd           = RenderSettings.fogEndDistance;
        _defaultFogMode          = RenderSettings.fogMode;
        _defaultAmbientColor     = RenderSettings.ambientLight;
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
        StopFootFog();
        HideCameraOverlay();
        RestoreCameraDefaults();
        TransitionToDefaultFog();
        RestorePreviousMusic();
    }

    // -------------------------------------------------------------------------
    //  Fog lejano + luz ambiente
    // -------------------------------------------------------------------------

    private void ApplyZoneTransition()
    {
        if (ambientPreset == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[AmbientZone] '{gameObject.name}' no tiene AmbientPreset asignado.");
#endif
            PlayGroundMist();
            PlayFootFog();
            return;
        }

        _currentTween?.Kill();

        Color targetAmbient  = ambientPreset.controlAmbientLight ? ambientPreset.ambientLightColor     : _defaultAmbientColor;
        float targetAmbientI = ambientPreset.controlAmbientLight ? ambientPreset.ambientLightIntensity : _defaultAmbientIntensity;

        RenderSettings.fog     = ambientPreset.enableFog;
        RenderSettings.fogMode = ambientPreset.fogMode;

        float startDensity  = RenderSettings.fogDensity;
        Color startFogColor = RenderSettings.fogColor;
        float startFogStart = RenderSettings.fogStartDistance;
        float startFogEnd   = RenderSettings.fogEndDistance;
        Color startAmbient  = RenderSettings.ambientLight;
        float startAmbientI = RenderSettings.ambientIntensity;

        _currentTween = DOTween.To(
            () => 0f,
            t => {
                RenderSettings.fogDensity       = Mathf.Lerp(startDensity,  ambientPreset.fogDensity,       t);
                RenderSettings.fogColor         = Color.Lerp(startFogColor, ambientPreset.fogColor,         t);
                RenderSettings.fogStartDistance = Mathf.Lerp(startFogStart, ambientPreset.fogStartDistance, t);
                RenderSettings.fogEndDistance   = Mathf.Lerp(startFogEnd,   ambientPreset.fogEndDistance,   t);
                RenderSettings.ambientLight     = Color.Lerp(startAmbient,  targetAmbient,                  t);
                RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientI, targetAmbientI,                 t);
            },
            1f,
            ambientPreset.transitionDuration
        ).SetEase(ambientPreset.transitionEase).SetUpdate(true);

        PlayGroundMist();
        PlayFootFog();
        ShowCameraOverlay();
        ApplyCameraTransition();
    }

    private void TransitionToDefaultFog()
    {
        _currentTween?.Kill();

        float dur  = ambientPreset != null ? ambientPreset.transitionDuration : 1.5f;
        Ease  ease = ambientPreset != null ? ambientPreset.transitionEase      : Ease.InOutSine;

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
            dur
        ).SetEase(ease).SetUpdate(true).OnComplete(() => {
            RenderSettings.fog     = _defaultFogEnabled;
            RenderSettings.fogMode = _defaultFogMode;
        });
    }

    // -------------------------------------------------------------------------
    //  Niebla de suelo
    // -------------------------------------------------------------------------

    private void PlayGroundMist()
    {
        if (groundMistPS == null) return;

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
        if (groundMistPS == null) return;

        var restoreParent = _mistOriginalParent != null ? _mistOriginalParent : transform;
        groundMistPS.transform.SetParent(restoreParent, true);
        _mistOriginalParent = null;

        // StopEmitting: las partículas ya emitidas se desvanecen solas por su lifetime
        groundMistPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    // -------------------------------------------------------------------------
    //  Niebla de pies
    // -------------------------------------------------------------------------

    private void PlayFootFog()
    {
        if (footFogObject == null) return;

        // Igual que con groundMistPS: si el plano de niebla de pies queda fijo en el mundo,
        // su borde se ve como un "corte" en cuanto el jugador mira hacia el límite de la zona.
        // Al engancharlo al jugador, el plano viaja con él y ese borde nunca queda a la vista
        // dentro del área de la zona.
        if (_playerTransform != null)
        {
            _footFogOriginalParent = footFogObject.transform.parent;

            // FIX INC-057: SetParent(..., false) también hereda la ROTACIÓN del jugador, no solo
            // la posición. Como el plano no es cuadrado (ej: 70x30), al girar el jugador el borde
            // corto podía acercarse mucho a la cámara y hacerse visible como un "corte" — el
            // motivo por el que esta niebla se había desactivado en vez de arreglarse. Guardamos
            // la rotación de mundo original (plano, alineada al suelo) para re-fijarla cada frame
            // en LateUpdate: el plano sigue la POSICIÓN del jugador pero nunca gira con él.
            _footFogLockedRotation = footFogObject.transform.rotation;
            _footFogRotationLocked = true;

            footFogObject.transform.SetParent(_playerTransform, false);
            footFogObject.transform.localPosition = Vector3.zero;
            footFogObject.transform.rotation = _footFogLockedRotation;
        }

        if (!footFogObject.activeSelf)
            footFogObject.SetActive(true);
    }

    private void StopFootFog()
    {
        if (footFogObject == null) return;

        _footFogRotationLocked = false;

        var restoreParent = _footFogOriginalParent != null ? _footFogOriginalParent : transform;
        footFogObject.transform.SetParent(restoreParent, true);
        _footFogOriginalParent = null;

        if (footFogObject.activeSelf)
            footFogObject.SetActive(false);
    }

    private void LateUpdate()
    {
        // FIX INC-057: reafirmar la rotación del plano de niebla de pies cada frame. Al estar
        // parentado al jugador (para seguir su posición sin exponer el borde de la zona), Unity
        // recalcula su rotación de mundo a partir del padre en cuanto éste gira; sin este re-fijado
        // el plano giraría con el jugador/cámara y volvería a mostrar el "corte" del borde.
        if (_footFogRotationLocked && footFogObject != null)
            footFogObject.transform.rotation = _footFogLockedRotation;
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
        if (ambientPreset == null || !ambientPreset.enableCameraOverlay) return;

        EnsureFogCanvas();
        _fogImage.color = ambientPreset.overlayColor;

        _overlayTween?.Kill();
        _overlayTween = _fogCanvasGroup
            .DOFade(ambientPreset.overlayMaxAlpha, ambientPreset.transitionDuration)
            .SetEase(ambientPreset.transitionEase).SetUpdate(true);
    }

    private void HideCameraOverlay()
    {
        if (_fogCanvasGroup == null) return;

        float dur  = ambientPreset != null ? ambientPreset.transitionDuration : 1.5f;
        Ease  ease = ambientPreset != null ? ambientPreset.transitionEase      : Ease.InOutSine;

        _overlayTween?.Kill();
        _overlayTween = _fogCanvasGroup.DOFade(0f, dur).SetEase(ease).SetUpdate(true);
    }

    // -------------------------------------------------------------------------
    //  Cámara
    // -------------------------------------------------------------------------

    private void ApplyCameraTransition()
    {
        if (ambientPreset == null || !ambientPreset.controlCamera) return;

        var cam = GetOrCacheCamera();
        if (cam == null) return;

        if (!_cameraDefaultsCaptured)
        {
            _defaultCameraState = new CameraState
            {
                distance = cam.defaultDistance,
                height   = cam.height,
                yMin     = cam.yMinLimit,
                yMax     = cam.yMaxLimit
            };
            _cameraDefaultsCaptured = true;
        }

        float targetDist   = ambientPreset.cameraDistance;
        float targetHeight = _defaultCameraState.height;
        float targetYMin, targetYMax;
        bool  lockRot    = false;
        float lockMouseX = ambientPreset.cameraHorizontalAngle;
        float lockMouseY = 0f;

        switch (ambientPreset.cameraMode)
        {
            case ZoneCameraMode.Plataformas2D:
                targetYMin = 5f;   targetYMax = 15f;
                lockRot = true;    lockMouseY = 10f;
                break;
            case ZoneCameraMode.TopDown:
                targetYMin = 60f;  targetYMax = 85f;
                lockRot = true;    lockMouseY = 80f;
                break;
            case ZoneCameraMode.Isometrico:
                targetHeight = 2f;
                targetYMin = 30f;  targetYMax = 50f;
                lockRot = true;    lockMouseY = 35f;
                break;
            case ZoneCameraMode.CorredorEstrecho:
                targetHeight = 1.2f;
                targetYMin = -20f; targetYMax = 60f;
                break;
            case ZoneCameraMode.PanoramicoLejano:
                targetHeight = 2f;
                targetYMin = -30f; targetYMax = 70f;
                break;
            default: // SoloDistancia
                targetYMin = _defaultCameraState.yMin;
                targetYMax = _defaultCameraState.yMax;
                break;
        }

        cam.yMinLimit = targetYMin;
        cam.yMaxLimit = targetYMax;

        if (lockRot)
            cam.SetZoneRotation(lockMouseX, lockMouseY);
        else
            cam.ClearZoneRotation();

        _cameraTween?.Kill();
        float sd = cam.defaultDistance, sh = cam.height;
        float ed = targetDist,          eh = targetHeight;

        _cameraTween = DOTween.To(
            () => 0f,
            t =>
            {
                cam.defaultDistance = Mathf.Lerp(sd, ed, t);
                cam.height          = Mathf.Lerp(sh, eh, t);
            },
            1f, ambientPreset.transitionDuration
        ).SetEase(ambientPreset.transitionEase).SetUpdate(true);
    }

    private void RestoreCameraDefaults()
    {
        if (ambientPreset == null || !ambientPreset.controlCamera || !_cameraDefaultsCaptured) return;
        var cam = GetOrCacheCamera();
        if (cam == null) return;
        DoRestoreCamera(cam, ambientPreset.transitionDuration, ambientPreset.transitionEase);
    }

    private static void DoRestoreCamera(vThirdPersonCamera cam, float dur, Ease ease)
    {
        cam.ClearZoneRotation();
        cam.yMinLimit = _defaultCameraState.yMin;
        cam.yMaxLimit = _defaultCameraState.yMax;

        _cameraTween?.Kill();
        float sd = cam.defaultDistance, sh = cam.height;
        float ed = _defaultCameraState.distance, eh = _defaultCameraState.height;

        _cameraTween = DOTween.To(
            () => 0f,
            t =>
            {
                cam.defaultDistance = Mathf.Lerp(sd, ed, t);
                cam.height          = Mathf.Lerp(sh, eh, t);
            },
            1f, dur
        ).SetEase(ease).SetUpdate(true);
    }

    private static vThirdPersonCamera GetOrCacheCamera()
    {
        if (_cachedCamera != null) return _cachedCamera;
        _cachedCamera = Object.FindFirstObjectByType<vThirdPersonCamera>();
        return _cachedCamera;
    }

    // -------------------------------------------------------------------------
    //  Música
    // -------------------------------------------------------------------------

    private void TransitionToZoneMusic()
    {
        if (ambientPreset == null || !ambientPreset.changeMusic || string.IsNullOrEmpty(ambientPreset.musicZoneId)) return;

        var audioService = AudioService.Instance;
        if (audioService == null) return;

        _previousMusic   = audioService.CurrentMusicClip;
        _wasMusicPlaying = _previousMusic != null;

        if (audioService.profile == null) return;

        var rule = audioService.profile.GetAmbientZoneRule(ambientPreset.musicZoneId);
        if (rule?.music != null)
        {
            audioService.PlayMusic(rule.music, rule.fade);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else if (showDebugLogs)
        {
            Debug.LogWarning($"[AmbientZone] No se encontró música para musicZoneId '{ambientPreset.musicZoneId}'");
        }
#endif
    }

    private void RestorePreviousMusic()
    {
        if (ambientPreset == null || !ambientPreset.changeMusic) return;

        var audioService = AudioService.Instance;
        if (audioService == null) return;

        float fade = audioService.profile?.GetAmbientZoneRule(ambientPreset.musicZoneId)?.fade ?? 1.5f;

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
            StopFootFog();
            HideCameraOverlay();
            RestoreDefaultsImmediate();
        }
    }

    public static void RestoreDefaultsImmediate()
    {
        _currentTween?.Kill();
        _overlayTween?.Kill();
        _cameraTween?.Kill();
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

        if (_cameraDefaultsCaptured && _cachedCamera != null)
        {
            _cachedCamera.defaultDistance = _defaultCameraState.distance;
            _cachedCamera.height          = _defaultCameraState.height;
            _cachedCamera.yMinLimit       = _defaultCameraState.yMin;
            _cachedCamera.yMaxLimit       = _defaultCameraState.yMax;
            _cachedCamera.ClearZoneRotation();
        }
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

        Color gizmoColor = ambientPreset != null && ambientPreset.enableFog
            ? ambientPreset.fogColor
            : Color.gray;
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
        string presetInfo = ambientPreset != null ? ambientPreset.name : "Sin preset";
        string mistInfo   = groundMistPS  != null ? "Niebla suelo"    : "";
        string footInfo   = footFogObject != null ? "Niebla pies"     : "";

        string[] parts = System.Array.FindAll(
            new[] { presetInfo, mistInfo, footInfo },
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

        var main = ps.main;
        main.loop            = true;
        main.playOnAwake     = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(5f, 8f);
        main.startSpeed      = 0f;
        main.startSize       = new ParticleSystem.MinMaxCurve(areaX * 0.4f, areaX * 0.7f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 1f),
            new Color(0.88f, 0.9f, 0.95f, 1f)
        );
        main.gravityModifier = 0f;
        main.maxParticles    = 250;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled      = true;
        emission.rateOverTime = 40f;

        var shapeModule = ps.shape;
        shapeModule.enabled   = true;
        shapeModule.shapeType = ParticleSystemShapeType.Box;
        shapeModule.position  = new Vector3(0f, 0.75f, 0f);
        shapeModule.scale     = new Vector3(areaX, 1.5f, areaZ);

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

        var sizeLife = ps.sizeOverLifetime;
        sizeLife.enabled = true;
        sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.1f));

        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.08f;
        noise.frequency   = 0.3f;
        noise.scrollSpeed = 0.05f;
        noise.quality     = ParticleSystemNoiseQuality.Medium;

        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode   = ParticleSystemRenderMode.Billboard;
        psRenderer.sortingOrder = 0;

        groundMistPS = ps;
        UnityEditor.EditorUtility.SetDirty(this);

        Debug.Log($"[AmbientZone] GroundMist_PS creado en '{gameObject.name}'. " +
                  "Asigna tu material al Renderer del hijo GroundMist_PS.");
    }
#endif
}
