using System;
using UnityEditor;
using UnityEngine;

namespace Core.InputGlyphs.EditorTools
{
    /// <summary>
    /// Rellena automáticamente los sprites "baked" (InputGlyphFamilySpriteSet_*.asset,
    /// InteractionHintIconSet.asset, TeleportHintIconSet.asset) leyendo los PNG que YA existen en
    /// Assets/Resources/InputGlyphs/&lt;Familia&gt;/&lt;nombre&gt;.png — el mismo arte (real para Xbox,
    /// placeholder para el resto salvo que ya se haya sustituido, ver InputGlyphAssetGeneratorWindow)
    /// que antes cargaba InputGlyphService por Resources.Load en tiempo de ejecución. Con esto no hace
    /// falta arrastrar a mano los 68 sprites de los 4 InputGlyphFamilySpriteSet_*.asset ni repetir los
    /// 8 de los dos icon set de un solo botón (South/Teleport): se leen del mismo arte que ya estaba
    /// en el proyecto, así que el resultado visual es idéntico al que había antes de pasar a
    /// referencias baked.
    ///
    /// Solo rellena campos que estén vacíos (fileID: 0) — si ya se arrastró un sprite a mano en algún
    /// campo (como InteractionHintIconSet.asset, confirmado por el usuario en el punto de guardado) no
    /// lo pisa. Por eso también se puede volver a correr sin miedo después de sustituir un PNG
    /// placeholder por arte final: como el PNG se sobreescribe en la MISMA ruta, conserva el mismo
    /// GUID, así que la referencia baked que ya apunta a él recoge el arte nuevo sola, sin tener que
    /// volver a correr esta herramienta ni tocar el campo a mano.
    /// </summary>
    public static class PopulateFamilySpriteSetsFromResourcesTool
    {
        const string ResourcesRoot = "Assets/Resources/InputGlyphs";
        const string UiRoot = "Assets/_UI";

        [MenuItem("Tools/Input Glyphs/Rellenar sprites baked desde los PNG de Resources")]
        public static void Run()
        {
            int filled = 0, alreadySet = 0, missingPng = 0;

            // 1) Los 4 InputGlyphFamilySpriteSet_<Familia>.asset (12 botones cada uno).
            foreach (InputGlyphDeviceFamily family in Enum.GetValues(typeof(InputGlyphDeviceFamily)))
            {
                var setPath = $"{UiRoot}/InputGlyphFamilySpriteSet_{family}.asset";
                var set = AssetDatabase.LoadAssetAtPath<InputGlyphFamilySpriteSet>(setPath);
                if (set == null)
                {
                    Debug.LogWarning($"[PopulateFamilySpriteSetsFromResourcesTool] No existe {setPath}, se salta.");
                    continue;
                }

                var so = new SerializedObject(set);
                bool changed = false;

                foreach (var buttonName in InputGlyphNames.All)
                {
                    var fieldName = FieldNameForButton(buttonName);
                    var prop = fieldName != null ? so.FindProperty(fieldName) : null;
                    if (prop == null)
                    {
                        Debug.LogWarning($"[PopulateFamilySpriteSetsFromResourcesTool] Campo '{fieldName}' no encontrado en {setPath}.");
                        continue;
                    }

                    if (prop.objectReferenceValue != null)
                    {
                        alreadySet++;
                        continue;
                    }

                    var pngPath = $"{ResourcesRoot}/{family}/{buttonName}.png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                    if (sprite == null)
                    {
                        missingPng++;
                        Debug.LogWarning($"[PopulateFamilySpriteSetsFromResourcesTool] No se encontró sprite en {pngPath}.");
                        continue;
                    }

                    prop.objectReferenceValue = sprite;
                    changed = true;
                    filled++;
                }

                if (changed)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(set);
                }
            }

