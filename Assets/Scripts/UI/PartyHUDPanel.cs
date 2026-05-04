using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Game.NPC;

namespace Sendero.UI
{
    /// <summary>
    /// Panel de HUD del equipo con los retratos de los 3 personajes.
    ///
    /// Disposición: Liam (índice 0, izquierda) | Will (índice 1, centro) | Estela (índice 2, derecha)
    ///
    /// Alfa de los retratos:
    ///   - Personaje NO en el equipo: 0 (oculto)
    ///   - En el equipo, inactivo:    0.5
    ///   - Activo (controlado):       1.0
    ///
    /// Referencia de escena: añade este componente al GameObject del panel de equipo en el HUD.
    /// </summary>
    public class PartyHUDPanel : MonoBehaviour
    {
        [Header("Retratos del equipo")]
        [SerializeField] private Image liamPortrait;
        [SerializeField] private Image willPortrait;
        [SerializeField] private Image estelaPortrait;

        [Header("Alfa de los retratos")]
        [Tooltip("Alfa cuando el personaje está en el equipo pero no activo")]
        [SerializeField] private float inactiveAlpha = 0.5f;
        [Tooltip("Alfa cuando el personaje es el activo")]
        [SerializeField] private float activeAlpha = 1f;
        [Tooltip("Duración del tween de alpha al cambiar")]
        [SerializeField] private float alphaTweenDuration = 0.25f;
        [Tooltip("Escala de punch al activarse (ej: 0.1 = 10% de agrandamiento momentáneo)")]
        [SerializeField] private float portraitPunchStrength = 0.1f;
        [SerializeField] private float portraitPunchDuration = 0.35f;

        [Header("Panel de estado de seguimiento")]
        [SerializeField] private CanvasGroup followStatusPanel;
        [SerializeField] private TextMeshProUGUI followStatusText;
        [SerializeField] private string followingTextKey = "HUD_FOLLOW_STATUS_FOLLOWING";
        [SerializeField] private string freeTextKey = "HUD_FOLLOW_STATUS_FREE";
        [SerializeField] private float panelFadeInDuration = 0.25f;
        [SerializeField] private float panelVisibleDuration = 1f;
        [SerializeField] private float panelFadeOutDuration = 0.3f;
        [SerializeField] private float panelSlideOffset = 40f;

        [Header("Nombres para identificar compañeros en el party")]
        [SerializeField] private string liamDisplayName = "Liam";
        [SerializeField] private string estelaDisplayName = "Estela";

        private Image[] _portraits; // 0=Liam, 1=Will, 2=Estela
        private Sequence _panelSequence;
        private RectTransform _followStatusRect;
        private Vector2 _panelStartPos;

        private void Awake()
        {
            _portraits = new[] { liamPortrait, willPortrait, estelaPortrait };
            InitPortraits();
        }

        private void OnEnable()
        {
            PlayerParty.OnPartyChanged += OnPartyChanged;
            PartyControlManager.OnActiveCharacterChanged += OnActiveCharacterChanged;
            PartyControlManager.OnFollowModeChanged += OnFollowModeChanged;
        }

        private void OnDisable()
        {
            PlayerParty.OnPartyChanged -= OnPartyChanged;
            PartyControlManager.OnActiveCharacterChanged -= OnActiveCharacterChanged;
            PartyControlManager.OnFollowModeChanged -= OnFollowModeChanged;
            _panelSequence?.Kill();
        }

        private void Start()
        {
            if (followStatusPanel != null)
            {
                _followStatusRect = followStatusPanel.GetComponent<RectTransform>();
                _panelStartPos = _followStatusRect != null ? _followStatusRect.anchoredPosition : Vector2.zero;
                followStatusPanel.alpha = 0f;
                followStatusPanel.gameObject.SetActive(false);
            }
            Refresh();
        }

        // ─── Estado inicial ────────────────────────────────────────────────────────

        private void InitPortraits()
        {
            // Solo Will visible al inicio, los demás ocultos hasta que se unan
            SetAlphaImmediate(0, 0f);   // Liam oculto
            SetAlphaImmediate(1, 1f);   // Will activo
            SetAlphaImmediate(2, 0f);   // Estela oculta
        }

