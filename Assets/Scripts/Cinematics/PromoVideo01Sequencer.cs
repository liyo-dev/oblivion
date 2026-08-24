using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Sendero.Core.Feedback;

/// Orquestador del VÍDEO PROMO #1 ("en personaje": Estela, Liam y Will hablando a cámara para
/// redes/itch.io). Vive en la escena de estudio que genera
/// Assets/Scripts/Editor/PromoStudioSceneBuilder.cs ("El Sendero → Marketing → Crear Escena de
/// Estudio (Vídeos Promo)"), que además crea y enlaza este componente con sus referencias.
///
/// Usa el MISMO sistema de cinemáticas que el resto del juego (CinematicSequencerBase): bloqueo de
/// input/HUD, CinematicCameraDriver para los planos, SpeechBubbleUI para el diálogo y las señales
/// genéricas de DefaultNarrativeSignals para entrar/salir. Es una secuencia más simple que las del
/// juego real: sin combate, sin minijuego, sin NavMeshAgent — solo 3 personajes hablando, cortes de
/// cámara y un gag de sonido.
///
/// Señal de entrada: "PROMO_VIDEO_01_START" (valor por defecto que deja el builder; editable en el
/// Inspector). Señal de salida: "PROMO_VIDEO_01_END". Son claves genéricas del bus de señales, NO
/// tocan el grafo narrativo real ni ninguna quest — nada más en el proyecto las escucha.
///
/// CÓMO PROBARLO: abrir Assets/Scenes/Marketing/PromoEstudio.unity, dar Play, seleccionar el
/// GameObject "PromoVideo01_Sequencer" en la Hierarchy y, en el Inspector, click derecho sobre la
/// cabecera del componente → "Simular secuencia" (ContextMenu heredado de CinematicSequencerBase,
/// que levanta la señal de entrada directamente). No hace falta ningún sistema de preview propio.
///
/// ── ESTRUCTURA DE LA SECUENCIA ──────────────────────────────────────────────────────────────
///   PARTE 1 (Presentación) — plano solo de Estela: se presenta (reverencia teatral), le ruge el
///     estómago (gag con el SFX real "Taberna_StomachRumble" + camera shake, el mismo que usa
///     TabernaSequencer), busca culpable, nadie responde, se recompone.
///   PARTE 2 (Comedia) — pan al plano medio Estela+Liam: Liam le roba la frase, se pican, Liam
///     señala fuera de plano ("Falta uno"), los dos dicen "...Will." y un whip-pan rápido
///     (MoveTo con Ease.Linear y duración muy corta, no un corte seco) descubre a Will de
///     espaldas practicando; Will se gira pillado; corte/contracorte para el remate.
///   PARTE 3 (Promo) — corte al plano de grupo (los 3 recolocados en sus marcas de grupo justo en
///     el frame del corte, así el reposicionamiento es invisible): frase encadenada de los tres y
///     se activa el panel de CTA ("DEMO GRATIS · itch.io"), que se queda fijo en pantalla el resto
///     del vídeo (no se oculta hasta el cierre definitivo).
///   LOGO — última acotación del guion ("[Fundido a negro. Logo del juego + enlace de itch.io.]"):
///     fundido a negro y, sobre ese negro, el logo del juego + texto (ver Co_LogoFinal()).
///   COLETILLA — corte de vuelta al plano de grupo (la cámara nunca se movió mientras estaba
///     tapada) para la última puya de Estela ("Si Liam os cae mal aquí...") y la réplica resignada
///     de Liam, movidas aquí DESPUÉS del logo a petición de Raúl (antes iban justo antes del
///     fundido) — ver Co_ColetillaTrasLogo(). Cierra con un segundo fundido a negro, definitivo.
///
/// ── DESVIACIÓN DELIBERADA RESPECTO AL GUION ESCRITO ────────────────────────────────────────
///   En el guion, la línea conjunta "...Will." va DESPUÉS del whip-pan. Aquí se dice ANTES, con la
///   cámara todavía en el plano de Estela+Liam, y el whip-pan viene justo detrás. Motivo técnico:
///   SpeechBubbleUI ancla el bocadillo sobre el Transform de quien habla, y ese bocadillo es de
///   Estela ("Estela y Liam"); si la cámara ya estuviera sobre Will, el bocadillo quedaría fuera
///   de encuadre o pegado al borde. El remate cómico se mantiene igual ("Falta uno" → "...Will."
///   → zas, ahí está). Si se prefiere el orden literal, basta con mover el bloque del whip-pan por
///   encima de la línea conjunta en Co_Sequence().
///
/// ── OJO CON LAS CLAVES DE ANIMACIÓN ────────────────────────────────────────────────────────
///   Los 3 personajes de la escena de estudio se instancian en modo "solo visual"
///   (PromoStudioSceneBuilder.StripToVisualOnly): se les destruyen casi TODOS los MonoBehaviour, así
///   que NO tienen NPCSimpleAnimator — solo sobreviven el Animator y NPCEmotionController (este
///   último por la allowlist PreservedBehaviourTypes del builder, ver más abajo). Eso implica que
///   las claves de animación de aquí abajo se resuelven al final como Animator.Play(estado), es
///   decir, deben ser NOMBRES DE ESTADO del Base Layer del Animator Controller de cada personaje,
///   NO nombres de parámetro Trigger.
///
///   Los 3 prefabs comparten el mismo controller de cuerpo:
///   Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller.
///   Los valores por defecto de aquí abajo son estados REALES de su Base Layer (layer 0).
///
///   CORRECCIÓN (23 ago 2026, tras aviso de Raúl "recuerda que tenemos varias de hablar"): la lista
///   de estados válidos que se documentaba aquí antes era INCOMPLETA — se obtuvo con una búsqueda de
///   texto simple que no seguía el grafo real del state machine (que usa fileIDs NEGATIVOS para
///   varios estados, entre ellos Talk01/02/03 y Greeting01_NoWeapon, y una búsqueda de texto ingenua
///   los pasaba por alto). Re-verificado ahora recorriendo de verdad el grafo del Base Layer
///   (AnimatorController → m_AnimatorLayers[0].m_StateMachine → m_ChildStates, con fileIDs negativos
///   incluidos) — el Base Layer tiene en realidad 76 estados, bastantes más "de hablar"/sociales de
///   lo que se pensaba: Angry01, Angry02, Attack1, Attack2, Attack3, Attack4, Beg01, Cheer01, Cheer02,
///   ClimbDown_RM_NoWeapon, ClimbUp_RM_NoWeapon, Cry01, Dance_NoWeapon, DefendHit_NoWeapon,
///   Defend_NoWeapon, Die02_NoWeapon, Dizzy_NoWeapon, DrinkPotion_NoWeapon, Eat_Begin, Eat_Loop,
///   Falling, Fear01, FoundSomething_NoWeapon, Free Locomotion, Greeting01_NoWeapon, HandClap01,
///   HandWave01, HandWave02, HeadNod01, HeadShake01, HeadShake02, Idle01, Idle02, Idle03,
///   IdleWounded01, InteractWithGateObject_NoWeapon, InteractWithPeople_NoWeapon, Jump, JumpMove,
///   LandLow, Laugh01, LevelUp_NoWeapon, Pain01, Push_InPlace_NoWeapon, Question01, Question02,
///   Reverence01, RollBWD/FWD/LFT/RGT_Battle_RM_NoWeapon, SenseSomethingStart_NoWeapon,
///   SitGround/High/Low/Medium_Begin/Exit/Loop, Sleeping_NoWeapon, Strafing Movement,
///   Swimming_Floating_NoWeapon, TakeDamage, TakeDamage_2, Talk01, Talk02, Talk03, Victory_NoWeapon,
///   fly_dive, fly_idle. **Talk01/02/03 SÍ son estados reales de "hablar" — se usan ya abajo para dar
///   variedad en vez de repetir InteractWithPeople_NoWeapon en cada línea genérica.**
///   La UpperBody layer (índice 1, no se usa aquí) tiene su propia lista, mucho más corta.
///   Vacío = no se dispara nada, la línea se ve igual, solo sin gesto.
///
/// ── GESTOS QUE NO SE QUEDAN "IDLE" A MITAD DE LÍNEA ────────────────────────────────────────
///   Casi todos los gestos del Base Layer duran 1-2s, pero un bocadillo dura 3-6s: sin gestión
///   propia, el Animator termina el clip y transiciona solo hacia el Idle de locomoción mientras
///   el texto sigue en pantalla (bug reportado por Raúl: "Estela hace su presentación pero el
///   texto sigue en pantalla y ella se queda idle"). Los personajes de esta escena están saneados
///   a "solo visual" (StripToVisualOnly) y NO tienen NPCSimpleAnimator, así que ni siquiera pasan
///   por el mecanismo que sí resuelve esto en los NPC reales (NPCSimpleAnimator.BeginInteraction()
///   deja al NPC en su interactState — "InteractWithPeople_NoWeapon" por defecto — durante toda la
///   interacción, y PlayBodyEmotion() vuelve ahí tras cada gesto puntual en vez de caer a Idle).
///   El helper ReproducirGesto() de aquí abajo replica exactamente ese patrón a mano:
///     · mantenerConversando: true (todo gesto atado a una línea, vía Co_Linea) — al terminar el
///       clip del gesto, pasa a EstadoConversando ("InteractWithPeople_NoWeapon", la misma pose
///       de "hablando" en bucle que usan los NPC reales) y se queda ahí hasta el siguiente gesto.
///     · mantenerConversando: false (reacciones sueltas sin bocadillo — el rugido, la pose de
///       práctica de Will, su "pillado") — caer en la pose de "hablando" no pega narrativamente
///       si nadie está conversando con nadie todavía, así que en su lugar se CONGELA en el último
///       frame del propio gesto (Play a normalizedTime≈1 + Animator.speed=0) hasta que la
///       siguiente llamada explícita lo reemplace.
///   SpeechBubbleUI.Show() ya NO recibe animTrigger desde aquí (siempre null): gestionar el gesto
///   nosotros mismos evita que su propio fallback (un Animator.Play() de un solo disparo, sin
///   este manejo) compita con ReproducirGesto() por el mismo Animator.
///
///   COMPLEMENTO (23 ago 2026, pedido de Raúl): lo anterior resuelve que NO se caiga a Idle mientras
///   SÍ hay bocadillo — pero el reverso también era un problema: EstadoConversando se quedaba puesto
///   indefinidamente hasta el SIGUIENTE gesto de ESE personaje, así que alguien podía seguir con la
///   pose de "hablando" mucho después de que su propio bocadillo se cerrara, mientras otro personaje
///   tenía la palabra. Co_Linea() ahora llama a EntrarEnEstadoAtento() justo al cerrarse su propio
///   bocadillo: pasa a EstadoAtento ("Free Locomotion", el blend tree de reposo del propio Base
///   Layer — la postura neutra de pie, ni hablando ni haciendo nada especial) hasta que le vuelva a
///   tocar. PonerTodosAtentos() arranca los 3 ahí al empezar cada pasada, por si acaso.
///
/// ── CARAS (NPCEmotionController) ───────────────────────────────────────────────────────────
///   Además del gesto de cuerpo, cada línea cambia la expresión facial con
///   NPCEmotionController.SetEmotion(), exactamente el mismo patrón que ya usan TabernaSequencer y
///   LiamCrystalBallSequencer con los NPC del juego real. Las referencias las enlaza el builder
///   (que además se encarga de coger, en el caso de Will, el NPCEmotionController que sí tiene
///   EmotionProfile asignado — su prefab lleva dos). Todo va con '?.': si alguna referencia falta,
///   la secuencia sigue igual, solo sin cambio de cara.
///
/// ── REACCIÓN DE QUIEN ESCUCHA (ronda 8, 24 ago 2026) ───────────────────────────────────────
///   Petición de Raúl: que un personaje sin bocadillo activo no se quede plantado en EstadoAtento
///   mientras el otro habla — que gire a mirarlo y suelte un gesto corto de "me he dado cuenta/estoy
///   atento" (p.ej. SenseSomethingStart_NoWeapon), igual que haría alguien escuchando de verdad.
///   Nuevo helper ReaccionOyente() (ver más abajo): generaliza a un solo sitio el mismo patrón que ya
///   usaban _animLiamReaccionPulla y _animWillPillado (ReproducirGesto con mantenerConversando:false,
///   se reproduce una vez y se congela en el último frame — no "conversando", solo reaccionando),
///   añadiéndole el giro hacia quien habla (FaceTarget, ya existente en CinematicSequencerBase).
///   De momento cableado en los 2 beats de la PARTE 2 donde Liam le roba/contesta la frase a
///   Estela (ver Co_Parte2_Comedia) — son los que más se benefician del gag ("¿pero qué...?" antes
///   de su propio "¡Oye!"). No se ha tocado ningún otro beat: es una mejora selectiva, no un rediseño
///   de toda la secuencia, para no arriesgar el timing cómico ya afinado en las 7 rondas anteriores.
///   CONFIRMADO EN VÍDEO (24 ago 2026, "promo buena 1.mp4"): ambos gestos se leen bien — alerta con
///   los brazos cruzados (SenseSomethingStart_NoWeapon) y escepticismo con las manos alzadas
///   (Question02) — y el giro no se nota brusco porque coincide con el pan de cámara.
///
///   RONDA 9 (24 ago 2026): mismo tratamiento aplicado a la reacción muda de Liam a la última puya de
///   Estela en Co_ColetillaTrasLogo() — gira a mirarla al oírla (ReaccionOyente) y luego, justo antes
///   de su propia línea, se le devuelve la cara hacia el plano de grupo (FaceTarget) para no
///   contradecir el "(sin mirarla, resignado)" del guion en SU línea — ver el comentario en el propio
///   Co_ColetillaTrasLogo() para el porqué.
///
///   RONDA 10 (24 ago 2026, pasada completa a petición de Raúl — "termina de afinarlo con todas las
///   reacciones"): revisados TODOS los beats de la secuencia buscando más huecos de "alguien habla,
///   nadie reacciona". Único hueco real encontrado: Estela no giraba hacia Liam en Liam04 ("Por
///   cierto. Falta uno."), justo antes de la línea conjunta que descubre a Will — añadido con
///   ReaccionOyente(..., animReaccion: null), es decir SOLO el giro, sin gesto nuevo (el beat dura
///   2.2s y enseguida vuelve a girar hacia Will, así que un gesto completo se habría visto recargado
///   y habría competido con el propio gesto de Liam). El resto de beats candidatos se descartaron
///   A PROPÓSITO por chocar con una acotación explícita del guion: Estela04 "¡Oye!" (Liam
///   "ignorándola" — no debe reaccionar), Liam03 "Funciona." (Liam "sin inmutarse" — el chiste es
///   que NO reacciona a la pulla de Estela05), y toda la PARTE 3 (los 3 se dirigen a cámara/audiencia
///   en bloque, no entre ellos — añadir reacciones ahí rompería el efecto de "equipo unido").
///
///   Para qué gesto elegir en cada caso (y qué aspecto tiene cada estado del Base Layer), ver el
///   catálogo de animaciones del proyecto (documento "Catálogo de animaciones — Base Layer Invector"
///   en el Proyecto de Claude) — reúne, con confirmación visual siempre que la hay, qué pinta tiene
///   cada estado usado hasta ahora en esta serie de vídeos, para no tener que redescubrirlo cada vez.
[DisallowMultipleComponent]
public class PromoVideo01Sequencer : CinematicSequencerBase
{
    // ── Guion (texto literal, en español, sin pasar por Loc()) ──────────────────────────────────
    // Es un vídeo de marketing puntual, no contenido de juego localizable: va directo en código
    // para no ensuciar las tablas de localización con claves de un solo uso.

