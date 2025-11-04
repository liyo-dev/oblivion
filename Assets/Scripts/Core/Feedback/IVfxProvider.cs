namespace Sendero.Core.Feedback
{
    using UnityEngine;

    public interface IVfxProvider
    {
        GameObject Play(MonoBehaviour runner, GameObject prefab, Vector3 position, Quaternion rotation, float lifeTimeSeconds = 3f, Transform parent = null);
    }
}
