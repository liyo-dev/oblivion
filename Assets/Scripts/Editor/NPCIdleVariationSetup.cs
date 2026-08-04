#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Herramienta de configuración de un solo uso: añade el estado "Idle03" (que faltaba) en el
/// Base Layer de los dos Animator Controller que usan los personajes (NPC_NoWeapon para NPCs
/// normales, e Invector@BasicLocomotion para los personajes principales: Liam, Estela, Will),
/// corrige el nombre con espacio de más en los estados de sentarse de NPC_NoWeapon
/// (' SitLow_Loop' / ' SitMedium_Loop') y actualiza todos los prefabs cuyo idleVariationStates
/// apuntaba a nombres de estado que no existen ('Idle02_NoWeapon'/'Idle03_NoWeapon') para que
/// usen los tres idles reales (Idle01, Idle02, Idle03) y roten entre ellos.
///
/// El estado Idle03 se toma del mismo pack (Kevin Iglesias - HumanM@Idle03) que ya usan
/// Idle01/Idle02 en ambos controllers, para mantener consistencia de animación.
///
/// Solo toca prefabs cuyo idleVariationStates sea exactamente el valor roto por defecto,
/// para no pisar ninguna configuración manual que ya hubiera hecho Raúl.
///
/// Menú: El Sendero > NPCs > Setup > Añadir Idle03 y arreglar variaciones de Idle
/// </summary>
public static class NPCIdleVariationSetup
{
    private static readonly string[] ControllerPaths =
    {
        "Assets/Art/Characters/Animator/NPC_NoWeapon.controller",
        "Assets/Plugins/Invector-3rdPersonController_LITE/Animator/Invector@BasicLocomotion.controller"
    };

    private const string Idle03ClipPath = "Assets/Plugins/Kevin Iglesias/Human Animations/Animations/Male/Idles/HumanM@Idle03.fbx";
    private const string Idle03ClipName = "HumanM@Idle03";

    private static readonly string[] BrokenIdleVariationDefault = { "Idle02_NoWeapon", "Idle03_NoWeapon" };
    private static readonly string[] FixedIdleVariationStates = { "Idle01", "Idle02", "Idle03" };

    [MenuItem("El Sendero/NPCs/Setup/Añadir Idle03 y arreglar variaciones de Idle")]
    public static void Run()
    {
        var allLogLines = new List<string>();
        bool anyControllerFailed = false;

        foreach (var controllerPath in ControllerPaths)
        {
            bool ok = FixController(controllerPath, out string controllerLog);
            anyControllerFailed |= !ok;
            allLogLines.Add($"--- {controllerPath} ---");
            allLogLines.Add(controllerLog);
        }

        var prefabLog = FixNpcPrefabs();

        string summary = string.Join("\n", allLogLines) + "\n\n--- Prefabs ---\n" + string.Join("\n", prefabLog);
        Debug.Log($"[NPCIdleVariationSetup] Resultado:\n{summary}");

        EditorUtility.DisplayDialog(
            "Setup de variaciones de Idle",
            (anyControllerFailed ? "⚠ Revisa la Console, hubo un problema con algún controller.\n\n" : "Controllers actualizados correctamente.\n\n") +
            $"{prefabLog.Count} prefab(s) actualizados a [Idle01, Idle02, Idle03].\n" +
            "Detalles completos en la Console.",
            "OK");
    }

