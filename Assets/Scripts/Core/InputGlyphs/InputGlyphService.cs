using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TextCore;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Servicio estático "cerebro": detecta con qué tipo de dispositivo está jugando la persona
    /// (Xbox / PlayStation / Switch / Teclado&amp;Ratón) y expone los sprites de botón correctos
    /// para esa familia. Las imágenes en sí NO se generan por código en tiempo de ejecución — son
    /// archivos PNG reales en <c>Assets/Resources/InputGlyphs/&lt;Familia&gt;/&lt;nombre&gt;.png</c>
    /// (mismos nombres que <see cref="InputGlyphNames"/>), creados con la herramienta de Editor
    /// <c>Tools/Input Glyphs/Generar Assets de Botones</c> (ver <c>Assets/Scripts/Editor/InputGlyphs</c>).
    /// Este servicio solo detecta qué familia usar y los carga con <see cref="Resources.Load"/>; si se
    /// quiere sustituir el arte de un botón basta con reemplazar su PNG en esa carpeta, no hace falta
    /// tocar ni una línea de este archivo.
    ///
    /// Dos formas de consumirlo:
    ///  1. Diálogos (TMP con &lt;sprite name="..."&gt;): este servicio inyecta el
    ///     <see cref="TMP_SpriteAsset"/> de la familia activa al principio de la lista
    ///     <c>fallbackSpriteAssets</c> de <c>DialogueIcons.asset</c> (cargado desde Resources), así
    ///     que el contenido de diálogo ya traducido no cambia ni una letra: solo cambia la imagen.
    ///  2. UI normal (componentes <see cref="UnityEngine.UI.Image"/>): llamar a
    ///     <see cref="GetSprite"/> con el mismo nombre (<see cref="InputGlyphNames"/>).
    ///
    /// Se arranca solo, sin tener que añadir nada en el Editor: <see cref="Bootstrap"/> crea su
    /// propio GameObject oculto y persistente la primera vez que carga una escena, siguiendo el
    /// mismo patrón "auto-bootstrap" que ya usa el proyecto para otros servicios estáticos
    /// (ver <c>ServiceLocator</c>).
    /// </summary>
    public static class InputGlyphService
    {
        public static InputGlyphDeviceFamily CurrentFamily { get; private set; } = InputGlyphDeviceFamily.KeyboardMouse;

        /// <summary>Se dispara cuando cambia la familia activa (por si alguna UI ya visible quiere refrescarse).</summary>
        public static event Action<InputGlyphDeviceFamily> FamilyChanged;

        const float MouseMoveThresholdSqr = 4f;   // px de movimiento de ratón por frame para contar como actividad
        const float StickThresholdSqr = 0.35f * 0.35f; // deadzone para contar el stick como actividad

        static readonly Dictionary<InputGlyphDeviceFamily, List<TMP_SpriteAsset>> _tmpAssetsByFamily = new();
        static readonly Dictionary<InputGlyphDeviceFamily, Dictionary<string, Sprite>> _rawSpritesByFamily = new();

        static TMP_SpriteAsset _dialogueIcons;
        static List<TMP_SpriteAsset> _originalFallbacks;
        static bool _bootstrapped;

        // Librería de sprites baked por familia (ver LoadFamilySprites) — sustituye a los
        // Resources.Load por PNG que se hacían antes en esta misma función.
        static InputGlyphFamilySpriteLibraryLink _familyLibraryLink;
        static bool _familyLibraryLoadAttempted;

        // ── Arranque automático ──────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_bootstrapped) return;
            _bootstrapped = true;

            var go = new GameObject("InputGlyphService (auto)") { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<InputGlyphServiceDriver>();

            LoadDialogueIconsAsset();

            // Arranque razonable: si ya hay un mando conectado (persona que abrió el juego mando en
            // mano), empezamos con esa familia; si no, con teclado/ratón. En cuanto detecte actividad
            // real de un dispositivo concreto, el driver corrige esto en el siguiente frame.
            var initialFamily = Gamepad.current != null
                ? DetectFamilyFromGamepad(Gamepad.current)
                : InputGlyphDeviceFamily.KeyboardMouse;
            SetFamily(initialFamily);
        }

        // Reinicio del estado estático entre sesiones de Play Mode en el Editor (mismo patrón que
        // el resto de servicios estáticos del proyecto, ver ServiceLocator).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _bootstrapped = false;
            _tmpAssetsByFamily.Clear();
            _rawSpritesByFamily.Clear();
            _dialogueIcons = null;
            _originalFallbacks = null;
            _familyLibraryLink = null;
            _familyLibraryLoadAttempted = false;
            CurrentFamily = InputGlyphDeviceFamily.KeyboardMouse;
            FamilyChanged = null;
        }

        static void LoadDialogueIconsAsset()
        {
            var link = Resources.Load<DialogueIconsResourceLink>("DialogueIconsLink");
            _dialogueIcons = link != null ? link.dialogueIcons : null;

            if (_dialogueIcons == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[InputGlyphService] No se pudo resolver DialogueIcons.asset a través de " +
                                 "Resources/DialogueIconsLink. Los iconos de botones en los diálogos no se " +
                                 "generarán dinámicamente hasta revisar esa referencia.");
#endif
                _originalFallbacks = new List<TMP_SpriteAsset>();
                return;
            }

            _originalFallbacks = _dialogueIcons.fallbackSpriteAssets != null
                ? new List<TMP_SpriteAsset>(_dialogueIcons.fallbackSpriteAssets)
                : new List<TMP_SpriteAsset>();

            StripDirectButtonEntries();
        }

        /// <summary>
        /// <c>DialogueIcons.asset</c> trae, además de los 18 fallbacks de un solo sprite, el nombre
        /// "interactable_A" ya incrustado directamente en su PROPIA tabla de sprites. TMP resuelve
        /// cada &lt;sprite name="..."&gt; mirando primero la tabla del propio asset y solo si no lo
        /// encuentra ahí pasa a mirar <c>fallbackSpriteAssets</c> — así que mientras "interactable_A"
        /// siga ahí, nuestro glyph generado por familia nunca se llegaría a usar para ese botón en
        /// concreto, por mucho que lo pongamos el primero de la lista de fallback.
        /// Por eso quitamos de la tabla propia (solo en memoria, en tiempo de ejecución; el .asset en
        /// disco no se toca) cualquier entrada cuyo nombre sea uno de los 11 botones que gestionamos
        /// nosotros, dejando intacto todo lo demás (iconos de objetos, monedas, etc.). Así la
        /// resolución cae siempre a la lista de fallback, donde el generado por familia va primero.
        /// </summary>
        static void StripDirectButtonEntries()
        {
            var table = _dialogueIcons.spriteCharacterTable;
            if (table == null) return;

            int removed = table.RemoveAll(ch => Array.IndexOf(InputGlyphNames.All, ch.name) >= 0);
            if (removed > 0) _dialogueIcons.UpdateLookupTables();
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>Sprite suelto (para <see cref="UnityEngine.UI.Image"/>) del botón <paramref name="name"/>
        /// (usar las constantes de <see cref="InputGlyphNames"/>) en la familia actualmente activa.</summary>
        public static Sprite GetSprite(string name)
        {
            EnsureFamilyBuilt(CurrentFamily);
            if (_rawSpritesByFamily.TryGetValue(CurrentFamily, out var dict) && dict.TryGetValue(name, out var sprite))
                return sprite;
            return null;
        }

        /// <summary>Igual que <see cref="GetSprite"/> pero para una familia concreta (por si alguna UI quiere
        /// mostrar siempre, por ejemplo, los iconos de teclado independientemente del mando activo).</summary>
        public static Sprite GetSprite(string name, InputGlyphDeviceFamily family)
        {
            EnsureFamilyBuilt(family);
            if (_rawSpritesByFamily.TryGetValue(family, out var dict) && dict.TryGetValue(name, out var sprite))
                return sprite;
            return null;
        }

        public static void SetFamily(InputGlyphDeviceFamily family)
        {
            EnsureFamilyBuilt(family);
            if (family == CurrentFamily) return;

            CurrentFamily = family;
            ApplyToDialogueIcons(family);
            FamilyChanged?.Invoke(family);
        }

        static void EnsureFamilyBuilt(InputGlyphDeviceFamily family)
        {
            if (_rawSpritesByFamily.ContainsKey(family)) return;

            var raw = LoadFamilySprites(family);
            _rawSpritesByFamily[family] = raw;
            _tmpAssetsByFamily[family] = BuildTmpSpriteAssets(raw, "InputGlyphs_" + family);

            // Si es la primera vez que se construye la familia activa, hay que aplicarla ya
            // (por ejemplo la primera llamada a EnsureFamilyBuilt desde Bootstrap).
            if (family == CurrentFamily) ApplyToDialogueIcons(family);
        }

        /// <summary>
        /// Puntero cacheado a los 4 sets de sprites baked (uno por familia), vía un único
        /// Resources.Load del link (ver <see cref="InputGlyphFamilySpriteLibraryLink"/>) — no de
        /// las imágenes en sí. Se intenta cargar una sola vez; si falla, se avisa una vez y se
        /// sigue devolviendo null en llamadas siguientes (sin reintentar Resources.Load cada vez
        /// que se pide un sprite).
        /// </summary>
        static InputGlyphFamilySpriteLibraryLink GetFamilyLibraryLink()
        {
            if (_familyLibraryLink != null) return _familyLibraryLink;
            if (_familyLibraryLoadAttempted) return null;
            _familyLibraryLoadAttempted = true;

            _familyLibraryLink = Resources.Load<InputGlyphFamilySpriteLibraryLink>("InputGlyphFamilySpriteLibraryLink");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_familyLibraryLink == null)
                Debug.LogWarning("[InputGlyphService] No se pudo resolver InputGlyphFamilySpriteLibraryLink desde " +
                                  "Resources. Los iconos de botón no se resolverán hasta revisar esa referencia " +
                                  "(Assets/Resources/InputGlyphFamilySpriteLibraryLink.asset).");
#endif
            return _familyLibraryLink;
        }

        /// <summary>
        /// Lee los 12 sprites de botón de la familia pedida (ver <see cref="InputGlyphNames"/>) desde
        /// el <see cref="InputGlyphFamilySpriteSet"/> baked correspondiente — referencia directa, sin
        /// Resources.Load por archivo. Si a una familia le falta algún sprite (hueco vacío en el
        /// asset), se usa el de Xbox como respaldo para ese botón concreto en vez de dejar un hueco
        /// vacío — mejor un icono "equivocado" pero visible que un hint invisible en mitad de una
        /// partida.
        /// </summary>
        static Dictionary<string, Sprite> LoadFamilySprites(InputGlyphDeviceFamily family)
        {
            var result = new Dictionary<string, Sprite>(InputGlyphNames.All.Length);

            var link = GetFamilyLibraryLink();
            var familySet = link != null ? link.GetSet(family) : null;
            var xboxSet = link != null ? link.GetSet(InputGlyphDeviceFamily.Xbox) : null;

            foreach (var buttonName in InputGlyphNames.All)
            {
                var sprite = familySet != null ? familySet.GetSprite(buttonName) : null;

                // Confirm (UI/Submit) es fisicamente el mismo boton que South (Interactuar) en
                // cualquier mando real -- Xbox/PlayStation/Switch comparten un unico boton para
                // ambos conceptos, asi que si la familia no es teclado y no hay sprite propio de
                // "confirm" en su set, reutilizamos el de South de esa MISMA familia antes de caer
                // al respaldo de Xbox de mas abajo -- evita tener que arrastrar el mismo sprite dos
                // veces en 3 assets distintos. Teclado&Raton SI necesita un sprite propio de Confirm
                // (Espacio/Enter), porque ahi Interactuar (E) y Confirmar son teclas distintas -- ver
                // InputGlyphNames.Confirm.
                if (sprite == null && buttonName == InputGlyphNames.Confirm && family != InputGlyphDeviceFamily.KeyboardMouse && familySet != null)
                    sprite = familySet.GetSprite(InputGlyphNames.South);


                if (sprite == null && family != InputGlyphDeviceFamily.Xbox && xboxSet != null)
                {
                    sprite = xboxSet.GetSprite(buttonName);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (sprite != null)
                        Debug.LogWarning($"[InputGlyphService] Falta el sprite '{buttonName}' en " +
                                          $"InputGlyphFamilySpriteSet_{family}.asset. Usando el de Xbox como respaldo.");
#endif
                }

                if (sprite == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[InputGlyphService] No se encontró ningún sprite para '{buttonName}' " +
                                      $"(ni en {family} ni en Xbox). Rellena Assets/_UI/InputGlyphFamilySpriteSet_" +
                                      $"{family}.asset antes de jugar.");
#endif
                    continue;
                }

                result[buttonName] = sprite;
            }

            return result;
        }

        // Calibración de tamaño para los sprites inyectados en TMP (ver BuildTmpSpriteAssets).
        //
        // 2026-08-14 — REESCRITA. La versión anterior partía de las métricas "afinadas a ojo" de la
        // entrada interactable_A de DialogueIcons.asset (832×1248 px, bearingX -60, bearingY 700,
        // advance 750, scale 3) y las reescalaba con (RefHeight * 3) / alto_del_PNG, con la intención
        // de que todos los botones salieran del mismo tamaño viniera el PNG en la resolución que
        // viniera. El problema es que TMP YA normaliza la altura del sprite él solo:
        //
        //     currentElementScale = fontFace.ascentLine / glyph.metrics.height * character.scale
        //     (TextMeshProUGUI.cs, rama spriteFace.pointSize == 0)
        //
        // …así que el alto renderizado sale = ascentLine * character.scale, INDEPENDIENTE ya del alto
        // del PNG. Al multiplicar además por RefHeight/alto se dividía dos veces por el mismo alto y
        // el resultado era alto_renderizado = ascentLine * 3 * 1248 / alto_PNG: con los PNG de teclado
        // (300 px) salía a ~12 veces el ascender de la fuente — el "botón gigante" que tapaba media
        // línea de diálogo.
        //
        // Ahora se usa directamente el número de ascenders que debe medir el icono, el MISMO valor que
        // llevan los iconos propios de DialogueIcons.asset (m_Scale en su m_SpriteCharacterTable), y
        // las métricas se derivan del alto del glifo con las mismas proporciones que allí, para que un
        // <sprite name="start"> (botón, generado aquí) y un <sprite name="algas"> (objeto, tabla del
        // asset) se vean exactamente igual de grandes y apoyados en la misma línea base.
        //
        // Si hay que retocar el tamaño de los iconos en diálogo, se tocan LOS DOS a la vez:
        // este GlyphScale y el m_Scale de Assets/Art/UI/DialogueIcons/DialogueIcons.asset.
        const float GlyphScale = 1.25f;        // altura del icono, en ascenders de la fuente
        const float GlyphBearingXRatio = 0.06f; // margen izquierdo, en fracción del alto del glifo
        const float GlyphBearingYRatio = 0.95f; // 0.95 → el icono se apoya en la línea base
        const float GlyphAdvanceRatio = 0.14f;  // hueco tras el icono, en fracción de su alto

        /// <summary>
        /// Empaqueta un diccionario nombre→sprite ya cargado (ver <see cref="LoadFamilySprites"/>) en
        /// UN <see cref="TMP_SpriteAsset"/> POR SPRITE (no uno solo con todos los glifos dentro), para
        /// que TMP pueda resolver &lt;sprite name="..."&gt; en los textos de diálogo. Esto NO genera
        /// imágenes, solo envuelve sprites ya existentes en la estructura que TMP necesita — igual que
        /// hace a mano <c>DialogueIcons.asset</c> con sus 18 fallbacks originales, que también son uno
        /// por sprite (ver comentario en <see cref="StripDirectButtonEntries"/>).
        ///
        /// Es imprescindible un asset por sprite y no uno compartido con varios glifos: TMP calcula las
        /// UV de cada carácter-sprite dividiendo <c>glyphRect</c> entre el tamaño de
        /// <c>TMP_SpriteAsset.spriteSheet</c> (ver <c>TMP_Text.SaveSpriteVertexInfo</c>), es decir que
        /// TODOS los glifos de un mismo asset comparten forzosamente una única textura de fondo. Como
        /// cada botón viene de su propio PNG independiente (no de un atlas empaquetado), la única forma
        /// de que esa división salga bien para cada uno es darle a cada glifo su propio asset con
        /// <c>spriteSheet</c> apuntando exactamente a la textura de SU sprite. Antes esta función metía
        /// los 11-17 botones de una familia en un único asset sin asignar <c>spriteSheet</c> en
        /// absoluto (quedaba null): en cuanto un diálogo insertaba uno de estos sprites, TMP intentaba
        /// leer <c>spriteSheet.width</c> sobre null y lanzaba el NullReferenceException en
        /// <c>SaveSpriteVertexInfo</c> que rompía el renderizado del texto (y de paso el
        /// diálogo/cinemática que lo mostraba).
        /// </summary>
        static List<TMP_SpriteAsset> BuildTmpSpriteAssets(Dictionary<string, Sprite> sprites, string assetNamePrefix)
        {
            var result = new List<TMP_SpriteAsset>(sprites.Count);

            foreach (var kvp in sprites)
            {
                var sprite = kvp.Value;
                if (sprite == null) continue;

                var texture = sprite.texture;
                if (texture == null) continue;

                var assetName = assetNamePrefix + "_" + kvp.Key;
                var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                asset.name = assetName;

                var shader = Shader.Find("TextMeshPro/Sprite");
                var material = new Material(shader) { name = assetName + " Material" };
                material.SetTexture(ShaderUtilities.ID_MainTex, texture);

                // TMP_SpriteAsset trae un modo de migración pensado para assets viejos creados en el
                // Editor: en cuanto el asset tiene material asignado y su campo interno "version" está
                // vacío (el caso de CUALQUIER instancia recién creada por código), la primera llamada a
                // UpdateLookupTables() dispara UpgradeSpriteAsset(), que vacía spriteCharacterTable/
                // spriteGlyphTable y los reconstruye leyendo el campo legado "spriteInfoList" — que en
                // una instancia nueva es null (a diferencia de spriteCharacterTable/spriteGlyphTable,
                // este NO se inicializa solo), así que revienta con NullReferenceException en cuanto se
                // le asigna material y se llama a UpdateLookupTables(). "version" tiene setter interno
                // al ensamblado de TMP, así que en vez de tocarlo por reflection dejamos que esa
                // migración se dispare una única vez, de forma inofensiva, con spriteInfoList ya vacío
                // (no null) y las tablas de glifos todavía sin rellenar: así el asset queda marcado
                // como "ya migrado" antes de volcarle nuestro sprite real.
                asset.spriteInfoList = new List<TMP_Sprite>();
                asset.material = material;
                asset.spriteSheet = texture;
                asset.UpdateLookupTables();

                // spriteCharacterTable/spriteGlyphTable son propiedades de solo lectura (el setter es
                // interno al paquete de TMP) — tras la migración de arriba ya están inicializadas a
                // listas vacías, así que basta con vaciarlas por si acaso y rellenarlas con Add() en vez
                // de reasignarlas.
                asset.spriteCharacterTable?.Clear();
                asset.spriteGlyphTable?.Clear();

                // Usamos sprite.textureRect (posición + tamaño DENTRO de sprite.texture) en vez de
                // sprite.rect: si el sprite viniera de un atlas empaquetado por Unity, sprite.texture
                // sería la textura del atlas completo y el icono ocuparía solo una región de ella, no
                // toda — textureRect es la que da esa región real. Para un PNG suelto (el caso normal
                // aquí) coincide con (0, 0, ancho, alto) igual que antes.
                Rect texRect = sprite.textureRect;
                float w = texRect.width;
                float h = texRect.height;
                // TMP normaliza él solo la altura del sprite al ascender de la fuente (ver el bloque de
                // constantes Glyph* arriba), así que el alto en pantalla ya es independiente de la
                // resolución del PNG y basta con decir cuántos ascenders debe medir. NO hay que volver
                // a dividir por h aquí: eso era el bug del icono gigante.
                float characterScale = GlyphScale;

                var glyph = new TMP_SpriteGlyph
                {
                    index = 0,
                    metrics = new GlyphMetrics(w, h, GlyphBearingXRatio * h, GlyphBearingYRatio * h, w + GlyphAdvanceRatio * h),
                    glyphRect = new GlyphRect((int)texRect.x, (int)texRect.y, (int)w, (int)h),
                    scale = 1f,
                    atlasIndex = 0,
                    sprite = sprite,
                };
                asset.spriteGlyphTable.Add(glyph);

                var character = new TMP_SpriteCharacter(0xFFFE, glyph) { name = kvp.Key, scale = characterScale };
                asset.spriteCharacterTable.Add(character);

                asset.UpdateLookupTables();
                result.Add(asset);
            }

            return result;
        }

        static void ApplyToDialogueIcons(InputGlyphDeviceFamily family)
        {
            if (_dialogueIcons == null) return;
            if (!_tmpAssetsByFamily.TryGetValue(family, out var generated)) return;

            // Los generados van primero: TMP resuelve cada <sprite name="..."> recorriendo la lista de
            // fallback en orden y se queda con la primera coincidencia, así que esto pisa únicamente
            // los nombres de botón que generamos y deja intactos el resto de iconos originales.
            var merged = new List<TMP_SpriteAsset>(_originalFallbacks.Count + generated.Count);
            merged.AddRange(generated);
            merged.AddRange(_originalFallbacks);
            _dialogueIcons.fallbackSpriteAssets = merged;
        }

        // ── Detección de familia ─────────────────────────────────────────────

        internal static InputGlyphDeviceFamily DetectFamilyFromGamepad(Gamepad gp)
        {
            string product = (gp.description.product ?? string.Empty).ToLowerInvariant();
            string manufacturer = (gp.description.manufacturer ?? string.Empty).ToLowerInvariant();
            string combined = product + " " + manufacturer;

            if (combined.Contains("dualsense") || combined.Contains("dualshock") ||
                combined.Contains("wireless controller") || combined.Contains("playstation") ||
                combined.Contains("sony"))
                return InputGlyphDeviceFamily.PlayStation;

            if (combined.Contains("pro controller") || combined.Contains("joy-con") ||
                combined.Contains("joycon") || combined.Contains("nintendo") || combined.Contains("switch"))
                return InputGlyphDeviceFamily.Switch;

            // Xbox / XInput y mandos genéricos (incluye la mayoría de mandos "retro"/terceros, que
            // suelen emular el layout de Xbox): usamos Xbox como estándar por defecto.
            return InputGlyphDeviceFamily.Xbox;
        }

        // ── Driver interno: detecta actividad de dispositivo frame a frame ───

        /// <summary>
        /// MonoBehaviour mínimo, creado únicamente por <see cref="Bootstrap"/> (nunca colocado a mano
        /// en una escena), cuyo único trabajo es vigilar qué dispositivo generó el último input real
        /// y avisar a <see cref="InputGlyphService"/> para que cambie de familia si hace falta.
        /// </summary>
        sealed class InputGlyphServiceDriver : MonoBehaviour
        {
            void Update()
            {
                var kb = Keyboard.current;
                var mouse = Mouse.current;
                bool keyboardMouseActivity =
                    (kb != null && kb.anyKey.wasPressedThisFrame) ||
                    (mouse != null &&
                     (mouse.delta.ReadValue().sqrMagnitude > MouseMoveThresholdSqr ||
                      mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                      mouse.middleButton.wasPressedThisFrame ||
                      mouse.scroll.ReadValue().sqrMagnitude > 0f));

                if (keyboardMouseActivity)
                {
                    SetFamily(InputGlyphDeviceFamily.KeyboardMouse);
                    return;
                }

                var gp = Gamepad.current;
                if (gp != null && HasAnyGamepadActivity(gp))
                    SetFamily(DetectFamilyFromGamepad(gp));
            }

            static bool HasAnyGamepadActivity(Gamepad gp)
            {
                var controls = gp.allControls;
                for (int i = 0; i < controls.Count; i++)
                {
                    if (controls[i] is ButtonControl button && button.wasPressedThisFrame)
                        return true;
                }

                return gp.leftStick.ReadValue().sqrMagnitude > StickThresholdSqr ||
                       gp.rightStick.ReadValue().sqrMagnitude > StickThresholdSqr;
            }
        }
    }
}
