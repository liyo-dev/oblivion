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
        // FIX INC-ARANAS-0-DE-4 (14 ago 2026): bug reportado por Raúl — mataba arañas sin límite y
        // el contador de la quest se quedaba siempre en "0/4". Todas las ramas de salida temprana de
        // este método eran completamente silenciosas (sin log ni siquiera con debugLogs activado, que
        // solo cubría el camino de ÉXITO) — así que un fallo aquí era indistinguible de "esta muerte
        // no debía contar por diseño". Ahora cada salida deja rastro en consola (bajo debugLogs, igual
        // que el resto del componente) para poder ver de un vistazo cuál de las cuatro condiciones
        // (questId vacío, QuestManager.Instance null, quest no Activa, o ya sin steps pendientes) es
        // la que está bloqueando el progreso la próxima vez que se reproduzca.
        if (string.IsNullOrEmpty(questId))
        {
            if (debugLogs)
                Debug.LogWarning($"[QuestKillContributor:{name}] ❌ questId vacío en el prefab — esta muerte no cuenta para ninguna quest.");
            return;
        }

        var qm = QuestManager.Instance;
        if (qm == null)
        {
            if (debugLogs)
                Debug.LogWarning($"[QuestKillContributor:{name}] ❌ QuestManager.Instance es null.");
            return;
        }

        if (onlyIfQuestActive && qm.GetState(questId) != QuestState.Active)
        {
            if (debugLogs)
                Debug.Log($"[QuestKillContributor:{name}] ℹ️ Quest '{questId}' no está Activa (estado actual: {qm.GetState(questId)}) — esta muerte no cuenta.");
            return;
        }

        if (qm.AreAllStepsCompleted(questId))
        {
            if (debugLogs)
                Debug.Log($"[QuestKillContributor:{name}] ℹ️ Quest '{questId}' ya no tiene steps pendientes — esta muerte no cuenta.");
            return; // nada pendiente para esta quest (p.ej. ya se mataron todos los enemigos requeridos)
        }

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

        if (debugLogs)
            Debug.LogWarning($"[QuestKillContributor:{name}] ⚠️ Quest '{questId}' no se encontró en qm.GetAll() (¿questId con typo o quest nunca añadida vía AddQuest/StartQuest?).");
    }
}
