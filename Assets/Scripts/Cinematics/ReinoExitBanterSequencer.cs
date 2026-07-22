using System.Collections;
using UnityEngine;
using Game.NPC;

/// Orquestador del banter previo a la salida del Reino:
///   0. Will se detiene; Estela y Liam siguen caminando unos pasos y se giran al notar que no les sigue.
///   1. Liam pregunta qué ocurre.
///   2. Will confiesa que tiene miedo de lo que viene.
///   3. Estela lo anima a su manera.
///   4. Liam le recuerda que no está solo.
///   5. Will da las gracias y el grupo retoma la marcha → señal de salida.
/// Señal de entrada: la misma que emite KingdomBoundaryTrigger al cruzar el límite del Reino
/// (por defecto "EVT_REINO_EXIT_BOUNDARY", heredada de CinematicSequencerBase._signalIn).
/// Señal de salida: heredada de CinematicSequencerBase._signalOut (por defecto vacía: rellenar
/// en el Inspector con "EVT_REINOEXIT_BANTER_DONE"). En el grafo, el WaitCustomEventNode que hoy
/// espera "EVT_REINO_EXIT_BOUNDARY" antes de KingdomExitTransitionNode debe pasar a esperar esta
/// señal de salida, para que el corte de cámara + logo llegue después del banter y no en paralelo.
[DisallowMultipleComponent]
public class ReinoExitBanterSequencer : CinematicSequencerBase
{
    [Header("Personajes — IDs de party (NPCBehaviourManagerV2.DialogueCharacterId)")]
    [Tooltip("DialogueCharacterId de Estela (para localizarla en PlayerParty en tiempo real).")]
    [SerializeField] private string _estelaCharacterId = "CHAR_ESTELA";

    [Tooltip("DialogueCharacterId de Liam (para localizarlo en PlayerParty en tiempo real).")]
    [SerializeField] private string _liamCharacterId = "CHAR_LIAM";

    [Header("Cámara — planos (dejar vacíos = sin corte, se queda en el plano anterior)")]
    [Tooltip("Plano general mientras el grupo camina, al entrar en la secuencia.")]
    [SerializeField] private Transform _shotWalkGroup;
    [Tooltip("Primer plano de Will — Fase 2 (confiesa su miedo)")]
    [SerializeField] private Transform _shotWillClose;
    [Tooltip("Primer plano de Estela — Fase 3 (lo anima)")]
    [SerializeField] private Transform _shotEstelaClose;
    [Tooltip("Primer plano de Liam — Fases 1 y 4 (pregunta / apoyo)")]
    [SerializeField] private Transform _shotLiamClose;

    // ── Fase 0 — Will se detiene; Estela y Liam siguen caminando y se giran ──────

    [Header("Fase 0 — Estela y Liam continúan y se giran")]
    [Tooltip("Punto al que camina Estela antes de girarse hacia Will.")]
    [SerializeField] private Transform _estelaContinueTarget;
    [Tooltip("Punto al que camina Liam antes de girarse hacia Will.")]
    [SerializeField] private Transform _liamContinueTarget;
    [SerializeField] private float _continueWalkDuration = 1.5f;
    [SerializeField] private float _continueWalkMaxDuration = 6f;

    // ── Fase 1 — Liam pregunta ───────────────────────────────────────────────────

    [Header("Fase 1 — Liam pregunta")]
    [SerializeField] private string _keyLiamPregunta = "EVT_REINOEXIT_LIAM_01";
    [SerializeField] private float _liamPreguntaDuration = 2.5f;
    [SerializeField] private string _animLiamPregunta;

    // ── Fase 2 — Will confiesa su miedo ─────────────────────────────────────────

    [Header("Fase 2 — Will confiesa su miedo (líneas separadas por '\\n' en la localización)")]
    [SerializeField] private string _keyWillMiedo = "EVT_REINOEXIT_WILL_01";
    [SerializeField] private float _willMiedoDurationPerPage = 2.5f;
    [SerializeField] private string _animWillMiedo;

    // ── Fase 3 — Estela lo anima ────────────────────────────────────────────────

    [Header("Fase 3 — Estela lo anima (líneas separadas por '\\n' en la localización)")]
    [SerializeField] private string _keyEstelaAnimo = "EVT_REINOEXIT_ESTELA_01";
    [SerializeField] private float _estelaAnimoDurationPerPage = 2.5f;
    [SerializeField] private string _animEstelaAnimo;

    // ── Fase 4 — Liam lo apoya ───────────────────────────────────────────────────

    [Header("Fase 4 — Liam lo apoya (líneas separadas por '\\n' en la localización)")]
    [SerializeField] private string _keyLiamApoyo = "EVT_REINOEXIT_LIAM_02";
    [SerializeField] private float _liamApoyoDurationPerPage = 2.2f;
    [SerializeField] private string _animLiamApoyo;

    // ── Fase 5 — Will da las gracias ────────────────────────────────────────────

    [Header("Fase 5 — Will da las gracias")]
    [SerializeField] private string _keyWillGracias = "EVT_REINOEXIT_WILL_02";
    [SerializeField] private float _willGraciasDuration = 2f;
    [SerializeField] private string _animWillGracias;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private Transform _willTransform;
    private NPCBehaviourManagerV2 _estelaManager;
    private NPCBehaviourManagerV2 _liamManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Test — iniciar sin señal")]
    private void TestStartDirect() => StartCoroutine(Co_Sequence());
#endif

