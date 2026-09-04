using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Sendero.Core.Feedback;
using Sendero.UI;
using Invector.vCharacterController;

/// <summary>
/// Orquestador de la escena 20 del GDD — "El Mago Oscuro y la Verdad" (la Batalla Final Épica).
/// Guion técnico completo (Materiales/Actores/Fases): guion-tecnico-batalla-final-2026-08-30.md
/// en el proyecto de Cowork "El Sendero de las Estrellas". Sigue el mismo patrón "obra de
/// teatro" que el resto de sequencers del proyecto (ver TDD.md §10) y el patrón de código de
/// PrologueDreamSequencer.cs, que ya anima a este mismo prefab (_MAGO_OSCURO.prefab).
///
/// Alcance deliberado de esta pasada (decidido por Raúl, 30/08/2026): solo la batalla final y el
/// desenlace (escenas 20-22). Las pruebas del Sendero, la Ruptura y la Reunión (17-19) quedan
/// fuera a propósito — este sequencer asume que Will, Estela y Liam YA están reunidos con
/// Sinergia Total concedida cuando arranca (probarlo vía un preset de testing que arranque
/// directo en el altar, ver TDD §9, en vez de jugar 17-19 primero).
///
/// Fases (ver guion técnico para el desglose completo):
///   A. Llegada al altar + aparición del Mago Oscuro (cinemática)
///   B. El monólogo (texto literal ya en GDD.md líneas 238-252)
///   C. Revelación — Will recupera su potencial (cinemática, cambio de aura)
///   D. Combate cooperativo real (GAMEPLAY — no es responsabilidad de este script; lo gestiona
///      NPCCombatConfig sobre el propio _MAGO_OSCURO.prefab vía BossArenaController). Este
///      sequencer solo espera a que la vida del jefe cruce el umbral de la Fase 2.
///   E. Trigger cataclísmico (cinemática corta + freeze de Time.timeScale)
///   F. Tutorial in-game del Hechizo Prohibido del Tiempo
///   G. Rebobinado + reposicionamiento + contrahechizo crítico — TODO ESTO ES CINEMÁTICO
///      (decisión explícita de Raúl, 30/08/2026: "lo hacemos durante la cinematica" — no hay
///      control de movimiento real durante el rebobinado, solo una ventana corta de un botón).
///   H. Traición del Mago Oscuro + sacrificio de Liam (cinemática, foco total en Liam)
///
/// Pendiente a mano en el Editor tras montar esta escena (no se puede hacer desde aquí):
///   1) Colocar el prefab del Mago Oscuro (_MAGO_OSCURO.prefab) y los de Will/Estela/Liam en
///      Sendero.unity, y arrastrar sus Transforms a los campos de este componente.
///   2) RESUELTO (30/08/2026): _MAGO_OSCURO.prefab no traía Damageable de partida, pero no hace
///      falta añadirlo a mano — MagoOscuroSpellBuilder.WireCombatConfigIntoMagoOscuroPrefab() ya
///      asigna configuration.combatConfig y el flag NPCBehaviourType.Combat sobre el prefab; en
///      cuanto eso está puesto, NPCBehaviourManagerV2 añade Damageable (+ NPCCombatLifecycleHandler
///      + Targetable + NPCHealthBarSpawner) solo, en tiempo de ejecución (ver ese script, rama
///      "3. COMBAT MODULE"), con SetMaxAndCurrent(combatConfig.health, ...) y destroyOnDeath=false.
///      BossArenaController.EnableBossCombat() también quedó extendido con una rama para
///      NPCBehaviourManagerV2 (llama a EnterCombat()), además de las ya existentes ImpDemonAI/GolemBossAI.
///   3) RESUELTO (30/08/2026): ejecutar el menú "El Sendero/Magia/Crear Hechizos del Mago Oscuro
///      (Batalla Final)" ya genera NPC_Combat_Config_MagoOscuro.asset Y lo asigna solo al prefab
///      (punto 2). Solo falta ejecutar ese menú una vez desde el Editor.
///   4) Crear los shot points de cámara (Transforms) para cada fase y asignarlos.
///   5) Asignar los VFX de cataclismo/rebobinado/contrahechizo crítico (ninguno existe todavía —
///      ver guion técnico para candidatos de packs ya en el proyecto).
///   6) Dar de alta las claves de audio de esta escena si se usa algún SFX puntual además de la
///      música (ya resuelta vía _sequenceMusicId → AudioGraphProfile: "MAGOOSCURO_REVEAL" y
///      "MAGOOSCURO_CLIMAX", añadidas en esta misma pasada).
/// </summary>
public class MagoOscuroFinalBattleSequencer : CinematicSequencerBase
{
    [Header("Actores")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _estelaActor;
    [SerializeField] private Transform _liamActor;
    [SerializeField] private Transform _magoOscuroActor;
    [Tooltip("Componente Damageable del Mago Oscuro — ver punto 2 del pendiente en la cabecera. Si se deja vacío, se intenta obtener de _magoOscuroActor en tiempo de ejecución.")]
    [SerializeField] private Damageable _magoOscuroHealth;

    // FIX (31/08/2026): estos 3 campos se usaban en el script (SetEmotion en Co_MemoryVision y
    // Co_PhaseH_BetrayalAndSacrifice) pero nunca llegaron a declararse — error de compilación real
    // reportado por Raúl (CS0103). Añadidos ahora; mismo patrón NPCEmotionController.SetEmotion()
    // que ya usan EstelaAppearsSequencer/PromoVideo01Sequencer/etc. en el resto del proyecto.
    [Header("Caras (NPCEmotionController) — para SetEmotion() en la visión y la traición")]
    [Tooltip("NPCEmotionController de cada personaje: el componente que intercambia los meshes de ojos y boca según la emoción. Arrastra el que esté en el prefab de cada actor.")]
    [SerializeField] private NPCEmotionController _estelaEmotion;
    [SerializeField] private NPCEmotionController _liamEmotion;
    [Tooltip("OJO: _WILL.prefab puede llevar más de un NPCEmotionController en el mismo GameObject (uno por variante de malla/outfit) — confirma en el Inspector cuál es el activo/visible antes de arrastrarlo aquí.")]
    [SerializeField] private NPCEmotionController _willEmotion;

    [Header("Arena de combate (Fase 1 — gameplay real)")]
    [Tooltip("BossArenaController de esta arena — se usa para levantar la barrera/activar el combate real al terminar la Fase C.")]
    [SerializeField] private BossArenaController _arena;
    [Tooltip("Fracción de vida (0-1) del Mago Oscuro a la que se dispara el ataque cataclísmico de la Fase 2. Ver GDD: 'viéndose acorralado' — valor de partida razonado, no ajuste fino.")]
    [Range(0.05f, 0.6f)] [SerializeField] private float _cataclysmHealthThreshold = 0.3f;

    [Header("Cámara — shot points por fase")]
    [SerializeField] private Transform _shotAltarWide;
    [SerializeField] private Transform _shotMagoCloseup;
    [SerializeField] private Transform _shotRevelationWill;
    [SerializeField] private Transform _shotCataclysm;
    [SerializeField] private Transform _shotLiamSacrifice;

    [Header("Fase A — Aparición")]
    [Tooltip("VFX de la grieta por la que emerge el Mago Oscuro (novela: 'el aire frente al altar se rasgó').")]
    [SerializeField] private GameObject _appearanceVfx;
    [SerializeField] private float _appearanceDuration = 3f;
    [Tooltip("FIX (30/08/2026, Raúl): distancia a la que el Mago Oscuro se detiene al caminar hacia Will al principio de la Fase A.")]
    [SerializeField] private float _approachStopDistance = 3f;
    [Tooltip("FIX (30/08/2026, Raúl): duración del andar de aproximación de la Fase A.")]
    [SerializeField] private float _approachWalkDuration = 2.5f;

    [Header("Fase B — El monólogo (vía localización, ver cinematics_es.json/cinematics_en.json)")]
    [Tooltip("Clave de localización, NO el texto en sí — el texto real vive en Assets/Resources/Localization/cinematics_{es,en}.json. Añadido en esta pasada (30/08/2026) junto con su traducción al inglés.")]
    [SerializeField] private string _magoMonologueTextKey = "MAGOOSCURO_MONOLOGUE";
    [SerializeField] private float _monologuePageDuration = 5f;

    [Header("Fase C — Revelación de Will (vía localización)")]
    [Tooltip("Clave de localización — ver cinematics_es.json/cinematics_en.json.")]
    [SerializeField] private string _willFlashbackTextKey = "WILL_FLASHBACK_REVELATION";
    [Tooltip("VFX de aura que se activa en Will al recuperar su potencial (ej. reutilizar el VFX de Corazón Estelar como idle pasivo).")]
    [SerializeField] private GameObject _willAwakenedAuraVfx;
    private GameObject _willAuraInstance;

    [Header("Fase C — Visión de la Voz (pedido por Raúl, 30/08/2026)")]
    [Tooltip("Antes de las frases actuales de Will: la cámara ya está en _shotRevelationWill (reutilizado, no hace falta un shot nuevo), Will pone cara de asombro, y se mantiene un instante antes del fundido a blanco.")]
    [SerializeField] private float _visionZoomHoldDuration = 1f;
    [Tooltip("Duración de cada uno de los fundidos a blanco de FeedbackService.ScreenFadeAsync que cubren los cortes de escenario/cámara de la visión (entrada, y vuelta a la realidad).")]
    [SerializeField] private float _visionFadeDuration = 0.9f;
    [Tooltip("sequenceId de música durante la visión (Assets/_AUDIOPROFILE/AudioGraphProfile.asset) — 'Vast Silent Wonder', el mismo tema ambiente por defecto de Sendero.unity (sceneMusic), reutilizado aquí porque encaja con el 'vacío suave, casi acogedor' del capítulo XX de la novela. Al terminar la visión se restaura _magoMonologueMusicId ('MAGOOSCURO_REVEAL').")]
    [SerializeField] private string _visionMusicId = "MAGOOSCURO_VISION";
    [Tooltip("Copia de Will para la visión (NO el actor real de la arena — se instancia y se destruye al terminar). Auto-wireado por SenderoFinalSceneWiring si se deja vacío (Assets/Prefabs/_WILL.prefab).")]
    [SerializeField] private GameObject _willVisionPrefab;
    [Tooltip("El mago de la leyenda / 'la Voz' — mismo actor que ya representa a este personaje en PrologueDreamSequencer (MainWorld.unity). Auto-wireado por SenderoFinalSceneWiring si se deja vacío (Assets/Prefabs/_WILL_ORIGINAL.prefab).")]
    [SerializeField] private GameObject _goodWizardVisionPrefab;
    [Tooltip("Separación horizontal entre los dos actores en el 'stage' de la visión (metros) — Will a la izquierda, el mago a la derecha, mirándose.")]
    [SerializeField] private float _visionActorSeparation = 2.6f;
    [Tooltip("FIX (30/08/2026, Raúl: gestos de la visión desincronizados con las tarjetas de texto). Segundos que AddPhrase() suma por línea, ADEMÁS de su duración de hold, para estimar cuánto tarda DramaticTextOverlayUI en mostrar cada tarjeta de verdad: su entryDuration+exitDuration (fundido de entrada/salida, ver DramaticTextOverlayUI.GetPreset()) no se contaban antes, así que el 'cursor' que programa cada gesto se adelantaba a la tarjeta real y el desfase se acumulaba línea a línea. 0.7 = entryDuration(0.4)+exitDuration(0.3) por defecto de GetPreset() — si el preset 'Memory' del Inspector usa otros valores, ajustar aquí.")]
    [SerializeField] private float _visionCardTransitionOverhead = 0.7f;

    [Header("Fase E — Ataque cataclísmico")]
    [Tooltip("VFX que barre el escenario entero (ver guion técnico: candidato a diseñar reutilizando 'Rays'/PLASMA u otro pack ya en el proyecto).")]
    [SerializeField] private GameObject _cataclysmSweepVfx;
    [SerializeField] private float _cataclysmBuildupDuration = 2f;
    [Tooltip("Time.timeScale durante el freeze previo al impacto (no 0 exacto, para que Update() de la UI de tutorial siga respondiendo).")]
    [Range(0f, 0.2f)] [SerializeField] private float _freezeTimeScale = 0.02f;

    [Header("Fase F — Tutorial del Hechizo Prohibido del Tiempo (vía localización)")]
    [Tooltip("Clave de localización — ver cinematics_es.json/cinematics_en.json.")]
    [SerializeField] private string _timeTutorialTextKey = "TIME_SPELL_TUTORIAL";
    [SerializeField] private float _tutorialDisplayDuration = 3f;

    [Header("Fase G — Rebobinado (100% cinemático, decisión de Raúl 30/08/2026)")]
    [Tooltip("VFX que vende visualmente el rebobinado (ej. cromatismo/partículas en reversa, o el propio Volume de post-proceso con una receta dedicada).")]
    [SerializeField] private GameObject _rewindVfx;
    [SerializeField] private float _rewindDuration = 2.5f;
    [Tooltip("VFX del contrahechizo crítico de Will al reposicionarse en el punto ciego.")]
    [SerializeField] private GameObject _criticalCounterSpellVfx;
    [Tooltip("Tecla que el jugador debe pulsar en la ventana del contrahechizo. Key.None = se dispara automáticamente tras counterSpellWindowSeconds (sin depender de input, útil para probar sin mando/teclado a mano).")]
    [SerializeField] private Key _counterSpellKey = Key.None;
    [SerializeField] private float _counterSpellWindowSeconds = 2.5f;

    [Header("Fase H — Traición y sacrificio de Liam (vía localización)")]
    [SerializeField] private GameObject _betrayalStrikeVfx;
    [Tooltip("Clave de localización — ver cinematics_es.json/cinematics_en.json.")]
    [SerializeField] private string _liamLastWordsTextKey = "LIAM_LAST_WORDS";
    [SerializeField] private float _sacrificeHoldDuration = 4f;

    protected override IEnumerator Co_Sequence()
    {
        // FIX (31/08/2026, hallado leyendo Editor.log de Raul tras "seguimos con los mismos
        // fallos... te quiero pasar logs pero no salen"): DisableActorBehaviour()/DisableActorAutoRotation()
        // se llamaban DESPUES de "yield return Co_BeginCinematicWithTransition(_shotAltarWide)" --
        // es decir, dejaban una ventana real (la duracion del fundido/corte de entrada) en la que
        // _MAGO_OSCURO seguia con su NPCBehaviourManagerV2 totalmente activo desde el arranque de la
        // escena (configuration.behaviourType = Ambient+Combat desde la seccion 6.1). El log real de
        // Raul confirma que en esa ventana el boss se auto-agreaba a combate de verdad
        // (ActiveCombatRegistry lo registraba, Estela entraba en AllyCombatState y le lanzaba un
        // BolaFuego) ANTES de que el guion tuviera ocasion de desactivar la FSM -- mismo bug de la
        // seccion 11, reabierto por este hueco de temporizacion, no por el behaviourType en si. Se
        // adelantan estas desactivaciones al primerisimo momento del metodo, antes de cualquier
        // yield, para que no quede ningun frame en el que la FSM pueda correr sin control.
        DisableActorAutoRotation(_magoOscuroActor);
        DisableActorAutoRotation(_estelaActor);
        DisableActorAutoRotation(_liamActor);
        DisableActorBehaviour(_magoOscuroActor);
        DisableActorBehaviour(_estelaActor);
        DisableActorBehaviour(_liamActor);

        yield return Co_BeginCinematicWithTransition(_shotAltarWide);
        PlaySequenceMusic("MAGOOSCURO_REVEAL");

        yield return Co_PhaseA_Appearance();
        yield return Co_PhaseB_Monologue();
        yield return Co_PhaseC_Revelation();

        // Se devuelve el control de rotación a la FSM de combate antes de que empiece el gameplay
        // real — ver comentario de arriba.
        EnableActorAutoRotation(_magoOscuroActor);
        EnableActorAutoRotation(_estelaActor);
        EnableActorAutoRotation(_liamActor);

        // Fin del tramo cinemático inicial: se entrega el control al combate real (Fase 1,
        // gameplay). NO se restaura la música de escena aquí — la batalla usa su propio
        // battleMusicId ("MagoOscuro") vía NPCCombatConfig/AudioGraphProfile.battles en cuanto
        // BossArenaController active el combate.
        yield return Co_EndCinematicStayBlack();
        // API real confirmada en Assets/Scripts/Rooms/BossArenaController.cs: TriggerStartBattle()
        // levanta la barrera/puerta, dispara la señal BATTLE_START:{battleId}, inicia la música de
        // batalla vía AudioService.BeginBattleById y llama a SpawnBoss()/EnableBossCombat() (esta
        // última ya extendida para soportar NPCBehaviourManagerV2, el FSM del Mago Oscuro).
        // Se reactiva la FSM justo aquí (no antes, ni junto al EnableActorAutoRotation de arriba)
        // para que no haya ninguna ventana en la que el boss pueda auto-agrear antes de que
        // TriggerStartBattle()/EnableBossCombat() lo active de verdad vía EnterCombat().
        EnableActorBehaviour(_magoOscuroActor);
        EnableActorBehaviour(_estelaActor);
        EnableActorBehaviour(_liamActor);

        _arena?.TriggerStartBattle();

        // FIX (30/08/2026, Raul: "el final se queda en negro y no empieza la batalla"):
        // TriggerStartBattle() termina en BossArenaController.SpawnBoss(), que delega la
        // revelacion de pantalla (el fundido de vuelta desde negro que dejo Co_EndCinematicStayBlack
        // mas arriba) a BossIntroPresentation.PlayIntroduction() -- pero esa presentacion exige una
        // Camera hija en el boss (PlayPresentationAndPlaceBoss: boss.GetComponentInChildren<Camera>).
        // _MAGO_OSCURO.prefab no tiene ninguna (confirmado: 0 componentes Camera) -- es un actor
        // 100% cinematico que lleva ya varios minutos en pantalla, no un boss "de camara propia"
        // como el resto de jefes del juego. Sin camara, PlayPresentationAndPlaceBoss() detecta
        // bossCamera == null, coloca al boss y activa el combate igualmente, pero sale sin revertir
        // JAMAS el fundido a negro -- el combate arranca de verdad detras de una pantalla que se
        // queda negra para siempre. Se revela aqui explicitamente, y SOLO en este caso (si en el
        // futuro se le anade una camara propia al Mago Oscuro, esta condicion se salta sola y
        // BossIntroPresentation vuelve a encargarse de su fundido normal sin interferencias).
        // No se toca BossArenaController.cs a proposito: es un componente generico compartido por
        // el resto de combates del juego, que nunca llegan con la pantalla ya en negro -- anadir ahi
        // un fundido de revelacion a ciegas causaria un flash a negro no deseado en esos otros jefes.
        if (_magoOscuroActor == null || _magoOscuroActor.GetComponentInChildren<Camera>(true) == null)
            yield return FeedbackService.ScreenFadeAsync(Color.black, 0.3f, fadeIn: false);

        yield return Co_WaitForCataclysmThreshold();

        // Se recupera el control cinemático para la Fase 2 en adelante — y con él, el control de
        // rotación manual (ver comentario de arriba).
        yield return Co_BeginCinematicWithTransition(_shotCataclysm);
        PlaySequenceMusic("MAGOOSCURO_CLIMAX");
        DisableActorAutoRotation(_magoOscuroActor);
        DisableActorAutoRotation(_estelaActor);
        DisableActorAutoRotation(_liamActor);

        // Se corta también la FSM de combate real (ver comentario arriba) para que el boss no siga
        // atacando por su cuenta durante la cinemática guionizada de traición/sacrificio.
        DisableActorBehaviour(_magoOscuroActor);
        DisableActorBehaviour(_estelaActor);
        DisableActorBehaviour(_liamActor);

        yield return Co_PhaseE_Cataclysm();
        yield return Co_PhaseF_TimeTutorial();
        yield return Co_PhaseG_Rewind();
        yield return Co_PhaseH_BetrayalAndSacrifice();

        // Se restaura por limpieza (WillSacrificeSequencer no llama a FaceTarget, pero Estela sigue
        // viva para el Epílogo y no debe quedar con la rotación "congelada").
        EnableActorAutoRotation(_magoOscuroActor);
        EnableActorAutoRotation(_estelaActor);
        EnableActorAutoRotation(_liamActor);

        // Misma limpieza para la FSM de combate — Estela sigue viva para el Epílogo y su
        // NPCBehaviourManagerV2 (si lo usa) no debe quedar deshabilitado para siempre.
        EnableActorBehaviour(_magoOscuroActor);
        EnableActorBehaviour(_estelaActor);
        EnableActorBehaviour(_liamActor);

        // Deliberadamente SIN Co_EndCinematicWithTransition ni RestoreMusic(): la escena 21
        // (WillSacrificeSequencer) continúa directamente desde aquí, misma música MAGOOSCURO_CLIMAX
        // sin corte — ver comentario en WillSacrificeSequencer.Co_Sequence().
        RaiseSignalOut();
    }

    // ── Fase A ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseA_Appearance()
    {
        _cinematicCamera?.Cut(_shotAltarWide);

        // FIX (30/08/2026, Raúl: "el mago oscuro (no se ve en el video) al principio camina hacia
        // will y hay un tramo que hace con la animacion de hablar. Hoy se han pasado animaciones al
        // upperbody... y no ha funcionado porque hace la animacion de hablar y los pies quitos"):
        // confirmado por grep que en este archivo no existía NINGUNA llamada de movimiento para el
        // Mago Oscuro — el cambio de capa de Animator (Base→UpperBody) de hoy solo podía afectar en
        // qué capa se reproduce el gesto de hablar, nunca iba a mover los pies, porque nada en
        // código llamaba a NPCBehaviourManagerV2.MoveToPosition() (la API ya establecida para
        // movimiento cinemático de NPCs — ver CinematicState.MoveToPositionSequence, y su uso en
        // otros sequencers). Además NPCBehaviourManagerV2 está deshabilitado para todo este tramo
        // (ver DisableActorBehaviour() en Co_Sequence(), sección 11 — anti-autoagro): con el
        // componente deshabilitado su Update() no corre, así que MoveToPosition() no podría avanzar
        // ni un solo frame aunque se llamara. Se reactiva aquí SOLO durante el andar: MoveToPosition()
        // fuerza el estado a CinematicState (ForceState) antes de que el componente recién
        // reactivado llegue a su próximo Update() — CinematicState.CheckTransitions() únicamente
        // mira WasDefeatedInCombat (ver States/CinematicState.cs), a diferencia de Ambient/Idle, que
        // es por donde entraba la detección de proximidad que motivó la sección 11 — así que no
        // reabre ese bug. Se vuelve a desactivar al terminar de andar, para el resto de la Fase A-C.
        // FIX (31/08/2026, ronda 15, Raul: "ahora al principio camina bien se para da un salto
        // se coloca mirando a la derecha y luego se gira, con lo facil que es: camino hacia el
        // player, me paro a x metros de el"): la version anterior usaba
        // NPCBehaviourManagerV2.MoveToPosition() (NavMesh, con turn:true) -- MoveToPositionSequence
        // hace el giro final de llegada (turnAroundOnArrival) como un paso APARTE, no interpolado
        // junto con el propio andar, de ahi el "salto" + giro brusco que describe Raul. Se sustituye
        // por un andar hecho a mano, mucho mas simple: mover el Transform en linea recta hacia un
        // punto a _approachStopDistance de Will, girando SUAVEMENTE hacia la direccion de avance
        // en CADA frame -- asi nunca hay un giro desacoplado del andar, y el personaje ya llega
        // mirando a donde debe. No usa NavMesh ni MoveToPositionSequence en absoluto, asi que
        // tampoco puede reabrir el bug de la seccion 17 (lock de PlayerLockService huerfano): ese
        // lock solo lo adquiere MoveToPositionSequence, que aqui ya no se usa para nada.
        var magoAnim = _magoOscuroActor?.GetComponentInChildren<NPCSimpleAnimator>(true);
        var magoBehaviour = _magoOscuroActor?.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (magoAnim != null && _willActor != null)
        {
            Vector3 toWill = _willActor.position - _magoOscuroActor.position;
            toWill.y = 0f;
            if (toWill.sqrMagnitude > 0.01f)
            {
                Vector3 walkTarget = _willActor.position - toWill.normalized * _approachStopDistance;
                walkTarget.y = _magoOscuroActor.position.y;

                magoAnim.PlaySocialGesture("Talk01");
                yield return Co_SimpleWalkTo(_magoOscuroActor, magoAnim, magoBehaviour != null ? magoBehaviour.Agent : null,
                    walkTarget, _approachWalkDuration);
            }
        }

        // FIX (30/08/2026, Raul: "de momento quita el portal ese"): _appearanceVfx
        // (VFX_MagoOscuro_Aparicion, ver MagoOscuroCinematicVfxBuilder.cs) es un duplicado de
        // vfx_Portal_02 (GabrielAguiarProductions/FreeQuickEffectsVol1) que nunca llego a
        // retintarse a violeta/negro ni a escalarse/reposicionarse para esta escena (los dos TODOs
        // seguian pendientes desde que se creo) -- por eso se veia como un pilar de humo rojo/blanco
        // atravesando al Mago Oscuro y aparentando "volar" (reportado por Raul con capturas).
        // Desactivado a peticion suya hasta retocar esos valores; _appearanceVfx sigue asignado en
        // el Inspector (via SenderoFinalSceneWiring) por si se retoma mas adelante -- basta con
        // descomentar estas dos lineas.
        // if (_appearanceVfx != null && _magoOscuroActor != null)
        //     VfxPoolService.Instance?.Play(_appearanceVfx, _magoOscuroActor.position, Quaternion.identity, _appearanceDuration);

        _cinematicCamera?.MoveTo(_shotMagoCloseup, _appearanceDuration);
        yield return new WaitForSeconds(_appearanceDuration);

        FaceTarget(_magoOscuroActor, _willActor);
        FaceTarget(_willActor, _magoOscuroActor);
    }

    // ── Fase B ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseB_Monologue()
    {
        // FIX (31/08/2026): añadido animTrigger — Raúl reportó que ningún personaje anima durante
        // los diálogos, se quedan en Idle todo el rato. ShowBubblePaged sí soporta animTrigger
        // (ver SpeechBubbleUI.Show → NPCSimpleAnimator.PlaySocialGesture, resuelve capa Base/
        // UpperBody solo — ver claude/catalogo-animaciones-invector.md), pero ninguna llamada de
        // este guion lo estaba usando. El monólogo (13 páginas tras el recorte de la sección de
        // texto largo) se parte en 3 tramos con gesto propio en vez de uno solo repetido 13 veces
        // — loopAnim:true re-dispara el gesto en cada página de su propio tramo.
        string[] monologuePages = Loc(_magoMonologueTextKey).Split('\n');
        yield return ShowBubblePaged(_magoOscuroActor, JoinLines(monologuePages, 0, 4), _monologuePageDuration,
            animTrigger: "Angry01", loopAnim: true, speakerName: "Mago Oscuro");
        yield return ShowBubblePaged(_magoOscuroActor, JoinLines(monologuePages, 4, 5), _monologuePageDuration,
            animTrigger: "Talk02", loopAnim: true, speakerName: "Mago Oscuro");
        yield return ShowBubblePaged(_magoOscuroActor, JoinLines(monologuePages, 9, 4), _monologuePageDuration,
            animTrigger: "Angry02", loopAnim: true, speakerName: "Mago Oscuro");
    }

    private static string JoinLines(string[] lines, int start, int count) => string.Join("\n", lines, start, count);

    // ── Fase C ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseC_Revelation()
    {
        // Visión de la Voz (pedido por Raúl, 30/08/2026): zoom + asombro + fundido a blanco +
        // diálogo con la Voz (el mago de la leyenda cuya alma ocupa el cuerpo de Will — ver
        // capítulo XX de la novela / biblia-del-universo.md, "La leyenda fundacional"), ANTES de
        // que Will diga sus frases actuales. Ver Co_MemoryVision más abajo.
        yield return Co_MemoryVision();

        yield return ShowBubblePaged(_willActor, Loc(_willFlashbackTextKey), 3f,
            animTrigger: "Question02", loopAnim: true, speakerName: "Will");

        if (_willAwakenedAuraVfx != null && _willActor != null)
        {
            // FIX (30/08/2026, Raul: "el prefab que sale sobre will esta metido en el suelo en
            // lugar de cubrirle entero se ve feo"): se instanciaba en hand.position, la raiz del
            // actor -- a la altura de los pies, no del cuerpo -- de ahi que se viera hundido en el
            // suelo en vez de envolviendo a Will. Se calcula el centro real del personaje a partir
            // de los bounds combinados de sus Renderers (mas fiable que adivinar un offset fijo a
            // ciegas, porque no depende de saber la altura exacta del modelo) y se instancia ahi.
            // Sigue pendiente, como ya decia el TODO original, sustituir esto por el hueso/anchor
            // real cuando se monte en el Editor -- esto es una mejora de la posicion por defecto,
            // no ese anchor definitivo.
            var hand = _willActor; // TODO: sustituir por el hueso/anchor real de aura cuando se monte en el Editor
            Vector3 auraPosition = hand.position;
            var willRenderers = hand.GetComponentsInChildren<Renderer>();
            if (willRenderers.Length > 0)
            {
                Bounds willBounds = willRenderers[0].bounds;
                for (int i = 1; i < willRenderers.Length; i++)
                    willBounds.Encapsulate(willRenderers[i].bounds);
                auraPosition = willBounds.center;
            }
            _willAuraInstance = Instantiate(_willAwakenedAuraVfx, auraPosition, Quaternion.identity, hand);
        }

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// Reescrita otra vez (31/08/2026, ronda 3) a petición de Raúl tras probar la v2 (bocadillos
    /// ShowBubblePaged sobre los dos actores reales): "no quiero eso, me gustaba más el fondo
    /// blanco con el dream overlay y el sparkle... en primer plano los magos, como en la secuencia
    /// del prólogo — sería repetir la secuencia del prólogo pero con texto en medio". Se mantienen
    /// los dos personajes 3D reales de la v2 (Will a la izquierda / la Voz a la derecha, mirándose
    /// — eso SÍ lo quería) pero el diálogo ya no son bocadillos: es DramaticTextOverlayUI (el mismo
    /// sistema de "recuerdos/momentos épicos" que ya usan PrologueDreamSequencer/
    /// CreditsSceneController para su "modo sueño" — nebulosa _dreamBackground + chispas
    /// _dreamSparkles, activadas con DramaticPhraseConfig.dreamMode=true), con cada frase en
    /// background:None (transparente) para que se vea a través nuestro fondo blanco real de cámara
    /// + los dos personajes, y positionOffset hacia el hablante (izquierda=Will, derecha=la Voz) en
    /// vez de un bocadillo pegado a la cabeza — pedido explícito de Raúl ("para saber quien habla
    /// acercamos el texto un poco al personaje").
    ///
    /// Por qué background:None y no DreamWhite: DreamWhite es una capa de color SÓLIDA (alpha 1)
    /// que RunSequence() solo sabe mantener opaca sin re-fundir entre frases si es FullBlack
    /// (limitación ya diagnosticada en la v1) — con cualquier frase que la tenga puesta, cada frase
    /// nueva vuelve a fundir TODO el overlay 0→1, lo que expondría nuestro escenario un instante (el
    /// mismo parpadeo blanco↔Will que Raúl reportó en la v1). Con None, esa capa nunca es opaca
    /// (siempre Color.clear) así que no hay nada que parpadear — el fondo blanco de verdad sigue
    /// siendo, igual que en la v2, el propio Camera.main con clearFlags=SolidColor, fijo todo el
    /// rato sin animación de opacidad de por medio.
    ///
    /// Las dos copias temporales (willVision/wizardVision) ahora se instancian dentro de un
    /// contenedor padre QUE EMPIEZA DESACTIVADO (visionStage) — mismo patrón que
    /// PrologueDreamSequencer.BuildStage()/PrepareActorInstance(): Awake() de los hijos se sigue
    /// disparando igual, pero OnEnable()/Start() se difieren hasta activar el contenedor, dando
    /// tiempo a PrepareVisionActorInstance() a apagar NavMeshAgent/NPCBehaviourManagerV2/
    /// NPCSimpleAnimator (mismo bug de rotación que Co_PhaseA_Appearance, ver DisableActorAutoRotation
    /// más abajo) Y, como _willVisionPrefab es _WILL.prefab (el prefab COMPLETO del jugador, con el
    /// stack de Invector vThirdPersonInput/vThirdPersonController activo — no un NPC), también ese
    /// stack — causa real de la excepción reportada por Raúl (NullReferenceException en
    /// Invector.vCharacterController.vThirdPersonInput.OnAnimatorMove(), campo interno 'cc' sin
    /// inicializar): sin este apagado, la copia instanciada es un segundo "jugador fantasma"
    /// completo compitiendo por el mismo input, con su propio Awake/Start corriendo sin red antes de
    /// que ControlAnimatorRootMotion() tenga todo listo.
    ///
    /// Nota sobre el HUD: DramaticTextOverlayUI oculta/restaura el HUD de gameplay él solo
    /// (HideGameplayUI/ShowGameplayUI en su Play()/RunSequence()), sin saber que ya estamos dentro
    /// de una cinemática que lo tiene oculto desde antes (CinematicSequencerBase.LockCinematic()) —
    /// al terminar su propia secuencia lo volvería a mostrar de golpe, en mitad de esta cinemática.
    /// Por eso se vuelve a ocultar a mano justo después (mismas tres llamadas que LockCinematic()).
    /// </summary>
    private IEnumerator Co_MemoryVision()
    {
        _cinematicCamera?.Cut(_shotRevelationWill);
        _willEmotion?.SetEmotion(NPCEmotion.Surprised);
        yield return new WaitForSeconds(_visionZoomHoldDuration);

        if (_willVisionPrefab == null || _goodWizardVisionPrefab == null)
        {
            Debug.LogWarning("[MagoOscuroFinalBattleSequencer] _willVisionPrefab/_goodWizardVisionPrefab " +
                "sin asignar — vuelve a ejecutar 'El Sendero/Escena/Rellenar Referencias de la Batalla " +
                "Final' para que se autowireen (Assets/Prefabs/_WILL.prefab y _WILL_ORIGINAL.prefab). " +
                "Se salta la visión y se continúa directo con las frases de Will.");
            yield break;
        }

        PlaySequenceMusic(_visionMusicId);

        // Fundido corto que cubre el corte de escenario/cámara (de la arena real al 'stage' de la
        // visión, a 4000 unidades de distancia) para que no sea un salto seco.
        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: true);

        Vector3 stageAnchor = new Vector3(0f, 4000f, 0f);
        Vector3 half = Vector3.right * (_visionActorSeparation * 0.5f);
        Vector3 willPos = stageAnchor - half;
        Vector3 wizardPos = stageAnchor + half;

        // Contenedor desactivado — ver cabecera de este método (mismo truco de
        // PrologueDreamSequencer para diferir OnEnable()/Start() de las copias hasta que estén
        // completamente "apagadas" por PrepareVisionActorInstance()).
        var visionStage = new GameObject("VisionStage_Runtime");
        visionStage.SetActive(false);
        visionStage.transform.position = stageAnchor;

        GameObject willVision = Instantiate(_willVisionPrefab, willPos, Quaternion.identity, visionStage.transform);
        GameObject wizardVision = Instantiate(_goodWizardVisionPrefab, wizardPos, Quaternion.identity, visionStage.transform);
        PrepareVisionActorInstance(willVision);
        PrepareVisionActorInstance(wizardVision);
        FaceTarget(willVision.transform, wizardVision.transform);
        FaceTarget(wizardVision.transform, willVision.transform);
        // _WILL.prefab lleva más de un NPCEmotionController en el mismo GameObject (solo uno activo/
        // visible según el outfit) — se dispara la emoción en todos los que haya, en vez de arriesgar
        // GetComponent<> cogiendo el que no toca; no pasa nada si alguno no tiene efecto visible.
        var willVisionEmotions = willVision.GetComponentsInChildren<NPCEmotionController>(true);
        // Mismo motivo que el resto de bocadillos de esta pasada (Raúl: "no veo que hagan
        // animaciones ningún personaje") — la visión usa DramaticTextOverlayUI, no ShowBubblePaged,
        // así que no hay animTrigger automático: se dispara el gesto a mano.
        //
        // BUG REAL (Raúl, 30/08/2026, ronda 7): Will seguía sin gesticular en la visión aunque el
        // fondo/cámara ya estaban arreglados. Causa: _willVisionPrefab es _WILL.prefab (el jugador
        // completo, Invector), que NO lleva NPCSimpleAnimator — ese componente es exclusivo de NPCs
        // (confirmado por guid: 0 apariciones en _WILL.prefab, 1 en _WILL_ORIGINAL.prefab). El
        // jugador gesticula con PlayerDialogueAnimator.PlayGesture() en su lugar (mismo componente
        // que usa ShowSpeechBubbleNode para los diálogos normales de Will) — confirmado por guid:
        // 1 aparición en _WILL.prefab, 0 en _WILL_ORIGINAL.prefab. _goodWizardVisionPrefab SÍ es
        // _WILL_ORIGINAL.prefab (el mago de la leyenda, NPC puro), así que su lado sigue usando
        // NPCSimpleAnimator.PlaySocialGesture() sin cambios.
        var willVisionAnimator = willVision.GetComponentInChildren<PlayerDialogueAnimator>(true);
        var wizardVisionAnimator = wizardVision.GetComponentInChildren<NPCSimpleAnimator>(true);
        // FIX (30/08/2026, Raul: "en el pensamiento de will solo hace animaciones el mago, will ni
        // una"): el codigo que dispara los gestos de Will (Co_DelayedGestureWill/AddPhrase, mas
        // abajo) no ha cambiado desde la seccion 12 (donde se confirmo working por guid), asi que
        // no se pudo encontrar una causa concreta solo leyendo codigo esta vez. Aviso SIEMPRE
        // visible (no solo con debugMode) para el proximo test: si esto sale en consola, el
        // problema es que la copia de Will no tiene PlayerDialogueAnimator (revisar
        // _willVisionPrefab en el Inspector); si NO sale, el componente se encuentra bien y el
        // problema esta dentro de PlayGesture() -- ver el debugMode activado en
        // PlayerDialogueAnimator.cs para ese caso.
        if (willVisionAnimator == null)
            Debug.LogWarning("[MagoOscuroFinalBattleSequencer] willVisionAnimator es null -- " +
                "_willVisionPrefab no tiene PlayerDialogueAnimator en ningun hijo. Los gestos de Will " +
                "en la visión no pueden dispararse.");

        visionStage.SetActive(true);

        Vector3 midpoint = (willPos + wizardPos) * 0.5f + Vector3.up * 1.5f;
        Vector3 camPos = midpoint - Vector3.forward * 4.2f + Vector3.up * 0.1f;
        var camShotGO = new GameObject("Shot_VisionTwoShot_Runtime");
        camShotGO.transform.position = camPos;
        camShotGO.transform.rotation = Quaternion.LookRotation((midpoint - camPos).normalized, Vector3.up);
        var camShotCam = camShotGO.AddComponent<Camera>();
        camShotCam.enabled = false; // CinematicCameraDriver.Cut() solo lee el FOV vía TryGetComponent, no hace falta que esté activa
        camShotCam.fieldOfView = 32f;

        // Point, no Directional a propósito: un Directional Light "es sol" para TODA la escena, así
        // que iluminaría también la arena real (a 4000 unidades de aquí) mientras dura la visión —
        // un Point con rango corto se queda contenido en el propio stage.
        var lightGO = new GameObject("VisionLight_Runtime");
        lightGO.transform.position = midpoint + Vector3.up * 2.5f - Vector3.forward * 1f;
        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 12f;
        light.intensity = 4f;
        light.color = new Color(1f, 0.97f, 0.9f);
        light.shadows = LightShadows.None;

        var mainCam = Camera.main;
        CameraClearFlags origClearFlags = default;
        Color origBackgroundColor = default;
        bool restoreCam = mainCam != null;
        if (restoreCam)
        {
            origClearFlags = mainCam.clearFlags;
            origBackgroundColor = mainCam.backgroundColor;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.98f, 0.97f, 0.94f); // blanco cálido, no blanco puro
        }
        else
        {
            Debug.LogWarning("[MagoOscuroFinalBattleSequencer] Camera.main es null — no se puede forzar " +
                "el fondo blanco de la visión (CinematicCameraDriver dice mover Camera.main directamente).");
        }

        _cinematicCamera?.Cut(camShotGO.transform);

        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: false);

