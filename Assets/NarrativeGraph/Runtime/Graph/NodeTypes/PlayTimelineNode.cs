// PlayTimelineNode.cs
using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public sealed class PlayTimelineNode : NarrativeNode
{
    public ExposedReference<PlayableDirector> directorRef;
    Action _done;
    PlayableDirector _dir;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        _dir = directorRef.Resolve(ctx.Exposed);
        if (!_dir) { onReadyToAdvance?.Invoke(); return; }

        _done = () => { _dir.stopped -= OnStopped; onReadyToAdvance?.Invoke(); };
        _dir.stopped += OnStopped;
        _dir.Play();
    }

    void OnStopped(PlayableDirector d) => _done?.Invoke();

    public override void Exit(NarrativeContext ctx)
    {
        if (_dir) _dir.stopped -= OnStopped;
        _done = null;
    }
}