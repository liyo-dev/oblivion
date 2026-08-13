using UnityEngine;

/// <summary>
/// Añade un movimiento sutil y en bucle a la cámara del "backdrop" 3D del menú principal (el
/// mundo que se ve detrás de los personajes, estilo pantalla de título de muchos JRPG/anime —
/// ej. Seven Deadly Sins: Origin). No depende de Cinemachine: es un procedural simple pensado
/// para una escena de fondo ligera dedicada al menú, no para MainWorld.
///
/// IMPORTANTE sobre el diseño: por defecto la cámara se queda ANCLADA a la posición/orientación
/// con la que la dejaste encuadrada a mano en el Editor — solo le añade una pequeña deriva
/// lateral (en su propio plano local, nunca alejándose del sujeto) y un balanceo de rotación muy
/// suave. No es una órbita alrededor de un punto lejano: eso requeriría saber a qué distancia y
/// altura está el sujeto (el reino), algo que este script no puede adivinar sin ver la escena, y
/// un cálculo mal encajado provoca justo lo que hace que este efecto se note mal: saltos de
/// posición y la cámara mirando a un punto que no es el que quieres. Si en el futuro quieres una
/// órbita real alrededor de un punto fijo, asigna <see cref="lookTarget"/> y sube
/// <see cref="driftRadius"/> a mano; con ambos campos vacíos el comportamiento es siempre "quieta
/// donde la dejaste, con un poco de vida".
///
/// Uso: colocar en una cámara 3D NUEVA y dedicada al backdrop del menú — NO en la Main Camera
/// que ya usa MainMenuController (esa solo necesita existir para el EventSystem/Canvas; el
/// Canvas del menú es Screen Space - Overlay, así que se dibuja encima de cualquier cámara sin
/// necesidad de Camera Stacking). Ver propuesta técnica para el resto del setup.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuWorldCameraDrift : MonoBehaviour
{
    [Header("Mirada (opcional)")]
    [Tooltip("Si se asigna, la cámara siempre mira hacia este punto (comportamiento de órbita real). Si se deja vacío (recomendado por defecto), la cámara conserva la orientación con la que la dejaste encuadrada en el Editor y solo le añade un balanceo sutil de rotación.")]
    [SerializeField] private Transform lookTarget;

    [Header("Deriva de posición (sutil, alrededor del encuadre original)")]
    [Tooltip("Radio del pequeño círculo que describe la cámara alrededor de su posición inicial, en su propio plano local (X=lateral, Y=arriba/abajo). Con el valor por defecto el movimiento es discreto, no una órbita grande.")]
    [SerializeField] private float driftRadius = 0.35f;
    [SerializeField] private float driftDegreesPerSecond = 6f;

    [Header("Balanceo (respiración de la cámara)")]
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 0.25f;
    [SerializeField] private float swayAmplitudeDegrees = 0.6f;
    [SerializeField] private float swaySpeed = 0.18f;

    float _angle;
    float _time;
    Vector3 _basePosition;
    Quaternion _baseRotation;

    void Awake()
    {
        // Punto de partida = donde tú dejaste la cámara encuadrada a mano. Todo lo demás es un
        // pequeño desplazamiento relativo a esto, nunca un salto a otro sitio.
        _basePosition = transform.position;
        _baseRotation = transform.rotation;
    }

    void Update()
    {
        _time += Time.deltaTime;
        _angle += driftDegreesPerSecond * Time.deltaTime;

        float rad = _angle * Mathf.Deg2Rad;
        // Círculo pequeño en el plano local de la cámara (right/up), NO en coordenadas de mundo:
        // así la deriva siempre es "lateral respecto a lo que ves", nunca se aleja del sujeto ni
        // depende de dónde esté el reino en el mundo.
        Vector3 localOffset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad) * 0.6f, 0f) * driftRadius;
        float bob = Mathf.Sin(_time * bobSpeed * Mathf.PI * 2f) * bobAmplitude;

        transform.position = _basePosition
                            + _baseRotation * localOffset
                            + _baseRotation * Vector3.up * bob;

        if (lookTarget)
        {
            transform.rotation = Quaternion.LookRotation(lookTarget.position - transform.position, Vector3.up);
        }
        else
        {
            float sway = Mathf.Sin(_time * swaySpeed * Mathf.PI * 2f) * swayAmplitudeDegrees;
            transform.rotation = _baseRotation * Quaternion.Euler(0f, sway, 0f);
        }
    }
}
