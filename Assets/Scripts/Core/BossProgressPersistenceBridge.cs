using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-520)]
public class BossProgressPersistenceBridge : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        if (ServiceLocator.TryGet(out BossProgressPersistenceBridge existing))
        {
            _instance = existing;
            return;
        }

        var go = new GameObject(nameof(BossProgressPersistenceBridge));
        DontDestroyOnLoad(go);
        go.AddComponent<BossProgressPersistenceBridge>();
    }

    private bool _initialized;
    private static BossProgressPersistenceBridge _instance;

    private void OnEnable()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ServiceLocator.Register(this);
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(BossProgressPersistenceBridge));
        BossProgressTracker.OnBossMarkedDefeated += HandleBossMarked;

        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
        else
        {
            // ✅ CRÍTICO: Si no hay GameBootService (inicio directo desde MainWorld en editor),
            // inicializar de todas formas para permitir testing
            Debug.LogWarning("[BossProgressPersistenceBridge] GameBootService no disponible - Modo testing directo desde MainWorld");
            StartCoroutine(WaitForTrackerAndInitialize());
        }
    }
    
    private System.Collections.IEnumerator WaitForTrackerAndInitialize()
    {
        // Esperar a que BossProgressTracker esté listo
        while (!BossProgressTracker.TryGetInstance(out var _))
        {
            yield return null;
        }
        
        // Esperar un frame adicional para asegurar que todas las arenas se hayan registrado
        yield return null;
        
        Debug.Log("[BossProgressPersistenceBridge] ✅ BossProgressTracker listo - Inicializando estado vacío para testing");
        _initialized = true;
    }

    private void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
        BossProgressTracker.OnBossMarkedDefeated -= HandleBossMarked;

        if (_instance == this)
        {
            ServiceLocator.Unregister(this);
            _instance = null;
        }
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
