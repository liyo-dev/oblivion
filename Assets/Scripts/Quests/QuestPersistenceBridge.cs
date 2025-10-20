using UnityEngine;
using System.Collections.Generic;

/// Sincroniza los flags del preset con el QuestManager al arrancar la escena.
/// Convención: cada quest completada añade un flag "QUEST_COMPLETED:<questId>" al preset.
/// Este bridge lee esos flags y marca esas quests como completadas en runtime.
[DefaultExecutionOrder(-20)]
public class QuestPersistenceBridge : MonoBehaviour
{
    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable)
        {
            ApplyFlagsToQuestManager();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        ApplyFlagsToQuestManager();
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void ApplyFlagsToQuestManager()
    {
        var profile = GameBootService.Profile;
        if (profile == null) return;

        var questManager = QuestManager.Instance;
        if (questManager == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        questManager.RestoreFromProfileFlags(preset.flags);
    }
}