        // Diálogo — DramaticTextOverlayUI en vez de bocadillos (ver cabecera del método), una única
        // secuencia larga (un solo Play(), ver nota del HUD arriba: dos Play() consecutivos
        // mostrarían y ocultarían el HUD de golpe entre medias). El beat de emoción de Will (mismo
        // punto que en la v2: justo antes de preguntar por Estela y Liam) se dispara con un timer
        // en paralelo en vez de partir la secuencia en dos.
        Vector2 leftOffset = new Vector2(-320f, -60f);   // Will
        Vector2 rightOffset = new Vector2(320f, -60f);   // La Voz

        var visionConfig = ScriptableObject.CreateInstance<DramaticPhraseConfig>();
        visionConfig.dreamMode = true;
        visionConfig.pauseBetween = 0.25f;

        // BUG REAL (Raúl, 30/08/2026): con cada clave entera como UNA frase, el texto (hasta 3
        // oraciones seguidas) desbordaba por debajo de la pantalla — el recuadro de
        // DramaticTextOverlayUI es de solo 900x300px a fontSize 72-90 (Start.unity), y su
        // overflowMode es "Overflow" (no recorta, deja que el texto siga cayendo fuera de la caja).
        // Arreglo: cada clave de localización trae varias líneas separadas por '\n' (repaginadas a
        // mano para cada línea ≤ ~58 caracteres — ver escena-wiring-localizacion, sección 9), y
        // AddPhrase() la trocea en una DramaticPhrase por línea, mostradas en secuencia dentro de la
        // misma llamada a Play() (no una llamada por línea). "cursor" acumula duración+pausa según se
        // van añadiendo frases.
        //
        // BUG REAL (Raúl, 30/08/2026, ronda 8): "hace la animación que corresponda a la frase... y
        // luego debe continuar con la de hablando... si no, se ve en Idle y queda muy feo — esto
        // aplica a cualquier conversación, así es como lo tenemos en el juego". Mismo criterio ya
        // usado en TODO el resto del juego vía CinematicSequencerBase.ShowBubblePaged(loopAnim:
        // true) — re-dispara el animTrigger en CADA página, no solo una vez al principio del bloque
        // (ver comentario propio de ShowBubblePaged: "loopAnim: si true, re-dispara el animTrigger en
        // cada página"). Antes, esta visión solo disparaba 3 gestos sueltos para 15 tarjetas de
        // texto — entre gesto y gesto el one-shot terminaba y el personaje volvía a Idle antes de que
        // apareciera la siguiente tarjeta, dando la sensación de saltos aleatorios en vez de uno por
        // frase. Arreglo: AddPhrase() ahora recibe el gesto del grupo y lo vuelve a disparar en cada
        // línea (con el propio "cursor" de esa línea como delay) — mismo resultado que loopAnim=true,
        // adaptado a que aquí las líneas se reproducen dentro de una única llamada a Play() en vez de
        // una llamada por página.
        var visionPhrases = new System.Collections.Generic.List<DramaticPhrase>();
        float cursor = 0f;
        void AddPhrase(string locKey, DramaticTextStyle style, Vector2 offset, string gestureTrigger, bool isWill)
        {
            foreach (var line in Loc(locKey).Split('\n'))
            {
                float dur = Mathf.Clamp(line.Length / 14f, 1.6f, 4.2f);
                visionPhrases.Add(VisionPhrase(line, style, offset, dur));
                if (isWill)
                    StartCoroutine(Co_DelayedGestureWill(cursor, willVisionAnimator, gestureTrigger));
                else
                    StartCoroutine(Co_DelayedGesture(cursor, wizardVisionAnimator, gestureTrigger));
                // FIX (30/08/2026, Raúl: "a veces hablan y no hacen animación... la hacen los dos a
                // la vez... no está sincronizado"): _visionCardTransitionOverhead compensa el
                // entryDuration/exitDuration de cada tarjeta que este cursor no contaba (ver Tooltip
                // del campo) — sin él, el desfase entre "cuándo cree este script que se ve cada
                // tarjeta" y cuándo se ve de verdad se acumulaba línea a línea (hasta varios segundos
                // en una visión de 15 líneas), disparando gestos sobre la tarjeta equivocada.
                cursor += dur + visionConfig.pauseBetween + _visionCardTransitionOverhead;
            }
        }

