namespace Sendero.Core.Feedback
{
    using UnityEngine;

    /// <summary>
    /// Proveedor de VFX simple: instancia un prefab en una posición con vida limitada.
    /// </summary>
    public class SimpleVfxProvider : IVfxProvider
    {
        public GameObject Play(MonoBehaviour runner, GameObject prefab, Vector3 position, Quaternion rotation, float lifeTimeSeconds = 3f, Transform parent = null)
        {
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, position, rotation, parent);
            if (lifeTimeSeconds > 0f)
            {
                Object.Destroy(go, lifeTimeSeconds);
            }
            return go;
        }
    }
}
