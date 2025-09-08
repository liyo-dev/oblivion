using UnityEngine;

[CreateAssetMenu(menuName="Oblivion/Config/AudioMixerSettings")]
public class AudioMixerSettings : ScriptableObject
{
    [Range(0f,1f)] public float masterVolume = 1f;
    [Range(0f,1f)] public float musicVolume = 1f;
    [Range(0f,1f)] public float sfxVolume = 1f;
}