using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Muestra un popup cuando se desbloquea una habilidad.
/// Aparece en el lado derecho con animación DOTween y se cierra automáticamente.
/// Solo aparece una vez por habilidad — el flag se persiste en el preset.
/// </summary>
public class AbilityUnlockPopupUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RectTransform popupRoot;
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private TextMeshProUGUI abilityTitleText;
    [SerializeField] private TextMeshProUGUI abilityDescriptionText;
    [SerializeField] private Image abilityIcon;

    [Header("Datos")]
    [SerializeField] private List<AbilityPresentation> abilityPresentations = new();
    [SerializeField] private List<AbilityPresentationForKey> abilityKeyPresentations = new();

    [Header("Animación")]
    [SerializeField] private float slideOffscreenX = 450f;
    [SerializeField] private float animInDuration = 0.45f;
    [SerializeField] private float animOutDuration = 0.3f;
    [SerializeField] private float displayDuration = 3.5f;

    private AbilityId? _pendingAbility;
    private AbilityKey? _pendingAbilityKey;
    private readonly HashSet<string> _shownFlags = new();
    private bool _flagsLoaded;
    private Coroutine _autoDismissCoroutine;
    private bool _isShowing;
    private bool _suppressPopupDuringInit;
    private const float InitSuppressionDuration = 2f;

    const string AbilityFlagPrefix = "ABILITY_POPUP_ID:";
    const string AbilityKeyFlagPrefix = "ABILITY_POPUP_KEY:";

    void Awake()
    {
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReadyForSuppression;
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
        ReloadShownFlags();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReadyForSuppression;
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
    }

    void OnDestroy()
    {
        if (popupRoot != null) popupRoot.DOKill();
        if (popupCanvasGroup != null) popupCanvasGroup.DOKill();
    }

    // ── Supresión al cargar preset ─────────────────────────────────────────────

    private void HandleProfileReadyForSuppression()
    {
        _suppressPopupDuringInit = true;
        _flagsLoaded = false;
        ReloadShownFlags();
        if (gameObject.activeInHierarchy)
            StartCoroutine(ClearSuppressionAfterDelay());
    }

    private IEnumerator ClearSuppressionAfterDelay()
    {
        yield return new WaitForSeconds(InitSuppressionDuration);
        _suppressPopupDuringInit = false;
    }

    // ── Handlers de desbloqueo ─────────────────────────────────────────────────

    private void HandleAbilityUnlocked(AbilityId ability)
    {
        if (_suppressPopupDuringInit) return;
        _pendingAbility = ability;
        _pendingAbilityKey = null;
        ShowPopup();
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        if (_suppressPopupDuringInit) return;
        _pendingAbilityKey = key;
        _pendingAbility = null;
        ShowPopup();
    }

    // ── Lógica de popup ────────────────────────────────────────────────────────

    private void ShowPopup()
    {
        if (_pendingAbility == null && _pendingAbilityKey == null) return;
        ReloadShownFlags();

        if (_pendingAbility != null)
        {
            if (HasSeenFlag(GetAbilityFlag(_pendingAbility.Value)))
            {
                _pendingAbility = null;
                return;
            }
            var p = AbilityPresentationLookup.Resolve(_pendingAbility.Value, abilityPresentations);
            MarkFlag(GetAbilityFlag(_pendingAbility.Value));
            SetTexts(p.title, p.description, p.icon);
        }
        else
        {
            if (_pendingAbilityKey == null) return;
            if (HasSeenFlag(GetAbilityKeyFlag(_pendingAbilityKey.Value)))
            {
                _pendingAbilityKey = null;
                return;
            }
            var p = AbilityPresentationKeyLookup.Resolve(_pendingAbilityKey.Value, abilityKeyPresentations);
            MarkFlag(GetAbilityKeyFlag(_pendingAbilityKey.Value));
            SetTexts(p.title, p.description, p.icon);
        }

        AnimateIn();
    }

    private void SetTexts(string title, string description, Sprite icon)
    {
        if (abilityTitleText != null) abilityTitleText.text = title;
        if (abilityDescriptionText != null) abilityDescriptionText.text = description;
        if (abilityIcon != null) { abilityIcon.sprite = icon; abilityIcon.enabled = icon != null; }
    }

    private void AnimateIn()
    {
        if (popupRoot == null) return;

        if (_autoDismissCoroutine != null) StopCoroutine(_autoDismissCoroutine);
        popupRoot.DOKill();
        if (popupCanvasGroup != null) popupCanvasGroup.DOKill();

        // Posicionar fuera de pantalla (derecha) y transparente
        popupRoot.gameObject.SetActive(true);
        popupRoot.anchoredPosition = new Vector2(slideOffscreenX, popupRoot.anchoredPosition.y);
        if (popupCanvasGroup != null) popupCanvasGroup.alpha = 0f;

        _isShowing = true;

        popupRoot.DOAnchorPosX(0f, animInDuration).SetEase(Ease.OutBack).SetUpdate(true);
        if (popupCanvasGroup != null)
            popupCanvasGroup.DOFade(1f, animInDuration * 0.7f).SetUpdate(true);

        _autoDismissCoroutine = StartCoroutine(AutoDismiss());
    }

    private IEnumerator AutoDismiss()
    {
        yield return new WaitForSecondsRealtime(displayDuration);
        HidePopup();
    }

    public void HidePopup()
    {
        if (popupRoot == null || !_isShowing) return;

        _isShowing = false;
        _pendingAbility = null;
        _pendingAbilityKey = null;

        if (_autoDismissCoroutine != null) { StopCoroutine(_autoDismissCoroutine); _autoDismissCoroutine = null; }

        popupRoot.DOKill();
        if (popupCanvasGroup != null) popupCanvasGroup.DOKill();

        popupRoot.DOAnchorPosX(slideOffscreenX, animOutDuration).SetEase(Ease.InBack).SetUpdate(true)
            .OnComplete(() => { if (popupRoot != null) popupRoot.gameObject.SetActive(false); });
        if (popupCanvasGroup != null)
            popupCanvasGroup.DOFade(0f, animOutDuration).SetUpdate(true);
    }

    // ── Flags (persistencia de "ya mostrado") ──────────────────────────────────

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
                if (string.IsNullOrEmpty(flag)) continue;
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
        if (string.IsNullOrEmpty(flag)) return false;
        ReloadShownFlags();
        return _shownFlags.Contains(flag);
    }

    void MarkFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return;
        ReloadShownFlags();
        if (!_shownFlags.Add(flag)) return;
        var preset = UnlockService.GetActivePreset();
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new List<string>();
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);
    }
}
