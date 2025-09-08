using System.Collections.Generic;
using UnityEngine;

public interface ITickManager : IGameService
{
    void Register(ITickable t);
    void Unregister(ITickable t);
    void Update(float dt);
}

public class TickManager : ITickManager
{
    private readonly List<ITickable> _tickables = new();

    public void Initialize()
    {
        // Hook en un runner MonoBehaviour
        var runner = new GameObject("TickRunner").AddComponent<TickRunner>();
        runner.Init(this);
        UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
    }

    public void Dispose() => _tickables.Clear();
    public void Register(ITickable t) => _tickables.Add(t);
    public void Unregister(ITickable t) => _tickables.Remove(t);

    public void Update(float dt)
    {
        for (int i = 0; i < _tickables.Count; i++)
            _tickables[i].Tick(dt);
    }
}

public class TickRunner : UnityEngine.MonoBehaviour
{
    ITickManager _tm;
    public void Init(ITickManager tm) => _tm = tm;
    void Update() => _tm.Update(UnityEngine.Time.deltaTime);
}