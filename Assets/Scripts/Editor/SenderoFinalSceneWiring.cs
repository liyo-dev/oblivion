using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rellena de un tirón las referencias de Inspector que faltan en Sendero.unity para las escenas
/// 20-22 (Batalla Final del Mago Oscuro, Sacrificio de Will, Epílogo) una vez que ya has colocado
/// a mano los 4 actores, BattleArenaMagoOscuro y los 3 GameObjects de los sequencers — ver
/// checklist-editor-batalla-final-2026-08-30.md en el proyecto de Cowork.
///
/// Qué hace (todo idempotente — se puede volver a ejecutar sin duplicar nada ni pisar ajustes que
/// ya hayas hecho a mano en el Editor: cualquier campo que ya tenga un valor distinto de vacío/0 se
/// deja tal cual):
///   1) Engancha el boss al sistema de sala: añade RoomGoal a "BATTLE ARENA", reparenta
///      _MAGO_OSCURO bajo BattleArenaMagoOscuro y le añade EnemyMarker — sin esto,
///      BossArenaController.FindExistingBossInRoom() nunca lo encuentra (bossPrefab se deja vacío
///      a propósito, ver batalla-final-fase2-huecos-resueltos-2026-08-30.md).
///   2) Reposiciona los 4 actores alrededor de BossSpawn (solo si siguen exactamente en el origen
///      0,0,0 — si ya los has movido a mano, no se tocan). Son posiciones de partida razonables,
///      NO definitivas: ajusta framing/orientación a ojo en el Editor.
///   3) Crea "PortalSendero" (+ "PortalExitPoint") como marcador vacío en BATTLE ARENA si no existe
///      — todavía sin arte real (el altar/portal del Sendero está pendiente de diseño, ver aviso al
///      final del log y la respuesta sobre qué le falta al hub).
///   4) Renombra los 5 puntos de cámara genéricos (heredados de la plantilla — camShotProjectile,
///      CamShotEldran, CamShotWillProfile, camShotTwoShot, camShotWillFinal) de cada sequencer a
///      nombres semánticos (Shot_AltarWide, Shot_MagoCloseup, etc.) y los enlaza en el campo
///      correspondiente.
///   5) Enlaza actores, _cinematicCamera (el CinematicCameraDriver hermano en el mismo GameObject),
///      _actionManager (el PlayerActionManager de Will — Will ES el protagonista/jugador en esta
///      escena, ya estaba enlazado así en MagoOscuroFinalBattleSequencer), los 12 VFX cinemáticos
///      (9 variantes creadas por MagoOscuroCinematicVfxBuilder + 3 reutilizados tal cual) y las
///      señales _signalIn/_signalOut que faltaban en WillSacrificeSequencer/EpilogueSequencer.
///   6) (Pedido por Raúl, 30/08/2026) En MagoOscuroFinalBattleSequencer: _estelaEmotion/_liamEmotion
///      (un único NPCEmotionController cada uno, sin ambigüedad) y _willVisionPrefab/
///      _goodWizardVisionPrefab (Assets/Prefabs/_WILL.prefab y _WILL_ORIGINAL.prefab, por guid, para
///      la visión de "la Voz" dentro de Co_MemoryVision). _willEmotion se deja SIN enlazar a
///      propósito — _WILL.prefab lleva dos NPCEmotionController en el mismo GameObject y no hay
///      forma de saber por código cuál es el activo/visible; el log avisa para que lo arrastres a
///      mano en el Inspector.
///
/// _magoOscuroHealth se deja SIN enlazar a propósito: el propio script ya resuelve el Damageable
/// en tiempo de ejecución vía _magoOscuroActor.GetComponent<Damageable>() (Damageable no existe
/// como componente hasta que NPCBehaviourManagerV2.Awake() lo añade, así que no hay nada que
/// arrastrar en el Editor).
/// </summary>
public static class SenderoFinalSceneWiring
{
    [MenuItem("El Sendero/Escena/Rellenar Referencias de la Batalla Final")]
    public static void WireScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Sendero")
        {
            Debug.LogWarning($"[SenderoFinalSceneWiring] La escena activa es '{scene.name}', no 'Sendero'. Abre Assets/Scenes/Worlds/Sendero.unity y vuelve a ejecutar.");
            return;
        }

