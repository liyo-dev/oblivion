using UnityEditor;
using UnityEngine;
using Game.NPC.Modules;
using Game.NPC;
using Game.NPC.Common;

/// <summary>
/// Herramienta de Editor para crear los dos hechizos de combate del Mago Oscuro (Fase 1 de la
/// Batalla Final, escena 20 del GDD — ver guion-tecnico-batalla-final-2026-08-30.md en el
/// proyecto de Cowork), siguiendo el mismo patrón de herramienta de Editor con [MenuItem] que
/// LiamSpellBuilder.cs (Garra del Pacto) y LiamZoneSpellBuilder.cs (Sello del Pacto): un
/// MagicSpellSO no se puede editar internamente a mano de forma fiable porque MagicProjectile
/// exige un Collider ya presente en el prefab y los fileIDs internos de un .prefab no se pueden
/// inventar por YAML.
///
/// Deliberadamente NO se crea aquí el "ataque cataclísmico" de la Fase 2 (el que barre todo el
/// escenario y dispara la mecánica del Hechizo Prohibido del Tiempo): ese ataque es un evento
/// scriptado y garantizado por umbral de vida, no un hechizo más del pool aleatorio de
/// NPCCombatConfig — vive directamente en MagoOscuroFinalBattleSequencer.cs, con sus propios
/// campos serializados de VFX/cámara/tutorial (mismo patrón que magoOscuroLoadVfx en
/// PrologueDreamSequencer.cs, no el sistema de SpellId/MagicSpellSO).
///
/// VFX reutilizado del proyecto, sin crear ninguno nuevo:
///   - "Golpe del Sendero Corrompido" (proyectil): mismo visual base vfx_Projectile_01 de
///     GabrielAguiarProductions/FreeQuickEffectsVol1 que ya usa Garra del Pacto de Liam — se
///     retinta a mano en el Editor hacia una paleta violeta oscuro/negro (ver "Hallazgo" en el
///     guion técnico: los VFX de Will y del Mago Oscuro en el prólogo nunca se diferenciaron;
///     esta es la primera vez que el juego tiene un VFX de "magia oscura" real y distinto).
///   - "Grieta del Sendero" (zona): mismo visual "AoE Poison" de Matthew Guz que ya usa Sello del
///     Pacto de Liam (prefab distinto, mismo asset de origen) — vetted como limpio en este
///     proyecto (a diferencia de "AoE Magic", que arrastra el punto de guardado del Bosque, ver
///     el comentario de cabecera de LiamZoneSpellBuilder.cs). Temáticamente encaja mejor que
///     crear un VFX nuevo: el propio Mago Oscuro es quien corrompió el Sendero.
///
/// Idempotente: se puede volver a ejecutar sin duplicar nada.
///
/// Pendiente a mano en el Editor tras ejecutar el menú (no se puede hacer desde aquí):
///   1) Retintar ambos VFX hacia una paleta violeta oscuro/negro coherente entre sí y distinta de
///      la de Liam (violeta más "vivo/pacto") y de la de Will (dorado/blanco) — instancia propia
///      del material en cada prefab, nunca el material compartido del pack.
///   2) Asignar icono en "attackIcon" de ambos assets (no hay ninguno generado).
///   3) Dar de alta las claves de audio "MagoOscuroGolpe" y "MagoOscuroGrieta" en
///      AudioService/AudioGraphProfile — castSFXKey ya apunta a esas claves.
///   4) Asignar ambos prefabs (no los MagicSpellSO) en NPC_Combat_Config_MagoOscuro.asset →
///      spell1Prefab / spell2Prefab (ya están precargados por guid en el asset .asset creado
///      junto con esta herramienta, pero conviene confirmar en el Inspector tras generarlos).
///   5) Probarlo en juego: radio/daño/cooldown de "Grieta del Sendero" son valores de partida
///      razonados para un jefe final, no ajuste fino.
/// </summary>
public static class MagoOscuroSpellBuilder
{
    private const string PrefabFolder = "Assets/_SPELLS/Prefabs";
    private const string SpellLibraryPath = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";

