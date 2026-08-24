using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

    /// Se dispara cuando AnySequenceActive cambia (false→true al bloquear la primera cinemática
    /// encadenada, true→false al desbloquear la última). Pensado para que un controlador de UI
    /// persistente (ej. el botón global de "saltar cinemática") pueda mostrarse/ocultarse sin
    /// tener que sondear AnySequenceActive en Update() — ver GlobalCinematicSkipController.
    public static event Action<bool> OnAnySequenceActiveChanged;

    // ── Skip ("saltar cinemática") — registro de secuencias en curso ───────────
    // Lista de instancias con Co_Sequence() activo ahora mismo (normalmente una, pero puede haber
    // más de una encadenada, ver s_activeSequenceCount arriba). El botón global de skip llama a
    // RequestSkipAll() sin necesitar saber qué sequencer concreto está sonando.
    private static readonly List<CinematicSequencerBase> s_runningSequences = new();
    public static IReadOnlyList<CinematicSequencerBase> RunningSequences => s_runningSequences;

    /// Solicita saltar TODAS las cinemáticas activas ahora mismo (normalmente una sola). Pensado
    /// para el botón global de skip — no falla si no hay ninguna activa.
    public static void RequestSkipAll()
    {
        if (s_runningSequences.Count == 0) return;
        // Copia defensiva: RequestSkip() modifica s_runningSequences (se auto-elimina de la lista),
        // no se puede iterar la lista original mientras se modifica.
        var running = s_runningSequences.ToArray();
        foreach (var seq in running)
            seq.RequestSkip();
    }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsBase()
    {
        s_activeSequenceCount = 0;
        s_runningSequences.Clear();
        OnAnySequenceActiveChanged = null;
    }
