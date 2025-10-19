using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-520)]
public class BossProgressPersistenceBridge : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindObjectOfType<BossProgressPersistenceBridge>() != null) return;

        var go = new GameObject(nameof(BossProgressPersistenceBridge));
        DontDestroyOnLoad(go);
        go.AddComponent<BossProgressPersistenceBridge>();
    }

    private bool _initialized;

    private void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        BossProgressTracker.OnBossMarkedDefeated += HandleBossMarked;

        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    private void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        BossProgressTracker.OnBossMarkedDefeated -= HandleBossMarked;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;
        ApplyPresetToTracker();
        _initialized = true;
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void ApplyPresetToTracker()
    {
        var profile = GameBootService.Profile;
        if (profile == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        var tracker = BossProgressTracker.Instance;
        tracker.LoadFromSnapshot(preset.defeatedBossIds);
    }

    private void HandleBossMarked(string bossId)
    {
        if (!GameBootService.IsAvailable) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        if (preset.defeatedBossIds == null)
        {
            preset.defeatedBossIds = new List<string>();
        }

        if (!preset.defeatedBossIds.Contains(bossId))
        {
            preset.defeatedBossIds.Add(bossId);
        }
    }
}
