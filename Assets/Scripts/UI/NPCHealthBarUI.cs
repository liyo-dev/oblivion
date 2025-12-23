﻿using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Game.NPC;

namespace Game.UI
{
    /// <summary>
    /// Script simple que va en el prefab de la barra de vida.
    /// Se auto-configura y actualiza automáticamente cuando el NPC recibe daño.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class NPCHealthBarUI : MonoBehaviour
    {
        [Header("Referencias Automáticas")]
        [Tooltip("Se busca automáticamente si no se asigna")]
        [SerializeField] private Image fillImage;
        
        [Tooltip("Opcional - se busca automáticamente si no se asigna")]
        [SerializeField] private TextMeshProUGUI healthText;
        
        [Header("Configuración")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private float hideDelay = 3f;
        
        [Header("Animación")]
        [SerializeField] private float lerpSpeed = 8f;
        [Tooltip("Si está activado, la barra hace lerp suave al recibir daño")]
        [SerializeField] private bool smoothTransition = true;
        
        [Header("Colores")]
        [SerializeField] private Color healthyColor = new Color(0f, 1f, 0f);
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private float warningThreshold = 0.5f;
        [SerializeField] private float criticalThreshold = 0.25f;
        
        private Canvas _canvas;
        private Damageable _npcDamageable;
        private Transform _npcTransform;
        private float _hideTimer;
        private float _currentFillAmount;
        private float _targetFillAmount;
        
        private void Awake()
        {
            // Obtener canvas
            _canvas = GetComponent<Canvas>();
            
            // Auto-buscar fillImage si no está asignado
            if (fillImage == null)
            {
                fillImage = GetComponentInChildren<Image>();
            }
            
            // Auto-buscar healthText si no está asignado (opcional)
            if (healthText == null)
            {
                healthText = GetComponentInChildren<TextMeshProUGUI>();
            }
            
            // Asegurar que tiene BillboardUI para mirar a la cámara
            if (GetComponent<BillboardUI>() == null)
            {
                gameObject.AddComponent<BillboardUI>();
            }
            
            // Ocultar al inicio
            if (_canvas != null)
                _canvas.enabled = false;
        }
        
        private void Start()
        {
            // Buscar el NPCBehaviourManagerV2 en el padre
            _npcTransform = transform.parent;
            if (_npcTransform != null)
            {
                // Intentar obtener Damageable desde el NPCBehaviourManagerV2
                var behaviourManager = _npcTransform.GetComponent<NPCBehaviourManagerV2>();
                if (behaviourManager != null)
                {
                    _npcDamageable = behaviourManager.GetComponent<Damageable>();
                    
                    if (_npcDamageable != null)
                    {
                        // Suscribirse a eventos
                        _npcDamageable.OnDamaged += OnNPCDamaged;
                        _npcDamageable.OnDied += OnNPCDied;
                        Debug.Log($"[NPCHealthBarUI] ✅ Conectado a Damageable de {_npcTransform.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[NPCHealthBarUI] No se encontró Damageable en {_npcTransform.name}. Asegúrate de que el NPC está en combate.");
                    }
                }
                else
                {
                    // Fallback: buscar Damageable directamente en el padre
                    _npcDamageable = _npcTransform.GetComponent<Damageable>();
                    if (_npcDamageable != null)
                    {
                        _npcDamageable.OnDamaged += OnNPCDamaged;
                        _npcDamageable.OnDied += OnNPCDied;
                        Debug.Log($"[NPCHealthBarUI] ✅ Conectado a Damageable (fallback) de {_npcTransform.name}");
                    }
                    else
                    {
                        Debug.LogWarning($"[NPCHealthBarUI] No se encontró NPCBehaviourManagerV2 ni Damageable en {_npcTransform.name}");
                    }
                }
            }
            
            // Aplicar offset local
            transform.localPosition = offset;
            
            // Inicializar fill amounts
            _currentFillAmount = 1f;
            _targetFillAmount = 1f;
            if (fillImage != null)
            {
                fillImage.fillAmount = 1f;
            }
        }
        
        private void OnDestroy()
        {
            // Desuscribirse de eventos
            if (_npcDamageable != null)
            {
                _npcDamageable.OnDamaged -= OnNPCDamaged;
                _npcDamageable.OnDied -= OnNPCDied;
            }
        }
        
        private void Update()
        {
            // Lerp suave de la barra de vida
            if (smoothTransition && fillImage != null && Mathf.Abs(_currentFillAmount - _targetFillAmount) > 0.001f)
            {
                _currentFillAmount = Mathf.Lerp(_currentFillAmount, _targetFillAmount, Time.deltaTime * lerpSpeed);
                fillImage.fillAmount = _currentFillAmount;
            }
            
            // Timer para ocultar automáticamente
            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_hideTimer <= 0f && hideWhenFull)
                {
                    float healthPercent = _npcDamageable != null ? (_npcDamageable.Current / _npcDamageable.Max) : 0f;
                    if (healthPercent >= 0.99f)
                    {
                        Hide();
                    }
                }
            }
        }
        
        /// <summary>
        /// Llamado cuando el NPC recibe daño
        /// </summary>
        private void OnNPCDamaged(float amount)
        {
            Show();
            UpdateBar();
            ResetHideTimer();
        }
        
        /// <summary>
        /// Llamado cuando el NPC muere
        /// </summary>
        private void OnNPCDied()
        {
            UpdateBar();
            // Ocultar después de un delay
            Invoke(nameof(Hide), 2f);
        }
        
        /// <summary>
        /// Actualiza la barra con el estado actual de salud
        /// </summary>
        private void UpdateBar()
        {
            if (_npcDamageable == null || fillImage == null)
                return;
            
            float healthPercent = _npcDamageable.Current / _npcDamageable.Max;
            
            // Actualizar fill amount (con o sin animación)
            _targetFillAmount = Mathf.Clamp01(healthPercent);
            
            if (!smoothTransition)
            {
                // Actualización instantánea
                _currentFillAmount = _targetFillAmount;
                fillImage.fillAmount = _currentFillAmount;
            }
            // Si smoothTransition está activo, el Update() hará el lerp automáticamente
            
            // Cambiar color según salud
            if (healthPercent <= criticalThreshold)
                fillImage.color = criticalColor;
            else if (healthPercent <= warningThreshold)
                fillImage.color = warningColor;
            else
                fillImage.color = healthyColor;
            
            // Actualizar texto si existe
            if (healthText != null)
            {
                healthText.text = $"{Mathf.Ceil(_npcDamageable.Current)} / {_npcDamageable.Max}";
            }
        }
        
        /// <summary>
        /// Muestra la barra de vida
        /// </summary>
        public void Show()
        {
            if (_canvas != null && !_canvas.enabled)
            {
                _canvas.enabled = true;
            }
        }
        
        /// <summary>
        /// Oculta la barra de vida
        /// </summary>
        public void Hide()
        {
            if (_canvas != null && _canvas.enabled)
            {
                _canvas.enabled = false;
            }
        }
        
        /// <summary>
        /// Resetea el timer para ocultar
        /// </summary>
        private void ResetHideTimer()
        {
            _hideTimer = hideDelay;
        }
        
        #if UNITY_EDITOR
        // Preview en el editor
        private void OnValidate()
        {
            if (!Application.isPlaying && fillImage != null)
            {
                fillImage.fillAmount = 0.75f; // Vista previa al 75%
            }
        }
        #endif
    }
}

