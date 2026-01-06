using UnityEngine;

/// <summary>
/// Este ScriptableObject existe únicamente para forzar a Unity a incluir
/// ciertos shaders en la build que de otra forma serían "stripped" 
/// porque solo se referencian dinámicamente via Shader.Find()
/// </summary>
[CreateAssetMenu(fileName = "ShaderReferences", menuName = "Game/Shader References")]
public class ShaderReferences : ScriptableObject
{
    [Header("Shaders que deben incluirse en la build")]
    [Tooltip("Estos shaders se referencian dinámicamente y Unity los eliminaría sin esta referencia directa")]
    public Shader[] requiredShaders;
    
    private static ShaderReferences _instance;
    
    /// <summary>
    /// Obtiene un shader de la lista de referencias
    /// </summary>
    public static Shader GetShader(string shaderName)
    {
        // Primero intentar Shader.Find (funciona en editor)
        var shader = Shader.Find(shaderName);
        if (shader != null) return shader;
        
        // Cargar la instancia si no está cargada
        if (_instance == null)
        {
            _instance = Resources.Load<ShaderReferences>("ShaderReferences");
        }
        
        // Buscar en la lista de shaders requeridos
        if (_instance != null && _instance.requiredShaders != null)
        {
            foreach (var s in _instance.requiredShaders)
            {
                if (s != null && s.name == shaderName)
                    return s;
            }
        }
        
        return null;
    }
}