    private const string TextoEstela01 =
        "Hola. Soy Estela, la hechicera más poderosa de todo el Reino. La de mejor puntería, la de más daño... y sin duda la de mejor pelo.";
    private const string TextoEstela02 = "¿Quién ha sido?";
    private const string TextoEstela03 =
        "Como decía. Estoy aquí para hablaros de un juego increíble llamado...";

    private const string TextoLiam01 =
        "...El Sendero de las Estrellas. Lo digo yo, que soy el único aquí capaz de terminar una frase.";
    private const string TextoEstela04 = "¡Oye!";
    private const string TextoLiam02 = "Soy Liam. El cerebro del grupo.";
    private const string TextoEstela05 = "El cerebro que se cree muy misterioso con esa cara seria.";
    private const string TextoLiam03 = "Funciona.";
    private const string TextoLiam04 = "Por cierto. Falta uno.";
    private const string TextoConjunta = "...Will.";

    private const string TextoWill01 = "Eh... hola. Soy Will. Y no, no estaba ensayando nada.";
    private const string TextoEstela06 = "Estaba ensayando.";
    private const string TextoWill02 = "Estaba ensayando.";

    private const string TextoWill03 = "Somos los protagonistas de El Sendero de las Estrellas...";
    private const string TextoLiam05 = "...un RPG de acción y aventura...";
    private const string TextoEstela07 = "...¡y la demo es gratis!";
    private const string TextoEstela08 = "Si Liam os cae mal aquí... esperad a veros el juego.";
    private const string TextoLiam06 = "Voy a fingir que no he oído eso.";

    // Nombres que se pasan a SpeechBubbleUI como speakerName. Hoy el bocadillo no los pinta (ver el
    // comentario del propio parámetro en SpeechBubbleUI.Show), pero todas las llamadas del proyecto
    // lo pasan igualmente y aquí se mantiene la convención.
    private const string NombreEstela = "Estela";
    private const string NombreLiam = "Liam";
    private const string NombreWill = "Will";
    private const string NombreConjunto = "Estela y Liam";

    /// Clave de evento de audio del rugido de estómago. Es el MISMO gag (y el mismo SFX ya
    /// existente) que dispara TabernaSequencer.Co_StomachRumble() — se reutiliza tal cual.
    private const string SfxRugidoEstomago = "Taberna_StomachRumble";

    // ── Personajes ──────────────────────────────────────────────────────────────────────────────

    [Header("Personajes (los enlaza el builder de la escena)")]
    [Tooltip("Raíz de Estela en la escena de estudio ('Personajes_Estudio/Estela').")]
    [SerializeField] private Transform _estelaTransform;
    [Tooltip("Raíz de Liam en la escena de estudio ('Personajes_Estudio/Liam').")]
    [SerializeField] private Transform _liamTransform;
    [Tooltip("Raíz de Will en la escena de estudio ('Personajes_Estudio/Will').")]
    [SerializeField] private Transform _willTransform;

