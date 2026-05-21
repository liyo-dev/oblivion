using UnityEngine;
using Game.NPC;

/// <summary>
/// Añade este componente a cualquier enemigo con Damageable para que suelte orbes al recibir daño.
///
/// Reglas de drops:
///   - Golpes de CUALQUIERA: tirada de vida/maná/nada
///   - Golpes de ESTELA:     tirada adicional de orbe especial de Estela
///   - Golpes de LIAM:       tirada adicional de orbe especial de Liam
/// </summary>
[RequireComponent(typeof(Damageable))]
public class OrbDropper : MonoBehaviour
{
    [Header("Orbes de Vida y Maná (cualquier golpe)")]
    [SerializeField] private GameObject healthOrbPrefab;
    [SerializeField] private GameObject manaOrbPrefab;
    [SerializeField, Range(0f, 1f)] private float healthChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float manaChance   = 0.20f;

    [Header("Orbes Especiales de Compañeros")]
    [SerializeField] private GameObject estelaOrbPrefab;
    [SerializeField] private GameObject liamOrbPrefab;
    [SerializeField, Range(0f, 1f)] private float specialChance = 0.45f;

    [Header("Spawn")]
    [SerializeField] private float spawnRadius = 0.4f;

    private Damageable _damageable;

    void Awake()
    {
        _damageable = GetComponent<Damageable>();
    }

    void OnEnable()  => _damageable.OnDamagedBy += OnDamagedBy;
    void OnDisable() => _damageable.OnDamagedBy -= OnDamagedBy;

    private void OnDamagedBy(float _, GameObject instigator)
    {
        // Tirada de vida / maná (cualquier golpe)
        float roll = Random.value;
        if (roll < healthChance)
            SpawnOrb(healthOrbPrefab);
        else if (roll < healthChance + manaChance)
            SpawnOrb(manaOrbPrefab);

        // Tirada de orbe especial: solo si el golpe lo dio un compañero
        DuoCompanion? companion = GetCompanionFromInstigator(instigator);
        if (companion.HasValue && Random.value < specialChance)
        {
            var prefab = companion.Value == DuoCompanion.Estela ? estelaOrbPrefab : liamOrbPrefab;
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

        Vector3 offset = Random.insideUnitSphere * spawnRadius;
        offset.y = Mathf.Abs(offset.y) + 0.2f;
        Instantiate(prefab, transform.position + offset, Quaternion.identity);
    }
}
