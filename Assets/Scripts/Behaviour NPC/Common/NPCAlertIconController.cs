﻿﻿using System.Collections;
using UnityEngine;

namespace Game.NPC.Common
{
    /// <summary>
    /// Controla el icono de alerta visual sobre la cabeza del NPC.
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
        
        private void OnDestroy()
        {
            HideAlertIcon();
        }
    }
}

