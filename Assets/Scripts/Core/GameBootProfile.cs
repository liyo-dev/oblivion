using UnityEngine;

[CreateAssetMenu(fileName = "GameBootProfile", menuName = "Game/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
    [Header("Arranque")]
    public string sceneToLoad = "World";
    public string defaultAnchorId = "start";
    public PlayerPresetSO defaultPlayerPreset;
    
    [Header("Boot Settings")]
    public bool usePresetInsteadOfSave = false;
    public PlayerPresetSO bootPreset;
    public string startAnchorId;
    
    [Header("Runtime Fallback")]
    public PlayerPresetSO runtimePreset;
    
    public bool ShouldBootFromPreset()
    {
        return usePresetInsteadOfSave && bootPreset != null;
    }
    
    public PlayerPresetSO GetPreset()
    {
        return bootPreset;
    }
    
    public string GetStartAnchorId()
    {
        return startAnchorId;
    }
    
    // Método de ayuda para crear el guardado por defecto
    public PlayerSaveData BuildDefaultSave()
    {
        var d = new PlayerSaveData();
        d.lastSpawnAnchorId = defaultAnchorId;                        // campo existente en tu PlayerSaveData
        return d;
    }
}
