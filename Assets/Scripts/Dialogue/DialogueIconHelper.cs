using UnityEngine;

/// <summary>
/// Helper para insertar iconos en textos de diálogo usando TextMeshPro Sprite Assets.
/// 
/// USO EN DIÁLOGOS:
/// - Para mostrar un botón: "Pulsa <sprite name="ButtonA"> para saltar"
/// - Para UI: "Abre el inventario con <sprite name="DpadDown">"
/// 
/// ICONOS DISPONIBLES (configurar en el Sprite Asset):
/// - ButtonA, ButtonB, ButtonX, ButtonY
/// - DpadUp, DpadDown, DpadLeft, DpadRight
/// - LB, RB, LT, RT
/// - LeftStick, RightStick
/// - Start, Select
/// - Corazon (vida), Estrella (mana), Moneda, Llave, etc.
/// </summary>
public static class DialogueIconHelper
{
    // Nombres estándar de los iconos de botones
    public const string BUTTON_A = "ButtonA";
    public const string BUTTON_B = "ButtonB";
    public const string BUTTON_X = "ButtonX";
    public const string BUTTON_Y = "ButtonY";
    
    public const string DPAD_UP = "DpadUp";
    public const string DPAD_DOWN = "DpadDown";
    public const string DPAD_LEFT = "DpadLeft";
    public const string DPAD_RIGHT = "DpadRight";
    
    public const string LB = "LB";
    public const string RB = "RB";
    public const string LT = "LT";
    public const string RT = "RT";
    
    public const string LEFT_STICK = "LeftStick";
    public const string RIGHT_STICK = "RightStick";
    
    public const string START = "Start";
    public const string SELECT = "Select";
    
    // Iconos de UI comunes
    public const string HEART = "Heart";
    public const string STAR = "Star";
    public const string COIN = "Coin";
    public const string KEY = "Key";
    public const string CHEST = "Chest";
    public const string SWORD = "Sword";
    public const string SHIELD = "Shield";
    public const string POTION = "Potion";
    
    /// <summary>
    /// Genera el tag de sprite para usar en TextMeshPro.
    /// Ejemplo: Icon("ButtonA") → "<sprite name=\"ButtonA\">"
    /// </summary>
    public static string Icon(string spriteName)
    {
        return $"<sprite name=\"{spriteName}\">";
    }
    
    /// <summary>
    /// Genera el tag de sprite con índice.
    /// Ejemplo: Icon(0) → "<sprite=0>"
    /// </summary>
    public static string Icon(int spriteIndex)
    {
        return $"<sprite={spriteIndex}>";
    }
    
    /// <summary>
    /// Genera el tag de sprite con color personalizado.
    /// Ejemplo: IconWithColor("Heart", "#FF0000") → "<sprite name=\"Heart\" color=#FF0000>"
    /// </summary>
    public static string IconWithColor(string spriteName, string hexColor)
    {
        return $"<sprite name=\"{spriteName}\" color={hexColor}>";
    }
    
    /// <summary>
    /// Genera el tag de sprite con tamaño personalizado (porcentaje).
    /// Ejemplo: IconWithSize("ButtonA", 150) → "<sprite name=\"ButtonA\" tint=1 size=150%>"
    /// </summary>
    public static string IconWithSize(string spriteName, int sizePercent)
    {
        return $"<sprite name=\"{spriteName}\" tint=1 size={sizePercent}%>";
    }
    
    // Helpers rápidos para botones comunes
    public static string A => Icon(BUTTON_A);
    public static string B => Icon(BUTTON_B);
    public static string X => Icon(BUTTON_X);
    public static string Y => Icon(BUTTON_Y);
    
    public static string DpadUp => Icon(DPAD_UP);
    public static string DpadDown => Icon(DPAD_DOWN);
    public static string DpadLeft => Icon(DPAD_LEFT);
    public static string DpadRight => Icon(DPAD_RIGHT);
    
    /// <summary>
    /// Ejemplos de uso en código C# (para generar diálogos dinámicamente)
    /// </summary>
    public static class Examples
    {
        public static readonly string ExampleJump = $"Pulsa {Icon(BUTTON_A)} para saltar";
        public static readonly string ExampleInventory = $"Abre el inventario con {Icon(DPAD_DOWN)}";
        public static readonly string ExampleMenu = $"Usa {Icon(LB)} y {Icon(RB)} para cambiar de pestaña";
        public static readonly string ExampleHealth = $"Tu vida {Icon(HEART)} está baja, usa una poción {Icon(POTION)}";
        public static readonly string ExampleQuest = $"Has conseguido una {IconWithColor(KEY, "#FFD700")}";
    }
}

