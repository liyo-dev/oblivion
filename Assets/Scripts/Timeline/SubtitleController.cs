using UnityEngine;
using TMPro;
using DG.Tweening;

public class SubtitleController : MonoBehaviour
{
    public TextMeshProUGUI subtitleTMP;
    public CanvasGroup canvasGroup;
    public float fadeDuration = 0.35f;
    public float targetAlpha = 1f;
    [Range(0f, 1f)] public float backgroundAlpha = 0.40f;

    [Header("Localization")]
    [SerializeField] private bool autoRefreshOnLanguageChange = true;
    
    private Tween _currentTween;
    private string _lastSubtitleId = "";

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup) canvasGroup.alpha = 0f;
        
        var img = GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            var c = img.color;
            c.a = backgroundAlpha;
            img.color = c;
        }
    }

    void Start()
    {
        // Inicializar inmediatamente y luego suscribirse cuando esté listo
        StartCoroutine(InitializeSubtitleController());
    }

    private System.Collections.IEnumerator InitializeSubtitleController()
    {
        // Esperar hasta que LocalizationManager.Instance esté disponible
        float timeout = 5f; // Timeout de 5 segundos para evitar bucle infinito
        float elapsed = 0f;
        
        while (LocalizationManager.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null; // Usar yield return null en lugar de WaitForSeconds para mejor rendimiento
        }
        
        if (LocalizationManager.Instance == null)
        {
            Debug.LogWarning("[SubtitleController] LocalizationManager no se inicializó en 5 segundos. Continuando sin localización.");
            yield break;
        }
        
        // Suscribirse a cambios de idioma si está habilitado
        if (autoRefreshOnLanguageChange)
        {
            LocalizationManager.Instance.OnLocaleChanged += RefreshCurrentSubtitle;
        }
    }

    void OnDestroy()
    {
        // Desuscribirse para evitar memory leaks
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLocaleChanged -= RefreshCurrentSubtitle;
        }
    }

    public void ShowLine(string text)
    {
        if (!subtitleTMP || !canvasGroup) return;
        
        subtitleTMP.text = text;
        _lastSubtitleId = ""; // Limpiar ID ya que usamos texto directo
        
        _currentTween?.Kill();
        _currentTween = canvasGroup.DOFade(targetAlpha, fadeDuration).SetUpdate(true);
    }

    public void ShowLineById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[SubtitleController] ID de subtítulo vacío");
            return;
        }

        _lastSubtitleId = id;
        
        string text = id; // Fallback al ID si no hay LocalizationManager
        
        // Intentar obtener traducción
        if (LocalizationManager.Instance != null)
        {
            text = LocalizationManager.Instance.Get(id, id);
        }
        else
        {
            Debug.LogWarning($"[SubtitleController] LocalizationManager no disponible. Usando ID como texto: {id}");
        }
        
        ShowLine(text);
    }

    public void ShowLineTimed(string text, float holdSeconds)
    {
        ShowLine(text);
        _currentTween?.Kill();
        _currentTween = DOVirtual.DelayedCall(holdSeconds, Hide).SetUpdate(true);
    }

    public void ShowLineByIdTimed(string id, float holdSeconds)
    {
        ShowLineById(id);
        _currentTween?.Kill();
        _currentTween = DOVirtual.DelayedCall(holdSeconds, Hide).SetUpdate(true);
    }

    public void Hide()
    {
        if (!canvasGroup) return;
        
        _currentTween?.Kill();
        _currentTween = canvasGroup.DOFade(0f, fadeDuration).SetUpdate(true);
        
        _lastSubtitleId = ""; // Limpiar ID cuando se oculta
    }

    // Método público para forzar actualización de localización
    [ContextMenu("Force Refresh Localization")]
    public void ForceRefreshLocalization()
    {
        if (!string.IsNullOrEmpty(_lastSubtitleId))
        {
            ShowLineById(_lastSubtitleId);
        }
    }

    /// <summary>
    /// Refresca el subtítulo actual si se cambió el idioma
    /// </summary>
    private void RefreshCurrentSubtitle()
    {
        if (string.IsNullOrEmpty(_lastSubtitleId) || canvasGroup.alpha <= 0f)
        {
            return; // No refrescar si no hay subtítulo activo o está oculto
        }

        // Volver a mostrar el subtítulo con el nuevo idioma
        ShowLineById(_lastSubtitleId);
    }

    /// <summary>
    /// Método para usar desde Animation Events o Timeline
    /// </summary>
    public void ShowSubtitleEvent(string subtitleId)
    {
        ShowLineById(subtitleId);
    }

    /// <summary>
    /// Método para ocultar desde Animation Events o Timeline
    /// </summary>
    public void HideSubtitleEvent()
    {
        Hide();
    }

    void OnDisable()
    {
        _currentTween?.Kill();
        if (canvasGroup) canvasGroup.alpha = 0f;
        _lastSubtitleId = "";
    }

    // Métodos de debugging mejorados
    [ContextMenu("Test Subtitle Welcome")]
    private void TestSubtitle()
    {
        ShowLineById("welcome_intro");
    }
    
    [ContextMenu("Test Subtitle Tutorial")]
    private void TestSubtitleTutorial()
    {
        ShowLineById("tutorial_movement");
    }
    
    [ContextMenu("Test Direct Text")]
    private void TestDirectText()
    {
        ShowLine("Texto directo de prueba");
    }
    
    [ContextMenu("Debug Localization Status")]
    private void DebugLocalizationStatus()
    {
        Debug.Log($"[SubtitleController] LocalizationManager.Instance: {(LocalizationManager.Instance != null ? "DISPONIBLE" : "NULL")}");
        if (LocalizationManager.Instance != null)
        {
            Debug.Log($"[SubtitleController] Idioma actual: {LocalizationManager.Instance.CurrentLocale}");
        }
        Debug.Log($"[SubtitleController] Último subtítulo ID: '{_lastSubtitleId}'");
        Debug.Log($"[SubtitleController] Canvas Group Alpha: {canvasGroup?.alpha}");
        Debug.Log($"[SubtitleController] AutoRefresh habilitado: {autoRefreshOnLanguageChange}");
    }
}