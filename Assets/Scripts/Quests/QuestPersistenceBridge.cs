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
        if (QuestManager.Instance == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null || preset.flags == null) return;

        foreach (var flag in preset.flags)
        {
            if (string.IsNullOrEmpty(flag)) continue;
            if (!flag.StartsWith("QUEST_COMPLETED:")) continue;
            string questId = flag.Substring("QUEST_COMPLETED:".Length);
            if (string.IsNullOrEmpty(questId)) continue;

            // Asegurar que la quest exista en runtime y marcarla como completada
            QuestManager.Instance.StartQuest(questId);   // añade desde catálogo si no existe y la activa
            QuestManager.Instance.CompleteQuest(questId);
        }

        // Notificar a listeners que el estado puede haber cambiado
        // (QuestManager ya dispara eventos en CompleteQuest/StartQuest)
    }
}

