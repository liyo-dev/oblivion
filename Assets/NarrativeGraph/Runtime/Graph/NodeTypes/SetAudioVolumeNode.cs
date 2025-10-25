using System;
using UnityEngine;

[Serializable]
public sealed class SetAudioVolumeNode : NarrativeNode
{
    public AudioBus bus = AudioBus.Master;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        AudioManager.Instance?.SetVolume(bus, volume);
        onReadyToAdvance?.Invoke();
    }
}
