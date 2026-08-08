using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Re-skin procedural de un panel de UI existente: sustituye el sprite de un <see cref="Image"/> por uno
/// generado en memoria con <see cref="ProceduralUIKit"/> (relleno + borde + halo), sin tocar layout,
/// anclas ni ninguna otra referencia del prefab.
///
/// Uso: añadir este componente al MISMO GameObject que ya tiene el <see cref="Image"/> de fondo del panel
/// (HUD, ventana de misiones, inventario, tienda, popups...). No requiere cablear nada más en el Inspector:
/// en <see cref="OnEnable"/> toma el Image del propio GameObject, igual que ya hace
/// <c>DialogueTextBackdropUI</c> en <c>Awake</c>. Los colores por defecto salen de
/// <see cref="ProceduralUIKit.Palette"/>, así que todos los paneles que usen este componente comparten
/// automáticamente la misma identidad visual.
///
/// <c>[ExecuteAlways]</c>: a diferencia de los overlays de secuencias de sueño (que solo existen en
/// runtime), este componente viste "chrome" de UI permanente — necesitamos verlo YA en el editor (al
/// añadirlo, o al construir un panel nuevo desde una herramienta de editor) sin tener que darle a Play.
/// El sprite sale de la caché de <see cref="ProceduralUIKit"/>, así que repetir esto en cada recarga de
/// dominio o cambio en el Inspector es barato.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ProceduralPanelSkin : MonoBehaviour
{
    [Header("Resolución de la textura generada (px)")]
    [SerializeField, Range(64, 256)] int _textureSize = 128;

    [Header("Forma")]
    [SerializeField] float _cornerRadius = 26f;
    [SerializeField] float _borderThickness = 4f;
    [SerializeField] float _glowRange = 14f;
    [SerializeField] bool  _rimHighlight = true;

    [Header("Color (desmarcado = paleta por defecto del juego, la misma de las secuencias de sueño)")]
    [SerializeField] bool  _useCustomColors = false;
    [SerializeField] Color _fill   = default;
    [SerializeField] Color _border = default;
    [SerializeField] Color _glow   = default;

    void OnEnable()
    {
        var img = GetComponent<Image>();
        Sprite sprite = ProceduralUIKit.BuildPanelSprite(
            size: _textureSize,
            cornerRadius: _cornerRadius,
            borderThickness: _borderThickness,
            glowRange: _glowRange,
            fill:   _useCustomColors ? _fill   : (Color?)null,
            border: _useCustomColors ? _border : (Color?)null,
            glow:   _useCustomColors ? _glow   : (Color?)null,
            rimHighlight: _rimHighlight);

        img.sprite = sprite;
        img.type   = Image.Type.Sliced;
        // Mantenemos el color del Image en blanco puro: el tinte ya está horneado en la textura.
        // Si el Image tenía un color/alpha propio (p.ej. para fundidos con DOTween), se respeta tal cual.

        // Nota de rendimiento: el sprite viene de la caché de ProceduralUIKit y puede estar compartido
        // con otros paneles idénticos (misma forma/color). Por eso NO se destruye aquí en OnDestroy —
        // sería destruir una textura que otro panel todavía está usando. La caché es la dueña del ciclo
        // de vida, no esta instancia.
    }
}
