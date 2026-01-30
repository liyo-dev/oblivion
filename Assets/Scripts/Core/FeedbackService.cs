﻿namespace Sendero.Core.Feedback
{
    using UnityEngine;

    /// <summary>
    /// Servicio centralizado de feedbacks del juego (camera shake, VFX/SFX puntuales, etc.).
    /// - Orquesta llamadas y delega en proveedores especializados (Strategy pattern).
    /// - No mueve al jugador.
    /// - CameraShake actúa sobre la cámara especificada o la Main Camera por defecto.
    /// - Se auto-instancia (DontDestroyOnLoad) cuando se llama a cualquier método estático.
    /// </summary>
    public class FeedbackService : MonoBehaviour
    {
        private static FeedbackService _instance;

        // Proveedores (estrategias)
        private static ICameraShakeProvider _cameraShakeProvider;
        private static IScreenFlashProvider _screenFlashProvider;
        private static IHitStopProvider _hitStopProvider;
        private static IVfxProvider _vfxProvider;
        private static ISfxProvider _sfxProvider;
        private static DeathCameraEffect _deathCameraEffect;

        // ===================== API PÚBLICA =====================

        // Camera Shake
        public static void CameraShake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f) return;
            var inst = EnsureInstance();
            EnsureCameraShakeProvider().Shake(inst, intensity, duration);
        }

        /// <summary>
        /// Ejecuta un shake en una cámara específica.
        /// </summary>
        public static void CameraShake(Camera targetCamera, float intensity, float duration)
        {
            if (targetCamera == null || intensity <= 0f || duration <= 0f) return;
            var inst = EnsureInstance();
            EnsureCameraShakeProvider().Shake(inst, targetCamera, intensity, duration);
        }

        public static void SetCameraShakeProvider(ICameraShakeProvider provider) => _cameraShakeProvider = provider;

        // Screen Flash
        public static void ScreenFlash(Color color, float duration)
        {
            if (duration <= 0f) return;
            var inst = EnsureInstance();
            EnsureScreenFlashProvider().Flash(inst, color, duration);
        }

        public static void SetScreenFlashProvider(IScreenFlashProvider provider) => _screenFlashProvider = provider;

        // Hit Stop (pequeña pausa)
        public static void HitStop(float timeScale, float durationSeconds)
        {
            if (durationSeconds <= 0f) return;
            var inst = EnsureInstance();
            EnsureHitStopProvider().HitStop(inst, timeScale, durationSeconds);
        }

        public static void SetHitStopProvider(IHitStopProvider provider) => _hitStopProvider = provider;
        
        // Death Camera Effect (zoom + slowmotion cinematográfico)
        public static void TriggerDeathEffect(Transform target)
        {
            if (target == null) return;
            var inst = EnsureInstance();
            EnsureDeathCameraEffect().TriggerDeathEffect(target);
        }
        
        public static void CancelDeathEffect()
        {
            if (_deathCameraEffect != null)
                _deathCameraEffect.CancelEffect();
        }

        // VFX puntuales
        public static GameObject PlayVFX(GameObject prefab, Vector3 position, Quaternion rotation, float lifeTimeSeconds = 3f, Transform parent = null)
        {
            if (!prefab) return null;
            var inst = EnsureInstance();
            return EnsureVfxProvider().Play(inst, prefab, position, rotation, lifeTimeSeconds, parent);
        }

        public static void SetVfxProvider(IVfxProvider provider) => _vfxProvider = provider;

        // SFX puntuales
        public static void PlaySfx(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (!clip) return;
            var inst = EnsureInstance();
            EnsureSfxProvider().Play(inst, clip, position, Mathf.Clamp01(volume));
        }

        public static void SetSfxProvider(ISfxProvider provider) => _sfxProvider = provider;

        // ===================== Infraestructura =====================

        private static FeedbackService EnsureInstance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("FeedbackService");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FeedbackService>();
            return _instance;
        }

        private static ICameraShakeProvider EnsureCameraShakeProvider()
        {
            if (_cameraShakeProvider == null)
                _cameraShakeProvider = new TransformPivotCameraShakeProvider();
            return _cameraShakeProvider;
        }

        private static IScreenFlashProvider EnsureScreenFlashProvider()
        {
            if (_screenFlashProvider == null)
                _screenFlashProvider = new UiOverlayScreenFlashProvider();
            return _screenFlashProvider;
        }

        private static IHitStopProvider EnsureHitStopProvider()
        {
            if (_hitStopProvider == null)
                _hitStopProvider = new SimpleHitStopProvider();
            return _hitStopProvider;
        }

        private static IVfxProvider EnsureVfxProvider()
        {
            if (_vfxProvider == null)
                _vfxProvider = new SimpleVfxProvider();
            return _vfxProvider;
        }

        private static ISfxProvider EnsureSfxProvider()
        {
            if (_sfxProvider == null)
                _sfxProvider = new SimpleSfxProvider();
            return _sfxProvider;
        }
        
        private static DeathCameraEffect EnsureDeathCameraEffect()
        {
            if (_deathCameraEffect == null)
            {
                var inst = EnsureInstance();
                _deathCameraEffect = inst.GetComponent<DeathCameraEffect>();
                if (_deathCameraEffect == null)
                {
                    _deathCameraEffect = inst.gameObject.AddComponent<DeathCameraEffect>();
                }
            }
            return _deathCameraEffect;
        }
    }
}
