using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.NPC;
using Game.NPC.Common;
using Game.NPC.Modules;

/// <summary>
/// Escena 16 del GDD ("La Piedra Ancestral"): Will recita el conjuro, la realidad se resquebraja
/// y un Guardián de piedra viva surge para proteger el libro. Raúl va a comprar el modelo real del
/// Guardián — mientras tanto, esta herramienta deja montado TODO lo que no depende de ese modelo:
/// el NPC_Combat_Config, la arena de combate (mismo patrón que BattleArenaDemon1 / la Batalla Final
/// del Mago Oscuro) y un placeholder jugable (PBR_Golem, ya presente en el proyecto como
/// Assets/Prefabs/Enemy/PBR_Golem.prefab) para poder probar el encuentro completo hoy mismo.
///
/// Cuando llegue el modelo real: crea un nuevo prefab con ese modelo + NPCBehaviourManagerV2 +
/// NPCSimpleAnimator (mismo patrón que cualquier NPC de combate del proyecto), y sustituye el hijo
/// "GUARDIAN_PIEDRA_PLACEHOLDER" de "BattleArenaGuardianPiedra" por una instancia de ese prefab
/// nuevo — el NPCCombatConfig, el RoomGoal y el EnemyMarker no cambian, es solo una sustitución de
/// modelo. Vuelve a ejecutar este menú tras el cambio: es idempotente y no crea nada duplicado si
/// ya existe.
///
/// Recompensa (el libro): de momento se deja como un pickup simple sin enganchar al grafo
/// narrativo — el grafo real (MainNarrative_Cap*.asset) no llega todavía a esta escena (TDD.md §10
/// avisa de que tocarlo a ciegas es la forma más fácil de romper algo caro de depurar), así que el
/// enganche real ("al abrir el libro se aprende Hechizo del Tiempo/Resurrección") se deja para
/// cuando Raúl decida cómo conectar esta escena a la narrativa real.
/// </summary>
public static class PiedraAncestralGuardianBuilder
{
    private const string ConfigPath = "Assets/_NPCs/Combat/NPC_Combat_Config_GuardianPiedra.asset";
    private const string GolemPrefabPath = "Assets/Prefabs/Enemy/PBR_Golem.prefab";
    private const string GarraDelPactoPath = "Assets/_SPELLS/Prefabs/GarraDelPacto.prefab";
    private const string HuracanVfxSearchName = "Huracan"; // Huracan.asset es el MagicSpellSO, no el prefab de proyectil — ver nota abajo.
    private const string SelloDelPactoPath = "Assets/_SPELLS/Prefabs/SelloDelPacto.prefab";

    [MenuItem("El Sendero/Escena/Crear Guardián de la Piedra Ancestral")]
    public static void CreateGuardian()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new System.Text.StringBuilder();

        // 1) Combat config -----------------------------------------------------------------
        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(ConfigPath);
        bool configIsNew = config == null;
        if (configIsNew)
        {
            config = ScriptableObject.CreateInstance<NPCCombatConfig>();
            EnsureFolder("Assets/_NPCs/Combat");
            AssetDatabase.CreateAsset(config, ConfigPath);
            log.AppendLine($"+ Creado {ConfigPath}.");
        }
        else
        {
            log.AppendLine($"= {ConfigPath} ya existía, no se pisan valores ya ajustados a mano.");
        }

