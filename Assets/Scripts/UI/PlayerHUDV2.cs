using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;

namespace Sendero.UI
{
    /// <summary>
    /// Sistema de HUD del jugador V2 - Con referencias desde Inspector.
    /// Usa arte personalizado en lugar de crear UI dinámicamente.
    /// Version: 2025-12-24
    /// 
    /// NOTA: Este HUD trabaja con el sistema MagicCaster existente.
    /// Los slots son: Left, Right, Special (no Up).
    /// </summary>
    public class PlayerHUDV2 : MonoBehaviour
    {
        [Header("Referencias de Vida")]
        [Tooltip("Imagen fill para la barra de vida")]
        [SerializeField] private Image healthFillImage;
        
        [Tooltip("Texto opcional para mostrar HP numérico (ej: 100/100)")]
        [SerializeField] private TextMeshProUGUI healthText;
        
        [Header("Referencias de Magia")]
        [Tooltip("Imagen fill para la barra de maná")]
        [SerializeField] private Image manaFillImage;
        
        [Tooltip("Texto opcional para mostrar MP numérico (ej: 50/50)")]
        [SerializeField] private TextMeshProUGUI manaText;
        
        [Header("Slots de Magia")]
        [Tooltip("Imagen del slot de magia IZQUIERDO (Q / Left)")]
        [SerializeField] private Image leftMagicSlotImage;
        
        [Tooltip("Imagen del slot de magia DERECHO (E / Right)")]
        [SerializeField] private Image rightMagicSlotImage;
        
        [Tooltip("Imagen del slot de magia ESPECIAL (R / Special)")]
        [SerializeField] private Image specialMagicSlotImage;
        
        [Tooltip("Overlay de cooldown para slot izquierdo (opcional)")]
        [SerializeField] private Image leftCooldownOverlay;
        
        [Tooltip("Overlay de cooldown para slot derecho (opcional)")]
        [SerializeField] private Image rightCooldownOverlay;
        
        [Tooltip("Overlay de cooldown para slot especial (opcional)")]
        [SerializeField] private Image specialCooldownOverlay;
        
        [Tooltip("Texto de cooldown para slot izquierdo (opcional)")]
        [SerializeField] private TextMeshProUGUI leftCooldownText;
        
        [Tooltip("Texto de cooldown para slot derecho (opcional)")]
        [SerializeField] private TextMeshProUGUI rightCooldownText;
        
        [Tooltip("Texto de cooldown para slot especial (opcional)")]
        [SerializeField] private TextMeshProUGUI specialCooldownText;
        
        [Header("Configuración Visual")]
        [SerializeField] private Color availableColor = Color.white;
        [SerializeField] private Color cooldownColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        [SerializeField] private Color noManaColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        
        [Header("Sprites por Defecto")]
        [Tooltip("Sprite cuando el slot está vacío")]
        [SerializeField] private Sprite emptySlotSprite;
        
        // Referencias a sistemas del juego
        private PlayerHealthSystem _healthSystem;
        private ManaPool _manaPool;
        private MagicCaster _magicCaster;
        
        // Estado actual de los slots
        private Dictionary<MagicSlot, SlotState> _slotStates = new Dictionary<MagicSlot, SlotState>();
        
        private class SlotState
        {
            public Image slotImage;
            public Image cooldownOverlay;
            public TextMeshProUGUI cooldownText;
            public Sprite equippedSprite;
            public bool hasSpell;
        }
        
        private void Awake()
        {
            // Inicializar diccionario de slots
            _slotStates[MagicSlot.Left] = new SlotState
            {
                slotImage = leftMagicSlotImage,
                cooldownOverlay = leftCooldownOverlay,
                cooldownText = leftCooldownText
            };
            
            _slotStates[MagicSlot.Right] = new SlotState
            {
                slotImage = rightMagicSlotImage,
                cooldownOverlay = rightCooldownOverlay,
                cooldownText = rightCooldownText
            };
            
            _slotStates[MagicSlot.Special] = new SlotState
            {
                slotImage = specialMagicSlotImage,
                cooldownOverlay = specialCooldownOverlay,
                cooldownText = specialCooldownText
            };
            
            // Validar referencias críticas
            ValidateReferences();
            
            // Inicializar overlays ocultos
            HideAllCooldownOverlays();
        }
        
