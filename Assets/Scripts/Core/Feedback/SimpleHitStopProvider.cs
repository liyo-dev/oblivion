namespace Sendero.Core.Feedback
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Proveedor de HitStop: ajusta temporalmente el timeScale manteniendo animaciones con Time.unscaledDeltaTime.
    /// </summary>
    public class SimpleHitStopProvider : IHitStopProvider
    {
        public void HitStop(MonoBehaviour runner, float timeScale, float durationSeconds)
        {
            if (!runner || durationSeconds <= 0f) return;
            timeScale = Mathf.Clamp(timeScale, 0f, 1f);
            runner.StartCoroutine(Co_HitStop(timeScale, durationSeconds));
        }

        private IEnumerator Co_HitStop(float timeScale, float duration)
        {
            // FIX C6 (auditoría 2026-08-07): antes capturaba "el timeScale de antes" y lo
            // restauraba al acabar. Dos golpes solapados (trivial en combate, <0.2s entre
            // golpes) dejaban el juego en cámara lenta permanente: el segundo Co_HitStop
            // capturaba el valor ya ralentizado por el primero, y al restaurar pisaba la
            // restauración del primero. Un token único por invocación + TimeScaleArbiterService
            // (pila de peticiones, gana la más lenta) hace que cada hitstop se libere sin
            // pisar al resto de peticiones activas — de otro hitstop solapado, de una cinemática
            // o de una muerte en curso.
            object token = new object();
            TimeScaleArbiterService.Request(token, timeScale);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            TimeScaleArbiterService.Release(token);
        }
    }
}
