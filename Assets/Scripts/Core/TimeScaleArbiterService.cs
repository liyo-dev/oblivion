using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Árbitro central de <see cref="Time.timeScale"/> (FIX C6, auditoría 2026-08-07).
///
/// Antes, al menos 4 actores independientes leían y escribían Time.timeScale directamente, cada
/// uno capturando "el valor de antes" y restaurándolo al terminar, sin coordinarse entre ellos:
/// SimpleHitStopProvider (hitstop de golpes), DeathCameraEffect (slowmotion de muerte),
/// SimpleCinematicDirector (slowmotion por paso de cinemática) y NPCCombatLifecycleHandler.OnDestroy
/// (salvaguarda que forzaba timeScale=1 "por si acaso"). Dos hitstops solapados (trivial en
/// combate, menos de 0.2s entre golpes) dejaban el juego en cámara lenta permanente: A capturaba
/// 1.0, B capturaba el 0.1 que había puesto A, A restauraba 1.0, B restauraba 0.1 encima. Y las
/// restauraciones "a 1 por si acaso" de DeathCameraEffect/NPCCombatLifecycleHandler competían con
/// cualquier pausa u otro efecto activo en ese momento, rompiéndolo.
///
/// Mismo patrón que PlayerActionManager.PushMode/PopMode (pila de peticiones con dueño): cada
/// actor pide un timeScale con <see cref="Request"/> mientras lo necesite, y lo libera con
/// <see cref="Release"/> cuando termina. Time.timeScale siempre iguala a la petición ACTIVA MÁS
/// BAJA (la más lenta gana — apropiado aquí, donde 0 pausa del todo y valores intermedios son
/// "más lentos"; nunca se pisa una pausa por un hitstop que termina, ni al revés). Sin ninguna
/// petición activa, se usa el <see cref="SetBaseline">baseline</see> (1 por defecto).
/// </summary>
public static class TimeScaleArbiterService
{
    private class RequestEntry
    {
        public object Owner;
        public float Scale;
    }

    private static readonly List<RequestEntry> _requests = new();
    private static float _baseline = 1f;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _requests.Clear();
        _baseline = 1f;
        Time.timeScale = 1f;
    }
#endif

    /// <summary>
    /// Pide un timeScale concreto mientras <paramref name="owner"/> lo mantenga activo. Si el
    /// mismo owner ya tenía una petición activa, se actualiza su valor (no se acumulan
    /// peticiones del mismo owner) — así un director de cinemática puede pedir un timeScale
    /// distinto por cada paso con una simple llamada, sin tener que Release+Request cada vez.
    /// Aplica el timeScale resultante de inmediato.
    /// </summary>
    public static void Request(object owner, float scale)
    {
        if (owner == null) return;

        for (int i = 0; i < _requests.Count; i++)
        {
            if (ReferenceEquals(_requests[i].Owner, owner))
            {
                _requests[i].Scale = scale;
                Apply();
                return;
            }
        }

        _requests.Add(new RequestEntry { Owner = owner, Scale = scale });
        Apply();
    }

    /// <summary>
    /// Libera la petición de <paramref name="owner"/>, si tenía alguna activa. Es un no-op seguro
    /// si no la tenía — se puede llamar "por si acaso" en rutas de limpieza/OnDestroy sin
    /// comprobar antes si realmente había algo que liberar. Reaplica el timeScale resultante.
    /// </summary>
    public static void Release(object owner)
    {
        if (owner == null) return;

        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_requests[i].Owner, owner))
            {
                _requests.RemoveAt(i);
                break;
            }
        }
        Apply();
    }

    /// <summary>
    /// Cambia el timeScale "de reposo" para cuando no hay ninguna petición activa. Pensado para
    /// el menú de pausa u otros sistemas que hoy fuerzan Time.timeScale=0/1 directamente: usar
    /// esto en vez de tocar Time.timeScale a pelo evita pisar una petición activa de otro actor
    /// (p.ej. abrir el menú de pausa en mitad de un hitstop no debe hacer que, al cerrarlo, el
    /// hitstop siga vivo con un timeScale de pausa ya liberado).
    /// </summary>
    public static void SetBaseline(float scale)
    {
        _baseline = scale;
        Apply();
    }

    private static void Apply()
    {
        float effective = _baseline;
        for (int i = 0; i < _requests.Count; i++)
        {
            if (_requests[i].Scale < effective)
                effective = _requests[i].Scale;
        }
        Time.timeScale = effective;
    }
}
