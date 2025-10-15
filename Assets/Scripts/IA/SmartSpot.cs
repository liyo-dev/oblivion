using UnityEngine;

public enum SpotPose { Stand, Sit, Lean, Look, Vendor, Talk }

public class SmartSpot : MonoBehaviour
{
    [Header("Configuración")]
    public SpotPose pose = SpotPose.Stand;
    [Min(0.5f)] public float useDurationMin = 6f;
    [Min(0.5f)] public float useDurationMax = 14f;
    [Tooltip("Hacia dónde mirar el NPC al usar el spot (opcional)")]
    public Transform lookAtTarget;
    [Tooltip("Si es social, admite 2+ NPCs (ej: charlar)")]
    public bool isSocial = false;
    [Min(1)] public int capacity = 1;

    int _occupancy = 0;
    public bool TryReserve()
    {
        if (_occupancy >= capacity) return false;
        _occupancy++;
        return true;
    }
    public void Release()
    {
        _occupancy = Mathf.Max(0, _occupancy - 1);
    }

    public Vector3 GetStandPoint()
    {
        // Punto de pie: usa el propio transform
        return transform.position;
    }

    public Quaternion GetFacingRotation(Vector3 agentPos)
    {
        if (lookAtTarget)
        {
            Vector3 dir = (lookAtTarget.position - agentPos).normalized;
            if (dir.sqrMagnitude > 0.0001f) return Quaternion.LookRotation(dir, Vector3.up);
        }
        return transform.rotation;
    }

    public float GetRandomUseTime()
    {
        return Random.Range(useDurationMin, useDurationMax);
    }
}
