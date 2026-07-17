using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia de Liam mirando la bola de cristal.
/// Revela que el demonio fue cosa suya y que Will es el chico de corazón puro.
/// Señal de entrada: "LIAM_CRYSTAL_START".
/// Señal de salida:  "LIAM_CRYSTAL_DONE".
[DisallowMultipleComponent]
public class LiamCrystalBallSequencer : MonoBehaviour
{
    [Header("Personajes")]
    [SerializeField] private Transform liamTransform;

    [Header("Cámara — driver y planos")]
    [SerializeField] private CinematicCameraDriver cinematicCamera;
    [Tooltip("Plano detalle de la bola de cristal con Will visible dentro")]
    [SerializeField] private Transform camShotCrystalBall;
    [Tooltip("Plano medio de Liam inclinado sobre la bola, cara en penumbra")]
    [SerializeField] private Transform camShotLiamMedium;
    [Tooltip("Primer plano del rostro de Liam — para la risa final")]
    [SerializeField] private Transform camShotLiamFace;
    [Tooltip("Plano wide opcional: dolly back que revela el interior oscuro al final")]
    [SerializeField] private Transform camShotWide;

    [Header("Música")]
    [SerializeField] private AudioGraphProfile audioProfile;
    [SerializeField] private string sequenceMusicId = "LIAM_CRYSTAL";

    [Header("Entorno — interior House1")]
    [SerializeField] private AnchorEnvironment anchorEnvironment;

    [Header("Gameplay")]
    [SerializeField] private PlayerActionManager actionManager;

    [Header("Señales narrativas")]
    [SerializeField] private string signalIn  = "LIAM_CRYSTAL_START";
    [SerializeField] private string signalOut = "LIAM_CRYSTAL_DONE";

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

    [Header("Efectos atmosféricos")]
    [Tooltip("Color del overlay persistente que oscurece la escena durante la cinemática. Alpha controla intensidad.")]
    [SerializeField] private Color evilOverlayColor   = new Color(0.08f, 0f, 0.04f, 0.38f);
    [Tooltip("Tiempo de fade-in del overlay oscuro (ocurre durante el plano de la bola de cristal).")]
    [SerializeField] private float overlayFadeIn      = 0.6f;
    [Tooltip("Color del pulso siniestro que aparece entre frases. Alpha bajo para sutileza.")]
    [SerializeField] private Color evilFlashColor     = new Color(0.7f, 0f, 0.05f, 0.10f);
    [Tooltip("Duración del fade-out de cada pulso siniestro.")]
    [SerializeField] private float evilFlashDuration  = 2.5f;
    [Tooltip("Intensidad del shake ambiental continuo durante la secuencia.")]
    [SerializeField] private float cameraShakeAmbient = 0.018f;

    private NPCEmotionController _liamEmotion;
    private Image _evilOverlayImg;

    void Awake()
    {
        if (liamTransform != null)
            _liamEmotion = liamTransform.GetComponentInChildren<NPCEmotionController>();

        DefaultNarrativeSignals.EnsureInstance().OnCustom(signalIn,
            () => StartCoroutine(Co_Sequence()));
    }

    void OnDestroy() => DestroyEvilOverlay();

    private string Loc(string key) => LocalizationManager.Instance != null
        ? LocalizationManager.Instance.Get(key, key)
        : key;

    // ── Secuencia principal ───────────────────────────────────────────────────

    private IEnumerator Co_Sequence()
    {
        var musicRule = audioProfile?.GetSequenceRule(sequenceMusicId);

        actionManager.PushMode(ActionMode.Cinematic);
        cinematicCamera.Activate();

        EnvironmentController.Instance?.BeginCinematicOverride();

        // Fade de entrada — venimos de otro contexto; que la pantalla abra en el interior de Liam
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeInDuration, fadeIn: false);

        // Pantalla en negro: aplicar entorno de interior (sin skybox, fondo negro)
        EnvironmentController.Instance?.ApplyInteriorForCinematic(env: anchorEnvironment);

        // Música ambiental de la escena de Liam
        if (musicRule?.music != null && AudioService.Instance != null)
            AudioService.Instance.PlayMusic(musicRule.music, musicRule.fadeIn);

        // FASE 1: Plano detalle bola de cristal — imagen de Will brillando dentro
        // Arrancamos el overlay oscuro y el shake ambiental en paralelo con el plano
        cinematicCamera.Cut(camShotCrystalBall);
        StartCoroutine(Co_FadeInEvilOverlay());
        float shakeTotal = holdOnCrystalBall + line1Duration + line2Duration + line3Duration
                         + holdAfterLaugh + wideShotBlendTime;
        FeedbackService.CameraShake(cameraShakeAmbient, shakeTotal);
        yield return new WaitForSeconds(holdOnCrystalBall);

        // FASE 2: Plano medio Liam — frase 1
        cinematicCamera.Cut(camShotLiamMedium);

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
        cinematicCamera.Cut(camShotLiamFace);

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
        {
            yield return cinematicCamera.MoveTo(camShotWide, wideShotBlendTime);
        }

        yield return new WaitForSeconds(holdAfterLaugh);

        // Destruir overlay antes del fade a negro para que no compita con él
        DestroyEvilOverlay();

        // Fade a negro
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeOutDuration, fadeIn: true);

        // Restaurar música de gameplay; si no hay regla de escena, detener limpiamente
        if (AudioService.Instance != null)
        {
            float fadeDur = musicRule?.fadeOut ?? 0.8f;
            if (!AudioService.Instance.RestoreSceneMusic(fadeDur))
                AudioService.Instance.StopMusic(fadeDur);
        }

        // Restaurar cámara y modo de juego mientras la pantalla sigue en negro
        cinematicCamera.Deactivate();
        actionManager.PopMode(ActionMode.Cinematic);
        EnvironmentController.Instance?.EndCinematicOverride();

        // Abrir pantalla de vuelta para revelar el gameplay
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeInDuration, fadeIn: false);

        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signalOut);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular secuencia")]
    void SimulateSequence() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signalIn);
#endif
}
