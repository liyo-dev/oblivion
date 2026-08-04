using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;
using Sendero.UI;

/// <summary>
/// Orquesta la transición cinemática de salida del Reino (escena 13 → mundo abierto):
/// corte de cámara a un plano general fijo, silencio breve, entrada del tema principal
/// con crescendo, aparición y retención del logo del juego, y devolución de control
/// al jugador en el instante en que el logo se disuelve.
///
/// Si closeDemoAfterLogo está activo, en vez de devolver el control se cierra la demo:
/// fundido a negro y cambio a closeDemoTargetScene (flujo: Logo → Créditos → Título).
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

    [Header("Retorno a gameplay")]
    [Tooltip("Duración del fundido a negro (entrada y salida) que oculta el reencuadre automático de vThirdPersonCamera al desbloquear la cámara. Sin esto se ve un 'salto' de cámara ya en modo gameplay.")]
    [Min(0.05f)] public float cameraReturnFadeSeconds = 0.3f;

    [Tooltip("Margen en negro (tras el fundido de entrada) para dar tiempo a que la cámara termine su reencuadre automático antes de volver a mostrar la escena.")]
    [Min(0f)] public float cameraReturnHoldSeconds = 0.2f;

    [Header("Cierre de demo")]
    [Tooltip("Si está activo, tras el logo NO se devuelve el control al jugador: se hace un fundido a negro y se cambia de escena. Pensado para el final de la demo (flujo: Logo → Créditos → Título).")]
    public bool closeDemoAfterLogo = false;

    [Tooltip("Escena a cargar al cerrar la demo (normalmente 'Credits'; Credits.unity ya encadena a la pantalla de título por su cuenta). Ver EditorBuildSettings.")]
    public string closeDemoTargetScene = "Credits";

    [Tooltip("Escena overlay de carga a usar al cerrar la demo. Vacío = sin overlay, solo el fundido a negro de cameraReturnFadeSeconds.")]
    public string closeDemoLoadingOverlayScene = "";

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

        // --- Ocultar toda la UI de gameplay para la revelación del título ---
        PlayerHUDV2.Instance?.HideHUD();
        TimeOfDayIndicator.Instance?.Hide();
        MinimapController.Instance?.SetHiddenByCinematic(true);

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

        // --- Apagar ambiente de pueblo y música de gameplay antes del silencio ---
        // Sin esto el silencio no es tal: la música de fondo (pueblo, banter previo, etc.)
        // sigue sonando durante ambienceFadeOutSeconds + silenceDuration y solo se corta
        // de golpe (con crossfade de 6s) al arrancar mainThemeClip, dando la sensación de
        // que "la música original nunca se cortó".
        var audio = AudioService.Instance;
        float previousSfxVolume = audio != null ? audio.GetVolume(AudioBus.Sfx) : 1f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[KingdomExitTransitionNode] AudioService.Instance={(audio != null)}, " +
                  $"CurrentMusicClip antes de StopMusic='{(audio != null ? audio.CurrentMusicClip?.name : "N/A")}'");
#endif
        audio?.StopMusic(ambienceFadeOutSeconds);
        if (audio != null && ambienceFadeOutSeconds > 0f)
            yield return FadeSfxVolume(audio, previousSfxVolume, 0f, ambienceFadeOutSeconds);
        else
            audio?.SetVolume(AudioBus.Sfx, 0f);

        // --- Silencio breve antes del tema principal (ahora sí, sin música de fondo) ---
        if (silenceDuration > 0f)
            yield return new WaitForSecondsRealtime(silenceDuration);

        // --- Entrada del tema principal (crossfade de volumen; el crescendo lo aporta el propio clip) ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[KingdomExitTransitionNode] CurrentMusicClip justo antes de PlayMusic(mainThemeClip)=" +
                  $"'{(audio != null ? audio.CurrentMusicClip?.name : "N/A")}' (debería ser null/ninguno si StopMusic funcionó)");
