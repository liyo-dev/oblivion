using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tipo de refugio: bajo un árbol/porche exterior (el NPC se queda ahí parado) o puerta de una
/// casa (el NPC "entra" y se desactiva hasta que deje de llover). Ver SeekShelterState.
/// </summary>
public enum NPCShelterType
{
    TreeCanopy,
    HouseDoor
}

/// <summary>
/// Punto de refugio de lluvia donde un NPC ambiental puede resguardarse. Se añade manualmente a
/// objetos de escena (bajo árboles, en porches, en puertas de casas del pueblo).
///
/// Calcado deliberadamente de NPCWorldPoint.cs (mismo patrón: registro estático con
/// OnEnable/OnDisable, TryFindNearest) para que sea inmediatamente reconocible por cualquiera que
/// ya conozca ese componente. La diferencia es que aquí varios NPCs pueden compartir un mismo
/// punto (capacity > 1), útil para que varios se agrupen bajo el mismo árbol.
///
/// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § A.2.
/// </summary>
public class NPCShelterPoint : MonoBehaviour
{
    [Header("Tipo de refugio")]
    public NPCShelterType shelterType = NPCShelterType.TreeCanopy;

    [Header("Posición de refugio")]
    [Tooltip("Transform exacto donde el NPC se coloca al llegar. Si es null, usa este transform.")]
    public Transform interactionPoint;

    [Tooltip("Si es true, el NPC girará mirando en la dirección Forward de interactionPoint al llegar.")]
    public bool overrideFacing = true;

    [Header("Capacidad")]
    [Min(1)]
    [Tooltip("Cuántos NPCs caben a la vez en este punto. Los árboles/porches grandes pueden admitir " +
             "varios; las puertas de casa normalmente solo 1-2 para no saturar la entrada.")]
    public int capacity = 3;

    private readonly List<Transform> _occupants = new();

    public bool IsFull => _occupants.Count >= capacity;

    public Vector3 InteractionPosition =>
        interactionPoint != null ? interactionPoint.position : transform.position;

    public Quaternion InteractionRotation =>
        overrideFacing
            ? (interactionPoint != null ? interactionPoint.rotation : transform.rotation)
            : Quaternion.identity;

    // ── Registro estático ───────────────────────────────────────────────────────
    private static readonly List<NPCShelterPoint> _all = new();
    public static IReadOnlyList<NPCShelterPoint> All => _all;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all.Clear();
#endif

    void OnEnable()  => _all.Add(this);
    void OnDisable() { _all.Remove(this); _occupants.Clear(); }

    // ── Ocupación ───────────────────────────────────────────────────────────────

    public bool TryOccupy(Transform occupant)
    {
        if (occupant == null) return false;
        if (_occupants.Contains(occupant)) return true;
        if (IsFull) return false;
        _occupants.Add(occupant);
        return true;
    }

    public void Release(Transform occupant)
    {
        if (occupant != null) _occupants.Remove(occupant);
    }

    // ── Búsqueda ────────────────────────────────────────────────────────────────

    public static bool TryFindNearest(
        Vector3 position, NPCShelterType? filter, float maxDist, out NPCShelterPoint result)
    {
        result = null;
        float bestSqr = maxDist * maxDist;

        foreach (var sp in _all)
        {
            if (sp == null || sp.IsFull) continue;
            if (filter.HasValue && sp.shelterType != filter.Value) continue;

            float sqr = (sp.InteractionPosition - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                result  = sp;
            }
        }

        return result != null;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsFull ? Color.red : new Color(0.2f, 0.6f, 1f);
        Vector3 pos = InteractionPosition;
        Gizmos.DrawWireSphere(pos, 0.4f);
        if (overrideFacing)
        {
            Quaternion rot = interactionPoint != null ? interactionPoint.rotation : transform.rotation;
            Gizmos.DrawRay(pos, rot * Vector3.forward * 0.8f);
        }
        UnityEditor.Handles.Label(pos + Vector3.up * 0.6f,
            $"{shelterType}\n{_occupants.Count}/{capacity}");
    }
#endif
}
