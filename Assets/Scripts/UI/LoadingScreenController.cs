// Assets/Scripts/UI/LoadingScreenController.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public interface ILoadingUI
{
    void ShowImmediate();
    void HideImmediate();
    void SetProgress(float t);
    float FadeDuration { get; }
    float MinVisibleTime { get; }
}

public class LoadingScreenController : MonoBehaviour, ILoadingUI
{
    [Header("UI References")]
    public CanvasGroup panel;
    public Slider progressBar;
    public Image progressRadial;
    public TMP_Text progressText;

    [Header("Timing")]
    public float fadeDuration = 0.25f;
    public float minVisibleTime = 0.5f;

    public float FadeDuration => fadeDuration;
    public float MinVisibleTime => minVisibleTime;

    void Awake()
    {
        if (panel != null)
        {
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }
    }

    public void ShowImmediate()
    {
        if (!panel) { Debug.LogWarning("[LoadingScreen] Panel not assigned."); return; }
        panel.gameObject.SetActive(true);
        panel.alpha = 1f;
        SetProgress(0f);
    }

    public void HideImmediate()
    {
        if (!panel) return;
        panel.alpha = 0f;
        panel.gameObject.SetActive(false);
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (progressBar) progressBar.value = t;
        if (progressRadial) progressRadial.fillAmount = t;
        if (progressText) progressText.text = Mathf.RoundToInt(t * 100f) + "%";
    }

    public IEnumerator Fade(float from, float to)
    {
        if (!panel) yield break;
        float elapsed = 0f;
        panel.alpha = from;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        panel.alpha = to;
    }
}