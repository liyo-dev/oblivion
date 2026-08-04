using System;
using UnityEngine;

/// <summary>
/// Punto único de suscripción a los eventos de lluvia de DayNightCycle para todos los NPCs de
/// la escena. DayNightCycle no es un singleton (se busca con FindAnyObjectByType, sin Instance,
/// probablemente una instancia por escena de pueblo) — sin este relay, cada uno de los NPCs
/// tendría que hacer su propio FindAnyObjectByType<DayNightCycle>() en Awake, caro si hay
/// decenas de NPCs por escena. En su lugar, WorldBootstrap llama a Resubscribe() una vez por
/// carga de escena, y los NPCs solo escuchan el evento estático (barato).
///
/// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § A.2.
/// </summary>
public static class NPCWeatherAwareness
{
    public static event Action RainStarted;
    public static event Action RainStopped;
    public static bool IsRaining { get; private set; }

    private static DayNightCycle _cycle;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _cycle = null;
        IsRaining = false;
        RainStarted = null;
        RainStopped = null;
    }
#endif

    /// <summary>
    /// Busca (o re-busca) el DayNightCycle activo en la escena y se suscribe a sus eventos.
    /// Seguro llamar varias veces: se desuscribe del ciclo anterior antes de buscar uno nuevo.
    /// Si la escena no tiene DayNightCycle (interiores, mazmorras), IsRaining queda en false.
    /// </summary>
    public static void Resubscribe()
    {
        if (_cycle != null)
        {
            _cycle.RainStarted -= OnRainStarted;
            _cycle.RainStopped -= OnRainStopped;
        }

        _cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        if (_cycle == null)
        {
            IsRaining = false;
            return;
        }

        _cycle.RainStarted += OnRainStarted;
        _cycle.RainStopped += OnRainStopped;
        IsRaining = _cycle.IsRaining;
    }

    private static void OnRainStarted()
    {
        IsRaining = true;
        RainStarted?.Invoke();
    }

    private static void OnRainStopped()
    {
        IsRaining = false;
        RainStopped?.Invoke();
    }
}
