using UnityEngine;

[CreateAssetMenu(fileName = "GameBootProfile", menuName = "Game/Boot Profile")]
public class GameBootProfile : ScriptableObject
{
    [Header("Arranque")]
    public string sceneToLoad = "MainWorld";
    public string defaultAnchorId = "Bedroom";
    public PlayerPresetSO defaultPlayerPreset;

    [Header("Boot Settings")]
    [Tooltip("Ignora el save y aplica este preset al arrancar")]
    public bool usePresetInsteadOfSave = false;
    public PlayerPresetSO bootPreset;
    public string startAnchorId = "Bedroom";

    [Header("Runtime Fallback (opcional)")]
    public PlayerPresetSO runtimePreset;

    public bool ShouldBootFromPreset()
        => usePresetInsteadOfSave && bootPreset != null;

    public string GetStartAnchorOrDefault()
        => string.IsNullOrEmpty(startAnchorId)
            ? (string.IsNullOrEmpty(defaultAnchorId) ? "Bedroom" : defaultAnchorId)
            : startAnchorId;

    // Helper: aplica el preset de arranque al PlayerState
    public void ApplyBootPreset(PlayerState ps)
    {
        if (!ps || !bootPreset) return;

        ps.SetLevel(bootPreset.level);
        ps.SetMaxHealth(bootPreset.maxHP); ps.SetHealth(bootPreset.maxHP);
        ps.SetMaxMana(bootPreset.maxMP);   ps.SetMana(bootPreset.maxMP);

        ps.LoadAbilities(bootPreset.unlockedAbilities);
        ps.LoadSpells(bootPreset.unlockedSpells);
    }

    // (opcional) construir un save mínimo por defecto
    public PlayerSaveData BuildDefaultSave()
    {
        var d = new PlayerSaveData();
        d.lastSpawnAnchorId = string.IsNullOrEmpty(defaultAnchorId) ? "Bedroom" : defaultAnchorId;
        return d;
    }
}