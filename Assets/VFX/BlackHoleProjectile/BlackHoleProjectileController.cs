using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class BlackHoleProjectileController : MonoBehaviour
{
    [Header("Vínculos")]
    public Rigidbody rb; // velocidad para reactividad (opcional)

    [Header("Pulso")]
    public float pulseSpeed = 6f;
    public float pulseAmount = 0.05f; // cuánto varía el radio

    [Header("Distorsión dinámica")]
    public float distMin = 0.08f;
    public float distMax = 0.18f;
    public float distWithSpeed = 0.002f; // añade según magnitud de la velocidad

    [Header("Spin noise")]
    public Vector2 noiseScrollBase = new Vector2(0.2f, 0.1f);
    public float noiseSpin = 1.2f; // añade rotación virtual al noise

    Material _mat;
    int _CoreRadiusID, _DistortionID, _NoiseSpeedID;

    float _baseRadius;

    void Awake()
    {
        _mat = GetComponent<Renderer>().material; // instancia propia del material
        _CoreRadiusID = Shader.PropertyToID("_CoreRadius");
        _DistortionID = Shader.PropertyToID("_Distortion");
        _NoiseSpeedID = Shader.PropertyToID("_NoiseSpeed");
        _baseRadius = _mat.GetFloat(_CoreRadiusID);
    }

    void Update()
    {
        float t = Time.time;

        // Pulso suave del radio
        float pr = Mathf.Sin(t * pulseSpeed) * 0.5f + 0.5f;
        float radius = _baseRadius + (pr - 0.5f) * 2f * pulseAmount;
        _mat.SetFloat(_CoreRadiusID, Mathf.Clamp(radius, 0.02f, 0.45f));

        // Distorsión según velocidad
        float speed = rb ? rb.linearVelocity.magnitude : 0f;
        float dist = Mathf.Lerp(distMin, distMax, Mathf.InverseLerp(0f, 40f, speed)) + speed * distWithSpeed;
        _mat.SetFloat(_DistortionID, dist);

        // Noise que “gira”
        var rot = new Vector2(Mathf.Cos(t * noiseSpin), Mathf.Sin(t * noiseSpin));
        var ns = noiseScrollBase + rot * 0.1f;
        _mat.SetVector(_NoiseSpeedID, new Vector4(ns.x, ns.y, 0, 0));
    }
}