            // 2) InteractionHintIconSet.asset (South) y TeleportHintIconSet.asset (Teleport) —
            //    mismo tipo de asset, un botón fijo, 4 familias.
            filled += PopulateSingleButtonIconSet($"{UiRoot}/InteractionHintIconSet.asset", InputGlyphNames.South, ref alreadySet, ref missingPng);
            filled += PopulateSingleButtonIconSet($"{UiRoot}/TeleportHintIconSet.asset", InputGlyphNames.Teleport, ref alreadySet, ref missingPng);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var msg = $"Rellenados: {filled}\nYa tenían sprite (sin tocar): {alreadySet}\nPNG no encontrado: {missingPng}";
            Debug.Log($"[PopulateFamilySpriteSetsFromResourcesTool] {msg}");
            EditorUtility.DisplayDialog("Rellenar sprites baked", msg, "OK");
        }

        static int PopulateSingleButtonIconSet(string assetPath, string buttonName, ref int alreadySet, ref int missingPng)
        {
            var set = AssetDatabase.LoadAssetAtPath<InteractionHintIconSet>(assetPath);
            if (set == null)
            {
                Debug.LogWarning($"[PopulateFamilySpriteSetsFromResourcesTool] No existe {assetPath}, se salta.");
                return 0;
            }

            var so = new SerializedObject(set);
            int filled = 0;
            bool changed = false;

            foreach (InputGlyphDeviceFamily family in Enum.GetValues(typeof(InputGlyphDeviceFamily)))
            {
                var fieldName = FieldNameForFamily(family);
                var prop = so.FindProperty(fieldName);
                if (prop == null)
                {
                    Debug.LogWarning($"[PopulateFamilySpriteSetsFromResourcesTool] Campo '{fieldName}' no encontrado en {assetPath}.");
                    continue;
                }

                if (prop.objectReferenceValue != null)
                {
                    alreadySet++;
                    continue;
                }

                var pngPath = $"{ResourcesRoot}/{family}/{buttonName}.png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
                if (sprite == null)
                {
                    missingPng++;
                    Debug.LogWarning($"[PopulateFamilySpriteSetsFromResourcesTool] No se encontró sprite en {pngPath}.");
                    continue;
                }

                prop.objectReferenceValue = sprite;
                changed = true;
                filled++;
            }

            if (changed)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(set);
            }

            return filled;
        }

        static string FieldNameForButton(string buttonName)
        {
            if (buttonName == InputGlyphNames.South) return "south";
            if (buttonName == InputGlyphNames.East) return "east";
            if (buttonName == InputGlyphNames.West) return "west";
            if (buttonName == InputGlyphNames.North) return "north";
            if (buttonName == InputGlyphNames.ShoulderLeft) return "shoulderLeft";
            if (buttonName == InputGlyphNames.ShoulderRight) return "shoulderRight";
            if (buttonName == InputGlyphNames.TriggerLeft) return "triggerLeft";
            if (buttonName == InputGlyphNames.TriggerRight) return "triggerRight";
            if (buttonName == InputGlyphNames.Dpad) return "dpad";
            if (buttonName == InputGlyphNames.Stick) return "stick";
            if (buttonName == InputGlyphNames.Start) return "start";
            if (buttonName == InputGlyphNames.Confirm) return "confirm";
            if (buttonName == InputGlyphNames.Teleport) return "teleport";
            if (buttonName == InputGlyphNames.DpadLeft) return "dpadLeft";
            if (buttonName == InputGlyphNames.DpadRight) return "dpadRight";
            if (buttonName == InputGlyphNames.DpadUp) return "dpadUp";
            if (buttonName == InputGlyphNames.DpadDown) return "dpadDown";
            return null;
        }

        static string FieldNameForFamily(InputGlyphDeviceFamily family)
        {
            switch (family)
            {
                case InputGlyphDeviceFamily.Xbox: return "xbox";
                case InputGlyphDeviceFamily.PlayStation: return "playStation";
                case InputGlyphDeviceFamily.Switch: return "switchConsole";
                case InputGlyphDeviceFamily.KeyboardMouse: return "keyboardMouse";
                default: return null;
            }
        }
    }
}
