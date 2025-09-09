using UnityEngine;

[RequireComponent(typeof(PlayerTargeting))]
public class PlayerTargetingFeedback : MonoBehaviour
{
    [Header("Marker")]
    [SerializeField] private GameObject markerPrefab;   
    [SerializeField] private Vector3   offset = new Vector3(0, 1.8f, 0); // altura sobre la cabeza
    [SerializeField] private bool      billboardToCamera = true;

    Camera _cam;
    PlayerTargeting _targeting;
    Transform _marker;
    Collider _lastCol;

    void Awake()
    {
        _targeting = GetComponent<PlayerTargeting>();
        _cam = Camera.main;
        if (markerPrefab)
        {
            var go = Instantiate(markerPrefab);
            go.SetActive(false);
            _marker = go.transform;
        }
    }

    void LateUpdate()
    {
        if (!_marker) return;

        var t = _targeting.CurrentTarget;
        if (!t)
        {
            if (_marker.gameObject.activeSelf) _marker.gameObject.SetActive(false);
            _lastCol = null;
            return;
        }

        if (!_marker.gameObject.activeSelf) _marker.gameObject.SetActive(true);

        // Colócalo encima del bounds del target
        if (!_lastCol || _lastCol.transform != t) _lastCol = t.GetComponentInParent<Collider>();
        Vector3 pos = t.position + offset;
        if (_lastCol) pos = _lastCol.bounds.center + new Vector3(0, _lastCol.bounds.extents.y, 0) + offset * 0.2f;

        _marker.position = pos;

        if (billboardToCamera && _cam)
            _marker.forward = (_marker.position - _cam.transform.position).normalized;
    }
}