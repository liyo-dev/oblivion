using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition;
using DG.Tweening;
using Sendero.Core.Feedback;

/// <summary>
/// Gestiona el Game Over con feedback visual cinematográfico:
/// - Slow motion progresivo (via FeedbackService)
/// - Zoom de cámara hacia el jugador
/// - Música de Game Over
/// - Transición automática al menú principal (sin UI de botones)
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif

    [Header("Escenas")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Transiciones")]
    [SerializeField] private TransitionManager transitionManager;
    [SerializeField] private TransitionSettings mainMenuTransitionSettings;
    [SerializeField] private float transitionStartDelay;

    [Header("Slow Motion")]
    [Tooltip("Escala de tiempo mínima durante el slow-mo")]
    [SerializeField, Range(0.01f, 0.5f)] private float slowMotionScale = 0.15f;
    [Tooltip("Duración para llegar al slow-mo máximo")]
    [SerializeField] private float slowMotionRampDuration = 0.4f;
    [Tooltip("Duración total del slow-mo antes de la transición")]
    [SerializeField] private float slowMotionHoldDuration = 2.5f;

    [Header("Camera Zoom")]
    [Tooltip("¿Hacer zoom hacia el jugador?")]
    [SerializeField] private bool enableCameraZoom = true;
    [Tooltip("Factor de zoom (< 1 = acercar, > 1 = alejar). 0.85 = zoom sutil")]
    [SerializeField, Range(0.5f, 1f)] private float zoomFactor = 0.85f;
    [Tooltip("Duración del zoom")]
    [SerializeField] private float zoomDuration = 2f;

    [Header("Screen Flash")]
    [Tooltip("¿Hacer flash rojo al morir?")]
    [SerializeField] private bool enableDeathFlash = true;
    [SerializeField] private Color deathFlashColor = new Color(0.6f, 0f, 0f, 0.5f);
    [SerializeField] private float deathFlashDuration = 0.4f;

    [Header("Audio")]
    [Tooltip("Evento de audio para Game Over")]
    [SerializeField] private string gameOverAudioEvent = "GameOverMenu";

    private bool _isGameOverActive;
    private Coroutine _gameOverCoroutine;
    private Camera _mainCamera;
    private float _originalFOV;
    private Tween _zoomTween;

    public bool IsShown => _isGameOverActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsurePersistentIfPlacedInStartScene()
    {
        try
        {
            if (ServiceLocator.TryGet(out GameOverManager existing) && existing != null)
            {
                if (existing.transform.root != null)
                    DontDestroyOnLoad(existing.transform.root.gameObject);
                else
                    DontDestroyOnLoad(existing.gameObject);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameOverManager] EnsurePersistentIfPlacedInStartScene failed: {ex}");
        }
    }

    private void Awake()
    {
        // Debug.Log($"[GameOverManager] Awake - scene='{gameObject.scene.name}'");
        
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[GameOverManager] Destruyendo duplicado");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ServiceLocator.Register(this);
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // Siempre persistir entre escenas para evitar perder el GameOverManager
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
            Debug.Log("[GameOverManager] Marcado como DontDestroyOnLoad");
        }
        else
        {
            // Si tiene padre, persistir el root
            DontDestroyOnLoad(transform.root.gameObject);
            Debug.Log($"[GameOverManager] Root '{transform.root.name}' marcado como DontDestroyOnLoad");
        }
    }

    private void OnDestroy()
    {
        CleanupTweens();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
        {
            Instance = null;
        }
        ServiceLocator.Unregister(this);
    }

    private void CleanupTweens()
    {
        _zoomTween?.Kill();
        _zoomTween = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Debug.Log($"[GameOverManager] Scene '{scene.name}' loaded -> reseteando estado");
        ForceResetState();
    }

    public void ForceResetState(string reason = null)
    {
        if (_gameOverCoroutine != null)
        {
            StopCoroutine(_gameOverCoroutine);
            _gameOverCoroutine = null;
        }

        CleanupTweens();
        _isGameOverActive = false;
        Time.timeScale = 1f;

        // Restaurar FOV si es necesario
        if (_mainCamera != null && _originalFOV > 0)
        {
            _mainCamera.fieldOfView = _originalFOV;
        }

        if (!string.IsNullOrEmpty(reason))
            Debug.Log(reason);
    }

    /// <summary>
    /// Inicia la secuencia de Game Over cinematográfica
    /// </summary>
    public void ShowGameOver()
    {
        Debug.Log($"[GameOverManager] ShowGameOver() - _isGameOverActive={_isGameOverActive}");
        
        if (_isGameOverActive || _gameOverCoroutine != null) 
        {
            Debug.Log("[GameOverManager] ShowGameOver() IGNORADO - ya activo");
            return;
        }

        _gameOverCoroutine = StartCoroutine(GameOverSequence());
    }

    private System.Collections.IEnumerator GameOverSequence()
    {
        _isGameOverActive = true;
        Debug.Log("[GameOverManager] 💀 Iniciando secuencia de Game Over cinematográfica");

        // 0. Cerrar cualquier diálogo activo para limpiar el estado de UIMode antes de la transición
        DialogueManager.Instance?.Close();

        // 1. Flash rojo de muerte (via FeedbackService)
        if (enableDeathFlash)
        {
            FeedbackService.ScreenFlash(deathFlashColor, deathFlashDuration);
            Debug.Log("[GameOverManager] 🔴 Flash de muerte activado");
        }

        // 2. Reproducir música/SFX de Game Over
        PlayGameOverAudio();

        // 3. Configurar cámara para zoom
        SetupCamera();

        // 4. Iniciar slow-motion progresivo
        StartSlowMotion();

        // 5. Iniciar zoom de cámara (si está habilitado)
        if (enableCameraZoom && _mainCamera != null)
        {
            StartCameraZoom();
        }

        // 6. Esperar la duración del efecto (usando tiempo real porque Time.timeScale está modificado)
        float totalWaitTime = slowMotionRampDuration + slowMotionHoldDuration;
        yield return new WaitForSecondsRealtime(totalWaitTime);

        Debug.Log("[GameOverManager] ⏳ Secuencia cinematográfica completada, iniciando transición al menú");

        // 7. Restaurar Time.timeScale antes de la transición
        Time.timeScale = 1f;

        // 8. Transición al menú principal
        TransitionToMainMenu();

        _gameOverCoroutine = null;
    }

    private void PlayGameOverAudio()
    {
        if (AudioService.Instance != null && !string.IsNullOrEmpty(gameOverAudioEvent))
        {
            AudioService.Instance.PlaySFX(gameOverAudioEvent);
            Debug.Log($"[GameOverManager] 🔊 Audio '{gameOverAudioEvent}' reproducido");
        }
    }

    private void SetupCamera()
    {
        _mainCamera = Camera.main;
        
        if (_mainCamera != null)
        {
            _originalFOV = _mainCamera.fieldOfView;
            Debug.Log($"[GameOverManager] 📷 Cámara configurada - FOV original: {_originalFOV}");
        }
        else
        {
            Debug.LogWarning("[GameOverManager] ⚠️ No se encontró Camera.main");
        }
    }

    private void StartSlowMotion()
    {
        Debug.Log($"[GameOverManager] 🐌 Iniciando slow-mo: {Time.timeScale} -> {slowMotionScale} en {slowMotionRampDuration}s");
        
        // Usar DOTween para animar el timeScale
        DOTween.To(
            () => Time.timeScale,
            x => Time.timeScale = x,
            slowMotionScale,
            slowMotionRampDuration
        ).SetEase(Ease.OutQuad).SetUpdate(true);
    }

    private void StartCameraZoom()
    {
        if (_mainCamera == null) return;

        float targetFOV = _originalFOV * zoomFactor;
        Debug.Log($"[GameOverManager] 🔍 Iniciando zoom FOV: {_originalFOV} -> {targetFOV} en {zoomDuration}s");

        _zoomTween?.Kill();
        _zoomTween = DOTween.To(
            () => _mainCamera.fieldOfView,
            x => _mainCamera.fieldOfView = x,
            targetFOV,
            zoomDuration
        ).SetEase(Ease.OutSine).SetUpdate(true);
    }

    private void TransitionToMainMenu()
    {
        Debug.Log($"[GameOverManager] 🚪 Transición al menú principal: '{mainMenuScene}'");

        MainMenuController.RequestInputDebounce();

        var tm = ResolveTransitionManager();
        if (tm != null && mainMenuTransitionSettings != null)
        {
            tm.Transition(mainMenuScene, mainMenuTransitionSettings, transitionStartDelay);
        }
        else
        {
            Debug.LogWarning("[GameOverManager] TransitionManager o settings no disponibles, carga directa");
            SceneManager.LoadScene(mainMenuScene);
        }
    }

    private TransitionManager ResolveTransitionManager()
    {
        if (transitionManager != null) return transitionManager;

        if (ServiceLocator.TryGet(out TransitionManager cached) && cached != null)
        {
            transitionManager = cached;
            return transitionManager;
        }

        try
        {
            transitionManager = TransitionManager.Instance();
            if (transitionManager != null)
            {
                ServiceLocator.Register(transitionManager);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[GameOverManager] TransitionManager.Instance() falló: {ex.Message}");
        }

        return transitionManager;
    }

    /// <summary>
    /// Modo helper para notificar Game Over desde otros scripts de forma segura.
    /// Crea una instancia de emergencia si no existe ninguna.
    /// </summary>
    public static void NotifyGameOver()
    {
        Debug.Log("[GameOverManager] 💀 NotifyGameOver() llamado");
        
        if (Instance == null)
        {
            // Intentar buscar en ServiceLocator
            if (ServiceLocator.TryGet(out GameOverManager found) && found != null)
            {
                Instance = found;
            }
            else
            {
                // Buscar directamente incluyendo objetos inactivos
                #if UNITY_2022_3_OR_NEWER
                found = UnityEngine.Object.FindFirstObjectByType<GameOverManager>(FindObjectsInactive.Include);
                #else
                found = UnityEngine.Object.FindObjectOfType<GameOverManager>(true);
                #endif
                
                if (found != null)
                {
                    Instance = found;
                    ServiceLocator.Register(found);
                    Debug.Log("[GameOverManager] ✅ Instancia encontrada (estaba inactiva o no registrada)");
                }
                else
                {
                    // Crear instancia de emergencia
                    Debug.LogWarning("[GameOverManager] ⚠️ No se encontró instancia, creando una de emergencia...");
                    var emergencyGO = new GameObject("[GameOverManager_Emergency]");
                    Instance = emergencyGO.AddComponent<GameOverManager>();
                    DontDestroyOnLoad(emergencyGO);
                    Debug.Log("[GameOverManager] ✅ Instancia de emergencia creada");
                }
            }
        }

        if (Instance == null)
        {
            Debug.LogError("[GameOverManager] ❌ No se pudo crear/encontrar ninguna instancia.");
            // Fallback: cargar menú principal directamente
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
            return;
        }

        if (!Instance.gameObject.activeSelf)
            Instance.gameObject.SetActive(true);

        if (!Instance.enabled)
            Instance.enabled = true;

        Instance.ShowGameOver();
    }

    // ================== LEGACY COMPATIBILITY ==================
    public void HideGameOver(bool resumeTime = true) 
    {
        ForceResetState("HideGameOver llamado");
    }
}
