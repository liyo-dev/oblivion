using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.NPC
{
    /// <summary>
    /// ⚠️ OBSOLETO - Este componente será reemplazado por NPCHealthBarSpawner + NPCHealthBarUI
    /// 
    /// Sistema viejo (complejo):
    ///   - Instancia el prefab
    ///   - Busca componentes
    ///   - Se suscribe a eventos
    ///   - Actualiza la UI
    /// 
    /// Sistema nuevo (simple):
    ///   - NPCHealthBarSpawner (en el NPC): Solo instancia el prefab
    ///   - NPCHealthBarUI (en el prefab): Se auto-configura y actualiza
    /// 
    /// Ver: GUIA_NUEVO_SISTEMA_BARRAS_VIDA.md
    /// 
    /// MIGRACIÓN:
    ///   1. Remover este componente de los NPCs
    ///   2. Añadir NPCHealthBarSpawner
    ///   3. Asignar el prefab
    ///   4. Listo
    /// </summary>
    [System.Obsolete("Usar NPCHealthBarSpawner + NPCHealthBarUI en su lugar")]
    public class NPCHealthBarController : MonoBehaviour
{
        [Header("Prefab de Barra de Vida")]
        [SerializeField] private GameObject healthBarPrefab;
        
        [Header("Configuración")]
        [SerializeField] private Vector3 healthBarOffset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private bool showHealthBar = true;
        
        [Header("Colores")]
        [SerializeField] private Color healthyColor = new Color(0f, 1f, 0f);
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float warningThreshold = 0.5f;
        [SerializeField] private float criticalThreshold = 0.25f;
        
        private Damageable _damageable;
        private GameObject _healthBarInstance;
        private Image _fillImage;
        private TextMeshProUGUI _healthText;
        private Canvas _canvas;
        private bool _isInitialized;
        
        private void Awake()
        {
            _damageable = GetComponent<Damageable>();
            
            if (_damageable == null)
            {
                Debug.LogError($"[NPCHealthBarController] No se encontró Damageable en {name}");
                enabled = false;
                return;
            }
        }
        
        private void Start()
        {
            if (showHealthBar && healthBarPrefab != null)
            {
                InstantiateHealthBar();
            }
            
            // Suscribirse a eventos de daño
            if (_damageable != null)
            {
                _damageable.OnDamaged += OnDamaged;
                _damageable.OnDied += OnDied;
            }
        }
        
        private void OnDestroy()
        {
            if (_damageable != null)
            {
                _damageable.OnDamaged -= OnDamaged;
                _damageable.OnDied -= OnDied;
            }
            
            // Destruir la instancia de la barra de vida
            if (_healthBarInstance != null)
            {
                Destroy(_healthBarInstance);
            }
        }
        
        /// <summary>
        /// Instancia el prefab de la barra de vida sobre el NPC
        /// </summary>
        private void InstantiateHealthBar()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogWarning($"[NPCHealthBarController] No hay prefab asignado en {name}");
                return;
            }
            
            // Instanciar el prefab
            _healthBarInstance = Instantiate(healthBarPrefab);
            
            // Posicionar sobre el NPC con offset
            _healthBarInstance.transform.SetParent(transform);
            _healthBarInstance.transform.localPosition = healthBarOffset;
            _healthBarInstance.transform.localRotation = Quaternion.identity;
            
            // Obtener referencias a componentes del prefab
            _canvas = _healthBarInstance.GetComponentInChildren<Canvas>();
            _fillImage = _healthBarInstance.GetComponentInChildren<Image>();
            _healthText = _healthBarInstance.GetComponentInChildren<TextMeshProUGUI>();
            
            // Asegurar que tiene BillboardUI para que mire siempre a la cámara
            var billboard = _healthBarInstance.GetComponent<BillboardUI>();
            if (billboard == null)
            {
                _healthBarInstance.AddComponent<BillboardUI>();
            }
            
            _isInitialized = true;
            UpdateHealthBar();
            
            // Ocultar inicialmente (se mostrará al recibir daño)
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }
            
            Debug.Log($"[NPCHealthBarController] Barra de vida instanciada para {name}");
        }
        
        /// <summary>
        /// Actualiza la barra de vida con el estado actual
        /// </summary>
        private void UpdateHealthBar()
        {
            if (!_isInitialized || _damageable == null)
                return;
            
            float healthPercentage = _damageable.Current / _damageable.Max;
            
            // Actualizar fill
            if (_fillImage != null)
            {
                _fillImage.fillAmount = Mathf.Clamp01(healthPercentage);
                
                // Cambiar color según salud
                if (healthPercentage <= criticalThreshold)
                    _fillImage.color = criticalColor;
                else if (healthPercentage <= warningThreshold)
                    _fillImage.color = warningColor;
                else
                    _fillImage.color = healthyColor;
            }
            
            // Actualizar texto
            if (_healthText != null)
            {
                _healthText.text = $"{Mathf.Ceil(_damageable.Current)} / {_damageable.Max}";
            }
        }
        
        /// <summary>
        /// Llamado cuando el NPC recibe daño
        /// </summary>
        private void OnDamaged(float amount)
        {
            UpdateHealthBar();
            
            // Mostrar la barra de vida al recibir daño
            if (_canvas != null && !_canvas.enabled)
            {
                _canvas.enabled = true;
            }
        }
        
        /// <summary>
        /// Llamado cuando el NPC muere
        /// </summary>
        private void OnDied()
        {
            UpdateHealthBar();
            
            // Opcionalmente ocultar la barra después de un delay
            if (_healthBarInstance != null)
            {
                StartCoroutine(HideHealthBarAfterDelay(2f));
            }
        }
        
        private System.Collections.IEnumerator HideHealthBarAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }
        }
        
        /// <summary>
        /// Muestra la barra de vida manualmente
        /// </summary>
        public void ShowHealthBar()
        {
            if (_canvas != null)
            {
                _canvas.enabled = true;
            }
        }
        
        /// <summary>
        /// Oculta la barra de vida manualmente
        /// </summary>
        public void HideHealthBar()
        {
            if (_canvas != null)
            {
                _canvas.enabled = false;
            }
        }
    }
}

