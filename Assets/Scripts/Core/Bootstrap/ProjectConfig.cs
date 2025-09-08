using UnityEngine;

[CreateAssetMenu(menuName="Oblivion/Config/ProjectConfig")]
public class ProjectConfig : ScriptableObject
{
    [Header("Escenas")]
    public string titleScene = "Title";
    public string worldScene = "World_Open";

    [Header("Audio")]
    public AudioMixerSettings audioSettings;

    [Header("Gameplay")]
    public PlayerSpawnSettings spawnSettings; 
}
