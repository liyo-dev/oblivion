namespace Sendero.Core.Feedback
{
    using UnityEngine;

    public interface ICameraShakeProvider
    {
        void Shake(MonoBehaviour runner, float intensity, float duration);
        void Shake(MonoBehaviour runner, Camera targetCamera, float intensity, float duration);
        void CancelAll();
    }
}
