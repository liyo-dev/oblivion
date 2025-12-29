using UnityEngine;

public class SpawnAnchor : MonoBehaviour
{
    public string anchorId;        // único (p.ej. "Bedroom", "City_Gate", "Desert_Camp")
    
    [Tooltip("Si está marcado, el personaje mira hacia la puerta. Si no está marcado, el personaje mira de espaldas a la puerta. NOTA: La lógica usa el forward invertido debido a la orientación del transform en la escena.")]
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