    protected override IEnumerator Co_Sequence()
    {
        ResolveCharacters();

        // Sin transición asignada, esto corta de inmediato sin fundido (ver Co_Transition):
        // el banter arranca en marcha, sin interrumpir la caminata con un negro.
        yield return Co_BeginCinematicWithTransition(_shotWalkGroup);

        // Apagamos la música de gameplay al inicio. PlaySequenceMusic() se retrasa hasta
        // después de la Fase 0 (≈ _continueWalkDuration): para cuando el grupo se gira,
        // la música antigua ya ha terminado su fade y la nueva arranca limpia sin crossfade
        // contra la de gameplay. Si no hay clip de secuencia, la pausa queda en silencio
        // hasta que KingdomExitTransitionNode arranque el tema principal.
        AudioService.Instance?.StopMusic(1.5f);

        // ── Fase 0: Will se detiene; Estela y Liam siguen caminando y se giran ──
        yield return Co_GroupContinuesAndTurns();
        PlaySequenceMusic();

        // ── Fase 1: Liam pregunta ───────────────────────────────────────────────
        yield return Co_LiamPregunta();

        // ── Fase 2: Will confiesa su miedo ──────────────────────────────────────
        yield return Co_WillMiedo();

        // ── Fase 3: Estela lo anima ──────────────────────────────────────────────
        yield return Co_EstelaAnimo();

        // ── Fase 4: Liam lo apoya ─────────────────────────────────────────────────
        yield return Co_LiamApoyo();

        // ── Fase 5: Will da las gracias — cierre y señal de salida ──────────────
        yield return Co_WillGracias();

        yield return Co_EndCinematicWithTransition(() =>
        {
            // Sin RestoreMusic() aquí: KingdomExitTransitionNode corta la música un
            // instante después con StopMusic(). Si esta secuencia primero restaura la
            // música de escena (crossfade propio) y acto seguido el otro nodo la para,
            // los dos crossfades compiten por las mismas fuentes de AudioService y la
            // pista original queda huérfana a medio fundido en vez de llegar a silencio
            // — eso es lo que sonaba como "las dos músicas a la vez".
            RaiseSignalOut();
        });
    }

    private void ResolveCharacters()
    {
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            _willTransform = playerGo.transform;

        _estelaManager = FindPartyMember(_estelaCharacterId);
        _liamManager   = FindPartyMember(_liamCharacterId);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_willTransform == null) Debug.LogWarning("[ReinoExitBanterSequencer] No se encontró al jugador (Will).");
        if (_estelaManager == null) Debug.LogWarning($"[ReinoExitBanterSequencer] No se encontró a Estela (id='{_estelaCharacterId}') en PlayerParty.");
        if (_liamManager   == null) Debug.LogWarning($"[ReinoExitBanterSequencer] No se encontró a Liam (id='{_liamCharacterId}') en PlayerParty.");
#endif
    }

    private static NPCBehaviourManagerV2 FindPartyMember(string dialogueCharacterId)
    {
        if (string.IsNullOrEmpty(dialogueCharacterId) || !Game.NPC.PlayerParty.HasInstance)
            return null;

        foreach (var member in Game.NPC.PlayerParty.Instance.Members)
        {
            var mgr = member?.NPCManager;
            if (mgr != null && mgr.DialogueCharacterId == dialogueCharacterId)
                return mgr;
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 0 — Will se detiene; Estela y Liam siguen caminando y se giran
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_GroupContinuesAndTurns()
    {
        // Will (el jugador) ya está bloqueado por LockCinematic() dentro de
        // Co_BeginCinematicWithTransition: se para en seco donde estaba.
        bool estelaDone = _estelaManager == null || _estelaContinueTarget == null;
        bool liamDone   = _liamManager   == null || _liamContinueTarget   == null;

        if (!estelaDone)
            _estelaManager.MoveToPosition(_estelaContinueTarget.position, _continueWalkDuration,
                _continueWalkMaxDuration, turn: true, onComplete: () => estelaDone = true);

        if (!liamDone)
            _liamManager.MoveToPosition(_liamContinueTarget.position, _continueWalkDuration,
                _continueWalkMaxDuration, turn: true, onComplete: () => liamDone = true);

        yield return new WaitUntil(() => estelaDone && liamDone);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 1 — Liam pregunta
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_LiamPregunta()
    {
        if (_shotLiamClose != null) _cinematicCamera.Cut(_shotLiamClose);
        yield return ShowBubblePaged(_liamManager?.transform, Loc(_keyLiamPregunta),
            _liamPreguntaDuration, _animLiamPregunta, loopAnim: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 2 — Will confiesa su miedo
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WillMiedo()
    {
        if (_shotWillClose != null) _cinematicCamera.Cut(_shotWillClose);
        yield return ShowBubblePaged(_willTransform, Loc(_keyWillMiedo),
            _willMiedoDurationPerPage, _animWillMiedo, loopAnim: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 3 — Estela lo anima
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_EstelaAnimo()
    {
        if (_shotEstelaClose != null) _cinematicCamera.Cut(_shotEstelaClose);
        yield return ShowBubblePaged(_estelaManager?.transform, Loc(_keyEstelaAnimo),
            _estelaAnimoDurationPerPage, _animEstelaAnimo, loopAnim: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 4 — Liam lo apoya
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_LiamApoyo()
    {
        if (_shotLiamClose != null) _cinematicCamera.Cut(_shotLiamClose);
        yield return ShowBubblePaged(_liamManager?.transform, Loc(_keyLiamApoyo),
            _liamApoyoDurationPerPage, _animLiamApoyo, loopAnim: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 5 — Will da las gracias
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WillGracias()
    {
        if (_shotWillClose != null) _cinematicCamera.Cut(_shotWillClose);
        yield return ShowBubblePaged(_willTransform, Loc(_keyWillGracias),
            _willGraciasDuration, _animWillGracias, loopAnim: true);
    }
}
