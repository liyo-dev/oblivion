using UnityEngine;

public class SpawnAnchor : MonoBehaviour
{
    public string anchorId;        // único (p.ej. "Bedroom", "City_Gate", "Desert_Camp")
    
    [HideInInspector] public bool faceDoor = false;

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

    /// <summary>
    /// Obtiene la rotación correcta del anchor, aplicando la lógica faceDoor.
    /// Devuelve la rotación hacia donde debe mirar el personaje.
    /// </summary>
    public Quaternion GetCharacterRotation()
    {
        if (faceDoor)
        {
            // faceDoor = true → Invertir la dirección (mirar al lado contrario)
            // Usamos -forward para dar la vuelta 180°
            return Quaternion.LookRotation(-transform.forward, Vector3.up);
        }
        else
        {
            // faceDoor = false (por defecto) → Usar la dirección del anchor tal cual
            // El personaje mira en la dirección del eje Z del anchor
            return Quaternion.LookRotation(transform.forward, Vector3.up);
        }
    }
}