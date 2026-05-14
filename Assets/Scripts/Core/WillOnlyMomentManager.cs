using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestiona los momentos del juego en los que el personaje activo debe ser Will
/// y el cambio de personaje está deshabilitado (p.ej. minijuego de persecución).
///
/// Uso:
///   WillOnlyMomentManager.Instance.EnterMoment("TAG_MINIGAME_01");  // al iniciar
///   WillOnlyMomentManager.Instance.ExitMoment("TAG_MINIGAME_01");   // al terminar
///
/// Soporta anidamiento: el bloqueo se mantiene mientras haya al menos un momento activo.
/// </summary>
public class WillOnlyMomentManager : MonoBehaviour
{
    #region Singleton
    public static WillOnlyMomentManager Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        OnSwitchLockChanged = null;
    }
#endif
    #endregion

    /// <summary>
    /// Se dispara cuando cambia el estado del bloqueo de cambio de personaje.
    /// true = bloqueado (Will obligatorio), false = desbloqueado.
    /// </summary>
    public static event Action<bool> OnSwitchLockChanged;

    private readonly HashSet<string> _activeMoments = new();

    public bool IsSwitchingLocked => _activeMoments.Count > 0;

    #region Lifecycle
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null) return;
        var go = new GameObject("[WillOnlyMomentManager]");
        Instance = go.AddComponent<WillOnlyMomentManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
    #endregion

    #region API pública
    /// <summary>
    /// Registra un momento activo. Si es el primero, fuerza el cambio a Will y bloquea el switching.
    /// </summary>
    public void EnterMoment(string momentId)
    {
        if (string.IsNullOrEmpty(momentId)) return;

        bool wasLocked = IsSwitchingLocked;
        _activeMoments.Add(momentId);

        if (!wasLocked)
        {
            ForceSwitchToWill();
            OnSwitchLockChanged?.Invoke(true);
        }
    }

    /// <summary>
    /// Desregistra un momento. Si ya no quedan momentos activos, desbloquea el switching.
    /// </summary>
    public void ExitMoment(string momentId)
    {
        if (string.IsNullOrEmpty(momentId)) return;

        _activeMoments.Remove(momentId);

        if (!IsSwitchingLocked)
            OnSwitchLockChanged?.Invoke(false);
    }

    /// <summary>
    /// Limpia todos los momentos activos y desbloquea el switching.
    /// Útil al cargar escenas o reiniciar el estado del juego.
    /// </summary>
    public void ClearAllMoments()
    {
        if (!IsSwitchingLocked) return;
        _activeMoments.Clear();
        OnSwitchLockChanged?.Invoke(false);
    }
    #endregion

    #region Internals
    private void ForceSwitchToWill()
    {
        var manager = PartyControlManager.Instance;
        if (manager == null) return;
        if (manager.ActiveSlot == PartyControlManager.CharacterSlot.Will) return;
        manager.ForceSwitch(PartyControlManager.CharacterSlot.Will);
    }
    #endregion
}
