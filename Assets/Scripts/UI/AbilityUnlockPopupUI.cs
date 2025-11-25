using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra un popup cuando se desbloquea una habilidad y usa el mismo GO de HoldToSkip que las cinemáticas.
/// </summary>
public class AbilityUnlockPopupUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private TextMeshProUGUI abilityTitleText;
    [SerializeField] private TextMeshProUGUI abilityDescriptionText;
    [SerializeField] private Image abilityIcon;

    [Header("Hold to close")]
    [Tooltip("Referencia al HoldToSkipUI que ya se usa en las cinemáticas.")]
    [SerializeField] private HoldToSkipUI holdToSkip;

    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();
    [SerializeField] private List<AbilityPresentationForKey> abilityKeyPresentations = new();

    AbilityId? _pendingAbility;
    AbilityKey? _pendingAbilityKey;

    void Awake()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (holdToSkip != null)
        {
            holdToSkip.OnSkipCompleted.AddListener(HidePopup);
            holdToSkip.gameObject.SetActive(false);
        }

        // Suscribirse lo antes posible para no perder eventos de desbloqueo si la UI
        // todavía no fue activada en la jerarquía cuando se otorga la habilidad.
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
    }

    void OnEnable()
    {
        // Awake ya suscribe, pero mantener OnEnable para asegurar que la suscripción
        // siga activa si el dominio se recarga en modo editor.
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
    }

    void OnDisable()
    {
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
    }

    void OnDestroy()
    {
        if (holdToSkip != null)
            holdToSkip.OnSkipCompleted.RemoveListener(HidePopup);

        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
    }

    private void HandleAbilityUnlocked(AbilityId ability)
    {
        _pendingAbility = ability;
        _pendingAbilityKey = null;
        ShowPopup();
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        _pendingAbilityKey = key;
        _pendingAbility = null;
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_pendingAbility == null && _pendingAbilityKey == null) return;

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

        if (holdToSkip != null)
        {
            holdToSkip.gameObject.SetActive(true);
            holdToSkip.enabled = false; // reiniciar estado
            holdToSkip.enabled = true;
        }
    }

    public void HidePopup()
    {
        _pendingAbility = null;
        if (popupRoot != null)
            popupRoot.SetActive(false);
        if (holdToSkip != null)
            holdToSkip.gameObject.SetActive(false);
    }
}
