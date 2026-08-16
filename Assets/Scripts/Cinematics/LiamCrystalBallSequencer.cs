using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia de Liam mirando la bola de cristal.
/// Revela que el demonio fue cosa suya y que Will es el chico de corazón puro.
/// Señal de entrada: "LIAM_CRYSTAL_START".
/// Señal de salida:  "LIAM_CRYSTAL_DONE".
[DisallowMultipleComponent]
public class LiamCrystalBallSequencer : CinematicSequencerBase
{
    // loopId propio para AudioService.PlayLoopingSFX/StopLoopingSFX. LiamGolemSummonSequencer usa
    // el mismo loopId ("CrystalBallPulse") para su propia bola de cristal (idéntico mecanismo
    // visual) — como son secuencias mutuamente excluyentes nunca coinciden en el tiempo.
    private const string CrystalPulseLoopId = "CrystalBallPulse";

    [Header("Personajes")]
    [SerializeField] private Transform liamTransform;
    [Tooltip("Transform del jugador para que la cámara de visión lo siga.")]
    [SerializeField] private Transform playerTransform;

    [Header("Cámara — planos")]
    [Tooltip("Plano detalle de la bola de cristal con Will visible dentro")]
    [SerializeField] private Transform camShotCrystalBall;
    [Tooltip("Plano medio de Liam inclinado sobre la bola, cara en penumbra")]
    [SerializeField] private Transform camShotLiamMedium;
    [Tooltip("Primer plano del rostro de Liam — para la risa final")]
    [SerializeField] private Transform camShotLiamFace;
    [Tooltip("Plano wide opcional: dolly back que revela el interior oscuro al final")]
    [SerializeField] private Transform camShotWide;

    [Header("Bola de cristal — visión del jugador")]
    [Tooltip("Cámara secundaria que sigue al jugador y renderiza a un RT. Asígnale el Target Texture en el Inspector.")]
    [SerializeField] private CrystalBallVisionCamera crystalVisionCamera;
    [Tooltip("Renderer de la bola de cristal. Su material debe tener Emission ON y _EmissionMap = el mismo RT que usa crystalVisionCamera.")]
    [SerializeField] private Renderer crystalBallRenderer;
    [Tooltip("Color HDR de la emisión al mostrar la visión (controla brillo y tinte mágico).")]
    [SerializeField] private Color visionEmissionColor = new Color(0.15f, 1f, 0.45f, 1f);
    [SerializeField] private float visionFadeIn  = 0.8f;
    [SerializeField] private float visionFadeOut = 0.5f;

    [Header("Bola de cristal — pulso mágico")]
    [Tooltip("Ciclos de pulso por segundo mientras la visión está activa.")]
    [SerializeField] private float pulseFrequency   = 1.1f;
    [Tooltip("Escala mínima del brillo en el valle del pulso (0..1).")]
    [SerializeField, Range(0f, 1f)] private float pulseMinScale = 0.55f;
    [Tooltip("Cuánto mezcla el shimmer hacia visionShimmerColor (0 = sin shimmer de color).")]
    [SerializeField, Range(0f, 1f)] private float colorShimmerAmount = 0.35f;
    [Tooltip("Color secundario del shimmer. Se mezcla con visionEmissionColor al ritmo del pulso.")]
    [SerializeField] private Color visionShimmerColor = new Color(0.05f, 0.55f, 1f, 1f);
    [Tooltip("Sistema de partículas sobre la bola (opcional). Se activa con la visión.")]
    [SerializeField] private ParticleSystem crystalBallParticles;

