using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// <summary>
/// Sistema principal de teletransporte.
/// Gestiona el VFX previo al teletransporte y coordina con TeleportService.
/// </summary>
public class TeleportSystem : MonoBehaviour
{
    private static TeleportSystem _instance;
    public static TeleportSystem Instance => _instance;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        OnTeleportStarted = null;
        OnTeleportCompleted = null;
        IsTeleporting = false;
    }
    #endif
    
    [Header("VFX")]
    [SerializeField] private GameObject teleportVfxPrefab;
    [SerializeField] private float vfxDuration = 1.5f;
    [SerializeField] private float teleportDelayAfterVfx = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private string teleportStartSfxKey = "Teleport_Start";
    [SerializeField] private string teleportEndSfxKey = "Teleport_End";
    
    [Header("UI")]
    [SerializeField] private TeleportUI teleportUI;
    
    /// <summary>Evento disparado cuando inicia el proceso de teletransporte.</summary>
    public static event Action OnTeleportStarted;
    
    /// <summary>Evento disparado cuando termina el proceso de teletransporte.</summary>
    public static event Action OnTeleportCompleted;
    
    /// <summary>True si hay un teletransporte en progreso.</summary>
    public static bool IsTeleporting { get; private set; }
    
    private Coroutine _teleportCoroutine;
    private bool _hasPushedCutscene;
    private bool _hasBlockedMovement;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        ServiceLocator.Register(this);
    }
    
    private void OnDestroy()
    {
        if (_instance == this)
        {
            ServiceLocator.Unregister(this);
            _instance = null;
        }
    }
    
    private void Start()
    {
        // Auto-buscar TeleportUI si no está asignada
        if (teleportUI == null)
            teleportUI = FindAnyObjectByType<TeleportUI>();
    }
    
    /// <summary>
    /// Abre la UI de selección de destino de teletransporte.
    /// </summary>
    /// <param name="currentAnchorId">AnchorId del punto actual para excluirlo de la lista.</param>
    public void OpenTeleportMenu(string currentAnchorId = null)
    {
        if (IsTeleporting)
        {
            Debug.LogWarning("[TeleportSystem] Ya hay un teletransporte en progreso.");
            return;
        }
        
        if (!TeleportRegistry.IsSystemAvailable)
        {
            Debug.LogWarning("[TeleportSystem] Sistema de teletransporte no disponible (menos de 2 puntos desbloqueados).");
            return;
        }
        
        if (teleportUI == null)
        {
            teleportUI = FindAnyObjectByType<TeleportUI>();
            if (teleportUI == null)
            {
                Debug.LogError("[TeleportSystem] No se encontró TeleportUI en la escena.");
                return;
            }
        }
        
        teleportUI.Open(currentAnchorId);
    }
    
    /// <summary>
    /// Cierra la UI de teletransporte.
    /// </summary>
    public void CloseTeleportMenu()
    {
        if (teleportUI != null && teleportUI.IsOpen)
            teleportUI.Close();
    }
    
    /// <summary>
    /// Inicia el proceso de teletransporte hacia un destino.
    /// Muestra VFX, espera, y luego ejecuta el teletransporte.
    /// </summary>
    /// <param name="destinationAnchorId">ID del anchor de destino.</param>
    public void TeleportTo(string destinationAnchorId)
    {
        if (IsTeleporting)
        {
            Debug.LogWarning("[TeleportSystem] Ya hay un teletransporte en progreso.");
            return;
        }
        
        if (string.IsNullOrEmpty(destinationAnchorId))
        {
            Debug.LogError("[TeleportSystem] destinationAnchorId es nulo o vacío.");
            return;
        }
        
        // Verificar que el destino está desbloqueado
        if (!TeleportRegistry.IsUnlocked(destinationAnchorId))
        {
            Debug.LogWarning($"[TeleportSystem] El punto {destinationAnchorId} no está desbloqueado.");
            return;
        }
        
        // Cerrar la UI si está abierta
        CloseTeleportMenu();
        
        // Iniciar la corrutina de teletransporte
        if (_teleportCoroutine != null)
            StopCoroutine(_teleportCoroutine);
        _teleportCoroutine = StartCoroutine(TeleportSequence(destinationAnchorId));
    }
    
    private IEnumerator TeleportSequence(string destinationAnchorId)
    {
        IsTeleporting = true;
        OnTeleportStarted?.Invoke();
        
        // Obtener posición del jugador para el VFX
        GameObject player = null;
        PlayerService.TryGetPlayer(out player, allowSceneLookup: true);
        
        if (player == null)
        {
            Debug.LogError("[TeleportSystem] No se encontró el jugador para el VFX de teletransporte.");
            IsTeleporting = false;
            yield break;
        }
        
        // Bloquear input del jugador durante el teletransporte
        GameState.Push(GamePhase.Cutscene);
        _hasPushedCutscene = true;

        // Deshabilitar vThirdPersonInput directamente para que Invector no lea input
        Invector.vCharacterController.vThirdPersonInput tpi = null;
        PlayerService.TryGetComponent(out tpi);
        if (tpi != null)
        {
            tpi.enabled = false;
            _hasBlockedMovement = true;
        }

        // Reproducir SFX de inicio
        if (!string.IsNullOrWhiteSpace(teleportStartSfxKey))
            AudioService.Instance?.PlaySFX(teleportStartSfxKey, 1f, player.transform.position);

        // Instanciar VFX
        GameObject vfxInstance = null;
        if (teleportVfxPrefab != null)
        {
            vfxInstance = FeedbackService.PlayVFX(
                teleportVfxPrefab,
                player.transform.position,
                Quaternion.identity,
                vfxDuration + teleportDelayAfterVfx + 1f
            );
        }

        // Esperar la duración del VFX
        yield return new WaitForSeconds(vfxDuration);

        // Suscribirse al fin de transición antes de disparar el teletransporte
        bool transitionEnded = false;
        System.Action onTransitionEnd = () => transitionEnded = true;
        TeleportService.OnTeleportEnded += onTransitionEnd;

        // Realizar el teletransporte (dispara la transición de pantalla internamente)
        SpawnManager.TeleportTo(destinationAnchorId, true);

        // Esperar a que la transición termine por completo (fade in incluido).
        // FIX C3 (auditoría 2026-08-07): TeleportService.TeleportToAnchor ya emite siempre
        // OnTeleportEnded en sus paths de fallo (ver TeleportService.cs), pero se deja este
        // timeout como red de seguridad: sin él, cualquier otro camino de fallo futuro que se
        // cuele sin emitir el evento dejaría esta corrutina esperando para siempre, con el
        // jugador sin input y sin poder volver a teletransportar (IsTeleporting nunca baja).
        const float TeleportEndTimeout = 10f;
        float waitStartedAt = Time.unscaledTime;
        yield return new WaitUntil(() => transitionEnded || Time.unscaledTime - waitStartedAt > TeleportEndTimeout);
        TeleportService.OnTeleportEnded -= onTransitionEnd;
        if (!transitionEnded)
            Debug.LogError($"[TeleportSystem] Timeout esperando OnTeleportEnded para '{destinationAnchorId}' — se fuerza el cierre del teletransporte para no dejar al jugador bloqueado.");

        // Reproducir SFX de llegada
        PlayerService.TryGetPlayer(out player, allowSceneLookup: true);
        if (!string.IsNullOrWhiteSpace(teleportEndSfxKey) && player != null)
            AudioService.Instance?.PlaySFX(teleportEndSfxKey, 1f, player.transform.position);

        // Restaurar vThirdPersonInput ahora que la transición ha terminado
        if (_hasBlockedMovement && tpi != null)
        {
            tpi.enabled = false; // ciclo para resetear estado interno
            tpi.enabled = true;
            _hasBlockedMovement = false;
        }

        // Restaurar control al jugador
        if (_hasPushedCutscene)
        {
            GameState.Pop(GamePhase.Cutscene);
            _hasPushedCutscene = false;
        }

        IsTeleporting = false;
        OnTeleportCompleted?.Invoke();
        _teleportCoroutine = null;
        
        Debug.Log($"[TeleportSystem] Teletransporte completado a: {destinationAnchorId}");
    }
    
    /// <summary>
    /// Cancela el teletransporte en progreso (si es posible).
    /// </summary>
    public void CancelTeleport()
    {
        if (_teleportCoroutine != null)
        {
            StopCoroutine(_teleportCoroutine);
            _teleportCoroutine = null;
        }
        
        if (_hasBlockedMovement)
        {
            if (PlayerService.TryGetComponent<Invector.vCharacterController.vThirdPersonInput>(out var tpi))
            {
                tpi.enabled = false;
                tpi.enabled = true;
            }
            _hasBlockedMovement = false;
        }

        if (_hasPushedCutscene)
        {
            GameState.Pop(GamePhase.Cutscene);
            _hasPushedCutscene = false;
        }

        IsTeleporting = false;
    }
}

