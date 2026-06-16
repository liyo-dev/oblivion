using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Script para bake RÁPIDO de lightmaps para demos
/// Configuración de baja calidad pero resultados aceptables en minutos
/// </summary>
public class QuickDemoBake : EditorWindow
{
    private static LightingSettings originalSettings;
    private static bool settingsModified = false;
    
    [MenuItem("El Sendero/Lighting/🚀 Quick Demo Bake (5-15 min)")]
    public static void StartQuickBake()
    {
        if (!EditorUtility.DisplayDialog(
            "Bake Rápido para Demo",
            "Esto iniciará un baking RÁPIDO con configuración optimizada para demos.\n\n" +
            "✅ VENTAJAS:\n" +
            "- Solo 5-15 minutos (vs horas)\n" +
            "- Suficiente para demos y pruebas\n" +
            "- Mantiene sombras básicas\n\n" +
            "⚠️ LIMITACIONES:\n" +
            "- Menor calidad de iluminación indirecta\n" +
            "- Posible ruido visible de cerca\n" +
            "- Lightmaps más pequeños\n\n" +
            "Configuración:\n" +
            "- Lightmap Size: 512\n" +
            "- Direct Samples: 16\n" +
            "- Indirect Samples: 64\n" +
            "- Bounces: 1\n" +
            "- Denoiser: OpenImage (rápido)\n\n" +
            "¿Continuar?",
            "Sí, Bake Rápido",
            "Cancelar"))
        {
            return;
        }

        Debug.Log("=== INICIANDO BAKE RÁPIDO PARA DEMO ===");
        
        // Guardar escena actual
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("✓ Escena guardada");
        
        // Aplicar configuración rápida
        ApplyQuickSettings();
        
        // Iniciar bake
        Lightmapping.BakeAsync();
        
        Debug.Log("✓ Baking iniciado - Tiempo estimado: 5-15 minutos");
        Debug.Log("💡 TIP: Puedes seguir trabajando, Unity seguirá bakeando en segundo plano");
        
        EditorApplication.Beep();
    }
    
    [MenuItem("El Sendero/Lighting/⚡ Ultra Quick Bake (1-5 min)")]
    public static void StartUltraQuickBake()
    {
        if (!EditorUtility.DisplayDialog(
            "Bake ULTRA Rápido",
            "Esto iniciará un baking ULTRA RÁPIDO.\n\n" +
            "⚡ Solo 1-5 minutos\n" +
            "⚠️ Calidad muy baja - solo para testing\n\n" +
            "Configuración mínima:\n" +
            "- Lightmap Size: 256\n" +
            "- Samples: Mínimos\n" +
            "- Sin bounces\n\n" +
            "¿Continuar?",
            "Sí, Ultra Rápido",
            "Cancelar"))
        {
            return;
        }

        Debug.Log("=== BAKE ULTRA RÁPIDO ===");
        
        EditorSceneManager.SaveOpenScenes();
        ApplyUltraQuickSettings();
        Lightmapping.BakeAsync();
        
        Debug.Log("✓ Baking ultra rápido iniciado - 1-5 minutos");
        EditorApplication.Beep();
    }
    
    [MenuItem("El Sendero/Lighting/📊 Medium Quality Bake (30-60 min)")]
    public static void StartMediumBake()
    {
        if (!EditorUtility.DisplayDialog(
            "Bake Calidad Media",
            "Bake de CALIDAD MEDIA para demos más pulidas.\n\n" +
            "⏱️ Tiempo: 30-60 minutos\n" +
            "✅ Buena calidad para presentaciones\n\n" +
            "Configuración:\n" +
            "- Lightmap Size: 1024\n" +
            "- Direct Samples: 32\n" +
            "- Indirect Samples: 256\n" +
            "- Bounces: 2\n\n" +
            "¿Continuar?",
            "Sí, Calidad Media",
            "Cancelar"))
        {
            return;
        }

        Debug.Log("=== BAKE CALIDAD MEDIA ===");
        
        EditorSceneManager.SaveOpenScenes();
        ApplyMediumSettings();
        Lightmapping.BakeAsync();
        
        Debug.Log("✓ Baking calidad media iniciado - 30-60 minutos");
        EditorApplication.Beep();
    }
    
    private static void ApplyQuickSettings()
    {
        var settings = GetOrCreateLightingSettings();
        if (settings == null) return;
        
        // Configuración rápida para demo
        settings.lightmapMaxSize = 512;
        settings.directSampleCount = 16;
        settings.indirectSampleCount = 64;
        settings.environmentSampleCount = 64;
        settings.maxBounces = 1;
        settings.minBounces = 1;
        settings.lightmapResolution = 15f; // Texels por unidad
        
        // Denoiser rápido
        settings.denoiserTypeDirect = LightingSettings.DenoiserType.OpenImage;
        settings.denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage;
        settings.denoiserTypeAO = LightingSettings.DenoiserType.OpenImage;
        
        // Filtrado básico
        settings.filteringMode = LightingSettings.FilterMode.Auto;
        
        // AO básico
        settings.ao = true;
        settings.aoMaxDistance = 1f;
        
        // Compresión para menor tamaño
        settings.lightmapCompression = LightmapCompression.NormalQuality;
        
        EditorUtility.SetDirty(settings);
        Debug.Log("✓ Configuración RÁPIDA aplicada");
    }
    
