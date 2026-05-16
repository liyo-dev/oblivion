using UnityEngine;

/// <summary>
/// Punto de mundo que la primera vez que el jugador entra en la zona puede:
/// (a) Mostrar un popup de lore directamente (si loreConfig está asignado), y/o
/// (b) Disparar un evento personalizado en el grafo narrativo (si narrativeEventKey está asignado).
/// Registra el ID en seenLorePopupIds para no repetirse entre sesiones.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LorePopupPoint : MonoBehaviour
{
    [Tooltip("ID único de persistencia. Debe ser distinto para cada LorePopupPoint de la escena.")]
    [SerializeField] private string persistenceId;

    [Header("Popup directo (sin grafo)")]
    [Tooltip("Configuración del popup de lore a mostrar al entrar. Si está asignada, muestra el popup sin necesitar un nodo en el grafo.")]
    [SerializeField] private LorePopupConfig loreConfig;

    [Header("Evento para el grafo narrativo")]
    [Tooltip("Clave del evento a disparar en el grafo narrativo (RaiseCustomEvent compatible). Compatible con WaitCustomEventNode.")]
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

        // Mostrar popup directo si hay config asignada
        if (loreConfig != null)
            LorePopupUI.Instance?.Show(loreConfig, null);

        // Disparar evento al grafo si hay clave definida
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
