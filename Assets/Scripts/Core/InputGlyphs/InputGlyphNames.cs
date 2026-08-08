namespace Core.InputGlyphs
{
    /// <summary>
    /// Nombres de sprite EXACTOS que ya usa el contenido de diálogo (JSON de localización,
    /// <c>&lt;sprite name="..."&gt;</c>) contra <c>DialogueIcons.asset</c>. No cambiar estos strings:
    /// son los que ya están escritos en <c>dialogues_es.json</c>, <c>dialogues_en.json</c>,
    /// <c>quests_es.json</c>, <c>other_es.json</c>, <c>cinematics_es.json</c> — cambiar el nombre
    /// rompería ese contenido ya traducido. Lo único que cambia por dispositivo es la IMAGEN detrás
    /// de cada nombre, no el nombre en sí.
    ///
    /// También son los nombres de archivo (sin extensión) que espera
    /// Assets/Resources/InputGlyphs/&lt;Familia&gt;/&lt;nombre&gt;.png, generados por la herramienta de Editor
    /// Tools/Input Glyphs/Generar Assets de Botones. Por eso es pública: la usa tanto el runtime
    /// (ensamblado Assembly-CSharp) como esa herramienta de Editor (ensamblado
    /// Assembly-CSharp-Editor, distinto — no ve símbolos "internal" del primero).
    /// </summary>
    public static class InputGlyphNames
    {
        public const string South = "interactable_A";       // Interactuar/Saltar (Xbox A, PS Cross, Switch B)
        public const string East = "interactable_b";        // Ataque mágico derecho (Xbox B, PS Circle, Switch A)
        public const string West = "interactable_x";        // Ataque mágico izquierdo (Xbox X, PS Square, Switch Y)
        public const string North = "interactable_y";       // Ataque mágico especial (Xbox Y, PS Triangle, Switch X)
        public const string ShoulderLeft = "interactable_lb";
        public const string ShoulderRight = "interactable_rb";
        public const string TriggerLeft = "interactable_lt";
        public const string TriggerRight = "interactable_rt";
        public const string Dpad = "interactable_dpad";
        public const string Stick = "interactable_Joystick"; // mayúscula intencionada — así está en el asset real
        public const string Start = "start";

        public static readonly string[] All =
        {
            South, East, West, North, ShoulderLeft, ShoulderRight,
            TriggerLeft, TriggerRight, Dpad, Stick, Start
        };
    }
}
