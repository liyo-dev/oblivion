using System.Collections;
using SenderoNarrativeCore.Runtime.Context;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using SenderoNarrativeCore.Editor.Utilities;
#endif

namespace SenderoNarrativeCore.Runtime.Runner
{
    /// <summary>
    /// Bootstraps play mode when the editor "Play From Here" feature is used. Add this component to a bootstrap scene.
    /// </summary>
    public class PlayFromHereBootstrap : MonoBehaviour
    {
        private void Start()
        {
#if UNITY_EDITOR
            if (PlayFromHereEditorUtility.TryConsume(out var context, out var nodeId))
            {
                StartCoroutine(BootstrapRoutine(context, nodeId));
            }
#endif
        }

        private IEnumerator BootstrapRoutine(StoryContextAsset context, string nodeId)
        {
            if (context == null)
            {
                yield break;
            }

            foreach (var scene in context.SceneNamesToLoad)
            {
                if (!string.IsNullOrEmpty(scene))
                {
                    yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
                }
            }

            var runnerObject = new GameObject("NarrativeRunner (Play From Here)");
            var runner = runnerObject.AddComponent<NarrativeRunner>();
            runner.SetContext(context);
            runner.RunFromNode(nodeId);
        }
    }
}
