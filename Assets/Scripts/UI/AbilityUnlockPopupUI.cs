using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra un popup cuando se desbloquea una habilidad y usa el mismo GO de HoldToSkip que las cinemáticas.
/// </summary>
public class AbilityUnlockPopupUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Text abilityTitleText;
    [SerializeField] private Text abilityDescriptionText;
    [SerializeField] private Image abilityIcon;

    [Header("Hold to close")]
    [Tooltip("Referencia al HoldToSkipUI que ya se usa en las cinemáticas.")]
    [SerializeField] private HoldToSkipUI holdToSkip;

    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();

    AbilityId? _pendingAbility;

    void Awake()
    {
        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (holdToSkip != null)
        {
            holdToSkip.OnSkipCompleted.AddListener(HidePopup);
            holdToSkip.gameObject.SetActive(false);
        }
    }

    void OnEnable()
    {
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
    }

    void OnDisable()
    {
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
    }

    void OnDestroy()
    {
        if (holdToSkip != null)
            holdToSkip.OnSkipCompleted.RemoveListener(HidePopup);
    }

    private void HandleAbilityUnlocked(AbilityId ability)
    {
        _pendingAbility = ability;
        ShowPopup();
    }

    private void ShowPopup()
    {
        if (_pendingAbility == null) return;

        var presentation = AbilityPresentationLookup.Resolve(_pendingAbility.Value, abilityPresentations);

        if (abilityTitleText != null)
            abilityTitleText.text = presentation.title;

        if (abilityDescriptionText != null)
            abilityDescriptionText.text = presentation.description;

        if (abilityIcon != null)
        {
            abilityIcon.sprite = presentation.icon;
            abilityIcon.enabled = presentation.icon != null;
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
