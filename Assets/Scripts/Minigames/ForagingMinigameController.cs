using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;
using Core;
using Invector.vCharacterController;

/// <summary>
/// Controlador reutilizable para minijuegos de recogida sigilosa.
/// El jugador visita una serie de puntos (ForagingStallTrigger) en orden libre
/// pulsando un botón, evitando ser detectado por los NPCs cercanos.
/// </summary>
public class ForagingMinigameController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Configuración
    // -------------------------------------------------------------------------
    [Header("Configuración")]
    [SerializeField] private string minigameId = "FORAGE_MINIGAME_01";
    [Tooltip("Número de puestos requeridos. 0 = todos los registrados.")]
    [SerializeField] private int stallsRequired = 0;
    [Tooltip("Factor de velocidad de Estela durante el minijuego (0.1 = muy lento, 1 = normal).")]
    [SerializeField, Range(0.1f, 1f)] private float playerSpeedMultiplier = 0.35f;
    [Tooltip("Segundos de cuenta atrás antes de que arranque el juego.")]
    [SerializeField] private float countdownBeforeStart = 3f;
    [Tooltip("Cuántas veces puede ser detectada antes de perder. 0 = ilimitado.")]
    [SerializeField] private int maxDetections = 0;

    // -------------------------------------------------------------------------
    // UI — Monólogo
    // -------------------------------------------------------------------------
    [Header("UI — Monólogo")]
    [SerializeField] private GameObject monologuePanel;
    [SerializeField] private TextMeshProUGUI monologueText;
    [SerializeField] private TextMeshProUGUI monologueContinuePrompt;
    [Tooltip("Claves de localización de las líneas del monólogo (en orden).")]
    [SerializeField] private string[] monologueLineKeys;

    // -------------------------------------------------------------------------
    // UI — Instrucciones
    // -------------------------------------------------------------------------
    [Header("UI — Instrucciones")]
    [SerializeField] private GameObject instructionPanel;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI startPromptText;
    [SerializeField] private string instructionKey = "FORAGE_INSTRUCTION";
    [SerializeField] private string startPromptKey = "FORAGE_START_PROMPT";

    // -------------------------------------------------------------------------
    // UI — Juego
    // -------------------------------------------------------------------------
    [Header("UI — Juego")]
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private TextMeshProUGUI stallCounterText;
    [SerializeField] private TextMeshProUGUI detectionCounterText;
    [SerializeField] private TextMeshProUGUI statusMessageText;
    [Tooltip("Clave de localización del contador: usa {0} para recogidos y {1} para total.")]
    [SerializeField] private string stallCounterKey = "FORAGE_COUNTER";
    [SerializeField] private string caughtKey = "FORAGE_CAUGHT";
    [SerializeField] private string winKey = "FORAGE_WIN";
    [SerializeField] private string loseKey = "FORAGE_LOSE";
    [Tooltip("Cuántos segundos se muestra el mensaje de estado antes de desaparecer.")]
    [SerializeField] private float statusMessageDuration = 2f;

    // -------------------------------------------------------------------------
    // Eventos
    // -------------------------------------------------------------------------
    [Header("Eventos")]
    public UnityEvent OnMinigameStarted;
    public UnityEvent OnMinigameWon;
    public UnityEvent OnMinigameLost;
    public UnityEvent OnPlayerDetected;

    // -------------------------------------------------------------------------
    // Estado estático global
    // -------------------------------------------------------------------------
    public static bool IsAnyMinigameActive { get; private set; }
    private static ForagingMinigameController _activeInstance;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { IsAnyMinigameActive = false; _activeInstance = null; }
