namespace Sendero.Core.Feedback
{
    using UnityEngine;

    public interface ISfxProvider
    {
        void Play(MonoBehaviour runner, AudioClip clip, Vector3 position, float volume = 1f);
    }
}
