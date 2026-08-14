using UnityEngine;

/// <summary>
/// Componente reutilizable para misiones de "elimina N enemigos".
/// Se coloca en el prefab del enemigo (no en la escena) para que todas sus instancias
/// contribuyan automáticamente sin necesidad de wiring manual por instancia.
///
/// Al morir el enemigo (Damageable.OnDied), marca como completado el siguiente step
/// pendiente de la quest indicada. Reutiliza el sistema de steps existente de
/// QuestManager (sin contador propio, sin persistencia nueva que mantener): cada
/// muerte = un step más completado, en el orden en que están definidos en el QuestData.
///
/// No hace nada si la quest no existe o no está activa (p.ej. el jugador aún no ha
/// hablado con el NPC que la ofrece), así que es seguro dejarlo en el prefab del
/// enemigo de forma permanente.
/// </summary>
[RequireComponent(typeof(Damageable))]
public class QuestKillContributor : MonoBehaviour
{
    [Header("Quest")]
    [Tooltip("Quest a la que contribuye la muerte de este enemigo.")]
    [SerializeField] private string questId = "";

    [Tooltip("Si está activo, esta muerte solo cuenta cuando la quest está Activa (evita marcar steps de quests inactivas o ya completadas).")]
    [SerializeField] private bool onlyIfQuestActive = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Damageable _damageable;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
    }

    private void OnEnable()
    {
        if (_damageable != null)
            _damageable.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (_damageable != null)
            _damageable.OnDied -= HandleDied;
    }

    private void HandleDied()
    {
        if (string.IsNullOrEmpty(questId))
            return;

        var qm = QuestManager.Instance;
        if (qm == null)
            return;

        if (onlyIfQuestActive && qm.GetState(questId) != QuestState.Active)
            return;

        if (qm.AreAllStepsCompleted(questId))
            return; // nada pendiente para esta quest (p.ej. ya se mataron todos los enemigos requeridos)

        foreach (var runtimeQuest in qm.GetAll())
        {
            if (runtimeQuest.Id != questId)
                continue;

            var steps = runtimeQuest.Steps;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].completed)
                    continue;

                qm.MarkStepDone(questId, i);
                if (debugLogs)
                    Debug.Log($"[QuestKillContributor:{name}] ✅ Step {i} de '{questId}' completado por esta muerte.");
                return;
            }

            break;
        }
    }
}
