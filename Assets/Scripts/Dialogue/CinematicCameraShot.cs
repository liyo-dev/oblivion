using UnityEngine;

/// <summary>
/// Tipos de planos cinematográficos típicos en conversaciones
/// </summary>
public enum DialogueShotType
{
    /// <summary>
    /// Plano general: muestra a ambos personajes en la escena
    /// </summary>
    Wide,
    
    /// <summary>
    /// Plano medio del NPC
    /// </summary>
    MediumNPC,
    
    /// <summary>
    /// Primer plano del NPC (close-up)
    /// </summary>
    CloseUpNPC,
    
    /// <summary>
    /// Over-the-shoulder: mira al NPC desde detrás del hombro del jugador
    /// </summary>
    OverShoulderPlayer,
    
    /// <summary>
    /// Over-the-shoulder: mira al jugador desde detrás del hombro del NPC
    /// </summary>
    OverShoulderNPC,
    
    /// <summary>
    /// Plano lateral que muestra perfiles
    /// </summary>
    Profile,
    
    /// <summary>
    /// Plano personalizado definido manualmente
    /// </summary>
    Custom
}

/// <summary>
/// Define un plano cinematográfico con su configuración
/// </summary>
[System.Serializable]
public class CinematicCameraShot
{
    [Tooltip("Tipo de plano cinematográfico")]
    public DialogueShotType shotType = DialogueShotType.Wide;
    
    [Tooltip("Duración mínima de este plano en segundos (0 = hasta siguiente línea)")]
    [Min(0f)]
    public float minimumDuration = 0f;
    
    // Valores predefinidos según el tipo de plano (no visibles en Inspector)
    public float Distance => GetPredefinedDistance();
    public float Height => GetPredefinedHeight();
    public float LateralOffset => GetPredefinedLateralOffset();
    public float VerticalAngle => GetPredefinedVerticalAngle();
    public Vector3 LookAtOffset => GetPredefinedLookAtOffset();
    public float FieldOfView => GetPredefinedFOV();
    public float DutchAngle => 0f; // Siempre 0 por defecto
    
    private float GetPredefinedDistance()
    {
        return shotType switch
        {
            DialogueShotType.Wide => 4f,
            DialogueShotType.MediumNPC => 2.5f,
            DialogueShotType.CloseUpNPC => 1.2f,
            DialogueShotType.OverShoulderPlayer => 2f,
            DialogueShotType.OverShoulderNPC => 2f,
            DialogueShotType.Profile => 3f,
            _ => 3f
        };
    }
    
    private float GetPredefinedHeight()
    {
        return shotType switch
        {
            DialogueShotType.Wide => 1.8f,           // Aumentado de 1.6f
            DialogueShotType.MediumNPC => 0.7f,      // Ajustado a 0.7f según solicitud
            DialogueShotType.CloseUpNPC => 1.75f,    // Aumentado de 1.6f
            DialogueShotType.OverShoulderPlayer => 1.7f, // Aumentado de 1.5f
            DialogueShotType.OverShoulderNPC => 1.7f,    // Aumentado de 1.5f
            DialogueShotType.Profile => 1.8f,       // Aumentado de 1.6f
            _ => 1.7f
        };
    }
    
    private float GetPredefinedLateralOffset()
    {
        return shotType switch
        {
            DialogueShotType.OverShoulderPlayer => 0.5f,
            DialogueShotType.OverShoulderNPC => -0.5f,
            DialogueShotType.Profile => 2f,
            _ => 0f
        };
    }
    
    private float GetPredefinedVerticalAngle()
    {
        return shotType switch
        {
            DialogueShotType.CloseUpNPC => -5f,
            DialogueShotType.Wide => 5f,
            _ => 0f
        };
    }
    
    private Vector3 GetPredefinedLookAtOffset()
    {
        return shotType switch
        {
            DialogueShotType.CloseUpNPC => Vector3.up * 1.7f,  // Aumentado de 1.6f - mirar a la cara
            DialogueShotType.Wide => Vector3.up * 1.4f,       // Aumentado de 1.2f
            DialogueShotType.MediumNPC => Vector3.up * 1.65f, // Añadido - mirar a la cara
            _ => Vector3.up * 1.6f                            // Aumentado de 1.5f
        };
    }
    
    private float GetPredefinedFOV()
    {
        return shotType switch
        {
            DialogueShotType.Wide => 55f,
            DialogueShotType.MediumNPC => 45f,
            DialogueShotType.CloseUpNPC => 40f,
            DialogueShotType.OverShoulderPlayer => 50f,
            DialogueShotType.OverShoulderNPC => 50f,
            DialogueShotType.Profile => 50f,
            _ => 50f
        };
    }
}

