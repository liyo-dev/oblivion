using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Añade sonidos automáticos a sliders:
/// - Select: UI_Navigate
/// </summary>
[RequireComponent(typeof(Slider))]
public class UISliderAudio : MonoBehaviour, ISelectHandler
{
    [Header("Sound Keys")]
    [SerializeField] private string selectSoundKey = "UI_Navigate";
    
    [Header("Settings")]
    [SerializeField] private bool playSelectSound = true;
    [SerializeField] private float selectVolume = 0.7f;
    
    // Detecta selección por gamepad/teclado
    public void OnSelect(BaseEventData eventData)
    {
        if (!playSelectSound) return;
        PlaySound(selectSoundKey, selectVolume);
    }
    
    private void PlaySound(string soundKey, float volume)
    {
        if (string.IsNullOrEmpty(soundKey)) return;
        if (AudioService.Instance == null) return;
        
        AudioService.Instance.PlaySFX(soundKey, volume);
    }
}
