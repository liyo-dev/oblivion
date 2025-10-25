using UnityEngine;
using UnityEngine.UI;

public class AudioOptionsPanel : MonoBehaviour
{
    [Header("Sliders (0-1)")]
    [SerializeField] Slider masterSlider;
    [SerializeField] Slider musicSlider;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider dialogueSlider;

    void Awake()
    {
        RefreshUI();

        if (masterSlider) masterSlider.onValueChanged.AddListener(v => SetVolume(AudioBus.Master, v));
        if (musicSlider) musicSlider.onValueChanged.AddListener(v => SetVolume(AudioBus.Music, v));
        if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => SetVolume(AudioBus.Sfx, v));
        if (dialogueSlider) dialogueSlider.onValueChanged.AddListener(v => SetVolume(AudioBus.Dialogue, v));
    }

    void OnEnable() => RefreshUI();

    void OnDestroy()
    {
        if (masterSlider) masterSlider.onValueChanged.RemoveAllListeners();
        if (musicSlider) musicSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider) sfxSlider.onValueChanged.RemoveAllListeners();
        if (dialogueSlider) dialogueSlider.onValueChanged.RemoveAllListeners();
    }

    void RefreshUI()
    {
        var mgr = AudioManager.Instance;
        if (!mgr) return;

        if (masterSlider) masterSlider.SetValueWithoutNotify(mgr.GetVolume(AudioBus.Master));
        if (musicSlider) musicSlider.SetValueWithoutNotify(mgr.GetVolume(AudioBus.Music));
        if (sfxSlider) sfxSlider.SetValueWithoutNotify(mgr.GetVolume(AudioBus.Sfx));
        if (dialogueSlider) dialogueSlider.SetValueWithoutNotify(mgr.GetVolume(AudioBus.Dialogue));
    }

    void SetVolume(AudioBus bus, float value)
    {
        AudioManager.Instance?.SetVolume(bus, value);
    }

    // For binding from UI events without referencing sliders directly
    public void SetMasterVolume(float value) => SetVolume(AudioBus.Master, value);
    public void SetMusicVolume(float value) => SetVolume(AudioBus.Music, value);
    public void SetSfxVolume(float value) => SetVolume(AudioBus.Sfx, value);
    public void SetDialogueVolume(float value) => SetVolume(AudioBus.Dialogue, value);
}
