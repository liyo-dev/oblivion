using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[Serializable]
public struct DramaticStylePreset
{
    public DramaticTextStyle style;
    public Color textColor;
    [Range(12, 180)] public float fontSize;
    public FontStyles fontStyle;
    [Tooltip("Escala inicial para la animación ScaleUp.")]
    [Range(0.01f, 0.99f)] public float scaleUpFrom;
    [Tooltip("Duración de la animación de entrada (segundos).")]
    [Range(0.05f, 2f)] public float entryDuration;
    [Tooltip("Duración de la animación de salida (segundos).")]
    [Range(0.05f, 2f)] public float exitDuration;
    [Tooltip("Posición de inicio de la entrada: el texto parte de aquí y llega al centro. Ej: (0,-30) sube desde abajo.")]
    public Vector2 entryFromOffset;
    [Tooltip("Desplazamiento que recorre el texto durante el hold (flota lentamente). Ej: (0,25) sube suavemente.")]
    public Vector2 drift;
}

/// <summary>
/// Overlay de pantalla completa para frases dramáticas: recuerdos, momentos épicos y llamadas urgentes.
/// Debe vivir en el Canvas del HUD persistente (Start.unity). Singleton.
/// </summary>
public class DramaticTextOverlayUI : MonoBehaviour
{
    public static DramaticTextOverlayUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] Image _background;
    [SerializeField] TextMeshProUGUI _label;
    [SerializeField] RectTransform _textContainer;

    [Header("Fondos")]
    [SerializeField] Color _semiBlackColor = new Color(0f, 0f, 0f, 0.65f);

    [Header("Estilos por tipo")]
    [SerializeField] DramaticStylePreset[] _stylePresets;

    [Header("TypeWriter")]
    [Tooltip("Caracteres por segundo en la animación TypeWriter.")]
    [SerializeField] float _typewriterSpeed = 28f;

    [Header("Slide")]
    [Tooltip("Distancia en píxeles UI fuera de pantalla para las animaciones SlideFrom/SlideTo.")]
    [SerializeField] float _slideOffscreenX = 1500f;

    [Header("Kingdom Hearts Style")]
    [Tooltip("Caracteres por segundo en la animación KingdomHearts (independiente de TypeWriter).")]
    [SerializeField] float _khRevealSpeed = 10f;
    [Tooltip("Píxeles de desplazamiento vertical inicial por carácter (caen hacia su posición final).")]
    [SerializeField] float _khOffsetY = 22f;
    [Tooltip("Escala inicial de cada carácter individual (se reduce hasta 1.0 al aparecer).")]
    [Range(1f, 2.5f)] [SerializeField] float _khCharScaleFrom = 1.35f;
    [Tooltip("Escala inicial del contenedor completo (zoom suave hacia 1.0 mientras aparecen las letras).")]
    [Range(1f, 1.5f)] [SerializeField] float _khContainerZoom = 1.06f;

    bool _isPlaying;
    Coroutine _playRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        if (_rootGroup == null)
            Debug.LogError("[DramaticTextOverlayUI] ❌ _rootGroup no asignado en el Inspector.", this);
        if (_label == null)
            Debug.LogError("[DramaticTextOverlayUI] ❌ _label (TextMeshProUGUI) no asignado en el Inspector.", this);
        if (_textContainer == null)
            Debug.LogError("[DramaticTextOverlayUI] ❌ _textContainer (RectTransform) no asignado en el Inspector.", this);

        if (_rootGroup != null)
        {
            _rootGroup.alpha = 0f;
            _rootGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _rootGroup?.DOKill();
            _textContainer?.DOKill();
        }
    }

    // ── API pública ───────────────────────────────────────────────────────

    public bool IsPlaying => _isPlaying;

    /// <summary>Reproduce la secuencia de frases. Llama onComplete cuando termina la última.</summary>
    public void Play(DramaticPhraseConfig config, Action onComplete)
    {
        if (config == null || config.phrases == null || config.phrases.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        gameObject.SetActive(true); // debe estar activo antes de StartCoroutine
        if (_playRoutine != null) StopCoroutine(_playRoutine);
        _playRoutine = StartCoroutine(RunSequence(config, onComplete));
    }

    /// <summary>Detiene la secuencia y oculta el overlay inmediatamente.</summary>
    public void ForceStop()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }
        DOTween.Kill(_rootGroup);
        DOTween.Kill(_textContainer);
        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
        _isPlaying = false;
        gameObject.SetActive(false);
    }

    // ── Secuencia ─────────────────────────────────────────────────────────

    IEnumerator RunSequence(DramaticPhraseConfig config, Action onComplete)
    {
        _isPlaying = true;

        for (int i = 0; i < config.phrases.Length; i++)
        {
            var current = config.phrases[i];
            bool prevFullBlack = i > 0 && config.phrases[i - 1].background == DramaticTextBackground.FullBlack;
            bool nextFullBlack = i < config.phrases.Length - 1 && config.phrases[i + 1].background == DramaticTextBackground.FullBlack;
            bool currentFullBlack = current.background == DramaticTextBackground.FullBlack;

            // Si venimos de FullBlack y la actual también lo es, no hacer fade de entrada.
            // KingdomHearts tiene su propia gestión de visibilidad (vértices en alpha=0),
            // por eso nunca salta aunque la pantalla ya esté en negro.
            bool skipEntry = prevFullBlack && currentFullBlack
                             && current.entryAnim != DramaticEntryAnimation.KingdomHearts;
            bool skipExit  = currentFullBlack && nextFullBlack;

            yield return ShowPhrase(current, skipEntry, skipExit);

            if (i < config.phrases.Length - 1 && config.pauseBetween > 0f)
                yield return new WaitForSecondsRealtime(config.pauseBetween);
        }

        gameObject.SetActive(false);
        _isPlaying = false;
        _playRoutine = null;
        onComplete?.Invoke();
    }

    IEnumerator ShowPhrase(DramaticPhrase phrase, bool skipEntry = false, bool skipExit = false)
    {
        if (_label == null || _textContainer == null || _rootGroup == null)
        {
            Debug.LogError("[DramaticTextOverlayUI] ❌ Referencias UI nulas — asigna _label, _textContainer y _rootGroup en el Inspector.");
            yield break;
        }

        DramaticStylePreset preset = GetPreset(phrase.style);
        string text = GetLocalizedText(phrase);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DramaticTextOverlay] Frase: '{text}' | Style: {phrase.style} | Anim: {phrase.entryAnim} | Duration: {phrase.duration}s | fontSize: {preset.fontSize} | color alpha: {preset.textColor.a}");
