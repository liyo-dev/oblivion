using System;
using UnityEngine;

/// <summary>
/// Nodo que emite un evento custom al sistema de señales narrativas.
/// Útil para activar condiciones custom en NPCs u otros sistemas que escuchen eventos.
/// Ejemplo: EVT_MOUNTAIN, EVT_BOSS_DEFEATED, etc.
/// </summary>
[Serializable]
public sealed class RaiseCustomEventNode : NarrativeNode
{
    [Tooltip("Clave del evento a emitir (ej: EVT_MOUNTAIN, EVT_BOSS_DEFEATED)")]
    public string eventKey;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            Debug.LogWarning("[RaiseCustomEventNode] ⚠️ eventKey está vacío. No se emitió ningún evento.");
        }
        else
        {
            Debug.Log($"[RaiseCustomEventNode] 📤 Emitiendo evento: '{eventKey}'");
            ctx.Signals?.RaiseCustom(eventKey);
        }
        
        onReadyToAdvance?.Invoke();
    }
}
