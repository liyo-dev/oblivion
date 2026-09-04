using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Script super simple que instancia una barra de vida cuando el NPC entra en combate.
    /// Solo necesita el prefab asignado.
    /// </summary>
    [DisallowMultipleComponent]
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
                // Debug.Log($"[NPCHealthBarSpawner] Suscrito a OnDamaged de {name}");
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
        /// Instancia el prefab de la barra de vida.
        /// 
        /// INC-125 (30 ago 2026): Raúl reportó que, al matar NPCs, la barra de vida aparece
        /// duplicada (dos cajas superpuestas) justo durante el zoom/slowmo de
        /// DeathCameraEffect — momento en el que, al estar la cámara muy cerca del NPC, se hace
        /// evidente. No se pudo aislar un único punto del código que instancie dos veces de forma
        /// determinista (tanto NPCBehaviourManagerV2.EnsureRequiredComponents() como
        /// CombatState.OnEnter() reutilizan el MISMO componente vía GetComponent antes de
        /// añadir uno nuevo, y _healthBarInstance ya bloqueaba una doble instanciación DESDE ESTE
        /// componente). Como red de seguridad — y para dejar rastro en consola si vuelve a pasar —
        /// se limpia aquí cualquier instancia de barra de vida "huérfana" (no rastreada en
        /// _healthBarInstance, por ejemplo si otro camino la creó o si una referencia se perdió)
        /// antes de crear la nueva, y se añade [DisallowMultipleComponent] a la clase para que no
        /// puedan coexistir dos NPCHealthBarSpawner en el mismo NPC.
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
            
            // Red de seguridad INC-125: si por lo que sea ya cuelga una barra de vida de este NPC
            // que este spawner no está rastreando (huérfana), se destruye antes de crear la nueva
            // para no dejar dos barras superpuestas. Se deja el log en Warning (no solo en editor)
            // porque si esto se dispara alguna vez, es exactamente la pista que hace falta para
            // encontrar la causa raíz real.
            var existingBars = GetComponentsInChildren<Game.UI.NPCHealthBarUI>(true);
            if (existingBars != null && existingBars.Length > 0)
            {
                Debug.LogWarning($"[NPCHealthBarSpawner] ⚠️ INC-125: {existingBars.Length} barra(s) de vida huérfana(s) encontradas en {name} antes de instanciar — destruyéndolas para evitar duplicado.");
                foreach (var stray in existingBars)
                {
                    if (stray != null) Destroy(stray.gameObject);
                }
            }
            
            // Instanciar como hijo del NPC
            _healthBarInstance = Instantiate(healthBarPrefab, transform);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NPCHealthBarSpawner] Barra de vida instanciada para {name}");
#endif
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
