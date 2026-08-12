using UnityEngine;
using UnityEngine.UI;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Componente reutilizable para cualquier Image de UI que deba mostrar el icono de un botón
    /// (por familia de mando/teclado) usando un <see cref="InteractionHintIconSet"/> — referencia
    /// directa, sin Resources.Load. Mismo patrón que <c>Interactable.RefreshHintIcon()</c>, pero
    /// extraído a un componente aparte para poder colgarlo de prefabs que no son un Interactable
    /// (por ejemplo los icono "Sígueme"/"Dejar de seguir" de <c>CompanionFollowPrompt</c>, que se
    /// instancian sueltos vía <c>NPCAlertIconController</c>).
    ///
    /// Si no hay iconSet asignado, o le falta el sprite de la familia actual, no toca nada — se
    /// queda con el sprite que ya tuviera el prefab a mano.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionIconRefresher : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private InteractionHintIconSet iconSet;

        void Awake()
        {
            if (icon == null) icon = GetComponent<Image>();
        }

        void OnEnable()
        {
            InputGlyphService.FamilyChanged += HandleFamilyChanged;
            Refresh();
        }

        void OnDisable()
        {
            InputGlyphService.FamilyChanged -= HandleFamilyChanged;
        }

        void HandleFamilyChanged(InputGlyphDeviceFamily _) => Refresh();

        void Refresh()
        {
            if (iconSet == null || icon == null) return;

            var sprite = iconSet.GetSprite(InputGlyphService.CurrentFamily);
            if (sprite == null) return;

            icon.sprite = sprite;
            icon.preserveAspect = true;
        }
    }
}
