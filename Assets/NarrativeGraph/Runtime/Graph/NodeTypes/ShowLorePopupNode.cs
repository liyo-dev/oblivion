using System;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que muestra un popup de lore al jugador.
/// Asigna un LorePopupConfig con las entradas (retrato + texto + duración) que se mostrarán.
/// El grafo espera a que termine la secuencia automática para continuar.
/// </summary>
[Serializable]
public sealed class ShowLorePopupNode : NarrativeNode
{
    [Tooltip("Configuración del popup: entradas con retrato, hablante y texto.")]
    public LorePopupConfig config;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = LorePopupUI.Instance;

        if (ui == null)
        {
            Debug.LogError("[ShowLorePopupNode] ❌ LorePopupUI.Instance es NULL. " +
                           "Asegúrate de que hay un LorePopupUI en el Canvas de esta escena.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config == null)
        {
            Debug.LogError("[ShowLorePopupNode] ❌ 'config' no está asignado en el nodo del grafo.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config.entries == null || config.entries.Length == 0)
        {
            Debug.LogError($"[ShowLorePopupNode] ❌ El LorePopupConfig '{config.name}' no tiene entradas.");
            onReadyToAdvance?.Invoke();
            return;
        }

        Debug.Log($"[ShowLorePopupNode] ✅ Mostrando popup '{config.name}' ({config.entries.Length} entradas).");
        ui.Show(config, onReadyToAdvance);
    }
}
