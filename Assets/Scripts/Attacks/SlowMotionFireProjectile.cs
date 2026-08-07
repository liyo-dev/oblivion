using System;
using UnityEngine;

/// Proyectil enemigo para secuencias scripted.
/// Se mueve con Time.deltaTime, respetando Time.timeScale, para sincronizarse
/// con el fireball del jugador durante el slow-motion.
[DisallowMultipleComponent]
public class SlowMotionFireProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float turnSpeedDegPerSec = 60f;
    [SerializeField] private GameObject collisionVFX;
    [SerializeField] private float maxLifetimeSeconds = 12f;

    [Header("Feedback visual — se auto-genera si el prefab no trae ya un TrailRenderer/Light propios")]
    [Tooltip("Si el prefab ya tiene un TrailRenderer hijo configurado a mano, se respeta y no se crea uno automático.")]
    [SerializeField] private bool  autoTrail = true;
    [Tooltip("Si el prefab ya tiene una Light hija configurada a mano, se respeta y no se crea una automática.")]
    [SerializeField] private bool  autoGlow  = true;
    [SerializeField] private Color glowColor = new Color(1f, 0.35f, 0.15f);
    [Tooltip("Distancia al target a partir de la cual el brillo empieza a crecer (más cerca = más intenso).")]
    [SerializeField] private float glowMaxDistance   = 15f;
    [SerializeField] private float lightIntensityMin = 0.4f;
    [SerializeField] private float lightIntensityMax = 4f;

    // El sequencer escucha este evento para orquestar la explosión
    public event Action OnHitByPlayerFireball;

    private Transform _target;
    private bool _ended;
    private bool _paused;
    private float _spawnUnscaledTime;
    private Collider _col;
    private TrailRenderer _trail;
    private Light _light;
    private readonly Collider[] _overlapBuffer = new Collider[8];

    [Tooltip("Offset vertical sobre la posición del target (ajustar si el proyectil apunta demasiado bajo)")]
    [SerializeField] private float aimHeightOffset = 0.9f;

    void Awake()
    {
        _col = GetComponent<Collider>();
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        _trail = GetComponentInChildren<TrailRenderer>();
        if (_trail == null && autoTrail)
            _trail = CreateAutoTrail();

        _light = GetComponentInChildren<Light>();
        if (_light == null && autoGlow)
            _light = CreateAutoGlow();
    }

    // Genera un rastro básico (core brillante → transparente) para que el proyectil se lea como
    // algo que viene volando en vez de flotar quieto. No sustituye a un VFX artístico dedicado,
    // pero evita que la bola se vea "muerta" mientras nadie le añade una estela al prefab.
    private TrailRenderer CreateAutoTrail()
    {
        var go = new GameObject("AutoTrail");
        go.transform.SetParent(transform, false);
        var tr = go.AddComponent<TrailRenderer>();
        tr.time              = 0.25f;
        tr.startWidth        = 0.6f;
        tr.endWidth          = 0.05f;
        tr.minVertexDistance = 0.05f;
        tr.material          = new Material(Shader.Find("Sprites/Default"));

        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(glowColor, 0f), new GradientColorKey(glowColor * 0.5f, 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = grad;

        return tr;
    }

    // Luz que crece de intensidad conforme el proyectil se acerca al target: da la sensación de
    // amenaza creciente y hace que ilumine a Will/el entorno en vez de quedarse plano.
    private Light CreateAutoGlow()
    {
        var go = new GameObject("AutoGlow");
        go.transform.SetParent(transform, false);
        var l = go.AddComponent<Light>();
        l.type     = LightType.Point;
        l.color    = glowColor;
        l.range    = glowMaxDistance;
        l.intensity = lightIntensityMin;
        l.shadows  = LightShadows.None;
        return l;
    }

    private void UpdateGlow(float distanceToTarget)
    {
        if (_light == null) return;
        float proximity = Mathf.Clamp01(1f - distanceToTarget / glowMaxDistance);
        float pulse     = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
        _light.intensity = Mathf.Lerp(lightIntensityMin, lightIntensityMax, proximity) * pulse;
    }

    public void Launch(Transform target)
    {
        _target = target;
        _spawnUnscaledTime = Time.unscaledTime;

        if (_target != null)
        {
            Vector3 aimPos = _target.position + Vector3.up * aimHeightOffset;
            transform.LookAt(aimPos);
        }
    }

    /// Congela el proyectil en el aire; útil durante el panic input.
    public void Pause()
    {
        _paused = true;
        // Deshabilitar collider para que el fireball de Will no lo detone al spawnear cerca
        if (_col) _col.enabled = false;
    }

    /// Reanuda el movimiento tras un Pause(). Reactiva el collider un frame después
    /// para dar tiempo a que los dos proyectiles se separen antes de poder colisionar.
    public void Resume()
    {
        _paused = false;
        StartCoroutine(ReenableCollider());
    }

    private System.Collections.IEnumerator ReenableCollider()
    {
        yield return null;
        if (!_ended && _col) _col.enabled = true;
    }

    /// Llamado por el sequencer cuando quiere forzar la colisión desde código
    public void ForceCollide()
    {
        if (_ended) return;
        HandleCollision(transform.position);
    }

    void Update()
    {
        if (_ended || _paused) return;

        if (Time.unscaledTime - _spawnUnscaledTime > maxLifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (_target != null)
        {
            Vector3 aimPos = _target.position + Vector3.up * aimHeightOffset;
            Vector3 toTarget = aimPos - transform.position;
            UpdateGlow(toTarget.magnitude);
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Vector3 dir = toTarget.normalized;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up),
                    turnSpeedDegPerSec * Time.deltaTime);
                transform.position += dir * speed * Time.deltaTime;
            }
        }
        else
        {
            transform.position += transform.forward * speed * Time.deltaTime;
        }

    }

    void OnTriggerEnter(Collider other)
    {
        if (_ended) return;
        if (other.TryGetComponent<MagicProjectile>(out _))
            HandleCollision(other.ClosestPoint(transform.position));
    }

    void OnCollisionEnter(Collision col)
    {
        if (_ended) return;
        if (col.gameObject.TryGetComponent<MagicProjectile>(out _))
            HandleCollision(col.GetContact(0).point);
    }

    private void HandleCollision(Vector3 point)
    {
        _ended = true;

        if (collisionVFX != null)
            VfxPoolService.Instance.Play(collisionVFX, point, Quaternion.identity, 3f);

        OnHitByPlayerFireball?.Invoke();
        Destroy(gameObject);
    }
}
