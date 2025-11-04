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
            float original = Time.timeScale;
            Time.timeScale = timeScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Time.timeScale = original;
        }
    }
}
