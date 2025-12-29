using UnityEngine;

public class SpawnAnchor : MonoBehaviour
{
    public string anchorId;        // único (p.ej. "Bedroom", "City_Gate", "Desert_Camp")
    
    [Tooltip("Por defecto (false): El personaje mira en la dirección del eje Z del anchor (forward azul).\n" +
             "Si está marcado (true): El personaje mira en dirección OPUESTA al eje Z del anchor (-forward), es decir, da la vuelta 180°.\n\n" +
             "CONVENCIÓN DE DISEÑO:\n" +
             "- Coloca el anchor con el eje Z apuntando donde quieres que mire el jugador.\n" +
             "- Marca faceDoor=true solo si quieres invertir esa dirección.")]
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