    [Header("Animators (opcionales — solo para gestos fuera de bocadillo)")]
    [Tooltip("Si se deja vacío se resuelve solo en Awake con GetComponentInChildren<Animator>() sobre el Transform del personaje.")]
    [SerializeField] private Animator _estelaAnimator;
    [SerializeField] private Animator _liamAnimator;
    [SerializeField] private Animator _willAnimator;

    [Header("Caras / expresiones (los enlaza el builder de la escena)")]
    [Tooltip("NPCEmotionController de cada personaje: es el que intercambia los meshes de ojos y boca " +
             "según la emoción. Sobrevive al saneado 'solo visual' gracias a la allowlist " +
             "PreservedBehaviourTypes de PromoStudioSceneBuilder. Si se deja vacío, esa cara " +
             "simplemente no cambia (todas las llamadas van con '?.').")]
    [SerializeField] private NPCEmotionController _estelaEmotion;
    [SerializeField] private NPCEmotionController _liamEmotion;
    [Tooltip("OJO: _WILL.prefab lleva DOS NPCEmotionController en el mismo GameObject y solo uno tiene " +
             "EmotionProfile asignado. El builder enlaza el correcto; si se reasigna a mano, comprobar " +
             "que el que se elige tiene perfil o la cara de Will no cambiará.")]
    [SerializeField] private NPCEmotionController _willEmotion;

    // ── Planos de cámara ────────────────────────────────────────────────────────────────────────

    [Header("Planos de cámara (GameObjects con CinematicShot, los crea el builder)")]
    [Tooltip("PARTE 1 — plano medio corto solo de Estela, de frente.")]
    [SerializeField] private Transform _shotEstelaSolo;
    [Tooltip("PARTE 2 — plano medio con Estela y Liam los dos en cuadro.")]
    [SerializeField] private Transform _shotEstelaLiam;
    [Tooltip("PARTE 2 — plano de revelación de Will (destino del whip-pan).")]
    [SerializeField] private Transform _shotRevelacionWill;
    [Tooltip("PARTE 3 — plano final de grupo, los 3 de frente a cámara.")]
    [SerializeField] private Transform _shotGrupoFinal;

    [Header("Marcas de blocking del plano de grupo (opcionales)")]
    [Tooltip("Posiciones a las que se recolocan los personajes justo en el frame del corte al plano " +
             "de grupo (el corte oculta el salto). Si se dejan vacías, cada uno se queda donde estaba " +
             "y el plano de grupo tendrá que encuadrarlos así de separados.")]
    [SerializeField] private Transform _marcaGrupoEstela;
    [SerializeField] private Transform _marcaGrupoLiam;
    [SerializeField] private Transform _marcaGrupoWill;

    // ── Duraciones por línea ────────────────────────────────────────────────────────────────────
    // Valores de partida razonables para la longitud de cada frase. AJUSTAR A MANO al montar el
    // vídeo: son lo que marca el ritmo cómico y no hay forma de acertarlos sin verlo reproducido.

    [Header("PARTE 1 — Presentación (duraciones en segundos)")]
    [Tooltip("Aire antes de que Estela empiece a hablar, con el plano ya revelado.")]
    [SerializeField] private float _holdInicial = 0.6f;
    [SerializeField] private float _estela01Duracion = 6.0f;
    [Tooltip("Silencio justo después del rugido, antes de que Estela busque culpable.")]
    [SerializeField] private float _pausaTrasRugido = 0.8f;
    [SerializeField] private float _estela02Duracion = 2.0f;
    [Tooltip("El silencio incómodo en el que nadie responde. Es el chiste — no lo dejes corto.")]
    [SerializeField] private float _pausaSilencioIncomodo = 1.4f;
    [SerializeField] private float _estela03Duracion = 4.0f;

    [Header("PARTE 1 — Gag del rugido de estómago")]
    [Tooltip("Intensidad del camera shake que acompaña al rugido (mismo gag que TabernaSequencer).")]
    [SerializeField] private float _shakeRugidoIntensidad = 0.15f;
    [SerializeField] private float _shakeRugidoDuracion = 0.4f;

    [Header("PARTE 2 — Comedia (duraciones en segundos)")]
    [Tooltip("Duración del pan del plano de Estela al plano medio Estela+Liam.")]
    [SerializeField] private float _panAEstelaLiamDuracion = 0.8f;
    [SerializeField] private float _liam01Duracion = 5.0f;
    [SerializeField] private float _estela04Duracion = 1.2f;
    [SerializeField] private float _liam02Duracion = 2.6f;
    [SerializeField] private float _estela05Duracion = 3.4f;
    [SerializeField] private float _liam03Duracion = 1.4f;
    [Tooltip("Tensión cómica entre 'Funciona.' y 'Por cierto. Falta uno.'")]
    [SerializeField] private float _pausaTensionComica = 0.9f;
    [SerializeField] private float _liam04Duracion = 2.2f;
    [SerializeField] private float _conjuntaDuracion = 1.6f;

    [Header("PARTE 2 — Whip-pan y revelación de Will")]
    [Tooltip("Duración del whip-pan. Muy corta a propósito: se busca un giro brusco de cámara, no un " +
             "movimiento elegante. Con Ease.Linear y ~0.15s se lee como un latigazo.")]
    [SerializeField] private float _whipPanDuracion = 0.15f;
    [Tooltip("Tiempo que se ve a Will de espaldas practicando antes de que se dé cuenta y se gire.")]
    [SerializeField] private float _holdWillDeEspaldas = 0.8f;
    [Tooltip("Beat entre que Will se gira con cara de susto (Surprised) y empieza a hablar con cara de " +
             "apuro (Scared). Sin esta pausa las dos expresiones caerían en el mismo frame y la de " +
             "susto no llegaría a verse.")]
    [SerializeField] private float _pausaWillPillado = 0.5f;
    [SerializeField] private float _will01Duracion = 4.0f;
    [SerializeField] private float _estela06Duracion = 1.8f;
    [SerializeField] private float _will02Duracion = 2.0f;

    [Header("PARTE 3 — Promo (duraciones en segundos)")]
    [SerializeField] private float _will03Duracion = 3.2f;
    [SerializeField] private float _liam05Duracion = 2.4f;
    [SerializeField] private float _estela07Duracion = 2.2f;
    [SerializeField] private float _estela08Duracion = 3.8f;
    [SerializeField] private float _liam06Duracion = 2.8f;
    [Tooltip("Segundos que se ve el CTA solo (sin bocadillos) antes de cortar al fundido a negro + logo.")]
    [SerializeField] private float _holdCtaFinal = 3.5f;
    [Tooltip("Segundos que se mantiene la coletilla de Estela/Liam en pantalla (tras el logo) antes " +
             "del fundido a negro definitivo.")]
    [SerializeField] private float _holdTrasColetillaFinal = 1f;

    // ── Claves de animación ─────────────────────────────────────────────────────────────────────

    // Valores por defecto = estados REALES del Base Layer de Invector@BasicLocomotion (el controller
    // que comparten los 3 prefabs), verificados contra el .controller recorriendo de verdad el grafo
    // del state machine (ver la corrección en la sección "OJO CON LAS CLAVES DE ANIMACIÓN" de la
    // cabecera — el Base Layer tiene bastantes más gestos utilizables de lo que se pensaba, 76 en
    // total). Solo _animLiam04 se queda como aproximación real (no hay estado de "señalar").

    [Header("Claves de animación — revisar a ojo (ya rellenas, ver cabecera del script)")]
    [Tooltip("NOMBRE DE ESTADO del Base Layer del Animator Controller de Estela (no un Trigger). " +
             "Vacío = sin gesto en esta línea. Intro presumiendo: reverencia teatral de presentación.")]
    [SerializeField] private string _animEstela01 = "Reverence01";
    [Tooltip("Gesto de reacción al rugido de estómago (no va asociado a ningún bocadillo). " +
             "Question02 = '¿pero qué ha sido eso?'.")]
    [SerializeField] private string _animEstelaRugido = "Question02";
    [Tooltip("'¿Quién ha sido?' — mira alrededor buscando culpable.")]
    [SerializeField] private string _animEstela02 = "SenseSomethingStart_NoWeapon";
    [Tooltip("'Como decía...' — Talk01 es un estado real de 'hablar' del Base Layer (ver corrección en " +
             "la cabecera del script). Se reparten Talk01/02/03 entre las distintas líneas genéricas " +
             "de 'hablar' para no repetir siempre el mismo gesto.")]
    [SerializeField] private string _animEstela03 = "Talk01";
    [Tooltip("'¡Oye!' — negación indignada.")]
    [SerializeField] private string _animEstela04 = "HeadShake01";
    [Tooltip("REACCIÓN DE OYENTE (ronda 8): gesto de Estela al notar que Liam le roba la frase en " +
             "Liam01 — antes de que le dé tiempo a decir '¡Oye!'. SenseSomethingStart_NoWeapon = " +
             "'un momento, ¿qué...?'. Se reproduce una vez y se congela (ver ReaccionOyente()).")]
    [SerializeField] private string _animEstelaReaccionLiam01 = "SenseSomethingStart_NoWeapon";
    [Tooltip("REACCIÓN DE OYENTE (ronda 8): gesto de Estela mientras escucha a Liam presentarse en " +
             "Liam02 ('Soy Liam. El cerebro del grupo.'), justo antes de picarle en Estela05. " +
             "Question02 = gesto escéptico/interrogativo, para variar del gesto de la línea anterior.")]
    [SerializeField] private string _animEstelaReaccionLiam02 = "Question02";
    [Tooltip("Pica a Liam por su cara de misterio.")]
    [SerializeField] private string _animEstela05 = "Laugh01";
    [Tooltip("Aparte cómplice 'Estaba ensayando.' — repite Laugh01 a propósito: no hay ningún " +
             "asentimiento (HeadNod) en el Base Layer y la guasa es la misma que en la línea de antes.")]
    [SerializeField] private string _animEstela06 = "Laugh01";
    [Tooltip("'...¡y la demo es gratis!' — celebración con los brazos arriba (no hay Cheer01 como estado).")]
    [SerializeField] private string _animEstela07 = "LevelUp_NoWeapon";
    [Tooltip("Última puya a Liam.")]
    [SerializeField] private string _animEstela08 = "HeadShake02";
    [Tooltip("Línea conjunta '...Will.' — 'ahí está el que falta'.")]
    [SerializeField] private string _animConjunta = "FoundSomething_NoWeapon";

