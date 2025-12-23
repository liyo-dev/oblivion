using System.Collections;
using UnityEngine;

namespace Sendero.Core.Feedback
{
    /// <summary>
    /// Sistema de efectos cinematográficos para muertes de enemigos.
    /// - Zoom dramático hacia el enemigo
    /// - Slowmotion sincronizado
    /// - Restauración automática del estado de cámara
    /// </summary>
    public class DeathCameraEffect : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField, Tooltip("Cámara principal (se busca automáticamente si no se asigna)")]
        private Camera mainCamera;
        
        [Header("Configuración de Zoom")]
        [SerializeField, Range(0.5f, 2f), Tooltip("Factor de zoom (1 = sin cambio, <1 = acercar)")]
        private float zoomFactor = 0.7f;
        
        [SerializeField, Range(0f, 1f), Tooltip("Duración del zoom hacia el objetivo")]
        private float zoomDuration = 0.3f;
        
        [SerializeField, Range(0f, 2f), Tooltip("Tiempo de espera antes de volver")]
        private float holdDuration = 0.5f;
        
        [SerializeField, Range(0f, 1f), Tooltip("Duración de la vuelta a la normalidad")]
        private float returnDuration = 0.4f;
        
        [Header("Configuración de Slowmotion")]
        [SerializeField, Range(0f, 1f), Tooltip("TimeScale durante el efecto")]
        private float slowMotionScale = 0.1f;
        
        [SerializeField, Range(0f, 1f), Tooltip("Duración del slowmotion")]
        private float slowMotionDuration = 0.3f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;
        
        private float _originalFieldOfView;
        private Coroutine _currentEffect;
        private bool _isEffectActive;
        
        void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
            
            if (mainCamera != null)
            {
                _originalFieldOfView = mainCamera.fieldOfView;
            }
        }
        
        /// <summary>
        /// Activa el efecto cinematográfico de muerte enfocado en un objetivo
        /// </summary>
        /// <param name="target">Transform del enemigo que muere</param>
        public void TriggerDeathEffect(Transform target)
        {
            if (_isEffectActive)
            {
                if (showDebugLogs)
                    Debug.LogWarning("[DeathCameraEffect] Efecto ya activo, ignorando nueva llamada");
                return;
            }
            
            if (target == null)
            {
                if (showDebugLogs)
                    Debug.LogError("[DeathCameraEffect] Target es null");
                return;
            }
            
            if (mainCamera == null)
            {
                if (showDebugLogs)
                    Debug.LogError("[DeathCameraEffect] Main camera no encontrada");
                return;
            }
            
            if (_currentEffect != null)
            {
                StopCoroutine(_currentEffect);
            }
            
            _currentEffect = StartCoroutine(DeathEffectSequence(target));
        }
        
        private IEnumerator DeathEffectSequence(Transform target)
        {
            _isEffectActive = true;
            float originalTimeScale = Time.timeScale;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] 🎬 Iniciando efecto de muerte - Target: {target.name}");
            
            // ========== FASE 1: SLOWMOTION + ZOOM ==========
            // Aplicar slowmotion
            Time.timeScale = slowMotionScale;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] ⏱️ Slowmotion activado - TimeScale: {slowMotionScale}");
            
            // Zoom hacia el objetivo
            float startFOV = mainCamera.fieldOfView;
            float targetFOV = _originalFieldOfView * zoomFactor;
            float elapsed = 0f;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] 🔍 Zoom: {startFOV:F1}° → {targetFOV:F1}°");
            
            // Animar zoom usando unscaledDeltaTime (no afectado por slowmotion)
            while (elapsed < zoomDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / zoomDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                mainCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, smoothT);
                yield return null;
            }
            
            mainCamera.fieldOfView = targetFOV;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] ✅ Zoom completado");
            
            // ========== FASE 2: HOLD (MANTENER ZOOM Y SLOWMO) ==========
            elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] ⏸️ Hold completado ({holdDuration:F2}s)");
            
            // ========== FASE 3: RESTAURAR TIME SCALE ==========
            // CRÍTICO: Restaurar timeScale ANTES de volver el zoom
            Time.timeScale = originalTimeScale;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] ⏱️ TimeScale restaurado: {Time.timeScale}");
            
            // Pequeña espera para que se note la transición
            yield return new WaitForSecondsRealtime(0.05f);
            
            // ========== FASE 4: VOLVER ZOOM A LA NORMALIDAD ==========
            startFOV = mainCamera.fieldOfView;
            elapsed = 0f;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] 🔄 Volviendo zoom a normal: {startFOV:F1}° → {_originalFieldOfView:F1}°");
            
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime; // Ahora sí usamos deltaTime normal
                float t = elapsed / returnDuration;
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                mainCamera.fieldOfView = Mathf.Lerp(startFOV, _originalFieldOfView, smoothT);
                yield return null;
            }
            
            mainCamera.fieldOfView = _originalFieldOfView;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] ✅ Efecto completado - FOV restaurado: {_originalFieldOfView:F1}°");
            
            // ========== VERIFICACIÓN FINAL ==========
            // Asegurar que todo está en su estado original
            Time.timeScale = 1f; // Forzar a 1 por si acaso
            mainCamera.fieldOfView = _originalFieldOfView;
            
            _isEffectActive = false;
            _currentEffect = null;
            
            if (showDebugLogs)
                Debug.Log($"[DeathCameraEffect] 🎉 Sistema completamente restaurado");
        }
        
        /// <summary>
        /// Cancela el efecto actual y restaura todo inmediatamente
        /// </summary>
        public void CancelEffect()
        {
            if (_currentEffect != null)
            {
                StopCoroutine(_currentEffect);
                _currentEffect = null;
            }
            
            // Restaurar todo
            Time.timeScale = 1f;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = _originalFieldOfView;
            }
            
            _isEffectActive = false;
            
            if (showDebugLogs)
                Debug.Log("[DeathCameraEffect] ⛔ Efecto cancelado - Todo restaurado");
        }
        
        void OnDestroy()
        {
            // Asegurar restauración al destruir el componente
            Time.timeScale = 1f;
            if (mainCamera != null)
            {
                mainCamera.fieldOfView = _originalFieldOfView;
            }
        }
        
        /// <summary>
        /// Configura los parámetros del efecto en runtime
        /// </summary>
        public void ConfigureEffect(float zoom, float zoomTime, float hold, float returnTime, float slowmo, float slowmoTime)
        {
            zoomFactor = Mathf.Clamp(zoom, 0.5f, 2f);
            zoomDuration = Mathf.Max(0f, zoomTime);
            holdDuration = Mathf.Max(0f, hold);
            returnDuration = Mathf.Max(0f, returnTime);
            slowMotionScale = Mathf.Clamp01(slowmo);
            slowMotionDuration = Mathf.Max(0f, slowmoTime);
        }
        
        public bool IsEffectActive => _isEffectActive;
    }
}

