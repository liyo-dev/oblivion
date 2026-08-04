using System.Collections;
using UnityEngine;

/// <summary>
/// Zona de inspección que solo se activa durante el día y, opcionalmente, cuando una misión concreta está activa.
/// De noche muestra un mensaje toast. Si la misión requerida no está activa, ignora el trigger silenciosamente.
/// Compatible con WaitCustomEventNode: al entrar correctamente envía narrativeEventKey a DefaultNarrativeSignals.
///
/// Bloqueo físico: asignar physicalBlocker a un Collider sólido (no-trigger) en un hijo. Se habilita
/// automáticamente cuando la misión está activa, evitando que el jugador atraviese la zona.
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

    [Header("Bloqueo físico")]
    [Tooltip("Collider sólido (no-trigger) en un hijo que impide físicamente el paso cuando la misión está activa. Dejar vacío si no se necesita.")]
    [SerializeField] private Collider physicalBlocker;

    private bool _used;
    private bool _lockAcquired;
    private Collider _triggerCollider;
    private DayNightCycle _dayNightCycle;
    private QuestManager _questManager;

    void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        if (!_triggerCollider.isTrigger) _triggerCollider.isTrigger = true;

        if (physicalBlocker != null && physicalBlocker.isTrigger)
            physicalBlocker.isTrigger = false;

        _dayNightCycle = Object.FindAnyObjectByType<DayNightCycle>();
    }

    void Start()
    {
        UpdatePhysicalBlocker();

        if (questRequirement.IsConfigured)
        {
            _questManager = QuestManager.Instance;
            if (_questManager != null)
                _questManager.OnQuestsChanged += UpdatePhysicalBlocker;
        }

        // Restaurar estado de uso desde el preset para que no se dispare de nuevo al recargar
        if (disableAfterUse && !string.IsNullOrEmpty(narrativeEventKey) && GameBootService.IsAvailable)
        {
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset?.flags != null && preset.flags.Contains(GetUsedFlagKey()))
            {
                _used = true;
                gameObject.SetActive(false);
            }
        }
    }

    private string GetUsedFlagKey() => $"TRIGGER_USED:{narrativeEventKey}";

    void OnDisable()
    {
        ReleaseLock();
        if (physicalBlocker != null)
            physicalBlocker.enabled = false;
    }

    void OnDestroy()
    {
        if (_questManager != null)
            _questManager.OnQuestsChanged -= UpdatePhysicalBlocker;
        ReleaseLock();
    }

    void UpdatePhysicalBlocker()
    {
        if (physicalBlocker != null)
            physicalBlocker.enabled = !_used && questRequirement.IsSatisfied();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_used) return;
        if (!questRequirement.IsSatisfied()) return;

        if (_dayNightCycle == null)
            _dayNightCycle = Object.FindAnyObjectByType<DayNightCycle>();

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

        // Persistir que este trigger ya fue usado para que no se repita tras recargar partida
        if (disableAfterUse && !string.IsNullOrEmpty(narrativeEventKey) && GameBootService.IsAvailable)
        {
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset != null)
            {
                preset.flags ??= new System.Collections.Generic.List<string>();
                string flagKey = GetUsedFlagKey();
                if (!preset.flags.Contains(flagKey))
                    preset.flags.Add(flagKey);
            }
        }

        // El bloqueador físico cede el control al lock de movimiento
        if (physicalBlocker != null)
            physicalBlocker.enabled = false;

        // Adquirir lock ANTES de enviar el evento: puente hasta que el sistema narrativo tome el control
        var lockService = PlayerLockService.Instance;
        if (lockService != null)
        {
            lockService.Acquire(this);
            _lockAcquired = true;
        }

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

        // Diferir la desactivación un frame: el grafo narrativo procesa RaiseCustom de forma síncrona,
        // pero el DialogueManager / cinemática adquiere su propio lock ese mismo frame.
        // Liberar aquí con SetActive(false) causaría que OnDisable soltara el lock antes de que
        // el sistema narrativo tuviera tiempo de adquirir el suyo.
        StartCoroutine(FinishNextFrame());
    }

    IEnumerator FinishNextFrame()
    {
        yield return null;
        ReleaseLock();
        if (disableAfterUse)
            gameObject.SetActive(false);
    }

    void ReleaseLock()
    {
        if (_lockAcquired && PlayerLockService.HasInstance)
        {
            PlayerLockService.Instance.Release(this);
            _lockAcquired = false;
        }
    }
}
