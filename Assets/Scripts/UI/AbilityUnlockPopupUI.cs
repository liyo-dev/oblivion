using System.Collections.Generic;
using TMPro;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra un popup cuando se desbloquea una habilidad y usa el mismo GO de HoldToSkip que las cinemáticas.
/// </summary>
public class AbilityUnlockPopupUI : MonoBehaviour
{
    static AbilityUnlockPopupUI _instance;

    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI abilityTitleText;
    [SerializeField] private TextMeshProUGUI abilityDescriptionText;
    [SerializeField] private Image abilityIcon;
    [SerializeField] private HoldToSkipUI holdToSkip;
    
    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();
    [SerializeField] private List<AbilityPresentationForKey> abilityKeyPresentations = new();

    AbilityId? _pendingAbility;
    AbilityKey? _pendingAbilityKey;
    private bool _listeningForAnyButton = false;
    private bool _blockingGameplay;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("[AbilityUnlockPopupUI] Awake");

        EnsurePopupRoot();
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (holdToSkip != null)
        {
            holdToSkip.OnSkipCompleted.AddListener(HidePopup);
            holdToSkip.gameObject.SetActive(false);
        }

        // Suscribirse lo antes posible para no perder eventos de desbloqueo si la UI
        // todavía no fue activada en la jerarquía cuando se otorga la habilidad.
        SubscribeEvents();
    }

    void OnEnable()
    {
        // Awake ya suscribe, pero mantener OnEnable para asegurar que la suscripción
        // siga activa si el dominio se recarga en modo editor.
        SubscribeEvents();

        EnsurePopupRoot();

        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        if (_blockingGameplay)
            HidePopup();

        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;

        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    void OnDestroy()
    {
        if (holdToSkip != null)
            holdToSkip.OnSkipCompleted.RemoveListener(HidePopup);

        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
    }

    void SubscribeEvents()
    {
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
    }

    private void HandleAbilityUnlocked(AbilityId ability)
    {
        Debug.Log($"[AbilityUnlockPopupUI] HandleAbilityUnlocked: {ability}");
        _pendingAbility = ability;
        _pendingAbilityKey = null;
        ShowPopup();
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        Debug.Log($"[AbilityUnlockPopupUI] HandleAbilityUnlockedKey: {key}");
        _pendingAbilityKey = key;
        _pendingAbility = null;
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_pendingAbility == null && _pendingAbilityKey == null) return;

        EnsurePopupRoot();

        if (popupRoot == null)
        {
            Debug.LogWarning("[AbilityUnlockPopupUI] popupRoot no está asignado, se omite mostrar el popup para evitar bloquear la jugabilidad.");
            HidePopup();
            return;
        }

        if (!TryMarkAsPendingAndUnique())
        {
            HidePopup();
            return;
        }

        Debug.Log($"[AbilityUnlockPopupUI] ShowPopup: pendingAbility={_pendingAbility}, pendingAbilityKey={_pendingAbilityKey}");

        if (_pendingAbility != null)
        {
            var presentation = AbilityPresentationLookup.Resolve(_pendingAbility.Value, abilityPresentations);
            if (abilityTitleText != null) abilityTitleText.text = presentation.title;
            if (abilityDescriptionText != null) abilityDescriptionText.text = presentation.description;
            if (abilityIcon != null)
            {
                abilityIcon.sprite = presentation.icon;
                abilityIcon.enabled = presentation.icon != null;
            }
        }
        else if (_pendingAbilityKey != null)
        {
            var presentation = AbilityPresentationKeyLookup.Resolve(_pendingAbilityKey.Value, abilityKeyPresentations);
            if (abilityTitleText != null) abilityTitleText.text = presentation.title;
            if (abilityDescriptionText != null) abilityDescriptionText.text = presentation.description;
            if (abilityIcon != null)
            {
                abilityIcon.sprite = presentation.icon;
                abilityIcon.enabled = presentation.icon != null;
            }
        }

        if (popupRoot != null)
            popupRoot.SetActive(true);

        // Empezar a escuchar cualquier botón para poder cerrar el popup.
        _listeningForAnyButton = true;

        _blockingGameplay = true;
        GameState.Push(GamePhase.Cutscene);

        if (holdToSkip != null)
        {
            holdToSkip.gameObject.SetActive(true);
            holdToSkip.enabled = false; // reiniciar estado
            holdToSkip.enabled = true;
        }
    }

    void EnsurePopupRoot()
    {
        if (popupRoot != null) return;

        // Asignar un contenedor válido para evitar omitir el popup por referencias faltantes
        var found = GetComponentInChildren<CanvasGroup>(true);
        if (found != null)
        {
            popupRoot = found.gameObject;
            Debug.LogWarning("[AbilityUnlockPopupUI] popupRoot asignado automáticamente desde CanvasGroup.");
        }
        else
        {
            popupRoot = gameObject;
            Debug.LogWarning("[AbilityUnlockPopupUI] popupRoot no estaba asignado, usando el propio GameObject como raíz.");
        }
    }

    public void HidePopup()
    {
        Debug.Log("[AbilityUnlockPopupUI] HidePopup");
        _pendingAbility = null;
        _pendingAbilityKey = null;
        _listeningForAnyButton = false;
        if (_blockingGameplay)
        {
            _blockingGameplay = false;
            if (GameState.Is(GamePhase.Cutscene))
                GameState.Pop(GamePhase.Cutscene);
        }
        if (popupRoot != null)
            popupRoot.SetActive(false);
        if (holdToSkip != null)
            holdToSkip.gameObject.SetActive(false);
    }

    void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!_listeningForAnyButton || popupRoot == null || !popupRoot.activeInHierarchy)
            return;

        if (input.Phase != InputActionPhase.Performed)
            return;

        // Solo el botón "Cancel" (B en control) debe cerrar el popup.
        if (input.Type == GamepadInputReader.InputEventType.Cancel)
            HidePopup();
    }

    private bool TryMarkAsPendingAndUnique()
    {
        string flag = BuildFlagKey();
        if (string.IsNullOrEmpty(flag)) return false;

        var profile = GameBootService.Profile;
        var preset = profile != null ? profile.GetActivePresetResolved() : null;
        var flags = preset != null ? preset.flags : null;

        if (flags != null && flags.Contains(flag))
            return false;

        if (preset != null)
        {
            if (preset.flags == null)
                preset.flags = new List<string>();

            preset.flags.Add(flag);
            Debug.Log($"[AbilityUnlockPopupUI] Marcando popup mostrado: {flag}");
        }

        return true;
    }

    private string BuildFlagKey()
    {
        if (_pendingAbility != null)
            return $"ABILITY_POPUP_{_pendingAbility.Value}";
        if (_pendingAbilityKey != null)
            return $"ABILITY_POPUP_KEY_{_pendingAbilityKey.Value}";
        return null;
    }
}
