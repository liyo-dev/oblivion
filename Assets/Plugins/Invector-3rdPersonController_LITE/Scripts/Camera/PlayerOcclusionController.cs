using UnityEngine;

[ExecuteAlways]
public class PlayerOcclusionController : MonoBehaviour
{
    [Header("Target a proteger")]
    public Transform player;               // raíz del player
    public Vector3 boundsCenterOffset;     // ajuste fino del centro
    public Vector3 boundsExtents = new Vector3(0.5f, 1.0f, 0.5f); // radio/altura aprox

    [Header("Debug")]
    public bool drawGizmos;

    Camera _cam;
    static readonly int _PO_ScreenRect = Shader.PropertyToID("_PO_ScreenRect");   // xy=min, zw=max (0..1)
    static readonly int _PO_PlayerDepth = Shader.PropertyToID("_PO_PlayerDepth"); // depth lineal (eye)

    void OnEnable() { _cam = GetComponent<Camera>(); }
    void LateUpdate()
    {
        if (!_cam || !player) return;

        // Bounds en mundo
        var center = player.position + player.rotation * boundsCenterOffset;
        var ext = boundsExtents;
        // 8 esquinas del AABB
        Vector3[] corners =
        {
            center + new Vector3(-ext.x, -ext.y, -ext.z),
            center + new Vector3(-ext.x, -ext.y,  ext.z),
            center + new Vector3(-ext.x,  ext.y, -ext.z),
            center + new Vector3(-ext.x,  ext.y,  ext.z),
            center + new Vector3( ext.x, -ext.y, -ext.z),
            center + new Vector3( ext.x, -ext.y,  ext.z),
            center + new Vector3( ext.x,  ext.y, -ext.z),
            center + new Vector3( ext.x,  ext.y,  ext.z),
        };

        // Proyectar a viewport (0..1). Si un punto queda detrás, lo clipeamos al borde.
        Vector2 min = new Vector2( 2f,  2f);
        Vector2 max = new Vector2(-2f, -2f);
        float playerDepthEye = float.PositiveInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 sp = _cam.WorldToViewportPoint(corners[i]);
            // depth en espacio de ojo (positivo delante)
            float eyeZ = Vector3.Dot(_cam.worldToCameraMatrix.MultiplyPoint(corners[i]), Vector3.forward) * -1f;
            if (eyeZ > 0f) playerDepthEye = Mathf.Min(playerDepthEye, eyeZ);

            // clipeo simple para no NaN si queda fuera
            sp.x = Mathf.Clamp01(sp.x);
            sp.y = Mathf.Clamp01(sp.y);

            min = Vector2.Min(min, (Vector2)sp);
            max = Vector2.Max(max, (Vector2)sp);
        }

        if (!float.IsFinite(playerDepthEye)) playerDepthEye = 1e6f; // por si todo quedó detrás

        Shader.SetGlobalVector(_PO_ScreenRect, new Vector4(min.x, min.y, max.x, max.y));
        Shader.SetGlobalFloat(_PO_PlayerDepth, playerDepthEye);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !player) return;
        Gizmos.color = Color.cyan;
        var center = player.position + player.rotation * boundsCenterOffset;
        Gizmos.matrix = Matrix4x4.TRS(center, player.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boundsExtents * 2f);
    }
}
