using UnityEngine;

[DisallowMultipleComponent]
public class Targetable : MonoBehaviour
{
    [Tooltip("Radio en el que este enemigo puede ser auto-apuntado (si 0 usa el radio global del PlayerTargeting).")]
    public float targetingRadius = 8f;

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
        Vector3 center = transform.position;
        var col = GetComponentInParent<Collider>();
        if (col) center = col.bounds.center;
        Gizmos.DrawWireSphere(center, Mathf.Max(0.01f, targetingRadius));
    }
#endif
}