    [Tooltip("NOMBRE DE ESTADO del Base Layer del Animator Controller de Liam (no un Trigger). " +
             "Entrada robándole la frase a Estela: reverencia de listillo.")]
    [SerializeField] private string _animLiam01 = "Reverence01";
    [Tooltip("'Soy Liam. El cerebro del grupo.' — se presenta. Talk02 en vez de Talk01/InteractWithPeople " +
             "para que no repita el mismo gesto que la línea anterior de Estela.")]
    [SerializeField] private string _animLiam02 = "Talk02";
    [Tooltip("'Funciona.' — se queda impasible con Idle03 de relleno neutro: el chiste es precisamente " +
             "que no se inmuta ante la pulla de Estela.")]
    [SerializeField] private string _animLiam03 = "Idle03";
    [Tooltip("'Por cierto. Falta uno.' — APROXIMACIÓN: no hay ningún estado de 'señalar' en este Base " +
             "Layer; Question02 es lo más cercano (gesto interrogativo con la mano). Merece una " +
             "animación de señalar fuera de plano si algún día la tienen.")]
    [SerializeField] private string _animLiam04 = "Question02";
    [Tooltip("Línea del grupo '...un RPG de acción y aventura...'. Talk03 para variar frente a las " +
             "otras líneas de 'hablar' del vídeo.")]
    [SerializeField] private string _animLiam05 = "Talk03";
    [Tooltip("Reacción MUDA de Liam mientras Estela dice 'Si Liam os cae mal aquí...' — no va asociada " +
             "a ningún bocadillo propio (Liam no habla en este beat, solo reacciona; su réplica " +
             "'Voy a fingir que no he oído eso' es la línea siguiente). Se lanza con ReproducirGesto() " +
             "igual que el rugido o la pose de Will, así que aunque el clip sea en bucle (como " +
             "Greeting01_NoWeapon) se reproduce solo una vez y se congela en el último frame en vez de " +
             "repetirse todo el vídeo.")]
    [SerializeField] private string _animLiamReaccionPulla = "Greeting01_NoWeapon";
    [Tooltip("'Voy a fingir que no he oído eso.'")]
    [SerializeField] private string _animLiam06 = "HeadShake02";

    [Tooltip("NOMBRE DE ESTADO del Base Layer del Animator Controller de Will (no un Trigger). " +
             "Pose de práctica con la espada que hace de espaldas antes de ser descubierto. " +
             "CORREGIDO (23 ago 2026): Attack2 SÍ es un estado real del Base Layer (la verificación " +
             "anterior que decía lo contrario era incompleta, ver corrección en la cabecera del " +
             "script) y es un ataque de espada de verdad, mejor encaje que la pose de guardia " +
             "Defend_NoWeapon que se usaba antes — pendiente de revisión visual igualmente.")]
    [SerializeField] private string _animWillPracticando = "Attack2";
    [Tooltip("Reacción de 'me han pillado' justo al girarse.")]
    [SerializeField] private string _animWillPillado = "Fear01";
    [Tooltip("'Eh... hola. Soy Will...' — Talk01, mismo estado real de 'hablar' que usa Estela03 (no " +
             "pasa nada por repetirlo entre personajes distintos que no hablan seguidos).")]
    [SerializeField] private string _animWill01 = "Talk01";
    [Tooltip("'Estaba ensayando.' — el eco resignado, negando sin convicción.")]
    [SerializeField] private string _animWill02 = "HeadShake01";
    [Tooltip("Línea del grupo 'Somos los protagonistas...' — saludo a cámara.")]
    [SerializeField] private string _animWill03 = "HandWave02";

    // ── CTA ─────────────────────────────────────────────────────────────────────────────────────

    [Header("Llamada a la acción")]
    [Tooltip("Panel de UI con el texto 'DEMO GRATIS · itch.io'. Se construye aparte y se asigna aquí. " +
             "Debe estar DESACTIVADO en la escena: la secuencia lo activa al llegar a la frase de la " +
             "demo y lo vuelve a desactivar al cerrar. Opcional — si se deja vacío no pasa nada.")]
    [SerializeField] private GameObject _ctaPanel;

    // ── Logo final ──────────────────────────────────────────────────────────────────────────────
    // Última acotación del guion, sin implementar hasta ahora: "[Fundido a negro. Logo del juego +
    // enlace de itch.io.]". A diferencia del CTA de arriba, esta tarjeta de cierre NO hay que
    // construirla a mano en la escena: se construye sola en runtime (mismo patrón que
    // TitleLogoController.BuildUI(), ver EnsureLogoCard()) la primera vez que se necesita, así que
    // basta con tener el script compilado y (opcionalmente) el sprite asignado.

    [Header("Logo final")]
    [Tooltip("Sprite del logo del juego para la tarjeta de cierre. El builder lo autoasigna desde " +
             "'Assets/Art/UI/Menu/logo sendero 4.png' (el mismo sprite que usa LogoTitulo en " +
             "MainMenu.unity) si se deja vacío — reejecutar el menú del builder lo rellena solo.")]
    [SerializeField] private Sprite _logoSprite;
    [Tooltip("Texto bajo el logo en la tarjeta de cierre.")]
    [SerializeField] private string _logoLinkTexto = "Demo gratis · itch.io";
    [Tooltip("Duración del fundido a negro, antes de que aparezca el logo (primer beat del guion).")]
    [SerializeField] private float _logoFadeANegroDuracion = 0.6f;
    [Tooltip("Duración del fundido de entrada del logo/texto una vez la pantalla ya está en negro " +
             "(segundo beat del guion).")]
    [SerializeField] private float _logoFadeInDuracion = 0.6f;
    [Tooltip("Cuánto se queda el logo en pantalla, ya visible del todo, antes de terminar la secuencia.")]
    [SerializeField] private float _logoHoldDuracion = 3f;

    // ── Estado interno ──────────────────────────────────────────────────────────────────────────

    // Blocking original con el que el builder deja la escena. Se captura en Awake y se restaura al
    // arrancar cada pasada, para que "Simular secuencia" se pueda ejecutar varias veces seguidas en
    // la misma sesión de Play sin que los personajes se queden en las marcas del plano de grupo de
    // la pasada anterior. Mismo espíritu que el _liamDesignPosition de LiamCrystalBallSequencer,
    // pero aquí sin NavMeshAgent de por medio (los prefabs vienen saneados a "solo visual").
    private Vector3 _estelaPosDiseno, _liamPosDiseno, _willPosDiseno;
    private Quaternion _estelaRotDiseno, _liamRotDiseno, _willRotDiseno;

    // Tarjeta de cierre (fondo negro + logo + texto) construida en runtime la primera vez que hace
    // falta — ver EnsureLogoCard(). No es DontDestroyOnLoad ni persistente entre sesiones de Play a
    // propósito: cuelga de este mismo GameObject, así que desaparece sola al salir de la escena de
    // estudio (que es de un solo uso, para grabar), y se reconstruye limpia en cada nueva sesión.
    private CanvasGroup _logoCardGroup;
    private CanvasGroup _logoContentGroup;

    // Pose de "hablando/conversando" en bucle — la MISMA que usa NPCSimpleAnimator.interactState
    // en los NPC reales del juego (ver BeginInteraction()/PlayBodyEmotion() en NPCSimpleAnimator.cs).
    // Ver la sección "GESTOS QUE NO SE QUEDAN IDLE A MITAD DE LÍNEA" en la cabecera del script.
    private const string EstadoConversando = "InteractWithPeople_NoWeapon";

    // Pose de "escuchando/atento" — el blend tree de locomoción del propio Base Layer a velocidad
    // cero, es decir, la postura neutra de pie con la que arrancaría el personaje si no se tocara
    // nada de su Animator. Pedido de Raúl (23 ago 2026): quien NO tiene un bocadillo activo sobre la
    // cabeza no debe quedarse haciendo el gesto de "hablando" — debe volver aquí en cuanto su propia
    // línea se cierra. Ver EntrarEnEstadoAtento() y el uso en Co_Linea().
    private const string EstadoAtento = "Free Locomotion";

