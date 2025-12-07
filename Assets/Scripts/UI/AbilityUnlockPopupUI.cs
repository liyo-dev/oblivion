using System.Collections.Generic;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra un popup cuando se desbloquea una habilidad 
/// </summary>
public class AbilityUnlockPopupUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI abilityTitleText;
    [SerializeField] private TextMeshProUGUI abilityDescriptionText;
    [SerializeField] private Image abilityIcon;
    
    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();
    [SerializeField] private List<AbilityPresentationForKey> abilityKeyPresentations = new();

    AbilityId? _pendingAbility;
    AbilityKey? _pendingAbilityKey;
    private bool _waitingForCloseInput = false;
    private bool _deferredByCinematic = false;
    private PlayerActionManager _actionManager;
    private readonly HashSet<string> _shownFlags = new();
    private bool _flagsLoaded;

    const string AbilityFlagPrefix = "ABILITY_POPUP_ID:";
    const string AbilityKeyFlagPrefix = "ABILITY_POPUP_KEY:";

    // Evitar mostrar popup durante la inicialización de nueva partida
    private bool _suppressPopupDuringInit = false;
    private const float INIT_SUPPRESSION_DURATION = 2f;

    void Awake()
    {
        Debug.Log("[AbilityUnlockPopupUI] Awake");
        if (popupRoot != null)
            popupRoot.SetActive(false);

        // Suscribirse lo antes posible para no perder eventos de desbloqueo si la UI
        // todavía no fue activada en la jerarquía cuando se otorga la habilidad.
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
        
        // Suscribirse al evento de profile ready para suprimir popups durante inicialización
        GameBootService.OnProfileReady += HandleProfileReadyForSuppression;

        ReloadShownFlags();

        // Intentar resolver PlayerActionManager para detectar modo Cinematic
        if (_actionManager == null)
        {
            _actionManager = GetComponentInParent<PlayerActionManager>();
            if (_actionManager == null)
            {
                PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);
            }
        }
    }
    
    private void HandleProfileReadyForSuppression()
    {
        // Suprimir popups durante los primeros segundos tras cargar el preset
        // para evitar que aparezcan al aplicar abilities desde defaultPlayerPreset
        _suppressPopupDuringInit = true;
        _flagsLoaded = false;
        ReloadShownFlags();
        Debug.Log("[AbilityUnlockPopupUI] Suprimiendo popups durante inicialización de preset");
        
        // Cancelar supresión después de un tiempo
        if (gameObject.activeInHierarchy)
            StartCoroutine(ClearSuppressionAfterDelay());
    }
    
    private System.Collections.IEnumerator ClearSuppressionAfterDelay()
    {
        yield return new WaitForSeconds(INIT_SUPPRESSION_DURATION);
        _suppressPopupDuringInit = false;
        Debug.Log("[AbilityUnlockPopupUI] Supresión de popups finalizada");
    }

    void OnEnable()
    {
        // Awake ya suscribe, pero mantener OnEnable para asegurar que la suscripción
        // siga activa si el dominio se recarga en modo editor.
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;

        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;

        // Suscribirse a cambios de modo para saber cuándo termina la cinemática
        if (_actionManager == null)
        {
            _actionManager = GetComponentInParent<PlayerActionManager>();
            if (_actionManager == null)
            {
                PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);
            }
        }
        if (_actionManager != null)
            _actionManager.OnTopModeChanged += HandleTopModeChanged;
    }

    void OnDisable()
    {
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;

        GamepadInputReader.OnInput -= HandleGamepadInput;

        if (_actionManager != null)
            _actionManager.OnTopModeChanged -= HandleTopModeChanged;
    }

    void OnDestroy()
    {
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
        GameBootService.OnProfileReady -= HandleProfileReadyForSuppression;

        if (_actionManager != null)
            _actionManager.OnTopModeChanged -= HandleTopModeChanged;
    }

    private void HandleAbilityUnlocked(AbilityId ability)
    {  
        _pendingAbility = ability;
        _pendingAbilityKey = null;
        ShowPopup();
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        Debug.Log($"[AbilityUnlockPopupUI] HandleAbilityUnlockedKey: {key} (suppressed={_suppressPopupDuringInit})");
        
        if (_suppressPopupDuringInit)
        {
            Debug.Log("[AbilityUnlockPopupUI] Popup suprimido durante inicialización");
            return;
        }
        
        _pendingAbilityKey = key;
        _pendingAbility = null;
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_pendingAbility == null && _pendingAbilityKey == null) return;
        ReloadShownFlags();

        Debug.Log($"[AbilityUnlockPopupUI] ShowPopup: pendingAbility={_pendingAbility}, pendingAbilityKey={_pendingAbilityKey}");

        // Si estamos en modo Cinematic, aplazar el popup hasta que termine
        if (_actionManager != null && _actionManager.Top == ActionMode.Cinematic)
        {
            _deferredByCinematic = true;
            Debug.Log("[AbilityUnlockPopupUI] Cinemática activa → se aplaza el popup de habilidad");
            return;
        }

        if (_pendingAbility != null)
        {
            var presentation = AbilityPresentationLookup.Resolve(_pendingAbility.Value, abilityPresentations);
            if (HasSeenFlag(GetAbilityFlag(_pendingAbility.Value)))
            {
                Debug.Log($"[AbilityUnlockPopupUI] Ability {_pendingAbility.Value} ya mostrada. Se omite popup.");
                _pendingAbility = null;
                return;
            }
            MarkFlag(GetAbilityFlag(_pendingAbility.Value));
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
            if (HasSeenFlag(GetAbilityKeyFlag(_pendingAbilityKey.Value)))
            {
                Debug.Log($"[AbilityUnlockPopupUI] AbilityKey {_pendingAbilityKey.Value} ya mostrada. Se omite popup.");
                _pendingAbilityKey = null;
                return;
            }
            MarkFlag(GetAbilityKeyFlag(_pendingAbilityKey.Value));
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

        // Empezar a escuchar el botón B/Cancel para poder cerrar el popup.
        _waitingForCloseInput = true;
    }

    public void HidePopup()
    {
        Debug.Log("[AbilityUnlockPopupUI] HidePopup");
        _pendingAbility = null;
        _pendingAbilityKey = null;
        _waitingForCloseInput = false;
        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!_waitingForCloseInput || popupRoot == null || !popupRoot.activeInHierarchy)
            return;

        if (input.Phase != InputActionPhase.Performed)
            return;

        if (input.Type == GamepadInputReader.InputEventType.Cancel)
            HidePopup();
    }

    void HandleTopModeChanged(ActionMode top)
    {
        // Cuando la cinemática termina y existe un popup aplazado, mostrarlo ahora
        if (_deferredByCinematic && top != ActionMode.Cinematic)
        {
            _deferredByCinematic = false;
            // Reintentar mostrar usando los datos pendientes
            ShowPopup();
        }
    }

    void ReloadShownFlags()
    {
        if (_flagsLoaded) return;

        _shownFlags.Clear();
        var preset = UnlockService.GetActivePreset();
        var flags = preset?.flags;
        if (flags != null)
        {
            for (int i = 0; i < flags.Count; i++)
            {
                var flag = flags[i];
                if (string.IsNullOrEmpty(flag))
                    continue;
                if (flag.StartsWith(AbilityFlagPrefix) || flag.StartsWith(AbilityKeyFlagPrefix))
                    _shownFlags.Add(flag);
            }
        }
        _flagsLoaded = true;
    }

    string GetAbilityFlag(AbilityId id) => $"{AbilityFlagPrefix}{id}";
    string GetAbilityKeyFlag(AbilityKey key) => $"{AbilityKeyFlagPrefix}{key}";

    bool HasSeenFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
            return false;
        ReloadShownFlags();
        return _shownFlags.Contains(flag);
    }

    void MarkFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag))
            return;
        ReloadShownFlags();
        if (!_shownFlags.Add(flag))
            return;

        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;
        if (preset.flags == null)
            preset.flags = new List<string>();
        if (!preset.flags.Contains(flag))
            preset.flags.Add(flag);
    }
}
