using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Orquesta la transición cinemática de salida del Reino (escena 13 → mundo abierto):
/// corte de cámara a un plano general fijo, silencio breve, entrada del tema principal
/// con crescendo, aparición y retención del logo del juego, y devolución de control
/// al jugador en el instante en que el logo se disuelve.
///
/// Es un nodo único y autocontenido (mismo criterio que FocusCameraNode) para no
/// depender de sincronizar varios nodos en paralelo dentro del grafo. Colocar después
/// de un WaitCustomEventNode que espere el evento emitido por KingdomBoundaryTrigger
/// al cruzar el límite del Reino.
/// </summary>
[Serializable]
public sealed class KingdomExitTransitionNode : NarrativeNode
{
    [Header("Cámara")]
    [Tooltip("focusId del CameraFocusPoint con el plano general (landscape shot) de salida del Reino: grupo de espaldas, pequeño en el encuadre, mundo abierto delante.")]
    public string landscapeFocusId;

    [Header("Audio - silencio y tema principal")]
    [Tooltip("Segundos para apagar el ambiente de pueblo (gente, animales, martillos) antes del silencio.")]
    [Min(0f)] public float ambienceFadeOutSeconds = 0.4f;

    [Tooltip("Silencio total antes de que entre el tema principal (recomendado 0.5-1s).")]
    [Min(0f)] public float silenceDuration = 0.75f;

    [Tooltip("Tema principal del juego. El propio clip debe empezar con instrumento solista (piano/arpa/voces) y crecer en orquestación; el fade de aquí solo controla el volumen general de entrada.")]
    public AudioClip mainThemeClip;

    [Tooltip("Duración del fade-in de volumen de la música (sugerido 4-8 compases, ≈ 6-10s según tempo).")]
    [Min(0.1f)] public float musicFadeInSeconds = 6f;

    [Header("Logo")]
    public Sprite logoSprite;
    [Tooltip("Subtítulo o lema opcional bajo el logo.")]
    public string subtitle;
    [Tooltip("Retraso tras el inicio de la música antes de que aparezca el logo, para que la música 'respire' primero.")]
    [Min(0f)] public float logoDelayAfterMusicStart = 0.5f;
    [Min(0.01f)] public float logoFadeInSeconds = 1.75f;
    [Min(0f)]    public float logoHoldSeconds = 3.5f;
    [Min(0.01f)] public float logoFadeOutSeconds = 1.25f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        ctx.Runner.StartCoroutine(Run(onReadyToAdvance));
    }

    IEnumerator Run(Action onReadyToAdvance)
    {
        // --- Bloquear al jugador (ActionMode.Cinematic) ---
        PlayerActionManager pam = null;
        if (PlayerService.TryGetPlayer(out var playerGo, false))
        {
            pam = playerGo.GetComponent<PlayerActionManager>();
            pam?.PushMode(ActionMode.Cinematic);
        }

        // --- Corte de cámara al plano general fijo ---
        var cam = Camera.main;
        bool cameraCut = false;
        if (cam != null && !string.IsNullOrWhiteSpace(landscapeFocusId))
        {
            var focusPoint = FindFocusPoint(landscapeFocusId);
            if (focusPoint != null)
            {
                vThirdPersonCamera.lockCameraForCinematic = true;
                cam.transform.SetPositionAndRotation(focusPoint.transform.position, focusPoint.transform.rotation);
                cameraCut = true;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.LogWarning($"[KingdomExitTransitionNode] No se encontró CameraFocusPoint con focusId='{landscapeFocusId}'.");
#endif
        }

        yield return null; // dejar que el nuevo transform de cámara se procese antes de continuar

        // --- Apagar ambiente de pueblo antes del silencio ---
        var audio = AudioService.Instance;
        float previousSfxVolume = audio != null ? audio.GetVolume(AudioBus.Sfx) : 1f;
        if (audio != null && ambienceFadeOutSeconds > 0f)
            yield return FadeSfxVolume(audio, previousSfxVolume, 0f, ambienceFadeOutSeconds);
        else
            audio?.SetVolume(AudioBus.Sfx, 0f);

        // --- Silencio breve antes del tema principal ---
        if (silenceDuration > 0f)
            yield return new WaitForSecondsRealtime(silenceDuration);

        // --- Entrada del tema principal (crossfade de volumen; el crescendo lo aporta el propio clip) ---
        if (mainThemeClip != null)
            audio?.PlayMusic(mainThemeClip, musicFadeInSeconds);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
            Debug.LogWarning("[KingdomExitTransitionNode] No hay mainThemeClip asignado.");
#endif

        // --- Aparición del logo (sobreimpreso, fade-in lento) ---
        if (logoDelayAfterMusicStart > 0f)
            yield return new WaitForSecondsRealtime(logoDelayAfterMusicStart);

        var logoController = TitleLogoController.EnsureInstance();
        yield return logoController.ShowLogo(logoSprite, subtitle, logoFadeInSeconds, logoHoldSeconds, logoFadeOutSeconds);

        // --- Restaurar ambiente y devolver control al jugador ---
        audio?.SetVolume(AudioBus.Sfx, previousSfxVolume);

        if (cameraCut)
            vThirdPersonCamera.lockCameraForCinematic = false;

        pam?.PopMode(ActionMode.Cinematic);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[KingdomExitTransitionNode] Transición completada, control devuelto al jugador.");
#endif

        onReadyToAdvance?.Invoke();
    }

    static CameraFocusPoint FindFocusPoint(string focusId)
    {
        var all = UnityEngine.Object.FindObjectsByType<CameraFocusPoint>(FindObjectsSortMode.None);
        foreach (var p in all)
            if (p.focusId == focusId) return p;
        return null;
    }

    static IEnumerator FadeSfxVolume(AudioService audio, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            audio.SetVolume(AudioBus.Sfx, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        audio.SetVolume(AudioBus.Sfx, to);
    }
}
