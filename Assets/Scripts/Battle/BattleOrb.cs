using UnityEngine;

public enum OrbType { Health, Mana, SpecialCharge }

/// <summary>
/// Orbe de combate.
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
    [Tooltip("Altura sobre el suelo detectado. Se mantiene baja para que el orbe parezca apoyado en el suelo, no flotando.")]
    [SerializeField] private float hoverHeight    = 0.12f;
    [Tooltip("Amplitud del bamboleo vertical. 0 = el orbe queda quieto sobre el suelo (sin flotar).")]
    [SerializeField] private float hoverAmplitude = 0f;
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
    [Tooltip("Activar point light procedural. Desactivado por defecto: los orbes no deben emitir luz.")]
    [SerializeField] private bool  enableGlow     = false;
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
    // spawnSFXKey y attractSFXKey se inyectan desde OrbDropper via SetAudioKeys()
    private string _spawnSFXKey;
    private string _attractSFXKey;

    public void SetAudioKeys(string spawn, string attract)
    {
        _spawnSFXKey   = spawn;
        _attractSFXKey = attract;
    }

    /// Llamar antes de Start() cuando el componente se añade en runtime.
    public void Configure(OrbType orbType, float amount)
    {
        type = orbType;
        switch (orbType)
        {
            case OrbType.Health: healthAmount = amount; break;
            case OrbType.Mana:   manaAmount   = amount; break;
        }
    }

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
    private bool          _justBounced;
    private Vector3       _baseScale;
    private Light         _glowLight;
    private TrailRenderer _trail;
    private float         _pulseOffset;
    private LayerMask     _groundMask;

    // Capas que nunca son suelo (enemigos, proyectiles, jugador)
    private static readonly int[] _nonGroundLayers = { 3, 6, 7, 11 };

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        GetComponent<Collider>().isTrigger = true;
        _baseScale = transform.localScale;

        _groundMask = groundLayers;
        foreach (int layer in _nonGroundLayers)
            _groundMask &= ~(1 << layer);
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
            _rb.AddForce(new Vector3(h.x, upForce, h.y), ForceMode.VelocityChange);
            _rb.angularVelocity = Random.insideUnitSphere * 5f;
        }

        SetupProceduralVisuals();

        if (!string.IsNullOrEmpty(_spawnSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(_spawnSFXKey, 1f, transform.position);
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
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, _groundMask, QueryTriggerInteraction.Ignore))
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

        // Actualizar groundY mientras cae para corregir deteccion inicial erronea
        // (ej: colisionador del enemigo interpuesto en el raycast de Start)
        if (velY < -0.5f)
            _groundY = RaycastGroundY();

        bool nearGround = posY <= _groundY + hoverHeight + 0.1f;
        bool falling    = velY <= 0f;

        // _justBounced evita contar multiples rebotes en el mismo contacto frame a frame
        if (nearGround && falling && !_justBounced)
        {
            _bounceCount++;
            _justBounced = true;

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

        // Listo para detectar el siguiente rebote cuando sube con velocidad suficiente
        if (velY > 0.5f)
            _justBounced = false;

        // Timeout de seguridad
        if (Time.time - _spawnTime > 4f)
            EnterHovering();
    }

    float RaycastGroundY()
    {
        Vector3 origin = transform.position + Vector3.up * 0.3f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 25f, _groundMask, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return _groundY;
    }

    void EnterHovering()
    {
        _phase          = Phase.Hovering;
        _hoverStartTime = Time.time;

        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity      = false;
            _rb.isKinematic     = true;
        }

        // FIX INC-051: antes se usaba Mathf.Max(_groundY + hoverHeight, _spawnY - 0.2f). El orbe
        // aparece 1.5-2.3m por encima del enemigo (spawnCenterOffset + altura aleatoria), así que
        // "_spawnY - 0.2f" casi siempre era MAYOR que la altura real del suelo y ganaba el Max():
        // el orbe se teletransportaba de vuelta cerca de su altura de aparición justo al empezar a
        // flotar, aunque ya hubiera rebotado correctamente hasta el suelo. Resultado: las bolitas
        // se quedaban flotando en el aire en vez de posarse. Recalculamos el suelo real aquí mismo
        // (con fallback al último _groundY conocido si el raycast no encuentra nada) en vez de
        // usar la altura de aparición como referencia.
        _groundY = RaycastGroundY();
        float safeY = _groundY + hoverHeight;
        transform.position = new Vector3(transform.position.x, safeY, transform.position.z);
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

        if (_trail != null)
        {
            _trail.Clear();
            _trail.enabled = true;
        }

        if (!string.IsNullOrEmpty(_attractSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(_attractSFXKey, 1f, transform.position);
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
            VfxPoolService.Instance.Play(pickupVFX, transform.position, Quaternion.identity, 2f);

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