#endif

    // -------------------------------------------------------------------------
    // Estado interno
    // -------------------------------------------------------------------------
    private enum State { Idle, Monologue, Instructions, Countdown, Playing, Won, Lost }
    private State _state = State.Idle;

    private readonly List<ForagingStallTrigger> _stalls = new();
    private readonly HashSet<string> _collectedStalls = new();
    private int _detectionCount = 0;
    private int _monologueLine = 0;
    private Coroutine _countdownRoutine;
    private Coroutine _statusMessageRoutine;

    // Referencias al jugador
    private Transform _player;
    private vThirdPersonController _tpc;
    private PlayerActionManager _actionManager;
    private bool _speedModified = false;
    private bool _modePushed = false;
    private vThirdPersonMotor.vMovementSpeed _originalFreeSpeed;
    private vThirdPersonMotor.vMovementSpeed _originalStrafeSpeed;

    // Propiedades públicas
    public string MinigameId => minigameId;
    public bool IsPlaying => _state == State.Playing;

    // -------------------------------------------------------------------------
    // Registro de puestos
    // -------------------------------------------------------------------------

    internal void RegisterStall(ForagingStallTrigger stall)
    {
        if (!_stalls.Contains(stall))
            _stalls.Add(stall);
    }

    internal void UnregisterStall(ForagingStallTrigger stall)
    {
        _stalls.Remove(stall);
    }

    // -------------------------------------------------------------------------
    // Punto de entrada
    // -------------------------------------------------------------------------

    public void StartMinigame()
    {
        if (_state != State.Idle)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ForagingMinigame:{minigameId}] StartMinigame ignorado — estado actual: {_state}");
#endif
            return;
        }

        if (IsAlreadyCompleted())
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ForagingMinigame:{minigameId}] Ya completado, se omite.");
#endif
            return;
        }

        if (IsAnyMinigameActive)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[ForagingMinigame] Otro minijuego ya está activo.");
#endif
            return;
        }

        CachePlayerReferences();

        IsAnyMinigameActive = true;
        _activeInstance = this;

        _collectedStalls.Clear();
        _detectionCount = 0;

        if (monologuePanel != null && monologueLineKeys != null && monologueLineKeys.Length > 0)
            EnterMonologue();
        else if (instructionPanel != null)
            EnterInstructions();
        else
            StartCoroutine(CountdownThenPlay());
    }

    // -------------------------------------------------------------------------
    // Monólogo
    // -------------------------------------------------------------------------

    private void EnterMonologue()
    {
        _state = State.Monologue;
        _monologueLine = 0;
        ShowMonologueLine();
        SetPanelActive(monologuePanel, true);
        SetPanelActive(instructionPanel, false);
        SetPanelActive(gamePanel, false);
    }

    private void ShowMonologueLine()
    {
        if (monologueText == null) return;
        string key = monologueLineKeys[_monologueLine];
        monologueText.text = Loc(key);

        bool isLast = _monologueLine >= monologueLineKeys.Length - 1;
        if (monologueContinuePrompt != null)
            monologueContinuePrompt.text = isLast ? Loc("FORAGE_MONOLOGUE_START") : Loc("FORAGE_MONOLOGUE_NEXT");
    }

    void Update()
    {
        switch (_state)
        {
            case State.Monologue:
                UpdateMonologue();
                break;
            case State.Instructions:
                UpdateInstructions();
                break;
        }
    }

    private void UpdateMonologue()
    {
        if (!GamepadInputReader.JumpPressed) return;

        _monologueLine++;
        if (_monologueLine >= monologueLineKeys.Length)
        {
            SetPanelActive(monologuePanel, false);
            if (instructionPanel != null)
                EnterInstructions();
            else
                StartCoroutine(CountdownThenPlay());
        }
        else
        {
            ShowMonologueLine();
        }
    }

    // -------------------------------------------------------------------------
    // Instrucciones
    // -------------------------------------------------------------------------

    private void EnterInstructions()
    {
        _state = State.Instructions;
        if (instructionText != null) instructionText.text = Loc(instructionKey);
        if (startPromptText != null) startPromptText.text = Loc(startPromptKey);
        SetPanelActive(instructionPanel, true);
        SetPanelActive(gamePanel, false);
    }

    private void UpdateInstructions()
    {
        if (GamepadInputReader.JumpPressed)
        {
            SetPanelActive(instructionPanel, false);
            StartCoroutine(CountdownThenPlay());
        }
    }

    // -------------------------------------------------------------------------
    // Cuenta atrás y arranque
    // -------------------------------------------------------------------------

    private IEnumerator CountdownThenPlay()
    {
        _state = State.Countdown;
        SetPanelActive(gamePanel, true);
        UpdateStallCounter();

        float t = countdownBeforeStart;
        while (t > 0f)
        {
            ShowStatus(Mathf.CeilToInt(t).ToString());
            yield return new WaitForSeconds(1f);
            t -= 1f;
        }
        ClearStatus();

        BeginPlay();
    }

    private void BeginPlay()
    {
        _state = State.Playing;
        ApplySlowSpeed();
        PushMinigameMode();
        RefreshAllStalls();
        UpdateStallCounter();
        OnMinigameStarted?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ForagingMinigame:{minigameId}] Minijuego comenzado. Puestos registrados: {_stalls.Count}, requeridos: {GetRequiredCount()}");
