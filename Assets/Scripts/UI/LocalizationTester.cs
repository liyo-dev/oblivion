using UnityEngine;

/// <summary>
/// Script para testear el sistema de localización desde el inspector
/// Usa el nuevo Input System y las teclas QWERTYUIOP para cambiar idiomas
/// </summary>
public class LocalizationTester : MonoBehaviour
{
    [Header("Test de Localización")]
    [SerializeField] private string[] availableLanguages = { "es", "en" };
    [SerializeField] private int currentLanguageIndex = 0;
    
    [Header("Debug")]
    [SerializeField] private bool showCurrentLanguage = true;
    
    private void Start()
    {
        if (showCurrentLanguage && LocalizationManager.Instance != null)
        {
            Debug.Log($"[LocalizationTester] Idioma actual: {LocalizationManager.Instance.CurrentLocale}");
        }
    }
    
    [ContextMenu("Cambiar al siguiente idioma")]
    public void NextLanguage()
    {
        if (availableLanguages.Length == 0) return;
        
        currentLanguageIndex = (currentLanguageIndex + 1) % availableLanguages.Length;
        ChangeToLanguage(availableLanguages[currentLanguageIndex]);
    }
    
    [ContextMenu("Cambiar al idioma anterior")]
    public void PreviousLanguage()
    {
        if (availableLanguages.Length == 0) return;
        
        currentLanguageIndex--;
        if (currentLanguageIndex < 0) currentLanguageIndex = availableLanguages.Length - 1;
        ChangeToLanguage(availableLanguages[currentLanguageIndex]);
    }
    
    public void ChangeToLanguage(string languageCode)
    {
        if (LocalizationManager.Instance == null)
        {
            Debug.LogWarning("[LocalizationTester] LocalizationManager no está disponible");
            return;
        }
        
        LocalizationManager.Instance.ChangeLanguage(languageCode);
        Debug.Log($"[LocalizationTester] Idioma cambiado a: {languageCode}");
    }
    
    // Métodos públicos para UI
    public void ChangeToSpanish() => ChangeToLanguage("es");
    public void ChangeToEnglish() => ChangeToLanguage("en");
    
}
