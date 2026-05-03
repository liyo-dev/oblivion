using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;
using Game.NPC;

/// <summary>
/// Gestiona el personaje activo y el modo seguir/libre del equipo.
///
/// Slots fijos: 0 = Liam (izquierda), 1 = Will (centro), 2 = Estela (derecha)
///
/// DPad Down  → alterna Siguiendo / Libre para todos los compañeros
/// DPad Left  → cambia al personaje disponible a la izquierda
/// DPad Right → cambia al personaje disponible a la derecha
///
/// El cambio de personaje se delega a ActiveCharacterSwapper, que gestiona
/// el teleport del controller, el swap de apariencia y los hechizos.
/// </summary>
[DefaultExecutionOrder(-200)]
public class PartyControlManager : MonoBehaviour
{
    #region Singleton
    public static PartyControlManager Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnActiveCharacterChanged = null;
        OnFollowModeChanged = null;
    }
#endif
    #endregion

    #region Slots
    public enum CharacterSlot { Liam = 0, Will = 1, Estela = 2 }
    #endregion

    #region Inspector
    [Header("Nombres en el party (deben coincidir con NPCPartyConfig.displayName)")]
    [SerializeField] private string liamDisplayName = "Liam";
    [SerializeField] private string estelaDisplayName = "Estela";
    #endregion

    #region State
    private int _activeIndex = (int)CharacterSlot.Will;
    private bool _isPartyFollowing = true;
    #endregion

    #region Events
    /// <summary>Se dispara cuando cambia el personaje activo (índice 0=Liam, 1=Will, 2=Estela)</summary>
    public static event Action<int> OnActiveCharacterChanged;

    /// <summary>Se dispara cuando cambia el modo del equipo (true=Siguiendo, false=Libre)</summary>
    public static event Action<bool> OnFollowModeChanged;
    #endregion

    #region Properties
    public int ActiveIndex => _activeIndex;
    public bool IsPartyFollowing => _isPartyFollowing;
    public CharacterSlot ActiveSlot => (CharacterSlot)_activeIndex;
    #endregion

    #region Lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        GamepadInputReader.OnInput += HandleInput;
    }

    private void OnDestroy()
    {
        GamepadInputReader.OnInput -= HandleInput;
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Input
    private void HandleInput(GamepadInputReader.InputEvent evt)
    {
        if (evt.Phase != InputActionPhase.Performed) return;

        switch (evt.Type)
        {
            case GamepadInputReader.InputEventType.DpadDown:
                ToggleFollowMode();
                break;
            case GamepadInputReader.InputEventType.DpadLeft:
                TrySwitchLeft();
                break;
            case GamepadInputReader.InputEventType.DpadRight:
                TrySwitchRight();
                break;
        }
    }
    #endregion

    #region Character Switching
    private void TrySwitchLeft()
    {
        for (int i = _activeIndex - 1; i >= 0; i--)
        {
            if (IsSlotAvailable(i))
            {
                SwitchToCharacter(i);
                return;
            }
        }
    }

    private void TrySwitchRight()
    {
        for (int i = _activeIndex + 1; i <= 2; i++)
        {
            if (IsSlotAvailable(i))
            {
                SwitchToCharacter(i);
                return;
            }
        }
    }

    private bool IsSlotAvailable(int index)
    {
        if (index == (int)CharacterSlot.Will) return true;
        string name = index == (int)CharacterSlot.Liam ? liamDisplayName : estelaDisplayName;
        return PlayerParty.Instance?.GetMemberByName(name) != null;
    }

    private void SwitchToCharacter(int newIndex)
    {
        if (newIndex == _activeIndex) return;

        var from = (CharacterSlot)_activeIndex;
        var to   = (CharacterSlot)newIndex;

        _activeIndex = newIndex;

        ActiveCharacterSwapper.Instance?.SwitchCharacter(from, to);

        OnActiveCharacterChanged?.Invoke(_activeIndex);
    }
    #endregion

    #region Follow Mode
    private void ToggleFollowMode()
    {
        _isPartyFollowing = !_isPartyFollowing;
        ApplyFollowModeToParty();
        OnFollowModeChanged?.Invoke(_isPartyFollowing);
    }

    private void ApplyFollowModeToParty()
    {
        var party = PlayerParty.Instance;
        if (party == null) return;

        var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;

        foreach (var member in party.Members)
        {
            if (member == null || member == hiddenNpc) continue;

            if (_isPartyFollowing)
                member.StartFollowing();
            else
                member.StopFollowing();
        }
    }
    #endregion

    #region Public API
    /// <summary>Fuerza el refresco de eventos al cargar una escena o inicializar la UI.</summary>
    public void ForceRefreshFollowMode()
    {
        OnFollowModeChanged?.Invoke(_isPartyFollowing);
        OnActiveCharacterChanged?.Invoke(_activeIndex);
    }
    #endregion
}