    // ── Golpe del Sendero Corrompido (proyectil) ────────────────────────────────
    private const string GolpeSpellAssetPath = "Assets/_SPELLS/MagoOscuroGolpe.asset";
    private const string GolpePrefabPath     = PrefabFolder + "/MagoOscuroGolpe.prefab";
    // vfx_Projectile_01 — GabrielAguiarProductions/FreeQuickEffectsVol1 (mismo visual base que
    // Garra del Pacto de Liam; se retinta a mano, ver punto 1 del pendiente).
    private const string GolpeVisualPrefabGuid = "bc142210df3ec4545a4e3e1f21e00da7";

    // ── Grieta del Sendero (zona) ────────────────────────────────────────────────
    private const string GrietaSpellAssetPath = "Assets/_SPELLS/MagoOscuroGrieta.asset";
    private const string GrietaPrefabPath     = PrefabFolder + "/MagoOscuroGrieta.prefab";
    // "AoE Poison" — Matthew Guz/Spell Area of Effect FREE (mismo visual base que Sello del Pacto
    // de Liam; se retinta a mano, ver punto 1 del pendiente). NO usar el guid de "AoE Magic" —
    // ver la nota de corrección en LiamZoneSpellBuilder.cs: ese archivo concreto es en realidad
    // el punto de guardado del Bosque en este proyecto, no un VFX limpio.
    private const string GrietaVisualPrefabGuid = "bd67489a2df67ba4ba4c3e7b9c4e0e8c";

    // Mismo "sparkle" genérico de casteo que ya usan todos los hechizos del elenco
    // (BolaPrisma/CorazonEstelar/LlamaAstral/Tornado/GarraDelPacto/SelloDelPacto).
    private const string SpawnVfxGuid   = "895c6d094b6b213418cddcfb520298e9";
    private const string ImpactVfxGuid  = "67a684e320da6e7439421a07e3fa265c";
    private const string DespawnVfxGuid = "dcd90c4976197424b9958a7c54b6bb8c";

