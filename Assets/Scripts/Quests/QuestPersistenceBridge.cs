using UnityEngine;
using System.Collections.Generic;

/// Sincroniza los flags de quests entre el QuestManager (runtime) y el PlayerPreset (runtimePreset).
///
/// Al habilitar:
/// - Vuelca los flags del preset al QuestManager (restaurar estado).
/// - Se suscribe a cambios (archivar/mostrar/seguir) para volcar inmediatamente de vuelta al preset.
///
/// Esto garantiza que tus selecciones del menú (p.ej. archivar 2, seguir 3) queden en el runtimePreset
/// sin esperar a un punto de guardado, y luego el SaveSystem las serializa a JSON normalmente.
[DefaultExecutionOrder(-20)]
public class QuestPersistenceBridge : MonoBehaviour
{
    private Coroutine _bindCo;

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable) ApplyFlagsToQuestManager();

        // Enganchar eventos cuando el QuestManager esté disponible
        if (_bindCo != null) StopCoroutine(_bindCo);
        _bindCo = StartCoroutine(BindWhenReady());
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;

        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.OnQuestVisibilityChanged -= HandleQuestVisibilityChanged;
            qm.OnQuestFollowChanged -= HandleQuestFollowChanged;
        }
    }

    private System.Collections.IEnumerator BindWhenReady()
    {
        while (QuestManager.Instance == null) yield return null;
        var qm = QuestManager.Instance;
        if (qm != null)
        {
            qm.OnQuestVisibilityChanged -= HandleQuestVisibilityChanged;
            qm.OnQuestFollowChanged -= HandleQuestFollowChanged;
            qm.OnQuestVisibilityChanged += HandleQuestVisibilityChanged;
            qm.OnQuestFollowChanged += HandleQuestFollowChanged;
        }
        _bindCo = null;
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

    // === Propagación en vivo: QuestManager -> runtimePreset ===
    private void HandleQuestVisibilityChanged(string questId, QuestVisibility visibility)
    {
        PushQuestFlagsToPreset();
    }

    private void HandleQuestFollowChanged(string questId, bool isFollowed)
    {
        PushQuestFlagsToPreset();
    }

    private void PushQuestFlagsToPreset()
    {
        // Re-construye la lista de flags en el runtimePreset preservando los no-QUEST_
        if (!GameBootService.IsAvailable) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;

        var qm = QuestManager.Instance;
        if (qm == null) return;

        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;

        var newFlags = new System.Collections.Generic.List<string>(preset.flags?.Count ?? 0);

        if (preset.flags != null)
        {
            for (int i = 0; i < preset.flags.Count; i++)
            {
                var f = preset.flags[i];
                if (string.IsNullOrEmpty(f)) continue;
                if (f.StartsWith("QUEST_", System.StringComparison.Ordinal)) continue; // se reemplazan
                newFlags.Add(f);
            }
        }

        // Exportar el estado actual de quests (activas/completas/pasos/archivadas/seguidas)
        qm.ExportFlags(newFlags);

        preset.flags = newFlags;
        // Nota: no se escribe a disco aquí; el SavePoint/SaveSystem lo hará cuando se guarde la partida.
    }
}

