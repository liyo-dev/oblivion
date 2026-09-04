using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor que crea los VFX exclusivos de la Batalla Final y el desenlace
/// (escenas 20-22 del GDD — ver guion-tecnico-batalla-final-2026-08-30.md en el proyecto de
/// Cowork "El Sendero de las Estrellas"), reutilizando visuales ya presentes en los packs del
/// proyecto (GabrielAguiarProductions/FreeQuickEffectsVol1, Hovl Studio/Magic effects pack,
/// Univeral FX Shader, 100BestEffectPack) — ninguno se crea desde cero. Sigue el mismo criterio
/// que MagoOscuroSpellBuilder.cs: los fileID/guid internos de un asset no se pueden inventar a
/// mano de forma fiable, así que esta herramienta los localiza por guid confirmado y crea
/// Prefab Variants reales vía PrefabUtility (Unity gestiona los fileID internamente).
///
/// Cada variante creada en Assets/_VFX/BatallaFinal/ queda como Prefab Variant de su fuente: el
/// material/color se puede sobreescribir a mano en el Editor sobre la propia variante (Override)
/// sin tocar el material compartido del pack de origen — mismo criterio de "instancia propia,
/// nunca el material compartido" ya aplicado a MagoOscuroGolpe/MagoOscuroGrieta.
///
/// Tres VFX del elenco NO se duplican: se reutilizan tal cual porque ya encajan sin retoque
/// (ver constantes *Guid más abajo y el log de CrearVfxCinematicos()).
///
/// Idempotente: se puede volver a ejecutar sin duplicar nada (si el prefab de salida ya existe,
/// se deja como está — así no se pierde ningún retoque manual ya hecho en el Editor).
///
/// Pendiente a mano en el Editor tras ejecutar el menú (no se puede hacer desde aquí):
///   1) Retintar las variantes marcadas "retintar" más abajo hacia su paleta (violeta oscuro/negro
///      para las del Mago Oscuro, dorado/blanco cálido para el sacrificio de Liam) — Override de
///      material sobre la propia variante, nunca el material compartido del pack.
///   2) Ajustar escala/duración de cada VFX al tamaño real de la escena de Sendero.unity una vez
///      colocados los actores y shot points (fuera del alcance de esta pasada, ver cabecera de
///      MagoOscuroFinalBattleSequencer.cs).
///   3) Arrastrar cada prefab (o el ya existente, para los tres reutilizados tal cual) al campo
///      correspondiente en MagoOscuroFinalBattleSequencer / WillSacrificeSequencer /
///      EpilogueSequencer una vez esos componentes estén añadidos a la escena.
/// </summary>
public static class MagoOscuroCinematicVfxBuilder
{
    private const string RootFolder = "Assets/_VFX";
    private const string OutputFolder = RootFolder + "/BatallaFinal";

    // Reutilizados TAL CUAL, sin duplicar ni retintar — ya encajan en el elenco existente:
    // "Aura Estelar" es el propio hechizo idle/aura de Will ya en el juego (SpellId.AuraEstelar,
    // Assets/_SPELLS/AuraEstelar.asset) — máxima coherencia posible para "Will recupera su
    // potencial": es literalmente su propia magia, no un sustituto genérico.
    private const string WillAwakenedAuraGuid = "bbc5b80cac40a1e4d9abc000bcbdd80e"; // AuraEstelar (spell de Will) — Fase C
    private const string CriticalCounterSpellGuid = "98c4704d0fd7211449bcf5c451095a60"; // Hovl "Star hit" — Fase G (dorado, palette de Will ya correcta)
    private const string WillSpiritGuid = "354b6251e950209409c75ab984acf000"; // Hovl "Star aura" — Epílogo (dorado, palette de Will ya correcta)

    private struct VfxDef
    {
        public string Guid;
        public string OutputName;
        public string Note;
        public VfxDef(string guid, string outputName, string note)
        {
            Guid = guid; OutputName = outputName; Note = note;
        }
    }

