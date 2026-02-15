using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AnchorSetter : MonoBehaviour
{
    public string anchorId;
    public bool saveAfter = true;

    private bool _pendingSet;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(AnchorSetter));
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_pendingSet)
        {
            ApplyAnchorAndMaybeSave();
            _pendingSet = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameBootService.IsAvailable)
        {
            ApplyAnchorAndMaybeSave();
        }
        else
        {
            // Diferir hasta que el GameBootProfile esté listo
            _pendingSet = true;
        }
    }

    private void ApplyAnchorAndMaybeSave()
    {
        if (string.IsNullOrEmpty(anchorId)) return;

        SpawnManager.SetCurrentAnchor(anchorId);
        Debug.Log($"[AnchorSetter] Anchor establecido a: {anchorId}");

        if (saveAfter)
        {
            Debug.Log("[AnchorSetter] Auto-guardado deshabilitado. Visita un punto de guardado para persistir el nuevo anchor.");
        }
    }
}
