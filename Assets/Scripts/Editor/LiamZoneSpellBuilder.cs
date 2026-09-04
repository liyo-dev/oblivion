using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para crear el hechizo de zona "Sello del Pacto" de Liam: un
/// MagicSpellSO de tipo MagicKind.Zone más su prefab de zona, siguiendo el mismo patrón de
/// herramienta de Editor con [MenuItem] que LiamSpellBuilder.cs (Garra del Pacto) y
/// MainMenuVersionLabelBuilder.cs (INC-083).
///
/// Concepto (pedido por Raúl, 30 ago 2026): un hechizo de rango que sale de la mano de Liam
/// (reutilizando el mismo VFX/timing de casteo que el resto de hechizos, para aprovechar la
/// animación de casteo ya existente) pero que, en vez de volar como un proyectil, se materializa
/// al instante como una zona fija en el suelo. Encaja de forma directa con la ficha de Liam en el
/// GDD ("Habilidades: Intelecto, Trampas") y con su magia de pacto/Grimorio: Liam sella el suelo
/// con un símbolo arcano que atrapa y desgasta a quien se quede dentro — un hechizo-trampa, no un
/// golpe directo. Nombre a juego con "Garra del Pacto" (mismo prefijo "...del Pacto").
///
/// VFX reutilizado del proyecto (sin crear ninguno nuevo, tal y como se pidió revisar primero lo
/// que ya hay): "AoE Poison" del pack Matthew Guz - Spell Area of Effect FREE
/// (Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/AoE Poison.prefab) como visual de la
/// zona en sí — una nube/partícula que, retintada a violeta (ver punto 1 más abajo), encaja con
/// la idea de "trampa que atrapa y desgasta" mejor que un aro de estrellas — y el mismo VFX de
/// "sale de la mano" (sparkle genérico) que ya usan BolaPrisma/CorazonEstelar/LlamaAstral/Tornado/
/// GarraDelPacto para el flash de casteo.
///
/// CORRECCIÓN (30/08/2026, misma tarde): la primera versión de esta herramienta usaba
/// "AoE Magic.prefab" (guid 9029beab...) en vez de "AoE Poison". Ese archivo concreto YA NO es un
/// VFX limpio en este proyecto: en algún momento se reutilizó para construir directamente el
/// punto de guardado del Bosque (contiene SavePoint.cs con anchorIdToSet "Woods_SavePoint",
/// Interactable.cs, un Box Collider en layer "Interactable" y un Canvas), en vez de duplicarlo
/// antes de tocarlo. Instanciarlo por GUID arrastraba todo ese SavePoint dentro del prefab del
/// hechizo (visible en el Editor: hijo "AoE Magic" con nietos Canvas/Magic Base/Particle/
/// SavePoint, y el componente "Interactable (Script)" en el Inspector). Los otros 4 prefabs del
/// mismo pack (Ice Storm, Poison, Stars, Holy Sword) se comprobaron por grep y están limpios
/// (cero referencias a SavePoint/Interactable/Canvas) — de ahí el cambio a "AoE Poison". Si
/// alguna vez se generó ya el prefab con el visual viejo (SelloDelPacto.prefab con el SavePoint
/// dentro), bórralo a mano en el Editor antes de volver a ejecutar este menú — la herramienta no
/// sobrescribe un prefab que ya existe (ver CreateOrRepairPrefab).
///
/// Idempotente: se puede volver a ejecutar sin duplicar nada.
///
/// Pendiente a mano en el Editor tras ejecutar el menú (no se puede hacer desde aquí):
///   1) Retocar el color de "AoE Magic" hacia el violeta ya usado en las cinemáticas de Liam
///      (LiamGolemSummonSequencer / LiamCrystalBallSequencer) — instancia propia del material,
///      no el compartido por el resto del pack de Matthew Guz.
///   2) Asignar un icono en el campo "attackIcon" del asset generado.
///   3) Dar de alta la clave de audio "SelloDelPacto" en AudioService/AudioGraphProfile —
///      castSFXKey ya apunta a esa clave, pero no sonará hasta que exista. impactSFXKey se deja
///      vacío a propósito (sin sonido de tick) para no saturar el oído cada 0.5s; añadir uno
///      suave si se quiere.
///   4) Probarlo en juego: comprobar que la zona aparece apoyada en el suelo (no flotando) tanto
///      con un enemigo fijado como sin target, y ajustar zoneGroundLayers si hace falta
///      excluir alguna capa concreta del raycast.
///   5) Decidir en qué slot se equipa (queda como SpecialOnly de partida, coherente con ser un
///      hechizo de control de área más pesado que Garra del Pacto).
///   6) Si se quiere localizar, añadir SPELL_SELLO_PACTO_NAME al sistema de localización (si no
///      existe la clave, MagicSpellSO.GetLocalizedName() cae automáticamente al displayName).
/// </summary>
public static class LiamZoneSpellBuilder
{
    private const string SpellAssetPath   = "Assets/_SPELLS/SelloDelPacto.asset";
    private const string PrefabFolder     = "Assets/_SPELLS/Prefabs";
    private const string PrefabPath       = PrefabFolder + "/SelloDelPacto.prefab";
    private const string SpellLibraryPath = "Assets/Scripts/Attacks/SO/SpellLibrary.asset";

