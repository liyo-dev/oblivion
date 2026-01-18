using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Item individual en la lista de destinos de teletransporte.
/// </summary>
public class TeleportUIItem : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Image backgroundImage;
    
    [Header("Colors")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.25f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.8f, 0.9f);
    
    private TeleportPointData _pointData;
    private Action<TeleportPointData> _onClick;
    private bool _isSelected;
    
    public TeleportPointData PointData => _pointData;
    
    /// <summary>
    /// Configura el item con los datos del punto de teletransporte.
    /// </summary>
    public void Setup(TeleportPointData data, Action<TeleportPointData> onClick)
    {
        _pointData = data;
        _onClick = onClick;
        
        if (nameText != null)
            nameText.text = data.displayName;
        
        SetSelected(false);
    }
    
    /// <summary>
    /// Establece si este item está seleccionado.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        _onClick?.Invoke(_pointData);
    }
}

