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

    [Header("Atracción")]
    [SerializeField] private float attractRadius = 5f;
    [SerializeField] private float attractSpeed  = 8f;

    [Header("Popup inicial")]
    [SerializeField] private float popForce = 4f;

    [Header("Duración")]
    [SerializeField] private float lifetime = 8f;

    [Header("Feedback")]
    [SerializeField] private GameObject pickupVFX;
    [SerializeField] private string     pickupSFXKey;

    private Transform _playerTransform;
    private Rigidbody _rb;
    private bool      _attracted;
    private float     _spawnTime;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        GetComponent<Collider>().isTrigger = true;
    }

    void Start()
    {
        _spawnTime = Time.time;

        if (PlayerService.TryGetPlayer(out var player))
            _playerTransform = player.transform;

        if (_rb != null)
        {
            Vector3 pop = (Vector3.up + Random.insideUnitSphere * 0.6f).normalized;
            _rb.AddForce(pop * popForce, ForceMode.Impulse);
        }
    }

    void Update()
    {
        if (Time.time - _spawnTime > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_playerTransform == null) return;

        float dist = Vector3.Distance(transform.position, _playerTransform.position);

        if (!_attracted && dist <= attractRadius)
        {
            _attracted = true;
            if (_rb != null)
            {
                _rb.isKinematic   = true;
                _rb.linearVelocity = Vector3.zero;
            }
        }

        if (_attracted)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                _playerTransform.position + Vector3.up * 0.5f,
                attractSpeed * Time.deltaTime
            );
        }
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
