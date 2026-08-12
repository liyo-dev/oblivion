using UnityEngine;
using UnityEngine.UI;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Componente genérico para cualquier Image de UI suelta (no colgada de un Interactable) que deba
    /// mostrar el icono de botón/tecla correspondiente a <see cref="InputGlyphService.CurrentFamily"/>,
    /// usando cualquiera de los nombres de <see cref="InputGlyphNames"/>.
    ///
    /// A diferencia de <see cref="InteractionIconRefresher"/> (limitado al icono de un
    /// <see cref="InteractionHintIconSet"/> curado a mano, con solo 4 sprites fijos), este lee
    /// directamente de <see cref="InputGlyphService.GetSprite"/> — la vía "normal" que el propio
    /// servicio documenta para componentes Image sueltos (ver el comentario de clase de
    /// InputGlyphService), válida para cualquiera de los 14 nombres de InputGlyphNames sin tener que
    /// crear un ScriptableObject nuevo por cada hint.
    ///
    /// Caso de uso original (2026-08-12): las dos Image "ImageArrowLeft" del panel "MainCharacters" en
    /// Start.unity (hint de cambiar de personaje activo, a los lados de los retratos de Liam/Will/
    /// Estela) eran Image ESTÁTICAS que siempre mostraban la misma flecha dorada
    /// (Assets/Art/UI/Buttons/left.png), sin importar el dispositivo — así que en teclado/ratón
    /// mostraban una flecha aunque las teclas reales son "," y "." (ver InputGlyphNames.DpadLeft/
    /// DpadRight). Este componente las sustituye para que sigan la familia activa como el resto del
    /// HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputGlyphIcon : MonoBehaviour
    {
        [Tooltip("Nombre de InputGlyphNames (p.ej. InputGlyphNames.DpadLeft) del icono a mostrar.")]
        [SerializeField] private string glyphName;
        [SerializeField] private Image icon;

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
            if (icon == null || string.IsNullOrEmpty(glyphName)) return;

            var sprite = InputGlyphService.GetSprite(glyphName);
            if (sprite == null) return;

            icon.sprite = sprite;
            icon.preserveAspect = true;
        }
    }
}
