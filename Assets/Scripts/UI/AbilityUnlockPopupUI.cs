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
    [SerializeField] private List<SpellPresentation> spellPresentations = new();

    [Header("Animación")]
    [SerializeField] private float slideOffscreenX = 450f;
    [SerializeField] private float animInDuration = 0.45f;
    [SerializeField] private float animOutDuration = 0.3f;
    [SerializeField] private float displayDuration = 3.5f;

    private AbilityId? _pendingAbility;
    private AbilityKey? _pendingAbilityKey;
    private SpellId? _pendingSpell;
    private readonly HashSet<string> _shownFlags = new();
    private bool _flagsLoaded;
    private Coroutine _autoDismissCoroutine;
    private bool _isShowing;
    // True si ShowPopup() ya preparó los textos/flag pero difirió AnimateIn() porque la intro de
    // un boss estaba ocultando la UI persistente (ver SceneBoundUI.BeginBossIntro/EndBossIntro).
    private bool _awaitingBossIntroEnd;
    // Oculto temporalmente porque hay un menú (pausa, equipo, tienda...) abierto encima.
    private bool _hiddenByMenu;

    const string AbilityFlagPrefix = "ABILITY_POPUP_ID:";
    const string AbilityKeyFlagPrefix = "ABILITY_POPUP_KEY:";
    const string SpellFlagPrefix = "SPELL_POPUP_ID:";

    void Awake()
    {
        if (popupRoot != null)
            popupRoot.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(AbilityUnlockPopupUI));
        GameBootService.OnProfileReady += HandleProfileReady;
        UnlockService.OnAbilityUnlocked += HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey += HandleAbilityUnlockedKey;
        UnlockService.OnSpellUnlocked += HandleSpellUnlocked;
        SceneBoundUI.OnBossIntroEnded += HandleBossIntroEnded;
        // Mismo sistema que ya usan BossHealthBar/MinimapController: ocultarse mientras
        // hay un menú abierto (pausa incluida) y restaurarse al cerrar el último.
        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;
        ReloadShownFlags();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        UnlockService.OnAbilityUnlocked -= HandleAbilityUnlocked;
        UnlockService.OnAbilityUnlockedKey -= HandleAbilityUnlockedKey;
        UnlockService.OnSpellUnlocked -= HandleSpellUnlocked;
        SceneBoundUI.OnBossIntroEnded -= HandleBossIntroEnded;
        MenuManager.MenuOpened -= OnMenuOpened;
        MenuManager.MenuClosed -= OnMenuClosed;
    }

    void OnDestroy()
    {
        if (popupRoot != null) popupRoot.DOKill();
        if (popupCanvasGroup != null) popupCanvasGroup.DOKill();
    }

    // ── Recarga de flags al cambiar de preset ──────────────────────────────────

    private void HandleProfileReady()
    {
        _flagsLoaded = false;
        ReloadShownFlags();
    }

    // ── Handlers de desbloqueo ─────────────────────────────────────────────────

    private void HandleAbilityUnlocked(AbilityId ability)
    {
        GameLog.Log("AbilityUnlockPopupUI", $"Evento recibido: AbilityUnlocked({ability})");
        _pendingAbility = ability;
        _pendingAbilityKey = null;
        _pendingSpell = null;
        ShowPopup();
    }

    private void HandleAbilityUnlockedKey(AbilityKey key)
    {
        GameLog.Log("AbilityUnlockPopupUI", $"Evento recibido: AbilityUnlockedKey({key})");
        _pendingAbilityKey = key;
        _pendingAbility = null;
        _pendingSpell = null;
        ShowPopup();
    }

    private void HandleSpellUnlocked(SpellId spell)
    {
        GameLog.Log("AbilityUnlockPopupUI", $"Evento recibido: SpellUnlocked({spell})");
        _pendingSpell = spell;
        _pendingAbility = null;
        _pendingAbilityKey = null;
        ShowPopup();
    }

    // ── Lógica de popup ────────────────────────────────────────────────────────

    private void ShowPopup()
    {
        if (_pendingAbility == null && _pendingAbilityKey == null && _pendingSpell == null) return;
        ReloadShownFlags();

        if (_pendingAbility != null)
        {
            string flag = GetAbilityFlag(_pendingAbility.Value);
            if (HasSeenFlag(flag))
            {
                GameLog.Log("AbilityUnlockPopupUI", $"Ya mostrado ({flag}), saltando.");
                _pendingAbility = null;
                return;
            }
            var p = AbilityPresentationLookup.Resolve(_pendingAbility.Value, abilityPresentations);
            MarkFlag(flag);
            SetTexts(p.title, p.description, p.icon);
        }
        else if (_pendingAbilityKey != null)
        {
            string flag = GetAbilityKeyFlag(_pendingAbilityKey.Value);
            if (HasSeenFlag(flag))
            {
                GameLog.Log("AbilityUnlockPopupUI", $"Ya mostrado ({flag}), saltando.");
                _pendingAbilityKey = null;
                return;
            }
            var p = AbilityPresentationKeyLookup.Resolve(_pendingAbilityKey.Value, abilityKeyPresentations);
            MarkFlag(flag);
            SetTexts(p.title, p.description, p.icon);
        }
        else
        {
            string flag = GetSpellFlag(_pendingSpell.Value);
            if (HasSeenFlag(flag))
            {
                GameLog.Log("AbilityUnlockPopupUI", $"Ya mostrado ({flag}), saltando.");
                _pendingSpell = null;
                return;
            }
            var p = SpellPresentationLookup.Resolve(_pendingSpell.Value, spellPresentations);
            MarkFlag(flag);
            SetTexts(p.title, p.description, p.icon);
        }

        if (SceneBoundUI.IsBossIntroActive)
        {
            // No animar durante la intro del boss: BeginBossIntro la dejaría a alpha 0 igualmente
            // y el timer de AutoDismiss() correría en tiempo real mientras el popup es invisible.
            // Esperar a OnBossIntroEnded para que aparezca ya con la escena/diálogo normal en marcha.
            _awaitingBossIntroEnd = true;
            GameLog.Log("AbilityUnlockPopupUI", "Intro de boss activa: difiriendo popup hasta que termine.");
            return;
        }

        AnimateIn();
    }

    private void HandleBossIntroEnded()
    {
        if (!_awaitingBossIntroEnd) return;
        _awaitingBossIntroEnd = false;
        AnimateIn();
    }

    // ── MenuManager (pausa / cualquier menú) ────────────────────────────────────

    /// <summary>Oculta el popup mientras haya un menú (pausa incluida) abierto encima.</summary>
    private void OnMenuOpened(MenuKind kind)
    {
        if (!_isShowing || _hiddenByMenu || popupCanvasGroup == null) return;
        _hiddenByMenu = true;
        popupCanvasGroup.DOKill();
        popupCanvasGroup.DOFade(0f, 0.15f).SetUpdate(true);
        popupCanvasGroup.blocksRaycasts = false;
    }

    /// <summary>Restaura el popup al cerrarse el último menú abierto, si seguía en pantalla.</summary>
    private void OnMenuClosed(MenuKind kind)
    {
        if (!_hiddenByMenu) return;
        if (MenuManager.AnyOpen()) return; // todavía queda otro menú abierto
        _hiddenByMenu = false;

        if (!_isShowing || popupCanvasGroup == null) return; // se auto-cerró mientras estaba oculto
        popupCanvasGroup.DOKill();
        popupCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        popupCanvasGroup.blocksRaycasts = true;
    }

    private void SetTexts(string title, string description, Sprite icon)
    {
        if (abilityTitleText != null) abilityTitleText.text = title;
        if (abilityDescriptionText != null) abilityDescriptionText.text = description;
        if (abilityIcon != null) { abilityIcon.sprite = icon; abilityIcon.enabled = icon != null; }
    }

    private void AnimateIn()
    {
        if (popupRoot == null)
        {
            GameLog.Error("AbilityUnlockPopupUI", "popupRoot es null — asignar en el Inspector.");
            return;
        }

        GameLog.Log("AbilityUnlockPopupUI", $"AnimateIn — root activo: {gameObject.activeInHierarchy}, popupRoot activo: {popupRoot.gameObject.activeInHierarchy}");

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
        _pendingSpell = null;

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
                if (flag.StartsWith(AbilityFlagPrefix) || flag.StartsWith(AbilityKeyFlagPrefix) || flag.StartsWith(SpellFlagPrefix))
                    _shownFlags.Add(flag);
            }
        }
        _flagsLoaded = true;
    }

    string GetAbilityFlag(AbilityId id) => $"{AbilityFlagPrefix}{id}";
    string GetAbilityKeyFlag(AbilityKey key) => $"{AbilityKeyFlagPrefix}{key}";
    string GetSpellFlag(SpellId id) => $"{SpellFlagPrefix}{id}";

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
