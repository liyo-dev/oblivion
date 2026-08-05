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

    [Header("Entorno")]
    [Tooltip("Asignar si la cinemática ocurre en un interior. Activa/desactiva el skybox sólido automáticamente.")]
    [SerializeField] private AnchorEnvironment _interiorAnchor;

    protected AudioGraphProfile.SequenceRule MusicRule { get; private set; }

    private Action _signalInHandler;

    // FIX INC-059: contador estático de cinemáticas activas (puede haber más de un sequencer
    // encadenado). Otros sistemas (ej: NPCQuestIconManager) lo consultan para ocultar iconos de
    // quest sobre la cabeza de NPCs ajenos a la propia cinemática mientras esta se reproduce.
    private static int s_activeSequenceCount;
    public static bool AnySequenceActive => s_activeSequenceCount > 0;

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsBase() => s_activeSequenceCount = 0;
#endif

    // FIX INC-052: si Co_Sequence() lanza una excepción o el objeto se destruye a mitad de la
    // cinemática (ej: cambio de escena, referencia nula puntual de un prefab concreto), el HUD y
    // el minimapa se quedaban ocultos para siempre y el ActionMode.Cinematic nunca se hacía Pop.
    // Como la cinemática de Liam invocando al gólem se ejecuta justo antes de la batalla, un fallo
    // aislado aquí dejaba el HUD desaparecido durante todo el combate. Mismo patrón de fix que
    // BossIntroPresentation.PlayIntroduction() (try/finally que garantiza la restauración).
    private bool _cinematicLocked;

    // ── Ciclo de vida Unity ───────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _signalInHandler = () => StartCoroutine(Co_SequenceGuarded());
        DefaultNarrativeSignals.EnsureInstance().OnCustom(_signalIn, _signalInHandler);
    }

    /// Envuelve Co_Sequence() para garantizar que el HUD/minimapa/modo Cinematic se restauran
    /// aunque la subclase termine de forma anómala (excepción o destrucción del objeto).
    private IEnumerator Co_SequenceGuarded()
    {
        try
        {
            yield return Co_Sequence();
        }
        finally
        {
            if (_cinematicLocked)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name} terminó de forma anómala con la cinemática aún bloqueada (HUD oculto). Restaurando estado.");
#endif
                EndCinematic();
            }
        }
    }

    protected virtual void OnDestroy()
    {
        FeedbackService.CancelAllShakes();
        DefaultNarrativeSignals.Instance?.OffCustom(_signalIn, _signalInHandler);
    }

    // ── Punto de entrada de la subclase ──────────────────────────────────────

    protected abstract IEnumerator Co_Sequence();

    // ── Ciclo de vida de la cinemática ────────────────────────────────────────

    /// Bloquea el input del jugador y oculta el HUD y el minimapa. Debe llamarse ANTES de la transición de entrada.
    private void LockCinematic()
    {
        _cinematicLocked = true;
        s_activeSequenceCount++;
        _actionManager.PushMode(ActionMode.Cinematic);
        PlayerHUDV2.Instance?.HideHUD();
        MinimapController.Instance?.SetHiddenByCinematic(true);
        // FIX INC-058: el icono del período del día (HUD) también debe ocultarse durante secuencias.
        TimeOfDayIndicator.Instance?.Hide();
    }

    /// Activa la cámara cinemática y prepara la regla de música. Se llama en el cut point de la transición.
    protected void BeginCinematic()
    {
        MusicRule = _audioProfile?.GetSequenceRule(_sequenceMusicId);
        if (_interiorAnchor != null) EnvironmentController.Instance?.BeginCinematicOverride();
        _cinematicCamera.Activate();
    }

    /// Restaura el estado de gameplay: cancela shakes, desactiva la cámara, muestra el HUD/minimapa y desbloquea input.
    protected void EndCinematic()
    {
        if (!_cinematicLocked) return; // Ya restaurado (evita Pop/ShowHUD duplicados si se llama dos veces)
        _cinematicLocked = false;
        s_activeSequenceCount = Mathf.Max(0, s_activeSequenceCount - 1);

        FeedbackService.CancelAllShakes();
        _cinematicCamera.Deactivate();
        PlayerHUDV2.Instance?.ShowHUD();
        MinimapController.Instance?.SetHiddenByCinematic(false);
        // FIX INC-058: restaurar el icono del período del día al terminar la secuencia.
        TimeOfDayIndicator.Instance?.Show();
        _actionManager.PopMode(ActionMode.Cinematic);
        if (_interiorAnchor != null) EnvironmentController.Instance?.EndCinematicOverride();
    }

    // ── Transiciones ─────────────────────────────────────────────────────────

    /// Cubre la pantalla, llama a BeginCinematic() en el cut point y revela la cinemática.
    /// additionalOnCut: acciones extra que deben ocurrir junto con BeginCinematic (mismo frame, pantalla cubierta).
    protected IEnumerator Co_BeginCinematicWithTransition(Action additionalOnCut = null)
    {
        LockCinematic();
        yield return Co_Transition(_entryTransition, () =>
        {
            BeginCinematic();
            // El interior se aplica en el cut point, con la pantalla cubierta:
            // si se aplica tras el reveal se ve el skybox exterior un instante
            ApplyInteriorAtCutPoint();
            additionalOnCut?.Invoke();
        });
    }

    /// Igual que el anterior pero corta al plano inicial durante el blackout,
    /// de modo que la cámara ya está en posición cuando la transición revela la escena.
    protected IEnumerator Co_BeginCinematicWithTransition(Transform initialShot, Action additionalOnCut = null)
    {
        LockCinematic();
        yield return Co_Transition(_entryTransition, () =>
        {
            BeginCinematic();
            if (initialShot != null) _cinematicCamera.Cut(initialShot);
            // El interior se aplica en el cut point, con la pantalla cubierta:
            // si se aplica tras el reveal se ve el skybox exterior un instante
            ApplyInteriorAtCutPoint();
            additionalOnCut?.Invoke();
        });
    }

    /// Aplica el entorno interior con la pantalla cubierta. Debe llamarse después
    /// de BeginCinematic() (que activa el override en EnvironmentController).
    private void ApplyInteriorAtCutPoint()
    {
        if (_interiorAnchor != null)
            EnvironmentController.Instance?.ApplyInteriorForCinematic(env: _interiorAnchor);
    }

    /// Cubre la pantalla, llama a EndCinematic() en el cut point y revela el gameplay.
    /// additionalOnCut: acciones extra que deben ocurrir junto con EndCinematic (mismo frame, pantalla cubierta).
    protected IEnumerator Co_EndCinematicWithTransition(Action additionalOnCut = null)
        => Co_Transition(_exitTransition, () => { additionalOnCut?.Invoke(); EndCinematic(); });

    /// Cubre la pantalla con negro y llama a EndCinematic(), pero NO revela después.
    /// Usar cuando el sistema siguiente (ej: BossIntroPresentation) maneja su propia revelación,
    /// para evitar el parpadeo de la cámara del jugador entre la secuencia y la intro del boss.
    protected IEnumerator Co_EndCinematicStayBlack(Action additionalOnCut = null, float fadeDuration = 0.3f)
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeDuration, fadeIn: true);
        additionalOnCut?.Invoke();
        EndCinematic();
    }

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

    protected void PlaySequenceMusic(string sequenceId)
    {
        var rule = _audioProfile?.GetSequenceRule(sequenceId);
        if (rule?.music != null && AudioService.Instance != null)
            AudioService.Instance.PlayMusic(rule.music, rule.fadeIn);
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

    // ── Persistencia de "cinemática ya vista" ─────────────────────────────────
    // INC-075 (05/08/2026): EstelaAppearsSequencer llevaba su propia clave de flag
    // ("CINEMATIC_SEEN:ESTELA_APPEARS", añadida a mano el 27/28 jul) que NO coincidía con la
    // convención real del proyecto ("CINEMATIC_SEEN:Cinematic_{id}", la misma que genera
    // SimpleCinematicDirector.GetPersistenceId()/MarkAsSeen() y la que ya llevan escrita los 9
    // PlayerPreset_*.asset existentes como "CINEMATIC_SEEN:Cinematic_EstelaAppears"). Al activar
    // modo test contra un preset con la clave "buena", HasSequencePlayed() nunca la encontraba y
    // las arañas/guerreros volvían a aparecer aunque la secuencia constara como vista.
    //
    // Estos dos helpers centralizan la convención correcta para que cualquier sequencer que
    // necesite ocultar actores de forma permanente tras su cinemática (como EstelaAppearsSequencer)
    // la use igual, sin reinventar el formato de la clave cada vez.
    protected static bool HasCinematicBeenSeen(string id)
    {
        var preset = GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;
        return preset != null && preset.flags != null && preset.flags.Contains($"CINEMATIC_SEEN:Cinematic_{id}");
    }

    protected static void MarkCinematicAsSeen(string id)
    {
        var preset = GameBootService.Profile != null ? GameBootService.Profile.GetActivePresetResolved() : null;
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new System.Collections.Generic.List<string>();
        string flag = $"CINEMATIC_SEEN:Cinematic_{id}";
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);
    }

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
