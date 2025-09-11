using UnityEngine;

[CreateAssetMenu(fileName = "GameBootProfile", menuName = "Game/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
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
}
