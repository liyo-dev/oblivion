using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.NPC;

/// <summary>
/// HUD para los dos medidores de carga de ataques especiales dúo.
///
/// Estructura recomendada en el Canvas:
///   SpecialChargeMeterUI  ← este componente
///   ├── SlotEstela
///   │   ├── EstelaIcon         ← Image (retrato de Estela)     → estelaIcon
///   │   ├── EstelaFillBg       ← Image (fondo de la barra)
///   │   ├── EstelaFill         ← Image Filled/Horizontal       → estelaFill
///   │   ├── EstelaGlow         ← Image (brillo cuando listo)   → estelaGlow
///   │   └── EstelaReady        ← GameObject "¡LISTO!"          → estelaReadyIndicator
///   └── SlotLiam
///       ├── LiamIcon           ← Image (retrato de Liam)       → liamIcon
///       ├── LiamFillBg         ← Image (fondo de la barra)
///       ├── LiamFill           ← Image Filled/Horizontal       → liamFill
///       ├── LiamGlow           ← Image (brillo cuando listo)   → liamGlow
///       └── LiamReady          ← GameObject "¡LISTO!"          → liamReadyIndicator
/// </summary>
public class SpecialChargeMeterUI : MonoBehaviour
{
    // ── Slot Estela (LT) ───────────────────────────────────────────────────

    [Header("Slot Estela (LT)")]
    [SerializeField] private Image      estelaFill;
    [SerializeField] private Image      estelaIcon;
    [SerializeField] private Image      estelaGlow;
    [SerializeField] private GameObject estelaReadyIndicator;

    // ── Slot Liam (RT) ─────────────────────────────────────────────────────

    [Header("Slot Liam (RT)")]
    [SerializeField] private Image      liamFill;
    [SerializeField] private Image      liamIcon;
    [SerializeField] private Image      liamGlow;
    [SerializeField] private GameObject liamReadyIndicator;

    // ── Colores ────────────────────────────────────────────────────────────

    [Header("Colores de barras")]
    [SerializeField] private Color emptyColor = new Color(0.25f, 0.25f, 0.7f, 1f);
    [SerializeField] private Color fullColor  = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color readyColor = new Color(1f, 0.92f, 0.2f, 1f);

