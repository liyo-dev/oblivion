using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    public string targetAnchorId;
    public string requiredFlag;
    public string setFlagOnEnter;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var ps = other.GetComponent<PlayerState>() ?? other.GetComponentInParent<PlayerState>();
        if (!ps) return;

        if (!string.IsNullOrEmpty(requiredFlag) && !ps.HasFlag(requiredFlag)) return;
        if (!string.IsNullOrEmpty(setFlagOnEnter)) ps.SetFlag(setFlagOnEnter, true);

        TeleportService.TeleportToAnchor(ps.gameObject, targetAnchorId);
        // NO guardar aquí
    }
}