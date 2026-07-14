using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Línea de tutorial in-world: icono de botón opcional + texto. No bloquea la acción.
/// Vive en el Canvas persistente (Start.unity). Singleton.
/// </summary>
public class TutorialPromptUI : MonoBehaviour
{
    public static TutorialPromptUI Instance { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
#endif

    [Header("Referencias UI")]
    [SerializeField] CanvasGroup _rootGroup;
    [SerializeField] GameObject _iconContainer;
    [SerializeField] Image _icon;
    [SerializeField] TextMeshProUGUI _label;

    [Header("Animación")]
    [SerializeField] float _fadeInDuration = 0.3f;
    [SerializeField] float _fadeOutDuration = 0.25f;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        _rootGroup.alpha = 0f;
        _rootGroup.blocksRaycasts = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            _rootGroup?.DOKill();
        }
    }

    // ── API pública ───────────────────────────────────────────────────────

    public bool IsVisible => gameObject.activeSelf && _rootGroup.alpha > 0f;

    [ContextMenu("TEST Show")]
    void TestShow() => Show("Pulsa A para despertar");

    [ContextMenu("TEST Hide")]
    void TestHide() => Hide();

    public void Show(string text, Sprite icon = null)
    {
        _label.text = text;

        if (_iconContainer != null) _iconContainer.SetActive(icon != null);
        if (_icon != null && icon != null) _icon.sprite = icon;

        _rootGroup.DOKill();
        _rootGroup.DOFade(1f, _fadeInDuration).SetUpdate(true);
        _rootGroup.blocksRaycasts = false;
    }

    public void Hide()
    {
        _rootGroup.DOKill();
        _rootGroup.DOFade(0f, _fadeOutDuration).SetUpdate(true);
    }
}
