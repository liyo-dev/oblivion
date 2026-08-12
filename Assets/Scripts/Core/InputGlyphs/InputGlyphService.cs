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

        static readonly Dictionary<InputGlyphDeviceFamily, TMP_SpriteAsset> _tmpAssetsByFamily = new();
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
            _tmpAssetsByFamily[family] = BuildTmpSpriteAsset(raw, "InputGlyphs_" + family);

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

        // Calibración de tamaño para los sprites inyectados en TMP (ver BuildTmpSpriteAsset). Antes de
        // este servicio, DialogueIcons.asset ya tenía a mano una entrada "interactable_A" con métricas
        // afinadas por prueba y error para que el icono (arte Xbox real, 832×1248 px) se viera bien
        // insertado en medio de una línea de diálogo: ancho 832, alto 1248, bearingX -60, bearingY 700,
        // advance 750, con un multiplicador "scale" de 3 a nivel de carácter TMP. Esos números son la
        // única referencia real que tenemos de "esto se veía bien", así que en vez de inventar una
        // fórmula genérica los usamos como base y escalamos proporcionalmente para el resto de sprites
        // (que pueden venir en cualquier resolución: el arte Xbox real son 832×1248 px pero los
        // placeholders de PlayStation/Switch/Teclado son 96×96 px). Sin esto, un icono de 96 px con
        // "scale 1" sale con un tamaño y una posición vertical completamente distintos a los que tenía
        // el original calibrado, y termina recortado por la máscara del cuadro de diálogo.
        const float RefWidth = 832f;
        const float RefHeight = 1248f;
        const float RefCharacterScale = 3f;
        const float RefBearingXRatio = -60f / RefWidth;
        const float RefBearingYRatio = 700f / RefHeight;
        const float RefAdvanceRatio = 750f / RefWidth;

        /// <summary>
        /// Empaqueta un diccionario nombre→sprite ya cargado (ver <see cref="LoadFamilySprites"/>) en
        /// un <see cref="TMP_SpriteAsset"/> nuevo, para que TMP pueda resolver
        /// &lt;sprite name="..."&gt; en los textos de diálogo. Esto NO genera imágenes, solo envuelve
        /// sprites ya existentes en la estructura que TMP necesita.
        /// </summary>
        static TMP_SpriteAsset BuildTmpSpriteAsset(Dictionary<string, Sprite> sprites, string assetName)
        {
            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = assetName;

            var shader = Shader.Find("TextMeshPro/Sprite");
            var material = new Material(shader) { name = assetName + " Material" };

            // TMP_SpriteAsset trae un modo de migración pensado para assets viejos creados en el
            // Editor: en cuanto el asset tiene material asignado y su campo interno "version" está
            // vacío (el caso de CUALQUIER instancia recién creada por código), la primera llamada a
            // UpdateLookupTables() dispara UpgradeSpriteAsset(), que vacía spriteCharacterTable/
            // spriteGlyphTable y los reconstruye leyendo el campo legado "spriteInfoList" — que en una
            // instancia nueva es null (a diferencia de spriteCharacterTable/spriteGlyphTable, este NO
            // se inicializa solo), así que revienta con NullReferenceException en cuanto se le asigna
            // material y se llama a UpdateLookupTables(). "version" tiene setter interno al ensamblado
            // de TMP, así que en vez de tocarlo por reflection dejamos que esa migración se dispare una
            // única vez, de forma inofensiva, con spriteInfoList ya vacío (no null) y las tablas de
            // glifos todavía sin rellenar: así el asset queda marcado como "ya migrado" antes de
            // volcarle nuestros sprites reales.
            asset.spriteInfoList = new List<TMP_Sprite>();
            asset.material = material;
            asset.UpdateLookupTables();

            // spriteCharacterTable/spriteGlyphTable son propiedades de solo lectura (el setter es
            // interno al paquete de TMP) — tras la migración de arriba ya están inicializadas a listas
            // vacías, así que basta con vaciarlas por si acaso y rellenarlas con Add() en vez de
            // reasignarlas.
            asset.spriteCharacterTable?.Clear();
            asset.spriteGlyphTable?.Clear();

            uint glyphIndex = 0;
            foreach (var kvp in sprites)
            {
                var sprite = kvp.Value;
                if (sprite == null) continue;

                float w = sprite.rect.width;
                float h = sprite.rect.height;
                // Escala inversamente proporcional a la altura real del PNG para que TODOS los
                // botones se vean del mismo tamaño en pantalla dentro del texto, sea cual sea la
                // resolución de origen (ver constantes Ref* arriba) — sin esto, un placeholder de
                // 96 px queda minúsculo (o el arte Xbox de 1248 px queda gigante y recortado) frente
                // al calibrado original.
                float characterScale = h > 0f ? (RefHeight * RefCharacterScale) / h : RefCharacterScale;

                var glyph = new TMP_SpriteGlyph
                {
                    index = glyphIndex,
                    metrics = new GlyphMetrics(w, h, RefBearingXRatio * w, RefBearingYRatio * h, RefAdvanceRatio * w),
                    glyphRect = new GlyphRect(0, 0, (int)w, (int)h),
                    scale = 1f,
                    atlasIndex = 0,
                    sprite = sprite,
                };
                asset.spriteGlyphTable.Add(glyph);

                var character = new TMP_SpriteCharacter(0xFFFE, glyph) { name = kvp.Key, scale = characterScale };
                asset.spriteCharacterTable.Add(character);

                glyphIndex++;
            }

            asset.UpdateLookupTables();
            return asset;
        }

        static void ApplyToDialogueIcons(InputGlyphDeviceFamily family)
        {
            if (_dialogueIcons == null) return;
            if (!_tmpAssetsByFamily.TryGetValue(family, out var generated)) return;

            // El generado va primero: TMP resuelve cada <sprite name="..."> recorriendo la lista de
            // fallback en orden y se queda con la primera coincidencia, así que esto pisa únicamente
            // los 11 nombres de botón que generamos y deja intactos el resto de iconos originales.
            var merged = new List<TMP_SpriteAsset>(_originalFallbacks.Count + 1) { generated };
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
