using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// <summary>
/// Corta la cámara del jugador al CameraFocusPoint indicado, aguanta holdDuration segundos
/// y corta de vuelta. Bloquea la vThirdPersonCamera durante el efecto (vía CameraDirectorService)
/// y manipula Camera.main directamente (igual que DeathCameraEffect).
///
/// Si este nodo se ejecuta justo después de una cinemática que terminó con
/// Co_EndCinematicStayBlack (pantalla cubierta a propósito para no revelar gameplay de por
/// medio), este nodo detecta FeedbackService.IsScreenFaded y es él quien revela, cortando la
/// cámara mientras la pantalla sigue tapada. Si la pantalla no está cubierta (uso normal, en
/// medio del gameplay visible), el nodo se comporta exactamente igual que antes.
///
/// Opcionalmente aplica zoom in → hold → zoom out cambiando el FOV de la cámara.
///
/// 16 ago 2026: también oculta el techo de nubes de tormenta (CloudCoverSpawner) y las nubes
/// sueltas ambientales (AmbientCloudDirector) mientras dura el foco, por si alguna tapa el punto
/// que se quiere mostrar (caso detectado en el foco de MOUNTAIN_EXPLOSION_EVENT). Solo se tocan
/// los renderers, nunca el estado interno de esos sistemas, así que la lluvia/tormenta en curso
/// no se ve afectada y todo vuelve exactamente como estaba al terminar el foco.
/// </summary>
[Serializable]
public sealed class FocusCameraNode : NarrativeNode
{
    [Tooltip("focusId del CameraFocusPoint en escena al que cortar la cámara.")]
    public string focusId;

    [Tooltip("Segundos que la cámara permanece en el punto de foco.")]
    public float holdDuration = 3f;

    [Header("Reaparición (solo si la pantalla llega cubierta desde una cinemática)")]
    [Tooltip("Duración del fundido de reaparición si este nodo recibe la pantalla ya cubierta (ej: tras Co_EndCinematicStayBlack). Si la pantalla no está cubierta no se aplica ningún fundido.")]
    public float revealFadeDuration = 0.3f;

    [Tooltip("Si true, el grafo espera a que acabe el ciclo. Si false, avanza inmediatamente (fire & forget).")]
    public bool waitForCompletion = true;

    [Header("Zoom (opcional)")]
    [Tooltip("Factor de FOV relativo durante el hold. 1 = sin zoom. 0.7 = zoom in (FOV al 70%).")]
    [Range(0.3f, 1.5f)]
    public float zoomFactor = 1f;

    [Tooltip("Duración del zoom in en segundos (0 = instantáneo).")]
    public float zoomInDuration = 0.3f;

    [Tooltip("Duración del zoom out en segundos (0 = instantáneo).")]
    public float zoomOutDuration = 0.3f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var focusPoint = FindFocusPoint();
        if (focusPoint == null)
        {
            Debug.LogWarning($"[FocusCameraNode] No se encontró CameraFocusPoint con focusId='{focusId}'.");
            onReadyToAdvance?.Invoke();
            return;
        }

        if (waitForCompletion)
            ctx.Runner.StartCoroutine(DoFocus(focusPoint, onReadyToAdvance));
        else
        {
            ctx.Runner.StartCoroutine(DoFocus(focusPoint, null));
            onReadyToAdvance?.Invoke();
        }
    }

    CameraFocusPoint FindFocusPoint()
    {
        var all = UnityEngine.Object.FindObjectsByType<CameraFocusPoint>();
        foreach (var p in all)
            if (p.focusId == focusId) return p;
        return null;
    }

    IEnumerator DoFocus(CameraFocusPoint target, Action done)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[FocusCameraNode] Camera.main no encontrada. Asegúrate de que la cámara del jugador está taggeada como MainCamera.");
            done?.Invoke();
            yield break;
        }

        float originalFOV = cam.fieldOfView;
        float targetFOV   = originalFOV * Mathf.Clamp(zoomFactor, 0.3f, 1.5f);
        bool  doZoom      = Mathf.Abs(zoomFactor - 1f) > 0.01f;

        // --- CUT IN ---
        // Pausar el DayNightCycle para que no sobreescriba la niebla durante el foco
        var dayNight = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
        bool dayNightWasEnabled = dayNight != null && dayNight.enabled;
        if (dayNight != null) dayNight.enabled = false;

        bool fogWasEnabled = RenderSettings.fog;
        RenderSettings.fog = false;

        // Ocultar nubes (techo de tormenta + nubes sueltas ambientales) durante el foco: si una
        // nube cae delante del punto de foco (p.ej. la explosión de la montaña en
        // MountainSequencer) puede tapar del todo lo que se quiere mostrar. Solo se desactivan
        // los renderers (ver SetRenderersVisible/SetAmbientCloudsVisible), nunca su estado
        // interno, así que al restaurar vuelven exactamente como estaban.
        var cloudCover = UnityEngine.Object.FindAnyObjectByType<CloudCoverSpawner>();
        if (cloudCover != null) cloudCover.SetRenderersVisible(false);

        var ambientClouds = UnityEngine.Object.FindAnyObjectByType<AmbientCloudDirector>();
        if (ambientClouds != null) ambientClouds.SetAmbientCloudsVisible(false);

        CameraDirectorService.Claim(this);
        cam.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

        // Si venimos de una cinemática que se quedó en negro a propósito (Co_EndCinematicStayBlack),
        // el corte de arriba ya ocurrió con la pantalla tapada: revelamos nosotros en vez de dejar
        // que se vea un frame de gameplay antes de este enfoque.
        if (FeedbackService.IsScreenFaded && revealFadeDuration > 0f)
            yield return FeedbackService.ScreenFadeAsync(Color.black, revealFadeDuration, fadeIn: false);

        // Esperar un frame para que el nuevo transform se procese
        yield return null;

        // --- ZOOM IN ---
        if (doZoom && zoomInDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < zoomInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.fieldOfView = Mathf.Lerp(originalFOV, targetFOV,
                    Mathf.SmoothStep(0f, 1f, elapsed / zoomInDuration));
                yield return null;
            }
            cam.fieldOfView = targetFOV;
        }
        else if (doZoom)
        {
            cam.fieldOfView = targetFOV;
            yield return null;
        }

        // --- HOLD ---
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        // --- ZOOM OUT ---
        if (doZoom && zoomOutDuration > 0f)
        {
            float elapsed  = 0f;
            float startFOV = cam.fieldOfView;
            while (elapsed < zoomOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                cam.fieldOfView = Mathf.Lerp(startFOV, originalFOV,
                    Mathf.SmoothStep(0f, 1f, elapsed / zoomOutDuration));
                yield return null;
            }
        }

        // Asegurar FOV restaurado
        cam.fieldOfView = originalFOV;

        // --- CUT OUT ---
        RenderSettings.fog = fogWasEnabled;
        if (cloudCover != null) cloudCover.SetRenderersVisible(true);
        if (ambientClouds != null) ambientClouds.SetAmbientCloudsVisible(true);
        CameraDirectorService.Release(this);
        if (dayNight != null && dayNightWasEnabled) dayNight.enabled = true;

        done?.Invoke();
    }
}
