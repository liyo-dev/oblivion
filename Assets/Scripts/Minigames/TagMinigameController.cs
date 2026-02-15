using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using TMPro;

/// <summary>
/// Controlador del minijuego "Pilla Pilla" (Tag).
/// El jugador debe huir del perseguidor durante X segundos.
/// Si es atrapado, se reinicia. Si sobrevive, gana.
/// </summary>
public class TagMinigameController : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private string minigameId = "TAG_MINIGAME_01";
    [SerializeField] private float duration = 30f;
    [SerializeField] private float countdownBeforeStart = 3f;

    [Header("Referencias")]
    [SerializeField] private ChaserAI chaser;
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private Transform chaserSpawnPoint;

    [Header("UI")]
    [SerializeField] private GameObject uiContainer;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Mensajes")]
    [SerializeField] private string startMessage = "¡HUYE!";
    [SerializeField] private string caughtMessage = "¡Te atraparon!";
    [SerializeField] private string winMessage = "¡Escapaste!";

    [Header("Eventos")]
    public UnityEvent OnMinigameStarted;
    public UnityEvent OnMinigameWon;
    public UnityEvent OnMinigameLost;
    public UnityEvent OnPlayerCaught;

    // Estado interno
    private float remainingTime;
    private bool isRunning = false;
    private bool isCountingDown = false;
    private Transform player;
    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;
    private int catchCount = 0;

    // Para integración con sistema narrativo
    public string MinigameId => minigameId;

    void Awake()
    {
        if (uiContainer) uiContainer.SetActive(false);
    }

    void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj)
        {
            player = playerObj.transform;
        }

        if (chaser)
        {
            chaser.OnCaughtPlayer += OnCaught;
        }
    }

    void OnDestroy()
    {
        if (chaser)
        {
            chaser.OnCaughtPlayer -= OnCaught;
        }
    }

    void Update()
    {
        if (!isRunning) return;

        remainingTime -= Time.deltaTime;
        UpdateTimerUI();

        if (remainingTime <= 0f)
        {
            WinMinigame();
        }
    }

    public void StartMinigame()
    {
        if (isRunning || isCountingDown)
        {
            Debug.LogWarning("[TagMinigame] Ya está en ejecución.");
            return;
        }

        Debug.Log($"[TagMinigame] Iniciando minijuego '{minigameId}'...");
        catchCount = 0;

        if (player)
        {
            playerStartPosition = playerSpawnPoint ? playerSpawnPoint.position : player.position;
            playerStartRotation = playerSpawnPoint ? playerSpawnPoint.rotation : player.rotation;
        }

        if (chaser && chaserSpawnPoint)
        {
            chaser.SetStartPosition(chaserSpawnPoint.position, chaserSpawnPoint.rotation);
        }

        StartCoroutine(StartWithCountdown());
    }

    private IEnumerator StartWithCountdown()
    {
        isCountingDown = true;

        if (uiContainer) uiContainer.SetActive(true);
        if (timerText) timerText.text = FormatTime(duration);

        ResetPositions();

        float countdown = countdownBeforeStart;
        while (countdown > 0)
        {
            if (countdownText) countdownText.text = Mathf.CeilToInt(countdown).ToString();
            yield return new WaitForSeconds(1f);
            countdown -= 1f;
        }

        if (countdownText) countdownText.text = "";
        ShowMessage(startMessage, 1.5f);

        isCountingDown = false;
        isRunning = true;
        remainingTime = duration;

        if (chaser)
        {
            chaser.StartChasing();
        }

        OnMinigameStarted?.Invoke();
        Debug.Log($"[TagMinigame] ¡Minijuego iniciado! Duración: {duration}s");
    }

    public void StopMinigame()
    {
        StopAllCoroutines();
        isRunning = false;
        isCountingDown = false;

        if (chaser) chaser.StopChasing();
        if (uiContainer) uiContainer.SetActive(false);

        Debug.Log("[TagMinigame] Minijuego detenido.");
    }

    private void OnCaught()
    {
        if (!isRunning) return;

        catchCount++;
        Debug.Log($"[TagMinigame] ¡Jugador atrapado! (Vez #{catchCount})");

        OnPlayerCaught?.Invoke();
        ShowMessage(caughtMessage, 2f);

        StartCoroutine(RestartAfterCaught());
    }

    private IEnumerator RestartAfterCaught()
    {
        isRunning = false;

        yield return new WaitForSeconds(1.5f);

        ResetPositions();

        yield return new WaitForSeconds(0.5f);

        isRunning = true;
        if (chaser) chaser.StartChasing();
    }

    private void ResetPositions()
    {
        if (player)
        {
            var charController = player.GetComponent<CharacterController>();
            if (charController)
            {
                charController.enabled = false;
                player.position = playerStartPosition;
                player.rotation = playerStartRotation;
                charController.enabled = true;
            }
            else
            {
                player.position = playerStartPosition;
                player.rotation = playerStartRotation;
            }
        }

        if (chaser)
        {
            chaser.ResetToStart();
        }
    }

    private void WinMinigame()
    {
        isRunning = false;
        Debug.Log($"[TagMinigame] ¡Victoria! El jugador escapó.");

        if (chaser) chaser.StopChasing();

        ShowMessage(winMessage, 3f);

        OnMinigameWon?.Invoke();

        RaiseWinSignal();

        StartCoroutine(HideUIAfterDelay(3f));
    }

    private void RaiseWinSignal()
    {
        var signals = DefaultNarrativeSignals.Instance;
        if (signals != null)
        {
            string eventKey = $"MINIGAME_{minigameId}_WON";
            signals.RaiseCustom(eventKey);
            Debug.Log($"[TagMinigame] Señal emitida: '{eventKey}'");
        }
        else
        {
            Debug.LogWarning("[TagMinigame] No se encontró DefaultNarrativeSignals para emitir la señal de victoria.");
        }
    }

    private IEnumerator HideUIAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (uiContainer) uiContainer.SetActive(false);
    }

    private void UpdateTimerUI()
    {
        if (timerText)
        {
            timerText.text = FormatTime(remainingTime);
        }
    }

    private void ShowMessage(string msg, float messageDuration)
    {
        if (messageText)
        {
            StopCoroutine(nameof(ClearMessageAfter));
            messageText.text = msg;
            StartCoroutine(ClearMessageAfter(messageDuration));
        }
    }

    private IEnumerator ClearMessageAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (messageText) messageText.text = "";
    }

    private string FormatTime(float time)
    {
        time = Mathf.Max(0, time);
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        return $"{minutes:00}:{seconds:00}";
    }

    public float RemainingTime => remainingTime;
    public bool IsRunning => isRunning;
    public int CatchCount => catchCount;
}
