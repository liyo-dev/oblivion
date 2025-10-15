using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour
{
    public enum Mode { OpenDialogue, HandOffToTarget }
    public enum SessionSelect { UseField, AutoFirstOnThisGO, ByTypeName }

    [Header("Modo")]
    [SerializeField] private Mode mode = Mode.OpenDialogue;

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

    void Awake()
    {
        enabledForUse = initiallyEnabled;
        if (hint && hideHintAtStart) hint.SetActive(false);
        _questNPC = GetComponent<SimpleQuestNPC>();
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

        if (mode == Mode.OpenDialogue)
            StartDialogue();
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
    public void SetMode(Mode newMode) => mode = newMode;
}
