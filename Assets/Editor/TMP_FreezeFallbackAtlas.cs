// Assets/Editor/TMP_FreezeFallbackAtlas.cs
//
// Congela los Font Assets de TextMeshPro que están en modo Dynamic (p.ej.
// "LiberationSans SDF - Fallback") pasándolos a modo Static, después de
// hornear en su atlas todos los caracteres que aparecen de verdad en los
// textos del proyecto (diálogos, quests, items, hechizos, UI, etc.).
//
// Por qué existe esto: los Font Assets con Atlas Population Mode = Dynamic
// generan glifos nuevos en tiempo de ejecución y los escriben DE VERDAD en
// el asset (textura + tabla de glifos), no en una copia temporal de Play
// Mode. Por eso el asset queda "sucio" cada vez que entras en Play y usas
// un carácter no cacheado todavía (tildes, ñ, ¿¡...), y Unity pide guardar
// cambios al pulsar Stop. Además esos glifos añadidos en el Editor se
// BORRAN en la build final (Clear Dynamic Data On Build), así que un
// carácter que solo funciona gracias al fallback dinámico puede desaparecer
// en el juego compilado.
//
// Uso: El Sendero > Herramientas > Congelar Fuentes TMP (Dynamic -> Static)
// Vuelve a ejecutarlo cada vez que añadas diálogos/textos con caracteres
// nuevos, y siempre antes de sacar una build.

using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class TMP_FreezeFallbackAtlas
{
    // Carpetas del proyecto que contienen texto visible en juego.
    // Añade aquí cualquier carpeta nueva de contenido con texto.
    private static readonly string[] CarpetasConTexto =
    {
        "Assets/_DIALOGUES",
        "Assets/_QUEST",
        "Assets/_NPCs",
        "Assets/_ITEMS",
        "Assets/_SPELLS",
        "Assets/_ENEMY_SPELLS",
        "Assets/_SHOPS",
        "Assets/_UI",
        "Assets/_WARDROBE ITEMS",
        "Assets/_LORE POP UP CONFIG",
        "Assets/NarrativeGraph",
    };

    // Caracteres base que queremos garantizar siempre, aunque hoy no
    // aparezcan literalmente en ningún texto (evita sorpresas al escribir
    // diálogo nuevo mañana).
    private const string CaracteresBase =
        " ABCDEFGHIJKLMNOPQRSTUVWXYZÁÉÍÓÚÑÜ" +
        "abcdefghijklmnopqrstuvwxyzáéíóúñü" +
        "0123456789" +
        ".,;:!¡?¿'\"«»“”‘’-–—_()[]{}/\\%&@#*+=<>°ºª…";

    [MenuItem("El Sendero/Herramientas/Congelar Fuentes TMP (Dynamic -> Static)")]
    public static void CongelarFuentesDinamicas()
    {
        var caracteres = RecolectarCaracteresDelProyecto();

        var fontAssetGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int fuentesCongeladas = 0;
        var resumen = new StringBuilder();

        foreach (var guid in fontAssetGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fontAsset == null) continue;
            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic) continue;

            bool exito = fontAsset.TryAddCharacters(caracteres, out string faltantes);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);

            fuentesCongeladas++;
            resumen.AppendLine(exito
                ? $"- {path}: OK, todos los caracteres cupieron en el atlas."
                : $"- {path}: el atlas se quedó sin sitio. Faltan: {faltantes}  " +
                  "(sube Atlas Width/Height en el Font Asset y vuelve a ejecutar este menú).");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (fuentesCongeladas == 0)
        {
            Debug.Log("[TMP_FreezeFallbackAtlas] No se encontró ningún Font Asset en modo Dynamic. Nada que congelar.");
            return;
        }

        Debug.Log(
            $"[TMP_FreezeFallbackAtlas] Caracteres únicos recolectados del proyecto: {caracteres.Length}.\n" +
            $"Font Assets pasados de Dynamic a Static: {fuentesCongeladas}.\n{resumen}");
    }

    private static string RecolectarCaracteresDelProyecto()
    {
        var set = new HashSet<char>();
        foreach (var c in CaracteresBase) set.Add(c);

        var guids = AssetDatabase.FindAssets("t:ScriptableObject", CarpetasConTexto);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (so == null) continue;
            ExtraerStringsPorReflexion(so, set);
        }

        return new string(new List<char>(set).ToArray());
    }

    // Recorre por reflexión todos los campos string (incluyendo los que
    // están dentro de arrays/listas de clases serializables) de un
    // ScriptableObject y añade cada carácter encontrado al set. Así no hace
    // falta conocer de antemano el tipo exacto de cada asset de diálogo,
    // quest o item.
    //
    // IMPORTANTE: cualquier referencia a un UnityEngine.Object (Transform,
    // GameObject, Sprite, otro ScriptableObject, etc.) se ignora sin
    // recorrerla. Transform en concreto implementa IEnumerable (itera sus
    // hijos), así que si no se filtra ANTES de comprobar IEnumerable, una
    // referencia nula a un Transform revienta con NullReferenceException al
    // intentar enumerarla. También evita salirnos del propio asset hacia
    // otros objetos/escenas sin querer.
    private static void ExtraerStringsPorReflexion(object obj, HashSet<char> set, int profundidad = 0)
    {
        if (obj == null || profundidad > 4) return;

        var tipo = obj.GetType();
        if (tipo.Namespace != null && tipo.Namespace.StartsWith("UnityEngine")) return;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        foreach (var field in tipo.GetFields(flags))
        {
            if (field.IsNotSerialized) continue;

            object valor;
            try { valor = field.GetValue(obj); }
            catch { continue; }

            if (valor == null) continue;

            if (valor is string str)
            {
                foreach (var c in str) set.Add(c);
                continue;
            }

            // Referencias a assets/objetos de Unity: no se recorren.
            if (valor is Object) continue;

            if (valor is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (item is string s2)
                    {
                        foreach (var c in s2) set.Add(c);
                    }
                    else if (item is Object)
                    {
                        // ignorar referencias a assets/objetos de Unity
                    }
                    else if (item.GetType().IsClass)
                    {
                        ExtraerStringsPorReflexion(item, set, profundidad + 1);
                    }
                }
                continue;
            }

            if (valor.GetType().IsClass)
            {
                ExtraerStringsPorReflexion(valor, set, profundidad + 1);
            }
        }
    }
}
