using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Corta la cámara del jugador al CameraFocusPoint indicado, aguanta holdDuration segundos
/// y corta de vuelta. Bloquea la vThirdPersonCamera durante el efecto y manipula
/// Camera.main directamente (igual que DeathCameraEffect).
///
/// Opcionalmente aplica zoom in → hold → zoom out cambiando el FOV de la cámara.
/// </summary>
[Serializable]
public sealed class FocusCameraNode : NarrativeNode
{
    [Tooltip("focusId del CameraFocusPoint en escena al que cortar la cámara.")]
    public string focusId;

    [Tooltip("Segundos que la cámara permanece en el punto de foco.")]
    public float holdDuration = 3f;

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
        var all = UnityEngine.Object.FindObjectsByType<CameraFocusPoint>(FindObjectsSortMode.None);
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
        var dayNight = UnityEngine.Object.FindFirstObjectByType<DayNightCycle>();
        bool dayNightWasEnabled = dayNight != null && dayNight.enabled;
        if (dayNight != null) dayNight.enabled = false;

        bool fogWasEnabled = RenderSettings.fog;
        RenderSettings.fog = false;

        vThirdPersonCamera.lockCameraForCinematic = true;
        cam.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);

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
        vThirdPersonCamera.lockCameraForCinematic = false;
        if (dayNight != null && dayNightWasEnabled) dayNight.enabled = true;

        done?.Invoke();
    }
}
