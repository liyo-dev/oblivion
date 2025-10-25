using System;
using UnityEngine;

[Serializable]
public sealed class PlaySfxNode : NarrativeNode
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (clip != null)
            AudioManager.Instance?.PlaySfx(clip, volume);
        else
            Debug.LogWarning("[PlaySfxNode] No AudioClip asignado.");

        onReadyToAdvance?.Invoke();
    }
}
