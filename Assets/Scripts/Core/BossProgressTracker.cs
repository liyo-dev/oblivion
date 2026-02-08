using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-550)]
public class BossProgressTracker : MonoBehaviour
{
    [SerializeField] private bool persistAcrossScenes = true;

    private static BossProgressTracker _instance;
    private readonly HashSet<string> _defeated = new(StringComparer.Ordinal);

    public static event Action<string> OnBossMarkedDefeated;
    public static event Action OnProgressRestored;

    public static BossProgressTracker Instance
    {
        get
        {
            if (TryGetInstance(out var tracker))
            {
                return tracker;
            }

            var go = new GameObject(nameof(BossProgressTracker));
            return go.AddComponent<BossProgressTracker>();
        }
    }

    public static bool TryGetInstance(out BossProgressTracker tracker)
    {
        if (_instance != null)
        {
            tracker = _instance;
            return true;
        }

        var existing = ServiceLocator.Get<BossProgressTracker>(false);
        if (existing != null)
        {
            _instance = existing;
            tracker = existing;
            return true;
        }

        tracker = null;
        return false;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public IReadOnlyCollection<string> DefeatedBossIds => _defeated;

    public void MarkDefeated(string bossId)
    {
        if (string.IsNullOrEmpty(bossId)) return;

        if (_defeated.Add(bossId))
        {
            OnBossMarkedDefeated?.Invoke(bossId);
        }
    }

    public bool IsDefeated(string bossId)
    {
        if (string.IsNullOrEmpty(bossId)) return false;
        return _defeated.Contains(bossId);
    }

    public void LoadFromSnapshot(IEnumerable<string> defeatedBossIds)
    {
        _defeated.Clear();

        Debug.Log($"[BossProgressTracker] 📥 LoadFromSnapshot llamado");

        if (defeatedBossIds != null)
        {
            int count = 0;
            foreach (var id in defeatedBossIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                _defeated.Add(id);
                count++;
                Debug.Log($"[BossProgressTracker]   ✅ Boss derrotado cargado: '{id}'");
            }
            Debug.Log($"[BossProgressTracker] 📊 Total bosses derrotados cargados: {count}");
        }
        else
        {
            Debug.Log($"[BossProgressTracker] ⚠️ defeatedBossIds es NULL - no hay bosses derrotados");
        }

        OnProgressRestored?.Invoke();
    }

    public List<string> GetSnapshot()
    {
        return new List<string>(_defeated);
    }
}
