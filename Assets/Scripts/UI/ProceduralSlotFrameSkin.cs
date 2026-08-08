using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Re-skin procedural para marcos circulares (slots de magia/habilidad, retrato, iconos de estado...):
/// sustituye el sprite de un <see cref="Image"/> por un anillo brillante generado con
/// <see cref="ProceduralUIKit.BuildRingFrameSprite"/>. Mismo criterio que <see cref="ProceduralPanelSkin"/>:
/// se añade al GameObject que YA tiene el Image del marco (p. ej. el aro alrededor de
/// <c>leftMagicSlotImage</c>/<c>rightMagicSlotImage</c>/<c>specialMagicSlotImage</c> en el HUD), sin tocar
/// el icono del hechizo en sí ni ninguna referencia de <c>PlayerHUDV2</c>.
///
/// <c>[ExecuteAlways]</c>: por lo mismo que <see cref="ProceduralPanelSkin"/> — es chrome permanente,
/// tiene que verse en el editor sin necesidad de darle a Play.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class ProceduralSlotFrameSkin : MonoBehaviour
{
    [SerializeField, Range(48, 256)] int _textureSize = 96;
    [SerializeField] float _thickness = 5f;
    [SerializeField] float _glowRange = 8f;

    [SerializeField] bool  _useCustomColors = false;
    [SerializeField] Color _ringColor = default;
    [SerializeField] Color _glowColor = default;

    void OnEnable()
    {
        var img = GetComponent<Image>();
        Sprite sprite = ProceduralUIKit.BuildRingFrameSprite(
            size: _textureSize,
            thickness: _thickness,
            glowRange: _glowRange,
            ringColor: _useCustomColors ? _ringColor : (Color?)null,
            glowColor: _useCustomColors ? _glowColor : (Color?)null);

        img.sprite = sprite;
        img.type   = Image.Type.Simple;

        // El sprite viene de la caché compartida de ProceduralUIKit (ver nota de rendimiento ahí) —
        // no se destruye en OnDestroy porque otros marcos idénticos pueden seguir usándolo.
    }
}