        if (configIsNew)
        {
            // Valores de partida razonables para un jefe "tanque" de piedra — pesado, lento de
            // maná, pega fuerte en área. Pensados para ajustarse a ojo jugándolo, no un balance fino.
            config.health = 600f;
            config.detectionRange = 14f;
            config.fieldOfView = 220f; // un guardián de piedra no "vigila" con sigilo, vigila TODO el claro
            config.minAttackDistance = 3f;
            config.maxAttackDistance = 10f;
            config.isAggressive = true;
            config.canChaseOutOfBounds = false;

            // Placeholder de hechizos: se reutilizan proyectiles/zonas YA existentes en el proyecto
            // (ninguno pensado originalmente para un Guardián de piedra) solo para que el combate
            // funcione de verdad hoy — retintar/renombrar cuando haya tiempo de arte para esta escena.
            var garra = AssetDatabase.LoadAssetAtPath<GameObject>(GarraDelPactoPath);
            var sello = AssetDatabase.LoadAssetAtPath<GameObject>(SelloDelPactoPath);
            config.spell1Prefab = garra;              // golpe básico de alcance medio
            config.spell1Cooldown = 2.5f;
            config.spell1Damage = 22f;
            config.spell1ManaCost = 15f;
            config.spell1Chance = 0.55f;
            config.spell3Prefab = sello;              // "temblor" — zona de área, encaja con un ser de piedra
            config.spell3Cooldown = 9f;
            config.spell3Damage = 16f; // daño por tick de la zona (ver MagicZoneEffect)
            config.spell3ManaCost = 35f;
            config.spell3Chance = 0.25f;
            config.spell2Chance = 0f; // sin slot 2 de partida — 2 ataques ya dan variedad suficiente para probar

            config.maxMana = 120f;
            config.manaRegenPerSecond = 10f; // lento regenerando, para que no repita el hechizo especial sin parar
            config.postDeathBehavior = PostDeathBehavior.Disappear;
            config.postDefeatAction = PostDefeatAction.None;
            config.sendEventOnDefeat = true;
            config.defeatEventKey = "GUARDIAN_PIEDRA_DEFEATED"; // libre para engancharse a una quest/grafo más adelante
            config.battleMusicId = "Guardian_Piedra"; // dar de alta en AudioGraphProfile cuando haya pista elegida
            config.useTacticalRetreat = false; // un guardián de piedra no "huye" — no encaja con el personaje
            config.difficultyLevel = 0.6f;

            EditorUtility.SetDirty(config);
            log.AppendLine("  Config rellenado con valores de partida (vida 600, 2 ataques reutilizados de otros hechizos).");
            if (garra == null) log.AppendLine("  ⚠️ No se encontró GarraDelPacto.prefab — spell1 quedó vacío.");
            if (sello == null) log.AppendLine("  ⚠️ No se encontró SelloDelPacto.prefab — spell3 quedó vacío.");
        }

        // 2) Contenedor de la escena --------------------------------------------------------
        var root = GameObject.Find("PIEDRA_ANCESTRAL_SETUP");
        if (root == null)
        {
            root = new GameObject("PIEDRA_ANCESTRAL_SETUP");
            Undo.RegisterCreatedObjectUndo(root, "Crear PIEDRA_ANCESTRAL_SETUP");
            log.AppendLine("+ Creado contenedor 'PIEDRA_ANCESTRAL_SETUP' en la escena activa.");
        }

        // 3) Arena (mismo patrón que BattleArenaDemon1 / BattleArenaMagoOscuro) -------------
        var arenaGO = GameObject.Find("BattleArenaGuardianPiedra");
        if (arenaGO == null)
        {
            arenaGO = new GameObject("BattleArenaGuardianPiedra");
            Undo.RegisterCreatedObjectUndo(arenaGO, "Crear BattleArenaGuardianPiedra");
            arenaGO.transform.SetParent(root.transform, false);

            var box = arenaGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(10f, 4f, 10f);
            box.center = new Vector3(0f, 2f, 0f);

            arenaGO.AddComponent<RoomGoal>();
            var arena = arenaGO.AddComponent<BossArenaController>();
            arena.roomGoal = arenaGO.GetComponent<RoomGoal>();
            // bossPrefab se deja vacío A PROPÓSITO: igual que en la Batalla Final del Mago Oscuro,
            // BossArenaController.FindExistingBossInRoom() ya encuentra al Guardián por EnemyMarker
            // dentro de esta misma arena — no hace falta instanciarlo desde un prefab aparte.

            log.AppendLine("+ Creada 'BattleArenaGuardianPiedra' (BoxCollider trigger 10x4x10, RoomGoal, BossArenaController).");
            log.AppendLine("  ⚠️ Tamaño/posición de la arena son un punto de partida — ajusta a la geometría real del claro en el Editor.");
        }
        else
        {
            log.AppendLine("= 'BattleArenaGuardianPiedra' ya existía, sin tocar su BoxCollider/posición.");
        }

