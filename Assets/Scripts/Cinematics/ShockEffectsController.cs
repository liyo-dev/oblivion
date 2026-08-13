using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// Gestiona los efectos de shock post-colisión: post-process (blur/chromatic/vignette) + tinnitus.
/// Requiere un Volume URP dedicado configurado con los valores máximos deseados en el inspector.
[DisallowMultipleComponent]
public class ShockEffectsController : MonoBehaviour
{
    [Header("Post-process")]
    [Tooltip("Volume URP que contiene DepthOfField, ChromaticAberration y Vignette al máximo. Su weight se anima de 1 a 0.")]
    [SerializeField] private Volume shockVolume;

    [Header("Audio tinnitus (clave del Audio Graph Profile)")]
    [SerializeField] private string tinnitusSfxKey = "";
    [SerializeField, Range(0f, 1f)] private float tinnitusVolume = 0.6f;

    [Header("Timing")]
    [SerializeField] private float shockDuration = 2f;
    [SerializeField] private AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private Coroutine _activeRoutine;

    void Awake()
    {
        if (shockVolume != null)
            shockVolume.weight = 0f;
    }

    public void PlayShockSequence()
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(Co_Shock());
    }

    /// Pone el volumen al peso indicado y lo mantiene fijo (sin fade automático).
    /// Llamar HoldAt(1f) justo después de la explosión para que el efecto dure mientras conviene.
    public void HoldAt(float weight = 1f)
    {
        if (_activeRoutine != null) { StopCoroutine(_activeRoutine); _activeRoutine = null; }
        SetVolumeWeight(weight);
    }

    /// Reproduce solo el pitido (tinnitus) sin tocar el volumen de post-proceso.
    public void PlayTinnitus()
    {
        if (!string.IsNullOrWhiteSpace(tinnitusSfxKey))
            AudioService.Instance?.PlaySFX(tinnitusSfxKey, tinnitusVolume);
    }

    public void ForceEnd()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
        SetVolumeWeight(0f);
    }

    private IEnumerator Co_Shock()
    {
        SetVolumeWeight(1f);

        PlayTinnitus();

        float elapsed = 0f;
        while (elapsed < shockDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetVolumeWeight(recoveryCurve.Evaluate(elapsed / shockDuration));
            yield return null;
        }

        SetVolumeWeight(0f);
        _activeRoutine = null;
    }

    private void SetVolumeWeight(float w)
    {
        if (shockVolume != null)
            shockVolume.weight = w;
    }
}
