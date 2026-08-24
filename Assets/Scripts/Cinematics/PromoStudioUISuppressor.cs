using UnityEngine;

/// Esta escena (`PromoEstudio.unity`) es un "plató" de grabación para la serie de vídeos promo "en
/// personaje" (ver PromoVideo01Sequencer) — nunca la juega un jugador real, solo se usa para
/// capturar vídeo con "Simular secuencia"/F6. El botón global de "mantén pulsado para saltar"
/// (GlobalCinematicSkipController + HoldToSkipUI, Assets/Scripts/Core/, viven en Start.unity de
/// forma persistente) se activa automáticamente en cuanto CinematicSequencerBase.AnySequenceActive
/// se pone a true — exactamente lo que hace PromoVideo01Sequencer al reproducirse, porque hereda de
/// CinematicSequencerBase igual que cualquier cinemática real. Ese botón no tiene ningún sentido
/// aquí y arruinaría cualquier grabación si apareciera en pantalla. Este componente suprime el botón
/// mientras dura esta escena y lo restaura al salir de ella, sin tocar su comportamiento normal en
/// el resto del juego.
///
/// Por qué la supresión vive aquí y no en CinematicSequencerBase/GlobalCinematicSkipController: el
/// botón de skip SÍ debe seguir funcionando con normalidad en todas las cinemáticas reales del
/// juego — esto es una particularidad de ESTA escena concreta (una herramienta de grabación, no
/// gameplay real), así que la supresión vive a nivel de escena y no como comportamiento genérico del
/// sistema de cinemáticas.
///
/// Colocación: componente añadido por PromoStudioSceneBuilder sobre el contenedor
/// "Cinematica_Estudio" — no requiere ninguna referencia de Inspector.
[DisallowMultipleComponent]
public class PromoStudioUISuppressor : MonoBehaviour
{
    private GlobalCinematicSkipController _skipController;

    private void Start()
    {
        // Búsqueda única al entrar en la escena (no en Update — ver CLAUDE.md, regla de
        // FindObjectOfType/FindObjectsByType). El controlador vive en Start.unity, cargada de forma
        // aditiva por AutoBootstrapOnPlay.cs ANTES de que empiece el Play, así que para cuando Unity
        // llega a este Start() ya existe e inicializado (Awake/OnEnable de todos los objetos de
        // todas las escenas cargadas se procesan antes que cualquier Start(), sea cual sea la
        // escena) — ver Assets/Editor/AutoBootstrapOnPlay.cs.
        _skipController = FindAnyObjectByType<GlobalCinematicSkipController>();
        if (_skipController == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PromoStudioUISuppressor] No se encontró GlobalCinematicSkipController en la " +
                "escena — ¿se ha dado Play sin que 'Start.unity' se cargue de forma aditiva? El botón de skip " +
                "no debería aparecer de todos modos en ese caso, así que no hay nada que suprimir.");
#endif
            return;
        }
        _skipController.Suppress();
    }

    private void OnDestroy()
    {
        // Restaura el comportamiento normal del botón para el resto del juego al salir de esta
        // escena (volver al Editor, o si algún día esta escena se carga/descarga en cadena con otras).
        if (_skipController != null)
            _skipController.Unsuppress();
    }
}
