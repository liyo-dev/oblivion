﻿﻿namespace Sendero.Core.Feedback
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

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _cameraShakeProvider = null;
            _screenFlashProvider = null;
            _hitStopProvider = null;
            _vfxProvider = null;
            _sfxProvider = null;
            _deathCameraEffect = null;
            _fadeRoot = null;
        }
#endif

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

        public static void CancelAllShakes() => _cameraShakeProvider?.CancelAll();

        public static void SetCameraShakeProvider(ICameraShakeProvider provider) => _cameraShakeProvider = provider;

        // Screen Flash
        public static void ScreenFlash(Color color, float duration)
        {
            if (duration <= 0f) return;
            var inst = EnsureInstance();
            EnsureScreenFlashProvider().Flash(inst, color, duration);
        }

        public static void SetScreenFlashProvider(IScreenFlashProvider provider) => _screenFlashProvider = provider;

        // Screen Fade (transición suave a/desde negro)
        /// <summary>
        /// Hace un fade de pantalla completa. fadeIn=true va de transparente a color, fadeIn=false va de color a transparente.
        /// </summary>
        public static void ScreenFade(Color color, float duration, bool fadeIn = true)
        {
            if (duration <= 0f) return;
            var inst = EnsureInstance();
            inst.StartCoroutine(Co_ScreenFade(color, duration, fadeIn));
        }
        
        /// <summary>
        /// Versión async del fade de pantalla que permite hacer yield return.
        /// </summary>
        public static System.Collections.IEnumerator ScreenFadeAsync(Color color, float duration, bool fadeIn = true)
        {
            if (duration <= 0f) yield break;
            EnsureInstance();
            yield return Co_ScreenFade(color, duration, fadeIn);
        }

        /// <summary>
        /// Establece el overlay de fade de pantalla a un color exacto de forma instantánea, sin animación.
        /// Útil para poner la pantalla en negro antes de activar una nueva escena y evitar parpadeos.
        /// </summary>
        public static void SetScreenFadeImmediate(Color color)
        {
            EnsureInstance();
            var root = EnsureFadeRoot();
            if (root?.Image != null) root.Image.color = color;
        }

        /// Devuelve true si el overlay de fade está actualmente a opacidad completa (pantalla cubierta).
        public static bool IsScreenFaded
        {
            get
            {
                var root = _fadeRoot;
                return root?.Image != null && root.Image.color.a >= 0.99f;
            }
        }
        
        private static System.Collections.IEnumerator Co_ScreenFade(Color color, float duration, bool fadeIn)
        {
            var root = EnsureFadeRoot();
            if (root == null || root.Image == null) yield break;
            
            var img = root.Image;
            float elapsed = 0f;
            
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                var c = color;
                
                if (fadeIn)
                {
                    // De transparente a color (fade IN a negro)
                    c.a = Mathf.Lerp(0f, color.a, t);
                }
                else
                {
                    // De color a transparente (fade OUT desde negro)
                    c.a = Mathf.Lerp(color.a, 0f, t);
                }
                
                img.color = c;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            // Estado final
            var final = color;
            final.a = fadeIn ? color.a : 0f;
            img.color = final;
        }
        
        // Reutilizar el mismo sistema de overlay que Flash
        private class FadeRoot { public UnityEngine.UI.Image Image; }
        private static FadeRoot _fadeRoot;
        
        private static FadeRoot EnsureFadeRoot()
        {
            if (_fadeRoot != null && _fadeRoot.Image != null) return _fadeRoot;
            
            // Buscar si ya existe el FlashRoot y reutilizar su Image
            var flashGo = GameObject.Find("FS_ScreenFlash");
            if (flashGo != null)
            {
                var img = flashGo.GetComponentInChildren<UnityEngine.UI.Image>();
                if (img != null)
                {
                    _fadeRoot = new FadeRoot { Image = img };
                    return _fadeRoot;
                }
            }
            
            // Si no existe, crearlo
            var go = new GameObject("FS_ScreenFade");
            Object.DontDestroyOnLoad(go);
            
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9998; // justo debajo del flash
            
            var cg = go.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;
            
            var imageGo = new GameObject("FadeImage");
            imageGo.transform.SetParent(go.transform, false);
            var image = imageGo.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, 0);
            
            var rt = image.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            
            _fadeRoot = new FadeRoot { Image = image };
            return _fadeRoot;
        }

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

        /// <summary>
        /// Variante configurable: permite a quien dispara el efecto (ej. NPCCombatLifecycleHandler)
        /// controlar el zoom y la duración del slow-mo por NPC, en vez de usar siempre los valores
        /// por defecto del componente. Pensado para sustituir un `Time.timeScale` manual y "seco"
        /// por el efecto cinematográfico ya existente (zoom + easing de entrada/salida).
        /// </summary>
        public static void TriggerDeathEffect(
            Transform target,
            float slowMotionScale,
            float holdDuration,
            float zoomFactor = 0.85f,
            float zoomDuration = 0.15f,
            float returnDuration = 0.35f)
        {
            if (target == null) return;
            var inst = EnsureInstance();
            var effect = EnsureDeathCameraEffect();
            effect.ConfigureEffect(zoomFactor, zoomDuration, holdDuration, returnDuration, slowMotionScale, holdDuration);
            effect.TriggerDeathEffect(target);
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

        // SFX puntuales — LEGACY: acepta un AudioClip directo y por tanto NO pasa por
        // AudioGraphProfile. No usar en código nuevo: usar AudioService.Instance.PlaySFX(eventKey, ...)
        // con una clave registrada en Assets/_AUDIOPROFILE/AudioGraphProfile.asset (ver ejemplo en
        // Assets/Scripts/Teleport/TeleportSystem.cs). Se mantiene solo por compatibilidad.
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