        // ─── Callbacks de eventos ──────────────────────────────────────────────────

        private void OnPartyChanged(IReadOnlyList<NPCPartyMember> members)
        {
            RefreshPortraits(PartyControlManager.Instance?.ActiveIndex ?? 1);
        }

        private void OnActiveCharacterChanged(int newIndex)
        {
            RefreshPortraits(newIndex);
        }

        private void OnFollowModeChanged(bool isFollowing)
        {
            UpdateFollowText(isFollowing);
            ShowFollowStatusPanel();
        }

        private void ShowFollowStatusPanel()
        {
            if (followStatusPanel == null) return;

            _panelSequence?.Kill();

            followStatusPanel.gameObject.SetActive(true);
            followStatusPanel.alpha = 0f;

            if (_followStatusRect != null)
                _followStatusRect.anchoredPosition = _panelStartPos + Vector2.up * panelSlideOffset;

            _panelSequence = DOTween.Sequence().SetUpdate(true);
            _panelSequence.Append(followStatusPanel.DOFade(1f, panelFadeInDuration).SetEase(Ease.OutQuad));
            if (_followStatusRect != null)
                _panelSequence.Join(_followStatusRect.DOAnchorPos(_panelStartPos, panelFadeInDuration).SetEase(Ease.OutBack));
            _panelSequence.AppendInterval(panelVisibleDuration);
            _panelSequence.Append(followStatusPanel.DOFade(0f, panelFadeOutDuration).SetEase(Ease.InQuad));
            if (_followStatusRect != null)
                _panelSequence.Join(_followStatusRect.DOAnchorPos(_panelStartPos + Vector2.up * panelSlideOffset, panelFadeOutDuration).SetEase(Ease.InQuad));
            _panelSequence.OnComplete(() =>
            {
                followStatusPanel.gameObject.SetActive(false);
                _panelSequence = null;
            });
        }

        // ─── Refresco completo ─────────────────────────────────────────────────────

        private void Refresh()
        {
            int activeIdx = PartyControlManager.Instance?.ActiveIndex ?? 1;
            bool following = PartyControlManager.Instance == null || PartyControlManager.Instance.IsPartyFollowing;
            RefreshPortraits(activeIdx);
            UpdateFollowText(following);
        }

        private void RefreshPortraits(int activeIndex)
        {
            bool liamInParty = IsInParty(liamDisplayName);
            bool estelaInParty = IsInParty(estelaDisplayName);

            // Liam (0)
            float liamTarget = !liamInParty ? 0f : (activeIndex == 0 ? activeAlpha : inactiveAlpha);
            TweenAlpha(0, liamTarget);

            // Will (1) — siempre en el equipo
            TweenAlpha(1, activeIndex == 1 ? activeAlpha : inactiveAlpha);

            // Estela (2)
            float estelaTarget = !estelaInParty ? 0f : (activeIndex == 2 ? activeAlpha : inactiveAlpha);
            TweenAlpha(2, estelaTarget);
        }

        private void UpdateFollowText(bool isFollowing)
        {
            if (followStatusText == null) return;
            string key = isFollowing ? followingTextKey : freeTextKey;
            string fallback = isFollowing ? "Siguiendo" : "Libre";
            followStatusText.text = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.Get(key, fallback)
                : fallback;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private bool IsInParty(string displayName)
        {
            return PlayerParty.Instance?.GetMemberByName(displayName) != null;
        }

        private void TweenAlpha(int index, float target)
        {
            if (_portraits[index] == null) return;
            _portraits[index].DOKill();
            _portraits[index].DOFade(target, alphaTweenDuration).SetUpdate(true);

            var t = _portraits[index].transform;
            t.DOKill();
            if (target >= activeAlpha)
                t.DOPunchScale(Vector3.one * portraitPunchStrength, portraitPunchDuration, 1, 0.5f).SetUpdate(true);
        }

        private void SetAlphaImmediate(int index, float alpha)
        {
            if (_portraits[index] == null) return;
            var c = _portraits[index].color;
            c.a = alpha;
            _portraits[index].color = c;
        }
    }
}