    [MenuItem("El Sendero/Magia/Crear Hechizos del Mago Oscuro (Batalla Final)")]
    public static void CreateMagoOscuroSpells()
    {
        var golpePrefab = CreateOrRepairGolpePrefab();
        var golpeSpell  = CreateOrUpdateGolpeSpellAsset(golpePrefab);
        AddToSpellLibrary(golpeSpell);

        var grietaPrefab = CreateOrRepairGrietaPrefab();
        var grietaSpell  = CreateOrUpdateGrietaSpellAsset(grietaPrefab);
        AddToSpellLibrary(grietaSpell);

        CreateOrUpdateCombatConfig(golpePrefab, grietaPrefab);
        WireCombatConfigIntoMagoOscuroPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[MagoOscuroSpellBuilder] Hechizos del Mago Oscuro listos: " + GolpeSpellAssetPath +
            " y " + GrietaSpellAssetPath + ". " + MagoOscuroPrefabPath + " ya tiene combatConfig y el " +
            "flag Combat wireados (Damageable/NPCCombatLifecycleHandler/Targetable/NPCHealthBarSpawner " +
            "se añaden solos en tiempo de ejecución, no hace falta tocarlos a mano). Pendiente a mano " +
            "en el Editor: 1) retintar ambos VFX hacia violeta oscuro/negro (instancia propia, no el " +
            "material compartido), 2) asignar 'attackIcon' en los dos, 3) dar de alta " +
            "'MagoOscuroGolpe'/'MagoOscuroGrieta' en AudioService/AudioGraphProfile, 4) probar y " +
            "ajustar balance.");
    }

    // ── Golpe del Sendero Corrompido ─────────────────────────────────────────────

    private static GameObject CreateOrRepairGolpePrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GolpePrefabPath);
        if (existing != null)
        {
            Debug.Log("[MagoOscuroSpellBuilder] El prefab ya existía, no se duplica: " + GolpePrefabPath);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Prefabs");
        }

        var visualPath = AssetDatabase.GUIDToAssetPath(GolpeVisualPrefabGuid);
        var visualPrefab = string.IsNullOrEmpty(visualPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);

        var root = new GameObject("Proj_MagoOscuroGolpe");
        var collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.4f; // ligeramente mayor que Garra del Pacto (0.35) — es un jefe final
        root.AddComponent<MagicProjectile>();

        if (visualPrefab != null)
        {
            var visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning(
                "[MagoOscuroSpellBuilder] No se encontró el prefab visual base (guid " + GolpeVisualPrefabGuid +
                "). El proyectil se crea sin visual — asigna uno a mano dentro de " + GolpePrefabPath + ".");
        }

        PrefabUtility.SaveAsPrefabAsset(root, GolpePrefabPath);
        Object.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(GolpePrefabPath);
    }

    private static MagicSpellSO CreateOrUpdateGolpeSpellAsset(GameObject prefab)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(GolpeSpellAssetPath);
        bool isNew = spell == null;
        if (isNew) spell = ScriptableObject.CreateInstance<MagicSpellSO>();

        spell.spellId       = SpellId.MagoOscuroGolpe;
        spell.displayNameId = "SPELL_MAGO_OSCURO_GOLPE_NAME";
        spell.displayName   = "Golpe del Sendero Corrompido";
        spell.kind          = MagicKind.Projectile;
        spell.element       = MagicElement.Dark;
        spell.prefab        = prefab;

        spell.castDelaySeconds = 0.4f; // un poco más rápido que los hechizos del jugador — jefe final
        spell.initialSpeed     = 17f;
        spell.maxRange         = 45f;
        spell.lifeTime         = 3f;
        spell.damage            = 25f; // boss-tier: NPC_Combat_Config_Mago#2 (enemigo normal) usa 10-35 en sus 3 slots
        spell.aoeRadius         = 0f;
        spell.knockbackForce    = 14f;
        spell.forwardOffset     = 0.4f;
        spell.flattenDirection  = true;

        spell.manaCost = 20f;
        spell.cooldown = 2.2f;

        spell.spawnVFX    = LoadByGuid<GameObject>(SpawnVfxGuid);
        spell.impactVFX   = LoadByGuid<GameObject>(ImpactVfxGuid);
        spell.despawnVFX  = LoadByGuid<GameObject>(DespawnVfxGuid);
        spell.vfxLifetime = 3f;

        spell.castSFXKey   = "MagoOscuroGolpe";
        spell.impactSFXKey = "Impact1"; // mismo SFX genérico de impacto que ya usa Garra del Pacto
        spell.slotType      = SpellSlotType.Any;

        if (isNew) AssetDatabase.CreateAsset(spell, GolpeSpellAssetPath);
        else EditorUtility.SetDirty(spell);

        return spell;
    }

    // ── Grieta del Sendero ────────────────────────────────────────────────────────

    private static GameObject CreateOrRepairGrietaPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(GrietaPrefabPath);
        if (existing != null)
        {
            RepairExistingGrietaPrefab(existing);
            return AssetDatabase.LoadAssetAtPath<GameObject>(GrietaPrefabPath);
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Prefabs");
        }

        var visualPath = AssetDatabase.GUIDToAssetPath(GrietaVisualPrefabGuid);
        var visualPrefab = string.IsNullOrEmpty(visualPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);

        // Igual que Sello del Pacto: MagicZoneEffect usa Physics.OverlapSphereNonAlloc, no
        // necesita Collider propio.
        var root = new GameObject("MagoOscuroGrieta");
        root.AddComponent<MagicZoneEffect>();

        if (visualPrefab != null)
        {
            var visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            visualInstance.transform.localPosition = Vector3.zero;
            visualInstance.transform.localRotation = Quaternion.identity;
            ForceParticleSystemsToLoop(visualInstance);
        }
        else
        {
            Debug.LogWarning(
                "[MagoOscuroSpellBuilder] No se encontró el prefab visual base (guid " + GrietaVisualPrefabGuid +
                "). La zona se crea sin visual — asigna uno a mano dentro de " + GrietaPrefabPath + ".");
        }

        PrefabUtility.SaveAsPrefabAsset(root, GrietaPrefabPath);
        Object.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(GrietaPrefabPath);
    }

    private static void RepairExistingGrietaPrefab(GameObject existing)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(GrietaPrefabPath);
        bool changed = false;

        if (contents.GetComponent<MagicZoneEffect>() == null)
        {
            contents.AddComponent<MagicZoneEffect>();
            changed = true;
        }
        if (ForceParticleSystemsToLoop(contents)) changed = true;

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(contents, GrietaPrefabPath);
            Debug.Log("[MagoOscuroSpellBuilder] Prefab existente reparado en el sitio: " + GrietaPrefabPath);
        }
        else
        {
            Debug.Log("[MagoOscuroSpellBuilder] El prefab ya existía y no hacía falta reparar nada: " + GrietaPrefabPath);
        }

        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static bool ForceParticleSystemsToLoop(GameObject visualRoot)
    {
        bool changedAny = false;
        var systems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            if (!main.loop) { main.loop = true; changedAny = true; }
        }
        return changedAny;
    }

    private static MagicSpellSO CreateOrUpdateGrietaSpellAsset(GameObject prefab)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(GrietaSpellAssetPath);
        bool isNew = spell == null;
        if (isNew) spell = ScriptableObject.CreateInstance<MagicSpellSO>();

        spell.spellId       = SpellId.MagoOscuroGrieta;
        spell.displayNameId = "SPELL_MAGO_OSCURO_GRIETA_NAME";
        spell.displayName   = "Grieta del Sendero";
        spell.kind          = MagicKind.Zone;
        spell.element       = MagicElement.Dark;
        spell.prefab        = prefab;

        spell.castDelaySeconds = 0.5f;

        spell.damage          = 15f; // daño POR TICK — algo más que Sello del Pacto (12), jefe final
        spell.knockbackForce  = 0f;  // igual que Sello del Pacto: es una zona de corrupción, no un empujón
        spell.destroyOnHit    = true;

        spell.zoneRadius       = 5.5f; // arena de jefe más grande que un combate normal de NPC
        spell.zoneDuration     = 6f;
        spell.zoneTickInterval = 0.5f; // 12 ticks x 15 daño = hasta 180 si el jugador se queda dentro toda la duración
        spell.zoneRange        = 10f;
        spell.zoneSnapToTarget = true;
        spell.zoneGroundLayers = ~0;
        spell.zoneGroundOffset = 0.15f; // mismo fix anti z-fighting que Sello del Pacto/puntos de guardado

        spell.forwardOffset    = 0.4f;
        spell.flattenDirection = true;

        spell.manaCost = 30f;
        spell.cooldown = 8f;

        spell.spawnVFX    = LoadByGuid<GameObject>(SpawnVfxGuid);
        spell.despawnVFX  = LoadByGuid<GameObject>(DespawnVfxGuid);
        spell.vfxLifetime = 3f;

        spell.castSFXKey   = "MagoOscuroGrieta";
        spell.impactSFXKey = "";
        spell.slotType      = SpellSlotType.Any;

        if (isNew) AssetDatabase.CreateAsset(spell, GrietaSpellAssetPath);
        else EditorUtility.SetDirty(spell);

        return spell;
    }

    // ── NPC_Combat_Config_MagoOscuro (wireado automáticamente con los 2 prefabs de arriba) ────

    private const string CombatConfigPath = "Assets/_NPCs/Combat/NPC_Combat_Config_MagoOscuro.asset";

    // Iconos de alerta/pregunta/exclamación y prefabs de barra de vida/maná ya usados por el
    // resto del elenco (ej. NPC_Combat_Config_Mago#2.asset) — se reutilizan tal cual, no hace
    // falta arte nuevo para esto.
    private const string AlertIconGuid       = "8754cf18218ab1b4c8c7c227f1e9ad68";
    private const string QuestionIconGuid    = "e3166847a7c81b443bdbe623a681ae52";
    private const string ExclamationIconGuid = "07a0b7801b6ad424eae696bee0e938ff";
    private const string HealthBarGuid       = "c9ac1bd5123d1544b8b85daac217ad0b";
    private const string ManaBarGuid         = "06a21cf2429ee2d4e879a503cb78faf6";

    /// <summary>
    /// Crea (o actualiza si ya existe) el NPCCombatConfig del Mago Oscuro para la Fase 1 de la
    /// Batalla Final (combate cooperativo normal — ver guion-tecnico-batalla-final-2026-08-30.md
    /// en el proyecto de Cowork). Solo cubre esa fase: el ataque cataclísmico de la Fase 2, la
    /// traición y el sacrificio de Liam son eventos scriptados en
    /// MagoOscuroFinalBattleSequencer.cs, no parte de este pool aleatorio de hechizos.
    ///
    /// Valores de partida razonados para un jefe final (vida/rangos muy por encima de un NPC de
    /// combate normal como Mago#2, campo de visión de 360° porque es "casi divino" y no debería
    /// poder ser flanqueado con facilidad) — no son ajuste fino, se esperan retoques tras
    /// probarlo en juego.
    /// </summary>
    private static void CreateOrUpdateCombatConfig(GameObject golpePrefab, GameObject grietaPrefab)
    {
        var folder = "Assets/_NPCs/Combat";
        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(CombatConfigPath);
        bool isNew = config == null;
        if (isNew)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[MagoOscuroSpellBuilder] La carpeta " + folder + " no existe — no se crea el NPCCombatConfig.");
                return;
            }
            config = ScriptableObject.CreateInstance<NPCCombatConfig>();
        }

        config.health = 500f; // jefe final: muy por encima de un NPC normal (Mago#2 usa 100)
        config.detectionRange = 18f;
        config.fieldOfView = 360f; // "casi divino" — no debería poder flanquearse con facilidad
        config.minAttackDistance = 4f;
        config.maxAttackDistance = 14f;
        config.isAggressive = true;
        config.canChaseOutOfBounds = false;
        config.maxChaseDistance = 20f;

        config.alertIconPrefab = LoadByGuid<GameObject>(AlertIconGuid);
        config.questionIconPrefab = LoadByGuid<GameObject>(QuestionIconGuid);
        config.exclamationIconPrefab = LoadByGuid<GameObject>(ExclamationIconGuid);
        config.alertIconDuration = 2f;
        config.alertIconHeight = 3f; // el Mago Oscuro es más alto que un NPC humano normal

        config.healthBarPrefab = LoadByGuid<GameObject>(HealthBarGuid);
        config.manaBarPrefab = LoadByGuid<GameObject>(ManaBarGuid);
        config.showManaBarToPlayer = false;

        // Sin diálogo de alerta genérico: el monólogo real (texto ya escrito en GDD.md, escena 20)
        // lo dispara MagoOscuroFinalBattleSequencer como cinemática, no este sistema de NPCBrain.
        config.waitForAlertDialogue = false;

        config.postDeathBehavior = Game.NPC.Modules.PostDeathBehavior.GetUpDizzy;
        config.postDefeatAction = Game.NPC.Modules.PostDefeatAction.None;

        // battleMusicId enlaza con la entrada "MagoOscuro" de AudioGraphProfile.battles (Fase 1,
        // combate cooperativo real) — distinta de la música de las cinemáticas envolventes
        // (esas usan el sistema de _sequenceMusicId del propio Sequencer, no esto).
        config.alertMusicEvent = "MagoOscuro_Alert";
        config.battleMusicId = "MagoOscuro";
        config.endMusicEvent = "MagoOscuro_Victory";

        config.sendEventOnDefeat = true;
        config.defeatEventKey = "MagoOscuro_Defeated";
        config.sendDefeatEventBeforeDeath = false;

        config.spell1Prefab = golpePrefab;
        config.spell2Prefab = grietaPrefab;
        config.spell3Prefab = null; // sin 3er hechizo aleatorio a propósito — ver nota de la Fase 2 arriba

        config.spell1Cooldown = 2.2f;
        config.spell2Cooldown = 8f;
        config.spell1Chance = 0.65f;
        config.spell2Chance = 0.35f;

        config.maxMana = 200f;
        config.manaRegenPerSecond = 25f;
        config.manaRegenDelayAfterSpend = 1f;
        config.spell1ManaCost = 20f;
        config.spell2ManaCost = 30f;
        config.spell1Damage = 25f;
        config.spell2Damage = 15f;
        config.lowManaRetreatThreshold = 0.2f;

        // Sin escudo ni huida táctica a propósito: un jefe final no debería sentirse "cobarde" —
        // su única salida de la Fase 1 es el ataque cataclísmico scriptado, no retirarse.
        config.useShield = false;
        config.useTacticalRetreat = false;

        config.difficultyLevel = 0.85f; // el más "experto" del elenco: es el jefe final
        config.deceptionChance = 0.1f;  // casi nada de finta — se apoya en poder bruto, no en trucos

        if (isNew) AssetDatabase.CreateAsset(config, CombatConfigPath);
        else EditorUtility.SetDirty(config);
    }

    // ── Wireado automático en _MAGO_OSCURO.prefab ───────────────────────────────
    // Referencia directa al actor de la Batalla Final. Al asignar aquí combatConfig y el flag
    // NPCBehaviourType.Combat, NPCBehaviourManagerV2 se encarga solo en tiempo de ejecución de
    // añadir Damageable (con SetMaxAndCurrent(combatConfig.health, ...) y destroyOnDeath=false),
    // NPCCombatLifecycleHandler, Targetable y NPCHealthBarSpawner (ver NPCBehaviourManagerV2.cs,
    // método de inicialización de módulos, rama "3. COMBAT MODULE") — no hace falta añadir ningún
    // componente a mano en el prefab ni en el Editor.
    private const string MagoOscuroPrefabPath = "Assets/Prefabs/_MAGO_OSCURO.prefab";

    private static void WireCombatConfigIntoMagoOscuroPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MagoOscuroPrefabPath) == null)
        {
            Debug.LogWarning("[MagoOscuroSpellBuilder] No se encontró " + MagoOscuroPrefabPath +
                " — no se puede wirear el combatConfig automáticamente.");
            return;
        }

        var config = AssetDatabase.LoadAssetAtPath<NPCCombatConfig>(CombatConfigPath);
        if (config == null)
        {
            Debug.LogWarning("[MagoOscuroSpellBuilder] No se encontró " + CombatConfigPath +
                " todavía — no se puede wirear el prefab.");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(MagoOscuroPrefabPath);
        try
        {
            var manager = root.GetComponent<NPCBehaviourManagerV2>();
            if (manager == null)
            {
                Debug.LogWarning("[MagoOscuroSpellBuilder] " + MagoOscuroPrefabPath +
                    " no tiene NPCBehaviourManagerV2 — no se puede wirear.");
                return;
            }

            var so = new SerializedObject(manager);
            var behaviourTypeProp = so.FindProperty("configuration.behaviourType");
            var combatConfigProp = so.FindProperty("configuration.combatConfig");

            if (behaviourTypeProp == null || combatConfigProp == null)
            {
                Debug.LogWarning("[MagoOscuroSpellBuilder] No se encontraron las propiedades " +
                    "serializadas de 'configuration' en NPCBehaviourManagerV2 — revisar nombres " +
                    "de campo a mano en el Editor.");
                return;
            }

            // behaviourType es [Flags]: se añade el flag Combat sin tocar los demás flags ya
            // activos (p.ej. Ambient, usado en las fases narrativas previas a la Fase 1 de combate).
            behaviourTypeProp.intValue |= (int)NPCBehaviourType.Combat;
            combatConfigProp.objectReferenceValue = config;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, MagoOscuroPrefabPath);

            Debug.Log("[MagoOscuroSpellBuilder] " + MagoOscuroPrefabPath + ": configuration.combatConfig " +
                "y behaviourType (+= Combat) wireados correctamente.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── Comunes ───────────────────────────────────────────────────────────────────

    private static void AddToSpellLibrary(MagicSpellSO spell)
    {
        var library = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(SpellLibraryPath);
        if (library == null)
        {
            Debug.LogWarning(
                "[MagoOscuroSpellBuilder] No se encontró SpellLibrary en " + SpellLibraryPath +
                " — añade '" + spell.displayName + "' a mano a la librería de hechizos.");
            return;
        }

        var so = new SerializedObject(library);
        var list = so.FindProperty("spells");

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == spell)
                return; // ya está en la librería, no duplicar
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = spell;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(library);
    }

    private static T LoadByGuid<T>(string guid) where T : Object
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
