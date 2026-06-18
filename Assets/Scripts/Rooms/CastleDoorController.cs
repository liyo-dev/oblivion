using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class CastleDoorController : MonoBehaviour
{
    [Header("Puertas")]
    [SerializeField] private DoorGate leftDoor;
    [SerializeField] private DoorGate rightDoor;
    [Tooltip("Si true, las puertas arrancan cerradas al iniciar la escena (a menos que haya una flag de apertura guardada).")]
    [SerializeField] private bool startsLocked = true;

    [Header("Persistencia")]
    [Tooltip("Si está activo, el estado abierto se guarda en la partida y se restaura al cargar.")]
    [SerializeField] private bool persistState = true;
    [Tooltip("Override manual del ID de persistencia. Vacío = se genera automáticamente (escena + nombre + posición).")]
    [SerializeField] private string persistenceId;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando las puertas se abren durante la sesión activa.")]
    public UnityEvent OnDoorsOpened;
    [Tooltip("Se dispara cuando las puertas se restauran abiertas al cargar partida (sin animación).")]
    public UnityEvent OnDoorsOpenedRestored;

    bool _isOpen;

    string FlagKey => $"DOOR_OPEN_{ResolveId()}";

    void Awake()
    {
        if (startsLocked)
        {
            leftDoor?.Close();
            rightDoor?.Close();
        }
        else
        {
            _isOpen = true;
        }
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += OnProfileReady;
        PlayerPresetService.OnPresetApplied += OnPresetApplied;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(CastleDoorController));

        if (GameBootService.IsAvailable)
            OnProfileReady();
        else if (PlayerPresetService.HasAppliedPreset)
            OnPresetApplied();
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= OnProfileReady;
        PlayerPresetService.OnPresetApplied -= OnPresetApplied;
    }

    void OnProfileReady()
    {
        if (persistState && IsFlagSet())
            ApplyOpenInstant();
    }

    void OnPresetApplied()
    {
        if (persistState && !_isOpen && IsFlagSet())
            ApplyOpenInstant();
    }

    public void OpenDoors()
    {
        if (_isOpen) return;
        _isOpen = true;
        leftDoor?.Open();
        rightDoor?.Open();
        if (persistState) SetFlag();
        OnDoorsOpened?.Invoke();
    }

    public void CloseDoors()
    {
        _isOpen = false;
        leftDoor?.Close();
        rightDoor?.Close();
        if (persistState) ClearFlag();
    }

    void ApplyOpenInstant()
    {
        _isOpen = true;
        leftDoor?.OpenInstant();
        rightDoor?.OpenInstant();
        OnDoorsOpenedRestored?.Invoke();
    }

    void SetFlag()
    {
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null) return;
        preset.flags ??= new List<string>();
        var flag = FlagKey;
        if (!preset.flags.Contains(flag))
            preset.flags.Add(flag);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CastleDoorController] Flag '{flag}' añadida al preset.");
#endif
    }

    void ClearFlag()
    {
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        preset?.flags?.Remove(FlagKey);
    }

    bool IsFlagSet()
    {
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        return preset?.flags != null && preset.flags.Contains(FlagKey);
    }

    string ResolveId()
    {
        if (!string.IsNullOrEmpty(persistenceId)) return persistenceId;
        var sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Unknown";
        var pos = transform.position;
        return $"{sceneName}_{gameObject.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
    }
}
