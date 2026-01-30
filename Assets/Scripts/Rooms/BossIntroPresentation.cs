using System.Collections;
using UnityEngine;
using TMPro;
using Sendero.Core.Feedback;

/// <summary>
/// Sistema de presentación cinemática para bosses.
/// Intercambia la cámara principal por la cámara interna del boss durante la intro.
/// </summary>
public class BossIntroPresentation : MonoBehaviour
{
    [Header("Boss Info")]
    [SerializeField] private string bossName = "DEMONIO";
    [SerializeField] private Transform bossTransform;
    [SerializeField] private Camera bossCamera;
    
    [Header("Timing")]
    [SerializeField] private float introDuration = 3f;
    [SerializeField] private float shakeDelay = 0.5f;

    [Header("UI")]
    [SerializeField] private GameObject bossNameCanvas;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("Effects")]
    [SerializeField] private float shakeIntensity = 0.3f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip bossRoarClip;
    [SerializeField] private AudioSource audioSource;

    private Camera _mainCamera;
    private bool _isPlaying;

    void Awake()
    {
        if (bossNameCanvas != null) bossNameCanvas.SetActive(false);
        _mainCamera = Camera.main;
    }

    public void SetupBoss(Transform boss, Camera bossCam, string displayName = null)
    {
        bossTransform = boss;
        bossCamera = bossCam;
        if (!string.IsNullOrEmpty(displayName)) bossName = displayName;
    }

    public IEnumerator PlayIntroduction()
    {
        if (_isPlaying || bossCamera == null || _mainCamera == null) yield break;

        _isPlaying = true;

        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Acquire(this);

        // 1. Cambiar a la cámara del boss
        // Guardamos el estado para restaurar, pero desactivamos la principal
        _mainCamera.gameObject.SetActive(false);
        bossCamera.gameObject.SetActive(true);

        // 2. Iniciar efectos visuales y sonoros
        if (bossNameCanvas != null && bossNameText != null)
        {
            bossNameText.text = bossName;
            bossNameCanvas.SetActive(true);
        }

        if (bossRoarClip != null)
        {
            if (audioSource != null) audioSource.PlayOneShot(bossRoarClip);
            else AudioSource.PlayClipAtPoint(bossRoarClip, bossTransform != null ? bossTransform.position : transform.position);
        }

        float elapsed = 0f;
        bool shakeDone = false;

        // 3. Esperar duración y aplicar feedback sobre la cámara del boss
        while (elapsed < introDuration)
        {
            elapsed += Time.deltaTime;

            if (!shakeDone && elapsed >= shakeDelay)
            {
                shakeDone = true;
                // ✅ Sacudimos la cámara del boss, que es la que está activa ahora
                FeedbackService.CameraShake(bossCamera, shakeIntensity, shakeDuration);
                FeedbackService.ScreenFlash(Color.white, 0.1f);
            }

            yield return null;
        }

        // 4. Restaurar cámaras
        bossCamera.gameObject.SetActive(false);
        _mainCamera.gameObject.SetActive(true);

        if (bossNameCanvas != null) bossNameCanvas.SetActive(false);
        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Release(this);

        _isPlaying = false;
    }
}
