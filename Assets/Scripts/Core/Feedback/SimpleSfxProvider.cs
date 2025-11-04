namespace Sendero.Core.Feedback
{
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Proveedor SFX simple: crea un AudioSource temporal en la posición indicada y reproduce el clip.
    /// </summary>
    public class SimpleSfxProvider : ISfxProvider
    {
        public void Play(MonoBehaviour runner, AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (!clip || runner == null) return;
            runner.StartCoroutine(Co_Play(clip, position, Mathf.Clamp01(volume)));
        }

        private IEnumerator Co_Play(AudioClip clip, Vector3 position, float volume)
        {
            var go = new GameObject("FS_SFX");
            Object.DontDestroyOnLoad(go);
            go.transform.position = position;
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f; // 3D
            src.playOnAwake = false;
            src.volume = volume;
            src.clip = clip;
            src.Play();
            yield return new WaitForSecondsRealtime(clip.length);
            Object.Destroy(go);
        }
    }
}
