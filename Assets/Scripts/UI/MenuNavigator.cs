using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using Core;

/// <summary>
/// Script simple que da feedback visual al botón seleccionado.
/// Unity EventSystem maneja toda la navegación automáticamente.
/// </summary>
[DisallowMultipleComponent]
public class MenuNavigator : MonoBehaviour
{
    [Header("Animación selección")]
    [Tooltip("Desplazamiento horizontal cuando se selecciona un botón")]
    public float nudge = 6f;
    
    [Tooltip("Duración de la animación")]
    public float nudgeTime = 0.08f;

    [Header("Debug")]
    public bool debugLogs;

    private Button _lastSelected;
    private RectTransform _lastNudgedText;

    // FIX M10 (auditoría 2026-08-07): si no hay nada seleccionado, Update() llamaba a
    // SelectFirstButton() cada frame — y esa función hace GetComponentsInChildren<Button> +
    // Array.Sort completos cada vez. En un menú sin ningún botón interactable (o mientras el
    // EventSystem tarda en asentar la selección), esto se convertía en un bucle caro por frame.
    // Throttle a 4 reintentos/seg: de sobra para recuperar una selección perdida.
    private float _nextSelectFirstRetryAt;

    void OnEnable()
    {
        // Seleccionar el primer botón activo e interactable
        Invoke(nameof(SelectFirstButton), 0.1f);
        
        // Suscribirse a eventos de navegación para reproducir sonidos
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleNavigationInput;
    }
    
    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleNavigationInput;
        
        if (_lastNudgedText != null)
        {
            _lastNudgedText.DOKill();
            _lastNudgedText.anchoredPosition = Vector2.zero;
        }
        _lastNudgedText = null;
        _lastSelected = null;
    }
    
    void HandleNavigationInput(GamepadInputReader.InputEvent input)
    {
        // Solo reproducir sonido en navegación vertical (DPad o Navigate)
        if (input.Phase != UnityEngine.InputSystem.InputActionPhase.Performed) return;
        
        bool isVerticalNav = false;
        
        if (input.Type == GamepadInputReader.InputEventType.Navigate)
        {
            // Detectar navegación vertical significativa
            isVerticalNav = Mathf.Abs(input.Value.y) > 0.5f;
        }
        else if (input.Type == GamepadInputReader.InputEventType.DpadUp || 
                 input.Type == GamepadInputReader.InputEventType.DpadDown)
        {
            isVerticalNav = true;
        }
        
        if (isVerticalNav)
        {
            // El sonido ya se reproduce en GamepadInputReader, pero podemos añadir feedback extra aquí si queremos
            if (debugLogs) Debug.Log("[MenuNavigator] Navegación vertical detectada");
        }
    }

    void Update()
    {
        var es = EventSystem.current;
        if (!es) return;

        var selected = es.currentSelectedGameObject;
        
        // Si no hay nada seleccionado, seleccionar el primer botón automáticamente
        if (selected == null)
        {
            if (Time.unscaledTime >= _nextSelectFirstRetryAt)
            {
                _nextSelectFirstRetryAt = Time.unscaledTime + 0.25f;
                SelectFirstButton();
            }
            return;
        }

        var btn = selected.GetComponent<Button>();
        if (btn && btn != _lastSelected)
        {
            _lastSelected = btn;
            ApplyNudge(btn);
        }
    }

    void SelectFirstButton()
    {
        var buttons = GetComponentsInChildren<Button>(false);
        if (buttons.Length == 0) return;

        // Ordenar por posición Y (arriba primero)
        System.Array.Sort(buttons, (a, b) => 
            b.transform.position.y.CompareTo(a.transform.position.y));

        // Seleccionar el primero que esté activo e interactable
        foreach (var btn in buttons)
        {
            if (btn.interactable && btn.gameObject.activeInHierarchy)
            {
                btn.Select();
                if (EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(btn.gameObject);
                
                if (debugLogs) Debug.Log($"[MenuNavigator] Primer botón seleccionado: {btn.name}");
                return;
            }
        }
    }


    void ApplyNudge(Button button)
    {
        // Resetear el anterior
        if (_lastNudgedText != null)
        {
            _lastNudgedText.DOKill();
            _lastNudgedText.anchoredPosition = Vector2.zero;
        }

        // Buscar el texto hijo
        var textTransform = button.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.rectTransform;
        if (textTransform == null) return;

        _lastNudgedText = textTransform;
        
        // Asegurar que empieza en (0,0)
        textTransform.DOKill();
        textTransform.anchoredPosition = Vector2.zero;
        
        // Aplicar nudge
        textTransform.DOAnchorPosX(nudge, nudgeTime).SetEase(Ease.OutCubic).SetUpdate(true);
        
        if (debugLogs) Debug.Log($"[MenuNavigator] Nudge aplicado a: {button.name}");
    }
}