    // Corrutina de "qué hacer al terminar el gesto" pendiente por Animator (como mucho una activa
    // a la vez por personaje): si llega un gesto nuevo antes de que le tocara disparar a la
    // anterior, se cancela. Ver ReproducirGesto().
    private readonly Dictionary<Animator, Coroutine> _reposoPendiente = new Dictionary<Animator, Coroutine>();

    protected override void Awake()
    {
        base.Awake();

        // Resolución y cacheo de Animators: una sola vez, aquí — nunca dentro de la corrutina.
        if (_estelaAnimator == null && _estelaTransform != null)
            _estelaAnimator = _estelaTransform.GetComponentInChildren<Animator>();
        if (_liamAnimator == null && _liamTransform != null)
            _liamAnimator = _liamTransform.GetComponentInChildren<Animator>();
        if (_willAnimator == null && _willTransform != null)
            _willAnimator = _willTransform.GetComponentInChildren<Animator>();

        CapturarBlockingDeDiseno();
    }

    private void CapturarBlockingDeDiseno()
    {
        if (_estelaTransform != null)
        {
            _estelaPosDiseno = _estelaTransform.position;
            _estelaRotDiseno = _estelaTransform.rotation;
        }
        if (_liamTransform != null)
        {
            _liamPosDiseno = _liamTransform.position;
            _liamRotDiseno = _liamTransform.rotation;
        }
        if (_willTransform != null)
        {
            _willPosDiseno = _willTransform.position;
            _willRotDiseno = _willTransform.rotation;
        }
    }

    private void RestaurarBlockingDeDiseno()
    {
        if (_estelaTransform != null)
            _estelaTransform.SetPositionAndRotation(_estelaPosDiseno, _estelaRotDiseno);
        if (_liamTransform != null)
            _liamTransform.SetPositionAndRotation(_liamPosDiseno, _liamRotDiseno);
        if (_willTransform != null)
            _willTransform.SetPositionAndRotation(_willPosDiseno, _willRotDiseno);
    }

    /// Deja las 3 caras en Neutral. Se llama al arrancar cada pasada (para que "Simular secuencia"
    /// varias veces seguidas empiece siempre desde la misma expresión y no desde la última del
    /// intento anterior) y al saltar la secuencia. Null-safe: quien no tenga NPCEmotionController
    /// enlazado simplemente se queda como esté.
    private void PonerCarasNeutras()
    {
        _estelaEmotion?.SetEmotion(NPCEmotion.Neutral);
        _liamEmotion?.SetEmotion(NPCEmotion.Neutral);
        _willEmotion?.SetEmotion(NPCEmotion.Neutral);
    }

    /// Arranca a los 3 en EstadoAtento (en vez de confiar en que el Animator ya esté ahí por
    /// defecto). Se llama al empezar cada pasada, igual que PonerCarasNeutras — así "Simular
    /// secuencia" repetida no arrastra la pose de "hablando" de quien se haya quedado ahí en un
    /// intento anterior interrumpido a mitad.
    private void PonerTodosAtentos()
    {
        EntrarEnEstadoAtento(_estelaAnimator);
        EntrarEnEstadoAtento(_liamAnimator);
        EntrarEnEstadoAtento(_willAnimator);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Secuencia principal
    // ══════════════════════════════════════════════════════════════════════════════════════════

    protected override IEnumerator Co_Sequence()
    {
        RestaurarBlockingDeDiseno();
        PonerCarasNeutras();
        DetenerGestosPendientes();
        PonerTodosAtentos();
        MostrarCta(false);
        OcultarLogoFinal();

        // La transición de entrada (_entryTransition, heredada) puede quedarse sin asignar: en ese
        // caso CinematicSequencerBase corta directo al plano inicial sin fundido, que para un vídeo
        // promo es perfectamente válido. Si se quiere un fundido de apertura, asignar un
        // TransitionSettings en el Inspector.
        yield return Co_BeginCinematicWithTransition(_shotEstelaSolo);
        PlaySequenceMusic();

        yield return Co_Parte1_Presentacion();
        yield return Co_Parte2_Comedia();
        yield return Co_Parte3_Promo();

        // Última acotación del guion: "[Fundido a negro. Logo del juego + enlace de itch.io.]".
        // Ver Co_LogoFinal() — hasta ahora esto no estaba implementado, solo escrito en el guion.
        yield return Co_LogoFinal();

        // Coletilla de Estela/Liam, movida aquí (después del logo) a petición de Raúl — ver
        // Co_ColetillaTrasLogo(). Deja la pantalla en negro otra vez al terminar.
        yield return Co_ColetillaTrasLogo();

        MostrarCta(false);
        yield return Co_EndCinematicWithTransition(RestoreMusic);
        RaiseSignalOut();
    }

    // ── PARTE 1 — Presentación ──────────────────────────────────────────────────────────────────

    private IEnumerator Co_Parte1_Presentacion()
    {
        // Estela mira directamente al objetivo de cámara de su plano.
        FaceTarget(_estelaTransform, _shotEstelaSolo);

        if (_holdInicial > 0f) yield return new WaitForSeconds(_holdInicial);

        // Chulería de entrada. La reverencia (_animEstela01) dura mucho menos que el bocadillo (unos
        // 2s de clip frente a los 6s de _estela01Duracion): en cuanto termina, Co_Linea/ReproducirGesto
        // la deja en EstadoConversando ("hablando" en bucle) para el resto de la línea, en vez de caer
        // en el Idle de locomoción con el texto todavía en pantalla — ver cabecera del script.
        _estelaEmotion?.SetEmotion(NPCEmotion.Smirk);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela01, _estela01Duracion, _animEstela01);

        // Gag del rugido de estómago: SFX real + shake, exactamente como TabernaSequencer.Co_StomachRumble().
        if (_estelaTransform != null)
            AudioService.Instance?.PlaySFX(SfxRugidoEstomago, 1f, _estelaTransform.position);
        FeedbackService.CameraShake(_shakeRugidoIntensidad, _shakeRugidoDuracion);
        _estelaEmotion?.SetEmotion(NPCEmotion.Surprised);
        // mantenerConversando: false — nadie está hablando con nadie en esta reacción suelta, así que
        // al terminar el gesto se congela en su último frame en vez de saltar a la pose de "hablando".
        ReproducirGesto(_estelaAnimator, _animEstelaRugido, mantenerConversando: false);
        if (_pausaTrasRugido > 0f) yield return new WaitForSeconds(_pausaTrasRugido);

        // Molesta, buscando culpable.
        _estelaEmotion?.SetEmotion(NPCEmotion.Angry);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela02, _estela02Duracion, _animEstela02);

        // Silencio incómodo: nadie responde. Es la parte del chiste que se cuenta con el vacío.
        if (_pausaSilencioIncomodo > 0f) yield return new WaitForSeconds(_pausaSilencioIncomodo);