    [Header("Efectos atmosféricos")]
    // FIX (16/08/2026): pedido de diseño — la secuencia se veía "demasiado iluminada" (se
    // distinguía de más la habitación real detrás del overlay), restando tensión al momento en
    // que se revela que Liam es el villano. Se sube el alpha por defecto y además se fuerza un
    // suelo mínimo en Co_FadeInEvilOverlay() (ver MinRoomDarkenAlpha): así la sala queda siempre
    // lo bastante oscura aunque algún valor antiguo más claro quedara guardado en el Inspector.
    [Tooltip("Color del overlay persistente que oscurece la escena durante la cinemática. Alpha controla intensidad.")]
    [SerializeField] private Color evilOverlayColor  = new Color(0.05f, 0f, 0.03f, 0.55f);
    [Tooltip("Tiempo de fade-in del overlay oscuro (ocurre durante el plano de la bola de cristal).")]
    [SerializeField] private float overlayFadeIn     = 0.6f;
    [Tooltip("Color del pulso siniestro que aparece entre frases. Alpha bajo para sutileza.")]
    [SerializeField] private Color evilFlashColor    = new Color(0.7f, 0f, 0.05f, 0.16f);
    [Tooltip("Duración del fade-out de cada pulso siniestro.")]
    [SerializeField] private float evilFlashDuration = 2.5f;
    [Tooltip("Intensidad del camera shake que acompaña cada pulso siniestro (vende más la amenaza).")]
    [SerializeField] private float evilPulseShakeIntensity = 0.3f;
    [Tooltip("Duración del camera shake de cada pulso siniestro.")]
    [SerializeField] private float evilPulseShakeDuration  = 0.35f;

    /// Suelo mínimo de opacidad del overlay que oscurece la sala, aplicado en Co_FadeInEvilOverlay()
    /// independientemente del valor configurado en evilOverlayColor. Ver FIX 16/08/2026 arriba.
    private const float MinRoomDarkenAlpha = 0.55f;

    // ── Fase 1 — Bola de cristal: visión de Will ─────────────────────────────

    [Header("Fase 1 — Bola de cristal")]
    [SerializeField] private float holdOnCrystalBall = 1.2f;

    // ── Fase 2 — Primera revelación ───────────────────────────────────────────

    [Header("Fase 2 — Primera revelación")]
    [SerializeField] private string     keyLine1      = "EVT_LIAM_CRYSTAL_01";
    [SerializeField] private NPCEmotion emotionLine1  = NPCEmotion.Thinking;
    [SerializeField] private string     animLine1     = "Thinking01";
    [SerializeField] private float      line1Duration = 3.0f;

    // ── Fase 3 — Segunda revelación ───────────────────────────────────────────

    [Header("Fase 3 — Segunda revelación")]
    [SerializeField] private string     keyLine2      = "EVT_LIAM_CRYSTAL_02";
    [SerializeField] private NPCEmotion emotionLine2  = NPCEmotion.Happy;
    [SerializeField] private string     animLine2     = "Smug01";
    [SerializeField] private float      line2Duration = 3.2f;

    // ── Fase 4 — Risa final ───────────────────────────────────────────────────

    [Header("Fase 4 — Risa final")]
    [SerializeField] private string     keyLine3      = "EVT_LIAM_CRYSTAL_03";
    [SerializeField] private NPCEmotion emotionLine3  = NPCEmotion.Happy;
    [SerializeField] private string     animLine3     = "Laugh01";
    [SerializeField] private float      line3Duration = 2.8f;
    [SerializeField] private float      holdAfterLaugh = 1.0f;

    // ── Timings generales ─────────────────────────────────────────────────────

    [Header("Timings generales")]
    [Tooltip("Duración del dolly back al plano wide (0 = sin dolly)")]
    [SerializeField] private float wideShotBlendTime = 1.2f;
    [Tooltip("Duración del fundido que retira el negro tras saltar la secuencia con el botón global de skip (ver OnSkipCleanup).")]
    [SerializeField] private float _skipRevealDuration = 0.3f;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private NPCEmotionController _liamEmotion;
    private Game.NPC.NPCBehaviourManagerV2 _liamNpc;
    private NavMeshAgent _liamAgent;
    private ObstacleAvoidanceType _liamOriginalAvoidance;
    private bool _liamFrozen; // true tras FreezeLiamNavigation() — guard idempotente de UnfreezeLiamNavigation()
    private Vector3 _liamDesignPosition;
    private Quaternion _liamDesignRotation;
    private Image _evilOverlayImg;
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private bool _visionPulsing;
    // Corrutina fire-and-forget de Co_ShowCrystalVision() (Fase 1): StartCoroutine suelto, no
    // "yield return" dentro de Co_Sequence(), así que StopCoroutine(_activeSequenceCoroutine) del
    // skip global NO la detiene. Ver el guard en EmergencyCleanup().
    private Coroutine _showVisionCoroutine;

