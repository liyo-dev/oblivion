using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia de Liam mirando la bola de cristal.
/// Revela que el demonio fue cosa suya y que Will es el chico de corazón puro.
/// Señal de entrada: "LIAM_CRYSTAL_START".
/// Señal de salida:  "LIAM_CRYSTAL_DONE".
[DisallowMultipleComponent]
public class LiamCrystalBallSequencer : CinematicSequencerBase
{
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

    [Header("Entorno — interior House1")]
    [SerializeField] private AnchorEnvironment anchorEnvironment;

    [Header("SpeechBubble — claves de localización")]
    [SerializeField] private string keyLine1 = "EVT_LIAM_CRYSTAL_01";
    [SerializeField] private string keyLine2 = "EVT_LIAM_CRYSTAL_02";
    [SerializeField] private string keyLine3 = "EVT_LIAM_CRYSTAL_03";

    [Header("SpeechBubble — emociones")]
    [SerializeField] private NPCEmotion emotionLine1 = NPCEmotion.Thinking;
    [SerializeField] private NPCEmotion emotionLine2 = NPCEmotion.Happy;
    [SerializeField] private NPCEmotion emotionLine3 = NPCEmotion.Happy;
    [SerializeField] private string animLine1 = "Thinking01";
    [SerializeField] private string animLine2 = "Smug01";
    [SerializeField] private string animLine3 = "Laugh01";

    [Header("Timings")]
    [SerializeField] private float fadeInDuration       = 0.4f;
    [SerializeField] private float holdOnCrystalBall    = 1.2f;
    [SerializeField] private float line1Duration        = 3.0f;
    [SerializeField] private float line2Duration        = 3.2f;
    [SerializeField] private float line3Duration        = 2.8f;
    [SerializeField] private float holdAfterLaugh       = 1.0f;
    [SerializeField] private float fadeOutDuration      = 0.5f;
    [Tooltip("Duración del dolly back al plano wide (0 = sin dolly)")]
    [SerializeField] private float wideShotBlendTime    = 1.2f;

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
    [Tooltip("Color del overlay persistente que oscurece la escena durante la cinemática. Alpha controla intensidad.")]
    [SerializeField] private Color evilOverlayColor   = new Color(0.08f, 0f, 0.04f, 0.38f);
    [Tooltip("Tiempo de fade-in del overlay oscuro (ocurre durante el plano de la bola de cristal).")]
    [SerializeField] private float overlayFadeIn      = 0.6f;
    [Tooltip("Color del pulso siniestro que aparece entre frases. Alpha bajo para sutileza.")]
    [SerializeField] private Color evilFlashColor     = new Color(0.7f, 0f, 0.05f, 0.10f);
    [Tooltip("Duración del fade-out de cada pulso siniestro.")]
    [SerializeField] private float evilFlashDuration  = 2.5f;
    private NPCEmotionController _liamEmotion;
    private Image _evilOverlayImg;

    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private bool _visionPulsing;

    protected override void Awake()
    {
        base.Awake();

        if (liamTransform != null)
            _liamEmotion = liamTransform.GetComponentInChildren<NPCEmotionController>();

        if (crystalBallRenderer != null)
            _mpb = new MaterialPropertyBlock();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _visionPulsing = false;
        DestroyEvilOverlay();
        crystalVisionCamera?.Deactivate();
        crystalBallParticles?.Stop();
        if (crystalBallRenderer != null)
            crystalBallRenderer.SetPropertyBlock(null);
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    protected override IEnumerator Co_Sequence()
    {
        BeginCinematic();
        EnvironmentController.Instance?.BeginCinematicOverride();

        yield return FeedbackService.ScreenFadeAsync(Color.black, _fadeInDuration, fadeIn: false);

        EnvironmentController.Instance?.ApplyInteriorForCinematic(env: anchorEnvironment);
        PlaySequenceMusic();

        // FASE 1: Plano detalle bola de cristal — imagen de Will brillando dentro
        _cinematicCamera.Cut(camShotCrystalBall);
        StartCoroutine(Co_FadeInEvilOverlay());
        StartCoroutine(Co_ShowCrystalVision());
        yield return new WaitForSeconds(holdOnCrystalBall);

        // FASE 2: Plano medio Liam — la visión sigue activa en la bola de cristal
        _cinematicCamera.Cut(camShotLiamMedium);

        if (emotionLine1 != NPCEmotion.None)
            _liamEmotion?.SetEmotion(emotionLine1);

        bool line1Done = false;
        SpeechBubbleUI.Instance.Show(liamTransform, Loc(keyLine1),
            duration: line1Duration,
            onComplete: () => line1Done = true,
            animTrigger: animLine1);

        yield return new WaitUntil(() => line1Done);

        // Pulso siniestro entre frases — atmósfera de amenaza
        FeedbackService.ScreenFlash(evilFlashColor, evilFlashDuration);

        // FASE 3: Mismo plano medio — frase 2
        if (emotionLine2 != NPCEmotion.None)
            _liamEmotion?.SetEmotion(emotionLine2);

        bool line2Done = false;
        SpeechBubbleUI.Instance.Show(liamTransform, Loc(keyLine2),
            duration: line2Duration,
            onComplete: () => line2Done = true,
            animTrigger: animLine2);

        yield return new WaitUntil(() => line2Done);

        // Pulso siniestro antes de la risa — escalada de tensión
        FeedbackService.ScreenFlash(evilFlashColor, evilFlashDuration);

        // FASE 4: Primer plano del rostro — frase 3 (la risa)
        _cinematicCamera.Cut(camShotLiamFace);

        if (emotionLine3 != NPCEmotion.None)
            _liamEmotion?.SetEmotion(emotionLine3);

        bool line3Done = false;
        SpeechBubbleUI.Instance.Show(liamTransform, Loc(keyLine3),
            duration: line3Duration,
            onComplete: () => line3Done = true,
            animTrigger: animLine3);

        yield return new WaitUntil(() => line3Done);

        // FASE 5: Dolly back al wide (opcional) — revela el interior oscuro antes del fade
        if (camShotWide != null)
            yield return _cinematicCamera.MoveTo(camShotWide, wideShotBlendTime);

        yield return new WaitForSeconds(holdAfterLaugh);

        // Apagar la visión de la bola al final, junto con el overlay
        StartCoroutine(Co_HideCrystalVision());
        DestroyEvilOverlay();

        yield return FeedbackService.ScreenFadeAsync(Color.black, _fadeOutDuration, fadeIn: true);

        RestoreMusic();
        EndCinematic();
        EnvironmentController.Instance?.EndCinematicOverride();

        yield return FeedbackService.ScreenFadeAsync(Color.black, _fadeInDuration, fadeIn: false);

        RaiseSignalOut();
    }

    // ── Overlay tenebroso ─────────────────────────────────────────────────────

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

        float elapsed = 0f;
        while (elapsed < overlayFadeIn)
        {
            if (_evilOverlayImg == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            _evilOverlayImg.color = Color.Lerp(Color.clear, evilOverlayColor, elapsed / overlayFadeIn);
            yield return null;
        }

        if (_evilOverlayImg != null)
            _evilOverlayImg.color = evilOverlayColor;
    }

    private void DestroyEvilOverlay()
    {
        if (_evilOverlayImg == null) return;
        Destroy(_evilOverlayImg.transform.parent.gameObject);
        _evilOverlayImg = null;
    }

    // ── Visión del jugador en la bola de cristal ──────────────────────────────

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
        StartCoroutine(Co_PulseCrystalVision());
    }

    private IEnumerator Co_PulseCrystalVision()
    {
        _visionPulsing = true;
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
