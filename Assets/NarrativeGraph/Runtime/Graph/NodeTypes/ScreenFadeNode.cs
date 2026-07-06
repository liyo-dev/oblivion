using System;
using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

[Serializable]
public sealed class ScreenFadeNode : NarrativeNode
{
    public Color color = Color.black;
    [Min(0f)] public float duration = 0.5f;
    /// <summary>
    /// true = fundir a negro (pantalla se cubre con el color).
    /// false = quitar el negro (pantalla vuelve a ser visible).
    /// </summary>
    public bool fadeIn = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (duration <= 0f)
        {
            FeedbackService.SetScreenFadeImmediate(fadeIn ? color : Color.clear);
            onReadyToAdvance?.Invoke();
            return;
        }

        ctx.Runner.StartCoroutine(DoFade(onReadyToAdvance));
    }

    private IEnumerator DoFade(Action onReadyToAdvance)
    {
        yield return FeedbackService.ScreenFadeAsync(color, duration, fadeIn);
        onReadyToAdvance?.Invoke();
    }
}
