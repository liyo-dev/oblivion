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
        // Solo consultar el registro en memoria
        return AnchorRegistry.Get(id);
    }
}