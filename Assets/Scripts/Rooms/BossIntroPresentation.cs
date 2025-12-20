using System.Collections;
using UnityEngine;
using TMPro;
using Sendero.Core.Feedback;

/// <summary>
/// Sistema de presentación cinemática para bosses al estilo AAA.
/// Mueve la cámara principal para enfocar al boss, muestra su nombre y hace camera shake.
/// OPCIONAL: Si no está asignado en BossArenaController, se salta la presentación.
/// </summary>
public class BossIntroPresentation : MonoBehaviour
{
    [Header("Boss Info")]
    [Tooltip("Nombre del boss que se mostrará en pantalla")]
    [SerializeField] private string bossName = "DEMONIO";
    
    [Tooltip("Transform del boss - Se configura automáticamente desde BossArenaController")]
    [SerializeField] private Transform bossTransform;
    
    [Tooltip("Cámara del boss (hija del prefab) - Se configura automáticamente desde BossArenaController")]
    [SerializeField] private Camera bossCamera;
    
    [Tooltip("Duración total de la presentación en segundos")]
    [SerializeField] private float introDuration = 3f;
    
    [Tooltip("Velocidad de transición de la cámara (Lerp)")]
    [SerializeField] private float cameraTransitionSpeed = 2f;
    
    [Tooltip("Tiempo de espera antes del camera shake")]
    [SerializeField] private float shakeDelay = 0.8f;

    [Header("UI")]
    [Tooltip("Canvas con el texto del nombre del boss (se activa/desactiva)")]
    [SerializeField] private GameObject bossNameCanvas;
    
    [Tooltip("TextMeshPro donde se escribe el nombre")]
    [SerializeField] private TextMeshProUGUI bossNameText;
    
    [Tooltip("Tiempo que permanece visible el nombre")]
    [SerializeField] private float nameDisplayDuration = 2.5f;

    [Header("Effects")]
    [Tooltip("Intensidad del camera shake (usa FeedbackService.ScreenShake)")]
    [SerializeField] private float shakeIntensity = 0.3f;
    
    [Tooltip("Duración del camera shake en segundos")]
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Audio")]
    [Tooltip("Sonido opcional de rugido/aparición del boss")]
    [SerializeField] private AudioClip bossRoarClip;
    
    [SerializeField] private AudioSource audioSource;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private Camera _mainCamera;
    private Camera _bossCamera;
    private Vector3 _originalCameraPosition;
    private Quaternion _originalCameraRotation;

    void Awake()
    {
        // Ocultar UI por defecto
        if (bossNameCanvas != null)
            bossNameCanvas.SetActive(false);
        
        // Guardar referencia a la cámara principal
        _mainCamera = Camera.main;
    }

    /// <summary>
    /// Configurar el boss y su cámara dinámicamente desde BossArenaController.
    /// </summary>
    public void SetupBoss(Transform boss, Camera camera, string displayName = null)
    {
        bossTransform = boss;
        _bossCamera = camera;
        
        // Si se proporciona un nombre para mostrar, usarlo. Si no, mantener el configurado en el inspector.
        if (!string.IsNullOrEmpty(displayName))
            bossName = displayName;
    }

    /// <summary>
    /// Reproducir la introducción completa del boss.
    /// Mueve Camera.main a la posición de la cámara del boss y bloquea al jugador.
    /// </summary>
    public IEnumerator PlayIntroduction()
    {
        if (bossTransform == null)
        {
            Debug.LogWarning("[BossIntroPresentation] No hay boss transform configurado. Saltando presentación.");
            yield break;
        }

        if (_bossCamera == null)
        {
            Debug.LogWarning("[BossIntroPresentation] No hay cámara del boss configurada. Saltando presentación.");
            yield break;
        }

        if (_mainCamera == null)
        {
            Debug.LogWarning("[BossIntroPresentation] No se encontró Camera.main. Saltando presentación.");
            yield break;
        }

        if (showDebugLogs)
            Debug.Log($"[BossIntroPresentation] Iniciando presentación: {bossName}");

        // Guardar posición original de la cámara
        _originalCameraPosition = _mainCamera.transform.position;
        _originalCameraRotation = _mainCamera.transform.rotation;

        // Bloquear movimiento del player
        if (PlayerLockService.HasInstance)
            PlayerLockService.Instance.Acquire(this);

        // 1. Obtener posición de la cámara del boss
        Vector3 targetCameraPosition = _bossCamera.transform.position;
        Quaternion targetCameraRotation = _bossCamera.transform.rotation;
        
        if (showDebugLogs)
            Debug.Log($"[BossIntroPresentation] Usando cámara del boss: pos={targetCameraPosition}, rot={targetCameraRotation.eulerAngles}");

        // 2. Mover cámara suavemente hacia el boss
        float elapsed = 0f;
        float transitionDuration = 0.8f;
        
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            
            _mainCamera.transform.position = Vector3.Lerp(_originalCameraPosition, targetCameraPosition, t);
            _mainCamera.transform.rotation = Quaternion.Slerp(_originalCameraRotation, targetCameraRotation, t);
            
            yield return null;
        }
        
