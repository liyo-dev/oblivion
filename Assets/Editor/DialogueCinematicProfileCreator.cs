using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Crea el perfil cinematográfico por defecto si no existe
/// </summary>
public class DialogueCinematicProfileCreator
{
    [MenuItem("Tools/Dialogue/Create Default Cinematic Profile")]
    public static void CreateDefaultProfile()
    {
        string path = "Assets/Scripts/Dialogue/DefaultDialogueCinematic.asset";
        
        // Verificar si ya existe
        var existing = AssetDatabase.LoadAssetAtPath<DialogueCinematicProfile>(path);
        if (existing != null)
        {
            Debug.LogWarning("[DialogueCinematicProfileCreator] El perfil ya existe en: " + path);
            Selection.activeObject = existing;
            return;
        }
        
        // Crear nuevo perfil
        var profile = ScriptableObject.CreateInstance<DialogueCinematicProfile>();
        
        // Plano de apertura (Wide)
        profile.openingShot = new CinematicCameraShot
        {
            shotType = DialogueShotType.Wide,
            minimumDuration = 0f
        };
        
        // Planos del NPC (Medium y CloseUp)
        profile.npcShots = new CinematicCameraShot[]
        {
            new CinematicCameraShot
            {
                shotType = DialogueShotType.MediumNPC,
                minimumDuration = 0f
            },
            new CinematicCameraShot
            {
                shotType = DialogueShotType.CloseUpNPC,
                minimumDuration = 0f
            }
        };
        
        // Configuración de transiciones
        profile.blendDuration = 0.8f;
        profile.chainedDialogueDelay = 0.3f;
        
        // Crear el directorio si no existe
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Guardar el asset
        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"[DialogueCinematicProfileCreator] ✅ Perfil creado exitosamente en: {path}");
        
        // Seleccionarlo en el Project
        Selection.activeObject = profile;
        EditorGUIUtility.PingObject(profile);
    }
}