    protected override void Awake()
    {
        base.Awake();

        if (liamTransform != null)
        {
            _liamEmotion = liamTransform.GetComponentInChildren<NPCEmotionController>();
            _liamNpc     = liamTransform.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            _liamAgent   = liamTransform.GetComponent<NavMeshAgent>();

            // FIX: capturar la posición/rotación colocadas a mano en el editor ANTES de que el
            // NavMeshAgent de Liam pueda corregirlas él solo al activarse. Si el punto exacto donde
            // está colocado (pegado a la mesa/alfombra de esta habitación) no cae sobre el NavMesh
            // baked, Unity lo desplaza al punto válido más cercano en cuanto el agente se activa —
            // esto ocurre a nivel de motor, nada más arrancar la partida, sin pasar por ningún
            // script nuestro. Ver Co_RestoreLiamDesignPosition().
            _liamDesignPosition = liamTransform.position;
            _liamDesignRotation = liamTransform.rotation;
        }

        if (crystalBallRenderer != null)
            _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (liamTransform != null)
            StartCoroutine(Co_RestoreLiamDesignPosition());
    }

    /// Un frame después de Awake, el NavMeshAgent de Liam ya habrá aplicado (si iba a hacerlo) su
    /// corrección automática al activarse. Si su posición se alejó de donde lo colocamos a mano,
    /// lo devolvemos ahí con Warp (evita que NavMesh/física generen un nuevo path).
    private IEnumerator Co_RestoreLiamDesignPosition()
    {
        yield return null;
        if (liamTransform == null) yield break;

        if ((liamTransform.position - _liamDesignPosition).sqrMagnitude > 0.0001f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[LiamCrystalBallSequencer] Liam se había desplazado de su posición diseñada " +
                $"({liamTransform.position} → objetivo {_liamDesignPosition}), probablemente por corrección " +
                $"automática del NavMeshAgent al activarse. Restaurando.");
#endif
            if (_liamAgent != null && _liamAgent.isOnNavMesh)
                _liamAgent.Warp(_liamDesignPosition);
            else
                liamTransform.position = _liamDesignPosition;
        }

