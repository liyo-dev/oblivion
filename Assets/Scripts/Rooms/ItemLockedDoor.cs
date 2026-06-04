using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Puerta/portal que requiere un ítem para desbloquearse por primera vez.
///
/// Persistencia:
///   - La flag DOOR_UNLOCKED_{id} se escribe en el runtime preset en cuanto se desbloquea.
///   - Sobrevive cambios de escena durante la sesión (preset es DontDestroyOnLoad).
///   - Solo se serializa a JSON cuando el jugador guarda partida explícitamente.
///   - Al volver a la escena, OnEnable detecta la flag y abre la puerta sin animación.
/// </summary>
[DisallowMultipleComponent]
public class ItemLockedDoor : MonoBehaviour
{
    [Header("Persistencia")]
    [Tooltip("ID único de esta puerta. Si está vacío se genera como: {escena}_{nombreObjeto}. " +
             "Usa un ID manual si el nombre del objeto puede cambiar.")]
    [SerializeField] private string persistenceId;

    [Header("Ítem requerido")]
    [SerializeField] private ItemData requiredItem;
    [SerializeField][Min(1)] private int requiredItemAmount = 1;
    [SerializeField] private bool consumeItemOnUnlock = true;

    [Header("Puertas visuales (opcionales)")]
    [SerializeField] private DoorGate leftDoor;
    [SerializeField] private DoorGate rightDoor;

    [Header("Colisionador bloqueante (opcional)")]
    [Tooltip("Collider que impide físicamente el paso. Se desactiva al desbloquear.")]
    [SerializeField] private Collider doorBlocker;

    [Header("Feedback")]
    [Tooltip("VFX al desbloquear por primera vez (no al restaurar desde preset).")]
    [SerializeField] private GameObject unlockVfxPrefab;

    [Header("Eventos")]
    public UnityEvent OnUnlocked;
    public UnityEvent OnRestoredFromSave;

    Interactable _interactable;
    bool _unlocked;

    string FlagKey => $"DOOR_UNLOCKED_{ResolveId()}";

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        GameBootService.OnProfileReady += OnProfileReady;
        PlayerPresetService.OnPresetApplied += OnPresetApplied;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(ItemLockedDoor));

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
        if (IsFlagSet())
            ApplyOpenState(isRestore: true);
    }

    void OnPresetApplied()
    {
        if (!_unlocked && IsFlagSet())
            ApplyOpenState(isRestore: true);
    }

    /// <summary>
    /// Conectar desde Interactable.OnInteract (UnityEvent en el Inspector).
    /// Comprueba el ítem y desbloquea si procede.
    /// </summary>
    public void TryUnlock()
    {
        if (_unlocked) return;

        if (requiredItem != null)
        {
            if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[ItemLockedDoor] Inventario no encontrado.");
#endif
                return;
            }

            if (!inventory.HasItem(requiredItem, requiredItemAmount))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ItemLockedDoor] Ítem '{requiredItem.itemId}' (x{requiredItemAmount}) no disponible.");
#endif
                return;
            }

            if (consumeItemOnUnlock)
            {
                if (!inventory.TryConsume(requiredItem, requiredItemAmount))
                    Debug.LogWarning($"[ItemLockedDoor] No se pudo consumir '{requiredItem.itemId}'.");
            }
        }

        SetFlag();
        ApplyOpenState(isRestore: false);
    }

    void ApplyOpenState(bool isRestore)
    {
        _unlocked = true;

        if (isRestore)
        {
            leftDoor?.OpenInstant();
            rightDoor?.OpenInstant();
            OnRestoredFromSave?.Invoke();
        }
        else
        {
            leftDoor?.Open();
            rightDoor?.Open();

            if (unlockVfxPrefab)
                Instantiate(unlockVfxPrefab, transform.position, Quaternion.identity);

            OnUnlocked?.Invoke();
        }

        if (doorBlocker) doorBlocker.enabled = false;
        _interactable?.EnableInteraction(false);
    }

    void SetFlag()
    {
        var profile = GameBootService.Profile;
        var preset = profile?.GetActivePresetResolved();
        if (preset == null) return;

        if (preset.flags == null) preset.flags = new List<string>();
        var flag = FlagKey;
        if (!preset.flags.Contains(flag))
            preset.flags.Add(flag);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ItemLockedDoor] Flag '{flag}' añadida al preset.");
#endif
    }

    bool IsFlagSet()
    {
        var profile = GameBootService.Profile;
        var preset = profile?.GetActivePresetResolved();
        return preset?.flags != null && preset.flags.Contains(FlagKey);
    }

    string ResolveId()
    {
        if (!string.IsNullOrEmpty(persistenceId)) return persistenceId;
        var sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Unknown";
        return $"{sceneName}_{gameObject.name}";
    }
}