        private void Start()
        {
            // Obtener el jugador
            var player = PlayerService.Player;
            if (player == null)
            {
                Debug.LogError("[PlayerHUDV2] ❌ No se pudo obtener el jugador desde PlayerService");
                return;
            }
            
            // Obtener componentes del jugador
            _healthSystem = player.GetComponent<PlayerHealthSystem>();
            _manaPool = player.GetComponent<ManaPool>();
            _magicCaster = player.GetComponent<MagicCaster>();
            
            // Validar componentes críticos
            if (_healthSystem == null)
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró PlayerHealthSystem en el jugador");
            if (_manaPool == null)
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró ManaPool en el jugador");
            if (_magicCaster == null)
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró MagicCaster en el jugador");
            
            // Suscribirse a eventos
            SubscribeToEvents();
            
            // Actualización inicial
            RefreshHealthBar();
            RefreshManaBar();
            RefreshAllMagicSlots();
        }
        
        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            
            // Limpiar todos los tweens
            if (healthFillImage != null) healthFillImage.DOKill();
            if (manaFillImage != null) manaFillImage.DOKill();
        }
        
        #region Validación y Setup
        
        private void ValidateReferences()
        {
            bool hasErrors = false;
            
            if (healthFillImage == null)
            {
                Debug.LogError("[PlayerHUDV2] ❌ healthFillImage no está asignado en el Inspector!");
                hasErrors = true;
            }
            
            if (manaFillImage == null)
            {
                Debug.LogError("[PlayerHUDV2] ❌ manaFillImage no está asignado en el Inspector!");
                hasErrors = true;
            }
            
            if (leftMagicSlotImage == null || rightMagicSlotImage == null || specialMagicSlotImage == null)
            {
                Debug.LogError("[PlayerHUDV2] ❌ Faltan referencias de slots de magia en el Inspector!");
                hasErrors = true;
            }
            
            if (hasErrors)
            {
                Debug.LogError("[PlayerHUDV2] ⚠️ El HUD no funcionará correctamente sin las referencias necesarias.");
            }
            else
            {
                Debug.Log("[PlayerHUDV2] ✅ Todas las referencias críticas están asignadas correctamente.");
            }
        }
        
        private void HideAllCooldownOverlays()
        {
            if (leftCooldownOverlay != null) leftCooldownOverlay.gameObject.SetActive(false);
            if (rightCooldownOverlay != null) rightCooldownOverlay.gameObject.SetActive(false);
            if (specialCooldownOverlay != null) specialCooldownOverlay.gameObject.SetActive(false);
            
            if (leftCooldownText != null) leftCooldownText.gameObject.SetActive(false);
            if (rightCooldownText != null) rightCooldownText.gameObject.SetActive(false);
            if (specialCooldownText != null) specialCooldownText.gameObject.SetActive(false);
        }
        
        #endregion
        
        #region Eventos
        
