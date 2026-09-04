using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Singleton que orquesta el sistema de minimapa circular:
/// - Sigue al jugador con la cámara ortográfica
/// - Rota la flecha del jugador según su orientación
/// - Oculta el minimapa al entrar en interiores
/// </summary>
[DisallowMultipleComponent]
public class MinimapController : MonoBehaviour
{
    public static MinimapController Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    [Header("Cámara")]
    [SerializeField] Camera minimapCamera;
    [SerializeField] RenderTexture renderTexture;
    [SerializeField] float cameraHeight = 200f;
    [SerializeField] float defaultZoom = 25f;

    [Header("UI")]
    [SerializeField] GameObject minimapRoot;
    [SerializeField] RawImage minimapImage;
    [SerializeField] RectTransform playerArrow;

    [Header("Bounds (opcional)")]
    [SerializeField] MinimapBounds worldBounds;

    [Header("Mapa grande")]
    [Tooltip("Tamaño ortográfico de la cámara cuando el mapa grande está abierto (ver BigMapController), " +
             "para mostrar más área del mundo que el zoom normal (defaultZoom/worldBounds).")]
    [SerializeField] float bigMapZoom = 60f;

    Transform _playerTransform;
    bool _hiddenByInterior;
    bool _hiddenByBattle;
    bool _hiddenByMenu;
    bool _hiddenByCinematic;
    float _normalOrthoSize;
    bool _fogWasEnabledBeforeMinimap;
    bool _minimapFogOverrideActive;

    // ── API para MinimapUIController ─────────────────────────────────────────
    public Vector3 PlayerPosition => _playerTransform != null ? _playerTransform.position : Vector3.zero;
    public float OrthoSize => minimapCamera != null ? minimapCamera.orthographicSize : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Suscribir aquí (no en OnEnable) para que los eventos lleguen aunque
        // minimapRoot esté desactivado (lo que haría llamar OnDisable en este componente).
        EnvironmentController.OnInteriorEntered += OnInteriorEntered;
        EnvironmentController.OnInteriorExited  += OnInteriorExited;
        BossArenaController.OnAnyBattleStarted  += OnBattleStarted;
        BossArenaController.OnAnyBattleEnded    += OnBattleEnded;
        MenuManager.MenuOpened                  += OnMenuOpened;
        MenuManager.MenuClosed                  += OnMenuClosed;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering   += OnEndCameraRendering;