        var will = GameObject.Find("_WILL");
        var estela = GameObject.Find("_ESTELA");
        var liam = GameObject.Find("_LIAM");
        var magoOscuro = GameObject.Find("_MAGO_OSCURO");
        var battleArena = GameObject.Find("BattleArenaMagoOscuro");
        var battleArenaRoot = GameObject.Find("BATTLE ARENA");
        var bossSpawn = GameObject.Find("BossSpawn");
        var magoSeq = GameObject.Find("MagoOscuroFinalBattleSequencer");
        var willSeq = GameObject.Find("WillSacrificeSequencer");
        var epilogueSeq = GameObject.Find("EpilogueSequencer");

        var missing = new List<string>();
        void Req(Object o, string label) { if (o == null) missing.Add(label); }
        Req(will, "_WILL"); Req(estela, "_ESTELA"); Req(liam, "_LIAM"); Req(magoOscuro, "_MAGO_OSCURO");
        Req(battleArena, "BattleArenaMagoOscuro"); Req(battleArenaRoot, "BATTLE ARENA"); Req(bossSpawn, "BossSpawn");
        Req(magoSeq, "MagoOscuroFinalBattleSequencer"); Req(willSeq, "WillSacrificeSequencer"); Req(epilogueSeq, "EpilogueSequencer");
        if (missing.Count > 0)
        {
            Debug.LogError("[SenderoFinalSceneWiring] Faltan objetos en la escena, abortando: " + string.Join(", ", missing));
            return;
        }

        // Capturar ANTES de reparentar nada (reparentar cambia la posición local aunque conserve
        // la posición de mundo, así que hay que decidir "¿seguía en el origen?" ahora).
        bool magoWasAtOrigin = magoOscuro.transform.position == Vector3.zero;
        bool willWasAtOrigin = will.transform.position == Vector3.zero;
        bool estelaWasAtOrigin = estela.transform.position == Vector3.zero;
        bool liamWasAtOrigin = liam.transform.position == Vector3.zero;

        var log = new StringBuilder();

        // --- 1) Enganchar el boss al sistema de sala (RoomGoal / EnemyMarker) ---
        var roomGoal = battleArenaRoot.GetComponent<RoomGoal>();
        if (roomGoal == null)
        {
            roomGoal = Undo.AddComponent<RoomGoal>(battleArenaRoot);
            log.AppendLine("+ RoomGoal añadido a 'BATTLE ARENA'.");
        }
        var arenaController = battleArena.GetComponent<BossArenaController>();
        if (arenaController != null)
        {
            var soArena = new SerializedObject(arenaController);
            var pRoomGoal = soArena.FindProperty("roomGoal");
            if (pRoomGoal != null && pRoomGoal.objectReferenceValue == null)
            {
                pRoomGoal.objectReferenceValue = roomGoal;
                log.AppendLine("+ BossArenaController.roomGoal -> RoomGoal (BATTLE ARENA).");
            }
            soArena.ApplyModifiedProperties();
        }

        if (magoOscuro.transform.parent != battleArena.transform)
        {
            Undo.SetTransformParent(magoOscuro.transform, battleArena.transform, "Reparent MagoOscuro bajo BattleArenaMagoOscuro");
            log.AppendLine("+ _MAGO_OSCURO reparentado bajo BattleArenaMagoOscuro (necesario para que FindExistingBossInRoom() lo encuentre).");
        }
        if (magoOscuro.GetComponent<EnemyMarker>() == null)
        {
            Undo.AddComponent<EnemyMarker>(magoOscuro);
            log.AppendLine("+ EnemyMarker añadido a _MAGO_OSCURO.");
        }
        if (magoOscuro.GetComponent<BossHealthBar>() == null)
        {
            // FIX (ronda 16): sin este componente, BossArenaController.SpawnBoss() hace
            // boss.GetComponent<BossHealthBar>() -> null y _activeBossHealthBar?.Show() no hace nada
            // ("no sale la vida" del boss en pantalla). BossHealthBar se auto-construye su UI en
            // Start() a partir del Damageable del propio GameObject, así que no requiere más wiring.
            Undo.AddComponent<BossHealthBar>(magoOscuro);
            log.AppendLine("+ BossHealthBar añadido a _MAGO_OSCURO (barra de vida de boss en pantalla).");
        }

        // --- 2) Reposicionar actores alrededor de BossSpawn (solo si seguían en el origen) ---
        Vector3 arenaCenter = bossSpawn.transform.position;
        Reposition(magoOscuro.transform, arenaCenter, magoWasAtOrigin, log, "_MAGO_OSCURO");
        Reposition(will.transform, arenaCenter + new Vector3(0f, 0f, -5f), willWasAtOrigin, log, "_WILL");
        Reposition(estela.transform, arenaCenter + new Vector3(-3.5f, 0f, -3.5f), estelaWasAtOrigin, log, "_ESTELA");
        Reposition(liam.transform, arenaCenter + new Vector3(3.5f, 0f, -3.5f), liamWasAtOrigin, log, "_LIAM");

