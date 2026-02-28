using UnityEngine;

/// <summary>
/// Define el área del mundo exterior que cubre el minimapa.
/// Coloca este componente en un GameObject con BoxCollider ajustado al límite del mundo.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class MinimapBounds : MonoBehaviour
{
    BoxCollider _col;

    void Awake()
    {
        _col = GetComponent<BoxCollider>();
    }

    /// <summary>
    /// Tamaño del mundo en unidades mundo (X = ancho, Y = profundidad).
    /// </summary>
    public Vector2 WorldSize
    {
        get
        {
            if (_col == null) _col = GetComponent<BoxCollider>();
            return new Vector2(_col.size.x, _col.size.z);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var col = GetComponent<BoxCollider>();
        if (col == null) return;

        Gizmos.color = Color.green;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireCube(col.center, col.size);
    }
#endif
}
