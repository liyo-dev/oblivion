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

        [Header("Texto de estado")]
        [SerializeField] private TextMeshProUGUI followStatusText;
        [SerializeField] private string followingText = "Siguiendo";
        [SerializeField] private string freeText = "Libre";

        [Header("Nombres para identificar compañeros en el party")]
        [SerializeField] private string liamDisplayName = "Liam";
        [SerializeField] private string estelaDisplayName = "Estela";

        private Image[] _portraits; // 0=Liam, 1=Will, 2=Estela
        private Coroutine _flashRoutine;

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
        }

        private void Start()
        {
            if (followStatusText != null)
                followStatusText.gameObject.SetActive(false);
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
            if (followStatusText != null)
            {
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(FlashFollowText());
            }
        }

        private System.Collections.IEnumerator FlashFollowText()
        {
            followStatusText.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(1f);
            followStatusText.gameObject.SetActive(false);
            _flashRoutine = null;
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
            if (followStatusText != null)
                followStatusText.text = isFollowing ? followingText : freeText;
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
