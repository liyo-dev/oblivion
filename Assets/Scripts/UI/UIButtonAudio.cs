using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Añade sonidos automáticos a botones UI:
/// - Click: UI_Submit
/// - Hover/Select: UI_Navigate
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonAudio : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    // Flag estático para silenciar temporalmente (útil para evitar sonido de selección inicial)
    public static bool MuteAll { get; set; }
    
    [Header("Sound Keys")]
    [SerializeField] private string clickSoundKey = "UI_Submit";
    [SerializeField] private string hoverSoundKey = "UI_Navigate";
    
    [Header("Settings")]
    [SerializeField] private bool playClickSound = true;
    [SerializeField] private bool playHoverSound = true;
    [SerializeField] private float clickVolume = 1f;
    [SerializeField] private float hoverVolume = 0.7f;
    
    private Button _button;
    
    /// <summary>
    /// Permite forzar el valor de playHoverSound desde código
    /// </summary>
    public void SetPlayHoverSound(bool value)
    {
        playHoverSound = value;
    }
    
    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnButtonClick);
    }
    
    void OnEnable()
    {
        Debug.Log($"[UIButtonAudio] OnEnable en {gameObject.name}, playHoverSound={playHoverSound}");
    }
    
    void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnButtonClick);
    }
    
    void OnButtonClick()
    {
        if (!playClickSound) return;
        if (_button != null && !_button.interactable) return;
        
        PlaySound(clickSoundKey, clickVolume);
    }
    
    // Detecta hover del mouse
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!playHoverSound) return;
        // No verificar interactable - permitir sonido hover incluso en botones inactivos
        
        PlaySound(hoverSoundKey, hoverVolume);
    }
    
    // Detecta selección por gamepad/teclado
    public void OnSelect(BaseEventData eventData)
    {
        Debug.Log($"[UIButtonAudio] OnSelect en {gameObject.name}, playHoverSound={playHoverSound}, MuteAll={MuteAll}");
        if (!playHoverSound) return;
        PlaySound(hoverSoundKey, hoverVolume);
    }
    
    private void PlaySound(string soundKey, float volume)
    {
        if (MuteAll)
        {
            Debug.Log($"[UIButtonAudio] PlaySound BLOQUEADO por MuteAll - {soundKey}");
            return;
        }
        if (string.IsNullOrEmpty(soundKey))
        {
            Debug.Log($"[UIButtonAudio] PlaySound - soundKey vacío");
            return;
        }
        if (AudioService.Instance == null)
        {
            Debug.LogWarning($"[UIButtonAudio] AudioService.Instance es NULL");
            return;
        }
        
        Debug.Log($"[UIButtonAudio] ✅ PlaySound: {soundKey}");
        AudioService.Instance.PlaySFX(soundKey, volume);
    }
}