        // BUG REAL (Raúl, 30/08/2026, ronda 9): mismo bug que el bocadillo del Mago Oscuro (ver
        // CinematicSequencerBase.ShowBubblePaged()) — sin BeginInteraction(), cada gesto one-shot de
        // la Voz (NPCSimpleAnimator) cae solo a Idle en cuanto termina su clip (1-2s), mucho antes de
        // que cambie de tarjeta (hasta 4.2s por línea). Mismo arreglo: BeginInteraction() aquí (deja
        // a la Voz en su interactState entre gesto y gesto, en vez de Idle) y EndInteraction() al
        // terminar la secuencia, más abajo. Will no lo necesita — PlayerDialogueAnimator no tiene
        // este problema (ver comentario de ShowBubblePaged).
        wizardVisionAnimator?.BeginInteraction();

        // Question02 = pregunta/confusión (Will no sabe dónde está / si está muerto — y de nuevo más
        // abajo, preocupado por Estela y Liam). Talk03 = la Voz explicando con calma (mismo trigger
        // que ya usa EpilogueSequencer para Estela explicando). Talk01 = Will respondiendo/afirmando
        // (mismo trigger que ya usa WillSacrificeSequencer para su réplica calmada).
        AddPhrase("WILL_VISION_WHERE_AM_I", DramaticTextStyle.Memory, leftOffset, "Question02", isWill: true);
        AddPhrase("WILL_VISION_AM_I_DEAD", DramaticTextStyle.Memory, leftOffset, "Question02", isWill: true);

