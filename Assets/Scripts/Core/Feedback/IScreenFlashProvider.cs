namespace Sendero.Core.Feedback
{
    using UnityEngine;

    public interface IScreenFlashProvider
    {
        void Flash(MonoBehaviour runner, Color color, float duration);
    }
}
