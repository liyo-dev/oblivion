using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Datos (no lógica) de la pantalla "Controles" del menú principal: qué acciones se listan, en
    /// qué orden y con qué descripción. El icono y la etiqueta de tecla/botón de cada fila se
    /// resuelven en tiempo real contra InputGlyphService/InputGlyphLabels según la familia de
    /// dispositivo activa (ver ControlRowWidget) — este asset solo fija el CONTENIDO (qué acciones
    /// existen y cómo se explican), no el arte, siguiendo la convención del proyecto de
    /// "ScriptableObjects como datos, nunca lógica" (ver README § Arquitectura clave).
    ///
    /// IDIOMAS: los textos de este asset NO se muestran tal cual. Cada campo de texto tiene su
    /// clave de catálogo hermana (descriptionKey / keyboardLabelKey / gamepadLabelKey) que
    /// ControlRowWidget resuelve contra LocalizationManager (claves CONTROLS_*/GLYPH_* de
    /// ui_es.json/ui_en.json); el literal en español se queda como FALLBACK para cuando falta la
    /// clave o el manager todavía no ha cargado (p.ej. una escena abierta suelta en el editor). Sin
    /// esto, la pantalla de Controles mostraba las descripciones en español aunque el juego
    /// estuviera en inglés — el resto de la fila (icono y etiqueta de tecla) sí se traducía ya vía
    /// InputGlyphLabels, así que se veía media fila en inglés y media en español.
    ///
    /// Uso: Assets → Create → El Sendero → Controles → Esquema de Controles. Al crearlo, Reset()
    /// pre-rellena la lista con las acciones reales del juego (ver GamepadInputReader.cs) para no
    /// partir de cero. LB/RB (rueda abajo/arriba en teclado) disparan
    /// InputEventType.LeftShoulder/RightShoulder en GamepadInputReader; el único suscriptor de esos
    /// eventos es CombatCameraTargeting.HandleGamepadInput, que llama a
    /// SwitchToPreviousTarget()/SwitchToNextTarget() — solo tienen efecto mientras hay un objetivo de
    /// combate bloqueado (isLockActive), para ciclar entre los enemigos cercanos. Todas las filas
    /// están verificadas contra GamepadInputReader/InputGlyphLabels/CombatCameraTargeting.
    /// </summary>
    [CreateAssetMenu(menuName = "El Sendero/Controles/Esquema de Controles", fileName = "ControlsSchemeConfig")]
    public class ControlsSchemeConfig : ScriptableObject
    {
        public List<ControlsSchemeEntry> entries = new();

        void Reset()
        {
            entries = BuildDefaultEntries();
        }

        /// <summary>
        /// Lista de arranque con las acciones reales del juego (verificadas contra
        /// GamepadInputReader.cs/InputGlyphLabels.cs). Público y estático para que tanto Reset()
        /// (al crear el asset a mano desde el menú Assets → Create) como herramientas de Editor que
        /// generan el asset por código (ver Assets/Scripts/Editor/ControlsMenuSceneBuilder.cs)
        /// partan exactamente de los mismos datos.
        /// </summary>
        public static List<ControlsSchemeEntry> BuildDefaultEntries()
        {
            return new List<ControlsSchemeEntry>
            {
                Entry(InputGlyphNames.Stick, "CONTROLS_MOVE", "Mover"),
                Manual("CONTROLS_CAMERA", "Cámara",
                       "GLYPH_MOUSE", "Ratón",
                       "GLYPH_STICK_RIGHT", "Stick derecho"),
                // Saltar necesita override de teclado: el icono South se etiqueta "E" por defecto
                // (reservado para Interactuar), pero la tecla real de salto es Espacio. Reutiliza la
                // clave GLYPH_KEY_SPACE que ya usa InputGlyphLabels para Confirm.
                Entry(InputGlyphNames.South, "CONTROLS_JUMP", "Saltar",
                      keyboardLabelKey: "GLYPH_KEY_SPACE", keyboardOverride: "Espacio"),
                Entry(InputGlyphNames.South, "CONTROLS_INTERACT", "Interactuar (NPCs, puertas, objetos)"),
                Entry(InputGlyphNames.West, "CONTROLS_SPELL_LEFT", "Hechizo — slot izquierdo"),
                Entry(InputGlyphNames.East, "CONTROLS_SPELL_RIGHT", "Hechizo — slot derecho"),
                Entry(InputGlyphNames.North, "CONTROLS_SPELL_SPECIAL", "Hechizo especial"),
                Entry(InputGlyphNames.Sprint, "CONTROLS_SPRINT", "Correr"),
                // Flechas Unicode (←/→) sustituidas por texto: ni LiberationSans SDF ni Nunito-Bold
                // SDF (las fuentes que usa el menú) tienen esos glifos, así que TMP los pintaba como
                // "□" y llenaba la consola de avisos de fuente en cada refresco del ScrollRect.
                Entry(InputGlyphNames.ShoulderLeft, "CONTROLS_TARGET_PREV", "Cambiar objetivo de combate (anterior)"),
                Entry(InputGlyphNames.ShoulderRight, "CONTROLS_TARGET_NEXT", "Cambiar objetivo de combate (siguiente)"),
                // Nuevo (1 sep 2026, petición Raúl): activar/desactivar el lock-on automático de
                // cámara/objetivo en combate. Sin glyph propio (botón sin icono de familia) — fila
                // manual como la de Cámara, mismo patrón. Ver GamepadInputReader.InputEventType.
                // ToggleTargetLock / CombatCameraTargeting.ToggleTargetingSuppressed.
                Manual("CONTROLS_TARGET_TOGGLE", "Activar/desactivar el bloqueo automático de objetivo",
                       "GLYPH_KEY_F", "F",
                       "GLYPH_STICK_RIGHT_CLICK", "Clic stick derecho (R3)"),
                Entry(InputGlyphNames.DpadLeft, "CONTROLS_CHARACTER_PREV", "Cambiar de personaje (anterior)"),
                Entry(InputGlyphNames.DpadRight, "CONTROLS_CHARACTER_NEXT", "Cambiar de personaje (siguiente)"),
                Entry(InputGlyphNames.Teleport, "CONTROLS_TELEPORT", "Teletransportarse al punto de guardado"),
                Entry(InputGlyphNames.Start, "CONTROLS_PAUSE", "Pausa / Menú"),
                Entry(InputGlyphNames.Select, "CONTROLS_BIG_MAP", "Ver mapa grande"),
            };
        }

        static ControlsSchemeEntry Entry(string glyphName, string descriptionKey, string description,
                                         string keyboardLabelKey = null, string keyboardOverride = null,
                                         string gamepadLabelKey = null, string gamepadOverride = null)
            => new ControlsSchemeEntry
            {
                glyphName = glyphName,
                descriptionKey = descriptionKey,
                description = description,
                keyboardLabelKey = keyboardLabelKey,
                keyboardLabelOverride = keyboardOverride,
                gamepadLabelKey = gamepadLabelKey,
                gamepadLabelOverride = gamepadOverride
            };

        static ControlsSchemeEntry Manual(string descriptionKey, string description,
                                          string keyboardLabelKey, string keyboardLabel,
                                          string gamepadLabelKey, string gamepadLabel)
            => new ControlsSchemeEntry
            {
                glyphName = null,
                descriptionKey = descriptionKey,
                description = description,
                keyboardLabelKey = keyboardLabelKey,
                keyboardLabelOverride = keyboardLabel,
                gamepadLabelKey = gamepadLabelKey,
                gamepadLabelOverride = gamepadLabel
            };
    }

    [Serializable]
    public class ControlsSchemeEntry
    {
        [Tooltip("Clave del catálogo de localización (CONTROLS_*) con el texto que ve el jugador. Si está vacía o no existe en el idioma activo, se usa 'description' tal cual.")]
        public string descriptionKey;

        [Tooltip("Texto que ve el jugador (qué hace este control). Es solo el FALLBACK en español: lo que se muestra sale de 'descriptionKey' vía LocalizationManager.")]
        public string description;

        [Tooltip("Nombre de InputGlyphNames a usar para el icono. Déjalo vacío para una fila solo de texto, sin icono propio (p.ej. Cámara/ratón).")]
        public string glyphName;

        [Tooltip("Clave del catálogo (GLYPH_*) para la etiqueta de Teclado&Ratón. Solo se usa si 'keyboardLabelOverride' está relleno; si falta la clave, se muestra el override tal cual.")]
        public string keyboardLabelKey;

        [Tooltip("Si se rellena, sustituye a InputGlyphLabels para Teclado&Ratón (p.ej. Saltar necesita 'Espacio', no la 'E' por defecto del icono South, que está reservada para Interactuar).")]
        public string keyboardLabelOverride;

        [Tooltip("Clave del catálogo (GLYPH_*) para la etiqueta de mando. Solo se usa si 'gamepadLabelOverride' está relleno; si falta la clave, se muestra el override tal cual.")]
        public string gamepadLabelKey;

        [Tooltip("Si se rellena, sustituye a InputGlyphLabels para CUALQUIER familia de mando (Xbox/PlayStation/Switch) con el mismo texto. Déjalo vacío para que cada familia use su propio botón (✕/○/□/△ etc.) vía InputGlyphLabels.")]
        public string gamepadLabelOverride;
    }
}
