using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tipo de refugio: bajo la copa de un árbol (bosque) o bajo una estructura con techo del pueblo
/// (puesto de mercado, porche, tejadillo — cualquier GO con techo, nunca la puerta de una casa:
/// el NPC no "entra" a ningún sitio). Ambos se comportan igual (el NPC se queda ahí, de pie o
/// sentado, siempre visible, hasta que deje de llover) — es solo una etiqueta para poder
/// distinguirlos en el editor o filtrar por tipo si hiciera falta más adelante. Ver
/// SeekShelterState.
/// </summary>
public enum NPCShelterType
{
    TreeCanopy,
    RoofedSpot
}

/// <summary>
/// Punto de refugio de lluvia donde un NPC ambiental puede resguardarse. Se añade manualmente a
/// objetos de escena: bajo árboles en el bosque, o bajo cualquier GO con techo del pueblo (puesto
/// de mercado, porche, tejadillo). Nunca en puertas de casa — el NPC no entra a ningún sitio, se
/// queda de pie o sentado a la vista, igual que bajo un árbol.
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
    [Tooltip("Cuántos NPCs caben a la vez en este punto. Los árboles grandes o techados amplios " +
             "pueden admitir varios; un tejadillo pequeño normalmente solo 1-2 para no amontonar.")]
    public int capacity = 3;

    [Header("Colisión propia a ignorar (aproximación manual)")]
    [Tooltip("Collider físico del propio árbol/estructura bajo el que está este punto (p.ej. el " +
             "tronco). NPCStateBase.ManualApproachStep lo ignora al comprobar obstrucciones — es " +
             "precisamente el obstáculo que este punto existe para rodear, no un bloqueo real. Si se " +
             "deja vacío, se autodetecta en Awake buscando un Collider en este GameObject o en algún " +
             "padre (funciona colocando el punto como hijo del árbol/prop, patrón recomendado). " +
             "Otros obstáculos ajenos (otro árbol, una roca) SÍ siguen bloqueando la aproximación.")]
    [SerializeField] private Collider ownerCollider;

    public Collider OwnerCollider => ownerCollider;

    void Awake()
    {
        // Autodetección única (no en Update/OnUpdate): coste cero en runtime, solo se paga una vez
        // al cargar la escena. Si el diseñador ya asignó ownerCollider a mano, se respeta tal cual.
        if (ownerCollider == null)
            ownerCollider = GetComponentInParent<Collider>();
    }

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

    // FIX INC-071: elegir el refugio más cercano en línea recta podía mandar al NPC a un punto
    // más corto "a vista de pájaro" pero bloqueado por un muro/casa/río, forzando un rodeo
    // larguísimo o dejándolo atascado a mitad de camino (ver IsPathBlocked en SeekShelterState).
    // Entre los candidatos dentro de maxDist se prefiere ahora el que tenga la ruta de NavMesh
    // COMPLETA más corta; si ninguno tiene ruta completa (p.ej. todos bloqueados), se cae al
    // comportamiento anterior — el más cercano en línea recta — para no dejar nunca al NPC sin
    // refugio asignado.
    public static bool TryFindNearest(
        Vector3 position, NPCShelterType? filter, float maxDist, out NPCShelterPoint result)
    {
        result = null;
        float maxSqr = maxDist * maxDist;

        NPCShelterPoint bestStraightLine = null;
        float bestStraightLineSqr = maxSqr;

        NPCShelterPoint bestByPath = null;
        float bestPathDist = float.MaxValue;
        NavMeshPath path = new NavMeshPath();

        foreach (var sp in _all)
        {
            if (sp == null || sp.IsFull) continue;
            if (filter.HasValue && sp.shelterType != filter.Value) continue;

            Vector3 candidatePos = sp.InteractionPosition;
            float sqr = (candidatePos - position).sqrMagnitude;
            if (sqr >= maxSqr) continue;

            if (sqr < bestStraightLineSqr)
            {
                bestStraightLineSqr = sqr;
                bestStraightLine = sp;
            }

            if (NavMesh.CalculatePath(position, candidatePos, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                float pathDist = PathLength(path);
                if (pathDist < bestPathDist)
                {
                    bestPathDist = pathDist;
                    bestByPath = sp;
                }
            }
        }

        result = bestByPath != null ? bestByPath : bestStraightLine;
        return result != null;
    }

    private static float PathLength(NavMeshPath path)
    {
        var corners = path.corners;
        float dist = 0f;
        for (int i = 1; i < corners.Length; i++)
            dist += Vector3.Distance(corners[i - 1], corners[i]);
        return dist;
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