        liamTransform.rotation = _liamDesignRotation;
    }

    // FIX: HardStop (isStopped=true) no evita que un NavMeshAgent con obstacle avoidance activo
    // (aquí HighQuality) siga siendo empujado por la avoidance del jugador u otros agentes cercanos
    // — es el mismo motivo por el que CinematicState.MoveToPositionSequence desactiva la avoidance
    // durante el movimiento cinemático (ver su comentario "para que Player y Party Members no
    // bloqueen al NPC"). Sin esto Liam podía seguir desplazándose ligeramente aunque su FSM
    // estuviera desactivada y el agente "detenido".
    private void FreezeLiamNavigation()
    {
        _liamFrozen = true; // ver guard idempotente en UnfreezeLiamNavigation()
        _liamNpc?.ForceIdle();
        if (_liamAgent != null)
        {
            _liamOriginalAvoidance = _liamAgent.obstacleAvoidanceType;
            _liamAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        if (_liamNpc != null) _liamNpc.enabled = false;

        // FIX INC-069: Co_RestoreLiamDesignPosition() (llamado desde Start) solo corrige el
        // desplazamiento que provoca el NavMeshAgent al activarse en la carga de la escena. Pero
        // esta cinemática se dispara por señal narrativa y puede arrancar mucho más tarde, después
        // de que la IA normal de Liam (NPCBehaviourManagerV2) lo haya movido libremente por la
        // sala — así que para cuando empieza la secuencia ya no está en el sitio/orientación
        // diseñados para los planos de cámara ("se mueve quedando la cara oculta"). Lo recolocamos
        // aquí también, justo cuando se congela su navegación con la pantalla ya cubierta por la
        // transición de entrada.
        if (liamTransform != null)
        {
            if (_liamAgent != null && _liamAgent.isOnNavMesh)
                _liamAgent.Warp(_liamDesignPosition);
            else
                liamTransform.position = _liamDesignPosition;
            liamTransform.rotation = _liamDesignRotation;
        }
    }

    private void UnfreezeLiamNavigation()
    {
        // FIX (16 ago 2026 — auditoría de skip): FreezeLiamNavigation() se invoca como
        // additionalOnCut de Co_BeginCinematicWithTransition, es decir, solo cuando la transición
        // de ENTRADA alcanza su cut point — no de inmediato al arrancar Co_Sequence(). Pero
        // LockCinematic() (dentro de esa misma transición) ya deja la secuencia registrada como
        // "saltable" desde el primer instante. Si el botón global de skip se dispara durante esa
        // ventana (p. ej. el jugador ya mantenía pulsado skip al encadenar esta cinemática con
        // otra), Co_Sequence() se corta ANTES de que FreezeLiamNavigation() llegue a ejecutarse:
        // sin este guard, _liamOriginalAvoidance seguiría con su valor por defecto sin inicializar
        // (0 = NoObstacleAvoidance) y lo pisaría sobre el obstacleAvoidanceType real de Liam para
        // el resto de la partida, además de interrumpirlo con un ForceIdle() gratuito.
        if (!_liamFrozen) return;
        _liamFrozen = false;

        if (_liamNpc != null)
        {
            _liamNpc.enabled = true;
            _liamNpc.ForceIdle();
        }
        if (_liamAgent != null)
            _liamAgent.obstacleAvoidanceType = _liamOriginalAvoidance;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EmergencyCleanup();
    }

    /// Limpieza de emergencia compartida por OnDestroy() (destrucción real del objeto) y
    /// OnSkipCleanup() (skip a mitad de secuencia): para el pulso/loop de la bola de cristal,
    /// destruye el overlay tenebroso persistente y desactiva la cámara de visión. _visionPulsing en
    /// false basta para que Co_PulseCrystalVision (fire-and-forget) salga de su propio bucle solo,
    /// sin necesidad de StopCoroutine explícito — mismo mecanismo que ya usa Co_HideCrystalVision.
    private void EmergencyCleanup()
    {
        // FIX (16 ago 2026 — auditoría de skip): a diferencia de Co_PulseCrystalVision (que sí sale
        // solo de su bucle en cuanto _visionPulsing es false), Co_ShowCrystalVision() es la propia
        // corrutina fire-and-forget que hace el fade-in inicial y NO comprueba ningún flag de
        // cancelación en su bucle — si el skip corta mientras sigue viva (ventana real: sus ~0.8s
        // de fade-in más el fundido a negro del propio skip), sigue ejecutándose después de esta
        // limpieza, vuelve a escribir el PropertyBlock ya limpiado y relanza
        // Co_PulseCrystalVision() con _visionPulsing = true otra vez — pulso + SFX en loop
        // sonando indefinidamente. Detenerla explícitamente aquí.
        if (_showVisionCoroutine != null) { StopCoroutine(_showVisionCoroutine); _showVisionCoroutine = null; }

        _visionPulsing = false;
        AudioService.Instance?.StopLoopingSFX(CrystalPulseLoopId);
        DestroyEvilOverlay();
        crystalVisionCamera?.Deactivate();
        crystalBallParticles?.Stop();
        if (crystalBallRenderer != null)
            crystalBallRenderer.SetPropertyBlock(null);
    }

    /// Ver CinematicSequencerBase.OnSkipCleanup(). Reutiliza la misma limpieza de emergencia que
    /// OnDestroy() (overlay tenebroso, pulso de la bola de cristal, cámara de visión) y además
    /// libera a Liam, que UnfreezeLiamNavigation() solo hacía en el cierre normal.
    protected override void OnSkipCleanup()
    {
        EmergencyCleanup();
        UnfreezeLiamNavigation();

        // FIX (16 ago 2026 — misma auditoría que TabernaSequencer): el cierre normal
        // (Co_EndCinematicWithTransition) revela la pantalla solo; el cierre genérico de skip
        // (Co_SkipToEnd -> Co_EndCinematicStayBlack) NO revela — esta secuencia no da paso a
        // ningún sistema (boss intro, etc.) que se encargue de revelar por su cuenta, así que sin
        // este fade manual saltarla deja la pantalla en negro para siempre.
        StartCoroutine(Co_RevealAfterSkip());
    }

    private IEnumerator Co_RevealAfterSkip()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, _skipRevealDuration, fadeIn: false);
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    protected override IEnumerator Co_Sequence()
    {
        // Congelar FSM + navegación de Liam en el cut point (pantalla cubierta), igual que
        // hacen TabernaSequencer/EstelaAppearsSequencer con sus NPCs. Ver FreezeLiamNavigation().
        yield return Co_BeginCinematicWithTransition(camShotCrystalBall, FreezeLiamNavigation);
        PlaySequenceMusic();

        // ── Fase 1: Bola de cristal — imagen de Will brillando dentro ─────────
        StartCoroutine(Co_FadeInEvilOverlay());
        _showVisionCoroutine = StartCoroutine(Co_ShowCrystalVision());
        yield return new WaitForSeconds(holdOnCrystalBall);

        // ── Fase 2: Plano medio Liam — primera revelación ─────────────────────
        _cinematicCamera.Cut(camShotLiamMedium);

        if (emotionLine1 != NPCEmotion.None)
            _liamEmotion?.SetEmotion(emotionLine1);

        bool line1Done = false;
        SpeechBubbleUI.Instance.Show(liamTransform, Loc(keyLine1),
            duration: line1Duration,
            onComplete: () => line1Done = true,
            animTrigger: animLine1);
        yield return new WaitUntil(() => line1Done);

        FeedbackService.ScreenFlash(evilFlashColor, evilFlashDuration);
        FeedbackService.CameraShake(evilPulseShakeIntensity, evilPulseShakeDuration);
        AudioService.Instance?.PlaySFX("CrystalBall_EvilPulse", 1f, liamTransform.position);

        // ── Fase 3: Mismo plano — segunda revelación ──────────────────────────
        if (emotionLine2 != NPCEmotion.None)
            _liamEmotion?.SetEmotion(emotionLine2);

        bool line2Done = false;
        SpeechBubbleUI.Instance.Show(liamTransform, Loc(keyLine2),
            duration: line2Duration,
            onComplete: () => line2Done = true,
            animTrigger: animLine2);
        yield return new WaitUntil(() => line2Done);

        FeedbackService.ScreenFlash(evilFlashColor, evilFlashDuration);
        FeedbackService.CameraShake(evilPulseShakeIntensity, evilPulseShakeDuration);
        AudioService.Instance?.PlaySFX("CrystalBall_EvilPulse", 1f, liamTransform.position);

        // ── Fase 4: Primer plano del rostro — risa final ──────────────────────
        _cinematicCamera.Cut(camShotLiamFace);

        if (emotionLine3 != NPCEmotion.None)
            _liamEmotion?.SetEmotion(emotionLine3);

        bool line3Done = false;
        SpeechBubbleUI.Instance.Show(liamTransform, Loc(keyLine3),
            duration: line3Duration,
            onComplete: () => line3Done = true,
            animTrigger: animLine3);
        yield return new WaitUntil(() => line3Done);

        // ── Fase 5: Dolly back al wide — revela el interior oscuro ───────────
        if (camShotWide != null)
            yield return _cinematicCamera.MoveTo(camShotWide, wideShotBlendTime);

        yield return new WaitForSeconds(holdAfterLaugh);

        StartCoroutine(Co_HideCrystalVision());
        DestroyEvilOverlay();

        yield return Co_EndCinematicWithTransition(() =>
        {
            RestoreMusic();
            UnfreezeLiamNavigation();
        });

        RaiseSignalOut();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Overlay tenebroso
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_FadeInEvilOverlay()
    {
        var go = new GameObject("LiamEvilOverlay") { hideFlags = HideFlags.HideAndDontSave };
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9990; // debajo del flash (9999) y el fade (9998)
        var cg = go.AddComponent<CanvasGroup>();
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        var imgGO = new GameObject("Image");
        imgGO.transform.SetParent(go.transform, false);
        _evilOverlayImg = imgGO.AddComponent<Image>();
        var rt = _evilOverlayImg.GetComponent<RectTransform>();
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.offsetMin  = rt.offsetMax = Vector2.zero;
        _evilOverlayImg.color = Color.clear;

        // Suelo mínimo de oscuridad (ver MinRoomDarkenAlpha) — nunca más claro que esto aunque
        // evilOverlayColor se haya quedado con un alpha antiguo más bajo en el Inspector.
        Color targetColor = evilOverlayColor;
        if (targetColor.a < MinRoomDarkenAlpha) targetColor.a = MinRoomDarkenAlpha;

        float elapsed = 0f;
        while (elapsed < overlayFadeIn)
        {
            if (_evilOverlayImg == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            _evilOverlayImg.color = Color.Lerp(Color.clear, targetColor, elapsed / overlayFadeIn);
            yield return null;
        }

        if (_evilOverlayImg != null)
            _evilOverlayImg.color = targetColor;
    }

    private void DestroyEvilOverlay()
    {
        if (_evilOverlayImg == null) return;
        Destroy(_evilOverlayImg.transform.parent.gameObject);
        _evilOverlayImg = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Visión del jugador en la bola de cristal
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_ShowCrystalVision()
    {
        if (crystalVisionCamera == null || crystalBallRenderer == null || _mpb == null)
            yield break;

        crystalVisionCamera.Activate(playerTransform);

        float elapsed = 0f;
        while (elapsed < visionFadeIn)
        {
            elapsed += Time.deltaTime;
            _mpb.SetColor(EmissionColorId,
                Color.Lerp(Color.black, visionEmissionColor, elapsed / visionFadeIn));
            crystalBallRenderer.SetPropertyBlock(_mpb);
            yield return null;
        }
        _mpb.SetColor(EmissionColorId, visionEmissionColor);
        crystalBallRenderer.SetPropertyBlock(_mpb);

        crystalBallParticles?.Play();
        AudioService.Instance?.PlaySFX("CrystalBall_Activate", 1f, crystalBallRenderer.transform.position);
        StartCoroutine(Co_PulseCrystalVision());
        _showVisionCoroutine = null; // completada con normalidad, ya no hay nada que StopCoroutine() en el skip
    }

    private IEnumerator Co_PulseCrystalVision()
    {
        _visionPulsing = true;
        AudioService.Instance?.PlayLoopingSFX(CrystalPulseLoopId, "CrystalBall_PulseLoop", 0.5f);
        while (_visionPulsing)
        {
            float t = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(pulseMinScale, 1f, t);
            Color emissive = colorShimmerAmount > 0f
                ? Color.Lerp(visionEmissionColor, visionShimmerColor, t * colorShimmerAmount)
                : visionEmissionColor;
            _mpb.SetColor(EmissionColorId, emissive * scale);
            crystalBallRenderer.SetPropertyBlock(_mpb);
            yield return null;
        }
    }

    private IEnumerator Co_HideCrystalVision()
    {
        if (crystalBallRenderer == null || _mpb == null) yield break;

        _visionPulsing = false;
        yield return null; // deja que el loop de pulso salga antes de leer el color

        AudioService.Instance?.StopLoopingSFX(CrystalPulseLoopId, 0.3f);
        AudioService.Instance?.PlaySFX("CrystalBall_Deactivate", 1f, crystalBallRenderer.transform.position);

        crystalBallParticles?.Stop();

        crystalBallRenderer.GetPropertyBlock(_mpb);
        Color startColor = _mpb.GetColor(EmissionColorId);

        float elapsed = 0f;
        while (elapsed < visionFadeOut)
        {
            elapsed += Time.deltaTime;
            _mpb.SetColor(EmissionColorId,
                Color.Lerp(startColor, Color.black, elapsed / visionFadeOut));
            crystalBallRenderer.SetPropertyBlock(_mpb);
            yield return null;
        }
        crystalBallRenderer.SetPropertyBlock(null);
        crystalVisionCamera?.Deactivate();
    }
}
