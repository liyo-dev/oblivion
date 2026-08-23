using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para crear el hechizo "Garra del Pacto" de Liam: un MagicSpellSO
/// más su prefab de proyectil, siguiendo el mismo patrón que los hechizos ya existentes en
/// Assets/_SPELLS (BolaFuego, BolaPrisma, CorazonEstelar, LlamaAstral, Tornado...).
///
/// Por qué una herramienta de Editor y no un .asset/.prefab escrito a mano: MagicProjectile
/// exige un Collider ya presente en el prefab (ver [RequireComponent] en MagicProjectil.cs), y
/// las referencias internas de un .prefab (fileIDs de GameObjects/Componentes) no se pueden
/// inventar a mano de forma fiable. Este builder usa las APIs de Unity para que el prefab y el
/// ScriptableObject queden generados correctamente — mismo patrón que ya usa el proyecto para
/// tocar el Editor sin editar YAML a mano (ver MainMenuVersionLabelBuilder.cs / INC-083).
///
/// Idempotente: se puede volver a ejecutar sin duplicar nada.
///
/// Concepto del hechizo: Liam invoca (él es quien invoca al demonio y al gólem en la historia,
/// no le falta magia — lo que le falta es un "corazón puro" para cruzar el Sendero) una garra
/// espectral a través de una grieta que él mismo abre. Element = Mind, en línea con su ficha de
/// personaje del GDD ("Habilidades: Intelecto, Trampas").
///
/// Pendiente a mano en el Editor tras ejecutar el menú (no se puede hacer desde aquí):
///   1) Retocar el color del VFX del proyectil hacia el violeta ya usado en las cinemáticas de
///      Liam (LiamGolemSummonSequencer / LiamCrystalBallSequencer) — es una instancia propia del
///      material, no el compartido por el resto del pack.
///   2) Asignar un icono en el campo "attackIcon" del asset generado.
///   3) Dar de alta la clave de audio "GarraDelPacto" en AudioService/AudioGraphProfile —
///      castSFXKey ya apunta a esa clave, pero no sonará hasta que exista.
///   4) Si se quiere localizar, añadir SPELL_GARRA_PACTO_NAME al sistema de localización (si no
///      existe la clave, MagicSpellSO.GetLocalizedName() cae automáticamente al displayName, así
///      que no rompe nada dejarlo sin traducir de momento).
/// </summary>
public static class LiamSpellBuilder
{
    private const string SpellAssetPath   = "Assets/_SPELLS/GarraDelPacto.asset";
    private const string PrefabFolder     = "Assets/_SPELLS/Prefabs";
    private const string PrefabPath       = PrefabFolder + "/GarraDelPacto.prefab";
    private const string SpellLibraryPath = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";

    // Prefab visual base ya incluido en el proyecto (GabrielAguiarProductions — FreeQuickEffectsVol1).
    // Se instancia como hijo del proyectil real; el color se retoca a mano en el Editor (ver punto 1
    // de arriba) sobre la instancia, sin tocar el material compartido del resto del pack.
    private const string VisualPrefabGuid = "bc142210df3ec4545a4e3e1f21e00da7"; // vfx_Projectile_01

    // VFX de spawn/impacto/despawn reutilizados de los hechizos hermanos (mismo "sparkle" genérico
    // que ya usan BolaPrisma, CorazonEstelar, LlamaAstral y Tornado) — mantiene la coherencia visual
    // del sistema de magia sin depender de VFX nuevos sin probar.
    private const string SpawnVfxGuid   = "895c6d094b6b213418cddcfb520298e9";
    private const string ImpactVfxGuid  = "67a684e320da6e7439421a07e3fa265c";
    private const string DespawnVfxGuid = "dcd90c4976197424b9958a7c54b6bb8c";