        private void SubscribeToEvents()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged.AddListener(OnHealthChanged);
            }
            
            if (_manaPool != null)
            {
                _manaPool.OnManaChanged.AddListener(OnManaChanged);
            }
            
            // MagicCaster no tiene eventos, se actualiza cada frame en Update()
        }
        
        private void UnsubscribeFromEvents()
        {
            if (_healthSystem != null)
            {
                _healthSystem.OnHealthChanged.RemoveListener(OnHealthChanged);
            }
            
            if (_manaPool != null)
            {
                _manaPool.OnManaChanged.RemoveListener(OnManaChanged);
            }
        }
        
        #endregion
        
        #region Actualización de Vida
        
        private float _lastHealthFillAmount = 1f;
        
        private void OnHealthChanged(float healthPercent)
        {
            RefreshHealthBar();
        }
        
        private void RefreshHealthBar()
        {
            if (healthFillImage == null) return;
            
            float currentHp = 0f;
            float maxHp = 1f;
            
            if (_healthSystem != null)
            {
                currentHp = _healthSystem.CurrentHealth;
                maxHp = _healthSystem.MaxHealth;
            }
            
            float targetFillAmount = maxHp > 0 ? currentHp / maxHp : 0f;
            float currentFillAmount = healthFillImage.fillAmount;
            
            // Detectar si es daño o curación
            bool isDamage = targetFillAmount < currentFillAmount;
            bool isHealing = targetFillAmount > currentFillAmount;
            
            // Cancelar tweens previos
            healthFillImage.DOKill();
            
            if (isDamage)
            {
                // DAÑO: Animación rápida e impactante con punch
                healthFillImage.DOFillAmount(targetFillAmount, 0.15f)
                    .SetEase(Ease.OutQuad);
                
                // Efecto de shake/punch en la escala
                healthFillImage.transform.DOPunchScale(Vector3.one * 0.1f, 0.3f, 5, 0.5f);
            }
            else if (isHealing)
            {
                // CURACIÓN: Animación suave y gradual
                healthFillImage.DOFillAmount(targetFillAmount, 0.4f)
                    .SetEase(Ease.OutCubic);
            }
            else
            {
                // Sin cambio, solo actualizar
                healthFillImage.fillAmount = targetFillAmount;
            }
            
            _lastHealthFillAmount = targetFillAmount;
            
            // Actualizar texto si existe
            if (healthText != null)
            {
                healthText.text = $"{Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}";
            }
        }
        
        #endregion
        
        #region Actualización de Maná
        
        private float _lastManaFillAmount = 1f;
        private float _manaRegenStartTime = -1f;
        private bool _isRegenerating = false;
        
        private void OnManaChanged(float manaPercent)
        {
            RefreshManaBar();
        }
        
        private void RefreshManaBar()
        {
            if (manaFillImage == null) return;
            
            float currentMana = 0f;
            float maxMana = 1f;
            
            if (_manaPool != null)
            {
                currentMana = _manaPool.Current;
                maxMana = _manaPool.Max;
            }
            
            float targetFillAmount = maxMana > 0 ? currentMana / maxMana : 0f;
            float currentFillAmount = manaFillImage.fillAmount;
            
            // Detectar si es gasto o regeneración
            bool isSpending = targetFillAmount < currentFillAmount;
            bool isRegenerating = targetFillAmount > currentFillAmount;
            
            if (isSpending)
            {
                // GASTO: Animación rápida y cancelar cualquier regeneración
                manaFillImage.DOKill();
                _isRegenerating = false;
                
                manaFillImage.DOFillAmount(targetFillAmount, 0.2f)
                    .SetEase(Ease.OutQuad);
            }
            else if (isRegenerating)
            {
                // REGENERACIÓN: Actualización suave continua sin tweens
                // Usar interpolación directa para movimiento fluido
                if (!_isRegenerating)
                {
                    // Primera vez regenerando - cancelar tweens previos
                    manaFillImage.DOKill();
                    _isRegenerating = true;
                    _manaRegenStartTime = Time.time;
                }
                
                // Actualización directa con Lerp suave para regeneración continua
                float lerpSpeed = 5f; // Velocidad de interpolación
                manaFillImage.fillAmount = Mathf.Lerp(currentFillAmount, targetFillAmount, lerpSpeed * Time.deltaTime);
            }
            else
            {
                // Sin cambio o ya completo
                if (_isRegenerating && Mathf.Approximately(currentFillAmount, targetFillAmount))
                {
                    _isRegenerating = false;
                }
                manaFillImage.fillAmount = targetFillAmount;
            }
            
            _lastManaFillAmount = targetFillAmount;
            
            // Actualizar texto si existe
            if (manaText != null)
            {
                manaText.text = $"{Mathf.CeilToInt(currentMana)}/{Mathf.CeilToInt(maxMana)}";
            }
        }
        
        #endregion
        
        #region Actualización de Slots de Magia
        
        private void Update()
        {
            // Actualizar cooldowns cada frame
            UpdateMagicSlotCooldowns();
        }
        
        private void RefreshAllMagicSlots()
        {
            RefreshMagicSlot(MagicSlot.Left);
            RefreshMagicSlot(MagicSlot.Right);
            RefreshMagicSlot(MagicSlot.Special);
        }
        
        private void RefreshMagicSlot(MagicSlot slotType)
        {
            if (!_slotStates.ContainsKey(slotType)) return;
            if (_magicCaster == null) return;
            
            var slotState = _slotStates[slotType];
            if (slotState.slotImage == null) return;
            
            // Obtener el spell equipado en este slot
            MagicSpellSO equippedSpell = _magicCaster.GetSpellForSlot(slotType);
            
            if (equippedSpell != null && equippedSpell.attackIcon != null)
            {
                // Hay un hechizo equipado - asignar su sprite
                slotState.hasSpell = true;
                slotState.equippedSprite = equippedSpell.attackIcon;
                slotState.slotImage.sprite = equippedSpell.attackIcon;
                slotState.slotImage.color = availableColor;
                slotState.slotImage.enabled = true;
                
                // NUEVO: Overlay siempre visible para slots con hechizo
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.gameObject.SetActive(true);
                    slotState.cooldownOverlay.fillAmount = 1f; // Empezar lleno (disponible)
                }
                
                Debug.Log($"[PlayerHUDV2] ✅ Slot {slotType} asignado con hechizo: {equippedSpell.name} (sprite: {equippedSpell.attackIcon.name})");
            }
            else
            {
                // Slot vacío
                slotState.hasSpell = false;
                slotState.equippedSprite = null;
                
                if (emptySlotSprite != null)
                {
                    slotState.slotImage.sprite = emptySlotSprite;
                    slotState.slotImage.color = new Color(1f, 1f, 1f, 0.3f); // Semi-transparente
                    slotState.slotImage.enabled = true;
                }
                else
                {
                    // Si no hay sprite vacío, ocultar la imagen
                    slotState.slotImage.enabled = false;
                }
                
                // IMPORTANTE: Ocultar overlay de cooldown para slots vacíos
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.gameObject.SetActive(false);
                }
                
                Debug.Log($"[PlayerHUDV2] ⭕ Slot {slotType} vacío (sin hechizo equipado)");
            }
        }
        
        private void UpdateMagicSlotCooldowns()
        {
            UpdateSlotCooldown(MagicSlot.Left);
            UpdateSlotCooldown(MagicSlot.Right);
            UpdateSlotCooldown(MagicSlot.Special);
        }
        
        private void UpdateSlotCooldown(MagicSlot slotType)
        {
            if (!_slotStates.ContainsKey(slotType)) return;
            if (_magicCaster == null) return;
            
            var slotState = _slotStates[slotType];
            
            // CRÍTICO: No procesar slots vacíos
            if (!slotState.hasSpell)
            {
                // Asegurar que el overlay esté oculto cada frame
                if (slotState.cooldownOverlay != null && slotState.cooldownOverlay.gameObject.activeSelf)
                {
                    slotState.cooldownOverlay.gameObject.SetActive(false);
                    Debug.Log($"[PlayerHUDV2] 🔒 Overlay {slotType} desactivado (slot vacío)");
                }
                return;
            }
            
            // Obtener spell y cooldown del MagicCaster
            MagicSpellSO spell = _magicCaster.GetSpellForSlot(slotType);
            if (spell == null)
            {
                // Si no hay spell pero hasSpell es true, corregir el estado
                slotState.hasSpell = false;
                if (slotState.cooldownOverlay != null && slotState.cooldownOverlay.gameObject.activeSelf)
                {
                    slotState.cooldownOverlay.gameObject.SetActive(false);
                    Debug.Log($"[PlayerHUDV2] 🔒 Overlay {slotType} desactivado (spell null)");
                }
                return;
            }
            
            float cooldownRemaining = _magicCaster.GetCooldownTime(slotType);
            bool canCast = _magicCaster.CanCastSpell(slotType, spell, out string reason);
            
            // El overlay SIEMPRE está visible para slots con hechizo
            if (slotState.cooldownOverlay != null && !slotState.cooldownOverlay.gameObject.activeSelf)
            {
                slotState.cooldownOverlay.gameObject.SetActive(true);
                Debug.Log($"[PlayerHUDV2] 👁️ Overlay {slotType} activado (tiene hechizo)");
            }
            
            // Actualizar visual del slot
            if (cooldownRemaining > 0f)
            {
                // EN COOLDOWN: El overlay se va RELLENANDO de 0 a 1
                // fillAmount = tiempo_transcurrido / tiempo_total
                // = (cooldown_total - tiempo_restante) / cooldown_total
                // = 1 - (tiempo_restante / cooldown_total)
                if (slotState.cooldownOverlay != null)
                {
                    float progress = 1f - Mathf.Clamp01(cooldownRemaining / spell.cooldown);
                    slotState.cooldownOverlay.fillAmount = progress;
                }
                
                // Cambiar color del slot durante cooldown
                if (slotState.slotImage != null)
                {
                    slotState.slotImage.color = cooldownColor;
                }
            }
            else if (!canCast && reason.Contains("mana"))
            {
                // Sin maná - overlay lleno pero color de sin maná
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.fillAmount = 1f; // Lleno = disponible (pero sin maná)
                }
                
                if (slotState.slotImage != null)
                {
                    slotState.slotImage.color = noManaColor;
                }
            }
            else
            {
                // Disponible - overlay lleno y color normal
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.fillAmount = 1f; // Lleno = disponible
                }
                
                if (slotState.slotImage != null)
                {
                    slotState.slotImage.color = availableColor;
                }
            }
        }
        
        #endregion
        
        #region API Pública
        
        /// <summary>
        /// Fuerza una actualización completa del HUD
        /// </summary>
        public void ForceRefresh()
        {
            RefreshHealthBar();
            RefreshManaBar();
            RefreshAllMagicSlots();
        }
        
        /// <summary>
        /// Actualiza solo un slot de magia específico
        /// </summary>
        public void RefreshMagicSlot(string slotName)
        {
            if (System.Enum.TryParse<MagicSlot>(slotName, true, out var slotType))
            {
                RefreshMagicSlot(slotType);
            }
        }
        
        /// <summary>
        /// Muestra/oculta todo el HUD
        /// </summary>
        public void SetHUDVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        #endregion
        
        #region Editor Helpers
        
        #if UNITY_EDITOR
        [ContextMenu("Force Refresh All")]
        private void EditorForceRefresh()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PlayerHUDV2] Force Refresh solo funciona en Play Mode");
                return;
            }
            
            ForceRefresh();
            Debug.Log("[PlayerHUDV2] ✅ HUD actualizado manualmente");
        }
        
        [ContextMenu("Validate Setup")]
        private void EditorValidateSetup()
        {
            ValidateReferences();
        }
        
        [ContextMenu("Test Fill Amounts")]
        private void EditorTestFillAmounts()
        {
            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = 0.75f;
                Debug.Log("[PlayerHUDV2] Health fill set to 75%");
            }
            
            if (manaFillImage != null)
            {
                manaFillImage.fillAmount = 0.5f;
                Debug.Log("[PlayerHUDV2] Mana fill set to 50%");
            }
        }
        #endif
        
        #endregion
    }
}

