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
    
    [Header("Camera Transition")]
    [Tooltip("Duración del fade al entrar/salir de la cámara del boss")]
    [SerializeField] private float cameraFadeDuration = 0.3f;
    [Tooltip("Color del fade (negro por defecto)")]
    [SerializeField] private Color fadeColor = Color.black;
    [Tooltip("Activa si el boss está en interior: fuerza fondo negro en su cámara (evita el azul del skybox)")]
    [SerializeField] private bool isIndoor = false;

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

        // 1. FADE IN a negro (transición suave antes de cambiar cámaras)
        yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: true);
        
        // 2. Cambiar a la cámara del boss (ahora invisible porque la pantalla está negra)
        if (isIndoor)
        {
            bossCamera.clearFlags = CameraClearFlags.SolidColor;
            bossCamera.backgroundColor = Color.black;
        }
        _mainCamera.gameObject.SetActive(false);
        bossCamera.gameObject.SetActive(true);
        
        // 3. FADE OUT desde negro (revela la cámara del boss)
        yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: false);

        // 4. Iniciar efectos visuales y sonoros
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

        // 5. Esperar duración y aplicar feedback sobre la cámara del boss
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

        // 6. FADE IN a negro (transición suave antes de restaurar cámaras)
        yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: true);
        
        // 7. Restaurar cámaras (invisible porque la pantalla está negra)
        bossCamera.gameObject.SetActive(false);
        _mainCamera.gameObject.SetActive(true);
        
        if (bossNameCanvas != null) bossNameCanvas.SetActive(false);
        
        // 8. FADE OUT desde negro (revela la cámara principal)
        yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: false);

        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Release(this);

        _isPlaying = false;
    }
}