#endif
    }

    // -------------------------------------------------------------------------
    // Recolección en puestos
    // -------------------------------------------------------------------------

    /// <summary>Llamado por ForagingStallTrigger cuando el jugador interactúa.</summary>
    public void OnStallCollected(ForagingStallTrigger stall)
    {
        if (_state != State.Playing) return;
        if (_collectedStalls.Contains(stall.StallId)) return;

        _collectedStalls.Add(stall.StallId);
        stall.MarkCollected();
        UpdateStallCounter();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ForagingMinigame:{minigameId}] Puesto '{stall.StallId}' recogido. {_collectedStalls.Count}/{GetRequiredCount()}");
#endif

        if (_collectedStalls.Count >= GetRequiredCount())
            StartCoroutine(WinRoutine());
    }

    // -------------------------------------------------------------------------
    // Detección
    // -------------------------------------------------------------------------

    /// <summary>Llamado por ForagingStallTrigger cuando un NPC detecta al jugador.</summary>
    public void OnPlayerDetectedByNPC()
    {
        if (_state != State.Playing) return;

        _detectionCount++;
        OnPlayerDetected?.Invoke();

        ShowStatus(Loc(caughtKey));
        UpdateDetectionCounter();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ForagingMinigame:{minigameId}] Detectado ({_detectionCount}/{(maxDetections > 0 ? maxDetections.ToString() : "∞")})");