#endif
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

        // --- Fin de la demo: cambiar a la pantalla de títulos en vez de devolver el control ---
        if (closeDemoAfterLogo)
        {
            yield return CloseDemo(audio, previousSfxVolume);
            yield break;
        }

        // --- Fundido a negro breve: oculta el reencuadre automático de vThirdPersonCamera ---
        // Al poner lockCameraForCinematic = false, vThirdPersonCamera hace un smooth-snap (~0.15s,
        // ver SnapSmoothTime) desde el plano general de vuelta a la posición normal tras el jugador.
        // Sin este fundido, ese salto se ve a pantalla completa justo cuando el jugador ya tiene
        // el control (PopMode ya aplicado), lo que se percibe como una transición fuera de lugar.
        yield return FeedbackService.ScreenFadeAsync(Color.black, cameraReturnFadeSeconds, fadeIn: true);

        // --- Restaurar ambiente y devolver control al jugador (oculto tras el negro) ---
        audio?.SetVolume(AudioBus.Sfx, previousSfxVolume);

        if (cameraCut)
            vThirdPersonCamera.lockCameraForCinematic = false;

        pam?.PopMode(ActionMode.Cinematic);

        // --- Restaurar la UI de gameplay (oculta tras el negro, junto con la cámara) ---
        PlayerHUDV2.Instance?.ShowHUD();
        TimeOfDayIndicator.Instance?.Show();
        MinimapController.Instance?.SetHiddenByCinematic(false);

        // Margen para que el smooth-snap de la cámara termine mientras la pantalla sigue en negro
        if (cameraReturnHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(cameraReturnHoldSeconds);

        yield return FeedbackService.ScreenFadeAsync(Color.black, cameraReturnFadeSeconds, fadeIn: false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log("[KingdomExitTransitionNode] Transición completada, control devuelto al jugador.");
#endif

        onReadyToAdvance?.Invoke();
    }

    /// <summary>
    /// Cierra la demo: funde a negro y carga la pantalla de títulos, sin devolver nunca el
    /// control ni la cámara al modo gameplay.
    ///
    /// A propósito NO se hace <c>vThirdPersonCamera.lockCameraForCinematic = false</c> ni
    /// <c>pam.PopMode(ActionMode.Cinematic)</c> aquí: hacerlo desbloqueaba la cámara y el modo
    /// ANTES del fundido a negro (el fundido empieza después), así que durante ese instante
    /// se veía el snap de vThirdPersonCamera de vuelta a la vista de juego y cualquier sistema
    /// que reaccione a "ya no estamos en Cinematic" (p. ej. música de mundo) se reactivaba,
    /// todo con la pantalla aún visible. El GameObject del jugador se destruye con el cambio
    /// de escena (SceneTransitionLoader.Load), así que ese Push de Cinematic nunca queda
    /// "colgado" en un stack que sobreviva — es la única excepción deliberada a la regla de
    /// "siempre Pop para cada Push" de CLAUDE.md, y es intencional: no tocar.
    /// No llama a onReadyToAdvance: la demo termina aquí, no hay más nodos que avanzar.
    /// </summary>
    IEnumerator CloseDemo(AudioService audio, float previousSfxVolume)
    {
        // Restaurar volumen SFX para que la pantalla de títulos no herede el silencio de la cinemática.
        audio?.SetVolume(AudioBus.Sfx, previousSfxVolume);

        // El tema principal (mainThemeClip) hereda loop=true por defecto (música de ambiente/gameplay).
        // Para los créditos necesitamos que termine de verdad: CreditsSceneController usa
        // AudioService.GetMusicRemainingSeconds() para mantener "Gracias por jugar la demo" en
        // pantalla hasta que el tema acabe, lo cual no tiene sentido si está en loop.
        audio?.SetMusicLooping(false);

        yield return FeedbackService.ScreenFadeAsync(Color.black, cameraReturnFadeSeconds, fadeIn: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[KingdomExitTransitionNode] Fin de la demo → cargando '{closeDemoTargetScene}'.");
#endif

        if (string.IsNullOrEmpty(closeDemoLoadingOverlayScene))
            SceneTransitionLoader.Load(closeDemoTargetScene);
        else
            SceneTransitionLoader.LoadWithOverlay(closeDemoTargetScene, closeDemoLoadingOverlayScene);

        // SceneTransitionLoader detecta FeedbackService.IsScreenFaded == true y hace el
        // fundido de salida (revela la escena de créditos) automáticamente al terminar de cargar.
    }

    static CameraFocusPoint FindFocusPoint(string focusId)
    {
        var all = UnityEngine.Object.FindObjectsByType<CameraFocusPoint>();
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
