namespace Core.InputGlyphs
{
    /// <summary>
    /// Texto corto ("A", "Espacio", "WASD"...) del botón/tecla que corresponde a cada nombre de
    /// <see cref="InputGlyphNames"/> en la familia de dispositivo activa. Es el equivalente en TEXTO
    /// de lo que <see cref="InputGlyphService.GetSprite(string)"/> ya hace con sprites — pensado para
    /// frases tipo "Pulsa {BOTON} para continuar" en <c>TutorialPromptNode</c>, donde el LITERAL (no
    /// solo el icono) tiene que cambiar según el dispositivo conectado.
    ///
    /// Las etiquetas están escritas a mano a partir de los bindings reales de
    /// <c>Assets/Scripts/Core/PlayerControls.inputactions</c> (grupo "Keyboard&amp;Mouse" para
    /// teclado/ratón). Si se cambia un binding en ese asset, esta tabla se queda desincronizada —
    /// no hay forma automática de derivarla desde aquí sin acoplar este archivo runtime al asset de
    /// Input System, así que hay que actualizarla a mano (igual que ya pasa con los sprites baked de
    /// Assets/_UI/InputGlyphFamilySpriteSet_*.asset, que tampoco se generan solos).
    ///
    /// Las etiquetas que son PALABRAS (no símbolos ni nombres de botón universales) pasan por
    /// <see cref="LocalizationManager"/> con las claves GLYPH_* de ui_es.json/ui_en.json: sin esto,
    /// jugando en inglés se veía "[D-Pad arriba] Quest Detail" en el HUD de misiones (y lo mismo en
    /// el menú de controles, los prompts de tutorial y el minijuego del pilla-pilla, que comparten
    /// esta tabla). Los literales que NO se traducen —"A"/"B"/"X"/"Y", "✕○□△", "L1"/"RT"/"L3",
    /// "WASD", "Esc", "Ctrl", teclas sueltas ("E", "Q", "J", "G", ",", ".")— se quedan a pelo a
    /// propósito: son idénticos en todos los idiomas y meterlos en el catálogo solo añadiría claves
    /// que mantener.
    /// </summary>
    public static class InputGlyphLabels
    {
        /// <summary>
        /// Texto localizado de <paramref name="key"/>, con el español como fallback si todavía no hay
        /// <see cref="LocalizationManager"/> (p.ej. una escena arrancada suelta en el editor antes de
        /// que cargue Start.unity) o si falta la clave en el catálogo del idioma activo.
        /// </summary>
        static string Loc(string key, string fallbackEs)
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return fallbackEs;
            var text = loc.Get(key, fallbackEs);
            return string.IsNullOrEmpty(text) ? fallbackEs : text;
        }

