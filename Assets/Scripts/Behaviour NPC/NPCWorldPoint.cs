using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de interacción del mundo donde un NPC puede realizar una actividad ambiental.
/// Se añade a objetos de escena (bancos, mesas, camas) para que los NPCs puedan usarlos.
/// </summary>
public class NPCWorldPoint : MonoBehaviour
{
    [Header("Actividad")]
    [Tooltip("Tipo de actividad que se puede realizar en este punto")]
    public NPCAmbientActivity activityType = NPCAmbientActivity.SitMedium;

    [Header("Posición de Interacción")]
    [Tooltip("Transform exacto desde donde el NPC realiza la actividad. Si es null, usa este transform.")]
    public Transform interactionPoint;

    [Tooltip("Si es true, el NPC girará mirando en la dirección Forward de interactionPoint al llegar")]
    public bool overrideFacing = true;

    [Header("Interacción del Jugador")]
    [Tooltip("Distancia máxima desde el interactionPoint para que el jugador pueda activar este punto. " +
             "Evita que se pueda sentar desde muy lejos aunque esté dentro del rango global del InteractionDetector.")]
    public float requiredProximity = 1.5f;

    [Header("Prop (beber/comer)")]
    [Tooltip("Objeto que el personaje cogerá con la mano derecha al iniciar la actividad (ej: vaso, taza).")]
    public Transform prop;

    [Tooltip("Posición local del prop relativa al hueso de la mano derecha.")]
    public Vector3 propHandOffset = Vector3.zero;

    [Tooltip("Rotación local del prop relativa al hueso de la mano derecha (ángulos Euler).")]
    public Vector3 propHandRotation = Vector3.zero;

    [Header("Duración")]
    [Min(0f), Tooltip("Duración mínima de la ocupación (segundos)")]
    public float minOccupationDuration = 15f;

    [Min(0f), Tooltip("Duración máxima de la ocupación (segundos)")]
    public float maxOccupationDuration = 90f;

    private bool _isOccupied;
    private Transform _occupant;

    // Estado del prop
    private Transform _propOriginalParent;
    private Vector3 _propOriginalLocalPos;
    private Quaternion _propOriginalLocalRot;
    private bool _propAttached;

    public bool IsOccupied => _isOccupied;
    public NPCAmbientActivity ActivityType => activityType;

    public Vector3 InteractionPosition =>
        interactionPoint != null ? interactionPoint.position : transform.position;

    public Quaternion InteractionRotation =>
        overrideFacing
            ? (interactionPoint != null ? interactionPoint.rotation : transform.rotation)
            : Quaternion.identity;

    // ── Registro estático ───────────────────────────────────────────────────────
    private static readonly List<NPCWorldPoint> _all = new List<NPCWorldPoint>();
    public static IReadOnlyList<NPCWorldPoint> All => _all;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _all.Clear();
#endif

    void OnEnable()  => _all.Add(this);
    void OnDisable() { _all.Remove(this); Release(); }

    // ── Ocupación ───────────────────────────────────────────────────────────────

    public bool TryOccupy(Transform occupant)
    {
        if (_isOccupied) return false;
        _isOccupied = true;
        _occupant   = occupant;
        return true;
    }

    public void Release(Transform occupant = null)
    {
        if (occupant != null && _occupant != occupant) return;
        _isOccupied = false;
        _occupant   = null;
    }

    // ── Prop ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Adjunta el prop a la mano derecha del ocupante. Llama justo antes de reproducir la animación.
    /// </summary>
    public void AttachPropToOccupant(Animator occupantAnimator)
    {
        if (prop == null || occupantAnimator == null || _propAttached) return;

        var handBone = occupantAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (handBone == null) return;

        _propOriginalParent   = prop.parent;
        _propOriginalLocalPos = prop.localPosition;
        _propOriginalLocalRot = prop.localRotation;

        prop.SetParent(handBone, worldPositionStays: false);
        prop.localPosition = propHandOffset;
        prop.localRotation = Quaternion.Euler(propHandRotation);
        _propAttached = true;
    }

    /// <summary>
    /// Devuelve el prop a su padre y posición/rotación originales.
    /// </summary>
    public void DetachProp()
    {
        if (prop == null || !_propAttached) return;

        prop.SetParent(_propOriginalParent, worldPositionStays: false);
        prop.localPosition = _propOriginalLocalPos;
        prop.localRotation = _propOriginalLocalRot;
        _propAttached = false;
    }

    // ── Búsqueda ────────────────────────────────────────────────────────────────

    public static bool TryFindNearest(
        Vector3 position,
        NPCAmbientActivity activityFilter,
        float maxDist,
        out NPCWorldPoint result)
    {
        result = null;
        float bestSqr = maxDist * maxDist;

        foreach (var wp in _all)
        {
            if (wp == null || wp._isOccupied) continue;
            if (activityFilter != NPCAmbientActivity.None && wp.activityType != activityFilter) continue;

            float sqr = (wp.InteractionPosition - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                result  = wp;
            }
        }

        return result != null;
    }

    public static bool TryFindAny(Vector3 position, float maxDist, out NPCWorldPoint result)
        => TryFindNearest(position, NPCAmbientActivity.None, maxDist, out result);

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isOccupied ? Color.red : Color.green;
        Vector3 pos = InteractionPosition;
        Gizmos.DrawWireSphere(pos, 0.35f);
        if (overrideFacing)
        {
            Quaternion rot = interactionPoint != null ? interactionPoint.rotation : transform.rotation;
            Gizmos.DrawRay(pos, rot * Vector3.forward * 0.8f);
        }
        UnityEditor.Handles.Label(pos + Vector3.up * 0.6f,
            $"{activityType}\n{(_isOccupied ? "Ocupado" : "Libre")}");
    }
#endif
}