        AddPhrase("VOICE_VISION_REASSURANCE", DramaticTextStyle.Epic, rightOffset, "Talk03", isWill: false);
        AddPhrase("VOICE_VISION_EXPLANATION", DramaticTextStyle.Epic, rightOffset, "Talk03", isWill: false);

        AddPhrase("WILL_VISION_THEN_I", DramaticTextStyle.Memory, leftOffset, "Talk01", isWill: true);
        AddPhrase("VOICE_VISION_MUST_END_SENDERO", DramaticTextStyle.Epic, rightOffset, "Talk03", isWill: false);
        float scaredDelay = cursor; // arranca en "¿Y Estela? ¿Y Liam?"

        AddPhrase("WILL_VISION_WHAT_ABOUT_FRIENDS", DramaticTextStyle.Memory, leftOffset, "Question02", isWill: true);
        AddPhrase("VOICE_VISION_PROTECT_THEM", DramaticTextStyle.Epic, rightOffset, "Talk03", isWill: false);

        visionConfig.phrases = visionPhrases.ToArray();

        // Beat de emoción "Scared" de Will — coincide con el arranque de
        // "WILL_VISION_WHAT_ABOUT_FRIENDS" (mismo punto que en la v2). StartCoroutine (no yield)
        // para que corra EN PARALELO al Play() de abajo sin partirlo en dos.
        StartCoroutine(Co_DelayedEmotion(scaredDelay, willVisionEmotions, NPCEmotion.Scared));

