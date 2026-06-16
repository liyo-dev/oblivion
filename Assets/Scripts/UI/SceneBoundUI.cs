using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneBoundUI : MonoBehaviour
{
    [SerializeField] private string uniqueId = string.Empty;
    [SerializeField] private List<string> allowedScenes = new();
    [SerializeField] private bool allowWhenListEmpty = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool detachFromParent = true;

    private static readonly Dictionary<string, SceneBoundUI> Instances = new();
    private string instanceKey;

    private void Awake()
    {
        instanceKey = string.IsNullOrEmpty(uniqueId) ? name : uniqueId;
        if (Instances.TryGetValue(instanceKey, out var existing) && existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        Instances[instanceKey] = this;

        if (detachFromParent && transform.parent != null)
            transform.SetParent(null, worldPositionStays: false);

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        ApplySceneState();
    }

    private void OnDestroy()
    {
        if (Instances.TryGetValue(instanceKey, out var existing) && existing == this)
            Instances.Remove(instanceKey);

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnActiveSceneChanged(Scene _, Scene newScene) => ApplySceneState();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySceneState();
    private void OnSceneUnloaded(Scene scene) => ApplySceneState();

    private void ApplySceneState()
    {
        bool allowed;
        if (allowedScenes.Count == 0)
        {
            allowed = allowWhenListEmpty;
        }
        else
        {
            allowed = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (allowedScenes.Contains(SceneManager.GetSceneAt(i).name))
                {
                    allowed = true;
                    break;
                }
            }
        }

        if (gameObject.activeSelf != allowed)
            gameObject.SetActive(allowed);
    }
}
