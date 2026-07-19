using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using EasyTransition;
using Sendero.Core.Feedback;
using Sendero.UI;

/// Clase base para todos los orquestadores de cinemáticas del juego.
/// Centraliza señales narrativas, modo de acción, cámara cinemática, HUD,
/// gestión de música y utilidades reutilizables.
///
/// Subclases deben:
///   1. Implementar Co_Sequence() con la lógica específica.
///   2. Abrir con Co_BeginCinematicWithTransition() y cerrar con Co_EndCinematicWithTransition().
///   3. Usar PlaySequenceMusic() / RestoreMusic() para el audio.
///   4. Emitir la señal de salida con RaiseSignalOut() o RaiseSignal(string).
[DisallowMultipleComponent]
public abstract class CinematicSequencerBase : MonoBehaviour
{
    [Header("Señales narrativas")]
    [FormerlySerializedAs("signalIn")]
    [SerializeField] private string _signalIn;
    [FormerlySerializedAs("signalOut")]
    [SerializeField] private string _signalOut;

    [Header("Gameplay")]
    [FormerlySerializedAs("actionManager")]
    [SerializeField] private PlayerActionManager _actionManager;

    [Header("Cámara — driver cinemático")]
    [FormerlySerializedAs("cinematicCamera")]
    [SerializeField] protected CinematicCameraDriver _cinematicCamera;

    [Header("Música")]
    [FormerlySerializedAs("audioProfile")]
    [SerializeField] private AudioGraphProfile _audioProfile;
    [FormerlySerializedAs("sequenceMusicId")]
    [SerializeField] private string _sequenceMusicId;

    [Header("Transición")]
    [Tooltip("Transición al entrar: cubre el gameplay, el corte de cámara ocurre en el cut point, luego revela la cinemática.")]
    [SerializeField] private TransitionSettings _entryTransition;
    [Tooltip("Transición al salir: cubre la cinemática, el corte de cámara ocurre en el cut point, luego revela el gameplay.")]
    [SerializeField] private TransitionSettings _exitTransition;

    protected AudioGraphProfile.SequenceRule MusicRule { get; private set; }

    // ── Ciclo de vida Unity ───────────────────────────────────────────────────

    protected virtual void Awake()
    {
        DefaultNarrativeSignals.EnsureInstance().OnCustom(_signalIn,
            () => StartCoroutine(Co_Sequence()));
    }

    protected virtual void OnDestroy()
    {
        FeedbackService.CancelAllShakes();
    }

    // ── Punto de entrada de la subclase ──────────────────────────────────────

    protected abstract IEnumerator Co_Sequence();

    // ── Ciclo de vida de la cinemática ────────────────────────────────────────

    /// Prepara el estado inicial: bloquea input, oculta HUD y activa la cámara cinemática.
    /// Resuelve también la regla de música para que PlaySequenceMusic() funcione.
    protected void BeginCinematic()
    {
        MusicRule = _audioProfile?.GetSequenceRule(_sequenceMusicId);
        _actionManager.PushMode(ActionMode.Cinematic);
        PlayerHUDV2.Instance?.HideHUD();
        _cinematicCamera.Activate();
    }

    /// Restaura el estado de gameplay: cancela shakes, desactiva la cámara, muestra el HUD y desbloquea input.
    protected void EndCinematic()
    {
        FeedbackService.CancelAllShakes();
        _cinematicCamera.Deactivate();
        PlayerHUDV2.Instance?.ShowHUD();
        _actionManager.PopMode(ActionMode.Cinematic);
    }

    // ── Transiciones ─────────────────────────────────────────────────────────

    /// Cubre la pantalla, llama a BeginCinematic() en el cut point y revela la cinemática.
    /// additionalOnCut: acciones extra que deben ocurrir junto con BeginCinematic (mismo frame, pantalla cubierta).
    protected IEnumerator Co_BeginCinematicWithTransition(Action additionalOnCut = null)
        => Co_Transition(_entryTransition, () => { BeginCinematic(); additionalOnCut?.Invoke(); });

