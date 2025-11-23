using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente sencillo para rellenar una fila de habilidad en el inventario.
/// </summary>
public class AbilityInventoryEntry : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Image icon;

    public void SetPresentation(AbilityPresentation presentation)
    {
        if (presentation == null) return;

        if (titleText != null)
            titleText.text = presentation.title;

        if (descriptionText != null)
            descriptionText.text = presentation.description;

        if (icon != null)
        {
            icon.sprite = presentation.icon;
            icon.enabled = presentation.icon != null;
        }
    }
}
