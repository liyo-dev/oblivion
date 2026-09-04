using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para cerrar la auditoria de SFX de hechizos pedida por Raul (30 ago
/// 2026, ver claude/revision-sfx-hechizos-2026-08-30.md en el proyecto de Claude): de los 12
/// MagicSpellSO de Assets/_SPELLS/, 5 tenian un castSFXKey que no existia en absoluto en
/// AudioGraphProfile (no sonaba nada) y 3 reusaban la clave de otro hechizo (sonaban identicos
/// a uno distinto, dos de ellos ademas sin sentido tematico: Tornado sonaba a fuego).
///
/// Esta herramienta NO trae los clips de audio por si sola (no hay forma de descargar audio de
/// internet desde esta sesion) -- Raul tiene que guardar los 8 archivos elegidos (ver el mensaje
/// de Claude con los enlaces concretos de Pixabay, licencia Pixabay Content License: uso
/// comercial libre, sin atribucion obligatoria) en Assets/Audio/SFX_Hechizos/, con estos nombres
/// exactos (mp3 o wav, cualquiera de los dos vale):
///   GarraDelPacto | Huracan | MagoOscuroGolpe | MagoOscuroGrieta | SelloDelPacto
///   AuraEstelar | BolaFuego | Tornado
///
/// Al ejecutar el menu:
///   1) Por cada uno de los 8 nombres de arriba, busca el AudioClip en Assets/Audio/SFX_Hechizos/
///      (probando .mp3 y .wav). Si lo encuentra, añade o actualiza esa entrada en
///      AudioGraphProfile.eventSfx (idempotente: si la clave ya existe, solo actualiza el clip).
///      Si no lo encuentra, deja un warning en consola con el nombre que falta y no toca nada de
///      esa clave.
///   2) Para los 3 hechizos que hoy duplican clave (AuraEstelar->BolaPrisma,
///      BolaFuego->LlamaAstral, Tornado->LlamaAstral), reescribe su propio castSFXKey al nombre
///      del propio hechizo (AuraEstelar, BolaFuego, Tornado) -- se hace siempre, aunque el clip
///      correspondiente todavia no se haya añadido, porque el objetivo final es que cada hechizo
///      tenga su propia clave; si el clip aun no esta, ese hechizo se queda sin sonido de cast
///      hasta que se añada (en vez de sonar prestado de otro hechizo). Vuelve a ejecutar este
///      mismo menu despues de añadir el clip que falte -- es idempotente, no rompe nada si se
///      llama varias veces.
///
/// Mismo patron de herramienta de Editor con [MenuItem] que LiamSpellBuilder.cs /
/// LiamZoneSpellBuilder.cs / MagoOscuroSpellBuilder.cs.
/// </summary>
public static class SFXHechizosFixBuilder
{
    private const string AudioFolder = "Assets/Audio/SFX_Hechizos";
    private const string AudioGraphProfilePath = "Assets/_AUDIOPROFILE/AudioGraphProfile.asset";

    // Claves que hoy no suenan porque nunca se dieron de alta en AudioGraphProfile.
    private static readonly string[] MissingKeys =
    {
        "GarraDelPacto", "Huracan", "MagoOscuroGolpe", "MagoOscuroGrieta", "SelloDelPacto",
    };

    // Hechizo -> (clave nueva propia, ruta del asset .asset del hechizo). La clave nueva coincide
    // con el m_Name del propio hechizo para que sea facil de auditar despues.
    private static readonly (string spellAssetPath, string newKey)[] DuplicatedSpells =
    {
        ("Assets/_SPELLS/AuraEstelar.asset", "AuraEstelar"),
        ("Assets/_SPELLS/BolaFuego.asset",   "BolaFuego"),
        ("Assets/_SPELLS/Tornado.asset",     "Tornado"),
    };

    [MenuItem("El Sendero/Audio/Registrar SFX de Hechizos (Faltantes y Duplicados)")]
    public static void FixHechizosSfx()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AudioGraphProfile>(AudioGraphProfilePath);
        if (profile == null)
        {
            Debug.LogError("[SFXHechizosFixBuilder] No se encontro AudioGraphProfile en " + AudioGraphProfilePath);
            return;
        }

        int registered = 0;
        var stillMissing = new System.Collections.Generic.List<string>();

        // Union de las 5 claves que faltaban del todo + las 3 claves nuevas para los duplicados.
        var allKeysToRegister = new System.Collections.Generic.List<string>(MissingKeys);
        foreach (var (_, newKey) in DuplicatedSpells) allKeysToRegister.Add(newKey);

        foreach (string key in allKeysToRegister)
        {
            AudioClip clip = FindClip(key);
            if (clip == null)
            {
                stillMissing.Add(key);
                continue;
            }

            var existing = profile.eventSfx.Find(e => e.eventKey == key);
            if (existing != null)
            {
                existing.sfx = clip;
            }
            else
            {
                profile.eventSfx.Add(new AudioGraphProfile.EventSfx { eventKey = key, sfx = clip });
            }
            registered++;
        }

        if (registered > 0)
        {
            EditorUtility.SetDirty(profile);
        }

        // Paso 2: los 3 hechizos duplicados pasan a usar su propia clave (aunque el clip todavia
        // no exista -- ver comentario de cabecera).
        foreach (var (spellAssetPath, newKey) in DuplicatedSpells)
        {
            var spell = AssetDatabase.LoadAssetAtPath<MagicSpellSO>(spellAssetPath);
            if (spell == null)
            {
                Debug.LogWarning("[SFXHechizosFixBuilder] No se encontro el hechizo en " + spellAssetPath);
                continue;
            }
            if (spell.castSFXKey != newKey)
            {
                var so = new SerializedObject(spell);
                so.FindProperty("castSFXKey").stringValue = newKey;
                so.ApplyModifiedProperties();
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = "[SFXHechizosFixBuilder] " + registered + "/" + allKeysToRegister.Count + " claves de audio registradas en AudioGraphProfile.";
        if (stillMissing.Count > 0)
        {
            summary += " Faltan por añadir en " + AudioFolder + "/: " + string.Join(", ", stillMissing) + " (guarda el .mp3 o .wav con ese nombre exacto y vuelve a ejecutar este menu).";
        }
        Debug.Log(summary);
    }

    private static AudioClip FindClip(string key)
    {
        string[] extensions = { ".mp3", ".wav", ".ogg" };
        foreach (string ext in extensions)
        {
            string path = AudioFolder + "/" + key + ext;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;
        }
        return null;
    }
}
