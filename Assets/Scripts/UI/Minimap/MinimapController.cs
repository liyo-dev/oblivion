using UnityEngine;
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

    Transform _playerTransform;
    bool _hiddenByInterior;
    bool _hiddenByBattle;

    // ── API para MinimapUIController ─────────────────────────────────────────
    public Vector3 PlayerPosition => _playerTransform != null ? _playerTransform.position : Vector3.zero;
    public float OrthoSize => minimapCamera != null ? minimapCamera.orthographicSize : 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        SetupCamera();
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
    }

    void OnEnable()
    {
        EnvironmentController.OnInteriorEntered += OnInteriorEntered;
        EnvironmentController.OnInteriorExited  += OnInteriorExited;
        BossArenaController.OnAnyBattleStarted  += OnBattleStarted;
        BossArenaController.OnAnyBattleEnded    += OnBattleEnded;
    }

    void OnDisable()
    {
        EnvironmentController.OnInteriorEntered -= OnInteriorEntered;
        EnvironmentController.OnInteriorExited  -= OnInteriorExited;
        BossArenaController.OnAnyBattleStarted  -= OnBattleStarted;
        BossArenaController.OnAnyBattleEnded    -= OnBattleEnded;
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

        // Intentar via PlayerService
        _playerTransform = PlayerService.PlayerTransform;

        // Fallback: buscar por tag directamente
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

    void RefreshMinimapVisibility()
    {
        if (minimapRoot != null)
            minimapRoot.SetActive(!_hiddenByInterior && !_hiddenByBattle);
    }
}
