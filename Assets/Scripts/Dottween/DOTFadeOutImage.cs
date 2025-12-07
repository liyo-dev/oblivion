using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class DOTFadeOutImage : MonoBehaviour
{
    public UnityEvent OnFadeOutComplete;
    [SerializeField] private Image imageToFade;
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        switch (delay)
        {
            case 0f:
                FadeOut();
                break;
            case >= 0f:
                StartCoroutine(nameof(FadeOutDelay));
                break;
        }
    }

    private IEnumerator FadeOutDelay()
    {
        yield return new WaitForSeconds(delay);
        
        FadeOut();
    }

    public void FadeOut()
    {
        if (imageToFade != null)
        {
            imageToFade.DOFade(0.0f, fadeDuration).OnComplete(() =>
            {
                OnFadeOutComplete?.Invoke();
            }).Play();
        }
        else
        {
            Debug.LogError("Image to fade is not assigned!");
        }
    }
}
