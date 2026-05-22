using UnityEngine;

public enum OrbType { Health, Mana, SpecialCharge }

[RequireComponent(typeof(Collider))]
public class BattleOrb : MonoBehaviour
{
    [Header("Tipo")]
    [SerializeField] private OrbType      type      = OrbType.Health;
    [Tooltip("Solo si type == SpecialCharge: qué medidor llenar")]
    [SerializeField] private DuoCompanion chargeFor = DuoCompanion.Estela;

    [Header("Cantidades")]
    [SerializeField] private float healthAmount = 15f;
    [SerializeField] private float manaAmount   = 20f;
    [SerializeField] private float chargeAmount = 1f;

    [Header("Lanzamiento")]
    [SerializeField] private float popUpForce          = 5f;
    [SerializeField] private float popHorizontalSpread = 1.5f;

    [Header("Hover en suelo")]
    [SerializeField] private float hoverHeight    = 0.35f;
    [SerializeField] private float hoverAmplitude = 0.1f;
    [SerializeField] private float hoverFrequency = 2.5f;
    [SerializeField] private float minHoverTime   = 0.3f;

    [Header("Atracción")]
    [SerializeField] private float attractRadius       = 5f;
    [SerializeField] private float attractMaxSpeed     = 12f;
    [SerializeField] private float attractAcceleration = 20f;

    [Header("Suelo")]
    [Tooltip("Asigna solo la layer Ground para evitar detectar los colliders del enemigo")]
    [SerializeField] private LayerMask groundLayers = 1; // Default layer (layer 0)

    [Header("Duración")]
    [SerializeField] private float lifetime = 8f;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField] private string     pickupSFXKey;

    private enum Phase { Launching, Hovering, Attracted }

    private Transform _playerTransform;
    private Rigidbody _rb;
    private Phase     _phase;
    private float     _groundY;
    private float     _hoverOffset;
    private float     _hoverStartTime;
    private float     _spawnTime;
    private float     _attractSpeed;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        _spawnTime   = Time.time;
        _hoverOffset = Random.Range(0f, Mathf.PI * 2f);
        _phase       = Phase.Launching;

        if (PlayerService.TryGetPlayer(out var player))
            _playerTransform = player.transform;

        DetectGroundY();

        if (_rb != null)
        {
            _rb.isKinematic    = false;
            _rb.useGravity     = true;
            _rb.linearVelocity = Vector3.zero;

            // Arco con spread horizontal aleatorio
            Vector2 h = Random.insideUnitCircle.normalized * popHorizontalSpread;
            _rb.AddForce(new Vector3(h.x, popUpForce, h.y), ForceMode.Impulse);

            // Spin visual durante el vuelo
            _rb.angularVelocity = Random.insideUnitSphere * 3f;
        }
    }

    void DetectGroundY()
    {
        // Raycast desde bastante arriba para evitar golpear colliders del propio enemigo
        Vector3 origin = transform.position + Vector3.up * 5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 30f, groundLayers, QueryTriggerInteraction.Ignore))
            _groundY = hit.point.y + hoverHeight;
        else
            _groundY = transform.position.y - 1f + hoverHeight; // Fallback: asume ~1m sobre el suelo
    }

    void Update()
    {
        if (Time.time - _spawnTime > lifetime) { Destroy(gameObject); return; }
        if (_playerTransform == null) return;

        switch (_phase)
        {
            case Phase.Launching: UpdateLaunching(); break;
            case Phase.Hovering:  UpdateHovering();  break;
            case Phase.Attracted: UpdateAttracted(); break;
        }
    }

    void UpdateLaunching()
    {
        bool falling    = _rb != null && _rb.linearVelocity.y <= 0f;
        bool nearGround = transform.position.y <= _groundY + 0.15f;

        if (falling && nearGround)
            EnterHovering();
        else if (Time.time - _spawnTime > 4f)
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

        transform.position = new Vector3(transform.position.x, _groundY, transform.position.z);
    }

    void UpdateHovering()
    {
        float y = _groundY + Mathf.Sin((Time.time + _hoverOffset) * hoverFrequency) * hoverAmplitude;
        transform.position = new Vector3(transform.position.x, y, transform.position.z);

        if (Time.time - _hoverStartTime < minHoverTime) return;

        if (Vector3.Distance(transform.position, _playerTransform.position) <= attractRadius)
            EnterAttracted();
    }

    void EnterAttracted()
    {
        _phase        = Phase.Attracted;
        _attractSpeed = 2f; // Arranca lento y acelera → efecto "succión" estilo KH
    }

    void UpdateAttracted()
    {
        _attractSpeed = Mathf.MoveTowards(_attractSpeed, attractMaxSpeed, attractAcceleration * Time.deltaTime);
        transform.position = Vector3.MoveTowards(
            transform.position,
            _playerTransform.position + Vector3.up * 0.5f,
            _attractSpeed * Time.deltaTime
        );
    }

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

        Destroy(gameObject);
    }
}