    private static bool FixController(string controllerPath, out string log)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            log = $"❌ No se encontró el AnimatorController en '{controllerPath}'.";
            return false;
        }

        var baseLayer = controller.layers.FirstOrDefault(l => l.name == "Base Layer") ?? controller.layers.FirstOrDefault();
        if (baseLayer?.stateMachine == null)
        {
            log = "❌ El controller no tiene Base Layer con StateMachine.";
            return false;
        }

        var rootSm = baseLayer.stateMachine;
        bool dirty = false;
        var logLines = new List<string>();

        // 1) Añadir el estado Idle03 si no existe todavía en ninguna parte del Base Layer
        if (TryFindStateRecursive(rootSm, "Idle03", out _, out _))
        {
            logLines.Add("ℹ Estado 'Idle03' ya existía, no se ha tocado.");
        }
        else if (!TryFindStateRecursive(rootSm, "Idle02", out var parentSm, out var idle02State))
        {
            logLines.Add("❌ No se encontró 'Idle02' en el Base Layer, no se puede colocar Idle03 junto a él. Estado NO añadido.");
        }
        else
        {
            var clip = LoadAnimationClip(Idle03ClipPath, Idle03ClipName);
            if (clip == null)
            {
                logLines.Add($"❌ No se pudo cargar el clip '{Idle03ClipName}' desde '{Idle03ClipPath}'. Estado Idle03 NO añadido.");
            }
            else
            {
                // Posición junto a Idle02 para que sea fácil de encontrar en el grafo (solo estético)
                var idle02Child = parentSm.states.FirstOrDefault(cs => cs.state == idle02State);
                Vector3 position = idle02Child.position + new Vector3(180f, 0f, 0f);

                var newState = parentSm.AddState("Idle03", position);
                newState.motion = clip;
                logLines.Add($"✅ Estado 'Idle03' añadido junto a Idle02 (motion: {clip.name}).");
                dirty = true;
            }
        }

        // 2) Corregir el espacio de más en los estados de sentarse (bug detectado en NPC_NoWeapon)
        if (RenameStateIfExists(rootSm, " SitLow_Loop", "SitLow_Loop"))
        {
            logLines.Add("✅ Renombrado ' SitLow_Loop' -> 'SitLow_Loop'.");
            dirty = true;
        }

        if (RenameStateIfExists(rootSm, " SitMedium_Loop", "SitMedium_Loop"))
        {
            logLines.Add("✅ Renombrado ' SitMedium_Loop' -> 'SitMedium_Loop'.");
            dirty = true;
        }

        if (dirty)
        {
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        log = string.Join("\n", logLines);
        return true;
    }

    /// <summary>
    /// Busca recursivamente (incluyendo sub-state-machines) un estado por nombre dentro de una
    /// state machine. Devuelve la state machine que lo contiene directamente (para poder añadir
    /// estados hermanos junto a él).
    /// </summary>
    private static bool TryFindStateRecursive(AnimatorStateMachine sm, string name, out AnimatorStateMachine owner, out AnimatorState found)
    {
        foreach (var childState in sm.states)
        {
            if (childState.state != null && childState.state.name == name)
            {
                owner = sm;
                found = childState.state;
                return true;
            }
        }

        foreach (var childSm in sm.stateMachines)
        {
            if (childSm.stateMachine != null && TryFindStateRecursive(childSm.stateMachine, name, out owner, out found))
                return true;
        }

        owner = null;
        found = null;
        return false;
    }

    private static bool RenameStateIfExists(AnimatorStateMachine sm, string oldName, string newName)
    {
        if (!TryFindStateRecursive(sm, oldName, out _, out var state))
            return false;

        state.name = newName;
        return true;
    }

    private static AnimationClip LoadAnimationClip(string assetPath, string clipName)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        return assets.OfType<AnimationClip>().FirstOrDefault(c => c.name == clipName);
    }

    /// <summary>
    /// Busca todos los prefabs con NPCSimpleAnimator cuyo idleVariationStates coincida
    /// exactamente con el valor roto por defecto, y lo actualiza a los tres idles reales.
    /// Afecta tanto a NPCs normales (NPC_NoWeapon) como a personajes principales (Invector).
    /// </summary>
    private static List<string> FixNpcPrefabs()
    {
        var changed = new List<string>();
        var guids = AssetDatabase.FindAssets("t:Prefab");

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Comprobación rápida y barata antes de cargar el prefab entero
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (mainAsset == null) continue;
            if (mainAsset.GetComponentInChildren<NPCSimpleAnimator>(true) == null) continue;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool prefabDirty = false;

                foreach (var npcAnimator in root.GetComponentsInChildren<NPCSimpleAnimator>(true))
                {
                    var so = new SerializedObject(npcAnimator);
                    var prop = so.FindProperty("idleVariationStates");
                    if (prop == null || !prop.isArray) continue;

                    if (!ArrayMatches(prop, BrokenIdleVariationDefault)) continue;

                    prop.arraySize = FixedIdleVariationStates.Length;
                    for (int i = 0; i < FixedIdleVariationStates.Length; i++)
                        prop.GetArrayElementAtIndex(i).stringValue = FixedIdleVariationStates[i];

                    so.ApplyModifiedProperties();
                    prefabDirty = true;
                }

                if (prefabDirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed.Add($"✅ {path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }

    private static bool ArrayMatches(SerializedProperty arrayProp, string[] expected)
    {
        if (arrayProp.arraySize != expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (arrayProp.GetArrayElementAtIndex(i).stringValue != expected[i])
                return false;
        }

        return true;
    }
}
#endif
