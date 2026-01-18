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
    
    [Header("VFX")]
    [SerializeField] private GameObject teleportVfxPrefab;
    [SerializeField] private float vfxDuration = 1.5f;
    [SerializeField] private float teleportDelayAfterVfx = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip teleportStartSfx;
    [SerializeField] private AudioClip teleportEndSfx;
    
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
            teleportUI = FindFirstObjectByType<TeleportUI>();
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
            teleportUI = FindFirstObjectByType<TeleportUI>();
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
        
        // Reproducir SFX de inicio
        if (teleportStartSfx != null)
            FeedbackService.PlaySfx(teleportStartSfx, player.transform.position);
        
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
        
        // Realizar el teletransporte usando TeleportService existente
        SpawnManager.TeleportTo(destinationAnchorId, true);
        
        // Esperar un poco más después del teletransporte
        yield return new WaitForSeconds(teleportDelayAfterVfx);
        
        // Reproducir SFX de llegada
        PlayerService.TryGetPlayer(out player, allowSceneLookup: true);
        if (teleportEndSfx != null && player != null)
            FeedbackService.PlaySfx(teleportEndSfx, player.transform.position);
        
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
        
        if (_hasPushedCutscene)
        {
            GameState.Pop(GamePhase.Cutscene);
            _hasPushedCutscene = false;
        }
        
        IsTeleporting = false;
    }
}

