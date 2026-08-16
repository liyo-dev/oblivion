using System;

/// Punto de enganche genérico para el botón global de "saltar secuencia" (ver
/// GlobalCinematicSkipController / HoldToSkipUI).
///
/// Por qué existe: originalmente HoldToSkipUI llamaba directo a
/// CinematicSequencerBase.RequestSkipAll(), pero esa clase (Assets/Scripts/Cinematics/) ya NO es
/// el sistema que reproduce las secuencias reales del juego — eso lo hacen ahora
/// DialogueCinematicController/DialogueManager (diálogos) y ShowSpeechBubbleNode (bocadillos de
/// NarrativeGraph), y ninguno de los dos toca CinematicSequencerBase. Este hub desacopla "quién
/// pulsa saltar" de "quién sabe saltarse a sí mismo": cualquier sistema que reproduzca algo
/// saltable se suscribe aquí con RegisterSkipHandler() mientras esté activo (y se desuscribe al
/// terminar/salir), y HoldToSkipUI dispara RequestSkip() al completar el hold — sin necesitar
/// saber qué sistema concreto está corriendo en ese momento.
///
/// FIX (16/08/2026): además de la acción de saltar, este hub es también la fuente de verdad de
/// VISIBILIDAD del botón (ver AnySkippable / OnAnySkippableChanged, que consume
/// GlobalCinematicSkipController). Antes GlobalCinematicSkipController decidía mostrar/ocultar el
/// botón mirando PlayerActionManager.IsInMode(ActionMode.Cinematic) — pero eso deja fuera
/// cualquier secuencia que no bloquee el movimiento del jugador (ej. un bocadillo de villano tipo
/// "monólogo" vía ShowSpeechBubbleNode sin LockPlayerNode emparejado): el jugador podía seguir
/// moviéndose, pero el grafo narrativo seguía bloqueado esperando la duración del bocadillo y NO
/// había forma de saltarlo. Usar el propio registro del hub como señal de visibilidad garantiza
/// que "el botón se ve" y "pulsarlo hace algo" sean SIEMPRE la misma condición — ya no pueden
/// desincronizarse porque son literalmente el mismo contador.
///
/// ⚠️ CONTRATO para quien implemente un handler (ver auditoría de skip del 16/08/2026 en
/// Assets/Scripts/Cinematics/*.cs — EstelaAppearsSequencer.OnSkipCleanup,
/// LiamCrystalBallSequencer.OnSkipCleanup, etc. como referencia, y DialogueManager.
/// HandleSkipRequested para un ejemplo ya implementado): saltar una secuencia normalmente
/// significa abortar su corrutina/estado/temporizador a mitad de camino. Eso NO ejecuta el resto
/// de pasos pendientes por sí solo — cualquier SetActive(true), spawn, cambio de posición, flag
/// narrativo, etc. que estuviera programado más adelante SIMPLEMENTE NO OCURRE a menos que el
/// handler lo fuerce explícitamente a "como si la secuencia hubiera terminado con normalidad"
/// antes de devolver el control al jugador. No hay forma automática de resolver esto: cada
/// secuencia tiene que auditar qué estado final espera y aplicarlo a mano en su handler.
public static class NarrativeSkipHub
{
    public static event Action OnSkipRequested;

    /// Se dispara cuando AnySkippable cambia (false→true al registrar el primer handler activo,
    /// true→false al desregistrar el último). Mismo patrón que
    /// CinematicSequencerBase.OnAnySequenceActiveChanged, pero sobre la señal correcta — ver nota
    /// FIX arriba. GlobalCinematicSkipController se suscribe aquí para mostrar/ocultar el botón.
    public static event Action<bool> OnAnySkippableChanged;

    // AnySkippable se deriva directamente de si el delegado multicast tiene algún suscriptor (en
    // C# un multicast delegate vuelve a ser null en cuanto se le quita el último suscriptor con
    // -=). Deliberadamente NO es un contador propio mantenido a mano: un contador se puede
    // desincronizar de la realidad si algún Unregister defensivo (patrón ya usado en
    // DialogueManager, desregistrar-antes-de-registrar por si acaso) se dispara sobre un handler
    // que en verdad no estaba suscrito — aquí "-=" sobre algo no presente es un no-op real y
    // AnySkippable simplemente refleja el estado verdadero del delegado, sin poder desajustarse.
    public static bool AnySkippable => OnSkipRequested != null;

    /// El sistema que reproduce la secuencia debe suscribirse SOLO mientras tiene algo activo que
    /// saltar (típicamente mientras el grafo/diálogo está esperando un temporizador o input) y
    /// desuscribirse al salir — igual que CinematicSequencerBase.s_runningSequences, para que
    /// RequestSkip() no dispare sobre un handler de algo que ya terminó, y para que el botón se
    /// oculte en cuanto ya no quede nada saltable.
    public static void RegisterSkipHandler(Action handler)
    {
        if (handler == null) return;
        bool wasActive = AnySkippable;
        OnSkipRequested += handler;
        if (!wasActive) OnAnySkippableChanged?.Invoke(true);
    }

    public static void UnregisterSkipHandler(Action handler)
    {
        if (handler == null) return;
        bool wasActive = AnySkippable;
        OnSkipRequested -= handler;
        if (wasActive && !AnySkippable) OnAnySkippableChanged?.Invoke(false);
    }

    /// Llamado por HoldToSkipUI al completar el hold. No falla si no hay nadie suscrito (no-op
    /// seguro, mismo criterio que CinematicSequencerBase.RequestSkipAll()).
    public static void RequestSkip() => OnSkipRequested?.Invoke();

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnSkipRequested = null;
        OnAnySkippableChanged = null;
    }
#endif
}
