using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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

    [Tooltip("Si es true, una vez completado el contador no se puede resetear (la puerta queda abierta aunque se suelten las placas)")]
    [SerializeField] private bool lockOnComplete = false;

    [Header("Persistencia (opcional)")]
    [Tooltip("Override manual del ID. Si está vacío, se genera automáticamente usando escena+nombre+posición.")]
    [SerializeField] private string persistenceIdOverride;

    [Tooltip("Si está activo, al completarse se guarda en el preset y se restaura automáticamente al volver a la escena.")]
    [SerializeField] private bool persistOnComplete = false;

    [Header("Audio")]
    [Tooltip("Clave SFX que se reproduce al completar el puzzle. Vacío = sin sonido.")]
    [SerializeField] private string puzzleSolvedSfxKey;

    [Header("Evento")]
    [Tooltip("Se invoca cuando se alcanza el número requerido de activaciones (también al restaurar desde preset)")]
    public UnityEvent onRequirementMet;

    [Tooltip("Se invoca al llamar a Reset(). Conectar a ForceReset() de cada PressurePlate para que vuelvan a funcionar.")]
    public UnityEvent onReset;

    [Header("Estado")]
    [SerializeField] private int currentCount;

    private bool _isComplete;

    public int CurrentCount => currentCount;
    public bool IsComplete => _isComplete;

    string FlagKey => $"ACTIVATION_COMPLETE_{ResolveId()}";

    void OnEnable()
    {
        if (!persistOnComplete) return;

        GameBootService.OnProfileReady += OnProfileReady;
        PlayerPresetService.OnPresetApplied += OnPresetApplied;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(ActivationCounter));

        if (GameBootService.IsAvailable)
            OnProfileReady();
        else if (PlayerPresetService.HasAppliedPreset)
            OnPresetApplied();
    }

    void OnDisable()
    {
        if (!persistOnComplete) return;
        GameBootService.OnProfileReady -= OnProfileReady;
        PlayerPresetService.OnPresetApplied -= OnPresetApplied;
    }

    void OnProfileReady() => TryRestoreFromPreset();

    void OnPresetApplied()
    {
        if (!_isComplete) TryRestoreFromPreset();
    }

    void TryRestoreFromPreset()
    {
        if (_isComplete) return;
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset?.flags != null && preset.flags.Contains(FlagKey))
        {
            _isComplete = true;
            currentCount = requiredCount;
            onRequirementMet.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActivationCounter] {name}: restaurado desde preset.");
#endif
        }
    }

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
            if (persistOnComplete)
                SetFlag();
            if (!string.IsNullOrEmpty(puzzleSolvedSfxKey))
                AudioService.Instance?.PlaySFX(puzzleSolvedSfxKey, worldPosition: transform.position);
            onRequirementMet.Invoke();
        }
    }

    /// <summary>
    /// Decrementa el contador en 1 cuando una placa se desactiva.
    /// Conectar a PressurePlate.onDeactivated de cada placa.
    /// </summary>
    public void Decrement()
    {
        if (_isComplete && lockOnComplete) return;

        if (currentCount > 0) currentCount--;
        if (currentCount < requiredCount)
            _isComplete = false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ActivationCounter] {name}: decremento → {currentCount}/{requiredCount}");
#endif
    }

    /// <summary>
    /// Reinicia el contador a 0. Invoca onReset para que cada PressurePlate
    /// llame a ForceReset() y vuelva a responder a ocupantes.
    /// Si lockOnComplete es true y el contador está completo, no hace nada.
    /// </summary>
    public void Reset()
    {
        if (_isComplete && lockOnComplete) return;

        currentCount = 0;
        _isComplete = false;
        onReset.Invoke();
    }

    void SetFlag()
    {
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null) return;

        if (preset.flags == null) preset.flags = new List<string>();
        var flag = FlagKey;
        if (!preset.flags.Contains(flag))
            preset.flags.Add(flag);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ActivationCounter] Flag '{flag}' guardada en preset.");
#endif
    }

    string ResolveId()
    {
        if (!string.IsNullOrEmpty(persistenceIdOverride)) return persistenceIdOverride;
        var scene = gameObject.scene.IsValid() ? gameObject.scene.name : "Unknown";
        var pos = transform.position;
        return $"{scene}_{gameObject.name}_{pos.x:F1}_{pos.y:F1}_{pos.z:F1}";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (requiredCount < 1) requiredCount = 1;
    }
#endif
}
