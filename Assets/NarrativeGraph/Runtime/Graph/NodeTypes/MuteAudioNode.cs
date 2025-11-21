using System;

[Serializable]
public sealed class MuteAudioNode : NarrativeNode
{
    public AudioBus bus = AudioBus.Master;
    public bool mute = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        AudioService.Instance?.Mute(bus, mute);
        onReadyToAdvance?.Invoke();
    }
}
