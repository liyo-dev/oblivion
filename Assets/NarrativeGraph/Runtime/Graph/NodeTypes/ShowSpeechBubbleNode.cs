using System;
using UnityEngine;

/// <summary>
/// Muestra un bocadillo de cómic flotante sobre un personaje.
/// Si duration > 0 y waitForCompletion, el grafo espera a que se oculte automáticamente.
/// </summary>
[Serializable]
public sealed class ShowSpeechBubbleNode : NarrativeNode
{
    [Tooltip("Tag del GameObject sobre el que aparece el bocadillo (ej: 'Player').")]
    public string targetTag = "Player";

    [TextArea(1, 3)]
    public string text;

    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    public string textId;

    [Tooltip("Segundos antes de que el bocadillo desaparezca automáticamente. 0 = permanece.")]
    [Min(0f)]
    public float duration = 3f;

    [Tooltip("Si true y duration > 0, el grafo espera a que el bocadillo desaparezca.")]
    public bool waitForCompletion = true;

    [Header("Personaje")]
    [Tooltip("Nombre exacto del estado/animación del Animator del personaje (ej: 'Talk', 'Wave'). Vacío = sin animación.")]
    public string animTrigger;

    [Tooltip("Si true, usa el sprite de énfasis (burbuja explosiva) en lugar del normal.")]
    public bool emphasis;

    [Tooltip("Nombre del personaje que habla (ej: 'Will', 'Estela'). Opcional — si se rellena, aparece como primera línea dentro del bocadillo para dejar claro quién habla cuando hay varios NPCs juntos. Vacío = comportamiento de siempre, sin nombre.")]
    public string speakerName;

    // Runtime-only: handler activo registrado en NarrativeSkipHub mientras este nodo espera su
    // bocadillo (ver Enter/Exit). No es dato de diseño del grafo — Action no es serializable por
    // Unity de todas formas, pero se documenta como tal para que quede claro que no debe tratarse
    // como campo de configuración del nodo.
    [NonSerialized] private Action _activeSkipHandler;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = SpeechBubbleUI.Instance;

        if (ui == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[ShowSpeechBubbleNode] ❌ SpeechBubbleUI.Instance es NULL. " +
                           "Añade el prefab al Canvas persistente en Start.unity.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        GameObject target = string.IsNullOrEmpty(targetTag)
            ? null
            : GameObject.FindWithTag(targetTag);

        if (target == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[ShowSpeechBubbleNode] ❌ No se encontró objeto con tag '{targetTag}'.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        string resolved = string.IsNullOrEmpty(textId)
            ? text
            : LocalizationManager.Instance?.Get(textId, text) ?? text;

        bool willWait = waitForCompletion && duration > 0f;
        Action callback = null;

        if (willWait)
        {
            // FIX (16/08/2026): registrar en NarrativeSkipHub mientras el grafo está bloqueado
            // esperando este bocadillo — sin esto, un bocadillo sin LockPlayerNode emparejado (el
            // jugador sigue moviéndose, pero el grafo sigue parado igual) no tenía forma de
            // saltarse: nadie empujaba ActionMode.Cinematic (viejo criterio de visibilidad) y
            // NarrativeSkipHub tampoco tenía a nadie registrado (nuevo criterio), así que el botón
            // global de skip nunca aparecía aunque SÍ había algo saltable en curso.
            //
            // Guardado en el campo de instancia (no en una variable local) para que Exit() pueda
            // desregistrarlo si el runner interrumpe este nodo (StopExecution(), cambio de grafo,
            // etc.) ANTES de que el bocadillo termine solo o se salte — si no, el registro se
            // quedaría huérfano para siempre y NarrativeSkipHub.AnySkippable se quedaría en true
            // permanentemente (botón de skip visible sin nada real que saltar).
            _activeSkipHandler = () => SpeechBubbleUI.Instance?.SkipCurrent();

            // El callback que le pasamos a Show() se desregistra a sí mismo la primera vez que se
            // dispare, sea por la vía normal (el bocadillo termina solo) o por skip (SkipCurrent()
            // fuerza el mismo callback antes de tiempo) — SpeechBubbleUI garantiza no invocarlo dos
            // veces (ver AutoHide/SkipCurrent), así que este desregistro también ocurre una sola vez.
            Action wrappedCallback = () =>
            {
                NarrativeSkipHub.UnregisterSkipHandler(_activeSkipHandler);
                _activeSkipHandler = null;
                onReadyToAdvance?.Invoke();
            };

            NarrativeSkipHub.RegisterSkipHandler(_activeSkipHandler);
            callback = wrappedCallback;
        }

        ui.Show(target.transform, resolved, duration, callback, animTrigger, emphasis, speakerName);

        if (!willWait)
            onReadyToAdvance?.Invoke();
    }

    /// Red de seguridad: si el runner interrumpe/abandona este nodo (StopExecution(), GoTo() a
    /// otro nodo por una vía distinta a nuestro propio onReadyToAdvance, cambio de escena) mientras
    /// seguíamos esperando el bocadillo, el callback normal de Show() nunca llega a dispararse.
    /// Sin este Exit(), _activeSkipHandler se quedaría registrado en NarrativeSkipHub para
    /// siempre — ver comentario en Enter().
    public override void Exit(NarrativeContext ctx)
    {
        if (_activeSkipHandler == null) return;
        NarrativeSkipHub.UnregisterSkipHandler(_activeSkipHandler);
        _activeSkipHandler = null;
    }
}