#endif

        _label.color = preset.textColor;
        _label.fontSize = preset.fontSize;
        _label.fontStyle = preset.fontStyle;

        Vector2 basePos = phrase.positionOffset;
        _textContainer.anchoredPosition = basePos;

        SetBackground(phrase.background);

        if (phrase.voiceClip != null)
            AudioService.Instance?.PlayVoice(phrase.voiceClip);

        if (skipEntry)
        {
            // Ya estamos en pantalla totalmente negra: solo actualizar texto sin fade
            _label.text = text;
            _textContainer.localScale = Vector3.one;
            _textContainer.anchoredPosition = basePos;
            _rootGroup.alpha = 1f;
            _rootGroup.blocksRaycasts = true;
        }
        else
        {
            yield return EntryAnimation(phrase.entryAnim, text, preset, basePos);
        }

        float holdDuration = phrase.waitForAudio && phrase.voiceClip != null
            ? Mathf.Max(phrase.voiceClip.length - preset.entryDuration, 0f)
            : Mathf.Max(phrase.duration, 0.1f);

        // Movimiento durante el hold: de positionOffset a moveTo (o drift del preset como fallback)
        Vector2 holdTarget;
        bool hasMovement;
        if (phrase.useMovement)
        {
            holdTarget = phrase.moveTo;
            hasMovement = true;
        }
        else if (preset.drift != Vector2.zero)
        {
            holdTarget = basePos + preset.drift;
            hasMovement = true;
        }
        else
        {
            holdTarget = basePos;
            hasMovement = false;
        }

        if (hasMovement)
            _textContainer.DOAnchorPos(holdTarget, holdDuration)
                .SetEase(Ease.InOutSine).SetUpdate(true);

        yield return new WaitForSecondsRealtime(holdDuration);

        if (!skipExit)
            yield return ExitAnimation(phrase.exitAnim, preset, basePos);
    }

    // ── Animaciones de entrada ────────────────────────────────────────────

    IEnumerator EntryAnimation(DramaticEntryAnimation anim, string text, DramaticStylePreset preset, Vector2 basePos)
    {
        _rootGroup.blocksRaycasts = true;

        switch (anim)
        {
            case DramaticEntryAnimation.ScaleUp:
                _label.text = text;
                _textContainer.localScale = Vector3.one * preset.scaleUpFrom;
                _textContainer.anchoredPosition = basePos + preset.entryFromOffset;
                _rootGroup.alpha = 0f;
                _textContainer.DOScale(Vector3.one, preset.entryDuration)
                    .SetEase(Ease.OutQuart).SetUpdate(true);
                if (preset.entryFromOffset != Vector2.zero)
                    _textContainer.DOAnchorPos(basePos, preset.entryDuration)
                        .SetEase(Ease.OutQuart).SetUpdate(true);
                yield return _rootGroup.DOFade(1f, preset.entryDuration * 0.7f)
                    .SetUpdate(true).WaitForCompletion();
                break;

            case DramaticEntryAnimation.FadeIn:
                _label.text = text;
                _textContainer.localScale = Vector3.one;
                _textContainer.anchoredPosition = basePos + preset.entryFromOffset;
                _rootGroup.alpha = 0f;
                if (preset.entryFromOffset != Vector2.zero)
                    _textContainer.DOAnchorPos(basePos, preset.entryDuration)
                        .SetEase(Ease.InOutSine).SetUpdate(true);
                yield return _rootGroup.DOFade(1f, preset.entryDuration)
                    .SetEase(Ease.InOutSine).SetUpdate(true).WaitForCompletion();
                break;

            case DramaticEntryAnimation.TypeWriter:
                _textContainer.localScale = Vector3.one;
                _textContainer.anchoredPosition = basePos + preset.entryFromOffset;
                _rootGroup.alpha = 1f;
                if (preset.entryFromOffset != Vector2.zero)
                    _textContainer.DOAnchorPos(basePos, preset.entryDuration)
                        .SetEase(Ease.InOutSine).SetUpdate(true);
                yield return TypewriterRoutine(text);
                break;

            case DramaticEntryAnimation.Instant:
                _label.text = text;
                _textContainer.localScale = Vector3.one;
                _textContainer.anchoredPosition = basePos;
                _rootGroup.alpha = 1f;
                break;

            case DramaticEntryAnimation.SlideFromLeft:
                _label.text = text;
                _textContainer.localScale = Vector3.one;
                _textContainer.anchoredPosition = new Vector2(-_slideOffscreenX, basePos.y);
                _rootGroup.alpha = 0f;
                _rootGroup.DOFade(1f, preset.entryDuration * 0.35f).SetUpdate(true);
                yield return _textContainer.DOAnchorPos(basePos, preset.entryDuration)
                    .SetEase(Ease.OutQuart).SetUpdate(true).WaitForCompletion();
                break;

            case DramaticEntryAnimation.SlideFromRight:
                _label.text = text;
                _textContainer.localScale = Vector3.one;
                _textContainer.anchoredPosition = new Vector2(_slideOffscreenX, basePos.y);
                _rootGroup.alpha = 0f;
                _rootGroup.DOFade(1f, preset.entryDuration * 0.35f).SetUpdate(true);
                yield return _textContainer.DOAnchorPos(basePos, preset.entryDuration)
                    .SetEase(Ease.OutQuart).SetUpdate(true).WaitForCompletion();
                break;

            case DramaticEntryAnimation.KingdomHearts:
                yield return KingdomHeartsRoutine(text, preset, basePos);
                break;
        }
    }

    // ── Animaciones de salida ─────────────────────────────────────────────

    IEnumerator ExitAnimation(DramaticExitAnimation anim, DramaticStylePreset preset, Vector2 basePos)
    {
        switch (anim)
        {
            case DramaticExitAnimation.FadeOut:
                yield return _rootGroup.DOFade(0f, preset.exitDuration)
                    .SetUpdate(true).WaitForCompletion();
                break;

            case DramaticExitAnimation.ScaleUp:
                _textContainer.DOScale(Vector3.one * 1.5f, preset.exitDuration)
                    .SetEase(Ease.InQuart).SetUpdate(true);
                yield return _rootGroup.DOFade(0f, preset.exitDuration)
                    .SetUpdate(true).WaitForCompletion();
                _textContainer.localScale = Vector3.one;
                break;

            case DramaticExitAnimation.Instant:
                _rootGroup.alpha = 0f;
                break;

            case DramaticExitAnimation.SlideToLeft:
            {
                float currentY = _textContainer.anchoredPosition.y;
                _rootGroup.DOFade(0f, preset.exitDuration * 0.4f)
                    .SetDelay(preset.exitDuration * 0.6f).SetUpdate(true);
                yield return _textContainer.DOAnchorPos(new Vector2(-_slideOffscreenX, currentY), preset.exitDuration)
                    .SetEase(Ease.InQuart).SetUpdate(true).WaitForCompletion();
                _rootGroup.alpha = 0f;
                break;
            }

            case DramaticExitAnimation.SlideToRight:
            {
                float currentY = _textContainer.anchoredPosition.y;
                _rootGroup.DOFade(0f, preset.exitDuration * 0.4f)
                    .SetDelay(preset.exitDuration * 0.6f).SetUpdate(true);
                yield return _textContainer.DOAnchorPos(new Vector2(_slideOffscreenX, currentY), preset.exitDuration)
                    .SetEase(Ease.InQuart).SetUpdate(true).WaitForCompletion();
                _rootGroup.alpha = 0f;
                break;
            }
        }

        _rootGroup.blocksRaycasts = false;
    }

    // ── Kingdom Hearts ────────────────────────────────────────────────────

    IEnumerator KingdomHeartsRoutine(string text, DramaticStylePreset preset, Vector2 basePos)
    {
        // El color real se bake en los vértices; el label queda en blanco para no teñir.
        _label.color = Color.white;
        _label.text = text;
        _label.ForceMeshUpdate();

        _textContainer.anchoredPosition = basePos;
        _textContainer.localScale = Vector3.one * _khContainerZoom;
        _rootGroup.alpha = 1f;
        _rootGroup.blocksRaycasts = true;

        var textInfo = _label.textInfo;
        int charCount = textInfo.characterCount;
        if (charCount == 0) yield break;

        // Caché de posiciones originales de vértices por material
        int matCount = textInfo.materialCount;
        var originalVerts = new Vector3[matCount][];
        for (int m = 0; m < matCount; m++)
            originalVerts[m] = (Vector3[])textInfo.meshInfo[m].vertices.Clone();

        Color32 targetCol = preset.textColor;
        float delayPerChar = 1f / Mathf.Max(_khRevealSpeed, 1f);
        float letterAnim   = Mathf.Max(preset.entryDuration, 0.05f);
        float totalDuration = charCount * delayPerChar + letterAnim;

        // Zoom suave del contenedor mientras aparecen las letras
        _textContainer.DOScale(Vector3.one, totalDuration)
            .SetEase(Ease.OutCubic).SetUpdate(true);

        float elapsed = 0f;
        bool done = false;

        while (!done)
        {
            elapsed += Time.unscaledDeltaTime;
            done = elapsed >= totalDuration;
            float t_global = Mathf.Min(elapsed, totalDuration);

            for (int i = 0; i < charCount; i++)
            {
                var charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible) continue;

                int matIdx = charInfo.materialReferenceIndex;
                int vIdx   = charInfo.vertexIndex;

                float t    = Mathf.Clamp01((t_global - i * delayPerChar) / letterAnim);
                float ease = KhEaseOutQuart(t);
                byte  alpha = (byte)(ease * targetCol.a);

                // Color con alpha animado
                Color32 col = new Color32(targetCol.r, targetCol.g, targetCol.b, alpha);
                var colors = textInfo.meshInfo[matIdx].colors32;
                colors[vIdx + 0] = col;
                colors[vIdx + 1] = col;
                colors[vIdx + 2] = col;
                colors[vIdx + 3] = col;

                // Escala y offset Y por carácter, alrededor de su centro
                var  verts = textInfo.meshInfo[matIdx].vertices;
                var  origV = originalVerts[matIdx];
                Vector3 center = (origV[vIdx] + origV[vIdx+1] + origV[vIdx+2] + origV[vIdx+3]) * 0.25f;
                float   scale  = Mathf.Lerp(_khCharScaleFrom, 1f, ease);
                float   ofsY   = Mathf.Lerp(_khOffsetY, 0f, ease);
                Vector3 yOfs   = new Vector3(0f, ofsY, 0f);

                for (int k = 0; k < 4; k++)
                    verts[vIdx + k] = center + (origV[vIdx + k] - center) * scale + yOfs;
            }

            _label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
            yield return null;
        }
    }

    static float KhEaseOutQuart(float t) => 1f - Mathf.Pow(1f - t, 4f);

    // ── TypeWriter ────────────────────────────────────────────────────────

    IEnumerator TypewriterRoutine(string text)
    {
        _label.text = "";
        _rootGroup.alpha = 1f;

        if (_typewriterSpeed <= 0f)
        {
            _label.text = text;
            yield break;
        }

        float delayPerChar = 1f / _typewriterSpeed;
        for (int i = 1; i <= text.Length; i++)
        {
            _label.text = text.Substring(0, i);
            yield return new WaitForSecondsRealtime(delayPerChar);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    void SetBackground(DramaticTextBackground bg)
    {
        if (_background == null) return;
        switch (bg)
        {
            case DramaticTextBackground.None:
                _background.color = Color.clear;
                break;
            case DramaticTextBackground.SemiBlack:
                _background.color = _semiBlackColor;
                break;
            case DramaticTextBackground.FullBlack:
                _background.color = Color.black;
                break;
        }
    }

    DramaticStylePreset GetPreset(DramaticTextStyle style)
    {
        if (_stylePresets != null)
        {
            foreach (var p in _stylePresets)
            {
                if (p.style != style) continue;

                // Si el preset tiene valores vacíos (struct sin configurar), usamos el fallback
                if (p.fontSize < 1f || p.textColor.a < 0.01f)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[DramaticTextOverlayUI] Preset '{style}' tiene fontSize={p.fontSize} o textColor.a={p.textColor.a}. Usando fallback. Configura el preset en el Inspector.");
#endif
                    break;
                }
                return p;
            }
        }

        // Fallback con valores visibles garantizados
        return new DramaticStylePreset
        {
            textColor = Color.white,
            fontSize = 48f,
            fontStyle = FontStyles.Normal,
            scaleUpFrom = 0.3f,
            entryDuration = 0.4f,
            exitDuration = 0.3f
        };
    }

    string GetLocalizedText(DramaticPhrase phrase)
    {
        if (!string.IsNullOrEmpty(phrase.textId))
            return LocalizationManager.Instance?.Get(phrase.textId, phrase.text) ?? phrase.text;
        return phrase.text;
    }
}
