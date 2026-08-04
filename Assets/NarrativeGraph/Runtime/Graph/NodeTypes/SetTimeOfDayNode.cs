using System;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que cambia el periodo del día en la escena.
/// Avanza inmediatamente; la transición de luz/cielo ocurre en paralelo.
/// </summary>
[Serializable]
public sealed class SetTimeOfDayNode : NarrativeNode
{
    [Tooltip("Periodo del día al que transicionar.")]
    public DayNightCycle.TimeOfDay targetTime = DayNightCycle.TimeOfDay.Night;

    [Tooltip("Si true, aplica el cambio instantáneamente sin transición suave de luz.")]
    public bool immediate = false;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (cycle != null)
            cycle.SetTimeOfDay(targetTime, immediate);
        else
            Debug.LogWarning("[SetTimeOfDayNode] No se encontró DayNightCycle en la escena.");

        onReadyToAdvance?.Invoke();
    }
}
