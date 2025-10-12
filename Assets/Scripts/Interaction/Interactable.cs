using System;
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

    // ---- estado ----
    bool used, enabledForUse;
    SimpleQuestNPC _questNPC;

    void Awake()
    {
        enabledForUse = initiallyEnabled;
        if (hint && hideHintAtStart) hint.SetActive(false);
        
        // Detectar automáticamente SimpleQuestNPC
        _questNPC = GetComponent<SimpleQuestNPC>();
    }

    public void SetHintVisible(bool visible)
    {
        if (hint) hint.SetActive(visible && !used && enabledForUse);
    }

    public bool CanInteract(GameObject _)
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return false;
        return enabledForUse && (!singleUse || !used);
    }

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        OnInteract?.Invoke(interactor);

        // Si hay SimpleQuestNPC, delegar a él
        if (_questNPC != null)
        {
            _questNPC.Interact();
            return;
        }

        // Si no, comportamiento normal
        switch (mode)
        {
            case Mode.OpenDialogue:
                StartDialogue(interactor);
                break;
        }
    }

    void StartDialogue(GameObject _)
    {
        if (dialogue && DialogueManager.Instance)
        {
            OnStarted?.Invoke();
            DialogueManager.Instance.StartDialogue(dialogue, () =>
            {
                OnFinished?.Invoke();
                AfterUse();
            });
        }
        else
        {
            Debug.LogWarning($"[Interactable] No hay DialogueAsset o DialogueManager para {name}.");
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

    // Helpers
    public void EnableInteraction(bool enable)
    {
        enabledForUse = enable;
        if (!enable) SetHintVisible(false);
    }

    public void SetDialogue(DialogueAsset asset) => dialogue = asset;
    public void SetMode(Mode newMode) => mode = newMode;
}
