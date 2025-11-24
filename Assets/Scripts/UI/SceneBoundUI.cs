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

    private void Awake()
    {
        var key = string.IsNullOrEmpty(uniqueId) ? name : uniqueId;
        if (Instances.TryGetValue(key, out var existing) && existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        Instances[key] = this;

        if (detachFromParent && transform.parent != null)
        {
            transform.SetParent(null, worldPositionStays: false);
        }

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySceneState(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (Instances.TryGetValue(string.IsNullOrEmpty(uniqueId) ? name : uniqueId, out var existing) && existing == this)
        {
            Instances.Remove(string.IsNullOrEmpty(uniqueId) ? name : uniqueId);
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySceneState(scene.name);
    }

    private void ApplySceneState(string sceneName)
    {
        var allowed = allowedScenes.Count == 0 ? allowWhenListEmpty : allowedScenes.Contains(sceneName);
        if (gameObject.activeSelf != allowed)
        {
            gameObject.SetActive(allowed);
        }
    }
}
