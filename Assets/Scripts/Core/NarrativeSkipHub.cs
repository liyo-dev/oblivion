using System;

/// Punto de enganche genérico para el botón global de "saltar secuencia" (ver
/// GlobalCinematicSkipController / HoldToSkipUI).
///
/// Por qué existe: originalmente HoldToSkipUI llamaba directo a
/// CinematicSequencerBase.RequestSkipAll(), pero esa clase (Assets/Scripts/Cinematics/) ya NO es
/// el sistema que reproduce las secuencias reales del juego — eso lo hacen ahora
/// DialogueCinematicController y DialogueManager, que no la tocan para nada. Este hub desacopla
/// "quién pulsa saltar" de "quién sabe saltarse a sí mismo": cualquier sistema que reproduzca una
/// secuencia se suscribe aquí con RegisterSkipHandler() mientras esté activo (y se desuscribe al
/// terminar/salir), y HoldToSkipUI dispara RequestSkip() al completar el hold — sin necesitar
/// saber qué sistema concreto está corriendo en ese momento.
///
/// ⚠️ CONTRATO para quien implemente un handler (ver auditoría de skip del 16/08/2026 en
/// Assets/Scripts/Cinematics/*.cs — EstelaAppearsSequencer.OnSkipCleanup,
/// LiamCrystalBallSequencer.OnSkipCleanup, etc. como referencia): saltar una secuencia normalmente
/// significa abortar su corrutina/estado a mitad de camino (StopCoroutine o equivalente). Eso NO
/// ejecuta el resto de pasos pendientes — cualquier SetActive(true), spawn, cambio de posición,
/// etc. que estuviera programado más adelante en la secuencia SIMPLEMENTE NO OCURRE a menos que el
/// handler lo fuerce explícitamente a "como si la secuencia hubiera terminado con normalidad"
/// antes de devolver el control al jugador. No hay forma automática de resolver esto: cada
/// secuencia tiene que auditar qué estado final espera (NPCs que deben quedar visibles/ocultos,
/// flags narrativos, posiciones) y aplicarlo a mano en su handler, igual que ya hace
/// OnSkipCleanup() en el sistema viejo.
public static class NarrativeSkipHub
{
    public static event Action OnSkipRequested;

    /// El sistema que reproduce la secuencia debe suscribirse SOLO mientras tiene una secuencia
    /// activa (típicamente al entrar en su equivalente de "modo cinemático") y desuscribirse al
    /// salir — igual que CinematicSequencerBase.s_runningSequences, para que RequestSkip() no
    /// dispare sobre un handler de una secuencia que ya terminó.
    public static void RegisterSkipHandler(Action handler) => OnSkipRequested += handler;
    public static void UnregisterSkipHandler(Action handler) => OnSkipRequested -= handler;

    /// Llamado por HoldToSkipUI al completar el hold. No falla si no hay nadie suscrito (no-op
    /// seguro, mismo criterio que CinematicSequencerBase.RequestSkipAll()).
    public static void RequestSkip() => OnSkipRequested?.Invoke();

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnSkipRequested = null;
#endif
}
