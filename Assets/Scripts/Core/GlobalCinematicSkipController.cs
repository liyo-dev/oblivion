using UnityEngine;

/// Muestra/oculta el botón global de "saltar secuencia" (HoldToSkipUI, con
/// SkipAction = SkipNarrativeSequence) según NarrativeSkipHub.AnySkippable /
/// OnAnySkippableChanged — es decir, según haya o no AL MENOS UN sistema registrado ahora mismo en
/// el hub como "tengo algo activo que se puede saltar" — Y TAMBIÉN según
/// CinematicSequencerBase.AnySequenceActive (ver FIX 16/08/2026 más abajo).
///
/// FIX (16/08/2026, segunda vuelta): la primera versión de este fix escuchaba
/// PlayerActionManager.IsInMode(ActionMode.Cinematic) — mejor que el CinematicSequencerBase
/// original (ver historial), pero seguía dejando fuera cualquier secuencia que NO bloquee el
/// movimiento del jugador. Caso real que lo destapó: un bocadillo de villano (ShowSpeechBubbleNode
/// en NarrativeGraph) sin LockPlayerNode emparejado — el grafo se queda esperando la duración del
/// bocadillo igualmente, pero como nadie empuja ActionMode.Cinematic, el botón nunca aparecía
/// aunque SÍ había algo que saltar. Usar NarrativeSkipHub como única fuente de verdad de
/// visibilidad arregla esto de raíz: "el botón se ve" y "pulsarlo hace algo" son ahora
/// LITERALMENTE la misma condición (el mismo registro), así que no pueden desincronizarse — ver
/// NarrativeSkipHub.cs para el razonamiento completo y el contrato que debe cumplir cada handler.
///
/// Por qué un evento y no sondeo: a diferencia de PlayerActionManager (ligado a la instancia del
/// jugador, que se destruye/recrea entre escenas), NarrativeSkipHub es una clase estática que vive
/// todo el proceso — suscribirse a su evento aquí es seguro y no se queda colgado de nada que
/// pueda desaparecer.
///
/// Por qué existe este componente en vez de dejar el HoldToSkipUI siempre activo: OnEnable()
/// de HoldToSkipUI llama a PlayerInputManager.PushUIMode() (necesario para que Controls.UI.Submit
/// reciba input durante una secuencia, donde el mapa Gameplay está deshabilitado). Si el botón
/// viviera siempre activo en la escena persistente (Start.unity), ese PushUIMode() quedaría
/// enganchado permanentemente y rompería el input normal de gameplay en todo el juego. En su
/// lugar, este controlador — que SÍ vive siempre activo — mantiene el GameObject del botón
/// desactivado por defecto y solo lo activa mientras dura una secuencia saltable.
///
/// Colocación: como componente en un GameObject siempre activo de Start.unity (junto al resto de
/// managers persistentes), con skipButtonRoot apuntando al GameObject raíz de una instancia del
/// prefab HoldToSkipUI.prefab (hijo, con SkipAction = SkipNarrativeSequence en el Inspector).
///
/// FIX (16/08/2026): el cambio a NarrativeSkipHub como única fuente de visibilidad (ver más
/// arriba) dejó fuera, sin querer, a las cinemáticas del sistema VIEJO (CinematicSequencerBase —
/// PrologueDreamSequencer, StarAwakeningSequencer, LiamCrystalBallSequencer,
/// EstelaAppearsSequencer, LiamGolemSummonSequencer, TabernaSequencer...), que siguen en uso y
/// nunca se suscriben a NarrativeSkipHub (solo llevan su propio contador estático
/// s_activeSequenceCount / OnAnySequenceActiveChanged, que es lo que el controlador ANTERIOR sí
/// escuchaba). Resultado: en esas 6 cinemáticas el botón de skip dejó de aparecer aunque
/// HoldToSkipUI.ExecuteSkipAction() siga llamando correctamente a
/// CinematicSequencerBase.RequestSkipAll() (la ACCIÓN de saltar nunca se rompió, solo la
/// VISIBILIDAD del botón). En vez de revertir el fix de NarrativeSkipHub (que sí soluciona el
/// caso real de bocadillos sin LockPlayerNode), este controlador ahora escucha AMBAS fuentes y
/// muestra el botón si cualquiera de las dos tiene algo activo.
///
/// FIX (24/08/2026): el comentario de "Colocación" de arriba dice que este componente vive
/// "siempre activo... junto al resto de managers persistentes" — pero en la escena real vive en
/// su propio GameObject raíz, hermano de CoreSystems (el único objeto de Start.unity que de
/// verdad llama a DontDestroyOnLoad), sin heredar esa persistencia. BootLoader.Start() hace
/// SceneManager.LoadScene(sceneToLoad) en modo Single (descarga Start.unity entero) en cuanto la
/// escena activa es literalmente "Start" — es decir, en el flujo real de juego (build, o dando
/// Play desde la propia escena Start). Sin persistencia propia, este GameObject (y el
/// HoldToSkipUI que cuelga de él como hijo) se destruía justo al entrar a MainWorld: a partir de
/// ahí nadie quedaba escuchando NarrativeSkipHub/CinematicSequencerBase y el icono de "mantener
/// para saltar" no podía aparecer NUNCA en una partida real, sin importar cómo se disparara la
/// secuencia. Solo sobrevivía cuando Start.unity se quedaba cargado sin llegar a descargarse —
/// p.ej. abriendo una escena de secuencia (Prólogo, Taberna...) directamente en el Editor desde
/// MainWorld: AutoBootstrapOnPlay carga Start.unity de forma aditiva, pero BootLoader.Start()
/// nunca dispara el LoadScene porque la escena activa no es "Start", así que este objeto se
/// queda vivo todo el rato y el botón funciona con normalidad — de ahí la diferencia observada
/// entre "abrir la secuencia directo" (funciona) y "build/desde Start" (nunca sale). Arreglado
/// con el mismo patrón que ya usa CoreSystems: guarda propia de instancia única +
/// DontDestroyOnLoad en Awake(), en vez de depender de la jerarquía de la escena.
[DisallowMultipleComponent]
public class GlobalCinematicSkipController : MonoBehaviour
{
    [Tooltip("GameObject raíz de la instancia de HoldToSkipUI.prefab que este controlador muestra/oculta. Debe empezar desactivado en la escena.")]
    [SerializeField] private GameObject skipButtonRoot;

