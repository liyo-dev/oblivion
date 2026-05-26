using UnityEngine;

public enum OrbType { Health, Mana, SpecialCharge }

/// <summary>
/// Orbe de combate estilo Kingdom Hearts.
/// Fases: Launch (impulso hacia arriba) -> Bounce (rebotes contra el suelo) ->
///        Hover (flotar con sine wave) -> Attracted (vuela hacia el jugador).
/// Efectos visuales procedurales: rotacion, escala pulsante, glow con Light.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BattleOrb : MonoBehaviour
{
    [Header("Tipo")]
    [SerializeField] private OrbType      type      = OrbType.Health;
    [Tooltip("Solo si type == SpecialCharge: que medidor llenar")]
    [SerializeField] private DuoCompanion chargeFor = DuoCompanion.Estela;

    [Header("Cantidades")]
    [SerializeField] private float healthAmount = 15f;
    [SerializeField] private float manaAmount   = 20f;
    [SerializeField] private float chargeAmount = 1f;

    [Header("Lanzamiento")]
    [SerializeField] private float popUpForce          = 7f;
    [SerializeField] private float popHorizontalSpread = 2f;

    [Header("Rebote")]
    [Tooltip("Cuantas veces rebota antes de flotar")]
    [SerializeField] private int   maxBounces      = 3;
    [Tooltip("Factor de restitucion (0 = sin rebote, 1 = rebote perfecto)")]
    [SerializeField, Range(0f, 1f)] private float bounciness = 0.55f;
    [Tooltip("Velocidad vertical minima para contar como rebote")]
    [SerializeField] private float minBounceSpeed  = 0.8f;

    [Header("Hover")]
    [SerializeField] private float hoverHeight    = 0.35f;
    [SerializeField] private float hoverAmplitude = 0.08f;
    [SerializeField] private float hoverFrequency = 3f;
    [SerializeField] private float minHoverTime   = 0.25f;

    [Header("Atraccion")]
    [SerializeField] private float attractRadius       = 5f;
    [SerializeField] private float attractMaxSpeed     = 16f;
    [SerializeField] private float attractAcceleration = 28f;

    [Header("Rotacion visual")]
    [SerializeField] private float spinSpeed   = 360f;
    [SerializeField] private float wobbleSpeed = 120f;

    [Header("Escala pulsante")]
    [SerializeField] private float pulseAmplitude = 0.12f;
    [SerializeField] private float pulseFrequency = 4f;

    [Header("Glow (Light procedural)")]
    [Tooltip("Activar point light procedural")]
    [SerializeField] private bool  enableGlow     = true;
    [SerializeField] private float glowRange      = 1.5f;
    [SerializeField] private float glowIntensity  = 1.2f;
    [SerializeField] private float glowPulseSpeed = 3f;
    [SerializeField] private Color glowColorOverride = Color.clear;

    [Header("Trail (procedural)")]
    [Tooltip("Activar trail renderer procedural durante atraccion")]
    [SerializeField] private bool  enableTrail     = true;
    [SerializeField] private float trailTime       = 0.25f;
    [SerializeField] private float trailStartWidth = 0.15f;

    [Header("Suelo")]
    [Tooltip("Layer del suelo. Con -1 detecta cualquier superficie no-trigger")]
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Duracion")]
    [SerializeField] private float lifetime = 8f;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField] private string     pickupSFXKey;
    [SerializeField] private string     bounceSFXKey;

    // --- Fases internas ---
    private enum Phase { Launching, Bouncing, Hovering, Attracted }

    private Transform     _playerTransform;
    private Rigidbody     _rb;
    private Phase         _phase;
    private float         _groundY;
    private float         _hoverOffset;
    private float         _hoverStartTime;
    private float         _spawnTime;
    private float         _spawnY;
    private float         _attractSpeed;
    private int           _bounceCount;
    private bool          _wasGoingUp;
    private Vector3       _baseScale;
    private Light         _glowLight;
    private TrailRenderer _trail;
    private float         _pulseOffset;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        GetComponent<Collider>().isTrigger = true;
        _baseScale = transform.localScale;
    }

    void Start()
    {
        _spawnTime   = Time.time;
        _spawnY      = transform.position.y;
        _hoverOffset = Random.Range(0f, Mathf.PI * 2f);
        _pulseOffset = Random.Range(0f, Mathf.PI * 2f);
        _phase       = Phase.Launching;
        _bounceCount = 0;
        _wasGoingUp  = true;

        if (PlayerService.TryGetPlayer(out var player))
            _playerTransform = player.transform;

        _groundY = DetectGroundY();

        if (_rb != null)
        {
            _rb.isKinematic    = false;
            _rb.useGravity     = true;
            _rb.linearVelocity = Vector3.zero;

            Vector2 h = Random.insideUnitCircle.normalized * popHorizontalSpread;
            float upForce = popUpForce + Random.Range(-1f, 1f);
            _rb.AddForce(new Vector3(h.x, upForce, h.y), ForceMode.Impulse);
            _rb.angularVelocity = Random.insideUnitSphere * 5f;
        }

        SetupProceduralVisuals();
    }

    // --- Efectos visuales procedurales ---

    void SetupProceduralVisuals()
    {
        // Glow: point light hijo
        if (enableGlow)
        {
            var lightGO = new GameObject("OrbGlow");
            lightGO.transform.SetParent(transform, false);
            lightGO.transform.localPosition = Vector3.zero;
            _glowLight = lightGO.AddComponent<Light>();
            _glowLight.type      = LightType.Point;
            _glowLight.range     = glowRange;
            _glowLight.intensity = glowIntensity;
            _glowLight.color     = ResolveGlowColor();
            _glowLight.shadows   = LightShadows.None;
            _glowLight.renderMode = LightRenderMode.Auto;
        }

        // Trail: solo visible durante atraccion, inactivo al inicio
        if (enableTrail)
        {
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time       = trailTime;
            _trail.startWidth = trailStartWidth;
            _trail.endWidth   = 0f;
            _trail.material   = CreateTrailMaterial();
            Color tColor = ResolveGlowColor();
            _trail.startColor = tColor;
            _trail.endColor   = new Color(tColor.r, tColor.g, tColor.b, 0f);
            _trail.enabled    = false;
        }
    }

    Color ResolveGlowColor()
    {
        if (glowColorOverride != Color.clear) return glowColorOverride;
        switch (type)
        {
            case OrbType.Health:        return new Color(0.2f, 1f, 0.3f);
            case OrbType.Mana:          return new Color(0.3f, 0.5f, 1f);
            case OrbType.SpecialCharge: return new Color(1f, 0.85f, 0.2f);
            default:                    return Color.white;
        }
    }

    Material CreateTrailMaterial()
    {
        var shader = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) return null;
        return new Material(shader);
    }

    // --- Deteccion de suelo ---

    float DetectGroundY()
    {
        Vector3 origin = transform.position + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, groundLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.point.y < transform.position.y)
                return hit.point.y;
        }
        return transform.position.y - 0.5f;
    }

    // --- Update principal ---

    void Update()
    {
        if (Time.time - _spawnTime > lifetime) { Destroy(gameObject); return; }

        UpdateVisuals();

        if (_playerTransform == null) return;

        switch (_phase)
        {
            case Phase.Launching: UpdateLaunching(); break;
            case Phase.Bouncing:  UpdateBouncing();  break;
            case Phase.Hovering:  UpdateHovering();  break;
            case Phase.Attracted: UpdateAttracted(); break;
        }
    }

    // --- Efectos visuales en Update ---

    void UpdateVisuals()
    {
        float t = Time.time;

        // Rotacion visual (spin + wobble)
        float spin   = spinSpeed * Time.deltaTime;
        float wobble = Mathf.Sin(t * wobbleSpeed * Mathf.Deg2Rad) * 15f * Time.deltaTime;
        transform.Rotate(wobble, spin, 0f, Space.Self);

        // Escala pulsante
        float pulse = 1f + Mathf.Sin((t + _pulseOffset) * pulseFrequency * Mathf.PI * 2f) * pulseAmplitude;
        transform.localScale = _baseScale * pulse;

        // Glow pulsante
        if (_glowLight != null)
        {
            float glowPulse = glowIntensity * (0.7f + 0.3f * Mathf.Sin(t * glowPulseSpeed * Mathf.PI * 2f));
            _glowLight.intensity = glowPulse;
        }
    }

    // --- Fases ---

    void UpdateLaunching()
    {
        if (_rb == null) { EnterHovering(); return; }

        bool goingUp = _rb.linearVelocity.y > 0f;

        // Detectar cuando empieza a caer: transicion a Bouncing
        if (_wasGoingUp && !goingUp)
        {
            _phase = Phase.Bouncing;
            return;
        }
        _wasGoingUp = goingUp;

        // Timeout de seguridad
        if (Time.time - _spawnTime > 4f)
            EnterHovering();
    }

    void UpdateBouncing()
    {
        if (_rb == null) { EnterHovering(); return; }

        float posY = transform.position.y;
        float velY = _rb.linearVelocity.y;

        // Detectar contacto con el suelo (posicion cerca de groundY y cayendo)
        bool nearGround = posY <= _groundY + hoverHeight + 0.1f;
        bool falling    = velY <= 0f;

        if (nearGround && falling)
        {
            _bounceCount++;

            if (_bounceCount >= maxBounces || Mathf.Abs(velY) < minBounceSpeed)
            {
                EnterHovering();
                return;
            }

            // Simular rebote manualmente para control preciso
            Vector3 vel = _rb.linearVelocity;
            vel.y = Mathf.Abs(vel.y) * bounciness;
            // Reducir velocidad horizontal en cada rebote
            vel.x *= 0.7f;
            vel.z *= 0.7f;
            _rb.linearVelocity = vel;

            // Forzar posicion para evitar atravesar el suelo
            Vector3 pos = transform.position;
            pos.y = _groundY + hoverHeight + 0.05f;
            transform.position = pos;

            PlayBounceSFX();
        }

        // Timeout de seguridad
        if (Time.time - _spawnTime > 4f)
            EnterHovering();
    }

    void EnterHovering()
    {
        _phase          = Phase.Hovering;
        _hoverStartTime = Time.time;

        if (_rb != null)
        {
            _rb.useGravity      = false;
            _rb.isKinematic     = true;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        float safeY = Mathf.Max(_groundY + hoverHeight, _spawnY - 0.2f);
        transform.position = new Vector3(transform.position.x, safeY, transform.position.z);
        _groundY = safeY - hoverHeight;
    }

    void UpdateHovering()
    {
        float baseY = _groundY + hoverHeight;
        float y = baseY + Mathf.Sin((Time.time + _hoverOffset) * hoverFrequency) * hoverAmplitude;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        if (Time.time - _hoverStartTime < minHoverTime) return;

        if (Vector3.Distance(transform.position, _playerTransform.position) <= attractRadius)
            EnterAttracted();
    }

    void EnterAttracted()
    {
        _phase        = Phase.Attracted;
        _attractSpeed = 3f;

        // Activar trail durante atraccion
        if (_trail != null)
        {
            _trail.Clear();
            _trail.enabled = true;
        }
    }

    void UpdateAttracted()
    {
        _attractSpeed = Mathf.MoveTowards(_attractSpeed, attractMaxSpeed, attractAcceleration * Time.deltaTime);

        Vector3 target = _playerTransform.position + Vector3.up * 0.8f;
        Vector3 dir    = (target - transform.position);
        float   dist   = dir.magnitude;

        if (dist < 0.01f)
        {
            transform.position = target;
            return;
        }

        // Curva suave: los orbes no van en linea recta, suben ligeramente y curvan
        // Efecto de "swoosh" similar a KH
        Vector3 moveDir = dir.normalized;
        float distFactor = Mathf.Clamp01(dist / attractRadius);
        float liftAmount = distFactor * 0.5f;
        moveDir.y += liftAmount;
        moveDir.Normalize();

        transform.position += moveDir * (_attractSpeed * Time.deltaTime);
    }

    // --- Colision con jugador ---

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        Apply(other.gameObject);
    }

    private void Apply(GameObject playerGO)
    {
        switch (type)
        {
            case OrbType.Health:
                var phs = playerGO.GetComponentInParent<PlayerHealthSystem>();
                if (phs) phs.Heal(healthAmount);
                break;

            case OrbType.Mana:
                var mana = playerGO.GetComponentInParent<ManaPool>();
                if (mana) mana.Refill(manaAmount);
                break;

            case OrbType.SpecialCharge:
                var duo = playerGO.GetComponentInParent<DuoSpecialAttackSystem>();
                if (duo) duo.AddCharge(chargeFor, chargeAmount);
                break;
        }

        if (pickupVFX)
            Destroy(Instantiate(pickupVFX, transform.position, Quaternion.identity), 2f);

        if (!string.IsNullOrEmpty(pickupSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(pickupSFXKey);

        // Efecto de absorcion: flash de escala antes de destruir
        Destroy(gameObject);
    }

    // --- Utilidades ---

    void PlayBounceSFX()
    {
        if (string.IsNullOrEmpty(bounceSFXKey)) return;
        if (AudioService.Instance != null)
            AudioService.Instance.PlaySFX(bounceSFXKey);
    }
}
