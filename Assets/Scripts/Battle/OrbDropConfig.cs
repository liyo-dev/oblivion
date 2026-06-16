using UnityEngine;

/// <summary>
/// Configuracion centralizada de drops de orbes. Un SO en la escena Start.
/// Los enemigos de la layer Enemy reciben OrbDropper automaticamente en runtime.
/// Para asignar un perfil distinto al default, anadir un componente OrbDropper
/// al prefab del enemigo y rellenar solo el campo profileId.
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Batalla/OrbDropConfig", fileName = "OrbDropConfig")]
public class OrbDropConfig : ScriptableObject
{
    [SerializeField] private OrbDropProfile   _defaultProfile  = new();
    [SerializeField] private OrbDropProfile[] _profiles        = {};
    [SerializeField] private TagProfileMapping[] _tagMappings  = {};

    /// <summary>Devuelve el perfil con ese id, o el default si no existe.</summary>
    public OrbDropProfile GetProfile(string id)
    {
        if (!string.IsNullOrEmpty(id) && _profiles != null)
        {
            foreach (var p in _profiles)
                if (p.id == id) return p;
        }
        return _defaultProfile;
    }

    /// <summary>Devuelve el profileId mapeado a ese tag de Unity, o null si no hay mapeo.</summary>
    public string GetProfileIdForTag(string tag)
    {
        if (_tagMappings != null)
            foreach (var m in _tagMappings)
                if (m.tag == tag) return m.profileId;
        return null;
    }
}

[System.Serializable]
public class OrbDropProfile
{
    [Tooltip("Identificador del perfil. Ej: default, elite, boss")]
    public string id = "default";

    [Header("Prefabs")]
    public GameObject healthOrbPrefab;
    public GameObject manaOrbPrefab;

    [Header("Por golpe recibido")]
    [Range(0f, 1f)] public float healthChance    = 0.35f;
    [Range(0f, 1f)] public float manaChance      = 0.20f;
    [Tooltip("Orbes soltados cuando sale la tirada de vida o mana")]
    public int minOrbsPerHit = 1;
    public int maxOrbsPerHit = 3;

    [Header("Burst de muerte")]
    public int   deathBurstMin = 4;
    public int   deathBurstMax = 8;
    [Tooltip("Peso de vida en el burst (0-1). vida+mana <= 1. Lo restante son slots vacios.")]
    [Range(0f, 1f)] public float deathHealthWeight = 0.5f;
    [Range(0f, 1f)] public float deathManaWeight   = 0.3f;

    [Header("Sonidos")]
    [Tooltip("Suena cuando el orbe aparece y sale disparado del enemigo")]
    public string orbSpawnSFXKey;
    [Tooltip("Suena cuando el orbe empieza a volar hacia el jugador")]
    public string orbAttractSFXKey;

    [Header("Punto de spawn")]
    [Tooltip("Offset desde transform.position del enemigo")]
    public Vector3 spawnCenterOffset = new Vector3(0f, 1.5f, 0f);
    public float   spawnRadius       = 0.5f;
    public float   spawnHeightMin    = 0.3f;
    public float   spawnHeightMax    = 0.8f;
}

[System.Serializable]
public class TagProfileMapping
{
    [Tooltip("Tag de Unity del enemigo")]
    public string tag;
    [Tooltip("Id del perfil a usar para ese tag")]
    public string profileId;
}
