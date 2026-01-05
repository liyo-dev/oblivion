using UnityEngine;

/// <summary>
/// Perfil de configuración cinematográfica para un diálogo.
/// Define qué planos usar y cuándo cambiar de cámara.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Cinematic Profile", fileName = "DialogueCinematicProfile")]
public class DialogueCinematicProfile : ScriptableObject
{
    [Header("Configuración General")]
    [Tooltip("Si está activo, cambiará automáticamente entre planos")]
    public bool enableAutomaticCuts = true;
    
    [Tooltip("Tiempo entre cortes automáticos (en líneas de diálogo)")]
    [Min(1)]
    public int linesBetweenCuts = 2;
    
    [Tooltip("Variación aleatoria en el timing de cortes (+/- líneas)")]
    [Range(0, 2)]
    public int cutTimingVariation = 1;
    
    [Header("Planos Disponibles")]
    [Tooltip("Plano inicial al comenzar el diálogo")]
    public CinematicCameraShot openingShot = new CinematicCameraShot
    {
        shotType = DialogueShotType.Wide,
        distance = 4f,
        height = 1.6f,
        fieldOfView = 50f
    };
    
    [Tooltip("Planos que se usarán durante el diálogo con el NPC")]
    public CinematicCameraShot[] npcShots = new CinematicCameraShot[]
    {
        new CinematicCameraShot
        {
            shotType = DialogueShotType.MediumNPC,
            distance = 2f,
            height = 1.6f,
            fieldOfView = 45f
        }
    };
    
    [Tooltip("Planos adicionales que se pueden usar aleatoriamente")]
    public CinematicCameraShot[] alternativeShots = new CinematicCameraShot[0];
    
    [Header("Transiciones")]
    [Tooltip("Duración del blend entre cámaras (segundos)")]
    [Range(0.1f, 3f)]
    public float blendDuration = 0.8f;
    
    [Tooltip("Curva de animación del blend")]
    public AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Reglas Cinematográficas")]
    [Tooltip("Evitar cortar del mismo lado (respeta la regla de los 180°)")]
    public bool respectAxisRule = true;
    
    [Tooltip("Preferir planos más cerrados en momentos dramáticos")]
    public bool useEmotionalFraming = true;
    
    [Tooltip("Probabilidad de usar planos alternativos (0-1)")]
    [Range(0f, 1f)]
    public float alternativeShotProbability = 0.2f;

    /// <summary>
    /// Obtiene el siguiente plano para el diálogo con el NPC
    /// </summary>
    public CinematicCameraShot GetNextShot(int lineNumber, int totalLines)
    {
        // Usar planos alternativos ocasionalmente
        if (alternativeShots.Length > 0 && Random.value < alternativeShotProbability)
        {
            return alternativeShots[Random.Range(0, alternativeShots.Length)];
        }
        
        if (npcShots.Length == 0)
        {
            Debug.LogWarning("[DialogueCinematicProfile] No hay planos configurados para el NPC");
            return openingShot;
        }
        
        // Usar framing emocional en las últimas líneas
        if (useEmotionalFraming && lineNumber >= totalLines - 2)
        {
            // Preferir close-ups al final
            foreach (var shot in npcShots)
            {
                if (shot.shotType == DialogueShotType.CloseUpNPC)
                    return shot;
            }
        }
        
        // Selección pseudo-aleatoria pero determinística
        int index = (lineNumber / Mathf.Max(1, linesBetweenCuts)) % npcShots.Length;
        return npcShots[index];
    }
}

