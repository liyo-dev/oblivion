using UnityEngine;

[RequireComponent(typeof(Collider))]
public class AnchorSetter : MonoBehaviour
{
    public string anchorId;
    public bool saveAfter = true;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        SpawnManager.SetCurrentAnchor(anchorId);
        if (saveAfter) FindFirstObjectByType<SpawnManager>()?.SaveNow();
    }
}