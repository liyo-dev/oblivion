using UnityEngine;

public class SpawnAnchor : MonoBehaviour
{
    public string anchorId;        // único (p.ej. "Bedroom", "City_Gate", "Desert_Camp")
    public Vector3 facing = Vector3.forward; // opcional; si (0), usa forward del transform
    public static SpawnAnchor FindById(string id)
    {
        foreach(var a in GameObject.FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (a && a.anchorId == id) return a;
        return null;
    }
}