namespace Sendero.Core.Feedback
{
    using System.Collections;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Proveedor de Screen Flash basado en un Canvas Overlay con una Image a pantalla completa.
    /// Reutiliza una única instancia persistente y hace un fade sobre el alpha del color.
    /// </summary>
    public class UiOverlayScreenFlashProvider : IScreenFlashProvider
    {
        private class FlashRoot : MonoBehaviour { public Image Image; }

        public void Flash(MonoBehaviour runner, Color color, float duration)
        {
            if (!runner || duration <= 0f) return;
            var root = EnsureFlashRoot();
            runner.StartCoroutine(Co_Flash(root, color, duration));
        }

        private IEnumerator Co_Flash(FlashRoot root, Color color, float duration)
        {
            if (!root || !root.Image) yield break;
            var img = root.Image;

            // Forzar visible
            Color start = color; // alpha incluido
            img.color = start;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                // Fade out
                var c = color;
                c.a = Mathf.Lerp(color.a, 0f, t);
                img.color = c;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            var end = color; end.a = 0f;
            img.color = end;
        }

        private FlashRoot EnsureFlashRoot()
        {
            var existing = Object.FindFirstObjectByType<FlashRoot>(FindObjectsInactive.Include);
            if (existing) return existing;

            var go = new GameObject("FS_ScreenFlash");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999; // por encima de todo

            var cg = go.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;

            var imageGo = new GameObject("FlashImage");
            imageGo.transform.SetParent(go.transform, false);
            var image = imageGo.AddComponent<Image>();
            image.color = new Color(1,1,1,0);

            var rt = image.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var root = go.AddComponent<FlashRoot>();
            root.Image = image;
            return root;
        }
    }
}