        // 4) Placeholder del Guardián (PBR_Golem hasta que Raúl compre el modelo real) ------
        Transform existingGuardian = arenaGO.transform.Find("GUARDIAN_PIEDRA_PLACEHOLDER");
        GameObject guardian = existingGuardian != null ? existingGuardian.gameObject : null;
        if (guardian == null)
        {
            var golemAsset = AssetDatabase.LoadAssetAtPath<GameObject>(GolemPrefabPath);
            if (golemAsset == null)
            {
                log.AppendLine($"⚠️ No se encontró {GolemPrefabPath} — no se pudo crear el placeholder del Guardián. Créalo a mano cuando tengas un modelo.");
            }
            else
            {
                guardian = (GameObject)PrefabUtility.InstantiatePrefab(golemAsset, arenaGO.transform);
                guardian.name = "GUARDIAN_PIEDRA_PLACEHOLDER";
                guardian.transform.localPosition = new Vector3(0f, 0f, 3f);
                Undo.RegisterCreatedObjectUndo(guardian, "Crear GUARDIAN_PIEDRA_PLACEHOLDER");
                log.AppendLine("+ Creado placeholder 'GUARDIAN_PIEDRA_PLACEHOLDER' (instancia de PBR_Golem) — SUSTITUIR cuando llegue el modelo real comprado.");
            }
        }
        else
        {
            log.AppendLine("= El placeholder del Guardián ya existe en la escena, sin tocarlo.");
        }

        // 5) Wiring del NPC (combatConfig + behaviourType + EnemyMarker) --------------------
        if (guardian != null)
        {
            var npcManager = guardian.GetComponent<NPCBehaviourManagerV2>();
            if (npcManager == null)
            {
                log.AppendLine("⚠️ El placeholder no tiene NPCBehaviourManagerV2 — no se pudo wirear el combatConfig. Revisa el prefab.");
            }
            else
            {
                var so = new SerializedObject(npcManager);
                var configProp = so.FindProperty("configuration");
                if (configProp == null)
                {
                    log.AppendLine("⚠️ No se encontró el campo serializado 'configuration' en NPCBehaviourManagerV2 — revisa si el nombre cambió.");
                }
                else
                {
                    var combatConfigProp = configProp.FindPropertyRelative("combatConfig");
                    var behaviourTypeProp = configProp.FindPropertyRelative("behaviourType");
                    if (combatConfigProp != null && combatConfigProp.objectReferenceValue == null)
                        combatConfigProp.objectReferenceValue = config;
                    if (behaviourTypeProp != null)
                        behaviourTypeProp.intValue = (int)(NPCBehaviourType.Ambient | NPCBehaviourType.Combat);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    log.AppendLine("+ combatConfig y behaviourType (Ambient+Combat) wireados en el placeholder.");
                }
            }

            if (guardian.GetComponent<EnemyMarker>() == null)
            {
                guardian.AddComponent<EnemyMarker>();
                log.AppendLine("+ EnemyMarker añadido al placeholder (para que BossArenaController.FindExistingBossInRoom() lo encuentre).");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[PiedraAncestralGuardianBuilder]\n" + log.ToString() +
            "\nPendiente a mano en el Editor: colocar 'PIEDRA_ANCESTRAL_SETUP' en la ubicación real del claro, " +
            "ajustar la arena a la geometría definitiva, y guardar la escena (Ctrl+S). " +
            "El combate ya debería poder probarse hoy mismo con el placeholder de PBR_Golem.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        var name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
