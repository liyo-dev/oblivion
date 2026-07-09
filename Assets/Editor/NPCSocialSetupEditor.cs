using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Game.NPC.Modules;

/// <summary>
/// Herramienta de setup única: crea los NPCSocialConfig para todos los NPCs
/// y los asigna a sus prefabs.
/// Menú: El Sendero → NPCs → Setup Social Profiles
/// </summary>
public static class NPCSocialSetupEditor
{
    private const string SocialFolder = "Assets/_NPCs/Social";

    // ─── Definición de perfiles ────────────────────────────────────────────────
    private struct ProfileDef
    {
        public string FileName;
        public string NpcId;
        public string ModuleName;
        public float Sociability, Friendliness, Energy;
        public float DetectionRange, Cooldown, SearchRange;
    }

    // Perfiles individuales para NPCs con nombre propio
    private static readonly ProfileDef[] NamedProfiles =
    {
        new ProfileDef { FileName="NPC_Social_Eldran",    NpcId="npc_eldran",    ModuleName="Sabio Anciano",     Sociability=0.40f, Friendliness=0.85f, Energy=0.25f, DetectionRange=4f, Cooldown=45f, SearchRange=18f },
        new ProfileDef { FileName="NPC_Social_Sofia",     NpcId="npc_sofia",     ModuleName="Alegre",            Sociability=0.90f, Friendliness=0.90f, Energy=0.75f, DetectionRange=6f, Cooldown=20f, SearchRange=22f },
        new ProfileDef { FileName="NPC_Social_Manuel",    NpcId="npc_manuel",    ModuleName="Gregario",          Sociability=0.75f, Friendliness=0.70f, Energy=0.60f, DetectionRange=5f, Cooldown=25f, SearchRange=20f },
        new ProfileDef { FileName="NPC_Social_Nora",      NpcId="npc_nora",      ModuleName="Tímida",            Sociability=0.20f, Friendliness=0.65f, Energy=0.45f, DetectionRange=3f, Cooldown=60f, SearchRange=15f },
        new ProfileDef { FileName="NPC_Social_Oliver",    NpcId="npc_oliver",    ModuleName="Aventurero",        Sociability=0.65f, Friendliness=0.60f, Energy=0.85f, DetectionRange=6f, Cooldown=25f, SearchRange=25f },
        new ProfileDef { FileName="NPC_Social_Roberto",   NpcId="npc_roberto",   ModuleName="Gruñón",            Sociability=0.30f, Friendliness=0.20f, Energy=0.55f, DetectionRange=4f, Cooldown=60f, SearchRange=15f },
        new ProfileDef { FileName="NPC_Social_Sara",      NpcId="npc_sara",      ModuleName="Vivaz",             Sociability=0.85f, Friendliness=0.90f, Energy=0.80f, DetectionRange=6f, Cooldown=18f, SearchRange=22f },
        new ProfileDef { FileName="NPC_Social_Veronica",  NpcId="npc_veronica",  ModuleName="Misteriosa",        Sociability=0.25f, Friendliness=0.45f, Energy=0.50f, DetectionRange=4f, Cooldown=55f, SearchRange=16f },
        new ProfileDef { FileName="NPC_Social_Victoria",  NpcId="npc_victoria",  ModuleName="Digna",             Sociability=0.50f, Friendliness=0.70f, Energy=0.35f, DetectionRange=4f, Cooldown=40f, SearchRange=18f },
        new ProfileDef { FileName="NPC_Social_Tendera",   NpcId="npc_tendera",   ModuleName="Comerciante",       Sociability=0.80f, Friendliness=0.85f, Energy=0.65f, DetectionRange=5f, Cooldown=22f, SearchRange=20f },
        new ProfileDef { FileName="NPC_Social_Rudolfo",   NpcId="npc_rudolfo",   ModuleName="Sombrío",           Sociability=0.15f, Friendliness=0.30f, Energy=0.45f, DetectionRange=3f, Cooldown=70f, SearchRange=12f },
        new ProfileDef { FileName="NPC_Social_Jorge",     NpcId="npc_jorge",     ModuleName="Campesino",         Sociability=0.55f, Friendliness=0.65f, Energy=0.60f, DetectionRange=5f, Cooldown=30f, SearchRange=18f },
        new ProfileDef { FileName="NPC_Social_King",      NpcId="npc_king",      ModuleName="Regio",             Sociability=0.25f, Friendliness=0.60f, Energy=0.40f, DetectionRange=4f, Cooldown=50f, SearchRange=15f },
        new ProfileDef { FileName="NPC_Social_Guard",     NpcId="npc_guard",     ModuleName="Guardia",           Sociability=0.20f, Friendliness=0.40f, Energy=0.85f, DetectionRange=5f, Cooldown=40f, SearchRange=10f },
        new ProfileDef { FileName="NPC_Social_WoodsGuard",NpcId="npc_woodsguard",ModuleName="Guardia del bosque",Sociability=0.15f, Friendliness=0.35f, Energy=0.90f, DetectionRange=5f, Cooldown=45f, SearchRange=10f },
        new ProfileDef { FileName="NPC_Social_Estela",    NpcId="npc_estela",    ModuleName="Compañera Estela",  Sociability=0.80f, Friendliness=0.85f, Energy=0.80f, DetectionRange=6f, Cooldown=20f, SearchRange=22f },
        new ProfileDef { FileName="NPC_Social_Liam",      NpcId="npc_liam",      ModuleName="Compañero Liam",    Sociability=0.70f, Friendliness=0.75f, Energy=0.70f, DetectionRange=6f, Cooldown=22f, SearchRange=20f },
        new ProfileDef { FileName="NPC_Social_Will",      NpcId="npc_will",      ModuleName="Will",              Sociability=0.60f, Friendliness=0.65f, Energy=0.65f, DetectionRange=5f, Cooldown=28f, SearchRange=20f },
        new ProfileDef { FileName="NPC_Social_Nino",      NpcId="npc_nino_pez",  ModuleName="Niño Pez",          Sociability=0.70f, Friendliness=0.80f, Energy=0.90f, DetectionRange=5f, Cooldown=20f, SearchRange=22f },
        new ProfileDef { FileName="NPC_Social_AmigaNino", NpcId="npc_amiga_nino",ModuleName="Amiga del Niño Pez",Sociability=0.75f, Friendliness=0.80f, Energy=0.80f, DetectionRange=5f, Cooldown=22f, SearchRange=20f },
    };

