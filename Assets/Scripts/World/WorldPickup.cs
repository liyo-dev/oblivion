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
    [Tooltip("ID único para este pickup. Debe ser estable entre sesiones/escenas.")]
    [SerializeField] private string pickupId;

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
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private UnityEvent onCollected;

    private bool _collected;
    private Collider _collider;

    void OnEnable()
    {
        GameBootService.OnProfileReady += CheckPersistedState;

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

        if (pickupSfx)
        {
            AudioSource.PlayClipAtPoint(pickupSfx, transform.position, sfxVolume);
        }

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

        if (!Application.isPlaying && string.IsNullOrEmpty(pickupId))
        {
            pickupId = Guid.NewGuid().ToString("N");
        }
    }
#endif

    public bool HasBeenCollected => _collected;

    void CheckPersistedState()
    {
        if (!persistState) return;

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

    void PersistCollectedFlag()
    {
        if (!persistState) return;
        if (string.IsNullOrEmpty(pickupId))
        {
            Debug.LogWarning($"[WorldPickup] persistState=true pero pickupId vacío en {name}. El estado no se guardará.");
            return;
        }

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
        if (!persistState || string.IsNullOrEmpty(pickupId)) return false;

        var profile = GameBootService.Profile;
        var preset = profile != null ? profile.GetActivePresetResolved() : null;
        if (preset == null || preset.flags == null) return false;

        return preset.flags.Contains(GetFlag());
    }

    string GetFlag() => $"PICKUP_{pickupId}";
}
