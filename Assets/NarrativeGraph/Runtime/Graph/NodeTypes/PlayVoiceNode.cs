using System;
using UnityEngine;

[Serializable]
public sealed class PlayVoiceNode : NarrativeNode
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip != null)
            AudioManager.Instance?.PlayVoice(clip, volume);
        else
            Debug.LogWarning("[PlayVoiceNode] No AudioClip asignado.");

        onReadyToAdvance?.Invoke();
    }
}
