using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC
{
    /// <summary>
    /// Gestiona los prompts "Sígueme" / "Dejar de seguir" sobre la cabeza del NPC compañero.
    ///
    /// "Sígueme" aparece cuando:
    ///   - El NPC está disponible (no siguiendo activamente) Y
    ///   - Ningún otro compañero está siguiendo activamente al jugador.
    ///
    /// "Dejar de seguir" aparece cuando este NPC está siguiendo activamente.
    ///
    /// Presionar A ejecuta la acción correspondiente.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompanionFollowPrompt : MonoBehaviour
    {
        [Header("Proximidad")]
        [Tooltip("Distancia máxima al jugador para mostrar el icono.")]
        [SerializeField] private float proximityRadius = 3.5f;
        [Tooltip("Intervalo de verificación de distancia (segundos).")]
        [SerializeField] private float checkInterval = 0.15f;

        private NPCPartyMember _partyMember;
        private NPCBehaviourManagerV2 _npcManager;
        private NPCAlertIconController _iconController;
        private Interactable _interactable;
        private PlayerParty _playerParty;
        private GameObject _followIconPrefab;
        private GameObject _stopFollowIconPrefab;
        private bool _listenerAdded;

        private enum PromptMode { None, Follow, StopFollow }
        private PromptMode _currentMode;
        private float _nextCheck;

        public bool IsShowingPrompt => _currentMode != PromptMode.None;

        public void SetFollowIcon(GameObject iconPrefab)     => _followIconPrefab = iconPrefab;
        public void SetStopFollowIcon(GameObject iconPrefab) => _stopFollowIconPrefab = iconPrefab;

        private void Awake()
        {
            _npcManager   = GetComponent<NPCBehaviourManagerV2>();
            _interactable = GetComponent<Interactable>();

            const string childName = "_FollowPromptIcon";
            var child = transform.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }
            _iconController = child.GetComponent<NPCAlertIconController>()
                           ?? child.gameObject.AddComponent<NPCAlertIconController>();
        }

        private void Start()
        {
            _partyMember = GetComponent<NPCPartyMember>();
            if (_partyMember == null)
            {
                Debug.LogWarning($"[CompanionFollowPrompt:{name}] NPCPartyMember no encontrado — desactivando.");
                enabled = false;
                return;
            }

            _playerParty = PlayerParty.Instance;

            _partyMember.OnLeft   += OnLeftParty;
            _partyMember.OnJoined += OnJoinedParty;

            if (_interactable != null)
            {
                _interactable.OnInteract.AddListener(OnPlayerInteracted);
                _listenerAdded = true;
            }
        }

        private void OnDestroy()
        {
            if (_partyMember != null)
            {
                _partyMember.OnLeft   -= OnLeftParty;
                _partyMember.OnJoined -= OnJoinedParty;
            }
            if (_listenerAdded && _interactable != null)
                _interactable.OnInteract.RemoveListener(OnPlayerInteracted);
        }

        private void OnLeftParty()  => _nextCheck = 0f;
        private void OnJoinedParty() => HidePrompt();

        private void OnPlayerInteracted(GameObject _)
        {
            if (_currentMode == PromptMode.Follow)
            {
                HidePrompt();
                if (_npcManager?.Context != null)
                    _npcManager.Context.IsPinnedByParty = false;
                _partyMember?.StartFollowing();
            }
            else if (_currentMode == PromptMode.StopFollow)
            {
                HidePrompt();
                _partyMember?.StopFollowing();
                if (_npcManager?.Context != null)
                    _npcManager.Context.IsPinnedByParty = true;
            }
        }

        private void Update()
        {
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + checkInterval;

            if (_partyMember == null) return;

            // Suprimir durante cinemática o combate
            if (_npcManager?.Context != null &&
                (_npcManager.Context.IsInCinematic || _npcManager.Context.IsInCombat))
            {
                HidePrompt();
                return;
            }

            var playerTransform = _npcManager?.Player;
            if (playerTransform == null)
            {
                HidePrompt();
                return;
            }

            bool inRange = (playerTransform.position - transform.position).sqrMagnitude
                           <= proximityRadius * proximityRadius;

            PromptMode desired = PromptMode.None;

            if (inRange)
            {
                // "Dejar de seguir" solo en modo Libre (D-Pad Down), no cuando el equipo sigue junto.
                bool partyInLibreMode = !(PartyControlManager.Instance?.IsPartyFollowing ?? true);
                bool isActivelyFollowing = partyInLibreMode &&
                                           _partyMember.IsInParty &&
                                           _npcManager?.Context?.IsPinnedByParty == false;

                if (isActivelyFollowing)
                {
                    desired = PromptMode.StopFollow;
                }
                else
                {
                    // "Sígueme" solo si: el equipo está completo (todos los compañeros unidos),
                    // el jugador ha separado el equipo (modo Libre) y este miembro fue detenido.
                    bool fullParty = (_playerParty?.MemberCount ?? 0) >= 2;
                    bool isAvailable = fullParty &&
                                       partyInLibreMode &&
                                       _partyMember.IsInParty &&
                                       _npcManager?.Context?.IsPinnedByParty == true;

                    if (isAvailable && !AnyOtherMemberActivelyFollowing())
                        desired = PromptMode.Follow;
                }
            }

            if (desired != _currentMode)
                ApplyMode(desired);
        }

        private void ApplyMode(PromptMode mode)
        {
            _currentMode = mode;
            _iconController?.HideAlertIcon();

            if (mode == PromptMode.Follow && _followIconPrefab != null)
                _iconController?.ShowPersistentIcon(_followIconPrefab);
            else if (mode == PromptMode.StopFollow && _stopFollowIconPrefab != null)
                _iconController?.ShowPersistentIcon(_stopFollowIconPrefab);
        }

        private void HidePrompt()
        {
            if (_currentMode == PromptMode.None) return;
            _currentMode = PromptMode.None;
            _iconController?.HideAlertIcon();
        }

        // Verifica si algún otro miembro del party está siguiendo activamente (IsInParty && !IsPinnedByParty)
        private bool AnyOtherMemberActivelyFollowing()
        {
            if (_playerParty == null) return false;
            var members = _playerParty.Members;
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                if (m == null || m == _partyMember) continue;
                if (m.IsInParty && m.NPCManager?.Context?.IsPinnedByParty == false)
                    return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
        }
#endif
    }
}
