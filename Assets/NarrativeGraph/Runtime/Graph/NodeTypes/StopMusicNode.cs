using System;

[Serializable]
public sealed class StopMusicNode : NarrativeNode
{
     public float fadeSeconds = 0.5f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        AudioManager.Instance?.StopMusic(fadeSeconds);
        onReadyToAdvance?.Invoke();
    }
}

