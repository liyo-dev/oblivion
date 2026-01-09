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
            DialogueShotType.Wide => 1.0f,           // Vista general - altura media (antes: 1.5f)
            DialogueShotType.MediumNPC => 0.95f,     // Medium shot - altura de cara/ojos (antes: 1.35f)
            DialogueShotType.CloseUpNPC => 1.05f,    // Close-up - ligeramente más alto (antes: 1.45f)
            DialogueShotType.OverShoulderPlayer => 1.0f,  // Altura de hombros (antes: 1.4f)
            DialogueShotType.OverShoulderNPC => 1.0f,     // Altura de hombros (antes: 1.4f)
            DialogueShotType.Profile => 1.0f,        // Altura de cara para perfil (antes: 1.4f)
            _ => 0.95f                                // Por defecto (antes: 1.35f)
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
            DialogueShotType.OverShoulderPlayer => -8f,  // Añadido: ángulo hacia abajo para ver mejor al NPC
            DialogueShotType.OverShoulderNPC => -8f,     // Añadido: ángulo hacia abajo para ver mejor al Player
            _ => 0f
        };
    }
    
    private Vector3 GetPredefinedLookAtOffset()
    {
        return shotType switch
        {
            DialogueShotType.CloseUpNPC => Vector3.up * 0.80f,  // Cara completa (antes: 1.20f, -0.4m)
            DialogueShotType.Wide => Vector3.up * 0.75f,        // Altura media (antes: 1.15f, -0.4m)
            DialogueShotType.MediumNPC => Vector3.up * 0.70f,   // Centro de cara (antes: 1.10f, -0.4m)
            _ => Vector3.up * 0.70f                             // Por defecto (antes: 1.10f, -0.4m)
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

