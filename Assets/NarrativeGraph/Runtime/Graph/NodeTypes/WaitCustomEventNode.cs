using System;
using UnityEngine;

[Serializable]
public sealed class WaitCustomEventNode : NarrativeNode
{
    public string eventKey;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        Debug.Log($"[WaitCustom] Suscrito a {eventKey}");
        void Handler(){ ctx.Signals.OffCustom(eventKey, Handler); Debug.Log($"[WaitCustom] Recibido {eventKey}"); ready?.Invoke(); }
        ctx.Signals.OnCustom(eventKey, Handler);
    }

}