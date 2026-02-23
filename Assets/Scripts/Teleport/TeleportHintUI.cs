using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// UI global para mostrar el hint de teletransporte cuando el jugador está en un SavePoint.
/// Debe colocarse en el Canvas principal del HUD.
/// </summary>
public class TeleportHintUI : MonoBehaviour
{
    public static TeleportHintUI Instance { get; private set; }
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif
    
    [Header("Referencias UI")]
    [SerializeField] private GameObject hintRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Image buttonIcon;
    
    [Header("Configuración")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.15f;
    
    [Header("Localización")]
    [SerializeField] private string hintLocKey = "TELEPORT_HINT";
    [SerializeField] private string hintFallback = "Teletransporte";
    
    private Tween _fadeTween;
    private bool _isVisible;
    private int _activeRequestCount;
    
    public bool IsVisible => _isVisible;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Solo aplicar DontDestroyOnLoad si es un objeto raíz
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Si es hijo de otro objeto (ej. HUD), no podemos usar DontDestroyOnLoad directamente
            // pero podemos confiar en que el padre (HUD) persista o se gestione adecuadamente
            Debug.Log("[TeleportHintUI] Inicializado como hijo de otro objeto, no se aplica DontDestroyOnLoad.");
        }
        
        // Inicializar oculto
        if (hintRoot != null)
            hintRoot.SetActive(false);
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        
        UpdateHintText();
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        
        _fadeTween?.Kill();
    }
    
    private void OnEnable()
    {
        TeleportRegistry.OnRegistryChanged += OnRegistryChanged;
    }
    
    private void OnDisable()
    {
        TeleportRegistry.OnRegistryChanged -= OnRegistryChanged;
    }
    
    private void OnRegistryChanged()
    {
        // Si el sistema ya no está disponible, ocultar el hint
        if (!TeleportRegistry.IsSystemAvailable && _isVisible)
        {
            ForceHide();
        }
    }
    
    /// <summary>
    /// Solicita mostrar el hint. Múltiples SavePoints pueden solicitar mostrar el hint,
    /// se mantendrá visible mientras al menos uno lo solicite.
    /// </summary>
    public void RequestShow()
    {
        _activeRequestCount++;
        
        if (_activeRequestCount == 1)
        {
            Show();
        }
    }
    
    /// <summary>
    /// Libera la solicitud de mostrar el hint.
    /// </summary>
    public void RequestHide()
    {
        _activeRequestCount = Mathf.Max(0, _activeRequestCount - 1);
        
        if (_activeRequestCount == 0)
        {
            Hide();
        }
    }
    
    /// <summary>
    /// Fuerza ocultar el hint independientemente de las solicitudes activas.
    /// </summary>
    public void ForceHide()
    {
        _activeRequestCount = 0;
        Hide();
    }
    
    private void Show()
    {
        // Solo mostrar si el sistema de teletransporte está disponible
        if (!TeleportRegistry.IsSystemAvailable)
        {
            Debug.Log("[TeleportHintUI] Sistema no disponible, no se muestra el hint.");
            return;
        }
        
        // No mostrar si no se puede interactuar globalmente
        if (!GameState.CanInteractGlobally)
        {
            return;
        }
        
        if (_isVisible) return;
        
        _isVisible = true;
        
        UpdateHintText();
        
        if (hintRoot != null)
            hintRoot.SetActive(true);
        
        _fadeTween?.Kill();
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            _fadeTween = canvasGroup.DOFade(1f, fadeInDuration)
                .SetUpdate(true)
                .SetEase(Ease.OutQuad);
        }
        
        Debug.Log("[TeleportHintUI] Hint mostrado.");
    }
    
    private void Hide()
    {
        if (!_isVisible) return;
        
        _isVisible = false;
        
        _fadeTween?.Kill();
        
        if (canvasGroup != null)
        {
            _fadeTween = canvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    if (hintRoot != null)
                        hintRoot.SetActive(false);
                });
        }
        else if (hintRoot != null)
        {
            hintRoot.SetActive(false);
        }
        
        Debug.Log("[TeleportHintUI] Hint ocultado.");
    }
    
    private void UpdateHintText()
    {
        if (hintText == null) return;
        
        string text = hintFallback;
        if (LocalizationManager.Instance != null)
        {
            text = LocalizationManager.Instance.Get(hintLocKey, hintFallback);
        }
        
        hintText.text = text;
    }
}
