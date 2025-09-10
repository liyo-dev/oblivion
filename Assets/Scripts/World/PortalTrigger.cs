using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    public string targetAnchorId;          // a dónde te mueve (ej: "City_Gate")
    public string requiredFlag;            // opcional (ej: "HasKey")
    public string setFlagOnEnter;          // opcional (ej: "EnteredCity")
    public bool   saveAfterTeleport = true;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var ps = other.GetComponent<PlayerState>(); if (!ps) ps = other.GetComponentInParent<PlayerState>();
        if (!ps) return;

        if (!string.IsNullOrEmpty(requiredFlag) && !ps.HasFlag(requiredFlag))
            return; // no cumple requisito

        if (!string.IsNullOrEmpty(setFlagOnEnter)) ps.SetFlag(setFlagOnEnter, true);

        TeleportService.TeleportToAnchor(ps.gameObject, targetAnchorId, saveAfterTeleport);
    }
}