using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Collider))]
public class SavePoint : MonoBehaviour
{
    [Header("Config")]
    public string anchorIdToSet;        // si lo dejas vacío, conserva el actual
    public bool healOnSave = true;
    public bool teleportAfterSave;
    public string teleportAnchorId;

    [Header("Feedback")]
    [SerializeField] private string saveSuccessLocalizationKey = "SAVEPOINT_SAVED";
    [SerializeField] private string saveSuccessFallback = "¡Partida guardada con éxito!";

    CanvasGroup _promptCg;
    GameObject _playerInRange; // cache del jugador dentro del trigger

    // Estado para diferir el guardado si el perfil no está listo
    private bool _pendingSave;
    private GameObject _pendingPlayer;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(SavePoint));
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        _playerInRange = null;
    }

    private void HandleProfileReady()
    {
        if (_pendingSave && _pendingPlayer != null)
        {
            DoSave(_pendingPlayer);
            _pendingSave = false;
            _pendingPlayer = null;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!GameState.CanInteractGlobally) return;
        _playerInRange = other.gameObject;
        ShowPrompt(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_playerInRange == other.gameObject) _playerInRange = null;
        ShowPrompt(false);
    }

    // El SavePoint no lee inputs directamente; el Interactable u otros sistemas invocan Save() desde eventos.

    /// <summary>API única para guardar en este punto de guardado.</summary>
    public void Save()
    {
        if (TeleportSystem.IsTeleporting)
        {
            Debug.Log("[SavePoint] Teletransporte en progreso, guardado bloqueado.");
            return;
        }

        // ✅ Verificar si el nodo narrativo actual bloquea el guardado
        var narrativeRunner = FindObjectOfType<NarrativeRunner>();
        if (narrativeRunner != null && narrativeRunner.IsCurrentNodeBlockingSave)
        {
            Debug.Log("[SavePoint] No se puede guardar: el nodo narrativo actual bloquea el guardado.");
            ShowBlockedSaveFeedback();
            return;
        }

        // En este flujo el Interactable muestra el prompt cuando el player está encima,
        // pero no dependamos del trigger para guardar: usamos PlayerService y caemos a _playerInRange.
        GameObject player = null;
        PlayerService.TryGetPlayer(out player, allowSceneLookup: true);
        if (!player) player = _playerInRange; // fallback local sin usar Find

        if (!player)
        {
            Debug.LogWarning("[SavePoint] Save() llamado pero no se pudo resolver el jugador (PlayerService/_playerInRange null).");
            return;
        }
        TryDoSave(player);
    }

    private void TryDoSave(GameObject player)
    {
        if (GameBootService.IsAvailable)
        {
            DoSave(player);
        }
        else
        {
            _pendingSave = true;
            _pendingPlayer = player;
            Debug.Log("[SavePoint] Perfil no listo. Guardado diferido hasta OnProfileReady (desde Prompt)");
        }
    }

    void DoSave(GameObject playerGo)
    {
        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[SavePoint] GameBootProfile no disponible en GameBootService");
            return;
        }

        string anchorId = anchorIdToSet;
        if (string.IsNullOrEmpty(anchorId))
        {
            var anchor = GetComponentInParent<SpawnAnchor>() ?? GetComponent<SpawnAnchor>();
            if (anchor != null && !string.IsNullOrEmpty(anchor.anchorId))
            {
                anchorId = anchor.anchorId;
            }
        }

        if (!string.IsNullOrEmpty(anchorId))
        {
            SpawnManager.SetCurrentAnchor(anchorId);
            
            // Desbloquear este punto de teleport antes de guardar
            TeleportRegistry.UnlockPoint(anchorId);
        }

        if (healOnSave && playerGo != null)
        {
            // Curar al jugador a través del PlayerHealthSystem
            var playerHealth = playerGo.GetComponent<PlayerHealthSystem>() ?? playerGo.GetComponentInParent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
                playerHealth.SetCurrentHealth(playerHealth.MaxHealth);
            }

            // Curar maná a través del ManaPool
            var manaPool = playerGo.GetComponent<ManaPool>() ?? playerGo.GetComponentInParent<ManaPool>();
            if (manaPool != null)
            {
                // No rellenar maná si el preset actual indica que el jugador no tiene magia
                var preset = GameBootService.Profile?.GetActivePresetResolved();
                if (preset != null && preset.abilities != null && !preset.abilities.magic)
                {
                    Debug.Log("[SavePoint] Preset sin magia detectado; no se rellenará el ManaPool");
                }
                else
                {
                    manaPool.Init(manaPool.Max, manaPool.Max);
                }
            }
        }

        // Guardar usando GameBootProfile
        var saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);
        if (saveSystem != null)
        {
            // ✅ NUEVO: Si estamos en modo testeo, guardar el runtime actual para poder continuar
            // sin modo testeo más adelante. Esto permite "capturar" el progreso desde un preset.
            bool wasTestingMode = GameBootService.IsPresetOverrideActive;
            
            bool success = bootProfile.SaveCurrentGameState(saveSystem);

            if (success)
            {
                if (wasTestingMode)
                {
                    Debug.Log("[SavePoint] 🧪 Partida guardada en MODO TESTEO - El estado runtime actual se ha guardado en el JSON. " +
                              "Ahora puedes desactivar 'usePresetInsteadOfSave' para continuar desde aquí.");
                }
                else
                {
                    Debug.Log("[SavePoint] Partida guardada correctamente");
                }
                OnSaveCompleted?.Invoke();
                // El mensaje de éxito ya lo muestra el Dialogue Manager a través del
                // confirmFollowUp (DG_PARTIDA_GUARDADA) configurado en el Interactable tras
                // pulsar "Sí". Mostrarlo también aquí con ConfirmationPopupUI duplicaba el
                // aviso (un cuadro de diálogo de fondo + el popup encima).
            }
            else
            {
                Debug.LogError("[SavePoint] Error al guardar la partida");
            }
        }
        else
        {
            Debug.LogError("[SavePoint] No se encontró SaveSystem");
        }

        // Teletransporte opcional tras guardar
        if (teleportAfterSave && !string.IsNullOrEmpty(teleportAnchorId) && playerGo != null)
        {
            SpawnManager.TeleportTo(teleportAnchorId, true);
        }
    }

    void ShowPrompt(bool show)
    {
        // opcional: si tienes un Canvas local con CanvasGroup para el prompt
        if (!_promptCg) _promptCg = GetComponentInChildren<CanvasGroup>(true);
        if (_promptCg)
        {
            _promptCg.alpha = show ? 1f : 0f;
            _promptCg.blocksRaycasts = show;
        }
    }
    
    // La localización del prompt se gestiona en el prefab UI

    // Evento opcional para notificar cuando la partida se guarda correctamente
    public event System.Action OnSaveCompleted;

    /// <summary>Muestra feedback cuando el guardado está bloqueado</summary>
    void ShowBlockedSaveFeedback()
    {
        string message = "No puedes guardar en este momento...";
        
        // Intentar localizar el mensaje
        if (LocalizationManager.Instance != null)
            message = LocalizationManager.Instance.Get("SAVEPOINT_BLOCKED", message);

        StartCoroutine(ShowSaveFeedbackNextFrame(message));
    }

    IEnumerator ShowSaveFeedbackNextFrame(string message)
    {
        // Esperar un frame para permitir que los follow-ups de diálogo se activen primero
        yield return null;

        // Feedback de guardado bloqueado: usa el popup de confirmación reutilizable.
        // (El mensaje de éxito NO pasa por aquí: lo muestra el Dialogue Manager vía el
        // confirmFollowUp del Interactable). Evitamos abrirlo si ya hay uno abierto.
        if (ConfirmationPopupUI.Instance != null)
        {
            ConfirmationPopupUI.Instance.Show(message, onConfirm: null);
        }
        else
        {
            Debug.Log(message);
        }
    }
}
