using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC;
using Sendero.Core.Feedback;

/// Orquestador del banter previo a la salida del Reino:
///   0. Bajo un corte a negro, el grupo se recoloca de golpe en su posición DE PARTIDA (marcha
///      normal, los tres juntos) — nunca en la posición real que tuvieran al cruzar el límite del
///      Reino. Con la pantalla ya visible, Will se detiene ahí mismo mientras Estela y Liam
///      adelantan caminando unos pasos y se giran al notar que no les sigue, como si volvieran a
///      ver qué le pasa. BUGFIX (Agosto 2026): antes arrancaban esa caminata desde la posición
///      real de cada uno en el momento del disparo, y se veía mal si el personaje activo al
///      cruzar el límite no era Will (ver comentario largo en ResolveCharacters()). Al partir
///      siempre de la misma marca fija bajo negro, la caminata sale igual de bien sin importar
///      quién cruzara el límite.
///   1. Liam pregunta qué ocurre.
///   2. Will confiesa que tiene miedo de lo que viene.
///   3. Estela lo anima a su manera.
///   4. Liam le recuerda que no está solo.
///   5. Will da las gracias y el grupo retoma la marcha → señal de salida.
/// Señal de entrada: "EVT_REINOEXIT_BANTER_START" (heredada de CinematicSequencerBase._signalIn).
/// OJO: NO es la misma que "EVT_REINO_EXIT_BOUNDARY" que emite KingdomBoundaryTrigger al cruzar
/// el límite físico del Reino. Ese evento crudo lo consume primero el WaitCustomEventNode del
/// grafo (solo llega a él tras iniciar ELDRAN_MISSION14); el propio grafo re-emite entonces
/// "EVT_REINOEXIT_BANTER_START" mediante un RaiseCustomEventNode. Si este sequencer escuchara
/// directamente "EVT_REINO_EXIT_BOUNDARY" (como ocurría antes), reaccionaría al cruce físico
/// crudo aunque el grafo aún no hubiera llegado a ese punto (p.ej. cruzando el límite antes de
/// tiempo en testeo), porque CinematicSequencerBase.Awake() se suscribe sin comprobar el estado
/// narrativo. Mantener esta separación de claves es lo que garantiza que el banter solo se
/// dispare cuando el grafo realmente está en ese punto.
/// Señal de salida: heredada de CinematicSequencerBase._signalOut ("EVT_REINOEXIT_BANTER_DONE").
/// El WaitCustomEventNode antes de KingdomExitTransitionNode espera esta señal de salida, para
/// que el corte de cámara + logo llegue después del banter y no en paralelo.
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

    [Header("Decoración — pétalos al iniciar")]
    [Tooltip("Prefabs de pétalos a esparcir al arrancar la secuencia (mismos assets que decoran el Reino en MainWorld: Assets/Art/World/Fantasy_Kingdom_Pack/Particle/Petal01_P y Petal02_P). Se reproducen vía VfxPoolService, nunca con Instantiate/Destroy directo (ver CLAUDE.md §2). Vacío = sin decoración.")]
    [SerializeField] private GameObject[] _petalDecorPrefabs;
    [Tooltip("Puntos manuales donde aparecen los pétalos (uno por prefab/punto). Si se asigna, tiene prioridad sobre el reparto automático por pantalla de abajo. Dejar vacío para el efecto \"viento por toda la pantalla\".")]
    [SerializeField] private Transform[] _petalSpawnPoints;
    [Tooltip("Duración de cada instancia de pétalos. Con margen de sobra sobre la duración típica del banter (los diálogos paginados pueden alargarse según el idioma); si en playtest la secuencia dura más, subir este valor.")]
    [SerializeField] private float _petalsLifetime = 45f;

    [Header("Decoración — reparto por pantalla (si no hay _petalSpawnPoints)")]
    [Tooltip("Cuántas instancias de pétalos se reparten por delante de la cámara para cubrir toda la pantalla.")]
    [SerializeField] private int _petalScreenCount = 24;
    [Tooltip("Ancho x alto (en unidades de mundo) del área frente a la cámara donde se reparten los pétalos. Súbelo si quedan huecos sin cubrir en los bordes de pantalla.")]
    [SerializeField] private Vector2 _petalScreenSpread = new Vector2(16f, 9f);
    [Tooltip("Distancia mínima/máxima delante de la cámara a la que aparecen (varias profundidades = sensación de volumen, no un plano plano).")]
    [SerializeField] private Vector2 _petalScreenDepthRange = new Vector2(4f, 14f);

    [Header("Decoración — viento (rollo Pocahontas)")]
    [Tooltip("Dirección del viento en espacio de CÁMARA: x = hacia la derecha de pantalla (negativo = izquierda), y = hacia arriba de pantalla, z = alejándose de cámara. Se normaliza sola, solo importa la proporción entre ejes.")]
    [SerializeField] private Vector3 _petalWindDirectionCameraSpace = new Vector3(-1f, 0.2f, 0f);
    [Tooltip("Velocidad del viento en unidades/seg. ApplyWind() anula la gravedad y el impulso inicial aleatorio del prefab para que esta dirección se vea claramente, así que puede quedar más lento de lo que parece necesario.")]
    [SerializeField] private float _petalWindSpeed = 3.5f;

    // ── Fase 0 — marca de partida bajo blackout + caminata visible ──────────────

    [Header("Fase 0a — marcas de PARTIDA (Warp bajo blackout, ver RepositionCharactersForBanter)")]
    [Tooltip("BUGFIX (Agosto 2026): antes de recolocarlos aquí, Estela y Liam arrancaban su " +
             "caminata visible desde su posición REAL en el momento del disparo. Se veía bien " +
             "cuando Will era el personaje activo al cruzar el límite del Reino (esa posición " +
             "real era razonable — iban justo detrás), pero mal si el activo era Estela o Liam: " +
             "con ActiveCharacterSwapper, el personaje NO activo pasa a ser un NPC con su propia " +
             "IA (puede estar en cualquier sitio) y el que SÍ está activo deja su NPC real oculto " +
             "y congelado donde estaba al tomar el control. Ahora, mientras la pantalla está en " +
             "negro, los tres se colocan de un salto (Warp) en estas marcas de PARTIDA — pensadas " +
             "como \"marcha normal, los tres juntos\" — y solo entonces, ya con la pantalla " +
             "visible, Estela y Liam caminan de verdad hasta _estelaContinueTarget/" +
             "_liamContinueTarget y se giran. Da igual dónde estuviera nadie de verdad: la " +
             "caminata siempre arranca desde el mismo sitio.")]
    [SerializeField] private Transform _estelaWalkStartMark;
    [Tooltip("Igual que _estelaWalkStartMark pero para Liam.")]
    [SerializeField] private Transform _liamWalkStartMark;
    [Tooltip("Marca donde queda Will nada más revelarse la cinemática: se detiene ahí y no se " +
             "mueve más en toda la Fase 0 (es Estela y Liam quienes caminan y se giran a mirarlo). " +
             "Se aplica de forma instantánea (Warp) junto con las dos anteriores, en negro.")]
    [SerializeField] private Transform _willMark;

    [Header("Fase 0b — Estela y Liam adelantan a Will y se giran (caminata visible en pantalla)")]
    [Tooltip("Punto al que camina Estela, ya con la pantalla visible, antes de girarse hacia Will " +
             "(más adelantada que él). Solo importa la POSICIÓN: la rotación de esta marca no se " +
             "usa — al llegar, Estela gira 180° respecto a la dirección en la que caminaba (igual " +
             "que el turn:true de un NPC normal, ver MoveToPositionSequence.ApplyFallbackOrientation), " +
             "así que colócala pensando en el tramo recto que va a recorrer, no en hacia dónde " +
             "apunta la flecha del Transform.")]
    [SerializeField] private Transform _estelaContinueTarget;
    [Tooltip("Igual que _estelaContinueTarget pero para Liam.")]
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
    private Transform _estelaTransform;
    private Transform _liamTransform;
    private NPCBehaviourManagerV2 _estelaManager;
    private NPCBehaviourManagerV2 _liamManager;

    // Handles de Co_AdvanceAndTurn (ver Co_GroupAdvancesAndTurns) — se lanzan con StartCoroutine
    // suelto, no con yield directo, así que RequestSkip()/StopCoroutine(_activeSequenceCoroutine)
    // NO las toca. Se guardan aquí para poder pararlas explícitamente desde OnSkipCleanup().
    private Coroutine _estelaAdvanceCoroutine;
    private Coroutine _liamAdvanceCoroutine;

    [Tooltip("Duración del fundido que retira el negro tras saltar la secuencia con el botón global de skip (ver OnSkipCleanup).")]
    [SerializeField] private float _skipRevealDuration = 0.25f;

    // Mismos parámetros de Animator que usa MountainSequencer para mover manualmente al
    // controller del jugador durante una cinemática (ver Co_AdvanceAndTurn): "Free Locomotion"
    // es el estado del blend tree de caminar/correr, "InputMagnitude" es lo que lo alimenta.
    private static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    private static readonly int HashLocomotion     = Animator.StringToHash("Free Locomotion");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Test — iniciar sin señal")]
    private void TestStartDirect() => StartCoroutine(Co_Sequence());