        _mainCamera.transform.position = targetCameraPosition;
        _mainCamera.transform.rotation = targetCameraRotation;

        if (showDebugLogs)
            Debug.Log("[BossIntroPresentation] Cámara enfocada en el boss");

        if (showDebugLogs)
            Debug.Log("[BossIntroPresentation] Cámara enfocada en el boss");

        // 3. Mostrar nombre del boss
        if (bossNameCanvas != null && bossNameText != null)
        {
            bossNameText.text = bossName;
            bossNameCanvas.SetActive(true);
            
            if (showDebugLogs)
                Debug.Log($"[BossIntroPresentation] Mostrando nombre: {bossName}");
        }

        // 4. Reproducir sonido de rugido (opcional)
        if (bossRoarClip != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(bossRoarClip);
            else
                AudioSource.PlayClipAtPoint(bossRoarClip, bossTransform.position);
            
            if (showDebugLogs)
                Debug.Log("[BossIntroPresentation] Sonido reproducido");
        }

        // 5. Esperar antes del shake
        yield return new WaitForSeconds(shakeDelay);

        // 6. Camera shake sobre la cámara del boss
        if (_bossCamera != null)
        {
            var shaker = _bossCamera.gameObject.GetComponent<SimpleCameraShaker>();
            if (shaker == null)
                shaker = _bossCamera.gameObject.AddComponent<SimpleCameraShaker>();
            
            shaker.Shake(shakeIntensity, shakeDuration);
            
            if (showDebugLogs)
                Debug.Log($"[BossIntroPresentation] Camera shake aplicado a cámara del boss: intensidad={shakeIntensity}, duración={shakeDuration}");
        }
        
        // Screen flash para más impacto
        FeedbackService.ScreenFlash(Color.white, 0.15f);

        // 7. Esperar el resto de la duración
        float remainingTime = introDuration - transitionDuration - shakeDelay;
        if (remainingTime > 0)
            yield return new WaitForSeconds(remainingTime);

        // 8. Fade a negro antes de volver
        FeedbackService.ScreenFlash(Color.black, 0.2f);
        yield return new WaitForSeconds(0.15f);
        
        // 9. Ocultar nombre
        if (bossNameCanvas != null)
        {
            bossNameCanvas.SetActive(false);
        }

        // 10. Restaurar cámara a su posición original con transición suave
        elapsed = 0f;
        float returnDuration = 0.8f;
        Vector3 currentPos = _mainCamera.transform.position;
        Quaternion currentRot = _mainCamera.transform.rotation;
        
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / returnDuration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            _mainCamera.transform.position = Vector3.Lerp(currentPos, _originalCameraPosition, smoothT);
            _mainCamera.transform.rotation = Quaternion.Slerp(currentRot, _originalCameraRotation, smoothT);
            
            yield return null;
        }
        
        _mainCamera.transform.position = _originalCameraPosition;
        _mainCamera.transform.rotation = _originalCameraRotation;

        // Desbloquear jugador
        if (PlayerLockService.HasInstance)
            PlayerLockService.Instance.Release(this);

        if (showDebugLogs)
            Debug.Log("[BossIntroPresentation] Presentación completada. ¡A luchar!");
    }
}
