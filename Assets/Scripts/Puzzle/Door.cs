using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Puerta que se abre/cierra mediante rotación.
/// Llamar a Open() desde ActivationCounter.onRequirementMet o cualquier UnityEvent.
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Ángulo de apertura en grados (relativo a la rotación inicial)")]
    [SerializeField] private float openAngle = 90f;

    [Tooltip("Eje de rotación local sobre el que gira la puerta")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [Tooltip("Duración de la animación de apertura/cierre en segundos")]
    [SerializeField] private float duration = 1f;

    [Tooltip("Curva de animación para suavizar el movimiento")]
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Configuración")]
    [Tooltip("Si es true, la puerta comienza abierta")]
    [SerializeField] private bool startOpen;

    [Tooltip("Si es true, la puerta no se puede volver a cerrar una vez abierta")]
    [SerializeField] private bool oneWay = true;

    [Header("Audio")]
    [SerializeField] private string openSfxKey = "Door_Open";
    [SerializeField] private string closeSfxKey = "Door_Close";

    [Header("Eventos")]
    public UnityEvent onOpened;
    public UnityEvent onClosed;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private bool _isOpen;
    private Coroutine _moveCoroutine;

    private void Awake()
    {
        _closedRotation = transform.localRotation;
        _openRotation = _closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);

        if (startOpen)
        {
            transform.localRotation = _openRotation;
            _isOpen = true;
        }
    }

    public void Open()
    {
        if (_isOpen) return;

        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(Rotate(_openRotation, isOpening: true));
    }

    public void Close()
    {
        if (!_isOpen || oneWay) return;

        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(Rotate(_closedRotation, isOpening: false));
    }

    private IEnumerator Rotate(Quaternion target, bool isOpening)
    {
        Quaternion start = transform.localRotation;
        float elapsed = 0f;

        var sfxKey = isOpening ? openSfxKey : closeSfxKey;
        if (!string.IsNullOrEmpty(sfxKey))
            AudioService.Instance?.PlaySFX(sfxKey, worldPosition: transform.position);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = movementCurve.Evaluate(Mathf.Clamp01(elapsed / duration));
            transform.localRotation = Quaternion.Lerp(start, target, t);
            yield return null;
        }

        transform.localRotation = target;
        _isOpen = isOpening;
        _moveCoroutine = null;

        if (isOpening)
            onOpened.Invoke();
        else
            onClosed.Invoke();
    }

    public bool IsOpen => _isOpen;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Quaternion closed = Application.isPlaying ? _closedRotation : transform.localRotation;
        Quaternion open = closed * Quaternion.AngleAxis(openAngle, rotationAxis);

        Vector3 closedDir = closed * Vector3.forward;
        Vector3 openDir = open * Vector3.forward;

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, closedDir * 1.5f);

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, openDir * 1.5f);
    }
#endif
}