        // --- 3) Marcador del portal/altar del Sendero (sin arte todavía) ---
        var portalRoot = GameObject.Find("PortalSendero");
        if (portalRoot == null)
        {
            portalRoot = new GameObject("PortalSendero");
            Undo.RegisterCreatedObjectUndo(portalRoot, "Crear PortalSendero");
            portalRoot.transform.SetParent(battleArenaRoot.transform, false);
            portalRoot.transform.position = arenaCenter + new Vector3(0f, 0f, 3f);

            var exit = new GameObject("PortalExitPoint");
            Undo.RegisterCreatedObjectUndo(exit, "Crear PortalExitPoint");
            exit.transform.SetParent(portalRoot.transform, false);
            exit.transform.localPosition = new Vector3(0f, 0f, -1.5f);

            log.AppendLine("+ PortalSendero creado como marcador vacío (SIN arte todavía) en BATTLE ARENA, con hijo PortalExitPoint.");
        }
        Transform portalExitT = portalRoot.transform.Find("PortalExitPoint");

        // --- 4) Cámaras: renombrar y mapear los 5 placeholders de cada sequencer ---
        var magoShots = RenameShots(magoSeq, new[] {
            ("camShotProjectile", "Shot_AltarWide"),
            ("CamShotEldran", "Shot_MagoCloseup"),
            ("CamShotWillProfile", "Shot_RevelationWill"),
            ("camShotTwoShot", "Shot_Cataclysm"),
            ("camShotWillFinal", "Shot_LiamSacrifice"),
        }, log);
        var willShots = RenameShots(willSeq, new[] {
            ("camShotProjectile", "Shot_MagoDefeat"),
            ("camShotTwoShot", "Shot_WillEstelaClose"),
            ("CamShotEldran", "Shot_PortalCollapse"),
            ("camShotWillFinal", "Shot_WillAlone"),
        }, log);
        var epiShots = RenameShots(epilogueSeq, new[] {
            ("CamShotEldran", "Shot_LiamRevive"),
            ("CamShotWillProfile", "Shot_EstelaExplains"),
            ("camShotWillFinal", "Shot_WillSpirit"),
            ("camShotProjectile", "Shot_FarewellWide"),
        }, log);

        // --- 5) Wiring de cada sequencer ---
        var playerActionManager = will.GetComponentInChildren<PlayerActionManager>(true);
        WireBase(magoSeq, playerActionManager);
        WireBase(willSeq, playerActionManager);
        WireBase(epilogueSeq, playerActionManager);

