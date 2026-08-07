using System.Collections;
using DG.Tweening;
using UnityEngine;

/// Controlador de cámara cinemático reutilizable.
///
/// Uso:
///   1. Añade este componente a cualquier GameObject de la escena.
///   2. Crea GameObjects vacíos como hijos o donde quieras los planos y asígnalos
///      como "shot points" en los scripts que orquesten la cinemática.
///   3. Llama Activate() al inicio de la cinemática y Deactivate() al final.
///
/// Internamente congela vThirdPersonCamera y mueve Camera.main directamente,
/// igual que hace SimpleCinematicDirector en modo forceDirectCameraControl.
[DisallowMultipleComponent]
public class CinematicCameraDriver : MonoBehaviour
{
    [Tooltip("Duración por defecto de los movimientos suaves cuando no se especifica.")]
    [SerializeField] private float defaultMoveDuration = 0.5f;
    [Tooltip("Ease por defecto para movimientos suaves.")]
    [SerializeField] private Ease  defaultMoveEase     = Ease.InOutSine;

    private Coroutine _followRoutine;
    private Tweener   _moveTween;
    private Tweener   _rotTween;
    private bool      _active;

    // ── API pública ───────────────────────────────────────────────────────────

    /// Congela vThirdPersonCamera y toma control de la cámara.
    /// Pasa por CameraDirectorService (en vez de tocar el flag directamente) para que, si el
    /// sistema que suelta la cámara justo antes todavía está en su ventana de gracia de
    /// liberación, este Activate() coalesque con ese handoff sin que vThirdPersonCamera llegue
    /// a meter un frame de gameplay en medio.
    public void Activate()
    {
        if (_active) return;
        _active = true;
        CameraDirectorService.Claim(this);
    }

    /// Libera la cámara y devuelve el control al juego (con la ventana de gracia de
    /// CameraDirectorService, no al instante).
    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        StopFollow();
        KillTweens();
        CameraDirectorService.Release(this);
    }

    /// Corte instantáneo: mueve la cámara al shot point sin transición.
    public void Cut(Transform shot)
    {
        if (!shot) return;
        StopFollow();
        KillTweens();
        var cam = Camera.main;
        if (!cam) return;
        cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
        if (shot.TryGetComponent(out Camera shotCam))
            cam.fieldOfView = shotCam.fieldOfView;
    }

    /// Pan suave hacia un shot point en tiempo real (funciona en slow motion).
    /// Devuelve la coroutine para poder hacer yield si se necesita esperar.
    public Coroutine MoveTo(Transform shot, float duration = -1f, Ease ease = Ease.Unset)
    {
        StopFollow();
        KillTweens();
        return StartCoroutine(Co_MoveTo(shot,
            duration < 0f ? defaultMoveDuration : duration,
            ease == Ease.Unset ? defaultMoveEase : ease));
    }

    /// Sigue a un Transform en tiempo real, aplicando un offset fijo en espacio mundo.
    /// Útil para seguir proyectiles u objetos en movimiento.
    public void StartFollowing(Transform target, Vector3 worldOffset = default, bool lookAt = false)
    {
        StopFollow();
        KillTweens();
        _followRoutine = StartCoroutine(Co_Follow(target, worldOffset, lookAt));
    }

    public void StopFollowing() => StopFollow();

    // ── Coroutines internas ───────────────────────────────────────────────────

    private IEnumerator Co_MoveTo(Transform shot, float duration, Ease ease)
    {
        var cam = Camera.main;
        if (!shot || !cam) yield break;

        Vector3    startPos = cam.transform.position;
        Quaternion startRot = cam.transform.rotation;
        float      startFov = cam.fieldOfView;
        float      targetFov = shot.TryGetComponent(out Camera shotCam) ? shotCam.fieldOfView : startFov;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float st = t * t * (3f - 2f * t);
            if (!shot || !cam) yield break;
            cam.transform.position = Vector3.Lerp(startPos, shot.position, st);
            cam.transform.rotation = Quaternion.Slerp(startRot, shot.rotation, st);
            cam.fieldOfView        = Mathf.Lerp(startFov, targetFov, st);
            yield return null;
        }

        if (shot && cam)
        {
            cam.transform.SetPositionAndRotation(shot.position, shot.rotation);
            cam.fieldOfView = targetFov;
        }
    }

    private IEnumerator Co_Follow(Transform target, Vector3 offset, bool lookAt)
    {
        var cam = Camera.main;
        while (target && cam)
        {
            Vector3 desiredPos = target.position + offset;
            cam.transform.position = desiredPos;
            if (lookAt) cam.transform.LookAt(target.position);
            yield return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void StopFollow()
    {
        if (_followRoutine != null)
        {
            StopCoroutine(_followRoutine);
            _followRoutine = null;
        }
    }

    private void KillTweens()
    {
        _moveTween?.Kill();
        _rotTween?.Kill();
    }

    void OnDestroy()
    {
        KillTweens();
        if (_active)
            CameraDirectorService.Release(this);
    }
}
