using System;
using UnityEngine;

[Serializable]
public sealed class PlayMusicNode : NarrativeNode
{
    public AudioClip clip;
    [Min(0f)] public float fadeSeconds = 0.5f;
    public bool loop = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip != null)
            AudioManager.Instance?.PlayMusic(clip, fadeSeconds, loop);
        else
            Debug.LogWarning("[PlayMusicNode] No AudioClip asignado.");

        onReadyToAdvance?.Invoke();
    }
}
