using UnityEngine;
using System.Collections.Generic;

namespace Game.NPC
{
    /// <summary>
    /// Instancia prefabs de barras de estadísticas (vida, maná, etc.) cuando el NPC entra en combate.
    /// </summary>
    public class NPCStatBarsSpawner : MonoBehaviour
    {
        [Header("Prefabs de Barras de Estadísticas")]
        [SerializeField] private List<GameObject> statBarPrefabs = new List<GameObject>();
        
        [Header("Configuración")]
        [SerializeField] private bool spawnOnStart;
        [SerializeField] private bool spawnOnCombat = true;
        
        private List<GameObject> _statBarInstances = new List<GameObject>();
        private Damageable _damageable;
        private bool _isSubscribed;
        
        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
        }
        
        public void AddStatBarPrefab(GameObject prefab)
        {
            if (prefab != null && !statBarPrefabs.Contains(prefab))
            {
                statBarPrefabs.Add(prefab);
            }
        }
        
        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnBars();
            }
            
            SubscribeToDamageable();
        }
        
        private void OnEnable()
        {
            SubscribeToDamageable();
        }
        
        private void OnDisable()
        {
            UnsubscribeFromDamageable();
        }
        
        private void SubscribeToDamageable()
        {
            if (_damageable != null && spawnOnCombat && !_isSubscribed)
            {
                _damageable.OnDamaged += OnFirstDamage;
                _isSubscribed = true;
            }
        }
        
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
            DestroyBars();
        }
        
        private void OnFirstDamage(float amount)
        {
            if (_statBarInstances.Count == 0)
            {
                SpawnBars();
            }
            UnsubscribeFromDamageable();
        }
        
        public void SpawnBars()
        {
            if (statBarPrefabs.Count == 0) return;
            
            if (_statBarInstances.Count > 0) return;
            
            foreach (var prefab in statBarPrefabs)
            {
                if (prefab != null)
                {
                    GameObject instance = Instantiate(prefab, transform);
                    _statBarInstances.Add(instance);
                }
            }
        }
        
        public void DestroyBars()
        {
            foreach (var instance in _statBarInstances)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }
            _statBarInstances.Clear();
        }
    }
}
