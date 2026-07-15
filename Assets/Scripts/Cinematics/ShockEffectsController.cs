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

    [Header("Audio tinnitus")]
    [SerializeField] private AudioClip tinnitusClip;
    [SerializeField, Range(0f, 1f)] private float tinnitusVolume = 0.6f;

    [Header("Timing")]
    [SerializeField] private float shockDuration = 2f;
    [SerializeField] private AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private AudioSource _audioSource;
    private Coroutine   _activeRoutine;

    void Awake()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.spatialBlend = 0f; // 2D
        _audioSource.playOnAwake  = false;

        if (shockVolume != null)
            shockVolume.weight = 0f;
    }

    public void PlayShockSequence()
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = StartCoroutine(Co_Shock());
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

        if (tinnitusClip != null)
            _audioSource.PlayOneShot(tinnitusClip, tinnitusVolume);

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