    [MenuItem("El Sendero/Magia/Crear Hechizo de Liam (Garra del Pacto)")]
    public static void CreateGarraDelPacto()
    {
        GameObject prefab = CreateOrRepairPrefab();
        MagicSpellSO spell = CreateOrUpdateSpellAsset(prefab);
        AddToSpellLibrary(spell);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[LiamSpellBuilder] 'Garra del Pacto' listo en " + SpellAssetPath + " y " + PrefabPath + ". " +
            "Pendiente a mano en el Editor: 1) retocar el color del VFX hacia el violeta de Liam " +
            "(instancia propia, no el material compartido), 2) asignar 'attackIcon', " +
            "3) dar de alta la clave de audio 'GarraDelPacto' en AudioService/AudioGraphProfile, " +
            "4) opcional: añadir SPELL_GARRA_PACTO_NAME a localización (si no existe, usa displayName tal cual).",
            spell);
    }

    private static GameObject CreateOrRepairPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            Debug.Log("[LiamSpellBuilder] El prefab ya existía, no se duplica: " + PrefabPath);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Prefabs");
        }

        var visualPath = AssetDatabase.GUIDToAssetPath(VisualPrefabGuid);
        var visualPrefab = string.IsNullOrEmpty(visualPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);

        var root = new GameObject("Proj_GarraDelPacto");
        var collider = root.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.35f;
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
                "[LiamSpellBuilder] No se encontró el prefab visual base (guid " + VisualPrefabGuid +
                "). El proyectil se crea sin visual — asigna uno a mano dentro de " + PrefabPath + ".");
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static MagicSpellSO CreateOrUpdateSpellAsset(GameObject prefab)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(SpellAssetPath);
        bool isNew = spell == null;
        if (isNew)
        {
            spell = ScriptableObject.CreateInstance<MagicSpellSO>();
        }

        spell.spellId       = SpellId.GarraDelPacto;
        spell.displayNameId = "SPELL_GARRA_PACTO_NAME";
        spell.displayName   = "Garra del Pacto";
        spell.kind          = MagicKind.Projectile;
        // Mind, no un elemento "de combate" al uso — en línea con la ficha del GDD de Liam
        // ("Habilidades: Intelecto, Trampas"). Ya lo comparte Levitation; no hay problema en
        // que dos hechizos compartan elemento.
        spell.element = MagicElement.Mind;
        spell.prefab  = prefab;

        spell.castDelaySeconds  = 0.5f;
        spell.initialSpeed      = 14f;
        spell.maxRange          = 40f;
        spell.lifeTime          = 2f;
        spell.damage            = 35f;
        spell.aoeRadius         = 0f;
        spell.knockbackForce    = 12f;
        spell.forwardOffset     = 0.35f;
        spell.flattenDirection  = true;
        spell.manaCost          = 15f;
        spell.cooldown          = 1.2f;

        spell.spawnVFX    = LoadByGuid<GameObject>(SpawnVfxGuid);
        spell.impactVFX   = LoadByGuid<GameObject>(ImpactVfxGuid);
        spell.despawnVFX  = LoadByGuid<GameObject>(DespawnVfxGuid);
        spell.vfxLifetime = 3f;

        spell.castSFXKey   = "GarraDelPacto";
        spell.impactSFXKey = "Impact1";
        spell.slotType      = SpellSlotType.Any;

        if (isNew)
        {
            AssetDatabase.CreateAsset(spell, SpellAssetPath);
        }
        else
        {
            EditorUtility.SetDirty(spell);
        }

        return spell;
    }

    private static void AddToSpellLibrary(MagicSpellSO spell)
    {
        var library = AssetDatabase.LoadAssetAtPath<SpellLibrarySO>(SpellLibraryPath);
        if (library == null)
        {
            Debug.LogWarning(
                "[LiamSpellBuilder] No se encontró SpellLibrary en " + SpellLibraryPath +
                " — añade 'Garra del Pacto' a mano a la librería de hechizos.");
            return;
        }

        var so = new SerializedObject(library);
        var list = so.FindProperty("spells");

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == spell)
            {
                return; // ya está en la librería, no duplicar
            }
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
