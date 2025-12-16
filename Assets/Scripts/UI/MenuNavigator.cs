using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

/// <summary>
/// Proporciona feedback visual cuando un botón del menú es seleccionado por el EventSystem.
/// La navegación es manejada automáticamente por Unity EventSystem.
/// </summary>
[DisallowMultipleComponent]
public class MenuNavigator : MonoBehaviour
{
    [Header("Animación selección")]
    [Tooltip("Desplazamiento horizontal sutil cuando se selecciona un botón")]
    public float nudge = 6f;
    
    [Tooltip("Duración de la animación del nudge")]
    public float nudgeTime = 0.08f;

    [Header("Opciones visuales")]
    [Tooltip("Si está activo, desactiva las imágenes de los botones para que solo se muestre el texto")]
    public bool hideButtonImages = false;

    private Button _lastSelected;
    private readonly System.Collections.Generic.List<RectTransform> _nudged = new();

    void Start()
    {
        if (hideButtonImages)
            ApplyHideButtonImages();
    }

    void Update()
    {
        var es = EventSystem.current;
        if (!es) return;

        var selected = es.currentSelectedGameObject;
        if (!selected) return;

        var btn = selected.GetComponent<Button>();
        if (btn != _lastSelected)
        {
            _lastSelected = btn;
            ApplyNudgeFeedback(btn);
        }
    }

    void ApplyNudgeFeedback(Button button)
    {
        if (!button) return;

        // Resetear animaciones previas
        foreach (var prev in _nudged)
        {
            if (prev)
            {
                prev.DOKill();
                prev.anchoredPosition = Vector2.zero;
            }
        }
        _nudged.Clear();

        // Aplicar nudge al botón seleccionado
        var rt = button.GetComponentInChildren<RectTransform>();
        if (!rt) return;

        var startPos = rt.anchoredPosition;
        rt.DOKill();
        rt.DOComplete();
        rt.anchoredPosition = startPos;
        
        rt.DOAnchorPos(startPos + new Vector2(nudge, 0f), nudgeTime)
          .SetEase(Ease.OutCubic)
          .SetUpdate(true) // usar unscaled time para menús de pausa
          .OnKill(() => { if (rt) rt.anchoredPosition = startPos; });
        
        _nudged.Add(rt);
    }

    void ApplyHideButtonImages()
    {
        var buttons = GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            var img = btn.GetComponent<Image>();
            if (img) img.enabled = false;
        }
    }

    void OnDisable()
    {
        // Limpiar animaciones al desactivar
        foreach (var rt in _nudged)
        {
            if (rt)
            {
                rt.DOKill();
                rt.anchoredPosition = Vector2.zero;
            }
        }
        _nudged.Clear();
        _lastSelected = null;
    }
}

