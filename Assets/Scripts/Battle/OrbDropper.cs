using UnityEngine;

/// <summary>
/// Componente thin de drops de orbes. No necesita configuracion manual:
/// OrbDropService lo inyecta automaticamente en enemigos (layer Enemy + Damageable).
///
/// Para usar un perfil distinto al default (ej: jefes), anadir este componente
/// al prefab del enemigo y rellenar solo profileId.
/// Para un punto de spawn preciso, asignar spawnOrigin.
/// </summary>
[RequireComponent(typeof(Damageable))]
public class OrbDropper : MonoBehaviour
{
    [Tooltip("Vacio = perfil del tag del objeto; si tampoco hay mapeo, usa 'default'")]
    [SerializeField] private string    _profileId;
    [Tooltip("Punto de spawn opcional. Si es null usa el offset del perfil")]
    [SerializeField] private Transform _spawnOrigin;

    private Damageable     _damageable;
    private OrbDropProfile _profile;

    void Awake() => _damageable = GetComponent<Damageable>();

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

    private OrbDropProfile Profile
    {
        get
        {
            if (_profile != null) return _profile;
            ResolveProfile();
            return _profile;
        }
    }

    private void ResolveProfile()
    {
        var config = OrbDropService.Instance?.Config;
        if (config == null) return;

        string id = string.IsNullOrEmpty(_profileId)
            ? config.GetProfileIdForTag(gameObject.tag)
            : _profileId;

        _profile = config.GetProfile(id);
    }

    private void OnDamagedBy(float _, GameObject instigator)
    {
        var p = Profile;
        if (p == null) return;

        float roll = Random.value;
        if (roll < p.healthChance)
            SpawnBurst(p.healthOrbPrefab, Random.Range(p.minOrbsPerHit, p.maxOrbsPerHit + 1));
        else if (roll < p.healthChance + p.manaChance)
            SpawnBurst(p.manaOrbPrefab, Random.Range(p.minOrbsPerHit, p.maxOrbsPerHit + 1));

        // Orbes especiales desactivados temporalmente — solo vida y magia por ahora
        // DuoCompanion? companion = GetCompanionFromInstigator(instigator);
        // if (companion.HasValue && Random.value < specialChance)
        // {
        //     var prefab = companion.Value == DuoCompanion.Estela ? estelaOrbPrefab : liamOrbPrefab;
        //     SpawnOrb(prefab);
        // }
    }

    private void OnDied()
    {
        var p = Profile;
        if (p == null) return;

        int count = Random.Range(p.deathBurstMin, p.deathBurstMax + 1);
        for (int i = 0; i < count; i++)
        {
            float roll = Random.value;
            if (roll < p.deathHealthWeight)
                SpawnOrb(p.healthOrbPrefab);
            else if (roll < p.deathHealthWeight + p.deathManaWeight)
                SpawnOrb(p.manaOrbPrefab);
            // else: slot de orbe especial, desactivado temporalmente
        }
    }

    private void SpawnBurst(GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++) SpawnOrb(prefab);
    }

    private void SpawnOrb(GameObject prefab)
    {
        if (prefab == null) return;
        var p = Profile;
        Vector3 origin = _spawnOrigin != null
            ? _spawnOrigin.position
            : transform.position + p.spawnCenterOffset;

        Vector2 circle = Random.insideUnitCircle * p.spawnRadius;
        float   h      = Random.Range(p.spawnHeightMin, p.spawnHeightMax);
        var go = Instantiate(prefab, origin + new Vector3(circle.x, h, circle.y), Quaternion.identity);
        if (go.TryGetComponent<BattleOrb>(out var orb))
            orb.SetAudioKeys(p.orbSpawnSFXKey, p.orbAttractSFXKey);
    }

    // private DuoCompanion? GetCompanionFromInstigator(GameObject instigator) { ... }
}
