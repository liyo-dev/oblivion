using UnityEngine;

/// <summary>
/// Zona de inspección que solo se activa durante el día y, opcionalmente, cuando una misión concreta está activa.
/// De noche muestra un mensaje toast. Si la misión requerida no está activa, simplemente ignora el trigger.
/// Compatible con WaitCustomEventNode: al entrar correctamente envía narrativeEventKey a DefaultNarrativeSignals.
/// </summary>
[RequireComponent(typeof(Collider))]
[DisallowMultipleComponent]
public class DayOnlyInspectionTrigger : MonoBehaviour
{
    [Header("Evento narrativo")]
    [Tooltip("Clave del evento a enviar al grafo cuando el jugador entra durante el día.")]
    [SerializeField] private string narrativeEventKey;

    [Header("Requisito de misión")]
    [Tooltip("Si mode != None, el trigger solo se activa si la misión cumple la condición.")]
    [SerializeField] private QuestRequirement questRequirement;

    [Header("Mensajes")]
    [Tooltip("Clave de localización del toast si el jugador intenta entrar de noche. Vacío = sin mensaje.")]
    [SerializeField] private string blockedNightMessageKey = "";

    [Header("Comportamiento")]
    [Tooltip("Si true, desactiva este trigger tras el primer uso exitoso.")]
    [SerializeField] private bool disableAfterUse = true;

    private bool _used;
    private DayNightCycle _dayNightCycle;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger) col.isTrigger = true;
        _dayNightCycle = Object.FindFirstObjectByType<DayNightCycle>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_used) return;

        // Comprobar misión antes que nada: si no se cumple, ignorar silenciosamente
        if (!questRequirement.IsSatisfied()) return;

        if (_dayNightCycle == null)
            _dayNightCycle = Object.FindFirstObjectByType<DayNightCycle>();

        bool isNight = _dayNightCycle != null &&
                       (_dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Night ||
                        _dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Midnight);

        if (isNight)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DayOnlyInspectionTrigger:{name}] Bloqueado: es de noche ({_dayNightCycle.CurrentTimeOfDay}).");
#endif
            if (!string.IsNullOrEmpty(blockedNightMessageKey))
                HudToastService.Instance?.Show(blockedNightMessageKey);
            return;
        }

        _used = true;

        if (!string.IsNullOrEmpty(narrativeEventKey))
        {
            var signals = DefaultNarrativeSignals.Instance;
            if (signals != null)
                signals.RaiseCustom(narrativeEventKey, name);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.LogError($"[DayOnlyInspectionTrigger:{name}] DefaultNarrativeSignals.Instance es null al intentar emitir '{narrativeEventKey}'.");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[DayOnlyInspectionTrigger:{name}] Zona inspeccionada ({_dayNightCycle?.CurrentTimeOfDay}) → evento '{narrativeEventKey}' enviado.");
#endif

        if (disableAfterUse)
            gameObject.SetActive(false);
    }
}
