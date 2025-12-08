using System.Collections;
using UnityEngine;

public class CameraMover : MonoBehaviour
{
[Header("Refs")]
    [SerializeField] private Transform cameraTransform; // Si es null, usa el propio transform

    [Header("Movimiento")]
    [SerializeField] private float moveHeight = 30f;    // Altura relativa a subir
    [SerializeField] private float duration = 5f;       // Segundos que dura la subida
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Vector3 startLocalPosition; // Posicion de partida definida en el inspector

    [Header("Opciones")]
    [SerializeField] private bool resetOnEnable = true; // Volver al punto inicial al reactivar
    [SerializeField] private bool disableAfterMove = false; // Desactiva el GO al terminar

    private Vector3 _startPos;
    private Vector3 _endPos;
    private Coroutine _moveRoutine;

    private void OnEnable()
    {
        if (cameraTransform == null)
            cameraTransform = transform;

        // Usar siempre la posicion indicada en el inspector como punto de partida
        _startPos = startLocalPosition;

        if (resetOnEnable)
            cameraTransform.localPosition = _startPos;

        _endPos = _startPos + Vector3.up * moveHeight;

        _moveRoutine = StartCoroutine(MoveUp());
    }

    private IEnumerator MoveUp()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
            float eased = ease.Evaluate(Mathf.Clamp01(t));
            cameraTransform.localPosition = Vector3.LerpUnclamped(_startPos, _endPos, eased);
            yield return null;
        }

        cameraTransform.localPosition = _endPos;

        if (disableAfterMove)
            gameObject.SetActive(false);
    }
}
