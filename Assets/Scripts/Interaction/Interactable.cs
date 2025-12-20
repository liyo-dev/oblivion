using System.Collections.Generic;
using Game.NPC;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour
{
    // Cooldown global breve para evitar re-disparar interacción justo al cerrar prompts/opciones
    static float s_globalCooldownUntil = 0f;
    [Header("Modo")]
    [SerializeField] private InteractableMode mode = InteractableMode.OpenDialogue;

    [Header("Hint (icono botón)")]
    [SerializeField] private GameObject hint;
    [SerializeField] private bool hideHintAtStart = true;

    [Header("Uso")]
    [SerializeField] private bool singleUse = false;
    [SerializeField] private bool initiallyEnabled = true;

    [Header("Persistencia single-use")]
    [SerializeField] private string persistentIdOverride;
    [SerializeField] private bool includeSceneInPersistentId = true;

    [Header("Abrir diálogo")]
    [SerializeField] private DialogueAsset dialogue;
    [SerializeField] private DialogueAsset yesOption;
    [SerializeField] private DialogueAsset noOption;
    [SerializeField] private DialogueAsset confirmFollowUp;
    [SerializeField] private DialogueAsset cancelFollowUp;

    [Header("Opciones (UnityEvent)")]
    public UnityEvent OnConfirm;

    [Header("Eventos opcionales")]
    public UnityEvent<GameObject> OnInteract;
    public UnityEvent OnStarted;
    public UnityEvent OnFinished;
    public UnityEvent OnConsumed;

    bool used, enabledForUse;
    NPCBehaviourManagerV2 _npcManager;

    void Awake()
    {
        enabledForUse = initiallyEnabled;
        if (hint && hideHintAtStart) hint.SetActive(false);
        _npcManager = GetComponent<NPCBehaviourManagerV2>();
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += RestoreSingleUseStateFromPreset;
        // Re-aplicar estado single-use también cuando el preset se re-aplique en runtime (tras cargar save)
        PlayerPresetService.OnPresetApplied += HandlePresetApplied;

        if (GameBootService.IsAvailable)
            RestoreSingleUseStateFromPreset();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= RestoreSingleUseStateFromPreset;
        PlayerPresetService.OnPresetApplied -= HandlePresetApplied;
    }

    void HandlePresetApplied()
    {
        // Cuando el PlayerPresetService re-aplica el preset (por ejemplo tras Load),
        // refrescar el estado single-use desde el preset activo.
        RestoreSingleUseStateFromPreset();
    }

    public void SetHintVisible(bool visible)
    {
        if (hint) hint.SetActive(visible && !used && enabledForUse);
    }

    public bool CanInteract(GameObject _)
    {
        var dm = DialogueManager.Instance;
        if (dm != null && dm.IsOpen)
        {
            Debug.Log($"[Interactable:{name}] ❌ CanInteract=false - Diálogo abierto");
            return false;
        }
        if (!GameState.CanInteractGlobally)
        {
            Debug.Log($"[Interactable:{name}] ❌ CanInteract=false - GameState.CanInteractGlobally=false");
            return false;
        }
        if (Time.unscaledTime < s_globalCooldownUntil)
        {
            Debug.Log($"[Interactable:{name}] ❌ CanInteract=false - En cooldown global");
            return false;
        }
        if (!enabledForUse)
        {
            Debug.Log($"[Interactable:{name}] ❌ CanInteract=false - enabledForUse=false");
            return false;
        }
        if (singleUse && used)
        {
            Debug.Log($"[Interactable:{name}] ❌ CanInteract=false - singleUse y ya usado");
            return false;
        }
        
        Debug.Log($"[Interactable:{name}] ✅ CanInteract=true");
        return true;
    }

    public void Interact(GameObject interactor)
    {
        Debug.Log($"[Interactable:{name}] 🔔 Interact llamado - interactor={interactor?.name}");
        
        if (!CanInteract(interactor)) return;

        // IMPORTANTE: Establecer cooldown en PlayerActionManager para evitar que el botón A
        // se procese como salto inmediatamente después de interactuar
        if (interactor != null)
        {
            var actionManager = interactor.GetComponent<PlayerActionManager>();
            if (actionManager != null)
            {
                actionManager.SetInteractCooldown();
            }
        }

        OnInteract?.Invoke(interactor);

        // Modo HandOffToTarget: delegar al NPCBehaviourManagerV2 si existe
        if (mode == InteractableMode.HandOffToTarget && _npcManager != null)
        {
            _npcManager.HandleInteraction(interactor);
            return;
        }
        
        switch (mode)
        {
            case InteractableMode.OpenDialogue:
                StartDialogue();
                break;
            case InteractableMode.OpenDialogueWithOptions:
                StartDialogueWithOptions();
                break;
        }
    }

    public void InteractWithPlayer()
    {
        TryInteractWithPlayer();
    }

    public bool TryInteractWithPlayer()
    {
        if (!PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) || playerGo == null)
        {
            var fallback = GameObject.FindGameObjectWithTag("Player");
            if (!fallback)
            {
                Debug.LogWarning("[Interactable] Could not locate Player for interaction.");
                return false;
            }
            playerGo = fallback;
        }

        Interact(playerGo);
        return true;
    }

    void StartDialogue()
    {
        Debug.Log($"[Interactable:{name}] 📖 StartDialogue - dialogue={dialogue?.name}");
        
        var dm = DialogueManager.Instance;
        if (dialogue && dm != null)
        {
            Debug.Log($"[Interactable:{name}] ✅ Iniciando diálogo: {dialogue.name}");
            OnStarted?.Invoke();
            GameState.Push(GamePhase.Dialogue);
            dm.StartDialogue(dialogue, transform, () =>
            {
                Debug.Log($"[Interactable:{name}] 🔚 Diálogo terminado");
                OnFinished?.Invoke();
                if (GameState.Is(GamePhase.Dialogue)) GameState.Pop(GamePhase.Dialogue);
                AfterUse();
            });
        }
        else
        {
            Debug.LogWarning($"[Interactable] No DialogueAsset o DialogueManager en {name}.");
            AfterUse();
        }
    }

    void StartDialogueWithOptions()
    {
        var dm = DialogueManager.Instance;
        if (dm == null)
        {
            Debug.LogWarning("[Interactable] DialogueManager no disponible.");
            return;
        }

        // Resolver textos desde DialogueAssets (localizados por el propio manager o por LocalizationManager en SavePoint, según tu flujo)
        string prompt = ResolveDialogueText(dialogue, string.Empty);
        string yes = ResolveDialogueText(yesOption, "Sí");
        string no = ResolveDialogueText(noOption, "No");

        OnStarted?.Invoke();
        // Ocultar el hint del interactable mientras aparecen las opciones
        SetHintVisible(false);
        // Bloquear otros menús mientras se muestran las opciones
        GameState.Push(GamePhase.SavePrompt);
        try
        {
            dm.ShowWithChoices(prompt, yes, no,
                onYes: () => {
                    HandleChoiceResult(confirmFollowUp, invokeConfirm: true);
                },
                onNo:  () => {
                    HandleChoiceResult(cancelFollowUp, invokeConfirm: false);
                }
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[Interactable] ShowWithChoices failed: {ex.Message}\n{ex.StackTrace}");
            if (GameState.Is(GamePhase.SavePrompt)) GameState.Pop(GamePhase.SavePrompt);
        }
    }

    string ResolveDialogueText(DialogueAsset asset, string fallback)
    {
        if (asset == null || asset.lines == null || asset.lines.Length == 0) return fallback;
        var line = asset.lines[0];
        string textId = line.textId;
        string text = line.text;
        if (!string.IsNullOrEmpty(textId) && LocalizationManager.Instance != null)
        {
            return LocalizationManager.Instance.Get(textId, string.IsNullOrEmpty(text) ? fallback : text);
        }
        // Si text parece una clave (MAYÚS_CON_GUIONES), intentar localizarla
        if (!string.IsNullOrEmpty(text) && LocalizationManager.Instance != null && LooksLikeKey(text))
        {
            return LocalizationManager.Instance.Get(text, text);
        }
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    bool LooksLikeKey(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return false;
        bool hasUnderscore = trimmed.Contains('_');
        bool isUpper = trimmed.ToUpperInvariant() == trimmed;
        return hasUnderscore && isUpper;
    }

    void HandleChoiceResult(DialogueAsset followUp, bool invokeConfirm)
    {
        if (invokeConfirm)
            OnConfirm?.Invoke();

        if (GameState.Is(GamePhase.SavePrompt))
            GameState.Pop(GamePhase.SavePrompt);

        // Armar un pequeño cooldown tras cerrar el prompt para evitar reabrir al instante
        s_globalCooldownUntil = Time.unscaledTime + 0.25f;

        var dm = DialogueManager.Instance;
        if (followUp != null && dm != null)
        {
            dm.StartDialogue(followUp, transform, () =>
            {
                OnFinished?.Invoke();
                AfterUse();
            });
        }
        else
        {
            if (dm != null)
                dm.FinalizeChoiceNoFollowUp();
            OnFinished?.Invoke();
            AfterUse();
        }
    }

    void AfterUse()
    {
        if (singleUse && !used)
        {
            used = true;
            MarkConsumedInPreset();
            OnConsumed?.Invoke();
        }
        SetHintVisible(false);
    }

    public void EnableInteraction(bool enable)
    {
        enabledForUse = enable;
        if (!enable) SetHintVisible(false);
    }

    public void SetDialogue(DialogueAsset asset) => dialogue = asset;
    public void SetMode(InteractableMode newMode) => mode = newMode;

    internal void RegisterNPCManager(NPCBehaviourManagerV2 manager)
    {
        _npcManager = manager;
    }

    internal void UnregisterNPCManager(NPCBehaviourManagerV2 manager)
    {
        if (_npcManager == manager)
            _npcManager = null;
    }

    string GetPersistentId()
    {
        if (!string.IsNullOrEmpty(persistentIdOverride))
            return persistentIdOverride;

        var sceneName = includeSceneInPersistentId && gameObject.scene.IsValid()
            ? gameObject.scene.name
            : string.Empty;

        return string.IsNullOrEmpty(sceneName)
            ? gameObject.name
            : $"{sceneName}:{gameObject.name}";
    }

    void MarkConsumedInPreset()
    {
        if (!GameBootService.IsAvailable || !singleUse)
            return;

        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null)
            return;

        var id = GetPersistentId();
        if (string.IsNullOrEmpty(id))
            return;

        preset.consumedInteractableIds ??= new List<string>();
        if (!preset.consumedInteractableIds.Contains(id))
            preset.consumedInteractableIds.Add(id);
    }

    void RestoreSingleUseStateFromPreset()
    {
        if (!singleUse || used)
            return;

        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null || preset.consumedInteractableIds == null)
            return;

        var id = GetPersistentId();
        if (string.IsNullOrEmpty(id))
            return;

        if (preset.consumedInteractableIds.Contains(id))
        {
            used = true;
            SetHintVisible(false);
        }
    }
}

