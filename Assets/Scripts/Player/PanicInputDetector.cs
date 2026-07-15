using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// Detector de mash input para secuencias de pánico.
/// Usa unscaledTime para funcionar durante Time.timeScale reducido.
[DisallowMultipleComponent]
public class PanicInputDetector : MonoBehaviour
{
    [SerializeField] private InputActionReference panicAction;
    [SerializeField] private int pressesRequired = 8;
    [SerializeField] private float windowSeconds = 2.5f;

    public event Action<float> OnProgressChanged;
    public event Action OnSuccess;
    public event Action OnFailure;

    private bool _listening;
    private int  _pressCount;
    private float _windowEndUnscaled;

    public bool IsListening => _listening;
    public float TimeRemaining => _listening
        ? Mathf.Max(0f, _windowEndUnscaled - Time.unscaledTime)
        : 0f;

    public void StartListening()
    {
        _listening  = true;
        _pressCount = 0;
        _windowEndUnscaled = Time.unscaledTime + windowSeconds;

        // Force-enable ignorando el estado que PlayerActionManager pueda haber seteado
        panicAction.action.Enable();
        panicAction.action.performed += OnPress;
    }

    public void StopListening()
    {
        if (!_listening) return;
        _listening = false;
        panicAction.action.performed -= OnPress;
    }

    void Update()
    {
        if (!_listening) return;

        if (Time.unscaledTime >= _windowEndUnscaled)
        {
            StopListening();
            OnFailure?.Invoke();
        }
    }

    private void OnPress(InputAction.CallbackContext ctx)
    {
        if (!_listening) return;

        _pressCount++;
        float progress = Mathf.Clamp01((float)_pressCount / pressesRequired);
        OnProgressChanged?.Invoke(progress);

        if (_pressCount >= pressesRequired)
        {
            StopListening();
            OnSuccess?.Invoke();
        }
    }

    void OnDestroy() => StopListening();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular éxito")]
    void SimulateSuccess()
    {
        StopListening();
        OnSuccess?.Invoke();
    }
#endif
}
