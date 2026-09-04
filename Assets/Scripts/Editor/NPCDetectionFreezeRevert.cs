using UnityEditor;
using UnityEngine;
using Game.NPC;
using Game.NPC.Common;
using Game.NPC.Modules;

/// <summary>
/// Deshace el cableado narrativo que aplicaba NPCDetectionFreezeBuilder.cs (ya eliminado) a
/// Boy_Pirate, Mago #1/2/3 y Lety.
///
/// POR QUÉ: al probarlo en juego con Boy_Pirate, Raúl reportó que el jugador se quedaba
/// congelado sin poder moverse y el diálogo de combate nunca llegaba a arrancar - "como si
/// hubiera dos sistemas luchando". Diagnóstico confirmado: para un NPC que YA tenía su
/// NPCCombatConfig asignado desde el principio (Boy_Pirate, Mago #1/2/3, Lety - a diferencia de
/// Erika, cuyo combatConfig empezaba vacío), su propia IA de combate (IdleState.CheckPlayerDetection,
/// FOV+línea de visión, ~5m) YA estaba corriendo de forma independiente. Añadir encima
/// NPCInteractiveNarrativeExecutor creaba una SEGUNDA detección en paralelo (DetectPlayerRoutine,
/// solo distancia, sin FOV/LOS, 10m) que competía por el mismo NPC: al detectar, el módulo
/// narrativo pone Context.IsInteracting=true, y IdleState.CheckTransitions() se queda parado
/// mientras tanto ("if (context.IsInteracting) return null; // Quedarse en Idle mientras habla") -
/// así que la propia IA de combate no podía completar su transición a AlertState (de donde sale
/// el diálogo) mientras el otro sistema seguía haciendo lo suyo. Origen del bloqueo sin diálogo.
///
/// SOLUCIÓN REAL (sin este revert, ver el resto de cambios de esta sesión): el freeze se ha
/// movido al ÚNICO sitio donde ya se decide "el jugador ha sido detectado" para estos NPCs -
/// AlertState.cs (NPCs en solitario) y NPCCombatTeam.Co_DetectAndEngage (Lety/Vicky como equipo) -
/// gobernado por el nuevo NPCCombatConfig.freezePlayerOnAlert (true por defecto). Cero sistemas
/// en paralelo, cero prefabs que tocar: funciona automáticamente para CUALQUIER NPC con
/// NPCCombatConfig asignado, presente o futuro.
///
/// Erika NO se toca aquí: a ella solo se le añadió el NPCInteractiveNarrativeExecutor que le
/// faltaba (su propio config ya existía) - su combatConfig seguía vacío en el momento de tocarla,
/// así que no había IA de combate propia corriendo en paralelo. No hay nada que revertir ahí.
///
/// Idempotente: se puede volver a ejecutar sin error aunque ya esté todo revertido.
/// </summary>
public static class NPCDetectionFreezeRevert
{
    private struct SoloEntry
    {
        public string Name;
        public string PrefabPath;
        public string NarrativeConfigPath;
    }

