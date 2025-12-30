using System.Collections;
using UnityEngine;

namespace Game.NPC.Common
{
    /// <summary>
    /// Controla los iconos visuales sobre la cabeza del NPC.
    /// Los prefabs se configuran en NPCCombatConfig y se pasan como parámetros.
    /// Solo usa prefabs reutilizables - La forma más limpia y profesional.
    /// </summary>
    public class NPCAlertIconController : MonoBehaviour
    {
        [Header("Configuración por Defecto")]
        [Tooltip("Offset vertical sobre el NPC donde aparece el icono")]
        [SerializeField] private Vector3 iconOffset = new Vector3(0f, 2.5f, 0f);
        
        [Tooltip("Duración del icono en segundos (si no se especifica)")]
        [SerializeField] private float iconDuration = 2f;
        
        [Tooltip("Si está activo, el icono hace bounce animado")]
        [SerializeField] private bool animateBounce = true;
        
        [Tooltip("Amplitud del bounce")]
        [SerializeField] private float bounceAmplitude = 0.2f;
        
        [Tooltip("Velocidad del bounce")]
        [SerializeField] private float bounceSpeed = 3f;
        
        private GameObject _currentIconInstance;
        private Coroutine _iconRoutine;
        private Camera _mainCamera;
        
        private void Start()
        {
            _mainCamera = Camera.main;
        }
        
        /// <summary>
        /// Muestra un icono de alerta usando un prefab de GameObject
        /// </summary>
        public void ShowAlertIcon(GameObject iconPrefab, float duration = -1f)
        {
            if (iconPrefab == null)
            {
                Debug.LogWarning($"[NPCAlertIconController:{name}] IconPrefab es null");
                return;
            }
            
            HideAlertIcon(); // Limpiar icono anterior si existe
            
            float useDuration = duration > 0f ? duration : iconDuration;
            _iconRoutine = StartCoroutine(ShowIconRoutine(iconPrefab, useDuration));
        }
        
        /// <summary>
        /// Muestra el icono de alerta (❗) - Detectó al jugador
        /// </summary>
        public void ShowAlert(GameObject alertPrefab, float duration = -1f)
        {
            if (alertPrefab != null)
            {
                Debug.Log($"[NPCAlertIcon:{name}] ❗ Mostrando icono de alerta");
                ShowAlertIcon(alertPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ alertPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Muestra el icono de interrogación (❓) - Buscando al jugador
        /// </summary>
        public void ShowQuestion(GameObject questionPrefab, float duration = -1f)
        {
            if (questionPrefab != null)
            {
                Debug.Log($"[NPCAlertIcon:{name}] ❓ Mostrando icono de interrogación (buscando)");
                ShowAlertIcon(questionPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ questionPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Muestra el icono de admiración (❗) - ¡Encontró al jugador!
        /// </summary>
        public void ShowExclamation(GameObject exclamationPrefab, float duration = -1f)
        {
            if (exclamationPrefab != null)
            {
                Debug.Log($"[NPCAlertIcon:{name}] ❗ Mostrando icono de admiración (¡encontrado!)");
                ShowAlertIcon(exclamationPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ exclamationPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Muestra un icono persistente (sin duración) - se mantiene hasta llamar HideAlertIcon()
        /// Útil para indicar que hay una narrativa/quest disponible
        /// </summary>
        public void ShowPersistentIcon(GameObject iconPrefab)
        {
            if (iconPrefab == null)
            {
                Debug.LogWarning($"[NPCAlertIconController:{name}] ⚠️ persistentIconPrefab es null");
                return;
            }
            
            // No mostrar si ya hay un icono activo del mismo prefab
            if (_currentIconInstance != null && _currentIconInstance.name.Contains(iconPrefab.name))
            {
                return;
            }
            
            HideAlertIcon(); // Limpiar icono anterior si existe
            
            _iconRoutine = StartCoroutine(ShowPersistentIconRoutine(iconPrefab));
            Debug.Log($"[NPCAlertIcon:{name}] 📍 Mostrando icono persistente");
        }
        
        /// <summary>
        /// Verifica si hay un icono persistente activo
        /// </summary>
        public bool HasPersistentIcon => _currentIconInstance != null;
        
        /// <summary>
        /// Oculta el icono de alerta actual
        /// </summary>
        public void HideAlertIcon()
        {
            if (_iconRoutine != null)
            {
                StopCoroutine(_iconRoutine);
                _iconRoutine = null;
            }
            
            if (_currentIconInstance != null)
            {
                Destroy(_currentIconInstance);
                _currentIconInstance = null;
            }
        }
        
        private IEnumerator ShowIconRoutine(GameObject iconPrefab, float duration)
        {
            // Instanciar el prefab
            _currentIconInstance = Instantiate(iconPrefab, transform);
            _currentIconInstance.transform.localPosition = iconOffset;
            _currentIconInstance.transform.localRotation = Quaternion.identity;
            _currentIconInstance.SetActive(true);
            
            // Animar durante la duración
            float elapsed = 0f;
            Vector3 basePosition = _currentIconInstance.transform.localPosition;
            
            while (elapsed < duration)
            {
                if (_currentIconInstance == null) yield break;
                
                // Aplicar bounce si está activado
                if (animateBounce)
                {
                    float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
                    _currentIconInstance.transform.localPosition = basePosition + new Vector3(0f, bounce, 0f);
                }
                
                // Billboard hacia la cámara
                if (_mainCamera != null)
                {
                    _currentIconInstance.transform.rotation = _mainCamera.transform.rotation;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Limpiar al terminar
            HideAlertIcon();
        }
        
        /// <summary>
        /// Rutina para icono persistente (sin duración, se mantiene hasta HideAlertIcon)
        /// </summary>
        private IEnumerator ShowPersistentIconRoutine(GameObject iconPrefab)
        {
            // Instanciar el prefab
            _currentIconInstance = Instantiate(iconPrefab, transform);
            _currentIconInstance.name = iconPrefab.name + "_Persistent";
            _currentIconInstance.transform.localPosition = iconOffset;
            _currentIconInstance.transform.localRotation = Quaternion.identity;
            _currentIconInstance.SetActive(true);
            
            Vector3 basePosition = _currentIconInstance.transform.localPosition;
            
            // Animar indefinidamente hasta que se oculte
            while (_currentIconInstance != null)
            {
                // Aplicar bounce si está activado
                if (animateBounce)
                {
                    float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
                    _currentIconInstance.transform.localPosition = basePosition + new Vector3(0f, bounce, 0f);
                }
                
                // Billboard hacia la cámara
                if (_mainCamera != null && _currentIconInstance != null)
                {
                    _currentIconInstance.transform.rotation = _mainCamera.transform.rotation;
                }
                
                yield return null;
            }
        }
        
        private void OnDestroy()
        {
            HideAlertIcon();
        }
    }
}