        {
            var comp = magoSeq.GetComponent<MagoOscuroFinalBattleSequencer>();
            var so = new SerializedObject(comp);
            SetObj(so, "_willActor", will.transform);
            SetObj(so, "_estelaActor", estela.transform);
            SetObj(so, "_liamActor", liam.transform);
            SetObj(so, "_magoOscuroActor", magoOscuro.transform);
            SetShot(so, "_shotAltarWide", magoShots, "Shot_AltarWide");
            SetShot(so, "_shotMagoCloseup", magoShots, "Shot_MagoCloseup");
            SetShot(so, "_shotRevelationWill", magoShots, "Shot_RevelationWill");
            SetShot(so, "_shotCataclysm", magoShots, "Shot_Cataclysm");
            SetShot(so, "_shotLiamSacrifice", magoShots, "Shot_LiamSacrifice");
            SetObj(so, "_appearanceVfx", LoadVfx("VFX_MagoOscuro_Aparicion"));
            SetObj(so, "_willAwakenedAuraVfx", LoadByGuid("bbc5b80cac40a1e4d9abc000bcbdd80e"));
            SetObj(so, "_cataclysmSweepVfx", LoadVfx("VFX_MagoOscuro_CataclismoBarrido"));
            SetObj(so, "_rewindVfx", LoadVfx("VFX_Rebobinado"));
            SetObj(so, "_criticalCounterSpellVfx", LoadByGuid("98c4704d0fd7211449bcf5c451095a60"));
            SetObj(so, "_betrayalStrikeVfx", LoadVfx("VFX_GolpeTraicion"));

            // Caras (pedido por Raúl, 30/08/2026) — _ESTELA y _LIAM solo llevan un
            // NPCEmotionController cada uno, se enlazan solos sin ambigüedad.
            SetObj(so, "_estelaEmotion", estela.GetComponentInChildren<NPCEmotionController>(true));
            SetObj(so, "_liamEmotion", liam.GetComponentInChildren<NPCEmotionController>(true));
            // _willEmotion NO se autowirea: _WILL.prefab lleva DOS NPCEmotionController en el
            // mismo GameObject (confirmado por guid) y no hay forma de saber por código cuál es
            // el activo/visible — arrástralo a mano en el Inspector.
            var willEmotionProp = so.FindProperty("_willEmotion");
            if (willEmotionProp != null && willEmotionProp.objectReferenceValue == null)
                log.AppendLine("! MagoOscuroFinalBattleSequencer._willEmotion: _WILL lleva 2 NPCEmotionController — arrástralo a mano en el Inspector (el que corresponda al outfit visible).");

            // Visión de la Voz (pedido por Raúl, 30/08/2026) — copias de Will y del mago de la
            // leyenda para la visión (Co_MemoryVision), NO los actores reales de la arena.
            SetObj(so, "_willVisionPrefab", LoadByGuid("392e26f6a7263c241a8673d723f24f9a")); // Assets/Prefabs/_WILL.prefab
            SetObj(so, "_goodWizardVisionPrefab", LoadByGuid("0697f276b63523d4cad83528e85e3df2")); // Assets/Prefabs/_WILL_ORIGINAL.prefab

            so.ApplyModifiedProperties();
            log.AppendLine("= MagoOscuroFinalBattleSequencer: actores, shots, VFX, caras y visión enlazados.");
        }
        {
            var comp = willSeq.GetComponent<WillSacrificeSequencer>();
            var so = new SerializedObject(comp);
            SetStrIfEmpty(so, "_signalIn", "SENDERO_SACRIFICIO_START", log, "WillSacrificeSequencer._signalIn");
            SetStrIfEmpty(so, "_signalOut", "SENDERO_EPILOGO_START", log, "WillSacrificeSequencer._signalOut");
            SetObj(so, "_willActor", will.transform);
            SetObj(so, "_estelaActor", estela.transform);
            SetObj(so, "_magoOscuroActor", magoOscuro.transform);
            SetObj(so, "_portalTransform", portalRoot.transform);
            SetObj(so, "_portalExitPoint", portalExitT);
            SetShot(so, "_shotMagoDefeat", willShots, "Shot_MagoDefeat");
            SetShot(so, "_shotWillEstelaClose", willShots, "Shot_WillEstelaClose");
            SetShot(so, "_shotPortalCollapse", willShots, "Shot_PortalCollapse");
            SetShot(so, "_shotWillAlone", willShots, "Shot_WillAlone");
            SetObj(so, "_magoDefeatVfx", LoadVfx("VFX_MagoOscuro_Derrota"));
            SetObj(so, "_collapseAmbientVfx", LoadVfx("VFX_ColapsoAmbiental"));
            SetObj(so, "_portalVfx", LoadVfx("VFX_PortalSendero_Colapso"));
            SetObj(so, "_resurrectionVfx", LoadVfx("VFX_SacrificioLiam"));
            SetObj(so, "_senderoDestructionVfx", LoadVfx("VFX_SenderoDestruccion"));
            so.ApplyModifiedProperties();
            log.AppendLine("= WillSacrificeSequencer: señales, actores, portal, shots y VFX enlazados.");
        }
        {
            var comp = epilogueSeq.GetComponent<EpilogueSequencer>();
            var so = new SerializedObject(comp);
            SetStrIfEmpty(so, "_signalIn", "SENDERO_EPILOGO_START", log, "EpilogueSequencer._signalIn");
            SetObj(so, "_liamActor", liam.transform);
            SetObj(so, "_estelaActor", estela.transform);
            SetObj(so, "_willActor", will.transform);
            SetShot(so, "_shotLiamRevive", epiShots, "Shot_LiamRevive");
            SetShot(so, "_shotEstelaExplains", epiShots, "Shot_EstelaExplains");
            SetShot(so, "_shotWillSpirit", epiShots, "Shot_WillSpirit");
            SetShot(so, "_shotFarewellWide", epiShots, "Shot_FarewellWide");
            SetObj(so, "_willSpiritVfx", LoadByGuid("354b6251e950209409c75ab984acf000"));
            so.ApplyModifiedProperties();
            log.AppendLine("= EpilogueSequencer: señales, actores, shots y VFX enlazados.");
        }

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log("[SenderoFinalSceneWiring] Wiring completo:\n" + log +
            "\nPENDIENTE A MANO EN EL EDITOR:\n" +
            "1) Las posiciones de los 4 actores y los puntos de cámara son un punto de partida " +
            "razonable alrededor de BossSpawn, NO definitivo — ajusta framing/orientación a ojo.\n" +
            "2) PortalSendero es un GameObject vacío sin arte todavía (el altar/portal real del " +
            "Sendero está pendiente de diseño).\n" +
            "3) Si no lo has hecho ya: retinta los VFX marcados 'retintar' en el log de " +
            "MagoOscuroCinematicVfxBuilder.\n" +
            "4) Guarda la escena (Ctrl+S) para persistir todos estos cambios.");
    }

    static void Reposition(Transform t, Vector3 target, bool wasAtOrigin, StringBuilder log, string label)
    {
        if (wasAtOrigin)
        {
            Undo.RecordObject(t, "Reposition " + label);
            t.position = target;
            log.AppendLine($"+ {label} reposicionado a {target} (estaba en el origen).");
        }
        else
        {
            log.AppendLine($"= {label} ya tiene una posición distinta de (0,0,0) — no se toca.");
        }
    }

    static Dictionary<string, Transform> RenameShots(GameObject seqGO, (string oldName, string newName)[] map, StringBuilder log)
    {
        var result = new Dictionary<string, Transform>();
        var anchor = seqGO.transform.Find("CamAnchor");
        if (anchor == null)
        {
            Debug.LogWarning($"[SenderoFinalSceneWiring] {seqGO.name}: no se encontró 'CamAnchor' hijo — no se pueden mapear cámaras.");
            return result;
        }
        foreach (var (oldName, newName) in map)
        {
            var t = anchor.Find(oldName);
            if (t == null)
            {
                t = anchor.Find(newName); // ya renombrado en una ejecución anterior
                if (t == null)
                {
                    Debug.LogWarning($"[SenderoFinalSceneWiring] {seqGO.name}/CamAnchor: no se encontró '{oldName}' ni '{newName}'.");
                    continue;
                }
            }
            else if (t.name != newName)
            {
                Undo.RecordObject(t.gameObject, "Rename cam shot");
                t.gameObject.name = newName;
            }
            result[newName] = t;
        }
        log.AppendLine($"= {seqGO.name}: {result.Count} puntos de cámara mapeados/renombrados bajo CamAnchor.");
        return result;
    }

    static void WireBase(GameObject seqGO, PlayerActionManager pam)
    {
        var driver = seqGO.GetComponent<CinematicCameraDriver>();
        var baseComp = seqGO.GetComponent<CinematicSequencerBase>();
        if (baseComp == null) return;
        var so = new SerializedObject(baseComp);
        if (driver != null) SetObj(so, "_cinematicCamera", driver);
        var amProp = so.FindProperty("_actionManager");
        if (amProp != null && amProp.objectReferenceValue == null && pam != null)
            amProp.objectReferenceValue = pam;
        so.ApplyModifiedProperties();
    }

    static void SetShot(SerializedObject so, string prop, Dictionary<string, Transform> shots, string key)
    {
        if (shots.TryGetValue(key, out var t))
            SetObj(so, prop, t);
    }

    static void SetObj(SerializedObject so, string prop, Object value)
    {
        if (value == null) return;
        var p = so.FindProperty(prop);
        if (p == null)
        {
            Debug.LogWarning($"[SenderoFinalSceneWiring] Campo '{prop}' no encontrado en {so.targetObject.GetType().Name}.");
            return;
        }
        if (p.objectReferenceValue == null)
            p.objectReferenceValue = value;
    }

    static void SetStrIfEmpty(SerializedObject so, string prop, string value, StringBuilder log, string label)
    {
        var p = so.FindProperty(prop);
        if (p == null) return;
        if (string.IsNullOrEmpty(p.stringValue))
        {
            p.stringValue = value;
            log.AppendLine($"+ {label} = \"{value}\"");
        }
    }

    static GameObject LoadVfx(string name)
    {
        var path = $"Assets/_VFX/BatallaFinal/{name}.prefab";
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null)
            Debug.LogWarning($"[SenderoFinalSceneWiring] No se encontró {path} — ¿has ejecutado antes 'El Sendero/VFX/Crear VFX Cinematicos de la Batalla Final'?");
        return go;
    }

    static GameObject LoadByGuid(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"[SenderoFinalSceneWiring] No se encontró ningún asset con guid {guid}.");
            return null;
        }
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }
}
