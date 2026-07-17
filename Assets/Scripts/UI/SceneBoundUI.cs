using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class SceneBoundUI : MonoBehaviour
{
    [SerializeField] private string uniqueId = string.Empty;
    [SerializeField] private List<string> allowedScenes = new();
    [SerializeField] private bool allowWhenListEmpty = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool detachFromParent = true;
    [Tooltip("Si true, no se oculta durante la intro de boss (ej: DramaticTextOverlayUI).")]
    [SerializeField] private bool excludeFromBossIntroHide = false;

    private static readonly Dictionary<string, SceneBoundUI> Instances = new();
    private static bool _bossIntroActive = false;
    private string instanceKey;

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instances.Clear();
        _bossIntroActive = false;
    }
    #endif

    // ── API de boss intro ──────────────────────────────────────────────────

    /// <summary>
    /// Oculta con DOTween todos los elementos UI activos con SceneBoundUI.
    /// Llamar al inicio de la intro de un boss para limpiar la pantalla.
    /// </summary>
    public static void BeginBossIntro(float fadeDuration = 0.3f)
    {
        _bossIntroActive = true;
        foreach (var inst in Instances.Values)
        {
            if (inst == null || !inst.gameObject.activeSelf) continue;
            if (inst.excludeFromBossIntroHide) continue;
            var cg = inst.GetOrAddCanvasGroup();
            cg.DOKill();
            cg.DOFade(0f, fadeDuration).SetUpdate(true);
        }
    }

    /// <summary>
    /// Restaura con DOTween los elementos UI que corresponden a la escena actual.
    /// Llamar al finalizar la intro del boss.
    /// </summary>
    public static void EndBossIntro(float fadeDuration = 0.35f)
    {
        _bossIntroActive = false;
        foreach (var inst in Instances.Values)
        {
            if (inst == null) continue;
            bool allowed = inst.IsAllowedInCurrentScene();
            if (!allowed) { inst.gameObject.SetActive(false); continue; }
            inst.gameObject.SetActive(true);
            var cg = inst.GetOrAddCanvasGroup();
            cg.DOKill();
            cg.DOFade(1f, fadeDuration).SetUpdate(true);
        }
    }

    /// <summary>Marca este SceneBoundUI para no ser ocultado durante la intro de boss.</summary>
    public void ExcludeFromBossIntro() => excludeFromBossIntroHide = true;

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    private bool IsAllowedInCurrentScene()
    {
        if (allowedScenes.Count == 0) return allowWhenListEmpty;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (allowedScenes.Contains(SceneManager.GetSceneAt(i).name))
                return true;
        return false;
    }

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
        // Durante la intro de un boss, el estado lo controla BeginBossIntro/EndBossIntro
        if (_bossIntroActive) return;

        bool allowed = IsAllowedInCurrentScene();
        if (gameObject.activeSelf != allowed)
            gameObject.SetActive(allowed);
    }
}
