using UnityEngine;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Puntero minúsculo, colocado dentro de una carpeta Resources, que apunta a los 4 assets
    /// <see cref="InputGlyphFamilySpriteSet"/> reales (uno por familia), que viven fuera de
    /// Resources (Assets/_UI/). Mismo patrón que <see cref="DialogueIconsResourceLink"/>: permite
    /// que InputGlyphService (clase estática, sin sitio en escena donde enganchar referencias del
    /// Inspector) los encuentre con un único Resources.Load de ESTE puntero — no de las imágenes.
    /// </summary>
    public sealed class InputGlyphFamilySpriteLibraryLink : ScriptableObject
    {
        public InputGlyphFamilySpriteSet xbox;
        public InputGlyphFamilySpriteSet playStation;
        public InputGlyphFamilySpriteSet switchConsole;
        public InputGlyphFamilySpriteSet keyboardMouse;

        public InputGlyphFamilySpriteSet GetSet(InputGlyphDeviceFamily family)
        {
            switch (family)
            {
                case InputGlyphDeviceFamily.Xbox: return xbox;
                case InputGlyphDeviceFamily.PlayStation: return playStation;
                case InputGlyphDeviceFamily.Switch: return switchConsole;
                case InputGlyphDeviceFamily.KeyboardMouse: return keyboardMouse;
                default: return null;
            }
        }
    }
}