    // Duplicados como Prefab Variant independiente en OutputFolder (fuente original intacta).
    private static readonly VfxDef[] Variants = new[]
    {
        new VfxDef("31e1448bbc469064e89a752925fc2ea4", "VFX_MagoOscuro_Aparicion",
            "Fase A — grieta de aparición ('el aire frente al altar se rasgó'). Fuente: GabrielAguiarProductions/FreeQuickEffectsVol1 vfx_Portal_02. RETINTAR a violeta oscuro/negro."),
        new VfxDef("3dd50886582244645be87adb42aa8528", "VFX_MagoOscuro_CataclismoBarrido",
            "Fase E — barrido que cubre el escenario entero. Fuente: Hovl Studio 'Ground AOE explosion'. RETINTAR a violeta oscuro/negro."),
        new VfxDef("ff09299caf953d641bf09ba59bb62f09", "VFX_Rebobinado",
            "Fase G — vende visualmente el rebobinado del tiempo. Fuente: Univeral FX Shader 'Vortex'. Ajustar velocidad/color a gusto (no requiere paleta de ningún personaje concreto)."),
        new VfxDef("928ba7472ba727f42b8aeea8b2e89d9e", "VFX_GolpeTraicion",
            "Fase H — golpe de traición del Mago Oscuro sobre Liam. Fuente: 100BestEffectPack 'DarkEffect3' (ya oscuro de origen, sin retinte obligatorio)."),
        new VfxDef("0f3d407feb92ebb49b98d0157de5008a", "VFX_MagoOscuro_Derrota",
            "Derrota/disolución visual del Mago Oscuro. Fuente: GabrielAguiarProductions/FreeQuickEffectsVol1 vfx_Implosion_01. RETINTAR a violeta oscuro/negro."),
        new VfxDef("3535ca9d47f2b634e8d624b4b50de4b8", "VFX_ColapsoAmbiental",
            "Loop de colapso ambiental tras la traición (antes de la destrucción final). Fuente: Hovl Studio 'Dust hemisphere loop' (confirmar en Editor que el loop de origen encaja en duración sin retoque)."),
        new VfxDef("d5774d459f0f43d4591f34994be1404e", "VFX_PortalSendero_Colapso",
            "Escena 21 — el portal del Sendero cerrándose para siempre. Fuente: GabrielAguiarProductions/FreeQuickEffectsVol1 vfx_Portal_01 (variante DISTINTA de VFX_MagoOscuro_Aparicion — mismo pack, visual distinto — para no confundir visualmente 'grieta corrupta' con 'portal de las estrellas')."),
        new VfxDef("b21d155ec7ea5a349a0e36106f913148", "VFX_SacrificioLiam",
            "Sacrificio final de Liam — 'luz consumiéndose a sí misma' (GDD). Fuente: GabrielAguiarProductions/FreeQuickEffectsVol1 vfx_Heal_02. RETINTAR a dorado/blanco cálido."),
        new VfxDef("82066d902ed0dd6488ddfd32eb994801", "VFX_SenderoDestruccion",
            "Destrucción final del Sendero, tras completarse el sacrificio. Fuente: GabrielAguiarProductions/FreeQuickEffectsVol1 vfx_Shockwave_01. Escalar a mano al tamaño real de la escena."),
    };

    [MenuItem("El Sendero/VFX/Crear VFX Cinematicos de la Batalla Final")]
    public static void CreateCinematicVfx()
    {
        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets", "_VFX");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder(RootFolder, "BatallaFinal");

        int created = 0, skipped = 0, missing = 0;
        foreach (var def in Variants)
        {
            var result = CreateVariantIfMissing(def);
            if (result == 1) created++;
            else if (result == 0) skipped++;
            else missing++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[MagoOscuroCinematicVfxBuilder] VFX cinemáticos de la Batalla Final: " + created +
            " creados, " + skipped + " ya existían (sin tocar), " + missing + " con guid de origen no " +
            "encontrado (ver warnings arriba). Reutilizados TAL CUAL sin duplicar (ya encajan en el " +
            "elenco existente): AuraEstelar.prefab (propio hechizo de Will, Fase C — SpellId.AuraEstelar), " +
            "'Star hit' de Hovl Studio (contrahechizo crítico de Will, Fase G), 'Star aura' de Hovl Studio " +
            "(espíritu de Will, epílogo). Pendiente a mano en el Editor: 1) retintar las variantes marcadas " +
            "arriba, 2) ajustar escala/duración una vez la escena esté montada, 3) arrastrar cada prefab al " +
            "campo correspondiente de los tres sequencers de la Batalla Final.");
    }

    private static int CreateVariantIfMissing(VfxDef def)
    {
        var outputPath = OutputFolder + "/" + def.OutputName + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(outputPath) != null)
            return 0; // idempotente: ya existe, no se toca (podría llevar retoques manuales ya hechos)

        var sourcePath = AssetDatabase.GUIDToAssetPath(def.Guid);
        if (string.IsNullOrEmpty(sourcePath))
        {
            Debug.LogWarning("[MagoOscuroCinematicVfxBuilder] No se encontró el asset de origen con guid " +
                def.Guid + " para " + def.OutputName + " (" + def.Note + "). Revisar el guid a mano en el Editor.");
            return -1;
        }

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogWarning("[MagoOscuroCinematicVfxBuilder] El asset en " + sourcePath + " no es un " +
                "GameObject/prefab válido — no se puede crear " + def.OutputName + ".");
            return -1;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        var variant = PrefabUtility.SaveAsPrefabAsset(instance, outputPath, out bool success);
        Object.DestroyImmediate(instance);

        if (!success || variant == null)
        {
            Debug.LogWarning("[MagoOscuroCinematicVfxBuilder] Falló la creación de " + outputPath +
                " a partir de " + sourcePath + ".");
            return -1;
        }

        Debug.Log("[MagoOscuroCinematicVfxBuilder] Creado " + outputPath + " (Prefab Variant de " +
            sourcePath + "). " + def.Note);
        return 1;
    }
}
