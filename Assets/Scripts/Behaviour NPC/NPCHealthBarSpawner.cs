using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Script super simple que instancia una barra de vida cuando el NPC entra en combate.
    /// Solo necesita el prefab asignado.
    /// </summary>
    public class NPCHealthBarSpawner : MonoBehaviour
    {
        [Header("Prefab de Barra de Vida")]
        [Tooltip("Arrastra el prefab: Assets/_NPCs/OverHead/Canvas HealthBar.prefab")]
        [SerializeField] private GameObject healthBarPrefab;
        
        [Header("Configuración")]
        [SerializeField] private bool spawnOnStart;
        [SerializeField] private bool spawnOnCombat = true;
        
        private GameObject _healthBarInstance;
        private Damageable _damageable;
        private bool _isSubscribed;
        
        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
        }
        
        /// <summary>
        /// Asigna el prefab de barra de vida (llamado desde CombatState)
        /// </summary>
        public void SetHealthBarPrefab(GameObject prefab)
        {
            healthBarPrefab = prefab;
        }
        
        private void Start()
        {
            if (spawnOnStart && healthBarPrefab != null)
            {
                SpawnHealthBar();
            }
            
            // Suscribirse si tiene Damageable
            SubscribeToDamageable();
        }
        
        private void OnEnable()
        {
            // Suscribirse al habilitar
            SubscribeToDamageable();
        }
        
        private void OnDisable()
        {
            UnsubscribeFromDamageable();
        }
        
        /// <summary>
        /// Suscribe al Damageable si existe y no está suscrito
        /// </summary>
        private void SubscribeToDamageable()
        {
            if (_damageable != null && spawnOnCombat && !_isSubscribed)
            {
                _damageable.OnDamaged += OnFirstDamage;
                _isSubscribed = true;
                Debug.Log($"[NPCHealthBarSpawner] Suscrito a OnDamaged de {name}");
            }
        }
        
        /// <summary>
        /// Desuscribe del Damageable
        /// </summary>
        private void UnsubscribeFromDamageable()
        {
            if (_damageable != null && _isSubscribed)
            {
                _damageable.OnDamaged -= OnFirstDamage;
                _isSubscribed = false;
            }
        }
        
        private void OnDestroy()
        {
            // Limpiar la instancia
            if (_healthBarInstance != null)
            {
                Destroy(_healthBarInstance);
            }
        }
        
        /// <summary>
        /// Se llama la primera vez que el NPC recibe daño
        /// </summary>
        private void OnFirstDamage(float amount)
        {
            if (_healthBarInstance == null && healthBarPrefab != null)
            {
                SpawnHealthBar();
            }
            
            // Desuscribirse después de la primera vez
            UnsubscribeFromDamageable();
        }
        
        /// <summary>
        /// Instancia el prefab de la barra de vida
        /// </summary>
        public void SpawnHealthBar()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogWarning($"[NPCHealthBarSpawner] No hay prefab asignado en {name}");
                return;
            }
            
            if (_healthBarInstance != null)
            {
                return;
            }
            
            // Instanciar como hijo del NPC
            _healthBarInstance = Instantiate(healthBarPrefab, transform);
        }
        
        /// <summary>
        /// Destruye la barra de vida actual
        /// </summary>
        public void DestroyHealthBar()
        {
            if (_healthBarInstance != null)
            {
                Destroy(_healthBarInstance);
                _healthBarInstance = null;
            }
        }
    }
}

