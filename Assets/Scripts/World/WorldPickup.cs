using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class WorldPickup : MonoBehaviour
{
    [Header("Effects")]
    [SerializeField] private List<PickupEffect> effects = new();

    [Header("Consumption")]
    [SerializeField] private bool destroyOnCollect = true;
    [SerializeField] private float destroyDelay;
    [SerializeField] private bool deactivateRootOnCollect;
    [SerializeField] private GameObject[] disableOnCollect;

    [Header("Feedback")]
    [SerializeField] private GameObject vfxPrefab;
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private UnityEvent onCollected;

    private bool _collected;
    private Collider _collider;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        _collider = GetComponent<Collider>();
        if (_collider) _collider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
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
    }
#endif
}
