using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Recibe activaciones de múltiples fuentes (antorchas, placas, palancas…) y lanza
/// onRequirementMet cuando se alcanza el número requerido.
/// Cada fuente llama a RegisterActivation() desde su propio evento (ej: Burnable.onBurned).
/// </summary>
public class ActivationCounter : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Número de activaciones necesarias para lanzar el evento")]
    [SerializeField] private int requiredCount = 2;

    [Header("Evento")]
    [Tooltip("Se invoca cuando se alcanza el número requerido de activaciones")]
    public UnityEvent onRequirementMet;

    [Header("Estado")]
    [SerializeField] private int currentCount;

    private bool _isComplete;

    public int CurrentCount => currentCount;
    public bool IsComplete => _isComplete;

    /// <summary>
    /// Llamar desde el evento de cada fuente (Burnable.onBurned, PressurePlate.onActivated, etc.)
    /// </summary>
    public void RegisterActivation()
    {
        if (_isComplete) return;

        currentCount++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ActivationCounter] {name}: {currentCount}/{requiredCount}");
#endif

        if (currentCount >= requiredCount)
        {
            _isComplete = true;
            onRequirementMet.Invoke();
        }
    }

    /// <summary>
    /// Reinicia el contador (para puzzles que se puedan resetear)
    /// </summary>
    public void Reset()
    {
        currentCount = 0;
        _isComplete = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (requiredCount < 1) requiredCount = 1;
    }
#endif
}