    private static void ApplyUltraQuickSettings()
    {
        var settings = GetOrCreateLightingSettings();
        if (settings == null) return;
        
        // Configuración ultra mínima
        settings.lightmapMaxSize = 256;
        settings.directSampleCount = 8;
        settings.indirectSampleCount = 16;
        settings.environmentSampleCount = 16;
        settings.maxBounces = 1;
        settings.minBounces = 1;
        settings.lightmapResolution = 10f;
        
        settings.denoiserTypeDirect = LightingSettings.DenoiserType.OpenImage;
        settings.denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage;
        settings.denoiserTypeAO = LightingSettings.DenoiserType.None; // Sin denoiser para AO
        
        settings.ao = false; // Sin AO para más velocidad
        settings.filteringMode = LightingSettings.FilterMode.None;
        settings.lightmapCompression = LightmapCompression.LowQuality;
        
        EditorUtility.SetDirty(settings);
        Debug.Log("✓ Configuración ULTRA RÁPIDA aplicada");
    }
    
    private static void ApplyMediumSettings()
    {
        var settings = GetOrCreateLightingSettings();
        if (settings == null) return;
        
        // Configuración media - buen balance
        settings.lightmapMaxSize = 1024;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 128;
        settings.maxBounces = 2;
        settings.minBounces = 1;
        settings.lightmapResolution = 20f;
        
        settings.denoiserTypeDirect = LightingSettings.DenoiserType.OpenImage;
        settings.denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage;
        settings.denoiserTypeAO = LightingSettings.DenoiserType.OpenImage;
        
        settings.ao = true;
        settings.aoMaxDistance = 1.5f;
        settings.filteringMode = LightingSettings.FilterMode.Auto;
        settings.lightmapCompression = LightmapCompression.NormalQuality;
        
        EditorUtility.SetDirty(settings);
        Debug.Log("✓ Configuración MEDIA aplicada");
    }
    
    private static LightingSettings GetOrCreateLightingSettings()
    {
        // Intentar obtener los settings actuales de la escena
        var settings = Lightmapping.lightingSettings;
        
        if (settings == null)
        {
            // Buscar el asset existente
            string[] guids = AssetDatabase.FindAssets("MainWorldLightSettings t:LightingSettings");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
                Lightmapping.lightingSettings = settings;
                Debug.Log($"✓ Usando LightingSettings existente: {path}");
            }
            else
            {
                // Crear nuevo si no existe
                settings = new LightingSettings();
                settings.name = "QuickBakeSettings";
                AssetDatabase.CreateAsset(settings, "Assets/QuickBakeSettings.lighting");
                Lightmapping.lightingSettings = settings;
                Debug.Log("✓ Creado nuevo LightingSettings: QuickBakeSettings.lighting");
            }
        }
        
        return settings;
    }
    
    [MenuItem("El Sendero/Lighting/🔄 Restore Production Settings")]
    public static void RestoreProductionSettings()
    {
        var settings = GetOrCreateLightingSettings();
        if (settings == null) return;
        
        // Restaurar configuración de producción
        settings.lightmapMaxSize = 2048;
        settings.directSampleCount = 256;
        settings.indirectSampleCount = 4096;
        settings.environmentSampleCount = 2048;
        settings.maxBounces = 4;
        settings.minBounces = 2;
        settings.lightmapResolution = 25f;
        
        settings.denoiserTypeDirect = LightingSettings.DenoiserType.Optix;
        settings.denoiserTypeIndirect = LightingSettings.DenoiserType.Optix;
        settings.denoiserTypeAO = LightingSettings.DenoiserType.Optix;
        
        settings.ao = true;
        settings.aoMaxDistance = 1f;
        settings.filteringMode = LightingSettings.FilterMode.Advanced;
        settings.lightmapCompression = LightmapCompression.HighQuality;
        
        EditorUtility.SetDirty(settings);
        Debug.Log("✓ Configuración de PRODUCCIÓN restaurada");
        Debug.Log("⚠️ Recuerda: El próximo bake tardará horas");
    }
    
    [MenuItem("El Sendero/Lighting/❌ Cancel Current Bake")]
    public static void CancelBake()
    {
        if (Lightmapping.isRunning)
        {
            Lightmapping.Cancel();
            Debug.Log("⚠️ Baking CANCELADO");
        }
        else
        {
            Debug.Log("No hay baking en progreso");
        }
    }
    
    [MenuItem("El Sendero/Lighting/📈 Show Bake Progress")]
    public static void ShowProgress()
    {
        if (Lightmapping.isRunning)
        {
            float progress = Lightmapping.buildProgress * 100f;
            Debug.Log($"🔄 Baking en progreso: {progress:F1}%");
            
            // Mostrar en ventana emergente también
            EditorUtility.DisplayProgressBar("Baking Lightmaps", $"Progreso: {progress:F1}%", Lightmapping.buildProgress);
            EditorUtility.ClearProgressBar();
        }
        else
        {
            Debug.Log("✓ No hay baking en progreso");
        }
    }
    
    [MenuItem("El Sendero/Lighting/🗑️ Clear Baked Data")]
    public static void ClearBakedData()
    {
        if (EditorUtility.DisplayDialog(
            "Limpiar Datos de Bake",
            "¿Eliminar todos los lightmaps bakeados de la escena actual?\n\nEsto es útil antes de un nuevo bake.",
            "Sí, Limpiar",
            "Cancelar"))
        {
            Lightmapping.Clear();
            Debug.Log("✓ Datos de bake eliminados");
        }
    }
}