        // Se recompone, orgullosa, y vuelve a mirar a cámara.
        FaceTarget(_estelaTransform, _shotEstelaSolo);
        _estelaEmotion?.SetEmotion(NPCEmotion.Neutral);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela03, _estela03Duracion, _animEstela03);
    }

    // ── PARTE 2 — Comedia ───────────────────────────────────────────────────────────────────────

    private IEnumerator Co_Parte2_Comedia()
    {
        // Pan al plano medio: Liam ya está en cuadro (no camina, lo descubre la cámara).
        if (_cinematicCamera != null && _shotEstelaLiam != null)
            yield return _cinematicCamera.MoveTo(_shotEstelaLiam, _panAEstelaLiamDuracion, Ease.InOutSine);

        // Liam le roba la frase: habla hacia cámara, no hacia Estela.
        FaceTarget(_liamTransform, _shotEstelaLiam);
        _liamEmotion?.SetEmotion(NPCEmotion.Smirk);

        // REACCIÓN DE OYENTE (ronda 8): Estela nota que le están robando la frase y se gira alerta
        // hacia Liam, sin palabras todavía — el "¿pero qué...?" que se resuelve un beat después con
        // su propio "¡Oye!". Ver ReaccionOyente() y la cabecera del script.
        ReaccionOyente(_estelaTransform, _estelaAnimator, _liamTransform, _animEstelaReaccionLiam01);

        yield return Co_Linea(_liamTransform, NombreLiam, TextoLiam01, _liam01Duracion, _animLiam01);

        // Estela, indignada, se gira hacia él.
        FaceTarget(_estelaTransform, _liamTransform);
        _estelaEmotion?.SetEmotion(NPCEmotion.Angry);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela04, _estela04Duracion, _animEstela04);

        // Liam mira a cámara, media sonrisa.
        FaceTarget(_liamTransform, _shotEstelaLiam);
        _liamEmotion?.SetEmotion(NPCEmotion.Smirk);

        // REACCIÓN DE OYENTE (ronda 8): Estela sigue mirándolo (ya girada desde su "¡Oye!") y
        // reacciona nuevamente, escéptica, mientras él se presenta — deja el terreno abonado para
        // la pulla de Estela05 justo después.
        ReaccionOyente(_estelaTransform, _estelaAnimator, _liamTransform, _animEstelaReaccionLiam02);

        yield return Co_Linea(_liamTransform, NombreLiam, TextoLiam02, _liam02Duracion, _animLiam02);

        // Estela se la devuelve, ya sin enfado: puya con sorna.
        _estelaEmotion?.SetEmotion(NPCEmotion.Smirk);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela05, _estela05Duracion, _animEstela05);

        // Sin inmutarse: la pulla le resbala, y eso es justo lo que quiere transmitir.
        _liamEmotion?.SetEmotion(NPCEmotion.Smirk);
        yield return Co_Linea(_liamTransform, NombreLiam, TextoLiam03, _liam03Duracion, _animLiam03);

        // Tensión cómica breve, casi rivalidad.
        if (_pausaTensionComica > 0f) yield return new WaitForSeconds(_pausaTensionComica);

        // REACCIÓN DE OYENTE (ronda 10, 24 ago 2026): Estela gira brevemente hacia Liam al notar que
        // cambia de tema — SIN gesto nuevo (animReaccion a null: mantiene el que ya tuviera puesto),
        // solo el giro. Este beat dura muy poco (2.2s) y enseguida vuelve a girar hacia Will para la
        // línea conjunta siguiente, así que un gesto de cuerpo completo aquí competiría por atención
        // con el propio gesto de Liam (Question02, "señalando") y se vería recargado — un giro simple
        // ya transmite "le estoy siguiendo" sin robarle protagonismo a su frase. ReaccionOyente()
        // admite animReaccion vacío/null precisamente para este caso (ver ReproducirGesto()).
        ReaccionOyente(_estelaTransform, _estelaAnimator, _liamTransform, null);

        // Liam señala fuera de plano, hacia donde está Will.
        FaceTarget(_liamTransform, _willTransform);
        _liamEmotion?.SetEmotion(NPCEmotion.Thinking);
        yield return Co_Linea(_liamTransform, NombreLiam, TextoLiam04, _liam04Duracion, _animLiam04);

        // Línea conjunta. Va ANTES del whip-pan a propósito — ver "DESVIACIÓN DELIBERADA" en la
        // cabecera del script: el bocadillo se ancla sobre Estela y con la cámara ya en Will se
        // saldría de encuadre.
        FaceTarget(_estelaTransform, _willTransform);
        _estelaEmotion?.SetEmotion(NPCEmotion.Smirk);
        yield return Co_Linea(_estelaTransform, NombreConjunto, TextoConjunta, _conjuntaDuracion, _animConjunta);

        // Whip-pan: Ease.Linear + duración muy corta = latigazo de cámara, no un pan elegante.
        // Will sigue de espaldas, practicando su pose con la espada. mantenerConversando: false —
        // nadie le ve todavía, así que se congela en la pose en vez de saltar a "hablando".
        ReproducirGesto(_willAnimator, _animWillPracticando, mantenerConversando: false);
        if (_cinematicCamera != null && _shotRevelacionWill != null)
            yield return _cinematicCamera.MoveTo(_shotRevelacionWill, _whipPanDuracion, Ease.Linear);

        if (_holdWillDeEspaldas > 0f) yield return new WaitForSeconds(_holdWillDeEspaldas);

        // Will se gira: se ve pillado. Susto primero...
        FaceTarget(_willTransform, _shotRevelacionWill);
        _willEmotion?.SetEmotion(NPCEmotion.Surprised);
        ReproducirGesto(_willAnimator, _animWillPillado, mantenerConversando: false);

        // ...y un beat antes de hablar, para que el susto llegue a leerse: sin esta pausa, el
        // Surprised de arriba y el Scared de abajo caerían en el mismo frame.
        if (_pausaWillPillado > 0f) yield return new WaitForSeconds(_pausaWillPillado);

        // ...y ya con cara de apuro, la excusa.
        _willEmotion?.SetEmotion(NPCEmotion.Scared);
        yield return Co_Linea(_willTransform, NombreWill, TextoWill01, _will01Duracion, _animWill01);

        // Contracorte al plano de Estela+Liam para el aparte susurrado de Estela (si se quedase en
        // el plano de Will, su bocadillo saldría fuera de encuadre).
        _cinematicCamera?.Cut(_shotEstelaLiam);
        _estelaEmotion?.SetEmotion(NPCEmotion.Smirk);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela06, _estela06Duracion, _animEstela06);

        // Y de vuelta a Will para el remate resignado.
        _cinematicCamera?.Cut(_shotRevelacionWill);
        _willEmotion?.SetEmotion(NPCEmotion.Tired);
        yield return Co_Linea(_willTransform, NombreWill, TextoWill02, _will02Duracion, _animWill02);
    }

    // ── PARTE 3 — Promo ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Co_Parte3_Promo()
    {
        // Recolocación + corte en el MISMO frame: no se renderiza nada entre medias, así que el
        // salto de posición de Liam y Will es invisible. Por eso aquí es Cut() y no MoveTo().
        ColocarEnMarcasDeGrupo();
        _cinematicCamera?.Cut(_shotGrupoFinal);

        // Los tres, de frente a cámara.
        FaceTarget(_estelaTransform, _shotGrupoFinal);
        FaceTarget(_liamTransform, _shotGrupoFinal);
        FaceTarget(_willTransform, _shotGrupoFinal);

        // Frase encadenada de los tres: Will orgulloso, Liam sobrio, Estela rematando eufórica.
        _willEmotion?.SetEmotion(NPCEmotion.Happy);
        yield return Co_Linea(_willTransform, NombreWill, TextoWill03, _will03Duracion, _animWill03);

        _liamEmotion?.SetEmotion(NPCEmotion.Neutral);
        yield return Co_Linea(_liamTransform, NombreLiam, TextoLiam05, _liam05Duracion, _animLiam05);

        _estelaEmotion?.SetEmotion(NPCEmotion.Happy);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela07, _estela07Duracion, _animEstela07);

        // "...¡y la demo es gratis!" → aparece el CTA. Se queda un momento en pantalla, ya sin
        // bocadillos encima, antes de cortar al fundido a negro — tiempo para leerlo antes de que
        // la pantalla se cubra. El CTA NO se oculta aquí (sigue el guion: "el texto en pantalla se
        // mantiene fijo durante el resto del vídeo") — se queda activo durante el negro+logo y la
        // coletilla de después; solo se apaga en el cierre definitivo, ver Co_Sequence().
        MostrarCta(true);
        if (_holdCtaFinal > 0f) yield return new WaitForSeconds(_holdCtaFinal);
    }

    // ── Coletilla tras el logo ──────────────────────────────────────────────────────────────────

    /// Última puya y respuesta resignada de Liam. Movidas aquí (24 ago 2026, a petición de Raúl) DESDE
    /// el final de Co_Parte3_Promo: en el guion original iban justo antes del fundido a negro, pero
    /// quedan mejor como un aparte cómico DESPUÉS de que ya se haya visto el logo — se llama tras
    /// Co_LogoFinal() (ver Co_Sequence()), con la pantalla todavía en negro.
    ///
    /// Corta de vuelta al plano de grupo (fundiendo hacia fuera la propia tarjeta de cierre en el
    /// orden inverso a como apareció: primero se descubre el logo, luego el negro de fondo) — la
    /// cámara nunca se movió de _shotGrupoFinal mientras estaba tapada, así que al descubrirla los
    /// personajes ya están donde tienen que estar. Termina fundiendo el fondo negro otra vez (sin el
    /// logo esta vez) para el cierre definitivo.
    private IEnumerator Co_ColetillaTrasLogo()
    {
        yield return FadeCanvasGroup(_logoContentGroup, 1f, 0f, Mathf.Max(0.01f, _logoFadeInDuracion));
        yield return FadeCanvasGroup(_logoCardGroup, 1f, 0f, Mathf.Max(0.01f, _logoFadeANegroDuracion));

        // Liam no habla todavía en este beat — solo reacciona en silencio (sin bocadillo propio)
        // mientras Estela dice su línea; su réplica llega justo después. mantenerConversando: false
        // porque no está "conversando" activamente, solo reaccionando — y así el gesto (aunque sea en
        // bucle) se reproduce una vez y se congela, en vez de repetirse el resto del vídeo.
        //
        // CAMBIO (23 ago 2026): Raúl no quería que Estela terminara el vídeo (esta es su ÚLTIMA línea)
        // con Smirk — capturado en pantalla, el mesh de boca de Smirk se ve con la boca muy abierta
        // enseñando dientes, más agresivo que "traviesa". Se deja en Happy (la misma que ya tenía en
        // la línea anterior — la sonrisa se sostiene sin más, en vez de saltar a otra cara justo para
        // la puya). Si tampoco convence del todo, la alternativa más cercana sería probar Neutral aquí
        // — o, más a fondo, revisar directamente el mesh de boca que el EmotionProfile asigna a Smirk
        // (_EmotionProfile/NpcEmotionProfile.asset), porque esta no es la única línea del vídeo que
        // usa Smirk y las demás no se han revisado visualmente todavía.
        _estelaEmotion?.SetEmotion(NPCEmotion.Happy);
        _liamEmotion?.SetEmotion(NPCEmotion.Tired);
        // REACCIÓN DE OYENTE (ronda 9, 24 ago 2026): Liam gira a mirarla un instante al oír la puya.
        // OJO — esto NO contradice el "(sin mirarla, resignado)" del guion para SU PROPIA línea: es
        // al revés, es precisamente lo que vende mejor el chiste — sí reacciona, sí la mira un
        // segundo... y luego aparta la vista a propósito para fingir que no ha oído nada. Por eso
        // justo antes de Liam06 se le vuelve a girar hacia el plano de grupo (ver más abajo): sin ese
        // giro de vuelta se quedaría mirándola también en su propia línea, y ahí sí rompería la
        // acotación del guion.
        ReaccionOyente(_liamTransform, _liamAnimator, _estelaTransform, _animLiamReaccionPulla);
        yield return Co_Linea(_estelaTransform, NombreEstela, TextoEstela08, _estela08Duracion, _animEstela08);

        // Aparta la vista a propósito antes de su réplica — "(sin mirarla, resignado)" en el guion.
        FaceTarget(_liamTransform, _shotGrupoFinal);
        yield return Co_Linea(_liamTransform, NombreLiam, TextoLiam06, _liam06Duracion, _animLiam06);

        if (_holdTrasColetillaFinal > 0f) yield return new WaitForSeconds(_holdTrasColetillaFinal);

        // Cierre definitivo: mismo fondo negro de la tarjeta, sin volver a fundir el logo/texto —
        // el vídeo termina en negro después de la puya, no repitiendo la marca.
        yield return FadeCanvasGroup(_logoCardGroup, 0f, 1f, Mathf.Max(0.01f, _logoFadeANegroDuracion));
    }

    /// Mueve a Liam y Will (y a Estela si también tiene marca) a las marcas del plano de grupo.
    /// Null-safe: cada marca sin asignar simplemente deja a ese personaje donde estaba.
    private void ColocarEnMarcasDeGrupo()
    {
        if (_estelaTransform != null && _marcaGrupoEstela != null)
            _estelaTransform.SetPositionAndRotation(_marcaGrupoEstela.position, _marcaGrupoEstela.rotation);
        if (_liamTransform != null && _marcaGrupoLiam != null)
            _liamTransform.SetPositionAndRotation(_marcaGrupoLiam.position, _marcaGrupoLiam.rotation);
        if (_willTransform != null && _marcaGrupoWill != null)
            _willTransform.SetPositionAndRotation(_marcaGrupoWill.position, _marcaGrupoWill.rotation);
    }

    // ══════════════════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════════════════════

    /// Muestra una línea de diálogo y espera a que el bocadillo termine, con el mismo patrón que
    /// LiamCrystalBallSequencer/TabernaSequencer (Show + WaitUntil sobre el onComplete).
    ///
    /// Diferencia deliberada respecto a esos dos: aquí SÍ se comprueba SpeechBubbleUI.Instance. En
    /// las secuencias del juego real el bocadillo siempre existe (vive en el Canvas persistente de
    /// Start.unity); en esta escena de marketing es concebible dar Play sin que Start se haya
    /// cargado, y sin este guard la corrutina se quedaría colgada para siempre en el WaitUntil de
    /// un onComplete que nunca llega — con el HUD bloqueado y la señal de salida sin levantar.
    private IEnumerator Co_Linea(Transform hablante, string nombre, string texto, float duracion, string animKey)
    {
        if (duracion <= 0f) duracion = 1f;

        var bocadillo = SpeechBubbleUI.Instance;
        if (hablante == null || bocadillo == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[PromoVideo01Sequencer] No se puede mostrar la línea de {nombre} " +
                (hablante == null
                    ? "porque su Transform no está asignado en el Inspector."
                    : "porque SpeechBubbleUI.Instance es null (¿se ha dado Play sin que 'Start.unity' se cargara aditivamente?).") +
                " Se respeta el timing igualmente para no romper el montaje.");
#endif
            yield return new WaitForSeconds(duracion);
            yield break;
        }

        // El gesto lo gestionamos nosotros mismos (ver ReproducirGesto y la sección "GESTOS QUE NO SE
        // QUEDAN IDLE A MITAD DE LÍNEA" en la cabecera del script): por eso animTrigger va siempre a
        // null aquí abajo — dejar que Show() lo dispare por su cuenta duplicaría el Play() y, sobre
        // todo, no sabría devolver al personaje a la pose de "hablando" cuando el clip del gesto
        // termina antes que el bocadillo.
        var animadorHablante = AnimatorParaHablante(hablante);
        ReproducirGesto(animadorHablante, animKey, mantenerConversando: true);

        bool hecho = false;
        bocadillo.Show(hablante, texto,
            duration: duracion,
            onComplete: () => hecho = true,
            animTrigger: null,
            speakerName: nombre);
        yield return new WaitUntil(() => hecho);

        // El bocadillo YA se ha cerrado: este personaje deja de "hablar" activamente. Pedido de
        // Raúl: quien no tiene bocadillo en la cabeza debe quedarse en Idle/atento en vez de seguir
        // en EstadoConversando (que sin esto se quedaba puesto indefinidamente hasta el SIGUIENTE
        // gesto de ese mismo personaje — pudiendo ser mucho más tarde, mientras otro habla). Ver
        // EntrarEnEstadoAtento().
        EntrarEnEstadoAtento(animadorHablante);
    }

    /// Animator del personaje que corresponde a este Transform — para que Co_Linea pueda gestionar
    /// el gesto sin que cada llamada tenga que pasar el Animator explícitamente además del Transform.
    private Animator AnimatorParaHablante(Transform hablante)
    {
        if (hablante == _estelaTransform) return _estelaAnimator;
        if (hablante == _liamTransform) return _liamAnimator;
        if (hablante == _willTransform) return _willAnimator;
        return null;
    }

    /// Dispara un gesto puntual del Base Layer (vía Animator.Play — nombre de ESTADO, no un
    /// parámetro Trigger, ver cabecera del script) y decide qué pasa EN CUANTO ese gesto termina,
    /// que casi siempre es bastante antes de que termine la línea o pausa que lo acompaña: ver la
    /// sección "GESTOS QUE NO SE QUEDAN IDLE A MITAD DE LÍNEA" en la cabecera para el porqué.
    ///
    /// <param name="mantenerConversando">
    /// true (gestos atados a una línea, vía Co_Linea): al terminar el clip, pasa a EstadoConversando
    /// ("InteractWithPeople_NoWeapon", la misma pose de "hablando" en bucle que usan los NPC reales
    /// mientras dura una interacción) y se queda ahí hasta el siguiente gesto.
    /// false (reacciones sueltas sin bocadillo — el rugido, la pose de práctica de Will, su
    /// "pillado"): caer en la pose de "hablando" no pega narrativamente si nadie está conversando
    /// con nadie todavía, así que en su lugar se CONGELA en el último frame del propio gesto
    /// (Play a normalizedTime≈1 + Animator.speed=0) hasta que la siguiente llamada explícita lo
    /// reemplace (esa siguiente llamada siempre resetea speed a 1 antes de reproducir nada, así
    /// que no hace falta "descongelar" a mano en ningún otro sitio).
    /// </param>
    private void ReproducirGesto(Animator animator, string estado, bool mantenerConversando)
    {
        if (animator == null) return;

        if (_reposoPendiente.TryGetValue(animator, out var previa) && previa != null)
            StopCoroutine(previa);

        animator.speed = 1f; // por si un gesto anterior se congeló con speed = 0 (ver más arriba)

        if (string.IsNullOrEmpty(estado) || !animator.HasState(0, Animator.StringToHash(estado)))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!string.IsNullOrEmpty(estado))
                Debug.LogWarning($"[PromoVideo01Sequencer] El Animator Controller de '{animator.name}' no tiene " +
                    $"ningún estado llamado '{estado}' en su Base Layer — el gesto no se reproducirá. Revisa la " +
                    "clave en el Inspector (debe ser un NOMBRE DE ESTADO, no un parámetro Trigger).");