#endif

        if (maxDetections > 0 && _detectionCount >= maxDetections)
            StartCoroutine(LoseRoutine());
    }

    // -------------------------------------------------------------------------
    // Victoria / Derrota
    // -------------------------------------------------------------------------

    private IEnumerator WinRoutine()
    {
        _state = State.Won;
        StopGameplay();
        ShowStatus(Loc(winKey));
        SaveAsCompleted();
        yield return new WaitForSeconds(2f);
        SetPanelActive(gamePanel, false);
        OnMinigameWon?.Invoke();
        Cleanup();
    }

    private IEnumerator LoseRoutine()
    {
        _state = State.Lost;
        StopGameplay();
        ShowStatus(Loc(loseKey));
        yield return new WaitForSeconds(2f);
        SetPanelActive(gamePanel, false);
        OnMinigameLost?.Invoke();
        Cleanup();
    }

    private void StopGameplay()
    {
        RestoreSpeed();
        PopMinigameMode();
        foreach (var s in _stalls)
            s.SetInteractable(false);
    }

    private void Cleanup()
    {
        if (_activeInstance == this)
        {
            IsAnyMinigameActive = false;
            _activeInstance = null;
        }
        _state = State.Idle;
    }

    // -------------------------------------------------------------------------
    // Velocidad del jugador
    // -------------------------------------------------------------------------

    private void ApplySlowSpeed()
    {
        if (_tpc == null || _speedModified) return;

        _originalFreeSpeed = _tpc.freeSpeed;
        _originalStrafeSpeed = _tpc.strafeSpeed;

        var slow = _originalFreeSpeed;
        slow.walkSpeed   *= playerSpeedMultiplier;
        slow.runningSpeed *= playerSpeedMultiplier;
        slow.sprintSpeed  *= playerSpeedMultiplier;
        _tpc.freeSpeed = slow;

        var slowStrafe = _originalStrafeSpeed;
        slowStrafe.walkSpeed    *= playerSpeedMultiplier;
        slowStrafe.runningSpeed *= playerSpeedMultiplier;
        slowStrafe.sprintSpeed  *= playerSpeedMultiplier;
        _tpc.strafeSpeed = slowStrafe;

        _speedModified = true;
    }

    private void RestoreSpeed()
    {
        if (_tpc == null || !_speedModified) return;
        _tpc.freeSpeed   = _originalFreeSpeed;
        _tpc.strafeSpeed = _originalStrafeSpeed;
        _speedModified = false;
    }

    // -------------------------------------------------------------------------
    // Modo de acción
    // -------------------------------------------------------------------------

    private void PushMinigameMode()
    {
        if (_modePushed) return;
        if (_actionManager == null) return;
        _actionManager.PushMode(ActionMode.Minigame);
        _modePushed = true;
    }

    private void PopMinigameMode()
    {
        if (!_modePushed) return;
        if (_actionManager == null) return;
        _actionManager.PopMode(ActionMode.Minigame);
        _modePushed = false;
    }

    // -------------------------------------------------------------------------
    // UI helpers
    // -------------------------------------------------------------------------

    private void UpdateStallCounter()
    {
        if (stallCounterText == null) return;
        string fmt = Loc(stallCounterKey);
        stallCounterText.text = string.IsNullOrEmpty(fmt)
            ? $"{_collectedStalls.Count}/{GetRequiredCount()}"
            : string.Format(fmt, _collectedStalls.Count, GetRequiredCount());
    }

    private void UpdateDetectionCounter()
    {
        if (detectionCounterText == null) return;
        detectionCounterText.text = maxDetections > 0
            ? $"{_detectionCount}/{maxDetections}"
            : _detectionCount.ToString();
    }

    private void ShowStatus(string msg)
    {
        if (statusMessageText != null)
        {
            statusMessageText.text = msg;
            statusMessageText.gameObject.SetActive(true);
        }
        if (_statusMessageRoutine != null)
            StopCoroutine(_statusMessageRoutine);
        if (_state == State.Playing)
            _statusMessageRoutine = StartCoroutine(ClearStatusAfterDelay());
    }

    private void ClearStatus()
    {
        if (statusMessageText != null)
            statusMessageText.gameObject.SetActive(false);
    }

    private IEnumerator ClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(statusMessageDuration);
        ClearStatus();
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel == null) return;
        if (panel.activeSelf != active)
            panel.SetActive(active);
    }

    // -------------------------------------------------------------------------
    // Helpers internos
    // -------------------------------------------------------------------------

    private int GetRequiredCount() =>
        stallsRequired > 0 ? Mathf.Min(stallsRequired, _stalls.Count) : _stalls.Count;

    private void RefreshAllStalls()
    {
        foreach (var s in _stalls)
            s.SetInteractable(true);
    }

    private void CachePlayerReferences()
    {
        if (_player == null)
        {
            if (PlayerService.TryGetPlayer(out var go, allowSceneLookup: true) && go != null)
                _player = go.transform;
        }
        if (_player == null) return;

        if (_tpc == null)
            _tpc = _player.GetComponent<vThirdPersonController>()
                ?? _player.GetComponentInChildren<vThirdPersonController>(true);

        if (_actionManager == null)
            _actionManager = _player.GetComponent<PlayerActionManager>()
                ?? _player.GetComponentInChildren<PlayerActionManager>(true);
    }

    // -------------------------------------------------------------------------
    // Persistencia
    // -------------------------------------------------------------------------

    private bool IsAlreadyCompleted()
    {
        var preset = GameBootService.IsAvailable ? GameBootService.Profile?.GetActivePresetResolved() : null;
        return preset?.completedInteractiveNarratives != null
               && preset.completedInteractiveNarratives.Contains(minigameId);
    }

    private void SaveAsCompleted()
    {
        var preset = GameBootService.IsAvailable ? GameBootService.Profile?.GetActivePresetResolved() : null;
        if (preset == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[ForagingMinigame:{minigameId}] No se pudo registrar la victoria: preset no disponible.");
#endif
            return;
        }
        preset.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();
        if (!preset.completedInteractiveNarratives.Contains(minigameId))
        {
            preset.completedInteractiveNarratives.Add(minigameId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ForagingMinigame:{minigameId}] Victoria registrada en save.");
#endif
        }
    }

    // -------------------------------------------------------------------------
    // Localización
    // -------------------------------------------------------------------------

    private static string Loc(string key) =>
        LocalizationManager.Instance != null ? LocalizationManager.Instance.Get(key, key) : key;

    // -------------------------------------------------------------------------
    // Limpieza ante escena destruida
    // -------------------------------------------------------------------------

    void OnDestroy()
    {
        RestoreSpeed();
        PopMinigameMode();
        if (_activeInstance == this)
        {
            IsAnyMinigameActive = false;
            _activeInstance = null;
        }
    }
}
