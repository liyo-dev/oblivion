namespace Sendero.Core.Feedback
{
    using UnityEngine;

    public interface ICameraShakeProvider
    {
        void Shake(MonoBehaviour runner, float intensity, float duration);
    }
}