    /// Cubre la pantalla, llama a EndCinematic() en el cut point y revela el gameplay.
    /// additionalOnCut: acciones extra que deben ocurrir junto con EndCinematic (mismo frame, pantalla cubierta).
    protected IEnumerator Co_EndCinematicWithTransition(Action additionalOnCut = null)
        => Co_Transition(_exitTransition, () => { additionalOnCut?.Invoke(); EndCinematic(); });

    /// Ejecuta una transición via TransitionManager. onCutPoint se llama cuando la pantalla está cubierta.
    /// Si settings es null, llama onCutPoint de inmediato sin animación.
    private IEnumerator Co_Transition(TransitionSettings settings, Action onCutPoint)
    {
        var tm = TransitionManager.Instance();
        if (settings == null || tm == null)
        {
            onCutPoint?.Invoke();
            yield break;
        }

        bool done = false;

        UnityEngine.Events.UnityAction cutHandler = null;
        UnityEngine.Events.UnityAction endHandler = null;

        cutHandler = () =>
        {
            tm.onTransitionCutPointReached -= cutHandler;
            onCutPoint?.Invoke();
        };
        endHandler = () =>
        {
            tm.onTransitionEnd -= endHandler;
            done = true;
        };

        tm.onTransitionCutPointReached += cutHandler;
        tm.onTransitionEnd += endHandler;
        tm.Transition(settings, 0f);

        yield return new WaitUntil(() => done);
    }

    // ── Música ────────────────────────────────────────────────────────────────

    protected void PlaySequenceMusic()
    {
        if (MusicRule?.music != null && AudioService.Instance != null)
            AudioService.Instance.PlayMusic(MusicRule.music, MusicRule.fadeIn);
    }

    protected void RestoreMusic()
    {
        if (AudioService.Instance == null) return;
        float fadeDur = MusicRule?.fadeOut ?? 0.8f;
        if (!AudioService.Instance.RestoreSceneMusic(fadeDur))
            AudioService.Instance.StopMusic(fadeDur);
    }

    // ── Señales ───────────────────────────────────────────────────────────────

    protected void RaiseSignalOut() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_signalOut);

    protected void RaiseSignal(string signal) =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signal);

    // ── Localización ─────────────────────────────────────────────────────────

    protected string Loc(string key) => LocalizationManager.Instance != null
        ? LocalizationManager.Instance.Get(key, key)
        : key;

    // ── Bocadillos paginados ──────────────────────────────────────────────────

    /// Muestra un texto con saltos de línea como páginas sucesivas de bocadillo.
    /// loopAnim: si true, re-dispara el animTrigger en cada página.
    protected IEnumerator ShowBubblePaged(Transform target, string text, float durationPerPage,
        string animTrigger = null, bool loopAnim = false)
    {
        string[] pages = text.Split('\n');
        for (int i = 0; i < pages.Length; i++)
        {
            string page = pages[i].Trim();
            if (string.IsNullOrEmpty(page)) continue;
            bool done = false;
            string triggerThisPage = (i == 0 || loopAnim) ? animTrigger : null;
            SpeechBubbleUI.Instance.Show(target, page, durationPerPage, () => done = true, triggerThisPage);
            yield return new WaitUntil(() => done);
        }
    }

    // ── Rotación de personajes ────────────────────────────────────────────────

    protected static void FaceTarget(Transform from, Transform to)
    {
        if (from == null || to == null) return;
        Vector3 dir = to.position - from.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            from.rotation = Quaternion.LookRotation(dir);
    }

    protected static void FaceTarget(Transform from, Vector3 worldPos)
    {
        if (from == null) return;
        Vector3 dir = worldPos - from.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            from.rotation = Quaternion.LookRotation(dir);
    }

    protected static void FaceAway(Transform from, Transform away)
    {
        if (from == null || away == null) return;
        Vector3 dir = from.position - away.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            from.rotation = Quaternion.LookRotation(dir);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular secuencia")]
    protected void SimulateSequence() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_signalIn);
#endif
}
