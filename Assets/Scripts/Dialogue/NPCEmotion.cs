using UnityEngine;

/// <summary>
/// Enum que define las emociones disponibles para los NPCs durante los diálogos.
/// Cada emoción activa meshes específicos de ojos y boca (Eye01, Mouth01, etc.)
/// </summary>
public enum NPCEmotion
{
    [Tooltip("Sin cambio de expresión - mantiene la expresión actual")]
    None = -1,
    
    [Tooltip("Expresión neutral/relajada")]
    Neutral = 0,
    
    [Tooltip("Feliz - sonrisa y ojos alegres")]
    Happy = 1,
    
    [Tooltip("Triste - expresión decaída")]
    Sad = 2,
    
    [Tooltip("Enfadado - ceño fruncido")]
    Angry = 3,
    
    [Tooltip("Sorprendido - ojos abiertos y boca en O")]
    Surprised = 4,
    
    [Tooltip("Asustado/Preocupado - expresión de miedo")]
    Scared = 5,
    
    [Tooltip("Pensativo - expresión reflexiva")]
    Thinking = 6,
    
    [Tooltip("Cansado - ojos caídos")]
    Tired = 7,
    
    [Tooltip("Sonrisa maliciosa/pícara")]
    Smirk = 8
}

