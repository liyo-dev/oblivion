using UnityEngine;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Set de sprites del icono de "interactuar" (botón South: Xbox A / PS Cross / Switch B),
    /// uno por familia de dispositivo. A diferencia de <see cref="InputGlyphService"/> (que resuelve
    /// los suyos con <see cref="Resources.Load"/> desde Assets/Resources/InputGlyphs/...), este set
    /// se referencia de forma directa (serializada) desde <c>Interactable</c> — sin carga dinámica.
    ///
    /// Motivo (2026-08-11): la carga dinámica por Resources.Load rompió el hint de interacción en
    /// cuanto la carpeta Resources/InputGlyphs dejó de existir/estar completa. Con un asset como este,
    /// arrastrado una única vez a un campo del Inspector, el icono siempre está ahí — no depende de
    /// que exista un PNG con el nombre y la ruta exactos en Resources.
    ///
    /// Un único asset se comparte entre TODOS los Interactable del proyecto (ver herramienta de Editor
    /// "El Sendero/Input Glyphs/Asignar Interaction Icon Set a todos los Interactable"), porque el
    /// icono de interactuar es siempre el mismo botón (South) en cualquier punto de guardado, NPC u
    /// objeto — no hace falta un set distinto por instancia.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionHintIconSet", menuName = "El Sendero/Input/Interaction Hint Icon Set")]
    public class InteractionHintIconSet : ScriptableObject
    {
        [Tooltip("Xbox A")]
        [SerializeField] private Sprite xbox;
        [Tooltip("PlayStation Cross (X)")]
        [SerializeField] private Sprite playStation;
        [Tooltip("Nintendo Switch B")]
        [SerializeField] private Sprite switchConsole;
        [Tooltip("Teclado/Ratón")]
        [SerializeField] private Sprite keyboardMouse;

        public Sprite GetSprite(InputGlyphDeviceFamily family)
        {
            switch (family)
            {
                case InputGlyphDeviceFamily.Xbox: return xbox;
                case InputGlyphDeviceFamily.PlayStation: return playStation;
                case InputGlyphDeviceFamily.Switch: return switchConsole;
                case InputGlyphDeviceFamily.KeyboardMouse: return keyboardMouse;
                default: return xbox;
            }
        }
    }
}
