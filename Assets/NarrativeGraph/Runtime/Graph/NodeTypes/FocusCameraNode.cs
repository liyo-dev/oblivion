using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Corta la cámara al CameraFocusPoint indicado por focusId, aguanta holdDuration segundos
/// y vuelve a la posición original. Efecto de "ha sido visto y no visto": cut in → hold → cut out.
/// La posición y rotación del Transform del CameraFocusPoint son exactamente el encuadre.
/// </summary>
[Serializable]
public sealed class FocusCameraNode : NarrativeNode
{
    [Tooltip("focusId del CameraFocusPoint en escena al que cortar la cámara.")]
    public string focusId;

    [Tooltip("Segundos que la cámara permanece en el punto de foco.")]
    public float holdDuration = 3f;

    [Tooltip("Duración del tween de entrada al punto de foco. 0 = corte duro.")]
    public float transitionInDuration = 0.15f;

    [Tooltip("Duración del tween de vuelta a la posición original. 0 = corte duro.")]
    public float transitionOutDuration = 0.15f;

    [Tooltip("Si true, el grafo espera a que acabe todo el ciclo. Si false, avanza inmediatamente.")]
    public bool waitForCompletion = true;

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
        {
            ctx.Runner.StartCoroutine(DoFocus(focusPoint, onReadyToAdvance));
        }
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
            Debug.LogWarning("[FocusCameraNode] Camera.main no encontrada.");
            done?.Invoke();
            yield break;
        }

        // Guardar estado original para restaurarlo al salir
        var originalPos = cam.transform.position;
        var originalRot = cam.transform.rotation;

        var thirdPersonCam = ServiceLocator.Get<vThirdPersonCamera>(false);
        bool wasLocked = false;
        if (thirdPersonCam != null)
        {
            wasLocked = thirdPersonCam.lockCamera;
            thirdPersonCam.lockCamera = true;
        }

        // Transición de entrada al punto de foco
        yield return TweenCamera(cam, originalPos, originalRot,
                                 target.transform.position, target.transform.rotation,
                                 transitionInDuration);

        // Hold
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        // Transición de vuelta a la posición original
        yield return TweenCamera(cam, target.transform.position, target.transform.rotation,
                                 originalPos, originalRot,
                                 transitionOutDuration);

        if (thirdPersonCam != null)
            thirdPersonCam.lockCamera = wasLocked;

        done?.Invoke();
    }

    IEnumerator TweenCamera(Camera cam, Vector3 fromPos, Quaternion fromRot,
                                       Vector3 toPos,   Quaternion toRot, float duration)
    {
        if (duration <= 0f)
        {
            cam.transform.position = toPos;
            cam.transform.rotation = toRot;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cam.transform.position = Vector3.Lerp(fromPos, toPos, t);
            cam.transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        cam.transform.position = toPos;
        cam.transform.rotation = toRot;
    }
}
