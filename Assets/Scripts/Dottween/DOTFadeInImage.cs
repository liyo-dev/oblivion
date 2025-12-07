using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.Events;

public class DOTFadeInImage : MonoBehaviour
{
    public UnityEvent OnFadeInComplete;
    [SerializeField] private Image imageToFade;
    [SerializeField] private float fadeDuration = 1.0f;
    [SerializeField] private float delay = 0f;

    private void Start()
    {
        switch (delay)
        {
            case 0f:
                FadeIn();
                break;
            case >= 0f:
                StartCoroutine(nameof(FadeInDelay));
                break;
        }
    }
    
    private IEnumerator FadeInDelay()
    {
        yield return new WaitForSeconds(delay);
        
        FadeIn();
    }

    public void FadeIn()
    {
        if (imageToFade != null)
        {
            imageToFade.DOFade(1f, fadeDuration).OnComplete(() =>
            {
                OnFadeInComplete?.Invoke();
            }).Play();
        }
        else
        {
            Debug.LogError("Image to fade is not assigned!");
        }
    }
}
