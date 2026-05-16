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
            Debug.LogWarning("[ShowLorePopupNode] No hay LorePopupUI en escena. Avanzando sin mostrar popup.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config == null)
        {
            Debug.LogWarning("[ShowLorePopupNode] config no asignado. Avanzando sin mostrar popup.");
            onReadyToAdvance?.Invoke();
            return;
        }

        ui.Show(config, onReadyToAdvance);
    }
}
