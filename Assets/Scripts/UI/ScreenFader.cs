using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    CanvasGroup _cg;
    void Awake(){ _cg = GetComponent<CanvasGroup>(); _cg.alpha = 0f; _cg.blocksRaycasts=false; }
    public IEnumerator Co_FadeOut(float t){ yield return FadeTo(1f, t); }
    public IEnumerator Co_FadeIn (float t){ yield return FadeTo(0f, t); }
    IEnumerator FadeTo(float a, float t){
        float s=_cg.alpha, e=a, el=0f;
        while(el<t){ el+=Time.unscaledDeltaTime; _cg.alpha=Mathf.Lerp(s,e, el/t); yield return null; }
        _cg.alpha=e;
    }
}