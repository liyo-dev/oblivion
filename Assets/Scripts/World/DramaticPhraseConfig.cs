using UnityEngine;

public enum DramaticTextStyle
{
    Memory,   // Grisáceo, italic — recuerdos lejanos
    Dark,     // Rojizo/oscuro — presencia amenazante
    Urgent,   // Blanco puro — llamada de urgencia
    Epic      // Dorado — momento grandioso
}

public enum DramaticTextBackground
{
    None,
    SemiBlack,  // Alpha ~0.65
    FullBlack   // Alpha 1.0
}

public enum DramaticEntryAnimation
{
    ScaleUp,        // Pequeño → tamaño normal
    FadeIn,         // Opacidad 0 → 1
    TypeWriter,     // Carácter a carácter
    Instant,        // Sin animación
    SlideFromLeft,  // Entra deslizándose desde el borde izquierdo
    SlideFromRight  // Entra deslizándose desde el borde derecho
}

public enum DramaticExitAnimation
{
    FadeOut,      // Opacidad 1 → 0
    ScaleUp,      // Crece y desvanece (épico que se disuelve)
    Instant,      // Corte directo
    SlideToLeft,  // Sale deslizándose por el borde izquierdo
    SlideToRight  // Sale deslizándose por el borde derecho
}

[System.Serializable]
public struct DramaticPhrase
{
    [Tooltip("Texto de la frase. Usa textId para localización.")]
    [TextArea(1, 3)]
    public string text;

    [Tooltip("ID de localización. Si no está vacío, sobreescribe 'text'.")]
    public string textId;

    [Tooltip("Estilo visual de la frase.")]
    public DramaticTextStyle style;

    [Tooltip("Fondo de pantalla durante esta frase.")]
    public DramaticTextBackground background;

    [Tooltip("Cómo aparece el texto en pantalla.")]
    public DramaticEntryAnimation entryAnim;

    [Tooltip("Cómo desaparece el texto de pantalla.")]
    public DramaticExitAnimation exitAnim;

    [Tooltip("Segundos que la frase permanece visible (ignorado si waitForAudio es true).")]
    [Min(0.1f)]
    public float duration;

    [Tooltip("Clip de voz que suena al mostrar la frase.")]
    public AudioClip voiceClip;

    [Tooltip("Si true, la frase dura exactamente lo que dure el clip de audio.")]
    public bool waitForAudio;

    [Tooltip("Posición inicial del texto en el canvas (píxeles UI). El texto empieza aquí.")]
    public Vector2 positionOffset;

    [Tooltip("Si true, el texto se desplaza desde positionOffset hasta moveTo durante el tiempo que está visible.")]
    public bool useMovement;

    [Tooltip("Posición final del texto en el canvas (píxeles UI). Solo se usa si useMovement es true.")]
    public Vector2 moveTo;
}

[CreateAssetMenu(menuName = "El Sendero/UI/Dramatic Text Config", fileName = "DramaticText_")]
public class DramaticPhraseConfig : ScriptableObject
{
    [Tooltip("Frases que se mostrarán en secuencia.")]
    public DramaticPhrase[] phrases;

    [Tooltip("Pausa entre frases consecutivas (segundos).")]
    [Min(0f)]
    public float pauseBetween = 0.15f;
}
