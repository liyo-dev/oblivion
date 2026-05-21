using System.Collections;
using UnityEngine;

public class DoorGate : MonoBehaviour
{
    [Tooltip("Ángulo Y de apertura en grados. Negativo = izquierda, positivo = derecha.")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float animDuration = 0.8f;
    [SerializeField] private bool startsOpen = false;

    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private Coroutine _animCoroutine;

    void Awake()
    {
        _closedRotation = transform.localRotation;
        _openRotation   = _closedRotation * Quaternion.Euler(0f, openAngle, 0f);

        if (startsOpen) Open(); else Close();
    }

    public void Open()  => Animate(_openRotation);
    public void Close() => Animate(_closedRotation);

    private void Animate(Quaternion target)
    {
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(LerpRotation(target));
    }

    private IEnumerator LerpRotation(Quaternion target)
    {
        Quaternion start = transform.localRotation;
        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(start, target, t / animDuration);
            yield return null;
        }
        transform.localRotation = target;
    }
}
