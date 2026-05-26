using UnityEngine;
using Game.NPC;

/// <summary>
/// Componente para enemigos con Damageable: suelta orbes estilo Kingdom Hearts al recibir dano.
///
/// Reglas de drops:
///   - Golpes de CUALQUIERA: tirada de vida/mana/nada (puede soltar multiples orbes)
///   - Golpes de ESTELA:     tirada adicional de orbe especial de Estela
///   - Golpes de LIAM:       tirada adicional de orbe especial de Liam
///
/// Los orbes salen con un burst radial hacia arriba desde el enemigo.
/// </summary>
[RequireComponent(typeof(Damageable))]
public class OrbDropper : MonoBehaviour
{
    [Header("Orbes de Vida y Mana (cualquier golpe)")]
    [SerializeField] private GameObject healthOrbPrefab;
    [SerializeField] private GameObject manaOrbPrefab;
    [SerializeField, Range(0f, 1f)] private float healthChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float manaChance   = 0.20f;

    [Header("Orbes Especiales de Companeros")]
    [SerializeField] private GameObject estelaOrbPrefab;
    [SerializeField] private GameObject liamOrbPrefab;
    [SerializeField, Range(0f, 1f)] private float specialChance = 0.45f;

    [Header("Burst de multiples orbes")]
    [Tooltip("Minimo de orbes de vida/mana por golpe (si la tirada sale positiva)")]
    [SerializeField] private int minOrbsPerHit = 1;
    [Tooltip("Maximo de orbes de vida/mana por golpe")]
    [SerializeField] private int maxOrbsPerHit = 3;

    [Header("Spawn")]
    [SerializeField] private float spawnRadius   = 0.5f;
    [SerializeField] private float spawnHeightMin = 0.3f;
    [SerializeField] private float spawnHeightMax = 0.8f;

    [Header("Burst al morir")]
    [Tooltip("Orbes extra al morir el enemigo")]
    [SerializeField] private int   deathBurstMin = 4;
    [SerializeField] private int   deathBurstMax = 8;
    [Tooltip("Probabilidad de cada tipo en el burst de muerte (vida/mana/especial)")]
    [SerializeField, Range(0f, 1f)] private float deathHealthWeight  = 0.5f;
    [SerializeField, Range(0f, 1f)] private float deathManaWeight    = 0.3f;

    private Damageable _damageable;

    void Awake()
    {
        _damageable = GetComponent<Damageable>();
    }

    void OnEnable()
    {
        _damageable.OnDamagedBy += OnDamagedBy;
        _damageable.OnDied      += OnDied;
    }

    void OnDisable()
    {
        _damageable.OnDamagedBy -= OnDamagedBy;
        _damageable.OnDied      -= OnDied;
    }

    private void OnDamagedBy(float _, GameObject instigator)
    {
        // Tirada de vida / mana (cualquier golpe)
        float roll = Random.value;
        if (roll < healthChance)
        {
            int count = Random.Range(minOrbsPerHit, maxOrbsPerHit + 1);
            for (int i = 0; i < count; i++)
                SpawnOrb(healthOrbPrefab);
        }
        else if (roll < healthChance + manaChance)
        {
            int count = Random.Range(minOrbsPerHit, maxOrbsPerHit + 1);
            for (int i = 0; i < count; i++)
                SpawnOrb(manaOrbPrefab);
        }

        // Tirada de orbe especial: solo si el golpe lo dio un companero
        DuoCompanion? companion = GetCompanionFromInstigator(instigator);
        if (companion.HasValue && Random.value < specialChance)
        {
            var prefab = companion.Value == DuoCompanion.Estela ? estelaOrbPrefab : liamOrbPrefab;
            SpawnOrb(prefab);
        }
    }

    private void OnDied()
    {
        int burstCount = Random.Range(deathBurstMin, deathBurstMax + 1);
        for (int i = 0; i < burstCount; i++)
        {
            float roll = Random.value;
            float totalWeight = deathHealthWeight + deathManaWeight;

            GameObject prefab;
            if (roll < deathHealthWeight)
                prefab = healthOrbPrefab;
            else if (roll < totalWeight)
                prefab = manaOrbPrefab;
            else
            {
                // Orbe especial aleatorio entre los dos companeros
                prefab = Random.value < 0.5f ? estelaOrbPrefab : liamOrbPrefab;
            }

            SpawnOrb(prefab);
        }
    }

    private DuoCompanion? GetCompanionFromInstigator(GameObject instigator)
    {
        if (instigator == null) return null;

        var member = instigator.GetComponentInParent<NPCPartyMember>();
        if (member == null) return null;

        string name = member.DisplayName;
        if (name.Equals("Estela", System.StringComparison.OrdinalIgnoreCase)) return DuoCompanion.Estela;
        if (name.Equals("Liam",   System.StringComparison.OrdinalIgnoreCase)) return DuoCompanion.Liam;

        return null;
    }

    private void SpawnOrb(GameObject prefab)
    {
        if (prefab == null) return;

        // Patron de burst radial: cada orbe sale en una direccion diferente
        Vector2 circle = Random.insideUnitCircle * spawnRadius;
        float height = Random.Range(spawnHeightMin, spawnHeightMax);
        Vector3 offset = new Vector3(circle.x, height, circle.y);

        Instantiate(prefab, transform.position + offset, Quaternion.identity);
    }
}