    // Perfiles genéricos / arquetipos reutilizables
    private static readonly ProfileDef[] Archetypes =
    {
        new ProfileDef { FileName="NPC_Social_Archetype_Friendly",  NpcId="",  ModuleName="Arquetipo: Amigable",   Sociability=0.80f, Friendliness=0.80f, Energy=0.60f, DetectionRange=5f, Cooldown=25f, SearchRange=20f },
        new ProfileDef { FileName="NPC_Social_Archetype_Reserved",  NpcId="",  ModuleName="Arquetipo: Reservado",  Sociability=0.30f, Friendliness=0.50f, Energy=0.40f, DetectionRange=4f, Cooldown=50f, SearchRange=16f },
        new ProfileDef { FileName="NPC_Social_Archetype_Energetic", NpcId="",  ModuleName="Arquetipo: Energético", Sociability=0.70f, Friendliness=0.60f, Energy=0.90f, DetectionRange=6f, Cooldown=20f, SearchRange=24f },
        new ProfileDef { FileName="NPC_Social_Archetype_Lazy",      NpcId="",  ModuleName="Arquetipo: Perezoso",   Sociability=0.50f, Friendliness=0.60f, Energy=0.20f, DetectionRange=4f, Cooldown=35f, SearchRange=18f },
        new ProfileDef { FileName="NPC_Social_Archetype_Grumpy",    NpcId="",  ModuleName="Arquetipo: Gruñón",     Sociability=0.20f, Friendliness=0.25f, Energy=0.55f, DetectionRange=4f, Cooldown=65f, SearchRange=14f },
    };