        public static string GetLabel(string buttonName, InputGlyphDeviceFamily family)
        {
            bool kb = family == InputGlyphDeviceFamily.KeyboardMouse;

            switch (buttonName)
            {
                // Interactuar/Saltar — GamePlay.Interact (E) y GamePlay.Jump (Espacio) comparten este
                // mismo icono en mando (un único botón South), pero en teclado son teclas distintas.
                // Se etiqueta como "E" porque el uso real de este nombre en el proyecto es siempre
                // para interactuar con NPCs/puertas/objetos del mundo (Interactable), no para saltar.
                case InputGlyphNames.South:
                    if (kb) return "E";
                    return family == InputGlyphDeviceFamily.PlayStation ? "✕"
                         : family == InputGlyphDeviceFamily.Switch ? "B"
                         : "A";

                // Confirmar (UI/Submit) — prompts con GamePlay deshabilitado (cinemáticas). En mando
                // es el mismo botón físico que South; en teclado es Espacio/Enter, NO la tecla E.
                case InputGlyphNames.Confirm:
                    if (kb) return Loc("GLYPH_KEY_SPACE", "Espacio");
                    return family == InputGlyphDeviceFamily.PlayStation ? "✕"
                         : family == InputGlyphDeviceFamily.Switch ? "B"
                         : "A";

                case InputGlyphNames.East: // Ataque mágico derecho — <Mouse>/rightButton en teclado
                    if (kb) return Loc("GLYPH_CLICK_RIGHT", "clic derecho");
                    return family == InputGlyphDeviceFamily.PlayStation ? "○"
                         : family == InputGlyphDeviceFamily.Switch ? "A"
                         : "B";

                case InputGlyphNames.West: // Ataque mágico izquierdo — <Mouse>/leftButton en teclado
                    if (kb) return Loc("GLYPH_CLICK_LEFT", "clic izquierdo");
                    return family == InputGlyphDeviceFamily.PlayStation ? "□"
                         : family == InputGlyphDeviceFamily.Switch ? "Y"
                         : "X";

                case InputGlyphNames.North: // Ataque mágico especial — <Keyboard>/q
                    if (kb) return "Q";
                    return family == InputGlyphDeviceFamily.PlayStation ? "△"
                         : family == InputGlyphDeviceFamily.Switch ? "X"
                         : "Y";

                case InputGlyphNames.ShoulderLeft: // <Mouse>/scroll/down en teclado
                    // "rueda ↓" con la flecha Unicode no se veía: ni LiberationSans SDF (fallback
                    // por defecto) ni Nunito-Bold SDF (fuente del menú) tienen ese glifo en su
                    // atlas, así que TMP lo sustituía por un "□" y lo avisaba por consola en bucle.
                    // Texto en vez de símbolo: se entiende igual y no depende de qué fuente esté
                    // activa en cada sitio donde se use esta etiqueta.
                    if (kb) return Loc("GLYPH_WHEEL_DOWN", "rueda abajo");
                    return family == InputGlyphDeviceFamily.PlayStation ? "L1"
                         : family == InputGlyphDeviceFamily.Switch ? "L"
                         : "LB";

                case InputGlyphNames.ShoulderRight: // <Mouse>/scroll/up en teclado
                    if (kb) return Loc("GLYPH_WHEEL_UP", "rueda arriba");
                    return family == InputGlyphDeviceFamily.PlayStation ? "R1"
                         : family == InputGlyphDeviceFamily.Switch ? "R"
                         : "RB";

                // LT y RT comparten binding de teclado (<Keyboard>/leftCtrl) en PlayerControls.inputactions
                // a fecha de este comentario — no es un error de esta tabla, así está mapeado el asset.
                case InputGlyphNames.TriggerLeft:
                    if (kb) return "Ctrl";
                    return family == InputGlyphDeviceFamily.PlayStation ? "L2"
                         : family == InputGlyphDeviceFamily.Switch ? "ZL"
                         : "LT";

                case InputGlyphNames.TriggerRight:
                    if (kb) return "Ctrl";
                    return family == InputGlyphDeviceFamily.PlayStation ? "R2"
                         : family == InputGlyphDeviceFamily.Switch ? "ZR"
                         : "RT";

                // D-Pad de GamePlay (distinto de Move) — <Keyboard>/j, g, comma, period
                case InputGlyphNames.Dpad:
                    return kb ? "J/G/,/." : Loc("GLYPH_DPAD", "el D-Pad");

                case InputGlyphNames.Stick: // Move — WASD/flechas en teclado, stick izquierdo en mando
                    return kb ? "WASD" : Loc("GLYPH_STICK", "el Joystick");

                case InputGlyphNames.Start: // <Keyboard>/escape
                    if (kb) return "Esc";
                    return family == InputGlyphDeviceFamily.PlayStation ? "Options"
                         : family == InputGlyphDeviceFamily.Switch ? "+"
                         : Loc("GLYPH_MENU", "Menú");

                // Sprint — GamePlay.Sprint. En teclado, Mayús izquierda; en mando, clic del stick
                // izquierdo (L3), ver PlayerControls.inputactions. Sin sprite propio todavía (ver
                // InputGlyphNames.Sprint), así que de momento es solo texto.
                case InputGlyphNames.Sprint:
                    if (kb) return Loc("GLYPH_KEY_SHIFT", "Mayús");
                    return family == InputGlyphDeviceFamily.PlayStation ? "L3"
                         : family == InputGlyphDeviceFamily.Switch ? Loc("GLYPH_STICK_CLICK", "clic del stick")
                         : "L3";

                case InputGlyphNames.Teleport: // Mismo botón físico que North en mando; "T" en teclado
                    return kb ? "T" : GetLabel(InputGlyphNames.North, family);

                case InputGlyphNames.DpadLeft: // "," en teclado
                    return kb ? "," : Loc("GLYPH_DPAD_LEFT", "D-Pad izquierda");

                case InputGlyphNames.DpadRight: // "." en teclado
                    return kb ? "." : Loc("GLYPH_DPAD_RIGHT", "D-Pad derecha");

                // Faltaban estos dos casos: sin ellos caían en el "default" de abajo y devolvían
                // "?" literal — es lo que se veía en el HUD de misiones ("[?] Quest Detail") en vez
                // de la tecla/label real. Ver InputGlyphNames.DpadUp/DpadDown y
                // InputGlyphFamilySpriteSet ("J" arriba / "G" abajo en teclado).
                case InputGlyphNames.DpadUp: // "J" en teclado
                    return kb ? "J" : Loc("GLYPH_DPAD_UP", "D-Pad arriba");

                case InputGlyphNames.DpadDown: // "G" en teclado
                    return kb ? "G" : Loc("GLYPH_DPAD_DOWN", "D-Pad abajo");

                default:
                    return "?";
            }
        }
    }
}
