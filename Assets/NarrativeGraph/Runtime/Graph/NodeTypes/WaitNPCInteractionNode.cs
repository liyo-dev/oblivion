using System;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que espera hasta que el jugador interactúe con un NPC específico.
/// El NPC debe tener un componente NPCGraphBridge que emite el evento de interacción.
/// Usa el sistema sticky de señales para no perder interacciones que ocurran antes
/// de que el grafo llegue a este nodo.
/// </summary>
[Serializable]
[SavePoint("Seguro guardar mientras espera interacción con NPC")]
public sealed class WaitNPCInteractionNode : NarrativeNode
{
    [Header("NPC")]
    [Tooltip("ID narrativo del NPC con el que se espera interacción. " +
             "Debe coincidir con el campo npcId del componente NPCGraphBridge del NPC.")]
    public string npcId;

    public override void Enter(NarrativeContext ctx, Action ready)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[WaitNPCInteraction] npcId vacío → avanzando");
            ready?.Invoke();
            return;
        }

        var eventKey = $"NPC_INTERACT_{npcId}";

        // Flag específico de esta instancia de nodo para evitar re-ejecución
        var eventReceivedKey = $"__npc_interact_{guid}_{npcId}_received";
        if (ctx.Blackboard.Get<bool>(eventReceivedKey, false))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WaitNPCInteraction:{guid}] Interacción con '{npcId}' ya recibida → avanzando");
#endif
            ready?.Invoke();
            return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[WaitNPCInteraction:{guid}] Esperando interacción con NPC '{npcId}' (evento: '{eventKey}')...");
#endif

        void Handler()
        {
            ctx.Signals.OffCustom(eventKey, Handler);
            ctx.Blackboard.Set(eventReceivedKey, true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[WaitNPCInteraction:{guid}] Interacción con '{npcId}' recibida → avanzando");
#endif
            ready?.Invoke();
        }

        ctx.Signals.OnCustom(eventKey, Handler);
    }
}
