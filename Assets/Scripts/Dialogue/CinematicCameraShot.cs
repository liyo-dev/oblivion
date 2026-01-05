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
    
    [Header("Posición Relativa")]
    [Tooltip("Distancia desde el objetivo")]
    public float distance = 3f;
    
    [Tooltip("Altura relativa respecto al objetivo")]
    public float height = 1.6f;
    
    [Tooltip("Offset lateral (positivo = derecha, negativo = izquierda)")]
    public float lateralOffset = 0f;
    
    [Header("Rotación")]
    [Tooltip("Ángulo vertical de la cámara (pitch)")]
    [Range(-45f, 45f)]
    public float verticalAngle = 0f;
    
    [Tooltip("Offset del punto de mira (look-at target offset)")]
    public Vector3 lookAtOffset = Vector3.up * 1.5f;
    
    [Header("Composición")]
    [Tooltip("Field of View para este plano")]
    [Range(20f, 90f)]
    public float fieldOfView = 50f;
    
    [Tooltip("Dutch angle (rotación en Z para efecto dramático)")]
    [Range(-20f, 20f)]
    public float dutchAngle = 0f;
    
    [Header("Duración")]
    [Tooltip("Duración mínima de este plano en segundos (0 = hasta siguiente línea)")]
    [Min(0f)]
    public float minimumDuration = 0f;
}

