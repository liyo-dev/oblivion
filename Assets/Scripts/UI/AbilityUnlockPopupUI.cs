using System.Collections.Generic;
using TMPro;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
    [SerializeField] private HoldToSkipUI holdToSkip;
    
    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();
    [SerializeField] private List<AbilityPresentationForKey> abilityKeyPresentations = new();

    AbilityId? _pendingAbility;
    AbilityKey? _pendingAbilityKey;
    private bool _listeningForAnyButton = false;

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

        // Empezar a escuchar cualquier botón para poder cerrar el popup.
        _listeningForAnyButton = true;

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
        _listeningForAnyButton = false;
        if (popupRoot != null)
            popupRoot.SetActive(false);
        if (holdToSkip != null)
            holdToSkip.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!_listeningForAnyButton) return;
        if (popupRoot == null || !popupRoot.activeInHierarchy) return;

        // Legacy keyboard/mouse check (covers most keyboards)
        if (Input.anyKeyDown)
        {
            HidePopup();
            return;
        }

        // New Input System checks for gamepads, mice, joysticks
        var mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
        {
            HidePopup();
            return;
        }

        var gp = Gamepad.current;
        if (gp != null)
        {
            foreach (var btn in gp.allControls.OfType<ButtonControl>())
            {
                if (btn.wasPressedThisFrame)
                {
                    HidePopup();
                    return;
                }
            }
        }

        var joystick = Joystick.current;
        if (joystick != null)
        {
            foreach (var btn in joystick.allControls.OfType<ButtonControl>())
            {
                if (btn.wasPressedThisFrame)
                {
                    HidePopup();
                    return;
                }
            }
        }
    }
}
