using UnityEngine;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Set completo de los 17 sprites de botón (ver <see cref="InputGlyphNames"/>) para UNA familia
    /// de dispositivo (Xbox/PlayStation/Switch/Teclado). Referencias directas, sin Resources.Load
    /// por archivo — sustituye a la carpeta Resources/InputGlyphs/&lt;Familia&gt;/*.png que usaba
    /// antes <see cref="InputGlyphService"/>, que dependía de que cada PNG existiera con el nombre
    /// exacto en la ruta exacta.
    ///
    /// Hay un asset de este tipo por familia en Assets/_UI/ (InputGlyphFamilySpriteSet_Xbox.asset,
    /// _PlayStation.asset, _Switch.asset, _KeyboardMouse.asset), enganchados desde
    /// <see cref="InputGlyphFamilySpriteLibraryLink"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "InputGlyphFamilySpriteSet", menuName = "El Sendero/Input/Family Sprite Set")]
    public class InputGlyphFamilySpriteSet : ScriptableObject
    {
        [Header("Cara del mando")]
        [Tooltip("Interactuar/Saltar — Xbox A, PS Cross, Switch B")]
        [SerializeField] private Sprite south;
        [Tooltip("Ataque mágico derecho — Xbox B, PS Circle, Switch A")]
        [SerializeField] private Sprite east;
        [Tooltip("Ataque mágico izquierdo — Xbox X, PS Square, Switch Y")]
        [SerializeField] private Sprite west;
        [Tooltip("Ataque mágico especial — Xbox Y, PS Triangle, Switch X")]
        [SerializeField] private Sprite north;

        [Header("Hombros y gatillos")]
        [SerializeField] private Sprite shoulderLeft;
        [SerializeField] private Sprite shoulderRight;
        [SerializeField] private Sprite triggerLeft;
        [SerializeField] private Sprite triggerRight;

        [Header("Direccional y stick")]
        [SerializeField] private Sprite dpad;
        [Tooltip("Joystick (nombre con J mayúscula intencionada, ver InputGlyphNames.Stick)")]
        [SerializeField] private Sprite stick;

        [Header("Otros")]
        [SerializeField] private Sprite start;
        [Tooltip("Confirmar (UI/Submit) — prompts de \"pulsa para continuar\" con GamePlay deshabilitado " +
                 "(cinemáticas, despertar a Will...). En mando es el MISMO botón físico que 'south' " +
                 "(South), así que Xbox/PlayStation/Switch lo resuelven solos reutilizando ese sprite " +
                 "sin rellenar este campo. En Teclado&Ratón SÍ hace falta arte propio (Espacio/Enter, " +
                 "no la tecla E) — ver InputGlyphNames.Confirm.")]
        [SerializeField] private Sprite confirm;
        [Tooltip("Botón de teletransporte en un punto de guardado — mismo botón físico que North en mando, pero tecla distinta en teclado (T, no Q). Ver InputGlyphNames.Teleport.")]
        [SerializeField] private Sprite teleport;

        [Header("D-pad por dirección (sin HUD que las use todavía, ver InputGlyphNames.DpadLeft)")]
        [Tooltip("D-pad izquierda en mando; tecla \",\" en teclado. Ver InputGlyphNames.DpadLeft. " +
                 "Usado hoy por el hint de cambiar de personaje del HUD (panel MainCharacters).")]
        [SerializeField] private Sprite dpadLeft;
        [Tooltip("D-pad derecha en mando; tecla \".\" en teclado. Ver InputGlyphNames.DpadRight. " +
                 "Usado hoy por el hint de cambiar de personaje del HUD (panel MainCharacters).")]
        [SerializeField] private Sprite dpadRight;
        [Tooltip("D-pad arriba en mando; tecla \"J\" en teclado. Ver InputGlyphNames.DpadUp. " +
                 "Sin consumidor en HUD todavía (2026-08-12) — se rellena para completar el set.")]
        [SerializeField] private Sprite dpadUp;
        [Tooltip("D-pad abajo en mando; tecla \"G\" en teclado. Ver InputGlyphNames.DpadDown. " +
                 "Sin consumidor en HUD todavía (2026-08-12) — se rellena para completar el set.")]
        [SerializeField] private Sprite dpadDown;

        [Header("Ver mapa grande")]
        [Tooltip("Botón para ampliar el minimapa (BigMapController) — tecla M en teclado, botón " +
                 "Select/View/Back/\"-\" del mando. Ver InputGlyphNames.Select.")]
        [SerializeField] private Sprite select;

        public Sprite GetSprite(string buttonName)
        {
            switch (buttonName)
            {
                case InputGlyphNames.South: return south;
                case InputGlyphNames.East: return east;
                case InputGlyphNames.West: return west;
                case InputGlyphNames.North: return north;
                case InputGlyphNames.ShoulderLeft: return shoulderLeft;
                case InputGlyphNames.ShoulderRight: return shoulderRight;
                case InputGlyphNames.TriggerLeft: return triggerLeft;
                case InputGlyphNames.TriggerRight: return triggerRight;
                case InputGlyphNames.Dpad: return dpad;
                case InputGlyphNames.Stick: return stick;
                case InputGlyphNames.Start: return start;
                case InputGlyphNames.Confirm: return confirm;
                case InputGlyphNames.Teleport: return teleport;
                case InputGlyphNames.DpadLeft: return dpadLeft;
                case InputGlyphNames.DpadRight: return dpadRight;
                case InputGlyphNames.DpadUp: return dpadUp;
                case InputGlyphNames.DpadDown: return dpadDown;
                case InputGlyphNames.Select: return select;
                default: return null;
            }
        }
    }
}
