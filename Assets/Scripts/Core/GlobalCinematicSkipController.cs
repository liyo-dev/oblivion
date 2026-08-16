using UnityEngine;

/// Muestra/oculta el botón global de "saltar secuencia" (HoldToSkipUI, con
/// SkipAction = SkipNarrativeSequence) según NarrativeSkipHub.AnySkippable /
/// OnAnySkippableChanged — es decir, según haya o no AL MENOS UN sistema registrado ahora mismo en
/// el hub como "tengo algo activo que se puede saltar".
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
[DisallowMultipleComponent]
public class GlobalCinematicSkipController : MonoBehaviour
{
    [Tooltip("GameObject raíz de la instancia de HoldToSkipUI.prefab que este controlador muestra/oculta. Debe empezar desactivado en la escena.")]
    [SerializeField] private GameObject skipButtonRoot;

    private void Awake()
    {
        // Estado inicial defensivo: por si se ha dejado activo por error en el Editor, forzar
        // desactivado hasta que una secuencia lo active de verdad.
        if (skipButtonRoot != null && skipButtonRoot.activeSelf)
            skipButtonRoot.SetActive(false);
    }

    private void OnEnable()
    {
        NarrativeSkipHub.OnAnySkippableChanged += HandleAnySkippableChanged;
        // Por si este controlador se activa/recarga mientras ya hay algo saltable en curso
        // (recarga de dominio en el Editor, por ejemplo) — sincroniza el estado inicial.
        if (skipButtonRoot != null)
            skipButtonRoot.SetActive(NarrativeSkipHub.AnySkippable);
    }

    private void OnDisable()
    {
        NarrativeSkipHub.OnAnySkippableChanged -= HandleAnySkippableChanged;
    }

    private void HandleAnySkippableChanged(bool active)
    {
        if (skipButtonRoot != null)
            skipButtonRoot.SetActive(active);
    }
}
