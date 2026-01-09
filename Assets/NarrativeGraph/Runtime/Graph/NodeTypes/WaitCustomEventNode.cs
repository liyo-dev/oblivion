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
            // Debug.Log($"[WaitCustom:{guid}] Evento {eventKey} ya fue recibido previamente → avanzando inmediatamente");
            ready?.Invoke();
            return;
        }

        // Debug.Log($"[WaitCustom:{guid}] Suscrito a {eventKey}");
        void Handler()
        { 
            ctx.Signals.OffCustom(eventKey, Handler); 
            // Marcar el evento como recibido en el blackboard (específico a este nodo)
            ctx.Blackboard.Set(eventReceivedKey, true);
            // Debug.Log($"[WaitCustom:{guid}] Recibido {eventKey}"); 
            ready?.Invoke(); 
        }
        ctx.Signals.OnCustom(eventKey, Handler);
    }

}