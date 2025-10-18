using Alex.NPC;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour
{
    [Header("Modo")]
    [SerializeField] private InteractableMode mode = InteractableMode.OpenDialogue;

    [Header("Hint (icono botón)")]
    [SerializeField] private GameObject hint;
    [SerializeField] private bool hideHintAtStart = true;

    [Header("Uso")]
    [SerializeField] private bool singleUse = false;
    [SerializeField] private bool initiallyEnabled = true;

    [Header("Abrir diálogo")]
    [SerializeField] private DialogueAsset dialogue;

    [Header("Eventos opcionales")]
    public UnityEvent<GameObject> OnInteract;
    public UnityEvent OnStarted;
    public UnityEvent OnFinished;
    public UnityEvent OnConsumed;

    bool used, enabledForUse;
    SimpleQuestNPC _questNPC;
    NPCBehaviourManager _npcManager;

    void Awake()
    {
        enabledForUse = initiallyEnabled;
        if (hint && hideHintAtStart) hint.SetActive(false);
        _questNPC = GetComponent<SimpleQuestNPC>();
        _npcManager = GetComponent<NPCBehaviourManager>();
    }

    public void SetHintVisible(bool visible)
    {
        if (hint) hint.SetActive(visible && !used && enabledForUse);
    }

    public bool CanInteract(GameObject _)
    {
        var dm = DialogueManager.Instance;
        if (dm != null && dm.IsOpen) return false;
        return enabledForUse && (!singleUse || !used);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        OnInteract?.Invoke(interactor);

        if (_questNPC != null)
        {
            _questNPC.Interact();
            return;
        }

        if (_npcManager != null && _npcManager.HandleInteraction(interactor))
            return;

        if (mode == InteractableMode.OpenDialogue)
            StartDialogue();
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
        var dm = DialogueManager.Instance;
        if (dialogue && dm != null)
        {
            OnStarted?.Invoke();
            dm.StartDialogue(dialogue, transform, () =>
            {
                OnFinished?.Invoke();
                AfterUse();
            });
        }
        else
        {
            Debug.LogWarning($"[Interactable] No DialogueAsset o DialogueManager en {name}.");
            AfterUse();
        }
    }

    void AfterUse()
    {
        if (singleUse && !used)
        {
            used = true;
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

    internal void RegisterNPCManager(NPCBehaviourManager manager)
    {
        _npcManager = manager;
    }

    internal void UnregisterNPCManager(NPCBehaviourManager manager)
    {
        if (_npcManager == manager)
            _npcManager = null;
    }
}