        SetupCamera();
    }

    void OnDestroy()
    {
        EnvironmentController.OnInteriorEntered -= OnInteriorEntered;
        EnvironmentController.OnInteriorExited  -= OnInteriorExited;
        BossArenaController.OnAnyBattleStarted  -= OnBattleStarted;
        BossArenaController.OnAnyBattleEnded    -= OnBattleEnded;
        MenuManager.MenuOpened                  -= OnMenuOpened;
        MenuManager.MenuClosed                  -= OnMenuClosed;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering   -= OnEndCameraRendering;
    }

    void Start()
    {
        RefreshMinimapVisibility();
    }

    void SetupCamera()
    {
        if (minimapCamera == null) return;

        if (renderTexture != null)
        {
            minimapCamera.targetTexture = renderTexture;

            if (minimapImage != null)
                minimapImage.texture = renderTexture;
        }

        // La cámara siempre mira hacia abajo
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Si hay bounds definidos, ajustar el zoom para cubrir todo el mundo
        if (worldBounds != null)
        {
            var size = worldBounds.WorldSize;
            minimapCamera.orthographicSize = Mathf.Max(size.x, size.y) * 0.5f;
        }
        else
        {
            minimapCamera.orthographicSize = defaultZoom;
        }

        _normalOrthoSize = minimapCamera.orthographicSize;
    }

    /// <summary>
    /// Activa/desactiva el zoom ampliado de la cámara para el mapa grande (ver BigMapController).
    /// Al desactivarlo restaura el zoom normal (el mismo que calculó SetupCamera a partir de
    /// worldBounds o defaultZoom).
    /// </summary>
    public void SetBigMapMode(bool active)
    {
        if (minimapCamera == null) return;
        minimapCamera.orthographicSize = active ? bigMapZoom : _normalOrthoSize;
    }


    void Update()
    {
        ResolvePlayer();

        if (minimapCamera == null) return;

        // Mantener la rotación top-down cada frame (por si algo la resetea)
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        if (_playerTransform == null) return;

        // Seguir al jugador en XZ
        minimapCamera.transform.position = new Vector3(
            _playerTransform.position.x,
            cameraHeight,
            _playerTransform.position.z);

        // Rotar la flecha según la orientación Y del jugador (offset 90° porque el sprite apunta a la derecha)
        if (playerArrow != null)
            playerArrow.localEulerAngles = new Vector3(0f, 0f, 90f - _playerTransform.eulerAngles.y);
    }

    void ResolvePlayer()
    {
        if (_playerTransform != null) return;

        _playerTransform = PlayerService.PlayerTransform;

        if (_playerTransform == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) _playerTransform = go.transform;
        }
    }

    void OnInteriorEntered()
    {
        _hiddenByInterior = true;
        RefreshMinimapVisibility();
    }

    void OnInteriorExited()
    {
        _hiddenByInterior = false;
        RefreshMinimapVisibility();
    }

    void OnBattleStarted()
    {
        _hiddenByBattle = true;
        RefreshMinimapVisibility();
    }

    void OnBattleEnded()
    {
        _hiddenByBattle = false;
        RefreshMinimapVisibility();
    }

    void OnMenuOpened(MenuKind kind)
    {
        // El mapa grande ES el minimapa (minimapRoot ampliado por BigMapController), no un menú
        // aparte por encima: ocultarlo aquí lo apagaría justo al abrirlo. Los demás menús sí deben
        // ocultar el minimapa como hasta ahora.
        if (kind == MenuKind.BigMap) return;

        _hiddenByMenu = true;
        RefreshMinimapVisibility();
    }

    void OnMenuClosed(MenuKind kind)
    {
        if (kind == MenuKind.BigMap) return;

        _hiddenByMenu = MenuManager.AnyOpenExcept(MenuKind.BigMap);
        RefreshMinimapVisibility();
    }

    // ── Niebla global desactivada solo mientras renderiza esta cámara ──────────
    // RenderSettings.fog es un estado GLOBAL (no existe culling mask para niebla), así
    // que sin esto la niebla de lluvia/tormenta/niebla de DayNightCycle (rainFogDensityMultiplier,
    // etc.) se aplicaba también al minimapa. Al ser una cámara ortográfica top-down con altura
    // fija (cameraHeight), la distancia cámara-suelo es prácticamente constante en toda la
    // imagen — a diferencia de la cámara principal, donde la niebla degrada con la distancia,
    // aquí toda la textura del minimapa se "blanqueaba" de golpe y uniformemente en cuanto subía
    // la densidad de niebla (INC: minimapa en blanco durante la lluvia, 1 sep 2026).
    void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != minimapCamera) return;

        _fogWasEnabledBeforeMinimap = RenderSettings.fog;
        RenderSettings.fog = false;
        _minimapFogOverrideActive = true;
    }

    void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != minimapCamera || !_minimapFogOverrideActive) return;

        RenderSettings.fog = _fogWasEnabledBeforeMinimap;
        _minimapFogOverrideActive = false;
    }

    /// <summary>
    /// Oculta/muestra el minimapa durante cinemáticas que no pasan por interior/batalla/menú
    /// (p. ej. KingdomExitTransitionNode al revelar el título del juego).
    /// </summary>
    public void SetHiddenByCinematic(bool hidden)
    {
        _hiddenByCinematic = hidden;
        RefreshMinimapVisibility();
    }

    void RefreshMinimapVisibility()
    {
        if (minimapRoot != null)
            minimapRoot.SetActive(!_hiddenByInterior && !_hiddenByBattle && !_hiddenByMenu && !_hiddenByCinematic);
    }
}