    // "AoE Poison" — Assets/VFX/Matthew Guz/Spell Area of Effect FREE/Prefab/AoE Poison.prefab.
    // Visual de la zona en sí (nube/partícula persistente); se retoca el color a mano en el
    // Editor (ver punto 1 de arriba) sobre la instancia, sin tocar el material compartido.
    // NO usar el guid de "AoE Magic" (9029beab0dd9eb84aade1739487ed290) — ver la nota de
    // corrección en el comentario de cabecera de esta clase: en este proyecto ese archivo es en
    // realidad el punto de guardado del Bosque, no un VFX limpio.
    private const string ZoneVisualPrefabGuid = "bd67489a2df67ba4ba4c3e7b9c4e0e8c";

    // Mismo VFX de "sale de la mano" (sparkle genérico) que ya usan los hechizos hermanos de
    // Liam y del resto del elenco — mantiene la coherencia visual del sistema de magia.
    private const string SpawnVfxGuid   = "895c6d094b6b213418cddcfb520298e9";
    private const string DespawnVfxGuid = "dcd90c4976197424b9958a7c54b6bb8c";

    [MenuItem("El Sendero/Magia/Crear Hechizo de Liam (Sello del Pacto)")]
    public static void CreateSelloDelPacto()
    {
        GameObject prefab = CreateOrRepairPrefab();
        MagicSpellSO spell = CreateOrUpdateSpellAsset(prefab);
        AddToSpellLibrary(spell);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[LiamZoneSpellBuilder] 'Sello del Pacto' listo en " + SpellAssetPath + " y " + PrefabPath + ". " +
            "Pendiente a mano en el Editor: 1) retocar el color de la zona hacia el violeta de Liam " +
            "(instancia propia, no el material compartido), 2) asignar 'attackIcon', " +
            "3) dar de alta la clave de audio 'SelloDelPacto' en AudioService/AudioGraphProfile, " +
            "4) probarlo en juego (con y sin target fijado) y ajustar zoneGroundLayers si hace falta, " +
            "5) decidir el slot definitivo, 6) opcional: SPELL_SELLO_PACTO_NAME en localización.",
            spell);
    }

    private static GameObject CreateOrRepairPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            RepairExistingPrefab();
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/_SPELLS", "Prefabs");
        }

        var visualPath = AssetDatabase.GUIDToAssetPath(ZoneVisualPrefabGuid);
        var visualPrefab = string.IsNullOrEmpty(visualPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<GameObject>(visualPath);

        // A diferencia de un proyectil, la zona NO necesita Collider: MagicZoneEffect usa
        // Physics.OverlapSphereNonAlloc en vez de triggers físicos (ver comentario en el propio
        // componente), así que no hace falta añadir ni configurar ningún Collider aquí.
        var root = new GameObject("SelloDelPacto");
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
                "[LiamZoneSpellBuilder] No se encontró el prefab visual base (guid " + ZoneVisualPrefabGuid +
                "). La zona se crea sin visual — asigna uno a mano dentro de " + PrefabPath + ".");
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    /// <summary>
    /// Repara en el sitio un prefab ya existente, sin reconstruirlo desde cero (para no perder
    /// retoques manuales de Raúl, como el tinte de color pedido en el pendiente #3): se asegura
    /// de que el root tenga MagicZoneEffect y de que los Particle System del visual tengan loop
    /// activado. Usa LoadPrefabContents/SaveAsPrefabAsset (API correcta de Unity para editar un
    /// prefab ya guardado en disco) en vez de SaveAsPrefabAsset sobre un GameObject de escena
    /// nuevo (eso crearía un prefab distinto, no editaría el existente).
    /// </summary>
    private static void RepairExistingPrefab()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool changed = false;

        if (contents.GetComponent<MagicZoneEffect>() == null)
        {
            contents.AddComponent<MagicZoneEffect>();
            changed = true;
            Debug.LogWarning("[LiamZoneSpellBuilder] El prefab existente no tenía MagicZoneEffect en el root — añadido.");
        }

        if (ForceParticleSystemsToLoop(contents))
        {
            changed = true;
        }

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            Debug.Log("[LiamZoneSpellBuilder] Prefab existente reparado en el sitio: " + PrefabPath);
        }
        else
        {
            Debug.Log("[LiamZoneSpellBuilder] El prefab ya existía y no hacía falta reparar nada: " + PrefabPath);
        }

        PrefabUtility.UnloadPrefabContents(contents);
    }

    /// <summary>
    /// El VFX de Matthew Guz está pensado como una ráfaga de casteo de un solo uso (se instancia,
    /// se reproduce una vez y se destruye) — no como un efecto persistente. Al reutilizarlo para
    /// una zona que dura varios segundos (MagicSpellSO.zoneDuration), sin este ajuste el VFX
    /// termina su única pasada mucho antes de que la zona dañe de verdad, y se queda "a medias"
    /// el resto del tiempo. Forzar loop=true en todos los Particle System del visual (incluidos
    /// los anidados en el prefab de origen) evita eso. Devuelve true si tocó algo.
    /// </summary>
    private static bool ForceParticleSystemsToLoop(GameObject visualRoot)
    {
        bool changedAny = false;
        var systems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            var main = ps.main;
            if (!main.loop)
            {
                main.loop = true;
                changedAny = true;
            }
        }
        return changedAny;
    }

    private static MagicSpellSO CreateOrUpdateSpellAsset(GameObject prefab)
    {
        var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(SpellAssetPath);
        bool isNew = spell == null;
        if (isNew)
        {
            spell = ScriptableObject.CreateInstance<MagicSpellSO>();
        }

        spell.spellId       = SpellId.SelloDelPacto;
        spell.displayNameId = "SPELL_SELLO_PACTO_NAME";
        spell.displayName   = "Sello del Pacto";
        spell.kind          = MagicKind.Zone;
        // Mind, igual que Garra del Pacto y Levitation — coherente con la ficha de Liam del GDD
        // ("Habilidades: Intelecto, Trampas"); un hechizo de zona/trampa encaja de forma directa.
        spell.element = MagicElement.Mind;
        spell.prefab  = prefab;

        // Casting: mismo delay que Garra del Pacto para sincronizar con la animación de mano.
        spell.castDelaySeconds = 0.5f;

        // Campos de proyectil (initialSpeed/maxRange/lifeTime/useGravity) no se usan en
        // MagicKind.Zone — se dejan en sus valores por defecto del SO.

        // Daño / zona
        spell.damage           = 12f;   // daño POR TICK (ver zoneTickInterval)
        spell.knockbackForce   = 0f;    // sin empuje a propósito: es una trampa, no debe expulsar
        spell.destroyOnHit     = true;  // sin efecto real en Zone (no hay impacto único), se deja el default

        spell.zoneRadius        = 4.5f;
        spell.zoneDuration      = 5f;
        spell.zoneTickInterval  = 0.5f; // 10 ticks x 12 daño = hasta 120 si el objetivo se queda dentro toda la duración
        spell.zoneRange         = 9f;   // distancia delante de Liam si no hay target fijado
        spell.zoneSnapToTarget  = true;
        spell.zoneGroundLayers  = ~0;   // de partida sin restringir; ajustar en el Editor si hace falta
        spell.zoneGroundOffset  = 0.15f; // eleva el VFX sobre el suelo real (evita que se mezcle/z-fighting, mismo problema que tenían los puntos de guardado)

        spell.forwardOffset    = 0.35f; // solo afecta a dónde se ve el flash de casteo en la mano
        spell.flattenDirection = true;

        spell.manaCost = 25f;
        spell.cooldown = 7f; // hechizo de control de área, no para spamear

        spell.spawnVFX    = LoadByGuid<GameObject>(SpawnVfxGuid);
        spell.despawnVFX  = LoadByGuid<GameObject>(DespawnVfxGuid);
        spell.vfxLifetime = 3f;

        spell.castSFXKey   = "SelloDelPacto";
        spell.impactSFXKey = ""; // sin SFX de tick de partida (ver nota 3 en el comentario de arriba)
        spell.slotType      = SpellSlotType.SpecialOnly;

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
                "[LiamZoneSpellBuilder] No se encontró SpellLibrary en " + SpellLibraryPath +
                " — añade 'Sello del Pacto' a mano a la librería de hechizos.");
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