#endif

    protected override IEnumerator Co_Sequence()
    {
        ResolveCharacters();

        // additionalOnCut agrupa dos cosas que deben ocurrir en el MISMO instante en que la
        // pantalla queda cubierta (nunca antes, nunca después de revelar):
        //   - RepositionCharactersForBanter(): Warp de Will/Estela/Liam a sus marcas de PARTIDA
        //     de Fase 0. Si no hay _entryTransition asignada, Co_Transition llama a esto
        //     igualmente sin animación (corte seco) — el grupo sigue recolocándose bajo cubierto,
        //     solo que sin fundido; nunca se ve el salto.
        //   - SpawnPetalDecor(): igual que antes, para que los pétalos nazcan ya con la cámara en
        //     el plano del grupo, no antes ni después del reveal.
        yield return Co_BeginCinematicWithTransition(_shotWalkGroup, () =>
        {
            RepositionCharactersForBanter();
            SpawnPetalDecor();
        });

        // Apagamos la música de gameplay al inicio. PlaySequenceMusic() se retrasa hasta
        // después de la Fase 0 (≈ _continueWalkDuration): para cuando el grupo termina de
        // adelantar a Will y girarse, la música antigua ya ha terminado su fade y la nueva
        // arranca limpia sin crossfade contra la de gameplay. Si no hay clip de secuencia, la
        // pausa queda en silencio hasta que KingdomExitTransitionNode arranque el tema principal.
        AudioService.Instance?.StopMusic(1.5f);

        // ── Fase 0: Will ya está parado (recolocado bajo negro); Estela y Liam caminan de ──
        // verdad, ya en pantalla, adelantándolo y girándose al notar que no les sigue.
        yield return Co_GroupAdvancesAndTurns();

        // BUGFIX (Agosto 2026): Will se quedaba mirando hacia donde apuntara _willMark en la
        // escena (a menudo de perfil o dándoles la espalda a Estela y Liam), porque WarpActor
        // solo copia la rotación de esa marca sin tener en cuenta hacia dónde han quedado los
        // otros dos tras Co_GroupAdvancesAndTurns(). En vez de depender de ajustar a mano la
        // rotación de _willMark en el editor (frágil: cualquier retoque de las marcas de Fase 0b
        // la desincroniza), Will gira aquí hacia el punto medio de Estela y Liam nada más
        // terminar ellos de adelantarlo y girarse — mismo FaceTarget que usan el resto de
        // sequencers para mirar a un personaje concreto.
        FaceWillTowardsGroup();

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

    // FIX (16 ago 2026 — auditoría de skip en todas las cinemáticas, misma causa que
    // TabernaSequencer): el cierre normal usa Co_EndCinematicWithTransition (revela solo); el
    // cierre genérico de skip (Co_SkipToEnd -> Co_EndCinematicStayBlack) NO revela — sin este
    // override la pantalla se queda en negro para siempre tras saltar esta secuencia.
    //
    // Además, ver el comentario "Sin RestoreMusic()" en Co_Sequence(): el cierre normal
    // deliberadamente NO restaura la música de escena porque KingdomExitTransitionNode la corta
    // un instante después — restaurarla aquí en el skip reintroduciría exactamente el bug de
    // "las dos músicas a la vez" que ese comentario documenta.
    protected override bool SkipRestoresMusic => false;

    protected override void OnSkipCleanup()
    {
        // Co_AdvanceAndTurn se lanza con StartCoroutine suelto (no yield), así que el
        // StopCoroutine(_activeSequenceCoroutine) del cierre genérico no las para: si el skip
        // llega a mitad de la Fase 0, seguirían moviendo a Estela/Liam (o desactivando el
        // CharacterController del propio jugador, si es el personaje activo) varios frames
        // después de que EndCinematic() ya haya devuelto el control — el jugador podía quedarse
        // sin poder moverse (CharacterController.enabled == false para siempre).
        if (_estelaAdvanceCoroutine != null) { StopCoroutine(_estelaAdvanceCoroutine); _estelaAdvanceCoroutine = null; }
        if (_liamAdvanceCoroutine   != null) { StopCoroutine(_liamAdvanceCoroutine);   _liamAdvanceCoroutine   = null; }

        RestoreCharacterControllerIfDisabled(_estelaTransform);
        RestoreCharacterControllerIfDisabled(_liamTransform);
        RestoreCharacterControllerIfDisabled(_willTransform);

        StartCoroutine(Co_RevealAfterSkip());
    }

    private static void RestoreCharacterControllerIfDisabled(Transform actor)
    {
        if (actor == null) return;
        var cc = actor.GetComponent<CharacterController>();
        if (cc != null && !cc.enabled) cc.enabled = true;

        // FIX (16 ago 2026 — auditoría de skip): Co_AdvanceAndTurn(), cuando `actor` es el
        // controller real del jugador (sin NPCBehaviourManagerV2, ver el comentario de esa
        // función), alimenta el Animator con InputMagnitude=1 sobre "Free Locomotion" cada frame
        // mientras dura el avance manual, y solo lo resetea a 0 al llegar a destino. Si el skip
        // corta ese bucle a mitad (StopCoroutine de _estelaAdvanceCoroutine/_liamAdvanceCoroutine,
        // arriba), el CharacterController se reactiva pero el Animator se queda alimentando el
        // ciclo de caminar indefinidamente — el personaje sigue "andando en el sitio" tras
        // recuperar el control. Mismo patrón que MountainSequencer.FinishWillFlee(), que ya
        // resetea los dos a la vez. SetFloat con un hash que no existe en el Animator del actor
        // (p. ej. un NPC que sí llegó a moverse por el camino NPCBehaviourManagerV2) es un no-op
        // seguro.
        var animator = actor.GetComponent<Animator>();
        animator?.SetFloat(HashInputMagnitude, 0f);
    }

    private IEnumerator Co_RevealAfterSkip()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, _skipRevealDuration, fadeIn: false);
    }

    /// <summary>
    /// Resuelve quién representa AHORA MISMO a cada uno de los tres, teniendo en cuenta que el
    /// jugador puede haber cruzado el límite del Reino controlando a Will, a Estela o a Liam
    /// (ver ActiveCharacterSwapper/PartyControlManager).
    ///
    /// BUGFIX (Agosto 2026), causa raíz del bug reportado ("con Will se ve bien, con Estela mal"):
    /// esta función asumía siempre "Will = PlayerService.Player" y "Estela/Liam = FindPartyMember()
    /// en PlayerParty". Pero PlayerService.Player NO es fijo: es el único GameObject con tag
    /// Player, y ActiveCharacterSwapper hace que ese mismo GameObject visualmente "sea" Will,
    /// Estela o Liam según cuál esté activo. Cuando el activo es Estela o Liam, su
    /// NPCPartyMember real (el que encuentra FindPartyMember) se queda oculto e inmóvil justo
    /// donde estaba al tomar el control — no es lo que se ve en pantalla — y Will pasa a ser un
    /// NPC aparte con su propia IA (ActiveCharacterSwapper.WillNpcInstance), no el jugador.
    /// Ahora se resuelve el slot activo (PartyControlManager.ActiveSlot) y, para ESE personaje,
    /// se usa el controller; para los otros dos, su NPC real como antes (o WillNpcInstance para
    /// Will cuando no es el activo).
    /// </summary>
    private void ResolveCharacters()
    {
        var activeSlot = PartyControlManager.Instance != null
            ? PartyControlManager.Instance.ActiveSlot
            : PartyControlManager.CharacterSlot.Will;

        Transform controllerTransform = null;
        if (PlayerService.TryGetPlayer(out var playerGo, allowSceneLookup: true) && playerGo != null)
            controllerTransform = playerGo.transform;

        _willTransform = activeSlot == PartyControlManager.CharacterSlot.Will
            ? controllerTransform
            : ActiveCharacterSwapper.Instance?.WillNpcInstance?.transform;

        _estelaManager = FindPartyMember(_estelaCharacterId);
        _estelaTransform = activeSlot == PartyControlManager.CharacterSlot.Estela
            ? controllerTransform
            : _estelaManager?.transform;

        _liamManager = FindPartyMember(_liamCharacterId);
        _liamTransform = activeSlot == PartyControlManager.CharacterSlot.Liam
            ? controllerTransform
            : _liamManager?.transform;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_willTransform == null) Debug.LogWarning("[ReinoExitBanterSequencer] No se pudo resolver a Will (ni el controller activo ni ActiveCharacterSwapper.WillNpcInstance).");
        if (_estelaTransform == null) Debug.LogWarning($"[ReinoExitBanterSequencer] No se pudo resolver a Estela (id='{_estelaCharacterId}').");
        if (_liamTransform == null) Debug.LogWarning($"[ReinoExitBanterSequencer] No se pudo resolver a Liam (id='{_liamCharacterId}').");