    // Mapeo prefab → perfil (nombre de archivo, sin .asset)
    private static readonly Dictionary<string, string> PrefabToProfile = new Dictionary<string, string>
    {
        // Compañeros del party
        { "_ESTELA",          "NPC_Social_Estela"    },
        { "_LIAM",            "NPC_Social_Liam"      },
        { "_WILL_NPC",        "NPC_Social_Will"      },
        // NPCs con nombre propio
        { "Eldran",           "NPC_Social_Eldran"    },
        { "Sofia",            "NPC_Social_Sofia"     },
        { "Manuel",           "NPC_Social_Manuel"    },
        { "Nora",             "NPC_Social_Nora"      },
        { "Oliver",           "NPC_Social_Oliver"    },
        { "Roberto",          "NPC_Social_Roberto"   },
        { "Sara",             "NPC_Social_Sara"      },
        { "Verónica",         "NPC_Social_Veronica"  },
        { "Victoria",         "NPC_Social_Victoria"  },
        { "Tendera",          "NPC_Social_Tendera"   },
        { "Rudolfo",          "NPC_Social_Rudolfo"   },
        { "Jorge",            "NPC_Social_Jorge"     },
        { "King",             "NPC_Social_King"      },
        { "Guard",            "NPC_Social_Guard"     },
        { "WoodsGuard",       "NPC_Social_WoodsGuard"},
        { "NiñoPez",          "NPC_Social_Nino"      },
        { "AmigaDelNiñoPez",  "NPC_Social_AmigaNino" },
        // NPCs de pueblo / genéricos (rotan entre arquetipos)
        { "TownNpc#1",        "NPC_Social_Archetype_Friendly"  },
        { "TownNpc#2",        "NPC_Social_Archetype_Reserved"  },
        { "TownNpc#3",        "NPC_Social_Archetype_Energetic" },
        { "TownNpc#4",        "NPC_Social_Archetype_Lazy"      },
        { "TownNpc#5",        "NPC_Social_Archetype_Friendly"  },
        { "TownNpc#6",        "NPC_Social_Archetype_Grumpy"    },
        { "TownNpc#7",        "NPC_Social_Archetype_Reserved"  },
        { "TownNpc#8",        "NPC_Social_Archetype_Energetic" },
        { "TownNpc#9",        "NPC_Social_Archetype_Lazy"      },
        { "TownNpc#10",       "NPC_Social_Archetype_Friendly"  },
        { "MC01 (1)",         "NPC_Social_Archetype_Friendly"  },
        { "MC01 (1) 1",       "NPC_Social_Archetype_Reserved"  },
        { "MC01 (1) 2",       "NPC_Social_Archetype_Energetic" },
        { "MC01 (1) 3",       "NPC_Social_Archetype_Lazy"      },
        { "MC01 (1) 4",       "NPC_Social_Archetype_Grumpy"    },
        { "MC01 (1) 5",       "NPC_Social_Archetype_Friendly"  },
        { "MC01 (1) 6",       "NPC_Social_Archetype_Reserved"  },
        // Guerreros genéricos
        { "Guerrero#1",       "NPC_Social_Archetype_Reserved"  },
        { "Guerrero#2",       "NPC_Social_Archetype_Grumpy"    },
        { "Guerrero#3",       "NPC_Social_Archetype_Reserved"  },
        // NPCs de combate NO tienen social (enemies)
        // Boy_Pirate, Erika, Lety, Mago #1-3, Vicky → omitidos intencionalmente
    };

    // ─── Punto de entrada ─────────────────────────────────────────────────────
    [MenuItem("El Sendero/NPCs/Setup Social Profiles")]
    public static void SetupSocialProfiles()
    {
        // 1. Garantizar que la carpeta existe
        if (!Directory.Exists(SocialFolder))
            Directory.CreateDirectory(SocialFolder);

        // 2. Crear / actualizar assets
        var assetMap = new Dictionary<string, NPCSocialConfig>();

        foreach (var p in NamedProfiles)
            assetMap[p.FileName] = EnsureAsset(p);

        foreach (var p in Archetypes)
            assetMap[p.FileName] = EnsureAsset(p);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 3. Asignar a prefabs
        int assigned = 0, skipped = 0;

        string[] allPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in allPrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var manager = prefab.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            if (manager == null) continue;

            string prefabName = prefab.name;
            if (!PrefabToProfile.TryGetValue(prefabName, out string profileFile))
            {
                skipped++;
                continue;
            }

            if (!assetMap.TryGetValue(profileFile, out NPCSocialConfig config))
            {
                Debug.LogWarning($"[SocialSetup] Perfil '{profileFile}' no encontrado para '{prefabName}'");
                skipped++;
                continue;
            }

            // Usar SerializedObject para modificar el campo privado 'configuration.socialConfig'
            var so = new SerializedObject(manager);
            var configProp      = so.FindProperty("configuration");
            var socialProp      = configProp?.FindPropertyRelative("socialConfig");

            if (socialProp == null)
            {
                Debug.LogWarning($"[SocialSetup] No se encontró 'configuration.socialConfig' en '{prefabName}'");
                skipped++;
                continue;
            }

            if (socialProp.objectReferenceValue == config)
            {
                skipped++;
                continue; // Ya asignado correctamente
            }

            socialProp.objectReferenceValue = config;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            assigned++;

            Debug.Log($"[SocialSetup] ✅ {prefabName} → {profileFile}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Setup Social Profiles",
            $"Completado.\n\nAssets creados/actualizados: {NamedProfiles.Length + Archetypes.Length}\nPrefabs asignados: {assigned}\nOmitidos: {skipped}",
            "OK");
    }

    // ─── Utilidades ───────────────────────────────────────────────────────────

    private static NPCSocialConfig EnsureAsset(ProfileDef def)
    {
        string assetPath = $"{SocialFolder}/{def.FileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<NPCSocialConfig>(assetPath);
        if (existing != null)
            return existing; // No sobreescribir si ya existe

        var config = ScriptableObject.CreateInstance<NPCSocialConfig>();
        config.moduleName  = def.ModuleName;
        config.npcId       = def.NpcId;
        config.personality = new NPCPersonality
        {
            sociability  = def.Sociability,
            friendliness = def.Friendliness,
            energy       = def.Energy,
        };
        config.socialDetectionRange  = def.DetectionRange;
        config.socialCooldown        = def.Cooldown;
        config.worldPointSearchRange = def.SearchRange;

        AssetDatabase.CreateAsset(config, assetPath);
        return config;
    }
}
