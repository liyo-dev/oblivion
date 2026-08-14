using UnityEngine;
using UnityEngine.SceneManagement;
using EasyTransition;
using DG.Tweening;
using Sendero.Core.Feedback;
using Core;

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
    [Tooltip("Fallback si no hay referencia al jugador (ej. NotifyGameOver llamado sin contexto): duración fija de espera antes de transicionar")]
    [SerializeField] private float slowMotionHoldDuration = 2.5f;
    [Tooltip("Tope de seguridad esperando a que termine la animación de muerte, por si el Animator no resuelve el estado esperado")]
    [SerializeField] private float maxDeathAnimationWait = 3f;
    [Tooltip("Pequeña pausa tras aterrizar antes de cortar a la transición de pantalla")]
    [SerializeField] private float landingSettleDelay = 0.3f;

    [Header("Camera Zoom")]
    [Tooltip("¿Hacer zoom hacia el jugador?")]
    [SerializeField] private bool enableCameraZoom = true;
    [Tooltip("Factor de zoom (< 1 = acercar, > 1 = alejar). 0.85 = zoom sutil")]
    [SerializeField, Range(0.5f, 1f)] private float zoomFactor = 0.85f;
    [Tooltip("Duración del zoom")]
    [SerializeField] private float zoomDuration = 2f;

    [Header("Camera Focus")]
    [Tooltip("¿Reencuadrar la cámara para que enfoque al jugador al morir? Sin esto la cámara se queda mirando a lo que estuviera enfocando justo antes (p. ej. un enemigo con lock-on activo)")]
    [SerializeField] private bool enableCameraFocus = true;
    [Tooltip("Ángulo horizontal (yaw) del plano de muerte, relativo a la orientación del jugador")]
    [SerializeField] private float deathShotYawOffset = 150f;
    [Tooltip("Inclinación hacia abajo de la cámara para acompañar la caída al suelo")]
    [SerializeField] private float deathShotPitch = 20f;

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
    private vThirdPersonCamera _thirdPersonCamera;
    private PlayerHealthSystem _deadPlayer;

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

        // Soltar cualquier referencia al jugador muerto (se vuelve a asignar en el próximo Game Over)
        _deadPlayer = null;

        if (!string.IsNullOrEmpty(reason))
            Debug.Log(reason);
    }

    /// <summary>
    /// Inicia la secuencia de Game Over cinematográfica
    /// </summary>
    /// <param name="deadPlayer">
    /// Referencia al PlayerHealthSystem que ha muerto. Se usa para enfocar la cámara en él y
    /// para sincronizar la transición de pantalla con el final real de su animación de caída.
    /// Puede ser null (compatibilidad hacia atrás); en ese caso se usa una espera fija.
    /// </param>
    public void ShowGameOver(PlayerHealthSystem deadPlayer = null)
    {
        Debug.Log($"[GameOverManager] ShowGameOver() - _isGameOverActive={_isGameOverActive}");

        if (_isGameOverActive || _gameOverCoroutine != null)
        {
            Debug.Log("[GameOverManager] ShowGameOver() IGNORADO - ya activo");
            return;
        }

        _deadPlayer = deadPlayer;
        _gameOverCoroutine = StartCoroutine(GameOverSequence());
    }

    private System.Collections.IEnumerator GameOverSequence()
    {
        _isGameOverActive = true;
        Debug.Log("[GameOverManager] 💀 Iniciando secuencia de Game Over cinematográfica");
        
        // DialogueManager.Close() hace Pop de UIMode y de ActionMode.Cinematic si estaban activos.
        DialogueManager.Instance?.Close();
        // Fases de GameState que pueden quedar huérfanas si el jugador murió durante un Interactable,
        // un teleport o cualquier otra secuencia que no llegó a hacer su Pop normal.
        GameState.ForceRemove(GamePhase.Dialogue);
        GameState.ForceRemove(GamePhase.SavePrompt);
        GameState.ForceRemove(GamePhase.Cutscene);
        GameState.ForceRemove(GamePhase.GameOver);
        // Limpiar suppressors de GamepadInputReader (ej. QuestMenuManager que no llegó a cerrar).
        GamepadInputReader.ForceRestoreGameplaySuppression();

        // 1. Flash rojo de muerte (via FeedbackService)
        if (enableDeathFlash)
        {
            FeedbackService.ScreenFlash(deathFlashColor, deathFlashDuration);
            Debug.Log("[GameOverManager] 🔴 Flash de muerte activado");
        }

        // 1b. Limpiar el estado de cambio de personaje (destruye el NPC instanciado de Will si
        // lo hubiera). Sin esto, si el jugador moría mientras controlaba a Liam/Estela, el NPC de
        // Will quedaba instanciado y sobrevivía al Game Over; al recargar la partida y volver a
        // cambiar de personaje, ActiveCharacterSwapper podía terminar creando un segundo Will
        // (INC: "de pronto hay dos Will" tras un Game Over).
        ActiveCharacterSwapper.Instance?.ResetState();

        // 1c. Limpiar estado de combate huérfano: si el jugador murió en batalla, nunca se
        // llega a EndBattleById/RestoreAfterBattle/OnBattleWonRestoreMusic (esas rutas son solo
        // para victoria), así que AudioService._battleActive y ActiveCombatRegistry se quedaban
        // "encendidos" para siempre (ambos sobreviven al viaje a MainMenu). Efecto observado:
        // tras morir, volver a cargar partida y entrar en una AmbientZone, la música de zona no
        // sonaba porque AmbientZone.TransitionToZoneMusic ve IsBattleActive/Count>0 y aborta.
        AudioService.Instance?.ForceEndBattleState();
        ActiveCombatRegistry.ClearAll();

        // 2. Reproducir música/SFX de Game Over
        PlayGameOverAudio();

        // 3. Configurar cámara para zoom
        SetupCamera();

        // 3b. Reencuadrar la cámara en el jugador (evita que se quede mirando a lo que fuera que
        // tuviera enfocado justo antes de morir, p. ej. un enemigo con lock-on activo)
        if (enableCameraFocus)
        {
            FocusCameraOnDyingPlayer();
        }

        // 4. Iniciar slow-motion progresivo
        StartSlowMotion();

        // 5. Iniciar zoom de cámara (si está habilitado)
        if (enableCameraZoom && _mainCamera != null)
        {
            StartCameraZoom();
        }

        // 6. Esperar a que el jugador termine de caer al suelo (animación de muerte) antes de
        // cortar a la transición, usando tiempo real porque Time.timeScale está modificado
        yield return WaitForPlayerToLand();

        Debug.Log("[GameOverManager] ⏳ Secuencia cinematográfica completada, iniciando transición al menú");

        // 7. Restaurar Time.timeScale antes de la transición
        Time.timeScale = 1f;

        // 8. Transición al menú principal
        TransitionToMainMenu();

        _gameOverCoroutine = null;
    }

    private void PlayGameOverAudio()
    {
        if (AudioService.Instance != null)
        {
            // Detener la música de gameplay/batalla: antes solo se reproducía el SFX de Game Over
            // encima de la música que ya estuviera sonando (ej. la del Golem), quedando las dos
            // mezcladas. Un fade corto evita un corte seco.
            AudioService.Instance.StopMusic(0.3f);

            if (!string.IsNullOrEmpty(gameOverAudioEvent))
            {
                AudioService.Instance.PlaySFX(gameOverAudioEvent);
                Debug.Log($"[GameOverManager] 🔊 Audio '{gameOverAudioEvent}' reproducido");
            }
        }
    }

    private void SetupCamera()
    {
        _mainCamera = Camera.main;

        if (_mainCamera != null)
        {
            _originalFOV = _mainCamera.fieldOfView;
            _thirdPersonCamera = _mainCamera.GetComponent<vThirdPersonCamera>();
            Debug.Log($"[GameOverManager] 📷 Cámara configurada - FOV original: {_originalFOV}");
        }
        else
        {
            Debug.LogWarning("[GameOverManager] ⚠️ No se encontró Camera.main");
        }
    }

    /// <summary>
    /// Reencuadra la cámara en el jugador que acaba de morir. Sin esto, el zoom cinematográfico
    /// (StartCameraZoom) solo estrecha el FOV mientras la cámara sigue apuntando a lo que fuera
    /// que tuviera enfocado un instante antes (ej. un enemigo con lock-on de combate todavía
    /// activo), en vez de al jugador cayendo al suelo.
    /// </summary>
    private void FocusCameraOnDyingPlayer()
    {
        if (_thirdPersonCamera == null || _deadPlayer == null) return;

        // Soltar cualquier lock de combate que siguiera activo: si no, el ángulo calculado abajo
        // se ignora y CameraMovement() sigue mirando al enemigo bloqueado.
        _thirdPersonCamera.ClearLockTarget();

        // Plano fijo y deliberado detrás/lateral del jugador, inclinado hacia abajo para
        // acompañar la caída. Se calcula a partir de su orientación actual en vez de depender
        // del ángulo libre que tuviera la cámara justo antes de morir.
        float playerYaw = _deadPlayer.transform.eulerAngles.y;
        _thirdPersonCamera.SetAngles(playerYaw + deathShotYawOffset, deathShotPitch);

        Debug.Log($"[GameOverManager] 🎥 Cámara enfocada en el jugador (yaw={playerYaw + deathShotYawOffset:F0}°, pitch={deathShotPitch:F0}°)");
    }

    /// <summary>
    /// Espera a que el jugador termine de caer al suelo (animación de muerte completada) antes
    /// de dar paso a la transición de pantalla. Si no hay referencia al jugador, usa la espera
    /// fija anterior como fallback.
    /// </summary>
    private System.Collections.IEnumerator WaitForPlayerToLand()
    {
        // Dejar que se aprecie el ramp de slow-mo antes de comprobar nada más.
        float elapsed = 0f;
        while (elapsed < slowMotionRampDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_deadPlayer == null)
        {
            float holdElapsed = 0f;
            while (holdElapsed < slowMotionHoldDuration)
            {
                holdElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            yield break;
        }

        float safety = 0f;
        while (!_deadPlayer.HasDeathAnimationFinished() && safety < maxDeathAnimationWait)
        {
            safety += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.Log($"[GameOverManager] 🧍 Jugador en el suelo (esperado {safety:F2}s tras el ramp) -> transición de pantalla");

        // Pequeño respiro tras aterrizar antes de cortar a la transición.
        yield return new WaitForSecondsRealtime(landingSettleDelay);
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
    /// <param name="deadPlayer">
    /// PlayerHealthSystem que ha muerto (opcional). Permite enfocar la cámara en él y
    /// sincronizar la transición con el final real de su animación de caída.
    /// </param>
    public static void NotifyGameOver(PlayerHealthSystem deadPlayer = null)
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
                found = UnityEngine.Object.FindAnyObjectByType<GameOverManager>(FindObjectsInactive.Include);
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

        Instance.ShowGameOver(deadPlayer);
    }

    // ================== LEGACY COMPATIBILITY ==================
    public void HideGameOver(bool resumeTime = true) 
    {
        ForceResetState("HideGameOver llamado");
    }
}