#endif
    }

    /// <summary>
    /// Esparce pétalos ambientales al arrancar la secuencia (decoración, no gameplay). Un único
    /// disparo por punto configurado vía VfxPoolService — nunca Instantiate/Destroy directo, es el
    /// mismo patrón que un impacto o una explosión, solo que con una vida más larga (CLAUDE.md §2).
    /// Sin _petalSpawnPoints manuales, reparte los pétalos por delante de la cámara activa (varias
    /// profundidades para dar volumen) y les añade una deriva de viento constante en espacio de
    /// cámara, para el efecto "viento cruzando toda la pantalla" (rollo Pocahontas).
    /// </summary>
    private void SpawnPetalDecor()
    {
        if (_petalDecorPrefabs == null || _petalDecorPrefabs.Length == 0) return;
        if (VfxPoolService.Instance == null) return;

        // Viento suave de ambiente acompañando la decoración de pétalos — sutil, no un efecto
        // puntual como una explosión, solo para que la escena no quede completamente muda.
        AudioService.Instance?.PlaySFX("ReinoExit_PetalAmbience", 0.35f);

        if (_petalSpawnPoints != null && _petalSpawnPoints.Length > 0)
        {
            for (int i = 0; i < _petalSpawnPoints.Length; i++)
            {
                Transform point = _petalSpawnPoints[i];
                if (point == null) continue;

                GameObject prefab = _petalDecorPrefabs[i % _petalDecorPrefabs.Length];
                VfxPoolService.Instance.Play(prefab, point.position, point.rotation, _petalsLifetime);
            }
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Transform camT = cam.transform;
        Vector3 windVelocityWorld = (camT.right   * _petalWindDirectionCameraSpace.x
                                    + camT.up      * _petalWindDirectionCameraSpace.y
                                    + camT.forward * _petalWindDirectionCameraSpace.z)
                                    .normalized * _petalWindSpeed;

        for (int i = 0; i < _petalScreenCount; i++)
        {
            GameObject prefab = _petalDecorPrefabs[i % _petalDecorPrefabs.Length];

            float depth = Random.Range(_petalScreenDepthRange.x, _petalScreenDepthRange.y);
            float xOff  = Random.Range(-0.5f, 0.5f) * _petalScreenSpread.x;
            float yOff  = Random.Range(-0.5f, 0.5f) * _petalScreenSpread.y;

            Vector3 pos = camT.position + camT.forward * depth + camT.right * xOff + camT.up * yOff;

            // OJO: usar prefab.transform.rotation, no Quaternion.identity. El prefab trae una
            // rotación local de -90° en X (igual que las decoraciones estáticas del Reino en
            // MainWorld, ver Petal01_P/02_P) que orienta el ShapeModule del ParticleSystem; con
            // identity el volumen de emisión queda mal orientado y los pétalos pueden no llegar
            // a verse.
            Transform instance = VfxPoolService.Instance.Play(prefab, pos, prefab.transform.rotation, _petalsLifetime);
            ApplyWind(instance, windVelocityWorld);
        }
    }

    /// Añade una deriva constante en espacio de mundo (velocityOverLifetime) por encima del
    /// revoloteo aleatorio propio del ParticleSystem, para simular una ráfaga de viento sostenida.
    /// También anula la gravedad y el impulso inicial omnidireccional del prefab (InitialModule:
    /// gravityModifier y startSpeed con randomDirectionAmount=1): sin esto, esos dos siguen tirando
    /// de cada pétalo hacia abajo/en cualquier dirección y acaban ganándole visualmente al viento.
    private static void ApplyWind(Transform instance, Vector3 windVelocityWorld)
    {
        if (instance == null) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var ps = systems[i];

            var main = ps.main;
            main.gravityModifier = 0f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(windVelocityWorld.x);
            vel.y = new ParticleSystem.MinMaxCurve(windVelocityWorld.y);
            vel.z = new ParticleSystem.MinMaxCurve(windVelocityWorld.z);
        }
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
    // Fase 0 — marca de partida bajo blackout + caminata visible
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Recoloca a los tres en sus marcas de PARTIDA de un salto (Warp), nunca caminando. Se llama
    /// desde el additionalOnCut de Co_BeginCinematicWithTransition, con la pantalla cubierta — el
    /// warp nunca se ve. Will va directo a su marca final (_willMark: se detiene ahí y no se
    /// mueve más en Fase 0); Estela y Liam van a su marca de PARTIDA (_estelaWalkStartMark/
    /// _liamWalkStartMark, no a su destino) porque son ellos quienes deben caminar EN PANTALLA
    /// adelantando a Will y girándose — ver Co_GroupAdvancesAndTurns(). Ver el comentario largo de
    /// ResolveCharacters() para la causa raíz que esto arregla: al no depender de dónde estuviera
    /// nadie de verdad en el momento del disparo, la caminata visible arranca siempre desde el
    /// mismo sitio, sin importar qué personaje cruzara el límite del Reino.
    /// </summary>
    private void RepositionCharactersForBanter()
    {
        WarpActor(_willTransform, _willMark);
        WarpActor(_estelaTransform, _estelaWalkStartMark);
        WarpActor(_liamTransform, _liamWalkStartMark);
    }

    /// Mueve `actor` a la posición/rotación de `mark` sin caminar ni un frame de por medio.
    /// Usa NavMeshAgent.Warp para los NPCs normales (Estela/Liam en su forma habitual, o el
    /// clon de Will cuando no es el personaje activo) para no desincronizar su pathfinding; para
    /// el controller del jugador (CharacterController, sin NavMeshAgent — es él cuando ES el
    /// personaje activo) desactiva el CharacterController un frame, que es lo único que permite
    /// mover su transform directamente (mismo patrón que ActiveCharacterSwapper.TeleportPlayer()).
    private static void WarpActor(Transform actor, Transform mark)
    {
        if (actor == null || mark == null) return;

        var agent = actor.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(mark.position);
            actor.rotation = mark.rotation;
            return;
        }

        var cc = actor.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        actor.SetPositionAndRotation(mark.position, mark.rotation);
        if (cc != null) cc.enabled = true;
    }

    /// <summary>
    /// Hace que Estela y Liam caminen EN PANTALLA desde su marca de partida hasta adelantar a
    /// Will, y se giren a mirarlo — el gag visual de "se dan cuenta de que no les sigue y vuelven
    /// a comprobar qué le pasa". Corre ambos en paralelo (StartCoroutine, no yield directo) igual
    /// que hacía el viejo Co_GroupContinuesAndTurns, y espera a que los dos terminen.
    /// </summary>
    private IEnumerator Co_GroupAdvancesAndTurns()
    {
        bool estelaDone = _estelaTransform == null || _estelaContinueTarget == null;
        bool liamDone   = _liamTransform   == null || _liamContinueTarget   == null;

        if (!estelaDone)
            _estelaAdvanceCoroutine = StartCoroutine(Co_AdvanceAndTurn(_estelaTransform, _estelaContinueTarget, () => estelaDone = true));
        if (!liamDone)
            _liamAdvanceCoroutine = StartCoroutine(Co_AdvanceAndTurn(_liamTransform, _liamContinueTarget, () => liamDone = true));

        yield return new WaitUntil(() => estelaDone && liamDone);
    }

    /// <summary>
    /// Hace avanzar a `actor` hasta la posición de `target` y girarlo 180° al llegar (respecto a
    /// hacia dónde caminaba, no hacia una rotación concreta) — adelantar a Will y girarse a
    /// comprobarlo. Dos caminos según qué sea `actor` ahora mismo (ver ResolveCharacters()):
    ///   - NPC normal (tiene NPCBehaviourManagerV2 — el caso habitual, tanto si es Estela/Liam
    ///     siguiendo como IA como si es el clon de Will): usa su propio sistema de movimiento
    ///     cinemático (FSM + NavMeshAgent, turn:true), que ya anima el paso, resuelve colisiones
    ///     con el terreno y hace el giro de 180° al llegar — es el mismo MoveToPosition que usaba
    ///     el viejo Co_GroupContinuesAndTurns.
    ///   - Controller del jugador (CharacterController, SIN NPCBehaviourManagerV2 — pasa cuando
    ///     este personaje concreto es el activo, ver ActiveCharacterSwapper): no hay FSM de NPC ni
    ///     NavMeshAgent que lo mueva ni le anime el paso. BUGFIX (reportado tras el primer pase:
    ///     "se ve en pose T"): mover solo el transform no basta, porque nada le dice al Animator
    ///     que está caminando y se queda en su pose por defecto. Mismo patrón que
    ///     MountainSequencer.Co_GroupFlees()/FinishWillFlee() para este mismo caso (Will corriendo
    ///     durante la huida): desactivar el CharacterController, CrossFade al estado "Free
    ///     Locomotion" del Animator y alimentar "InputMagnitude" cada frame mientras se mueve el
    ///     transform con MoveTowards (rotando hacia la dirección de avance, para no caminar de
    ///     espaldas) — así el blend tree de locomoción anima el paso de verdad. Al llegar, mismo
    ///     giro de 180° instantáneo que hace el NPC (ver
    ///     MoveToPositionSequence.ApplyFallbackOrientation), para que el gesto se lea igual sin
    ///     importar por qué camino se resolvió `actor`.
    /// </summary>
    private IEnumerator Co_AdvanceAndTurn(Transform actor, Transform target, System.Action onComplete)
    {
        var npcManager = actor.GetComponent<NPCBehaviourManagerV2>();
        if (npcManager != null)
        {
            bool npcDone = false;
            npcManager.MoveToPosition(target.position, _continueWalkDuration, _continueWalkMaxDuration,
                turn: true, onComplete: () => npcDone = true);
            yield return new WaitUntil(() => npcDone);
            onComplete?.Invoke();
            yield break;
        }

        var cc = actor.GetComponent<CharacterController>();
        var animator = actor.GetComponent<Animator>();

        if (cc != null) cc.enabled = false;
        if (animator != null && animator.HasState(0, HashLocomotion))
            animator.CrossFade(HashLocomotion, 0.15f);

        Vector3 destination = target.position;
        float speed = Vector3.Distance(actor.position, destination) / Mathf.Max(_continueWalkDuration, 0.01f);

        while ((destination - actor.position).sqrMagnitude > 0.0025f)
        {
            Vector3 toTarget = destination - actor.position;
            Vector3 flat = toTarget; flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
                actor.rotation = Quaternion.LookRotation(flat);

            actor.position = Vector3.MoveTowards(actor.position, destination, speed * Time.deltaTime);
            animator?.SetFloat(HashInputMagnitude, 1f);
            yield return null;
        }
        actor.position = destination;
        animator?.SetFloat(HashInputMagnitude, 0f);

        // Giro instantáneo de 180° respecto a la dirección de llegada — mismo lenguaje visual que
        // el turn:true del camino NPC (ver comentario de arriba), no una rotación exacta hacia
        // `target` (su rotación como Transform no se usa, solo su posición).
        actor.rotation *= Quaternion.Euler(0f, 180f, 0f);

        if (cc != null) cc.enabled = true;
        onComplete?.Invoke();
    }

    /// <summary>
    /// Gira a Will (instantáneo, sin animación) hacia el punto medio entre Estela y Liam una vez
    /// que ambos han terminado de adelantarlo y girarse en Co_GroupAdvancesAndTurns(). Ver el
    /// comentario BUGFIX en Co_Sequence() para la causa raíz: la rotación de _willMark por sí
    /// sola no garantiza que Will quede mirando al grupo.
    /// </summary>
    private void FaceWillTowardsGroup()
    {
        if (_willTransform == null) return;

        int count = 0;
        Vector3 groupCenter = Vector3.zero;
        if (_estelaTransform != null) { groupCenter += _estelaTransform.position; count++; }
        if (_liamTransform != null)   { groupCenter += _liamTransform.position;   count++; }
        if (count == 0) return;

        FaceTarget(_willTransform, groupCenter / count);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 1 — Liam pregunta
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_LiamPregunta()
    {
        if (_shotLiamClose != null) _cinematicCamera.Cut(_shotLiamClose);
        yield return ShowBubblePaged(_liamTransform, Loc(_keyLiamPregunta),
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
        yield return ShowBubblePaged(_estelaTransform, Loc(_keyEstelaAnimo),
            _estelaAnimoDurationPerPage, _animEstelaAnimo, loopAnim: true);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fase 4 — Liam lo apoya
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_LiamApoyo()
    {
        if (_shotLiamClose != null) _cinematicCamera.Cut(_shotLiamClose);
        yield return ShowBubblePaged(_liamTransform, Loc(_keyLiamApoyo),
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
