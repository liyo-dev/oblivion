using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilidad de un solo uso para silenciar los warnings de consola:
/// "Serialized files [version N] before 2019.1 are deprecated. Open and re-save the file: ..."
/// Estos assets (materiales importados junto con paquetes de la Asset Store antiguos)
/// nunca se volvieron a guardar desde que se importaron, así que siguen en un formato
/// serializado viejo. Forzar su reserialización los actualiza al formato actual sin
/// tocar ningún valor del material.
/// </summary>
public static class ReserializeOldAssets
{
    private const string FantasyKingdomMaterialsPath = "Assets/Art/World/Fantasy_Kingdom_Pack/Materials";

    [MenuItem("El Sendero/Utilidades/Reserializar materiales de Fantasy Kingdom Pack")]
    public static void ReserializeFantasyKingdomMaterials()
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { FantasyKingdomMaterialsPath });
        ReserializeGuids(guids, "Fantasy Kingdom Pack/Materials");
    }

    /// <summary>
    /// Versión más agresiva: reserializa TODOS los assets del proyecto (materiales, prefabs,
    /// escenas, SOs, etc.). Úsala solo si siguen apareciendo warnings de este tipo en otras
    /// carpetas después de correr la opción de arriba. Puede tardar varios minutos en
    /// proyectos grandes y modificará el timestamp de muchos archivos (ruido en el diff de git).
    /// </summary>
    [MenuItem("El Sendero/Utilidades/Reserializar TODOS los assets del proyecto (lento)")]
    public static void ReserializeAllProjectAssets()
    {
        if (!EditorUtility.DisplayDialog(
                "Reserializar todos los assets",
                "Esto va a reescribir todos los assets del proyecto y puede generar un diff " +
                "de git enorme (aunque los valores no cambien). ¿Continuar?",
                "Sí, continuar", "Cancelar"))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { "Assets" });
        ReserializeGuids(guids, "proyecto completo");
    }

    private static void ReserializeGuids(string[] guids, string label)
    {
        var paths = new string[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
        }

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);
        AssetDatabase.SaveAssets();

        Debug.Log($"[ReserializeOldAssets] Reserializados {paths.Length} assets ({label}). " +
                   "Los warnings de 'Serialized files before 2019.1 are deprecated' deberían desaparecer.");
    }
}
