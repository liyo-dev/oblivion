using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class CinematicDirector : MonoBehaviour
{
    [Header("Sequence")]
    public CinematicSequence sequence;

    [Header("Camera Rig")]
    public Camera targetCamera;                // si no se asigna, usa Camera.main
    public Transform cameraRig;                // un GameObject vacío que moveremos; si es nulo, moveremos la propia cámara

    [Header("HUD (auto-instancia si está vacío)")]
    public Canvas hudCanvas;
    public Image fadeImage;
    public TextMeshProUGUI subtitleTMP;

    [Header("Audio")]
    public AudioSource sfxSource;

    [Header("Gameplay Handoff")]
    public GameObject playerRoot;              // para habilitar input/acciones al final
    public Behaviour playerInput;              // ej. PlayerInput (InputSystem) o tu PlayerActionManager

    bool running;
    float defaultFOV;

    void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) Debug.LogError("[CinematicDirector] No hay cámara asignada ni Camera.main.");

        if (!cameraRig) cameraRig = targetCamera ? targetCamera.transform : null;

        if (!hudCanvas) BuildHUD();
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }

    void Start()
    {
        if (sequence)
        {
            if (sequence.playOnlyOnce && IsCinematicSeen(GetSequenceId()))
            {
                if (sequence.handOffToGameplayOnEnd)
                {
                    if (subtitleTMP) subtitleTMP.text = "";
                    if (fadeImage) fadeImage.canvasRenderer.SetAlpha(0f);
                    SetPlayerInput(true);
                }
                Debug.Log($"[CinematicDirector] Saltando cinemática ya vista: {GetSequenceId()}");
                return;
            }
            StartCoroutine(Run());
        }
    }

    public void Play(CinematicSequence seq)
    {
        if (running) return;
        sequence = seq;

        if (sequence.playOnlyOnce && IsCinematicSeen(GetSequenceId()))
        {
            if (sequence.handOffToGameplayOnEnd)
            {
                if (subtitleTMP) subtitleTMP.text = "";
                if (fadeImage) fadeImage.canvasRenderer.SetAlpha(0f);
                SetPlayerInput(true);
            }
            Debug.Log($"[CinematicDirector] Saltando cinemática ya vista (Play): {GetSequenceId()}");
            return;
        }

        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        defaultFOV = targetCamera ? targetCamera.fieldOfView : 60f;

        // Lock input
        if (sequence.lockPlayerInput) SetPlayerInput(false);

        foreach (var shot in sequence.shots)
        {
            // Skip handling
            if (sequence.skippable && Input.GetKey(sequence.skipKey))
                break;

            // OnStart events
            shot.onStart?.Invoke();

            switch (shot.type)
            {
                case CinematicSequence.ShotType.Wait:
                    yield return Wait(shot.duration);
                    break;

                case CinematicSequence.ShotType.CameraMove:
                    yield return DoCameraMove(shot);
                    break;

                case CinematicSequence.ShotType.FocusTarget:
                    yield return DoFocus(shot);
                    break;

                case CinematicSequence.ShotType.ShowText:
                    yield return DoShowText(shot);
                    break;

                case CinematicSequence.ShotType.FadeOnly:
                    yield return DoFade(shot);
                    break;

                case CinematicSequence.ShotType.PlaySfx:
                    PlaySfx(shot);
                    yield return Wait(shot.duration);
                    break;
            }

            // OnEnd events
            shot.onEnd?.Invoke();
        }

        // Handoff al gameplay
        if (sequence.handOffToGameplayOnEnd)
        {
            if (subtitleTMP) subtitleTMP.text = "";
            yield return FadeTo(0f, 0.25f);
            SetPlayerInput(true);
        }

        if (sequence.playOnlyOnce)
        {
            MarkCinematicSeen(GetSequenceId());
        }

        running = false;
    }

    IEnumerator DoCameraMove(CinematicSequence.Shot shot)
    {
        if (!cameraRig) yield break;

        // Estado inicial
        Vector3 startPos = (shot.from ? shot.from.position : cameraRig.position);
        Quaternion startRot = (shot.from ? shot.from.rotation : cameraRig.rotation);
        float startFOV = targetCamera ? targetCamera.fieldOfView : defaultFOV;

        // path (si existe, se interpola por waypoints; si no, LERP directo a 'to')
        List<Transform> path = new();
        if (shot.pathRoot)
        {
            foreach (Transform t in shot.pathRoot) path.Add(t);
        }

        if (path.Count > 0)
        {
            // Precompute segmentos
            var points = path.ToArray();
            float dur = Mathf.Max(0.01f, shot.duration) / Mathf.Max(0.01f, sequence.timeScale);
            float elapsed = 0f;

            // Opcional fade/text
            if (shot.doFadeIn) yield return FadeTo(0f, shot.fadeDuration);

            while (elapsed < dur)
            {
                if (AbortOnSkip()) yield break;

                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                float k = shot.ease.Evaluate(u);

                // interpolación piecewise lineal entre waypoints
                Vector3 pos = EvaluatePolyline(points, k);
                cameraRig.position = pos;

                // Orientación
                if (shot.lookAt)
                {
                    Vector3 dir = (shot.lookAt.position - cameraRig.position).normalized;
                    if (dir.sqrMagnitude > 0.0001f)
                        cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, Quaternion.LookRotation(dir, Vector3.up), 0.9f);
                }
                else if (shot.to)
                {
                    cameraRig.rotation = Quaternion.Slerp(startRot, shot.to.rotation, k);
                }

                // FOV
                if (targetCamera && shot.targetFOV >= 1f)
                    targetCamera.fieldOfView = Mathf.Lerp(startFOV, shot.targetFOV, k);

                yield return null;
            }
        }
        else if (shot.to)
        {
            float dur = Mathf.Max(0.01f, shot.duration) / Mathf.Max(0.01f, sequence.timeScale);
            float elapsed = 0f;

            if (shot.doFadeIn) yield return FadeTo(0f, shot.fadeDuration);

            while (elapsed < dur)
            {
                if (AbortOnSkip()) yield break;

                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                float k = shot.ease.Evaluate(u);

                cameraRig.position = Vector3.Lerp(startPos, shot.to.position, k);

                if (shot.lookAt)
                {
                    var dir = (shot.lookAt.position - cameraRig.position).normalized;
                    if (dir.sqrMagnitude > 0.0001f)
                        cameraRig.rotation = Quaternion.Slerp(startRot, Quaternion.LookRotation(dir, Vector3.up), k);
                }
                else
                {
                    cameraRig.rotation = Quaternion.Slerp(startRot, shot.to.rotation, k);
                }

                if (targetCamera && shot.targetFOV >= 1f)
                    targetCamera.fieldOfView = Mathf.Lerp(startFOV, shot.targetFOV, k);

                yield return null;
            }
        }

        if (shot.doFadeOut) yield return FadeTo(1f, shot.fadeDuration);
    }

    IEnumerator DoFocus(CinematicSequence.Shot shot)
    {
        if (!cameraRig || !shot.lookAt) yield break;

        float dur = Mathf.Max(0.01f, shot.duration) / Mathf.Max(0.01f, sequence.timeScale);
        float elapsed = 0f;

        while (elapsed < dur)
        {
            if (AbortOnSkip()) yield break;

            elapsed += Time.deltaTime;
            Vector3 dir = (shot.lookAt.position - cameraRig.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
                cameraRig.rotation = Quaternion.Slerp(cameraRig.rotation, Quaternion.LookRotation(dir, Vector3.up), 0.15f);
            yield return null;
        }
    }

    IEnumerator DoShowText(CinematicSequence.Shot shot)
    {
        if (!subtitleTMP) yield break;

        if (shot.subtitleLeadIn > 0f)
            yield return Wait(shot.subtitleLeadIn);

        // Fade in
        subtitleTMP.text = shot.subtitleText;
        yield return SubtitleFade(0f, 1f, Mathf.Max(0.01f, shot.subtitleFade));

        // Hold
        yield return Wait(shot.subtitleHold);

        // Fade out
        yield return SubtitleFade(1f, 0f, Mathf.Max(0.01f, shot.subtitleFade));
        subtitleTMP.text = "";
    }

    IEnumerator DoFade(CinematicSequence.Shot shot)
    {
        if (shot.doFadeIn) yield return FadeTo(0f, shot.fadeDuration);
        if (shot.doFadeOut) yield return FadeTo(1f, shot.fadeDuration);
        yield return Wait(shot.duration);
    }

    void PlaySfx(CinematicSequence.Shot shot)
    {
        if (shot.sfx && sfxSource)
            sfxSource.PlayOneShot(shot.sfx, shot.sfxVolume);
    }

    // --- Helpers ---

    bool AbortOnSkip()
    {
        return sequence.skippable && Input.GetKey(sequence.skipKey);
    }

    IEnumerator Wait(float seconds)
    {
        float t = 0f;
        float dur = seconds / Mathf.Max(0.01f, sequence.timeScale);
        while (t < dur)
        {
            if (AbortOnSkip()) yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    Vector3 EvaluatePolyline(Transform[] pts, float t)
    {
        if (pts == null || pts.Length == 0) return cameraRig.position;
        if (pts.Length == 1) return pts[0].position;

        float segT = t * (pts.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(segT), 0, pts.Length - 2);
        float u = segT - i;
        return Vector3.Lerp(pts[i].position, pts[i+1].position, u);
    }

    void SetPlayerInput(bool isEnabled)
    {
        if (playerInput) playerInput.enabled = isEnabled;
        if (playerRoot) playerRoot.SetActive(true); // por si lo desactivaste antes
    }

    void BuildHUD()
    {
        hudCanvas = new GameObject("CineHUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = hudCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var fadeGo = new GameObject("Fade", typeof(Image));
        fadeGo.transform.SetParent(hudCanvas.transform, false);
        fadeImage = fadeGo.GetComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.rectTransform.anchorMin = Vector2.zero;
        fadeImage.rectTransform.anchorMax = Vector2.one;
        fadeImage.rectTransform.offsetMin = Vector2.zero;
        fadeImage.rectTransform.offsetMax = Vector2.zero;
        fadeImage.canvasRenderer.SetAlpha(0f);

        var textGo = new GameObject("Subtitle", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(hudCanvas.transform, false);
        subtitleTMP = textGo.GetComponent<TextMeshProUGUI>();
        subtitleTMP.alignment = TextAlignmentOptions.Center;
        subtitleTMP.fontSize = 36;
        subtitleTMP.textWrappingMode = TextWrappingModes.Normal;
        var rt = subtitleTMP.rectTransform;
        rt.anchorMin = new Vector2(0.1f, 0.05f);
        rt.anchorMax = new Vector2(0.9f, 0.25f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        subtitleTMP.text = "";
        subtitleTMP.alpha = 0f;
    }

    IEnumerator FadeTo(float target, float duration)
    {
        if (!fadeImage) yield break;
        float start = fadeImage.canvasRenderer.GetAlpha();
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(start, target, k);
            fadeImage.canvasRenderer.SetAlpha(a);
            yield return null;
        }
        fadeImage.canvasRenderer.SetAlpha(target);
    }

    IEnumerator SubtitleFade(float from, float to, float duration)
    {
        if (!subtitleTMP) yield break;
        float t = 0f;
        subtitleTMP.alpha = from;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            subtitleTMP.alpha = Mathf.Lerp(from, to, k);
            yield return null;
        }
        subtitleTMP.alpha = to;
    }

    // --- Single-play helpers ---
    string GetSequenceId()
    {
        if (!sequence) return string.Empty;
        if (!string.IsNullOrEmpty(sequence.sequenceId)) return sequence.sequenceId;
        return sequence.name;
    }

    bool IsCinematicSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        var profile = GameBootService.Profile;
        if (profile == null) return false;
        var preset = profile.GetActivePresetResolved();
        if (preset == null) return false;
        if (preset.flags == null) return false;
        string flag = $"CINEMATIC_SEEN:{id}";
        return preset.flags.Contains(flag);
    }

    void MarkCinematicSeen(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;
        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new List<string>();
        string flag = $"CINEMATIC_SEEN:{id}";
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);

        var saveSystem = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
        if (saveSystem != null)
        {
            profile.SaveCurrentGameState(saveSystem, SaveRequestContext.Auto);
        }
    }
}
