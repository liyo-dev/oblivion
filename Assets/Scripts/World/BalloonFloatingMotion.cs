using UnityEngine;

/// <summary>
/// Añade movimiento flotante y rotación al globo para darle vida.
/// Combina oscilación vertical, balanceo horizontal y rotación suave.
/// </summary>
public class BalloonFloatingMotion : MonoBehaviour
{
    [Header("Oscilación Vertical (Flotación)")]
    [Tooltip("Amplitud del movimiento arriba/abajo en metros")]
    [SerializeField] private float verticalAmplitude = 0.5f;
    [Tooltip("Velocidad del movimiento vertical")]
    [SerializeField] private float verticalSpeed = 1.0f;

    [Header("Balanceo Horizontal")]
    [Tooltip("Amplitud del balanceo lateral en metros")]
    [SerializeField] private float horizontalAmplitude = 0.3f;
    [Tooltip("Velocidad del balanceo horizontal")]
    [SerializeField] private float horizontalSpeed = 0.8f;

    [Header("Rotación")]
    [Tooltip("Ángulo máximo de rotación en el eje Y")]
    [SerializeField] private float rotationAmount = 15f;
    [Tooltip("Velocidad de rotación")]
    [SerializeField] private float rotationSpeed = 0.5f;

    [Header("Variación (para múltiples globos)")]
    [Tooltip("Offset aleatorio para que múltiples globos no se muevan igual")]
    [SerializeField] private bool randomizeOffset = true;

    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private float _timeOffset;

    void Start()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;
        
        if (randomizeOffset)
        {
            _timeOffset = Random.Range(0f, 100f);
        }
    }

    void Update()
    {
        float time = Time.time + _timeOffset;

        // Movimiento vertical (flotación)
        float yOffset = Mathf.Sin(time * verticalSpeed) * verticalAmplitude;

        // Balanceo horizontal (simulando viento)
        float xOffset = Mathf.Sin(time * horizontalSpeed) * horizontalAmplitude;
        float zOffset = Mathf.Cos(time * horizontalSpeed * 0.7f) * horizontalAmplitude * 0.5f;

        // Aplicar posición
        Vector3 newPosition = _startPosition + new Vector3(xOffset, yOffset, zOffset);
        transform.position = newPosition;

        // Rotación suave en Y (giro)
        float yRotation = Mathf.Sin(time * rotationSpeed) * rotationAmount;
        
        // Pequeña inclinación en X y Z por el balanceo
        float xTilt = Mathf.Sin(time * horizontalSpeed) * (rotationAmount * 0.3f);
        float zTilt = Mathf.Cos(time * horizontalSpeed * 0.7f) * (rotationAmount * 0.2f);

        Quaternion additionalRotation = Quaternion.Euler(xTilt, yRotation, zTilt);
        transform.rotation = _startRotation * additionalRotation;
    }

    // Método para ajustar la intensidad del movimiento en runtime
    public void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        verticalAmplitude = 0.5f * intensity;
        horizontalAmplitude = 0.3f * intensity;
        rotationAmount = 15f * intensity;
    }

    // Visualización en el editor
    void OnDrawGizmosSelected()
    {
        Vector3 center = Application.isPlaying ? _startPosition : transform.position;
        
        Gizmos.color = Color.yellow;
        // Rango vertical
        Gizmos.DrawLine(center + Vector3.up * verticalAmplitude, center - Vector3.up * verticalAmplitude);
        
        Gizmos.color = Color.cyan;
        // Rango horizontal X
        Gizmos.DrawLine(center + Vector3.right * horizontalAmplitude, center - Vector3.right * horizontalAmplitude);
        
        Gizmos.color = Color.blue;
        // Rango horizontal Z
        Gizmos.DrawLine(center + Vector3.forward * (horizontalAmplitude * 0.5f), 
                       center - Vector3.forward * (horizontalAmplitude * 0.5f));
        
        // Esfera en el centro
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(center, 0.1f);
    }
}
