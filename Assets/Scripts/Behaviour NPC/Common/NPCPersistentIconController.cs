using UnityEngine;

namespace Game.NPC.Common
{
    /// <summary>
    /// Controla un icono persistente sobre la cabeza del NPC.
    /// Usado para mostrar que el NPC tiene algo que ofrecer (quest, diálogo importante, etc.)
    /// A diferencia del AlertIcon (temporal), este es permanente hasta que se complete la condición.
    /// </summary>
    public class NPCPersistentIconController : MonoBehaviour
    {
        [Header("Configuración")]
        [Tooltip("Prefab del icono persistente (ej: signo de interrogación para quests)")]
        [SerializeField] private GameObject iconPrefab;
        
        [Tooltip("Offset vertical sobre el NPC donde aparece el icono")]
        [SerializeField] private Vector3 iconOffset = new Vector3(0f, 2.5f, 0f);
        
        [Tooltip("Escala del icono")]
        [SerializeField] private Vector3 iconScale = Vector3.one;
        
        [Tooltip("Si está activo, el icono hace bounce animado")]
        [SerializeField] private bool animateBounce = true;
        
        [Tooltip("Amplitud del bounce")]
        [SerializeField] private float bounceAmplitude = 0.15f;
        
        [Tooltip("Velocidad del bounce")]
        [SerializeField] private float bounceSpeed = 2f;
        
        [Tooltip("Si está activo, el icono siempre mira a la cámara (billboard)")]
        [SerializeField] private bool billboardToCamera = true;
        
        [Tooltip("Si está activo, el icono se muestra automáticamente al iniciar")]
        [SerializeField] private bool showOnStart = true;
        
        private GameObject _iconInstance;
        private Camera _mainCamera;
        private Vector3 _baseLocalPosition;
        private bool _isVisible;
        
        private void Start()
        {
            _mainCamera = Camera.main;
            
            if (showOnStart && iconPrefab != null)
            {
                ShowIcon();
            }
        }
        
        private void Update()
        {
            if (!_isVisible || _iconInstance == null)
                return;
            
            // Aplicar bounce si está activado
            if (animateBounce)
            {
                float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
                _iconInstance.transform.localPosition = _baseLocalPosition + new Vector3(0f, bounce, 0f);
            }
            
            // Billboard hacia la cámara
            if (billboardToCamera && _mainCamera != null)
            {
                _iconInstance.transform.rotation = _mainCamera.transform.rotation;
            }
        }
        
        /// <summary>
        /// Muestra el icono persistente
        /// </summary>
        public void ShowIcon()
        {
            if (_iconInstance != null || iconPrefab == null)
                return;
            
            // Instanciar el prefab como hijo
            _iconInstance = Instantiate(iconPrefab, transform);
            _iconInstance.transform.localPosition = iconOffset;
            _iconInstance.transform.localScale = iconScale;
            _iconInstance.transform.localRotation = Quaternion.identity;
            
            _baseLocalPosition = iconOffset;
            _isVisible = true;
        }
        
        /// <summary>
        /// Oculta el icono persistente
        /// </summary>
        public void HideIcon()
        {
            if (_iconInstance != null)
            {
                Destroy(_iconInstance);
                _iconInstance = null;
            }
            
            _isVisible = false;
        }
        
        /// <summary>
        /// Cambia el prefab del icono (útil para cambiar de estado, ej: ! a ?)
        /// </summary>
        public void SetIconPrefab(GameObject newPrefab)
        {
            bool wasVisible = _isVisible;
            
            HideIcon();
            iconPrefab = newPrefab;
            
            if (wasVisible && iconPrefab != null)
            {
                ShowIcon();
            }
        }
        
        /// <summary>
        /// Establece la visibilidad del icono
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (visible && !_isVisible)
            {
                ShowIcon();
            }
            else if (!visible && _isVisible)
            {
                HideIcon();
            }
        }
        
        /// <summary>
        /// Verifica si el icono está visible
        /// </summary>
        public bool IsVisible => _isVisible;
        
        private void OnDestroy()
        {
            HideIcon();
        }
        
        private void OnDisable()
        {
            // Ocultar temporalmente cuando el NPC se desactiva
            if (_iconInstance != null)
            {
                _iconInstance.SetActive(false);
            }
        }
        
        private void OnEnable()
        {
            // Mostrar de nuevo cuando se reactiva
            if (_iconInstance != null)
            {
                _iconInstance.SetActive(true);
            }
        }
    }
}

