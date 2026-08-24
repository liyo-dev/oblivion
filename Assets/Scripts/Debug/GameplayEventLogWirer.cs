using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Conecta GameplayEventLog a los sistemas del juego que ya exponen eventos propios, sin tocar
/// ninguno de ellos: TeleportService (OnTeleportStarted/OnTeleportEnded), CinematicSequencerBase
/// (OnAnySequenceActiveChanged + RunningSequences), MenuManager (MenuOpened/MenuClosed) y el propio
/// SceneManager de Unity (sceneLoaded). Todos esos eventos son estáticos y viven durante toda la
/// sesión de juego, así que basta con suscribirse UNA vez al arrancar — no hace falta volver a
/// suscribirse en cada cambio de escena.
///
/// Los sistemas que todavía no tenían un evento propio a nivel de proyecto (estado de batalla del
/// jugador, clima, diálogo) anotan directamente con GameplayEventLog.Log(...) desde su propio
/// código en vez de pasar por aquí — ver PlayerBattleModeController, DayNightCycle y SpeechBubbleUI.
///
/// Vive junto a PerformanceCapture en MainMenu (ver PerformanceCaptureBuilder.cs), con
/// DontDestroyOnLoad, para sobrevivir a todos los cambios de escena durante la partida.
/// </summary>
[DisallowMultipleComponent]
public class GameplayEventLogWirer : MonoBehaviour
{
    static bool s_wired;

    void Awake()
    {
        // Guard contra doble wiring: si por lo que sea llega a haber dos instancias vivas a la vez
        // (el builder de escena se reejecuta con Play ya en marcha, o una recarga de dominio deja
        // un residuo), un segundo Awake() no debe volver a suscribirse — cada evento se dispararía
        // dos veces y el .json quedaría con eventos duplicados. Se destruye solo ESTE componente
        // (no el GameObject entero), porque comparte GameObject con PerformanceCapture.
        if (s_wired)
        {
            Destroy(this);
            return;
        }
        s_wired = true;
        DontDestroyOnLoad(gameObject);

        TeleportService.OnTeleportStarted += OnTeleportStarted;
        TeleportService.OnTeleportEnded += OnTeleportEnded;

        CinematicSequencerBase.OnAnySequenceActiveChanged += OnCinematicActiveChanged;

        MenuManager.MenuOpened += OnMenuOpened;
        MenuManager.MenuClosed += OnMenuClosed;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnTeleportStarted() => GameplayEventLog.Log("TeletransporteInicio");
    static void OnTeleportEnded() => GameplayEventLog.Log("TeletransporteFin");

    static void OnCinematicActiveChanged(bool active)
    {
        if (active)
        {
            // El evento no dice CUÁL cinemática — se aproxima leyendo qué hay activo ahora mismo en
            // RunningSequences (normalmente una sola, salvo cinemáticas encadenadas). Suficiente para
            // saber "qué cinemática estaba corriendo" al leer el .json, sin tocar CinematicSequencerBase.
            var running = CinematicSequencerBase.RunningSequences;
            if (running.Count == 0)
            {
                GameplayEventLog.Log("CinematicaInicio");
                return;
            }
            foreach (var seq in running)
                GameplayEventLog.Log("CinematicaInicio", seq.GetType().Name);
        }
        else
        {
            GameplayEventLog.Log("CinematicaFin");
        }
    }

    static void OnMenuOpened(MenuKind kind) => GameplayEventLog.Log("MenuAbierto", kind.ToString());
    static void OnMenuClosed(MenuKind kind) => GameplayEventLog.Log("MenuCerrado", kind.ToString());

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => GameplayEventLog.Log("EscenaCargada", scene.name);

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_wired = false;
#endif
}
