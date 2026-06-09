using System;
using UnityEngine;

[Serializable]
[SavePoint("Seguro guardar mientras espera eventos")]
public sealed class WaitCustomEventNode : NarrativeNode
{
    public string eventKey;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        // Usar GUID del nodo para hacer el flag específico a esta instancia
        var eventReceivedKey = $"__event_{guid}_{eventKey}_received";
        if (ctx.Blackboard.Get<bool>(eventReceivedKey, false))
        {
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WaitCustom:{guid}] Evento '{eventKey}' ya fue recibido previamente → avanzando inmediatamente");
#endif
            ready?.Invoke();
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WaitCustom:{guid}] Suscribiéndose a '{eventKey}'...");
#endif
        void Handler()
        {
            ctx.Signals.OffCustom(eventKey, Handler);
            // Marcar el evento como recibido en el blackboard (específico a este nodo)
            ctx.Blackboard.Set(eventReceivedKey, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WaitCustom:{guid}] ✅ Recibido '{eventKey}' → avanzando");
#endif
            ready?.Invoke();
        }
        ctx.Signals.OnCustom(eventKey, Handler);
    }

}