    [Header("Colores de iconos")]
    [SerializeField] private Color iconAvailableColor   = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color iconUnavailableColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);

    // ── Animación ──────────────────────────────────────────────────────────

    [Header("Animación")]
    [SerializeField] private float fillAnimSpeed       = 3f;
    [SerializeField] private float readyPulseDuration  = 0.35f;

    // ── Compañeros ─────────────────────────────────────────────────────────

    [Header("Compañeros")]
    [SerializeField] private string estelaDisplayName    = "Estela";
    [SerializeField] private string liamDisplayName      = "Liam";
    [SerializeField] private float  companionCheckRadius = 10f;
    [SerializeField] private float  companionCheckInterval = 0.4f;

    // ── Internos ───────────────────────────────────────────────────────────

    private DuoSpecialAttackSystem _duo;
    private SpecialChargeMeter     _estelaM;
    private SpecialChargeMeter     _liamM;

    private float _estelaTarget, _estelaDisplay;
    private float _liamTarget,   _liamDisplay;
    private bool  _estelaWasReady, _liamWasReady;

    private float     _companionTimer;
    private Coroutine _estelaFlash;
    private Coroutine _liamFlash;

    // ── Ciclo de vida ──────────────────────────────────────────────────────

    void OnEnable()
    {
        Connect();
        RefreshImmediate();
    }

    void OnDisable()
    {
        Disconnect();
        StopAllCoroutines();
        _estelaFlash = _liamFlash = null;
    }

    void Update()
    {
        AnimateFill(ref _estelaDisplay, _estelaTarget, estelaFill, _estelaM);
        AnimateFill(ref _liamDisplay,   _liamTarget,   liamFill,   _liamM);

        _companionTimer -= Time.deltaTime;
        if (_companionTimer <= 0f)
        {
            _companionTimer = companionCheckInterval;
            RefreshCompanionSlots();
        }
    }

    // ── Conexión ───────────────────────────────────────────────────────────

    private void Connect()
    {
        // Buscar DuoSpecialAttackSystem en el jugador
        var player = PlayerService.Player;
        if (player != null)
            _duo = player.GetComponentInChildren<DuoSpecialAttackSystem>();
        if (_duo == null)
            _duo = FindFirstObjectByType<DuoSpecialAttackSystem>();

        if (_duo == null)
        {
            Debug.LogWarning("[SpecialChargeMeterUI] No se encontró DuoSpecialAttackSystem.");
            return;
        }

        _estelaM = _duo.EstelaChargeMeter;
        _liamM   = _duo.LiamChargeMeter;

        if (_estelaM != null) _estelaM.OnChargeUpdated += OnEstelaCharge;
        if (_liamM   != null) _liamM.OnChargeUpdated   += OnLiamCharge;
    }

    private void Disconnect()
    {
        if (_estelaM != null) _estelaM.OnChargeUpdated -= OnEstelaCharge;
        if (_liamM   != null) _liamM.OnChargeUpdated   -= OnLiamCharge;
        _estelaM = _liamM = null;
        _duo     = null;
    }

    private void RefreshImmediate()
    {
        float ep = _estelaM != null ? _estelaM.Normalized : 0f;
        float lp = _liamM   != null ? _liamM.Normalized   : 0f;

        _estelaDisplay = _estelaTarget = ep;
        _liamDisplay   = _liamTarget   = lp;

        SetFill(estelaFill, ep);
        SetFill(liamFill,   lp);

        SetReadyIndicator(estelaReadyIndicator, _estelaM != null && _estelaM.IsFullyCharged);
        SetReadyIndicator(liamReadyIndicator,   _liamM   != null && _liamM.IsFullyCharged);

        RefreshCompanionSlots();
    }

    // ── Callbacks de carga ─────────────────────────────────────────────────

    private void OnEstelaCharge(float normalized)
    {
        _estelaTarget = Mathf.Clamp01(normalized);

        bool ready = _estelaM != null && _estelaM.IsFullyCharged;
        SetReadyIndicator(estelaReadyIndicator, ready);

        if (ready && !_estelaWasReady)
            TriggerFlash(estelaFill, ref _estelaFlash);

        _estelaWasReady = ready;
    }

    private void OnLiamCharge(float normalized)
    {
        _liamTarget = Mathf.Clamp01(normalized);

        bool ready = _liamM != null && _liamM.IsFullyCharged;
        SetReadyIndicator(liamReadyIndicator, ready);

        if (ready && !_liamWasReady)
            TriggerFlash(liamFill, ref _liamFlash);

        _liamWasReady = ready;
    }

    // ── Animación de fill ──────────────────────────────────────────────────

    private void AnimateFill(ref float display, float target, Image fill, SpecialChargeMeter meter)
    {
        if (Mathf.Approximately(display, target)) return;

        display = Mathf.MoveTowards(display, target, fillAnimSpeed * Time.deltaTime);
        SetFill(fill, display);
    }

    private void SetFill(Image fill, float normalized)
    {
        if (fill == null) return;
        fill.fillAmount = normalized;
        fill.color      = normalized >= 1f ? readyColor : Color.Lerp(emptyColor, fullColor, normalized);
    }

    // ── Flash de "¡LISTO!" ─────────────────────────────────────────────────

    private void TriggerFlash(Image fill, ref Coroutine slot)
    {
        if (!isActiveAndEnabled || fill == null) return;
        if (slot != null) StopCoroutine(slot);
        slot = StartCoroutine(Co_Flash(fill));
    }

    private IEnumerator Co_Flash(Image fill)
    {
        for (int i = 0; i < 2; i++)
        {
            float half = readyPulseDuration * 0.5f;
            float t = 0f;
            while (t < half) { t += Time.deltaTime; fill.color = Color.Lerp(readyColor, Color.white, t / half); yield return null; }
            t = 0f;
            while (t < half) { t += Time.deltaTime; fill.color = Color.Lerp(Color.white, readyColor, t / half); yield return null; }
        }
        fill.color = readyColor;
    }

    // ── Disponibilidad de compañeros ───────────────────────────────────────

    private void SetReadyIndicator(GameObject indicator, bool active)
    {
        if (indicator != null) indicator.SetActive(active);
    }

    private void RefreshCompanionSlots()
    {
        bool estelaReady = _estelaM != null && _estelaM.IsFullyCharged;
        bool liamReady   = _liamM   != null && _liamM.IsFullyCharged;

        bool estelaInRange = estelaReady && IsCompanionInRange(estelaDisplayName);
        bool liamInRange   = liamReady   && IsCompanionInRange(liamDisplayName);

        SetSlotAvailability(estelaIcon, estelaGlow, estelaInRange);
        SetSlotAvailability(liamIcon,   liamGlow,   liamInRange);
    }

    private void SetSlotAvailability(Image icon, Image glow, bool available)
    {
        if (icon != null) icon.color  = available ? iconAvailableColor : iconUnavailableColor;
        if (glow != null) glow.enabled = available;
    }

    private bool IsCompanionInRange(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return false;
        var party  = PlayerParty.Instance;
        if (party == null) return false;
        var member = party.GetMemberByName(displayName);
        return member != null && member.GetDistanceToPlayer() <= companionCheckRadius;
    }

    // ── API pública ────────────────────────────────────────────────────────

    public void Reconnect()
    {
        Disconnect();
        Connect();
        RefreshImmediate();
    }
}
