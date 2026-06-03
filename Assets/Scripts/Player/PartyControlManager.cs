using System;
using System.Collections.Generic;
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
    private bool _switchingLocked;
    private int _pendingSlotToRestore = -1;
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
    public bool IsSwitchingLocked => _switchingLocked;
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
        GameBootService.OnProfileReady += HandleProfileReady;
        WillOnlyMomentManager.OnSwitchLockChanged += SetSwitchingLocked;
    }

    private void OnDestroy()
    {
        GamepadInputReader.OnInput -= HandleInput;
        GameBootService.OnProfileReady -= HandleProfileReady;
        WillOnlyMomentManager.OnSwitchLockChanged -= SetSwitchingLocked;
        PlayerParty.OnPartyChanged -= TryRestoreSavedSlot;
        if (Instance == this) Instance = null;
    }
    #endregion

    #region Input
    private void HandleInput(GamepadInputReader.InputEvent evt)
    {
        if (evt.Phase != InputActionPhase.Performed) return;
        if (Core.PlayerInputManager.Instance != null && Core.PlayerInputManager.Instance.IsInUIMode) return;

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
        if (_switchingLocked) { Debug.LogWarning($"[PartyControlManager] DPad-Left ignorado — switching BLOQUEADO. _activeIndex={_activeIndex}"); return; }

        Debug.Log($"[PartyControlManager] TrySwitchLeft — _activeIndex={_activeIndex}");
        for (int i = _activeIndex - 1; i >= 0; i--)
        {
            if (IsSlotAvailable(i))
            {
                SwitchToCharacter(i);
                return;
            }
        }
        Debug.Log($"[PartyControlManager] TrySwitchLeft — no hay slot disponible a la izquierda de {_activeIndex}");
    }

    private void TrySwitchRight()
    {
        if (_switchingLocked) { Debug.LogWarning($"[PartyControlManager] DPad-Right ignorado — switching BLOQUEADO. _activeIndex={_activeIndex}"); return; }

        Debug.Log($"[PartyControlManager] TrySwitchRight — _activeIndex={_activeIndex}");
        for (int i = _activeIndex + 1; i <= 2; i++)
        {
            if (IsSlotAvailable(i))
            {
                SwitchToCharacter(i);
                return;
            }
        }
        Debug.Log($"[PartyControlManager] TrySwitchRight — no hay slot disponible a la derecha de {_activeIndex}");
    }

    private void SetSwitchingLocked(bool locked)
    {
        _switchingLocked = locked;
    }

    private bool IsSlotAvailable(int index)
    {
        // Un personaje muerto en batalla no puede ser seleccionado
        if (PartyStatsManager.Instance?.IsDead(index) == true) return false;
        if (index == (int)CharacterSlot.Will) return true;
        string name = index == (int)CharacterSlot.Liam ? liamDisplayName : estelaDisplayName;
        return PlayerParty.Instance?.GetMemberByName(name) != null;
    }

    private void SwitchToCharacter(int newIndex)
    {
        if (newIndex == _activeIndex) return;

        var from = (CharacterSlot)_activeIndex;
        var to   = (CharacterSlot)newIndex;

        var swapper = ActiveCharacterSwapper.Instance;
        if (swapper == null)
        {
            Debug.LogError($"[PartyControlManager] SwitchToCharacter({from}→{to}) ABORTADO — ActiveCharacterSwapper.Instance es null. _activeIndex NO modificado.");
            return;
        }
        if (!swapper.IsReady)
        {
            Debug.LogError($"[PartyControlManager] SwitchToCharacter({from}→{to}) ABORTADO — ActiveCharacterSwapper.IsReady=false. _activeIndex NO modificado.");
            return;
        }

        _activeIndex = newIndex;
        swapper.SwitchCharacter(from, to);
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

    /// <summary>
    /// Fuerza el cambio de personaje activo, ignorando el bloqueo de switching.
    /// Usado internamente por WillOnlyMomentManager.
    /// </summary>
    public void ForceSwitch(CharacterSlot slot)
    {
        SwitchToCharacter((int)slot);
    }
    #endregion

    private void HandleProfileReady()
    {
        Debug.Log($"[PartyControlManager] 🔄 GameBootService.OnProfileReady recibido. _activeIndex={_activeIndex} ({(CharacterSlot)_activeIndex}). Reinicializando estado del party.");

        // Cancelar cualquier restauración diferida de una sesión anterior
        PlayerParty.OnPartyChanged -= TryRestoreSavedSlot;
        _pendingSlotToRestore = -1;

        // Al cargar partida siempre se arranca como Will
        _activeIndex = (int)CharacterSlot.Will;
        ActiveCharacterSwapper.Instance?.ResetState();

        _isPartyFollowing = true;
        ForceRefreshFollowMode();
    }

    private void TryRestoreSavedSlot(IReadOnlyList<NPCPartyMember> _)
    {
        if (_pendingSlotToRestore < 0) return;
        if (!IsSlotAvailable(_pendingSlotToRestore)) return;
        // Si el swapper aún no inicializó su Start(), SwitchCharacter falla silenciosamente.
        // Mantener la suscripción a OnPartyChanged y esperar a OnSwapperReady().
        if (ActiveCharacterSwapper.Instance == null || !ActiveCharacterSwapper.Instance.IsReady) return;

        PlayerParty.OnPartyChanged -= TryRestoreSavedSlot;
        Debug.Log($"[PartyControlManager] Party listo. Restaurando personaje guardado: {(CharacterSlot)_pendingSlotToRestore}");
        int slotToRestore = _pendingSlotToRestore;
        _pendingSlotToRestore = -1;
        SwitchToCharacter(slotToRestore);
    }

    /// <summary>
    /// Llamado por ActiveCharacterSwapper cuando su Start() termina y _ready pasa a true.
    /// Reintenta la restauración diferida del slot guardado si aún está pendiente.
    /// </summary>
    public void OnSwapperReady()
    {
        if (_pendingSlotToRestore >= 0)
            TryRestoreSavedSlot(null);
    }
}