    private static readonly SoloEntry[] SoloEntries =
    {
        new SoloEntry { Name = "Boy Pirate", PrefabPath = "Assets/_NPCs/Combat/Boy_Pirate.prefab", NarrativeConfigPath = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_BoyPirate.asset" },
        new SoloEntry { Name = "Mago #1",    PrefabPath = "Assets/_NPCs/Combat/Mago #1.prefab",    NarrativeConfigPath = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_Mago#1.asset" },
        new SoloEntry { Name = "Mago #2",    PrefabPath = "Assets/_NPCs/Combat/Mago #2.prefab",    NarrativeConfigPath = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_Mago#2.asset" },
        new SoloEntry { Name = "Mago #3",    PrefabPath = "Assets/_NPCs/Combat/Mago #3.prefab",    NarrativeConfigPath = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_Mago#3.asset" },
    };

    private const string LetyPrefabPath = "Assets/_NPCs/Combat/Lety.prefab";
    private const string LetyVickyNarrativeConfigPath = "Assets/_NPCs/Narrative/NPC_InteractiveNarrative_Config_Lety_Vicky.asset";

    [MenuItem("El Sendero/NPCs/Revertir Cableado Narrativo (Boy Pirate, Magos, Lety)")]
    public static void RevertAll()
    {
        foreach (var entry in SoloEntries)
        {
            RevertSolo(entry);
        }

        RevertLety();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[NPCDetectionFreezeRevert] ✅ Revertido. El freeze al detectar ahora vive directamente en " +
            "AlertState.cs / NPCCombatTeam.cs (NPCCombatConfig.freezePlayerOnAlert) - no necesita ningún " +
            "módulo narrativo ni prefab tocado. Prueba de nuevo con Boy_Pirate.");
    }

    private static void RevertSolo(SoloEntry entry)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(entry.PrefabPath);
        bool changed = false;
        try
        {
            var manager = contents.GetComponent<NPCBehaviourManagerV2>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                var configurationProp   = so.FindProperty("configuration");
                var behaviourTypeProp   = configurationProp?.FindPropertyRelative("behaviourType");
                var narrativeConfigProp = configurationProp?.FindPropertyRelative("interactiveNarrativeConfig");

                if (behaviourTypeProp != null && narrativeConfigProp != null)
                {
                    if ((behaviourTypeProp.intValue & (int)NPCBehaviourType.InteractiveNarrative) != 0
                        || narrativeConfigProp.objectReferenceValue != null)
                    {
                        behaviourTypeProp.intValue &= ~(int)NPCBehaviourType.InteractiveNarrative;
                        narrativeConfigProp.objectReferenceValue = null;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                }
            }

            var executor = contents.GetComponent<NPCInteractiveNarrativeExecutor>();
            if (executor != null)
            {
                Object.DestroyImmediate(executor, true);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(contents, entry.PrefabPath);
                Debug.Log($"[NPCDetectionFreezeRevert] {entry.Name}: revertido en {entry.PrefabPath}.");
            }
            else
            {
                Debug.Log($"[NPCDetectionFreezeRevert] {entry.Name}: ya estaba limpio, nada que revertir.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        if (AssetDatabase.LoadAssetAtPath<NPCInteractiveNarrativeConfig>(entry.NarrativeConfigPath) != null)
        {
            AssetDatabase.DeleteAsset(entry.NarrativeConfigPath);
            Debug.Log($"[NPCDetectionFreezeRevert] {entry.Name}: borrado {entry.NarrativeConfigPath} (ya no se usa).");
        }
    }

    private static void RevertLety()
    {
        // Solo se quita lo añadido ESTA sesión: el componente y el freeze del config compartido.
        // El flag InteractiveNarrative y la referencia al config ya estaban puestos de una sesión
        // anterior a esta - se dejan tal y como estaban, no son cosa nuestra.
        GameObject contents = PrefabUtility.LoadPrefabContents(LetyPrefabPath);
        try
        {
            var executor = contents.GetComponent<NPCInteractiveNarrativeExecutor>();
            if (executor != null)
            {
                Object.DestroyImmediate(executor, true);
                PrefabUtility.SaveAsPrefabAsset(contents, LetyPrefabPath);
                Debug.Log("[NPCDetectionFreezeRevert] Lety: quitado el NPCInteractiveNarrativeExecutor añadido esta sesión.");
            }
            else
            {
                Debug.Log("[NPCDetectionFreezeRevert] Lety: ya estaba sin el componente, nada que revertir ahí.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        var config = AssetDatabase.LoadAssetAtPath<NPCInteractiveNarrativeConfig>(LetyVickyNarrativeConfigPath);
        if (config != null && config.conditionalNarratives != null)
        {
            bool changed = false;
            foreach (var narrative in config.conditionalNarratives)
            {
                if (narrative.freezePlayerOnDetection)
                {
                    narrative.freezePlayerOnDetection = false;
                    changed = true;
                }
            }
            if (changed)
            {
                EditorUtility.SetDirty(config);
                Debug.Log("[NPCDetectionFreezeRevert] Lety/Vicky: freezePlayerOnDetection revertido a false en " + LetyVickyNarrativeConfigPath + ".");
            }
        }
    }
}
