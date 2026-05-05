using UnityEngine;

/// <summary>
/// Punto de mundo que dispara un evento narrativo personalizado la primera vez
/// que el jugador entra en la zona. Registra el ID en seenLorePopupIds para no
/// repetirse entre sesiones.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LorePopupPoint : MonoBehaviour
{
    [Tooltip("ID único de persistencia. Debe ser distinto para cada LorePopupPoint de la escena.")]
    [SerializeField] private string persistenceId;

    [Tooltip("Clave del evento a disparar en el grafo narrativo (RaiseCustomEvent compatible).")]
    [SerializeField] private string narrativeEventKey;

    [Tooltip("Si true, el trigger se desactiva en la escena después de usarse una vez en sesión.")]
    [SerializeField] private bool disableAfterUse = true;

    private bool _usedThisSession;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_usedThisSession) return;
        if (!other.CompareTag("Player")) return;
        if (AlreadySeen()) return;

        _usedThisSession = true;
        MarkAsSeen();

        if (!string.IsNullOrEmpty(narrativeEventKey))
        {
            var signals = DefaultNarrativeSignals.Instance;
            signals?.RaiseCustom(narrativeEventKey);
        }

        if (disableAfterUse)
            gameObject.SetActive(false);
    }

    private bool AlreadySeen()
    {
        if (string.IsNullOrEmpty(persistenceId)) return false;
        var preset = GameBootService.IsAvailable
            ? GameBootService.Profile?.GetActivePresetResolved()
            : null;
        return preset?.seenLorePopupIds?.Contains(persistenceId) ?? false;
    }

    private void MarkAsSeen()
    {
        if (string.IsNullOrEmpty(persistenceId)) return;
        var preset = GameBootService.IsAvailable
            ? GameBootService.Profile?.GetActivePresetResolved()
            : null;
        if (preset == null) return;

        preset.seenLorePopupIds ??= new System.Collections.Generic.List<string>();
        if (!preset.seenLorePopupIds.Contains(persistenceId))
            preset.seenLorePopupIds.Add(persistenceId);
    }
}
