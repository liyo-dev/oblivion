using UnityEngine;

public class SpawnAnchor : MonoBehaviour
{
    public string anchorId;        // único (p.ej. "Bedroom", "City_Gate", "Desert_Camp")
    
    [Tooltip("Si está marcado, el jugador mira hacia la puerta (forward del transform). Si no, mira en dirección opuesta (back).")]
    public bool faceDoor = false;

    private void OnEnable()
    {
        AnchorRegistry.Register(this);
    }

    private void OnDisable()
    {
        AnchorRegistry.Unregister(this);
    }

    public static SpawnAnchor FindById(string id)
    {
        // Fallback a registro en memoria
        var a = AnchorRegistry.Get(id);
        if (a) return a;
        // Búsqueda lenta de respaldo si no estaba registrado (escena no inicializada aún, etc.)
        foreach(var x in GameObject.FindObjectsByType<SpawnAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (x && x.anchorId == id) return x;
        return null;
    }
}