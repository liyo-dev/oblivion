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
        
        // Configurar valores por defecto
        profile.enableAutomaticCuts = true;
        profile.linesBetweenCuts = 2;
        profile.cutTimingVariation = 1;
        
        // Plano de apertura
        profile.openingShot = new CinematicCameraShot
        {
            shotType = DialogueShotType.Wide,
            distance = 4f,
            height = 1.6f,
            fieldOfView = 50f
        };
        
        // Planos del NPC
        profile.npcShots = new CinematicCameraShot[]
        {
            new CinematicCameraShot
            {
                shotType = DialogueShotType.MediumNPC,
                distance = 2.5f,
                height = 1.6f,
                fieldOfView = 45f
            },
            new CinematicCameraShot
            {
                shotType = DialogueShotType.CloseUpNPC,
                distance = 1.2f,
                height = 1.65f,
                fieldOfView = 35f,
                lookAtOffset = Vector3.up * 1.65f
            }
        };
        
        // Planos alternativos
        profile.alternativeShots = new CinematicCameraShot[]
        {
            new CinematicCameraShot
            {
                shotType = DialogueShotType.OverShoulderPlayer,
                distance = 1.5f,
                height = 1.6f,
                lateralOffset = 0.3f,
                verticalAngle = 5f,
                fieldOfView = 50f,
                lookAtOffset = Vector3.up * 1.6f
            },
            new CinematicCameraShot
            {
                shotType = DialogueShotType.OverShoulderNPC,
                distance = 1.5f,
                height = 1.6f,
                lateralOffset = -0.3f,
                verticalAngle = 5f,
                fieldOfView = 50f,
                lookAtOffset = Vector3.up * 1.6f
            }
        };
        
        // Configuración de transiciones
        profile.blendDuration = 0.8f;
        profile.blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        // Reglas cinematográficas
        profile.respectAxisRule = true;
        profile.useEmotionalFraming = true;
        profile.alternativeShotProbability = 0.2f;
        
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

