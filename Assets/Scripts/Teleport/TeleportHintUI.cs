using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Core.InputGlyphs;

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
        OnHintShown = null;
        OnHintHidden = null;
    }
    #endif
    
    [Header("Referencias UI")]
    [SerializeField] private GameObject hintRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private Image buttonIcon;
    [Tooltip("Icono del botón de teletransporte (distinto físicamente del de interactuar) por " +
             "familia de mando. Referencia directa (sin Resources.Load) — asset propio, no el " +
             "mismo que InteractionHintIconSet. Si se deja vacío, o le falta el sprite de la " +
             "familia actual, el icono se queda como esté puesto a mano en el prefab.")]
    [SerializeField] private InteractionHintIconSet teleportIconSet;
    
    [Header("Configuración")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.15f;
    
    [Header("Localización")]
    [SerializeField] private string hintLocKey = "TELEPORT_HINT";
    [SerializeField] private string hintFallback = "Teletransporte";
    
    public static event System.Action OnHintShown;
    public static event System.Action OnHintHidden;

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
        UpdateButtonIcon();
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
        GameState.OnChanged += OnGameStateChanged;
        InputGlyphService.FamilyChanged += OnInputFamilyChanged;
    }

    private void OnDisable()
    {
        TeleportRegistry.OnRegistryChanged -= OnRegistryChanged;
        GameState.OnChanged -= OnGameStateChanged;
        InputGlyphService.FamilyChanged -= OnInputFamilyChanged;
    }

    private void OnInputFamilyChanged(InputGlyphDeviceFamily _) => UpdateButtonIcon();

    /// Refresca el icono del botón de teletransporte con el generado por <see cref="InputGlyphService"/>
    /// para el dispositivo activo. OJO: el teletransporte NO usa el botón de "interactuar" (South/A) —
    /// <see cref="SavePointTeleportTrigger.IsYButtonPressed"/> lee directamente <c>gamepad.buttonNorth</c>
    /// (Y en Xbox, △ en PlayStation, X en Switch) + tecla T en teclado. Mostrar el botón equivocado aquí
    /// fue una regresión real: el jugador veía "A" en pantalla pero el punto de guardado solo respondía
    /// a Y. Usamos <see cref="InputGlyphNames.Teleport"/> en vez de <see cref="InputGlyphNames.North"/>
    /// a secas porque en teclado NO coinciden: North/AttackMagicNorth está en Q, pero el atajo de
    /// teletransporte está hardcodeado a T — son botones físicos iguales en mando pero teclas distintas.
    private void UpdateButtonIcon()
    {
        if (buttonIcon == null || teleportIconSet == null) return;
        var icon = teleportIconSet.GetSprite(InputGlyphService.CurrentFamily);
        if (icon == null) return;
        buttonIcon.sprite = icon;
        // Defensivo: si el icono real (arte Xbox, alto/estrecho) se sustituye por un placeholder
        // cuadrado (PlayStation/Switch/Teclado) sin Preserve Aspect, el sprite se estira para llenar
        // el RectTransform (100x150, pensado solo para el aspect ratio del arte Xbox) y sale deformado.
        buttonIcon.preserveAspect = true;
    }

    private void OnRegistryChanged()
    {
        // Si el sistema ya no está disponible, ocultar el hint
        if (!TeleportRegistry.IsSystemAvailable && _isVisible)
        {
            ForceHide();
        }
    }

    private void OnGameStateChanged()
    {
        // Si hay solicitudes activas pero el hint no se está mostrando (se bloqueó por GameState),
        // intentar mostrar ahora que el estado cambió
        if (_activeRequestCount > 0 && !_isVisible)
        {
            Show();
        }
        // Si el estado ya no permite interacción, ocultar
        else if (!GameState.CanInteractGlobally && _isVisible)
        {
            Hide();
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
        OnHintShown?.Invoke();

        UpdateHintText();
        UpdateButtonIcon();

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
        OnHintHidden?.Invoke();

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
