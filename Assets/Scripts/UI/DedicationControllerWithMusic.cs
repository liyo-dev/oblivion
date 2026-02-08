using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

/// <summary>
/// Versión extendida del DedicationController con soporte para música de fondo
/// NOTA: Este es un archivo OPCIONAL. Usa este en lugar del DedicationController.cs original
/// si deseas agregar música a la escena de dedicatoria.
/// </summary>
public class DedicationControllerWithMusic : MonoBehaviour
{
    [Header("Referencias de Texto")]
    [Tooltip("Primera línea de texto: 'A todas mis estrellas.'")]
    public TextMeshProUGUI textLine1;
    
    [Tooltip("Segunda línea de texto: 'A quienes hacen mi mundo un poco más brillante.'")]
    public TextMeshProUGUI textLine2;

    [Header("Configuración de Tiempos")]
    [Tooltip("Duración del efecto fade in/out en segundos")]
    [SerializeField] private float fadeDuration = 1.5f;
    
    [Tooltip("Tiempo de espera para leer el texto en segundos")]
    [SerializeField] private float readingTime = 3f;
    
    [Tooltip("Tiempo de espera inicial antes de mostrar el texto")]
    [SerializeField] private float initialDelay = 1f;
    
    [Tooltip("Tiempo de espera entre la primera y segunda línea")]
    [SerializeField] private float delayBetweenLines = 0.5f;

    [Header("Configuración de Escena")]
    [Tooltip("Nombre de la escena del menú principal a cargar")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Audio (Opcional)")]
    [Tooltip("Fuente de audio para música de fondo")]
    [SerializeField] private AudioSource backgroundMusic;
    
    [Tooltip("¿Hacer fade in de la música?")]
    [SerializeField] private bool fadeInMusic = true;
    
    [Tooltip("Duración del fade in de música")]
    [SerializeField] private float musicFadeDuration = 2f;
    
    [Tooltip("Volumen máximo de la música (0-1)")]
    [SerializeField] private float maxMusicVolume = 0.5f;

    private void Start()
    {
        // Validar referencias
        if (textLine1 == null || textLine2 == null)
        {
            Debug.LogError("[DedicationController] ¡Error! Faltan referencias a los textos. Por favor asigna textLine1 y textLine2 en el Inspector.");
            return;
        }

        // Inicializar ambos textos con opacidad 0
        SetTextAlpha(textLine1, 0f);
        SetTextAlpha(textLine2, 0f);

        // Configurar música si está asignada
        if (backgroundMusic != null)
        {
            backgroundMusic.volume = fadeInMusic ? 0f : maxMusicVolume;
            if (!backgroundMusic.isPlaying)
            {
                backgroundMusic.Play();
            }
        }

        // Iniciar la secuencia de dedicatoria
        StartCoroutine(DedicationSequence());
    }

    /// <summary>
    /// Secuencia principal de la dedicatoria
    /// </summary>
    private IEnumerator DedicationSequence()
    {
        // Fade in de música (si está habilitado)
        Coroutine musicFade = null;
        if (backgroundMusic != null && fadeInMusic)
        {
            musicFade = StartCoroutine(FadeInMusic(backgroundMusic, musicFadeDuration, maxMusicVolume));
        }

        // 1. Espera inicial en negro
        yield return new WaitForSeconds(initialDelay);

        // 2. Fade In del textLine1
        yield return StartCoroutine(FadeIn(textLine1, fadeDuration));

        // 3. Espera entre líneas
        yield return new WaitForSeconds(delayBetweenLines);

        // 4. Fade In del textLine2
        yield return StartCoroutine(FadeIn(textLine2, fadeDuration));

        // 5. Espera para que el jugador lea
        yield return new WaitForSeconds(readingTime);

        // 6. Fade Out de ambos textos y música simultáneamente
        Coroutine textsFadeOut = StartCoroutine(FadeOutBoth(textLine1, textLine2, fadeDuration));
        
        if (backgroundMusic != null)
        {
            StartCoroutine(FadeOutMusic(backgroundMusic, fadeDuration));
        }
        
        yield return textsFadeOut;

        // 7. Cargar la escena del menú principal
        LoadMainMenu();
    }

    /// <summary>
    /// Realiza un fade in suave de un texto
    /// </summary>
    private IEnumerator FadeIn(TextMeshProUGUI text, float duration)
    {
        float elapsedTime = 0f;
        Color color = text.color;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / duration);
            color.a = alpha;
            text.color = color;
            yield return null;
        }

        // Asegurar que el alpha final sea exactamente 1
        color.a = 1f;
        text.color = color;
    }

    /// <summary>
    /// Realiza un fade out suave de un texto
    /// </summary>
    private IEnumerator FadeOut(TextMeshProUGUI text, float duration)
    {
        float elapsedTime = 0f;
        Color color = text.color;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / duration));
            color.a = alpha;
            text.color = color;
            yield return null;
        }

        // Asegurar que el alpha final sea exactamente 0
        color.a = 0f;
        text.color = color;
    }

    /// <summary>
    /// Realiza un fade out simultáneo de dos textos
    /// </summary>
    private IEnumerator FadeOutBoth(TextMeshProUGUI text1, TextMeshProUGUI text2, float duration)
    {
        float elapsedTime = 0f;
        Color color1 = text1.color;
        Color color2 = text2.color;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / duration));
            
            color1.a = alpha;
            text1.color = color1;
            
            color2.a = alpha;
            text2.color = color2;
            
            yield return null;
        }

        // Asegurar que ambos alpha finales sean exactamente 0
        color1.a = 0f;
        text1.color = color1;
        
        color2.a = 0f;
        text2.color = color2;
    }

    /// <summary>
    /// Fade in de la música de fondo
    /// </summary>
    private IEnumerator FadeInMusic(AudioSource audio, float duration, float targetVolume)
    {
        float elapsedTime = 0f;
        float startVolume = audio.volume;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            audio.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / duration);
            yield return null;
        }

        audio.volume = targetVolume;
    }

    /// <summary>
    /// Fade out de la música de fondo
    /// </summary>
    private IEnumerator FadeOutMusic(AudioSource audio, float duration)
    {
        float elapsedTime = 0f;
        float startVolume = audio.volume;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            audio.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / duration);
            yield return null;
        }

        audio.volume = 0f;
        audio.Stop();
    }

    /// <summary>
    /// Establece la opacidad (alpha) de un texto
    /// </summary>
    private void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text != null)
        {
            Color color = text.color;
            color.a = Mathf.Clamp01(alpha);
            text.color = color;
        }
    }

    /// <summary>
    /// Carga la escena del menú principal
    /// </summary>
    private void LoadMainMenu()
    {
        if (string.IsNullOrEmpty(mainMenuSceneName))
        {
            Debug.LogError("[DedicationController] ¡Error! El nombre de la escena del menú principal está vacío.");
            return;
        }

        Debug.Log($"[DedicationController] Cargando escena: {mainMenuSceneName}");
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Permite saltar la secuencia presionando cualquier tecla o clic
    /// (Opcional - puedes habilitar/deshabilitar esta funcionalidad)
    /// </summary>
    private void Update()
    {
        // Descomentar las siguientes líneas si deseas permitir saltar la dedicatoria
        /*
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
        {
            StopAllCoroutines();
            if (backgroundMusic != null && backgroundMusic.isPlaying)
            {
                backgroundMusic.Stop();
            }
            LoadMainMenu();
        }
        */
    }
}
