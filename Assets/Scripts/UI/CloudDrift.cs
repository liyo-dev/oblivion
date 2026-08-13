using UnityEngine;

/// <summary>
/// Deriva lentamente una nube (prefabs de Assets/Prefabs/Clouds, shader Quibli Cloud3D) a lo largo
/// de una dirección fija, y cuando se aleja demasiado del punto de partida la reaparece por el
/// lado opuesto — un bucle infinito y barato, sin pooling ni lógica de spawn/despawn, pensado para
/// una escena de fondo de menú (nunca hay más de un puñado de nubes a la vez).
/// </summary>
[DisallowMultipleComponent]
public class CloudDrift : MonoBehaviour
{
    [Tooltip("Dirección de desplazamiento en espacio de mundo (se normaliza sola). Normalmente el eje X o Z de la escena del backdrop.")]
    [SerializeField] private Vector3 direction = Vector3.right;

    [SerializeField] private float speed = 0.6f;

    [Tooltip("Distancia desde el punto de partida a la que la nube 'da la vuelta' y reaparece por el lado opuesto.")]
    [SerializeField] private float wrapDistance = 60f;

    Vector3 _startPos;
    Vector3 _dirNormalized;

    void Awake()
    {
        _startPos = transform.position;
        _dirNormalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
    }

    void Update()
    {
        transform.position += _dirNormalized * (speed * Time.deltaTime);

        float traveled = Vector3.Dot(transform.position - _startPos, _dirNormalized);
        if (traveled > wrapDistance)
            transform.position -= _dirNormalized * (wrapDistance * 2f);
        else if (traveled < -wrapDistance)
            transform.position += _dirNormalized * (wrapDistance * 2f);
    }
}
