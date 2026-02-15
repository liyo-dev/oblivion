using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class WorldPickup : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private List<PickupEffect> effects = new();

    [Header("Persistencia")]
    [Tooltip("Si es true, al recogerlo se guardará su estado y no volverá a aparecer tras cargar partida.")]
    [SerializeField] private bool persistState = true;
    [Tooltip("Override manual del ID. Si está vacío, se genera automáticamente usando escena+nombre+posición.")]
    [SerializeField] private string pickupIdOverride;

    [Header("Consumption")]
    [SerializeField] private bool destroyOnCollect = true;
    [SerializeField] private float destroyDelay;
    [SerializeField] private bool deactivateRootOnCollect;
    [SerializeField] private GameObject[] disableOnCollect;
    
    [Header("Behavior")]
    [Tooltip("If true, the pickup will be collected automatically when a PlayerPickupCollector enters the trigger. If false, collection must be triggered explicitly (e.g. via an Interactable/Chest).")]
    [SerializeField] private bool collectOnTrigger = true;

    [Header("Feedback")]
    [SerializeField] private GameObject vfxPrefab;
    [Tooltip("Clave del SFX en AudioGraphProfile (ej: 'Coin', 'Chest')")]
    [SerializeField] private string pickupSfxKey;
    [SerializeField] private UnityEvent onCollected;

    private bool _collected;
    private Collider _collider;

    void OnEnable()
    {
        GameBootService.OnProfileReady += CheckPersistedState;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(WorldPickup));

        // Si el perfil ya está disponible (por ejemplo al volver a escena aditivamente),
        // re-evaluar inmediatamente para evitar que los pickups reaparezcan erróneamente.
        if (GameBootService.IsAvailable)
        {
            CheckPersistedState();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= CheckPersistedState;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider) _collider.isTrigger = true;
        CheckPersistedState();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!collectOnTrigger) return;

        TryCollectFrom(other);
    }

    

    /// <summary>
    /// Attempts to apply all configured effects to the provided collector.
    /// Returns true when at least one effect modified player state.
    /// </summary>
    public bool Collect(PlayerPickupCollector collector)
    {
        if (_collected || collector == null || effects.Count == 0) return false;

        bool anyConsume = false;
        bool anyChange = false;

        foreach (var effect in effects)
        {
            bool consume;
            bool changed = collector.TryCollect(effect, out consume);

            if (consume)
            {
                anyConsume = true;
                if (changed) anyChange = true;
            }
        }

        if (!anyConsume) return false;

        CompleteCollection();
        return anyChange;
    }

    private void TryCollectFrom(Collider other)
    {
        if (_collected || effects.Count == 0) return;

        if (!TryResolveCollector(other, out var collector)) return;

        Collect(collector);
    }

    private bool TryResolveCollector(Collider other, out PlayerPickupCollector collector)
    {
        collector = null;
        if (!other) return false;

        collector = other.GetComponent<PlayerPickupCollector>();
        if (collector) return true;

        collector = other.GetComponentInParent<PlayerPickupCollector>();
        if (collector) return true;

        collector = other.GetComponentInChildren<PlayerPickupCollector>();
        if (collector) return true;

        if (PlayerService.TryGetComponent(out PlayerPickupCollector cached))
        {
            if (cached != null && other.transform != null)
            {
                var root = other.transform.root;
                var playerRoot = cached.transform.root;
                if (root == playerRoot)
                {
                    collector = cached;
                    return true;
                }
            }
        }

        return false;
    }

    private void CompleteCollection()
    {
        if (_collected) return;
        _collected = true;

        PersistCollectedFlag();

        if (_collider) _collider.enabled = false;

        // SFX usando AudioService
        if (!string.IsNullOrEmpty(pickupSfxKey) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(pickupSfxKey);
        }

        // VFX
        if (vfxPrefab)
        {
            Instantiate(vfxPrefab, transform.position, Quaternion.identity);
        }

        if (disableOnCollect != null)
        {
            for (int i = 0; i < disableOnCollect.Length; i++)
            {
                if (disableOnCollect[i]) disableOnCollect[i].SetActive(false);
            }
        }

        if (deactivateRootOnCollect)
        {
            gameObject.SetActive(false);
        }

        onCollected?.Invoke();

        if (destroyOnCollect)
        {
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!_collider) _collider = GetComponent<Collider>();
        if (_collider && !_collider.isTrigger)
        {
            _collider.isTrigger = true;
        }
        // Ya no generamos GUID automáticamente - usamos posición para unicidad
    }
#endif

    public bool HasBeenCollected => _collected;

    void CheckPersistedState()
    {
        if (!persistState) return;

        // Limpiar flags GUID antiguos (formato: PICKUP_ seguido de 32 caracteres hex)
        CleanupLegacyGuidFlags();

        if (IsFlagSet())
        {
            _collected = true;
            if (_collider) _collider.enabled = false;

            if (disableOnCollect != null)
            {
                for (int i = 0; i < disableOnCollect.Length; i++)
                {
                    if (disableOnCollect[i]) disableOnCollect[i].SetActive(false);
                }
            }

            if (deactivateRootOnCollect)
            {
                gameObject.SetActive(false);
            }
            else if (destroyOnCollect)
            {
                Destroy(gameObject);
            }
        }
    }

    /// <summary>
    /// Limpia flags con formato GUID antiguo (PICKUP_[32 hex chars]) que marcaban todos los pickups iguales
    /// </summary>
    static bool _legacyFlagsCleaned = false;
    void CleanupLegacyGuidFlags()
    {
        if (_legacyFlagsCleaned) return;
        _legacyFlagsCleaned = true;

        var profile = GameBootService.Profile;
        var preset = profile != null ? profile.GetActivePresetResolved() : null;
        if (preset == null || preset.flags == null) return;

        int removed = preset.flags.RemoveAll(flag =>
        {
            // Detectar formato GUID: PICKUP_ + exactamente 32 caracteres hexadecimales
            if (!flag.StartsWith("PICKUP_")) return false;
            var id = flag.Substring(7); // Quitar "PICKUP_"
            return id.Length == 32 && System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-fA-F0-9]+$");
        });

        if (removed > 0)
        {
            Debug.Log($"[WorldPickup] 🧹 Limpiados {removed} flags GUID antiguos del preset");
        }
    }

    void PersistCollectedFlag()
    {
        if (!persistState) return;

        var profile = GameBootService.Profile;
        if (profile == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        if (preset.flags == null) preset.flags = new List<string>();
        string flag = GetFlag();
        if (!preset.flags.Contains(flag))
            preset.flags.Add(flag);
    }

    bool IsFlagSet()
    {
        if (!persistState) return false;

        var profile = GameBootService.Profile;
        var preset = profile != null ? profile.GetActivePresetResolved() : null;
        if (preset == null || preset.flags == null) return false;

        // Solo verificar el flag generado por posición (nuevo sistema)
        // Ignorar cualquier flag antiguo con GUID
        return preset.flags.Contains(GetFlag());
    }

    string GetFlag()
    {
        // Si hay un override explícito, usarlo directamente (para casos especiales)
        if (!string.IsNullOrEmpty(pickupIdOverride))
            return $"PICKUP_{pickupIdOverride}";
        
        // SIEMPRE generar ID único basado en escena + nombre + posición
        // Esto garantiza que cada pickup tenga un ID único automáticamente
        // y que no se use ningún GUID antiguo del prefab
        var sceneName = gameObject.scene.IsValid() ? gameObject.scene.name : "Unknown";
        var pos = transform.position;
        var posKey = $"{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
        return $"PICKUP_{sceneName}_{gameObject.name}_{posKey}";
    }
}