#endif

    // FIX INC-052: si Co_Sequence() lanza una excepción o el objeto se destruye a mitad de la
    // cinemática (ej: cambio de escena, referencia nula puntual de un prefab concreto), el HUD y
    // el minimapa se quedaban ocultos para siempre y el ActionMode.Cinematic nunca se hacía Pop.
    // Como la cinemática de Liam invocando al gólem se ejecuta justo antes de la batalla, un fallo
    // aislado aquí dejaba el HUD desaparecido durante todo el combate. Mismo patrón de fix que
    // BossIntroPresentation.PlayIntroduction() (try/finally que garantiza la restauración).
    private bool _cinematicLocked;

    // FIX (Agosto 2026): guarda contra solapamiento. Si la señal de entrada se dispara dos veces
    // seguidas (p. ej. un bucle de reintento del grafo narrativo que reenvía la señal antes de que
    // el jugador termine el intento anterior — bug reproducido con AWAKEN_FAILED/AWAKEN_START en
    // StarAwakeningSequencer), dos Co_Sequence() concurrentes llaman ambos a LockCinematic() pero
    // EndCinematic() solo restaura una vez (por el guard de _cinematicLocked de más abajo): el
    // segundo Push de ActionMode.Cinematic se queda sin su Pop y el jugador se queda pillado en
    // modo Cinematic en vez de volver al gameplay. Ignorar la señal mientras ya hay una secuencia
    // en curso mantiene Push/Pop siempre equilibrados sin importar cuántas veces dispare el grafo.
    private bool _sequenceRunning;

    // Handle de la corrutina Co_SequenceGuarded() activa, para poder detenerla en seco desde
    // RequestSkip() sin depender de que su try/finally se ejecute (StopCoroutine no garantiza
    // Dispose() de los IEnumerator anidados vía "yield return subrutina", a diferencia de
    // StartCoroutine hijas independientes). Todo el cierre limpio al saltar corre en
    // Co_SkipToEnd(), de forma explícita.
    private Coroutine _activeSequenceCoroutine;

    // True mientras se está resolviendo un RequestSkip() para esta instancia (evita disparos dobles
    // si el jugador mantiene pulsado el botón más allá del primer frame en que se completa el hold).
    private bool _skipRequested;

    // FIX A7 (auditoría 2026-08-07): handlers activos de Co_Transition, guardados a nivel de
    // instancia para poder desuscribirlos desde OnDestroy. TransitionManager es persistente
    // (DontDestroyOnLoad); si este objeto se destruye/su corrutina se interrumpe a mitad de una
    // transición (StopAllCoroutines, cambio de escena), los handlers locales de Co_Transition
    // quedaban suscritos para siempre y podían disparar más tarde para una transición de OTRO
    // sistema, ejecutando onCutPoint (p. ej. BeginCinematic()) sobre un sequencer ya destruido.
    private UnityEngine.Events.UnityAction _activeTransitionCutHandler;
    private UnityEngine.Events.UnityAction _activeTransitionEndHandler;

    // ── Ciclo de vida Unity ───────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _signalInHandler = () =>
        {
            if (_sequenceRunning)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: señal de entrada '{_signalIn}' recibida mientras la secuencia ya está en curso — ignorada para evitar solapamiento y un Push/Pop de ActionMode.Cinematic desbalanceado.");
#endif
                return;
            }
            _activeSequenceCoroutine = StartCoroutine(Co_SequenceGuarded());
        };
        DefaultNarrativeSignals.EnsureInstance().OnCustom(_signalIn, _signalInHandler);
    }

    /// Envuelve Co_Sequence() para garantizar que el HUD/minimapa/modo Cinematic se restauran
    /// aunque la subclase termine de forma anómala (excepción o destrucción del objeto).
    private IEnumerator Co_SequenceGuarded()
    {
        _sequenceRunning = true;
        s_runningSequences.Add(this);
        try
        {
            yield return Co_Sequence();
        }
        finally
        {
            _sequenceRunning = false;
            _activeSequenceCoroutine = null;
            s_runningSequences.Remove(this);
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

        // FIX A7 (auditoría 2026-08-07): ver comentario de _activeTransitionCutHandler/_activeTransitionEndHandler.
        var tm = TransitionManager.Instance();
        if (tm != null)
        {
            if (_activeTransitionCutHandler != null) tm.onTransitionCutPointReached -= _activeTransitionCutHandler;
            if (_activeTransitionEndHandler != null) tm.onTransitionEnd -= _activeTransitionEndHandler;
        }
        _activeTransitionCutHandler = null;
        _activeTransitionEndHandler = null;
    }

    // ── Punto de entrada de la subclase ──────────────────────────────────────

    protected abstract IEnumerator Co_Sequence();

    // ── Ciclo de vida de la cinemática ────────────────────────────────────────

    /// Bloquea el input del jugador y oculta el HUD y el minimapa. Debe llamarse ANTES de la transición de entrada.
    private void LockCinematic()
    {
        _cinematicLocked = true;
        bool wasInactive = s_activeSequenceCount == 0;
        s_activeSequenceCount++;
        if (wasInactive) OnAnySequenceActiveChanged?.Invoke(true);
        ResolveActionManager()?.PushMode(ActionMode.Cinematic);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (PlayerHUDV2.Instance == null)
            Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: PlayerHUDV2.Instance es null al bloquear la cinemática — el HUD no se ocultará (fallo silencioso con ?.).");
#endif
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
        // Null-safe: secuencias que no transcurren en el mundo (ej. PrologueDreamSequencer, un
        // "sueño" fuera de cualquier localización real) no necesitan controlar la cámara de mundo
        // y no asignan _cinematicCamera en el Inspector.
        _cinematicCamera?.Activate();
    }

    /// Restaura el estado de gameplay: cancela shakes, desactiva la cámara, muestra el HUD/minimapa y desbloquea input.
    protected void EndCinematic()
    {
        if (!_cinematicLocked) return; // Ya restaurado (evita Pop/ShowHUD duplicados si se llama dos veces)
        _cinematicLocked = false;
        s_activeSequenceCount = Mathf.Max(0, s_activeSequenceCount - 1);
        if (s_activeSequenceCount == 0) OnAnySequenceActiveChanged?.Invoke(false);

        FeedbackService.CancelAllShakes();
        _cinematicCamera?.Deactivate();
        PlayerHUDV2.Instance?.ShowHUD();
        MinimapController.Instance?.SetHiddenByCinematic(false);
        // FIX INC-058: restaurar el icono del período del día al terminar la secuencia.
        TimeOfDayIndicator.Instance?.Show();
        ResolveActionManager()?.PopMode(ActionMode.Cinematic);
        if (_interiorAnchor != null) EnvironmentController.Instance?.EndCinematicOverride();
    }

    /// Devuelve _actionManager si está asignado en el Inspector; si no, lo resuelve vía
    /// ServiceLocator (y lo cachea ahí mismo para las siguientes llamadas). Esto permite que
    /// secuencias que no viven junto al jugador en la escena (ej. PrologueDreamSequencer, que no
    /// transcurre en ningún lugar del mundo) no necesiten arrastrar la referencia a mano — antes,
    /// dejarla vacía provocaba un NullReferenceException aquí mismo que abortaba la coroutine sin
    /// restaurar nada ni levantar la señal de salida: la pantalla se quedaba en negro para siempre
    /// y el grafo narrativo nunca avanzaba tras el WaitCustomEventNode correspondiente.
    private PlayerActionManager ResolveActionManager()
    {
        if (_actionManager == null)
            _actionManager = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        return _actionManager;
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
            _activeTransitionCutHandler = null;
            onCutPoint?.Invoke();
        };
        endHandler = () =>
        {
            tm.onTransitionEnd -= endHandler;
            _activeTransitionEndHandler = null;
            done = true;
        };

        // FIX A7 (auditoría 2026-08-07): guardar referencia a nivel de instancia (ver OnDestroy).
        _activeTransitionCutHandler = cutHandler;
        _activeTransitionEndHandler = endHandler;
        tm.onTransitionCutPointReached += cutHandler;
        tm.onTransitionEnd += endHandler;
        tm.Transition(settings, 0f);

        yield return new WaitUntil(() => done);
    }

    // ── Skip ("saltar cinemática") ───────────────────────────────────────────

    [Header("Skip")]
    [Tooltip("Duración del fundido a negro al saltar esta cinemática con el botón global de skip.")]
    [SerializeField] private float _skipFadeDuration = 0.25f;

    /// Solicita saltar la cinemática en curso de ESTA instancia. No hace nada si no tiene una
    /// secuencia activa ahora mismo o si ya se solicitó un skip para ella. Detiene Co_Sequence()
    /// en el punto exacto en que esté (StopCoroutine, sin depender de que el try/finally de
    /// Co_SequenceGuarded se ejecute) y cierra la cinemática de forma determinista vía
    /// Co_SkipToEnd(): limpieza propia de la subclase (OnSkipCleanup) + fundido a negro +
    /// EndCinematic()/RestoreMusic()/señal de salida — el mismo patrón que ya usan varios
    /// sequencers para su cierre normal (Co_EndCinematicStayBlack).
    public void RequestSkip()
    {
        if (!_sequenceRunning || _skipRequested) return;
        _skipRequested = true;

        if (_activeSequenceCoroutine != null)
            StopCoroutine(_activeSequenceCoroutine);
        _activeSequenceCoroutine = null;

        _sequenceRunning = false;
        s_runningSequences.Remove(this);

        StartCoroutine(Co_SkipToEnd());
    }

    private IEnumerator Co_SkipToEnd()
    {
        yield return Co_EndCinematicStayBlack(() =>
        {
            try
            {
                OnSkipCleanup();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (SkipRestoresMusic) RestoreMusic();
        }, _skipFadeDuration);

        string customSignal = SkipCompletionSignal;
        if (!string.IsNullOrEmpty(customSignal))
            RaiseSignal(customSignal);
        else
            RaiseSignalOut();

        _skipRequested = false;
    }

    /// Hook de limpieza específico de cada sequencer, llamado UNA VEZ con la pantalla ya cubierta
    /// de negro (dentro del fundido de Co_SkipToEnd), antes de EndCinematic(). Debe ser síncrono
    /// (no una corrutina): congelar/liberar NPCs, destruir overlays u otros VFX/objetos runtime,
    /// resetear Time.timeScale, parar corrutinas fire-and-forget propias (bucles, locks de
    /// rotación, pulsos...), etc. — todo lo que en el flujo normal solo se limpia al llegar al
    /// final natural de Co_Sequence() y que StopCoroutine() del punto medio NO limpia por sí solo.
    /// Cada sequencer con ese tipo de estado DEBE sobrescribir esto reutilizando su propia lógica
    /// de emergencia existente (Cleanup()/OnDestroy si ya la tiene) — por defecto no hace nada.
    protected virtual void OnSkipCleanup() { }

    /// Señal narrativa a levantar al saltar, en vez de RaiseSignalOut() (_signalOut heredado).
    /// Usar cuando el sequencer no emite _signalOut en su flujo normal (ej. StarAwakeningSequencer,
    /// que levanta señales propias de éxito/fallo) — sin este override el WaitCustomEventNode real
    /// del grafo nunca recibiría nada y el juego se quedaría bloqueado tras el skip. Vacío/null
    /// (por defecto) = usar RaiseSignalOut() normal.
    protected virtual string SkipCompletionSignal => null;

    /// Si el skip debe restaurar la música de escena (RestoreMusic()) al cerrar. Poner a false en
    /// sequencers cuyo cierre normal deliberadamente NO restaura música porque el sistema siguiente
    /// gestiona su propio crossfade (ver comentario en ReinoExitBanterSequencer.Co_Sequence) —
    /// restaurarla en el skip igualmente reintroduciría el bug ya solucionado de "dos músicas
    /// sonando a la vez".
    protected virtual bool SkipRestoresMusic => true;

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
        // INC-075-bis (08/08/2026): si GameBootService.Profile es null, esta comprobación
        // devolvía silenciosamente "false" (no vista) — indistinguible de un save real sin el
        // flag. Eso hizo perder tiempo varias veces diagnosticando "el flag no persiste" cuando
        // en realidad la causa era entrar en Play Mode directamente sobre una escena de mundo
        // (ej. MainWorld) en vez de por 'Start.unity': GameBootService.Bootstrap() SOLO busca una
        // instancia ya existente (no crea una dinámica, ver comentario en GameBootService.cs), así
        // que sin 'Start' cargado (normalmente vía Editor/AutoBootstrapOnPlay.cs) Profile se queda
        // en null y CUALQUIER sequencer que dependa de este helper deja de ocultar sus actores, sin
        // que eso implique que el guardado/carga real esté roto. El log de aviso hace explícita esa
        // diferencia para no repetir la misma investigación cada vez.
        if (GameBootService.Profile == null)
        {
            Debug.LogWarning($"[CinematicSequencerBase] HasCinematicBeenSeen('{id}'): GameBootService.Profile " +
                "es null, así que no se puede saber si esta cinemática ya se vio (se asume que no). Si esperabas " +
                "que sus actores ya estuvieran ocultos, probablemente entraste en Play Mode directamente sobre " +
                "esta escena en vez de por 'Start.unity' — revisa la consola por '[AutoBootstrapOnPlay]' o " +
                "arranca desde 'Start.unity'/el flujo normal del menú. No es un fallo del guardado/carga real.");
            return false;
        }

        var preset = GameBootService.Profile.GetActivePresetResolved();
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
        string animTrigger = null, bool loopAnim = false, string speakerName = null)
    {
        string[] pages = text.Split('\n');
        for (int i = 0; i < pages.Length; i++)
        {
            string page = pages[i].Trim();
            if (string.IsNullOrEmpty(page)) continue;
            bool done = false;
            string triggerThisPage = (i == 0 || loopAnim) ? animTrigger : null;
            SpeechBubbleUI.Instance.Show(target, page, durationPerPage, () => done = true, triggerThisPage,
                speakerName: speakerName);
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
    [Header("Debug")]
    [Tooltip("Tecla que dispara 'Simular secuencia' en Play Mode — mismo efecto que el menú contextual " +
        "del componente (click derecho en la cabecera → 'Simular secuencia'), sin tener que entrar en el " +
        "Inspector cada vez. Key.None la desactiva (valor por defecto: no interfiere con ningún " +
        "sequencer existente salvo que se asigne una tecla a mano o desde un builder de escena).")]
    [SerializeField] private Key _simulateHotkey = Key.None;

    // Si alguna subclase necesita su propio Update(), debe declararlo como "protected override void
    // Update()" y llamar a "base.Update();" para no perder el atajo — de lo contrario el suyo oculta
    // (no sobrescribe) este por ausencia de "new"/"override", y el warning del compilador (CS0114) lo
    // avisaría en cuanto se intente.
    //
    // FIX (24 ago 2026): la primera versión usaba UnityEngine.Input.GetKeyDown(KeyCode) — el proyecto
    // tiene "Active Input Handling" puesto a "Input System Package" en Player Settings (todo el resto
    // del juego ya usa UnityEngine.InputSystem, ver PlayerInputManager/HoldToSkipUI), así que la clase
    // Input legacy está desactivada y cualquier lectura suya lanza InvalidOperationException en
    // tiempo real. Corregido leyendo el teclado del nuevo Input System directamente.
    //
    // FIX (24 ago 2026, mismo día): el campo pasó de KeyCode a Key JUSTO DESPUÉS de que el builder de
    // la escena de estudio ya lo hubiera rellenado con el KeyCode.F6 antiguo (entero 287) — Unity no
    // valida un enum al deserializarlo, así que ese 287 se quedó grabado en la escena tal cual, y no
    // es un miembro real de Key. Keyboard.current[key] con un Key fuera de rango lanza
    // ArgumentOutOfRangeException en vez de simplemente "tecla no encontrada", así que sin este guard
    // el atajo rompía Update() en cada frame. _simulateHotkeyValid cachea el resultado de
    // Enum.IsDefined la primera vez (evita repetir esa comprobación, que usa reflection, en cada
    // Update() — ver regla de no-Reflection-en-runtime-frecuente de CLAUDE.md); si el valor no es
    // válido, el atajo queda desactivado con un aviso en consola en vez de reventar. El builder
    // también se autocorrige en el próximo re-run (ver SetInputKeyIfNoneOrInvalid en
    // PromoStudioSceneBuilder.cs).
    private bool? _simulateHotkeyValid;
    private bool _invalidHotkeyWarned;

    protected virtual void Update()
    {
        if (_simulateHotkey == Key.None) return;

        _simulateHotkeyValid ??= System.Enum.IsDefined(typeof(Key), _simulateHotkey);
        if (_simulateHotkeyValid != true)
        {
            if (!_invalidHotkeyWarned)
            {
                _invalidHotkeyWarned = true;
                Debug.LogWarning($"[CinematicSequencerBase] {GetType().Name}: _simulateHotkey ({(int)_simulateHotkey}) " +
                    "no es un valor válido del enum Key — probablemente arrastrado de una versión antigua del campo " +
                    "(era KeyCode). Selecciona la tecla de nuevo a mano en el Inspector, o vuelve a ejecutar el " +
                    "builder de la escena si tiene uno. Atajo desactivado mientras tanto (aviso único, no se repite).");
            }
            return;
        }

        if (Keyboard.current != null && Keyboard.current[_simulateHotkey].wasPressedThisFrame)
            SimulateSequence();
    }

    [ContextMenu("Simular secuencia")]
    protected void SimulateSequence() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_signalIn);
#endif
}