    private static GlobalCinematicSkipController _instance;

    private void Awake()
    {
        // Mismo patrón que CoreSystems.cs: instancia única persistente. Necesario porque este
        // objeto (y el HoldToSkipUI que cuelga de él) debe sobrevivir a la transición Start →
        // MainWorld (ver FIX 24/08/2026 más arriba) — sin esto, BootLoader lo destruye al cargar
        // MainWorld en modo Single y el botón de skip deja de funcionar en el flujo real de juego.
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Estado inicial defensivo: por si se ha dejado activo por error en el Editor, forzar
        // desactivado hasta que una secuencia lo active de verdad.
        if (skipButtonRoot != null && skipButtonRoot.activeSelf)
            skipButtonRoot.SetActive(false);
    }

    // FIX (24 ago 2026): supresión a nivel de escena para PromoEstudio.unity (ver
    // PromoStudioUISuppressor, Assets/Scripts/Cinematics/) — un "plató" de grabación donde
    // PromoVideo01Sequencer (un CinematicSequencerBase normal) pone AnySequenceActive a true igual
    // que cualquier cinemática real, pero el botón de skip no tiene ningún sentido ahí y arruinaría
    // la grabación si apareciera en pantalla. Mientras _suppressed es true, Refresh() no reacciona a
    // ningún evento — el botón se queda oculto pase lo que pase en NarrativeSkipHub/
    // CinematicSequencerBase — sin afectar a su funcionamiento normal en el resto del juego.
    private bool _suppressed;

    private void OnEnable()
    {
        NarrativeSkipHub.OnAnySkippableChanged += HandleAnySkippableChanged;
        CinematicSequencerBase.OnAnySequenceActiveChanged += HandleAnySequenceActiveChanged;
        // Por si este controlador se activa/recarga mientras ya hay algo saltable en curso
        // (recarga de dominio en el Editor, por ejemplo) — sincroniza el estado inicial.
        Refresh();
    }

    private void OnDisable()
    {
        NarrativeSkipHub.OnAnySkippableChanged -= HandleAnySkippableChanged;
        CinematicSequencerBase.OnAnySequenceActiveChanged -= HandleAnySequenceActiveChanged;
    }

    private void HandleAnySkippableChanged(bool active) => Refresh();

    private void HandleAnySequenceActiveChanged(bool active) => Refresh();

    private void Refresh()
    {
        if (_suppressed) return;
        if (skipButtonRoot != null)
            skipButtonRoot.SetActive(NarrativeSkipHub.AnySkippable || CinematicSequencerBase.AnySequenceActive);
    }

    /// Oculta el botón y deja de reaccionar a NarrativeSkipHub/CinematicSequencerBase mientras dure
    /// la supresión. Pensado para escenas que no son gameplay real (ver PromoStudioUISuppressor) —
    /// no toca el comportamiento del botón en ninguna otra escena.
    public void Suppress()
    {
        if (_suppressed) return;
        _suppressed = true;
        if (skipButtonRoot != null) skipButtonRoot.SetActive(false);
    }

    /// Deshace Suppress() y vuelve a sincronizar la visibilidad real (por si algo se activó en el
    /// resto del juego persistente mientras tanto).
    public void Unsuppress()
    {
        if (!_suppressed) return;
        _suppressed = false;
        Refresh();
    }
}