#endif
            if (mantenerConversando)
                CrossFadeSiNoEsta(animator, EstadoConversando, 0.15f);
            _reposoPendiente[animator] = null;
            return;
        }

        animator.Play(estado, 0, 0f);
        _reposoPendiente[animator] = StartCoroutine(Co_TrasGesto(animator, estado, mantenerConversando));
    }

    /// Espera a que termine el clip que ReproducirGesto() acaba de lanzar y entonces aplica el
    /// comportamiento pedido (volver a EstadoConversando, o congelarse). La duración real del clip
    /// se lee del propio Animator (GetCurrentAnimatorStateInfo().length) en vez de mantener una
    /// tabla de duraciones a mano — así siempre está sincronizado aunque cambie el clip asignado al
    /// estado en el Animator Controller.
    private IEnumerator Co_TrasGesto(Animator animator, string estadoGesto, bool mantenerConversando)
    {
        yield return null; // 1 frame para que el Play() de ReproducirGesto ya esté activo
        var info = animator.GetCurrentAnimatorStateInfo(0);
        float duracionClip = info.IsName(estadoGesto) ? info.length : 1f;
        yield return new WaitForSeconds(duracionClip);

        if (mantenerConversando)
            CrossFadeSiNoEsta(animator, EstadoConversando, 0.2f);
        else
        {
            animator.Play(estadoGesto, 0, 0.999f);
            animator.speed = 0f;
        }

        _reposoPendiente[animator] = null;
    }

    /// Reacción no verbal de quien ESCUCHA (sin bocadillo propio en este beat) mientras otro
    /// personaje tiene la palabra: gira a mirar a quien habla y dispara un gesto corto de "me he
    /// dado cuenta/estoy atento" que se congela en su último frame — mismo mecanismo que ya usaban
    /// _animLiamReaccionPulla o el "pillado" de Will (ReproducirGesto con mantenerConversando:false),
    /// generalizado aquí a un solo sitio y con el giro añadido. Ver la sección "REACCIÓN DE QUIEN
    /// ESCUCHA" en la cabecera del script.
    ///
    /// A propósito SIEMPRE con mantenerConversando:false: el oyente no está "conversando"
    /// activamente (no tiene bocadillo), así que el gesto se congela en vez de caer en
    /// EstadoConversando — evita que parezca que está hablando él también.
    ///
    /// Null-safe en todo: si falta el Transform del oyente no hace nada; si falta el Animator o la
    /// clave de animación, al menos gira (FaceTarget ya es null-safe por su cuenta).
    private void ReaccionOyente(Transform oyente, Animator animOyente, Transform hablante, string animReaccion)
    {
        if (oyente == null) return;
        FaceTarget(oyente, hablante);
        ReproducirGesto(animOyente, animReaccion, mantenerConversando: false);
    }

    /// CrossFade a un estado del Base Layer solo si el personaje no está ya ahí (evita reiniciar el
    /// clip desde 0 si se llama varias veces seguidas sobre el mismo estado, p.ej. dos líneas
    /// seguidas del mismo personaje sin gesto propio entre medias).
    private void CrossFadeSiNoEsta(Animator animator, string estado, float blend)
    {
        if (animator == null || string.IsNullOrEmpty(estado)) return;
        if (!animator.HasState(0, Animator.StringToHash(estado))) return;
        if (animator.GetCurrentAnimatorStateInfo(0).IsName(estado)) return;
        animator.CrossFadeInFixedTime(estado, blend, 0);
    }

    /// Cancela cualquier corrutina de "qué hacer al terminar el gesto" en curso y deja los 3
    /// Animators a velocidad normal (por si alguno se había congelado con speed = 0). Se llama al
    /// arrancar cada pasada (para que "Simular secuencia" repetida no arrastre coroutines de la
    /// pasada anterior) y al saltar la secuencia.
    private void DetenerGestosPendientes()
    {
        foreach (var par in _reposoPendiente)
        {
            if (par.Value != null) StopCoroutine(par.Value);
            if (par.Key != null) par.Key.speed = 1f;
        }
        _reposoPendiente.Clear();
    }

    /// Saca a un personaje de la pose de "hablando" (EstadoConversando) en cuanto deja de tener un
    /// bocadillo activo, y lo deja en EstadoAtento (Idle/escuchando) — llamado desde Co_Linea justo
    /// después de que su propio bocadillo se cierre. Cancela cualquier corrutina de "qué hacer al
    /// terminar el gesto" que pudiera seguir pendiente (caso raro: un gesto con clip más largo que la
    /// propia línea) para que no reviva EstadoConversando después de esto, y resetea speed a 1 por si
    /// el último gesto se congeló.
    private void EntrarEnEstadoAtento(Animator animator)
    {
        if (animator == null) return;

        if (_reposoPendiente.TryGetValue(animator, out var previa) && previa != null)
        {
            StopCoroutine(previa);
            _reposoPendiente[animator] = null;
        }

        animator.speed = 1f;
        CrossFadeSiNoEsta(animator, EstadoAtento, 0.25f);
    }

    /// Activa/desactiva el panel de CTA con guard de estado previo (no llamar a SetActive si ya
    /// está como toca). No-op seguro si no hay panel asignado.
    private void MostrarCta(bool visible)
    {
        if (_ctaPanel == null) return;
        if (_ctaPanel.activeSelf == visible) return;
        _ctaPanel.SetActive(visible);
    }

    // ── Logo final ──────────────────────────────────────────────────────────────────────────────

    /// Construye, la primera vez que hace falta, la tarjeta de cierre: un Canvas propio con fondo
    /// negro a pantalla completa + logo + texto, en dos CanvasGroup separados (uno para el fondo,
    /// otro para el contenido) para poder fundir cada uno por separado y que el negro y el logo se
    /// lean como dos beats distintos, tal como pide el guion. Sorting order 9999: por ENCIMA de los
    /// dos overlays persistentes del proyecto que también viven en pantalla completa
    /// (FS_ScreenFade = 9998 en FeedbackService, TitleLogoController = 9997) para no depender de en
    /// qué estado se hayan quedado esos otros sistemas — esta tarjeta es autocontenida a propósito.
    private void EnsureLogoCard()
    {
        if (_logoCardGroup != null) return;

        var canvasGo = new GameObject("PromoLogoCard", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        _logoCardGroup = canvasGo.AddComponent<CanvasGroup>();
        _logoCardGroup.alpha = 0f;
        _logoCardGroup.interactable = false;
        _logoCardGroup.blocksRaycasts = false;

        var bgGo = new GameObject("Fondo");
        bgGo.transform.SetParent(canvasGo.transform, false);
        var bgImage = bgGo.AddComponent<UnityEngine.UI.Image>();
        bgImage.color = Color.black;
        var bgRt = bgImage.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var contentGo = new GameObject("Contenido", typeof(RectTransform), typeof(CanvasGroup));
        contentGo.transform.SetParent(canvasGo.transform, false);
        var contentRt = (RectTransform)contentGo.transform;
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = Vector2.zero;
        contentRt.offsetMax = Vector2.zero;
        _logoContentGroup = contentGo.GetComponent<CanvasGroup>();
        _logoContentGroup.alpha = 0f;

        var logoGo = new GameObject("Logo");
        logoGo.transform.SetParent(contentGo.transform, false);
        var logoImage = logoGo.AddComponent<UnityEngine.UI.Image>();
        logoImage.preserveAspect = true;
        // Si no hay sprite asignado, alpha 0 en vez de dejar el cuadrado blanco por defecto de Image.
        logoImage.color = _logoSprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        logoImage.sprite = _logoSprite;
        var logoRt = logoImage.rectTransform;
        logoRt.anchorMin = new Vector2(0.5f, 0.5f);
        logoRt.anchorMax = new Vector2(0.5f, 0.5f);
        logoRt.sizeDelta = new Vector2(700, 460);
        logoRt.anchoredPosition = new Vector2(0f, 60f);

        var textGo = new GameObject("Texto");
        textGo.transform.SetParent(contentGo.transform, false);
        var text = textGo.AddComponent<UnityEngine.UI.Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 40;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.text = _logoLinkTexto;
        var textRt = text.rectTransform;
        textRt.anchorMin = new Vector2(0.5f, 0.5f);
        textRt.anchorMax = new Vector2(0.5f, 0.5f);
        textRt.sizeDelta = new Vector2(900, 80);
        textRt.anchoredPosition = new Vector2(0f, -220f);

        if (_logoSprite == null)
            Debug.LogWarning("[PromoVideo01Sequencer] _logoSprite no está asignado — la tarjeta de cierre " +
                              "se verá solo con el texto, sin el logo del juego. Reejecuta el builder " +
                              "('El Sendero → Marketing → Crear Escena de Estudio (Vídeos Promo)') para que " +
                              "se autoasigne, o arrástralo a mano en el Inspector.");
    }

    /// Deja la tarjeta de cierre invisible (fondo y contenido a alpha 0). Se llama al empezar cada
    /// pasada — igual que MostrarCta(false) — para que "Simular secuencia" repetida no arranque ya
    /// con la pantalla en negro por una pasada anterior, y desde OnSkipCleanup si se salta la
    /// secuencia después de que la tarjeta ya haya empezado a aparecer.
    private void OcultarLogoFinal()
    {
        if (_logoCardGroup != null) _logoCardGroup.alpha = 0f;
        if (_logoContentGroup != null) _logoContentGroup.alpha = 0f;
    }

    /// Los dos beats de la última acotación del guion: "[Fundido a negro. Logo del juego + enlace de
    /// itch.io.]". Primero cubre la pantalla de negro (fondo del Canvas), y solo cuando ya está
    /// cubierta del todo, funde encima el contenido (logo + texto) — dos fundidos en serie, no uno
    /// solo, para que se lean como "primero negro, luego aparece el logo" y no como un único fundido
    /// donde todo entra a la vez.
    private IEnumerator Co_LogoFinal()
    {
        EnsureLogoCard();
        OcultarLogoFinal();

        yield return FadeCanvasGroup(_logoCardGroup, 0f, 1f, Mathf.Max(0.01f, _logoFadeANegroDuracion));
        yield return FadeCanvasGroup(_logoContentGroup, 0f, 1f, Mathf.Max(0.01f, _logoFadeInDuracion));

        if (_logoHoldDuracion > 0f)
            yield return new WaitForSecondsRealtime(_logoHoldDuracion);
    }

    /// Fundido genérico de un CanvasGroup con tiempo real (unscaled), igual que
    /// TitleLogoController.FadeCanvas() — para no depender de Time.timeScale durante la cinemática.
    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        group.alpha = to;
    }

    // ── Skip ────────────────────────────────────────────────────────────────────────────────────

    /// Ver CinematicSequencerBase.OnSkipCleanup(). Se llama con la pantalla ya cubierta si alguien
    /// salta la secuencia a mitad: hay que dejar el CTA oculto, el bocadillo cerrado, a los
    /// personajes en su blocking original y las caras en neutro, cosas que en el flujo normal solo
    /// ocurren al llegar al final de Co_Sequence().
    protected override void OnSkipCleanup()
    {
        MostrarCta(false);
        OcultarLogoFinal();
        SpeechBubbleUI.Instance?.Hide();
        RestaurarBlockingDeDiseno();
        PonerCarasNeutras();
        DetenerGestosPendientes();
        PonerTodosAtentos();
    }
}
