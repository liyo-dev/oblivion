using UnityEngine;

/// <summary>
/// Perfil de configuración cinematográfica para un diálogo.
/// Define qué planos usar y cuándo cambiar de cámara.
/// Simplificado para mostrar solo lo esencial.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Cinematic Profile", fileName = "DialogueCinematicProfile")]
public class DialogueCinematicProfile : ScriptableObject
{
    [Header("Planos del Diálogo")]
    [Tooltip("Plano inicial al comenzar el diálogo")]
    public CinematicCameraShot openingShot = new CinematicCameraShot
    {
        shotType = DialogueShotType.Wide,
        minimumDuration = 0f
    };
    
    [Tooltip("Planos que se usarán durante el diálogo con el NPC")]
    public CinematicCameraShot[] npcShots = new CinematicCameraShot[]
    {
        new CinematicCameraShot
        {
            shotType = DialogueShotType.MediumNPC,
            minimumDuration = 0f
        }
    };
    
    [Header("Configuración de Transiciones")]
    [Tooltip("Duración del blend entre cámaras (segundos)")]
    [Range(0.1f, 3f)]
    public float blendDuration = 0.8f;
    
    [Tooltip("Delay antes de cambiar de cámara cuando se encadenan diálogos (evita parpadeos)")]
    [Range(0f, 2f)]
    public float chainedDialogueDelay = 0.3f;

    // Valores predefinidos (no visibles en Inspector)
    private const bool EnableAutomaticCuts = true;
    private const int LinesBetweenCuts = 2;
    private const float AlternativeShotProbability = 0.2f;
    private const bool UseEmotionalFraming = true;

    /// <summary>
    /// Obtiene el siguiente plano para el diálogo con el NPC
    /// </summary>
    public CinematicCameraShot GetNextShot(int lineNumber, int totalLines)
    {
        if (npcShots.Length == 0)
        {
            Debug.LogWarning("[DialogueCinematicProfile] No hay planos configurados para el NPC");
            return openingShot;
        }
        
        // Usar framing emocional en las últimas líneas
        if (UseEmotionalFraming && lineNumber >= totalLines - 2)
        {
            // Preferir close-ups al final
            foreach (var shot in npcShots)
            {
                if (shot.shotType == DialogueShotType.CloseUpNPC)
                    return shot;
            }
        }
        
        // Selección pseudo-aleatoria pero determinística
        int index = (lineNumber / Mathf.Max(1, LinesBetweenCuts)) % npcShots.Length;
        return npcShots[index];
    }
    
    /// <summary>
    /// Indica si debe haber un delay antes de activar la cámara cinemática
    /// (útil para evitar parpadeos cuando se encadenan diálogos)
    /// </summary>
    public bool ShouldDelayActivation => chainedDialogueDelay > 0f;
}
