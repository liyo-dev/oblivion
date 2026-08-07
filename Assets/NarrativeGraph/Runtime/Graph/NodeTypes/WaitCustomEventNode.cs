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
            // FIX (Agosto 2026): el flag se consume aquí mismo (se limpia al leerlo) en vez de
            // dejarse permanentemente en true. Pensado para el resume tras recarga (el evento ya
            // se había disparado antes de guardar), pero si no se limpia, un nodo que vive dentro
            // de un bucle de reintento en vivo (p. ej. WaitCustomEventNode "AWAKEN_FAILED" en
            // MainNarrative_Cap1, cuya rama de fallo vuelve a entrar en el mismo fork para
            // reintentar StarAwakeningSequencer) queda marcado "ya recibido" para siempre: la
            // segunda vuelta del bucle avanza sin esperar el evento real, dispara un reintento
            // fantasma inmediato y dos StarAwakeningSequencer solapados dejan un Push de
            // ActionMode.Cinematic sin su Pop correspondiente (CinematicSequencerBase._cinematicLocked
            // solo se resetea en la primera llamada a EndCinematic) → el jugador queda pillado en
            // modo Cinematic en vez de volver limpio al gameplay para reintentar.
            ctx.Blackboard.Set(eventReceivedKey, false);
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WaitCustom:{guid}] Evento '{eventKey}' ya fue recibido previamente → avanzando inmediatamente (flag consumido)");
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