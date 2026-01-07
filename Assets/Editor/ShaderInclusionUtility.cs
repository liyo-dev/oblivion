using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utilidad de editor para agregar shaders a "Always Included Shaders" en Graphics Settings.
/// Esto asegura que los shaders estén disponibles en las builds.
/// </summary>
public class ShaderInclusionUtility : EditorWindow
{
    private const string OCCLUSION_SHADER_PATH = "Assets/Resources/Shaders/CameraOcclusionPaint.shader";
    private const string OCCLUSION_SHADER_NAME = "Custom/CameraOcclusionPaint";
    
    [MenuItem("Tools/Sendero/Fix Shader Inclusion")]
    public static void ShowWindow()
    {
        GetWindow<ShaderInclusionUtility>("Shader Inclusion Fix");
    }
    
    [MenuItem("Tools/Sendero/Add Occlusion Shader to Always Included")]
    public static void AddOcclusionShaderToAlwaysIncluded()
    {
        var shader = Shader.Find(OCCLUSION_SHADER_NAME);
        if (shader == null)
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(OCCLUSION_SHADER_PATH);
        }
        
        if (shader == null)
        {
            EditorUtility.DisplayDialog("Error", 
                $"No se encontró el shader '{OCCLUSION_SHADER_NAME}'.\nVerifica que existe en: {OCCLUSION_SHADER_PATH}", 
                "OK");
            return;
        }
        
        if (AddShaderToAlwaysIncluded(shader))
        {
            EditorUtility.DisplayDialog("Éxito", 
                $"Shader '{shader.name}' agregado a 'Always Included Shaders'.\nLas builds ahora incluirán este shader.", 
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Info", 
                $"El shader '{shader.name}' ya estaba en 'Always Included Shaders'.", 
                "OK");
        }
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Shader Inclusion Utility", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "Si los objetos aparecen de color ROSA en las builds, significa que el shader no está incluido.\n\n" +
            "Esta herramienta agrega los shaders necesarios a 'Always Included Shaders' en Graphics Settings.",
            MessageType.Info);
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Verificar Estado del Shader de Oclusión"))
        {
            CheckOcclusionShaderStatus();
        }
        
        GUILayout.Space(5);
        
        if (GUILayout.Button("Agregar Shader de Oclusión a Always Included"))
        {
            AddOcclusionShaderToAlwaysIncluded();
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Abrir Graphics Settings"))
        {
            SettingsService.OpenProjectSettings("Project/Graphics");
        }
        
        GUILayout.Space(20);
        GUILayout.Label("Estado Actual:", EditorStyles.boldLabel);
        
        var shader = Shader.Find(OCCLUSION_SHADER_NAME);
        if (shader != null)
        {
            EditorGUILayout.LabelField("Shader encontrado:", shader.name);
            EditorGUILayout.LabelField("¿Soportado?:", shader.isSupported ? "Sí ✓" : "No ✗");
            
            bool isIncluded = IsShaderInAlwaysIncluded(shader);
            EditorGUILayout.LabelField("¿En Always Included?:", isIncluded ? "Sí ✓" : "No ✗");
            
            if (!isIncluded)
            {
                EditorGUILayout.HelpBox("El shader NO está en 'Always Included Shaders'. Esto causará problemas en builds.", MessageType.Warning);
            }
        }
        else
        {
            EditorGUILayout.HelpBox($"No se encontró el shader '{OCCLUSION_SHADER_NAME}'", MessageType.Error);
        }
    }
    
    private static void CheckOcclusionShaderStatus()
    {
        var shader = Shader.Find(OCCLUSION_SHADER_NAME);
        if (shader == null)
        {
            shader = AssetDatabase.LoadAssetAtPath<Shader>(OCCLUSION_SHADER_PATH);
        }
        
        if (shader == null)
        {
            Debug.LogError($"[ShaderInclusionUtility] ❌ Shader '{OCCLUSION_SHADER_NAME}' NO encontrado");
            return;
        }
        
        Debug.Log($"[ShaderInclusionUtility] ✓ Shader encontrado: {shader.name}");
        Debug.Log($"[ShaderInclusionUtility] ¿Soportado?: {shader.isSupported}");
        Debug.Log($"[ShaderInclusionUtility] ¿En Always Included?: {IsShaderInAlwaysIncluded(shader)}");
    }
    
    private static bool IsShaderInAlwaysIncluded(Shader shader)
    {
        var graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>(
            "ProjectSettings/GraphicsSettings.asset");
        
        if (graphicsSettings == null) return false;
        
        var serializedObject = new SerializedObject(graphicsSettings);
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
        
        if (arrayProp == null) return false;
        
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == shader)
                return true;
        }
        
        return false;
    }
    
    private static bool AddShaderToAlwaysIncluded(Shader shader)
    {
        // Cargar GraphicsSettings
        var graphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath(graphicsSettingsPath);
        
        if (graphicsSettings == null || graphicsSettings.Length == 0)
        {
            Debug.LogError("[ShaderInclusionUtility] No se pudo cargar GraphicsSettings");
            return false;
        }
        
        SerializedObject serializedObject = null;
        foreach (var obj in graphicsSettings)
        {
            if (obj.GetType().Name == "GraphicsSettings")
            {
                serializedObject = new SerializedObject(obj);
                break;
            }
        }
        
        if (serializedObject == null)
        {
            Debug.LogError("[ShaderInclusionUtility] No se encontró GraphicsSettings en el asset");
            return false;
        }
        
        var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");
        
        if (arrayProp == null)
        {
            Debug.LogError("[ShaderInclusionUtility] No se encontró la propiedad m_AlwaysIncludedShaders");
            return false;
        }
        
        // Verificar si ya está incluido
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            var element = arrayProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue == shader)
            {
                Debug.Log($"[ShaderInclusionUtility] Shader '{shader.name}' ya está en Always Included Shaders");
                return false;
            }
        }
        
        // Agregar el shader
        int newIndex = arrayProp.arraySize;
        arrayProp.InsertArrayElementAtIndex(newIndex);
        var newElement = arrayProp.GetArrayElementAtIndex(newIndex);
        newElement.objectReferenceValue = shader;
        
        serializedObject.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        
        Debug.Log($"[ShaderInclusionUtility] ✓ Shader '{shader.name}' agregado a Always Included Shaders");
        return true;
    }
}

