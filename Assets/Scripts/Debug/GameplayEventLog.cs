using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro ligero de "qué está pasando en el juego" (batalla, teletransporte, cinemática, clima,
/// menús, diálogo...), pensado para acompañar a una captura de PerformanceCapture (ver
/// PerformanceCapture.cs, F9 para grabar/parar). Mientras hay una captura en curso, cualquier
/// sistema del juego puede anotar aquí un evento con GameplayEventLog.Log(...) y queda guardado en
/// el mismo .json que ya genera PerformanceCapture, con el mismo reloj (segundos desde que empezó la
/// grabación) que usan las muestras de rendimiento por segundo — así, para revisar una sesión, basta
/// con jugar y grabar (F9 al empezar, F9 al terminar): el .json resultante ya cuenta qué estaba
/// pasando en cada momento (¿en combate? ¿acaba de teletransportar? ¿está lloviendo?), sin que haga
/// falta jugar, parar y explicarlo aparte para poder interpretar los números.
///
/// Es una clase estática pasiva: no vive en ninguna escena ni requiere ninguna referencia — cualquier
/// script del proyecto puede llamar a GameplayEventLog.Log("Categoria", "detalle opcional") en
/// cualquier momento sin comprobar nada antes: si no hay ninguna captura en curso, Log() es un no-op
/// instantáneo (un solo chequeo de null). PerformanceCapture es quien controla el ciclo de vida real
/// (BeginSession/EndSession), igual que ya hace con sus propios buckets de rendimiento — ver
/// StartRecording()/StopAndSave() en PerformanceCapture.cs.
///
/// Quién anota eventos hoy (ver cada sitio para más detalle):
///  - GameplayEventLogWirer: teletransportes, cinemáticas, menús (inventario/tienda/pausa/misiones/
///    mapa), cambios de escena — todo ello ya tenía eventos propios en el proyecto (TeleportService,
///    CinematicSequencerBase, MenuManager, SceneManager de Unity), así que el wirer solo se suscribe,
///    sin tocar esos sistemas.
///  - PlayerBattleModeController: entrada/salida de modo batalla, victoria.
///  - DayNightCycle: inicio/fin de la formación de nubes de tormenta y de la lluvia en sí.
///  - SpeechBubbleUI: cada línea de diálogo mostrada (bocadillo).
/// </summary>
public static class GameplayEventLog
{
    // Límite defensivo: una sesión larga con muchísimo diálogo/menús no debe generar un .json
    // desproporcionado. Subido de 500 a 2000 (24/08) a la vez que maxDurationSeconds pasó de 5 a 45
    // min en PerformanceCapture.cs, pensado para poder cubrir una demo entera (~30 min, bastante
    // diálogo/menús incluidos) sin recortar eventos en el caso normal (ver
    // picosRecortados/MaxSpikesRecorded en PerformanceCapture.cs, mismo patrón de "cap con aviso").
    const int MaxEventsRecorded = 2000;

    [Serializable]
    public class EventEntry
    {
        // Segundos desde que empezó la grabación (mismo reloj que "segundo" en PerfBucket) — así un
        // evento y una muestra de rendimiento con el mismo valor aproximado de "segundo" corresponden
        // al mismo instante de juego.
        public float segundo;
        public string tipo;
        public string detalle;
    }

    static List<EventEntry> _events;
    static float _sessionStartRealtime;

    /// <summary>True mientras PerformanceCapture tiene una grabación en curso.</summary>
    public static bool IsActive => _events != null;

    /// <summary>True si se alcanzó MaxEventsRecorded y se empezaron a descartar eventos nuevos.</summary>
    public static bool EventsCapped { get; private set; }

    public static int EventCount => _events?.Count ?? 0;

    /// <summary>Llamado por PerformanceCapture al pulsar F9 para empezar a grabar.</summary>
    public static void BeginSession(float startRealtime)
    {
        _events = new List<EventEntry>(128);
        _sessionStartRealtime = startRealtime;
        EventsCapped = false;
    }

    /// <summary>Llamado por PerformanceCapture al pulsar F9 para parar. Deja la clase lista para la
    /// siguiente sesión (IsActive vuelve a false, así que Log() vuelve a ser no-op).</summary>
    public static List<EventEntry> EndSession()
    {
        var result = _events ?? new List<EventEntry>();
        _events = null;
        return result;
    }

    /// <summary>
    /// Anota un evento en el instante actual. Seguro de llamar desde cualquier sistema del juego en
    /// cualquier momento sin comprobar antes IsActive: si no hay ninguna captura en curso, no hace
    /// nada (ni siquiera reserva memoria para la entrada).
    /// </summary>
    public static void Log(string tipo, string detalle = null)
    {
        if (_events == null) return;
        if (_events.Count >= MaxEventsRecorded)
        {
            EventsCapped = true;
            return;
        }
        _events.Add(new EventEntry
        {
            segundo = Time.unscaledTime - _sessionStartRealtime,
            tipo = tipo,
            detalle = detalle,
        });
    }
}
