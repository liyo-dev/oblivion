using System.Collections;
using UnityEngine;
using TMPro;
using DG.Tweening;
using Sendero.Core.Feedback;

/// <summary>
/// Presentación cinemática de boss. Muestra el nombre con animación KingdomHearts + degradado,
/// oculta toda la UI con SceneBoundUI durante la intro y la restaura al terminar.
/// </summary>
public class BossIntroPresentation : MonoBehaviour
{
    [Header("Boss Info")]
    [SerializeField] private string bossName = "DEMONIO";
    [SerializeField] private Transform bossTransform;
    [SerializeField] private Camera bossCamera;

    [Header("Timing")]
    [SerializeField] private float introDuration = 3f;
    [SerializeField] private float shakeDelay    = 0.5f;

    [Header("Boss Name — Degradado")]
    [Tooltip("Color izquierdo del degradado del nombre (ej: dorado).")]
    [SerializeField] private Color bossNameGradientLeft  = new Color(1f, 0.75f, 0.1f, 1f);
    [Tooltip("Color derecho del degradado del nombre (ej: rojo vivo).")]
    [SerializeField] private Color bossNameGradientRight = new Color(0.9f, 0.12f, 0.05f, 1f);

    [Header("Effects")]
    [SerializeField] private float shakeIntensity = 0.3f;
    [SerializeField] private float shakeDuration  = 0.5f;

    [Header("Camera Transition")]
    [Tooltip("Duración del fade al entrar/salir de la cámara del boss.")]
    [SerializeField] private float cameraFadeDuration = 0.3f;
    [Tooltip("Color del fade (negro por defecto).")]
    [SerializeField] private Color fadeColor = Color.black;
    [Tooltip("Activa si el boss está en interior: fuerza fondo negro en su cámara.")]
    [SerializeField] private bool isIndoor = false;

    [Header("Audio")]
    [SerializeField] private AudioClip bossRoarClip;
    [SerializeField] private AudioSource audioSource;

    // Campos legacy — mantenidos para escenas ya configuradas; ya no se usan en el flujo principal
    [Header("(Legacy) Boss Name Canvas")]
    [Tooltip("Solo se usa si DramaticTextOverlayUI no está disponible.")]
    [SerializeField] private GameObject     bossNameCanvas;
    [SerializeField] private TextMeshProUGUI bossNameText;

    private Camera _mainCamera;
    private bool   _isPlaying;

    void Awake()
    {
        if (bossNameCanvas != null) bossNameCanvas.SetActive(false);
        _mainCamera = Camera.main;
    }

    public void SetupBoss(Transform boss, Camera bossCam, string displayName = null)
    {
        bossTransform = boss;
        bossCamera    = bossCam;
        if (!string.IsNullOrEmpty(displayName)) bossName = displayName;
    }

    public IEnumerator PlayIntroduction()
    {
        if (_isPlaying || bossCamera == null || _mainCamera == null) yield break;

        _isPlaying = true;

        if (PlayerLockService.HasInstance) PlayerLockService.Instance.Acquire(this);

        // Ocultar toda la UI persistente (SceneBoundUI) con fade
        SceneBoundUI.BeginBossIntro(0.25f);

        // IMPORTANTE: todo lo que sigue va envuelto en try/finally. Si cualquier boss concreto
        // lanza una excepción a mitad de la presentación (p.ej. referencia nula específica de
        // ese prefab), el finally garantiza que el HUD/UI se restaure igualmente. Sin esto, un
        // fallo aislado en un boss dejaba el HUD oculto para siempre durante toda la batalla.
        try
        {
            // 1. Fade a negro (se salta si la pantalla ya está cubierta, ej: venimos de una cinemática)
            if (!FeedbackService.IsScreenFaded)
                yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: true);

            // 2. Cambiar a cámara del boss
            if (isIndoor)
            {
                bossCamera.clearFlags      = CameraClearFlags.SolidColor;
                bossCamera.backgroundColor = Color.black;
            }
            _mainCamera.gameObject.SetActive(false);
            bossCamera.gameObject.SetActive(true);

            // 3. Revelar cámara del boss
            yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: false);

            // 4. Nombre del boss con animación KingdomHearts + degradado (fire-and-forget)
            float nameDuration    = Mathf.Max(introDuration - cameraFadeDuration - 0.5f, 0.5f);
            bool  usedDramaticText = false;

            if (DramaticTextOverlayUI.Instance != null)
            {
                DramaticTextOverlayUI.Instance.PlayBossName(
                    bossName, bossNameGradientLeft, bossNameGradientRight, nameDuration, null);
                usedDramaticText = true;
            }
            else if (bossNameCanvas != null && bossNameText != null)
            {
                bossNameText.text = bossName;
                bossNameCanvas.SetActive(true);
            }

            // 5. Roar y efectos de cámara
            if (bossRoarClip != null)
            {
                if (audioSource != null) audioSource.PlayOneShot(bossRoarClip);
                else AudioSource.PlayClipAtPoint(bossRoarClip,
                    bossTransform != null ? bossTransform.position : transform.position);
            }

            float elapsed  = 0f;
            bool  shakeDone = false;

            while (elapsed < introDuration)
            {
                elapsed += Time.deltaTime;
                if (!shakeDone && elapsed >= shakeDelay)
                {
                    shakeDone = true;
                    FeedbackService.CameraShake(bossCamera, shakeIntensity, shakeDuration);
                    FeedbackService.ScreenFlash(Color.white, 0.1f);
                }
                yield return null;
            }

            // Detener texto si aún estuviera reproduciendo (introDuration muy corto)
            if (usedDramaticText && DramaticTextOverlayUI.Instance != null
                && DramaticTextOverlayUI.Instance.IsPlaying)
            {
                DramaticTextOverlayUI.Instance.ForceStop();
            }

            // 6. Fade a negro antes de restaurar cámara
            yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: true);

            // 7. Restaurar cámara principal
            bossCamera.gameObject.SetActive(false);
            _mainCamera.gameObject.SetActive(true);

            if (bossNameCanvas != null) bossNameCanvas.SetActive(false);

            // 8. Revelar cámara principal
            yield return FeedbackService.ScreenFadeAsync(fadeColor, cameraFadeDuration, fadeIn: false);
        }
        finally
        {
            // Restaurar toda la UI persistente con fade, pase lo que pase durante la presentación.
            SceneBoundUI.EndBossIntro(0.35f);

            if (PlayerLockService.HasInstance) PlayerLockService.Instance.Release(this);

            _isPlaying = false;
        }
    }
}