        yield return Co_PlayDramaticSequence(visionConfig);
        wizardVisionAnimator?.EndInteraction();
        Destroy(visionConfig);

        // Ver nota del HUD en la cabecera del método: DramaticTextOverlayUI acaba de restaurarlo
        // por su cuenta al terminar su propia secuencia — se vuelve a ocultar porque seguimos
        // dentro de esta cinemática (mismas tres llamadas que CinematicSequencerBase.LockCinematic()).
        PlayerHUDV2.Instance?.HideHUD();
        MinimapController.Instance?.SetHiddenByCinematic(true);
        TimeOfDayIndicator.Instance?.Hide();

        // Vuelta a la realidad.
        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: true);

        if (restoreCam)
        {
            mainCam.clearFlags = origClearFlags;
            mainCam.backgroundColor = origBackgroundColor;
        }
        Destroy(visionStage);
        Destroy(camShotGO);
        Destroy(lightGO);

        _cinematicCamera?.Cut(_shotRevelationWill);
        yield return FeedbackService.ScreenFadeAsync(Color.white, _visionFadeDuration, fadeIn: false);

        PlaySequenceMusic("MAGOOSCURO_REVEAL");
    }

    /// <summary>Construye una DramaticPhrase con los valores fijos que comparten todas las frases de
    /// la visión (background:None, fade in/out simple — ver cabecera de Co_MemoryVision) más los que
    /// varían por frase (texto, estilo, posición, duración).</summary>
    private static DramaticPhrase VisionPhrase(string text, DramaticTextStyle style, Vector2 positionOffset, float duration)
    {
        return new DramaticPhrase
        {
            text = text,
            style = style,
            background = DramaticTextBackground.None,
            entryAnim = DramaticEntryAnimation.FadeIn,
            exitAnim = DramaticExitAnimation.FadeOut,
            duration = duration,
            positionOffset = positionOffset,
        };
    }

    /// <summary>Envuelve DramaticTextOverlayUI.Play() (API de callback) en una coroutine yieldable.</summary>
    private static IEnumerator Co_PlayDramaticSequence(DramaticPhraseConfig config)
    {
        if (DramaticTextOverlayUI.Instance == null)
        {
            Debug.LogWarning("[MagoOscuroFinalBattleSequencer] DramaticTextOverlayUI.Instance es null " +
                "(¿el HUD persistente de Start.unity no llegó a cargar en esta escena?) — se salta " +
                "el diálogo de la visión.");
            yield break;
        }
        bool done = false;
        DramaticTextOverlayUI.Instance.Play(config, () => done = true);
        yield return new WaitUntil(() => done);
    }

    private static IEnumerator Co_DelayedEmotion(float delay, NPCEmotionController[] targets, NPCEmotion emotion)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (targets == null) yield break;
        foreach (var emo in targets)
            if (emo != null) emo.SetEmotion(emotion);
    }

    private static IEnumerator Co_DelayedGesture(float delay, NPCSimpleAnimator animator, string stateName)
    {
        yield return new WaitForSecondsRealtime(delay);
        animator?.PlaySocialGesture(stateName);
    }

    /// <summary>Igual que Co_DelayedGesture() pero para el lado de Will en Co_MemoryVision(), que usa
    /// PlayerDialogueAnimator (jugador) en vez de NPCSimpleAnimator (NPC) — ver comentario en
    /// Co_MemoryVision() sobre por qué son dos componentes distintos.</summary>
    private static IEnumerator Co_DelayedGestureWill(float delay, PlayerDialogueAnimator animator, string stateName)
    {
        yield return new WaitForSecondsRealtime(delay);
        animator?.PlayGesture(stateName);
    }

    /// <summary>
    /// Prepara una copia temporal de un actor para la visión de Co_MemoryVision(): apaga todo lo
    /// que pueda moverla/rotarla/hacerla caer sola en el vacío sin NavMesh donde vive el stage (a
    /// 4000 unidades de cualquier escena real), dejando solo lo visual (Animator/mesh/
    /// NPCEmotionController) con vida — mismo criterio que
    /// PrologueDreamSequencer.PrepareActorInstance(), ampliado porque _willVisionPrefab es
    /// _WILL.prefab, el prefab COMPLETO del jugador (con el stack de Invector activo), no un NPC.
    /// Debe llamarse mientras el GameObject sigue dentro de un contenedor padre desactivado (ver
    /// Co_MemoryVision) para que estos apagados lleguen ANTES de que OnEnable()/Start() se disparen.
    /// </summary>
    private static void PrepareVisionActorInstance(GameObject actor)
    {
        if (actor == null) return;

        var agent = actor.GetComponentInChildren<UnityEngine.AI.NavMeshAgent>(true);
        if (agent != null) agent.enabled = false;

        var behaviourManager = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (behaviourManager != null) behaviourManager.enabled = false;

        actor.GetComponentInChildren<NPCSimpleAnimator>(true)?.DisableAutoRotation();

        // Stack de Invector — solo lo lleva _WILL.prefab (el jugador); _WILL_ORIGINAL.prefab (el
        // mago de la leyenda) es un NPC puro y no tiene ninguno de estos tres, GetComponentInChildren
        // devuelve null sin más. Causa real de la NullReferenceException reportada por Raúl en
        // vThirdPersonInput.OnAnimatorMove() — ver cabecera de Co_MemoryVision.
        var thirdPersonInput = actor.GetComponentInChildren<vThirdPersonInput>(true);
        if (thirdPersonInput != null) thirdPersonInput.enabled = false;
        var thirdPersonController = actor.GetComponentInChildren<vThirdPersonController>(true);
        if (thirdPersonController != null) thirdPersonController.enabled = false;

        var charController = actor.GetComponentInChildren<CharacterController>(true);
        if (charController != null) charController.enabled = false;

        var rb = actor.GetComponentInChildren<Rigidbody>(true);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        var animator = actor.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.applyRootMotion = false;

        // BUG REAL (Raúl, 30/08/2026): fondo blanco que no aparece + los dos personajes invisibles
        // en la visión. Causa: _willVisionPrefab es _WILL.prefab completo, que trae su propio rig de
        // cámara (vThirdPersonCamera, tag MainCamera, 2 componentes Camera + 1 AudioListener
        // confirmado en el YAML). Al clonarlo, ese rig se convierte en un SEGUNDO objeto activo con
        // tag MainCamera compitiendo con la cámara real — Camera.main puede devolver cualquiera de
        // los dos, así que el Camera.main.clearFlags/backgroundColor y el _cinematicCamera.Cut() de
        // Co_MemoryVision podían estar aplicándose sobre la cámara del clon (que nunca se ve en
        // pantalla) en vez de la real. _WILL_ORIGINAL.prefab no lleva ninguna (confirmado, NPC puro),
        // pero se desactiva en los dos por si acaso — GetComponentsInChildren en plural porque
        // _WILL.prefab lleva 2 Camera.
        foreach (var cam in actor.GetComponentsInChildren<Camera>(true))
            cam.enabled = false;
        foreach (var listener in actor.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
    }

    /// <summary>Ver comentario de uso en Co_Sequence() — mismo bug que PrologueDreamSequencer ya
    /// diagnosticó (NPCSimpleAnimator.ApplySmoothRotation() pisa cualquier FaceTarget() manual).</summary>
    private static void DisableActorAutoRotation(Transform actor)
    {
        if (actor == null) return;
        actor.GetComponentInChildren<NPCSimpleAnimator>(true)?.DisableAutoRotation();
    }

    private static void EnableActorAutoRotation(Transform actor)
    {
        if (actor == null) return;
        actor.GetComponentInChildren<NPCSimpleAnimator>(true)?.EnableAutoRotation();
    }

    /// <summary>Ver comentario de uso en Co_Sequence() — pausa la FSM (NPCBehaviourManagerV2) entera
    /// de un actor mientras dura un tramo cinemático, mismo patrón que EstelaAppearsSequencer /
    /// TabernaSequencer.SeatNPC. No hace nada si el actor no lleva ese componente (Will, que usa
    /// Invector, no NPCBehaviourManagerV2).</summary>
    private static void DisableActorBehaviour(Transform actor)
    {
        if (actor == null) return;
        var mgr = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (mgr != null) mgr.enabled = false;
    }

    private static void EnableActorBehaviour(Transform actor)
    {
        if (actor == null) return;
        var mgr = actor.GetComponentInChildren<Game.NPC.NPCBehaviourManagerV2>(true);
        if (mgr != null) mgr.enabled = true;
    }

    // ── Espera a la Fase 1 (gameplay) ────────────────────────────────────────

    private IEnumerator Co_WaitForCataclysmThreshold()
    {
        var health = _magoOscuroHealth != null ? _magoOscuroHealth : _magoOscuroActor?.GetComponent<Damageable>();
        if (health == null)
        {
            // No debería pasar tras ejecutar "El Sendero/Magia/Crear Hechizos del Mago Oscuro
            // (Batalla Final)" (ver punto 2 del pendiente en la cabecera) — Damageable se añade
            // solo al arrancar la escena en cuanto combatConfig está wireado. Si esto se dispara,
            // significa que ese menú no se ha ejecutado todavía sobre el prefab: se mantiene esta
            // red de seguridad por tiempo fijo para no bloquear el grafo narrativo mientras tanto.
            Debug.LogWarning("[MagoOscuroFinalBattleSequencer] No se encontró Damageable en el Mago Oscuro — " +
                "¿se ejecutó el menú 'El Sendero/Magia/Crear Hechizos del Mago Oscuro (Batalla Final)'? " +
                "De momento se usa un tiempo fijo de espera como red de seguridad.");
            yield return new WaitForSeconds(30f);
            yield break;
        }

        bool thresholdReached = false;
        System.Action<float> onDamaged = _ =>
        {
            if (health.Max > 0f && health.Current / health.Max <= _cataclysmHealthThreshold)
                thresholdReached = true;
        };
        health.OnDamaged += onDamaged;

        // Red de seguridad: si por lo que sea el jefe muere directamente en la Fase 1 sin llegar
        // nunca al umbral (jugador muy por encima de la dificultad esperada), no bloquear el
        // grafo esperando un umbral que ya no puede darse.
        bool died = false;
        System.Action onDied = () => died = true;
        health.OnDied += onDied;

        yield return new WaitUntil(() => thresholdReached || died);

        health.OnDamaged -= onDamaged;
        health.OnDied -= onDied;
    }

    // ── Fase E ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseE_Cataclysm()
    {
        _cinematicCamera?.Cut(_shotCataclysm);
        FeedbackService.CameraShake(0.4f, _cataclysmBuildupDuration);

        if (_cataclysmSweepVfx != null && _magoOscuroActor != null)
            VfxPoolService.Instance?.Play(_cataclysmSweepVfx, _magoOscuroActor.position, Quaternion.identity, _cataclysmBuildupDuration + 1f);

        yield return new WaitForSeconds(_cataclysmBuildupDuration);

        // Freeze justo antes del impacto (GDD: "El juego se congela justo antes del impacto").
        // FIX preventivo (ver advertencia de TDD §10 sobre SimpleCinematicDirector/slowMotion):
        // se restaura SIEMPRE en un finally más abajo, nunca se deja este método salir con
        // Time.timeScale distinto de 1 sin pasar por Co_PhaseF_TimeTutorial.
        Time.timeScale = _freezeTimeScale;
    }

    // ── Fase F ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseF_TimeTutorial()
    {
        // WaitForSecondsRealtime porque Time.timeScale sigue congelado desde la Fase E.
        if (SpeechBubbleUI.Instance != null)
        {
            bool done = false;
            SpeechBubbleUI.Instance.Show(_willActor, Loc(_timeTutorialTextKey), _tutorialDisplayDuration, () => done = true);
            yield return new WaitUntil(() => done); // el propio SpeechBubbleUI debe usar tiempo real para esto — confirmar en Editor
        }
        else
        {
            yield return new WaitForSecondsRealtime(_tutorialDisplayDuration);
        }
    }

    // ── Fase G ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseG_Rewind()
    {
        if (_rewindVfx != null && _willActor != null)
            VfxPoolService.Instance?.Play(_rewindVfx, _willActor.position, Quaternion.identity, _rewindDuration);

        yield return new WaitForSecondsRealtime(_rewindDuration);

        // Restaura el tiempo normal — Will "ya sabe" dónde caerá el golpe, se reposiciona
        // (cinemático: no hay movimiento libre real, ver cabecera) y lanza el contrahechizo.
        Time.timeScale = 1f;

        bool triggered = false;
        float elapsed = 0f;
        bool keyValid = System.Enum.IsDefined(typeof(Key), _counterSpellKey) && _counterSpellKey != Key.None;

        while (elapsed < _counterSpellWindowSeconds && !triggered)
        {
            if (keyValid && Keyboard.current != null && Keyboard.current[_counterSpellKey].wasPressedThisFrame)
                triggered = true;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_criticalCounterSpellVfx != null && _willActor != null && _magoOscuroActor != null)
        {
            VfxPoolService.Instance?.Play(_criticalCounterSpellVfx, _willActor.position, Quaternion.identity, 2f);
        }

        FeedbackService.CameraShake(0.6f, 0.3f);
    }

    // ── Fase H ────────────────────────────────────────────────────────────────

    private IEnumerator Co_PhaseH_BetrayalAndSacrifice()
    {
        _cinematicCamera?.Cut(_shotLiamSacrifice);

        // Traición: Liam y Estela reaccionan con enfado al golpe del Mago Oscuro (pedido por
        // Raúl, 30/08/2026) — mismo patrón NPCEmotionController.SetEmotion() que el resto de
        // sequencers (ver cabecera, sección CARAS). Se dejan así (no hay reset a Neutral después)
        // porque la escena 21 (WillSacrificeSequencer) continúa directamente desde aquí y ambos
        // siguen con motivos para seguir enfadados/afectados.
        _estelaEmotion?.SetEmotion(NPCEmotion.Angry);
        _liamEmotion?.SetEmotion(NPCEmotion.Angry);

        if (_betrayalStrikeVfx != null && _magoOscuroActor != null && _willActor != null)
        {
            var strike = Instantiate(_betrayalStrikeVfx, _magoOscuroActor.position, Quaternion.identity);
            yield return Co_TravelToPoint(strike.transform, _magoOscuroActor.position, _willActor.position, 0.6f);
        }

        // El foco de cámara y narrativo está en Liam, no en el golpe en sí — ver guion técnico.
        FaceTarget(_liamActor, _willActor);
        yield return ShowBubblePaged(_liamActor, Loc(_liamLastWordsTextKey), _sacrificeHoldDuration,
            animTrigger: "Beg01", speakerName: "Liam");

        yield return new WaitForSeconds(1f);
    }

    /// <summary>
    /// Mismo patrón que PrologueDreamSequencer.Co_TravelToPoint (VFX que viaja de un punto a otro
    /// en un tiempo dado) — duplicado aquí en vez de compartir código porque PrologueDreamSequencer
    /// lo tiene como método privado no reutilizable entre clases; si se quiere evitar la
    /// duplicación, promover ambos a un helper estático común en una pasada futura.
    /// </summary>
    private static IEnumerator Co_TravelToPoint(Transform vfx, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (vfx == null) yield break;
            vfx.position = Vector3.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (vfx != null) vfx.position = to;
    }

    /// <summary>
    /// Camina en línea recta hacia targetPos, girando SUAVEMENTE hacia la dirección de avance en
    /// cada frame — sin NavMesh ni PlayerLockService. Ver comentario en Co_PhaseA_Appearance()
    /// (FIX 31/08/2026, ronda 15) para el porqué.
    /// Desactiva el NavMeshAgent del actor mientras dura (si no, pelearía por la posición del
    /// Transform e intentaría arrastrarlo de vuelta a su último destino conocido) y lo vuelve a
    /// habilitar al terminar con NavMeshAgentUtility.SafeEnable() — mismo helper que ya usa el
    /// resto del proyecto para evitar el warning "no hay NavMesh válido" al reactivar un agente
    /// tras moverlo a mano.
    /// </summary>
    private static IEnumerator Co_SimpleWalkTo(Transform actor, NPCSimpleAnimator anim,
        UnityEngine.AI.NavMeshAgent agent, Vector3 targetPos, float duration)
    {
        if (actor == null) yield break;

        bool agentWasEnabled = agent != null && agent.enabled;
        if (agentWasEnabled)
            agent.enabled = false;

        Vector3 startPos = actor.position;
        anim?.TransitionToLocomotion();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            actor.position = Vector3.Lerp(startPos, targetPos, t);

            Vector3 faceDir = targetPos - actor.position;
            faceDir.y = 0f;
            if (faceDir.sqrMagnitude > 0.001f)
                actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(faceDir), Time.deltaTime * 6f);

            anim?.SetMovementSpeed(1f);
            yield return null;
        }

        actor.position = targetPos;
        anim?.SetMovementSpeed(0f);
        anim?.TransitionToIdle();

        if (agentWasEnabled)
            Game.NPC.Common.NavMeshAgentUtility.SafeEnable(agent, actor, actor.position);
    }

    protected override void OnSkipCleanup()
    {
        // Restaura Time.timeScale si se salta la cinemática a mitad de la Fase E/F/G — mismo
        // riesgo que TDD §10 avisa sobre SimpleCinematicDirector.
        Time.timeScale = 1f;
        if (_willAuraInstance != null) Destroy(_willAuraInstance);
    }
}
