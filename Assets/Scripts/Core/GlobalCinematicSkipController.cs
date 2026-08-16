using System.Collections;
using UnityEngine;

/// Muestra/oculta el botón global de "saltar cinemática" (HoldToSkipUI, con
/// SkipAction = SkipNarrativeSequence) según el jugador esté o no en ActionMode.Cinematic
/// (PlayerActionManager.IsInMode), no según CinematicSequencerBase.
///
/// FIX (16/08/2026): antes escuchaba solo CinematicSequencerBase.OnAnySequenceActiveChanged, pero
/// esa clase (Assets/Scripts/Cinematics/) es el sistema VIEJO de cinemáticas — ya no es el que
/// reproduce las secuencias reales del juego (eso lo hacen DialogueCinematicController y
/// DialogueManager, que nunca tocan CinematicSequencerBase). Resultado: el botón nunca se
/// activaba para ninguna secuencia real. PlayerActionManager.ActionMode.Cinematic sí es el punto
/// común real — lo empujan/popean los tres sistemas (DialogueManager, CinematicSequencerBase,
/// PlayerBattleModeController, SleepTrigger), así que es la señal correcta de "hay algo saltable
/// en curso". Ver también NarrativeSkipHub para el lado de "qué pasa al pulsar saltar".
///
/// Por qué un coroutine de sondeo de baja frecuencia (0.15s) en vez de un evento: el jugador
/// (y por tanto su PlayerActionManager) se destruye/recrea entre transiciones de escena, mientras
/// que este controlador vive siempre en Start.unity — suscribirse al evento de instancia de un
/// PlayerActionManager concreto se quedaría colgado de una instancia ya destruida tras el primer
/// cambio de escena. ServiceLocator no notifica altas/bajas, así que no hay forma de resuscribirse
/// sin volver a resolver la instancia; en su lugar, cada tick barato solo pregunta
/// ServiceLocator.TryGet + IsInMode (sin FindObjectOfType, ver CLAUDE.md §2). No es un sondeo en
/// Update() — corre en una coroutine con WaitForSecondsRealtime, mismo patrón que ya usa
/// HoldToSkipUI.InitializeInputWithRetry().
///
/// Por qué existe este componente en vez de dejar el HoldToSkipUI siempre activo: OnEnable()
/// de HoldToSkipUI llama a PlayerInputManager.PushUIMode() (necesario para que Controls.UI.Submit
/// reciba input durante una cinemática, donde el mapa Gameplay está deshabilitado). Si el botón
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

    [Tooltip("Frecuencia (segundos, tiempo real) a la que se comprueba PlayerActionManager.IsInMode(ActionMode.Cinematic).")]
    [SerializeField, Min(0.05f)] private float pollInterval = 0.15f;

    private bool _lastActive;
    private Coroutine _pollCoroutine;

    private void Awake()
    {
        // Estado inicial defensivo: por si se ha dejado activo por error en el Editor, forzar
        // desactivado hasta que una secuencia lo active de verdad.
        if (skipButtonRoot != null && skipButtonRoot.activeSelf)
            skipButtonRoot.SetActive(false);
    }

    private void OnEnable()
    {
        _lastActive = false;
        // Guard: no arrancar un segundo poll si ya hay uno corriendo (CLAUDE.md §2).
        if (_pollCoroutine == null)
            _pollCoroutine = StartCoroutine(Co_PollCinematicMode());
    }

    private void OnDisable()
    {
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
            _pollCoroutine = null;
        }
    }

    private IEnumerator Co_PollCinematicMode()
    {
        var wait = new WaitForSecondsRealtime(pollInterval);
        while (true)
        {
            bool active = ServiceLocator.TryGet<PlayerActionManager>(out var actionManager)
                && actionManager.IsInMode(ActionMode.Cinematic);

            if (active != _lastActive)
            {
                _lastActive = active;
                if (skipButtonRoot != null)
                    skipButtonRoot.SetActive(active);
            }

            yield return wait;
        }
    }
}
