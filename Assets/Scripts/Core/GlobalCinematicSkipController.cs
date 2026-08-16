using UnityEngine;

/// Muestra/oculta el botón global de "saltar cinemática" (HoldToSkipUI, con
/// SkipAction = SkipNarrativeSequence) según haya o no una CinematicSequencerBase activa
/// (CinematicSequencerBase.AnySequenceActive / OnAnySequenceActiveChanged).
///
/// Por qué existe este componente en vez de dejar el HoldToSkipUI siempre activo: OnEnable()
/// de HoldToSkipUI llama a PlayerInputManager.PushUIMode() (necesario para que Controls.UI.Submit
/// reciba input durante una cinemática, donde el mapa Gameplay está deshabilitado). Si el botón
/// viviera siempre activo en la escena persistente (Start.unity), ese PushUIMode() quedaría
/// enganchado permanentemente y rompería el input normal de gameplay en todo el juego. En su
/// lugar, este controlador — que SÍ vive siempre activo — mantiene el GameObject del botón
/// desactivado por defecto y solo lo activa mientras dura una cinemática saltable, vía el evento
/// estático (sin sondear nada en Update, ver CLAUDE.md §2).
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
        // desactivado hasta que una cinemática lo active de verdad.
        if (skipButtonRoot != null && skipButtonRoot.activeSelf)
            skipButtonRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CinematicSequencerBase.OnAnySequenceActiveChanged += HandleAnySequenceActiveChanged;
        // Por si este controlador se activa/recarga mientras ya hay una cinemática en curso
        // (recarga de dominio en el Editor, por ejemplo) — sincroniza el estado inicial.
        if (skipButtonRoot != null)
            skipButtonRoot.SetActive(CinematicSequencerBase.AnySequenceActive);
    }

    private void OnDisable()
    {
        CinematicSequencerBase.OnAnySequenceActiveChanged -= HandleAnySequenceActiveChanged;
    }

    private void HandleAnySequenceActiveChanged(bool active)
    {
        if (skipButtonRoot != null)
            skipButtonRoot.SetActive(active);
    }
}
