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
    /// Assets/Resources/InputGlyphs/&lt;Familia&gt;/&lt;nombre&gt;.png — la carpeta que rellenaba la
    /// vieja herramienta de Editor "Generar Assets de Botones" (retirada agosto 2026, generaba
    /// placeholders por código; ver Assets/_to_delete). El arte real ahora se arrastra a mano en los
    /// 4 assets Assets/_UI/InputGlyphFamilySpriteSet_&lt;Familia&gt;.asset.
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

        // Botón de "Confirmar" para prompts que esperan UI/Submit (TutorialPromptNode, SleepTrigger
        // despertando a Will, cualquier "pulsa para continuar" que aparezca con el mapa GamePlay
        // deshabilitado — p.ej. en ActionMode.Cinematic, ver PlayerLockService.ApplyHardLock). En
        // CUALQUIER mando real es físicamente el mismo botón que South (UI.Submit y GamePlay.Interact
        // están ambos ligados al botón South del gamepad), así que InputGlyphService reutiliza
        // automáticamente el sprite de South para Xbox/PlayStation/Switch — no hace falta arrastrar
        // arte dos veces. En TECLADO, en cambio, Interactuar (E) y Confirmar (Espacio/Enter, ver
        // PlayerControls.inputactions → UI/Submit) son teclas DISTINTAS: por eso Confirm necesita su
        // propio sprite de teclado (Assets/_UI/InputGlyphFamilySpriteSet_KeyboardMouse.asset, campo
        // "confirm") en vez de reutilizar el de South, que mostraría "E" para algo que en realidad
        // solo funciona con Espacio/Enter. No usar este nombre para hints de interacción normal con
        // NPCs/puertas/objetos del mundo (esos SÍ usan GamePlay/Interact de verdad y deben seguir
        // usando South).
        public const string Confirm = "interactable_confirm";

        // Botón de teletransporte en un punto de guardado. En mando es FÍSICAMENTE el mismo botón que
        // North/AttackMagicNorth (Y en Xbox, △ en PlayStation, X en Switch) — SavePointTeleportTrigger
        // lee gamepad.buttonNorth directamente — así que en mando reutiliza el mismo dibujo. Pero en
        // teclado NO comparte tecla: AttackMagicNorth está en Q, mientras que el teletransporte está
        // hardcodeado a la tecla T (ver SavePointTeleportTrigger.IsYButtonPressed). Por eso necesita su
        // propio nombre en vez de reutilizar North sin más — reutilizarlo mostraría "Q" en teclado para
        // un atajo que en realidad es "T".
        public const string Teleport = "interactable_teleport";

        // Hint del HUD para cambiar de personaje activo (panel "MainCharacters" en Start.unity, dos
        // Image a los lados de los retratos de Liam/Will/Estela — antes eran dos Image ESTÁTICAS que
        // siempre mostraban la misma flecha (Assets/Art/UI/Buttons/left.png, una de ellas espejada por
        // RectTransform), sin pasar por este sistema). En mando es literalmente el D-pad izq/der
        // (PartyControlManager.HandleInput, DpadLeft/DpadRight de GamepadInputReader) — incluso más
        // directo que el caso de Teleport, porque aquí SÍ es un botón dedicado en las 3 familias de
        // mando, así que puede tener su propia flecha por dirección en vez de reutilizar Dpad. En
        // teclado, en cambio, PlayerControls.cs mapea esas mismas acciones (DPadLeft/DPadRight) a las
        // teclas "," y "." — no a flechas de teclado ni a WASD — así que el icono de teclado tiene que
        // ser esas dos teclas concretas, no una flecha ni el WideKeycap("WASD") que ya usa Dpad.
        public const string DpadLeft = "interactable_dpad_left";
        public const string DpadRight = "interactable_dpad_right";

        // Mismo caso que DpadLeft/DpadRight (arriba), completando las 4 direcciones del D-pad por
        // separado. En mando siguen siendo el mismo control físico (el D-pad), así que Xbox/
        // PlayStation/Switch pueden reutilizar arte de flecha como con izq/der. En teclado, en cambio,
        // PlayerControls.cs mapea DPadUp/DPadDown a las teclas "J" y "G" — NO a flechas de teclado ni a
        // W/S de Move — así que, igual que con DpadLeft/DpadRight, el teclado necesita su propio sprite
        // por tecla en vez de un icono de flecha o el WideKeycap("WASD") que ya usa Dpad (genérico).
        // Sin consumidor en HUD todavía a fecha de esta entrada (2026-08-12) — Dpad, DpadUp y DpadDown
        // se añaden para completar el set; PartyControlManager solo escucha DpadDown/Left/Right hoy.
        public const string DpadUp = "interactable_dpad_up";
        public const string DpadDown = "interactable_dpad_down";

        public static readonly string[] All =
        {
            South, East, West, North, ShoulderLeft, ShoulderRight,
            TriggerLeft, TriggerRight, Dpad, Stick, Start, Confirm, Teleport,
            DpadLeft, DpadRight, DpadUp, DpadDown
        };
    }
}
