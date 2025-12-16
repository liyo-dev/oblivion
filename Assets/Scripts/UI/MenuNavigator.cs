using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

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

    void OnEnable()
    {
        // Seleccionar el primer botón activo e interactable
        Invoke(nameof(SelectFirstButton), 0.1f);
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

    void Update()
    {
        var es = EventSystem.current;
        if (!es) return;

        var selected = es.currentSelectedGameObject;
        if (!selected) return;

        var btn = selected.GetComponent<Button>();
        if (btn && btn != _lastSelected)
        {
            _lastSelected = btn;
            ApplyNudge(btn);
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

    void OnDisable()
    {
        if (_lastNudgedText != null)
        {
            _lastNudgedText.DOKill();
            _lastNudgedText.anchoredPosition = Vector2.zero;
        }
        _lastNudgedText = null;
        _lastSelected = null;
    }
}

