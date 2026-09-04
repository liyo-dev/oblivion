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
        // Singleton instance
        public static PlayerHUDV2 Instance { get; private set; }
        
        #if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
        #endif
        
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
        
        [Header("Configuración de Fade")]
        [Tooltip("Duración del fade in/out en segundos")]
        [SerializeField] private float fadeDuration = 0.5f;
        
        // Referencias a sistemas del juego
        private PlayerHealthSystem _healthSystem;
        private ManaPool _manaPool;
        private MagicCaster _magicCaster;
        
        // Control de visibilidad
        private CanvasGroup _canvasGroup;
        private Tween _currentFadeTween;
        private bool _isVisible = true;
        // Contador de referencias: cuántos sistemas han pedido ocultar el HUD y aún no lo han
        // liberado. Ver comentario en HideHUD()/ShowHUD().
        private int _hideRequestCount = 0;
        
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
            // Configurar Singleton
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerHUDV2] Ya existe una instancia. Destruyendo duplicado.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Registrar en ServiceLocator
            ServiceLocator.Register(this);
            
            // Obtener o añadir CanvasGroup para el fade
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Asegurar que empieza visible
            _canvasGroup.alpha = 1f;
            _isVisible = true;
            _hideRequestCount = 0;

            // FIX: este Canvas también lleva un SceneBoundUI (para ocultarse/mostrarse según la
            // escena activa), y SceneBoundUI.BeginBossIntro/EndBossIntro operan sobre el MISMO
            // CanvasGroup capturando su alpha actual y restaurándolo después. Si BeginBossIntro
            // se disparaba mientras HideHUD()/ShowHUD() todavía tenía un fade a medias (ej: justo
            // al entrar en combate contra un boss que arranca pegado a una cinemática, como el
            // Gólem), capturaba un alpha intermedio y lo dejaba "restaurado" ahí para siempre —
            // el HUD se quedaba prácticamente invisible el resto del combate. Excluimos este
            // Canvas del snapshot genérico de SceneBoundUI (mismo patrón que
            // DramaticTextOverlayUI.Awake()) y dejamos que HideHUD()/ShowHUD() sean la única
            // fuente de verdad sobre su visibilidad.
            GetComponent<SceneBoundUI>()?.ExcludeFromBossIntro();
            
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
            
            // Obtener componentes del jugador (buscar en hijos para soportar jerarquías anidadas)
            _healthSystem = player.GetComponentInChildren<PlayerHealthSystem>(true);
            _manaPool = player.GetComponentInChildren<ManaPool>(true);
            _magicCaster = player.GetComponentInChildren<MagicCaster>(true);

            // Validar componentes críticos
            if (_healthSystem == null)
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró PlayerHealthSystem en el jugador");
            if (_manaPool == null)
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró ManaPool en el jugador");
            if (_magicCaster == null)
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No se encontró MagicCaster en el jugador");

            // Suscribirse a eventos
            SubscribeToEvents();

            // Suscribirse al evento OnPresetApplied para refrescar cuando se carga partida
            PlayerPresetService.OnPresetApplied += OnPresetApplied;
            // Refrescar iconos de hechizo al cambiar personaje activo
            PartyControlManager.OnActiveCharacterChanged += OnCharacterSwitched;
            
            // Marcar como inicializado
            _hasStarted = true;
            
            // Actualización inicial
            RefreshHealthBar();
            RefreshManaBar();
            RefreshAllMagicSlots();
            
            // Debug.Log($"[PlayerHUDV2] ✅ Start completado - Mana: {_manaPool?.Current ?? 0}/{_manaPool?.Max ?? 0}, HP: {_healthSystem?.CurrentHealth ?? 0}");
        }
        
        private void OnDestroy()
        {
            // Limpiar Singleton
            if (Instance == this)
            {
                Instance = null;
                ServiceLocator.Unregister(this);
            }
            
            UnsubscribeFromEvents();
            
            // ✅ Desuscribirse del evento de preset
            PlayerPresetService.OnPresetApplied -= OnPresetApplied;
            PartyControlManager.OnActiveCharacterChanged -= OnCharacterSwitched;
            
            // Limpiar todos los tweens (incluido el punch de escala del daño, que corre sobre el Transform, no sobre la Image)
            if (healthFillImage != null)
            {
                healthFillImage.DOKill();
                healthFillImage.transform.DOKill(true);
            }
            if (manaFillImage != null) manaFillImage.DOKill();
            _currentFadeTween?.Kill();
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
        }
        
        private void HideAllCooldownOverlays()
        {
            if (leftCooldownOverlay != null)
            {
                leftCooldownOverlay.enabled = false;
                leftCooldownOverlay.gameObject.SetActive(false);
                leftCooldownOverlay.fillAmount = 0f;
            }
            
            if (rightCooldownOverlay != null)
            {
                rightCooldownOverlay.enabled = false;
                rightCooldownOverlay.gameObject.SetActive(false);
                rightCooldownOverlay.fillAmount = 0f;
            }
            
            if (specialCooldownOverlay != null)
            {
                specialCooldownOverlay.enabled = false;
                specialCooldownOverlay.gameObject.SetActive(false);
                specialCooldownOverlay.fillAmount = 0f;
            }
            
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
                // Debug.Log("[PlayerHUDV2] ✅ Suscrito a OnHealthChanged");
            }
            
            if (_manaPool != null)
            {
                _manaPool.OnManaChanged.AddListener(OnManaChanged);
                // Debug.Log($"[PlayerHUDV2] ✅ Suscrito a OnManaChanged de ManaPool en '{_manaPool.gameObject.name}'");
            }
            else
            {
                Debug.LogWarning("[PlayerHUDV2] ⚠️ No hay ManaPool para suscribirse a OnManaChanged");
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
        
        /// <summary>
        /// Bandera para indicar si ya se ejecutó Start()
        /// </summary>
        private bool _hasStarted = false;

        /// <summary>
        /// Callback cuando se aplica el preset (al cargar partida o cambiar preset)
        /// </summary>
        private void OnPresetApplied()
        {
            // Si Start() aún no se ha ejecutado, las referencias se configurarán ahí
            if (!_hasStarted)
            {
                Debug.Log("[PlayerHUDV2] ⏭️ OnPresetApplied llamado antes de Start() - Se refrescará en Start()");
                return;
            }
            
            Debug.Log("[PlayerHUDV2] 🔄 OnPresetApplied - Refrescando HUD completo tras cargar partida");
            
            // RE-OBTENER referencias del player actual (pueden haber cambiado)
            var player = PlayerService.Player;
            if (player != null)
            {
                // Desuscribir de eventos anteriores
                UnsubscribeFromEvents();
                
                // Obtener nuevas referencias (buscar en hijos para soportar jerarquías anidadas)
                _healthSystem = player.GetComponentInChildren<PlayerHealthSystem>(true);
                _manaPool = player.GetComponentInChildren<ManaPool>(true);
                _magicCaster = player.GetComponentInChildren<MagicCaster>(true);
                
                // Re-suscribirse a eventos
                SubscribeToEvents();
            }
            
            // Validar que tenemos las referencias
            if (_healthSystem == null || _manaPool == null || _magicCaster == null)
            {
                Debug.LogWarning("[PlayerHUDV2] ⚠️ OnPresetApplied - Faltan referencias de componentes del player");
                return;
            }
            
            // Refrescar todo el HUD porque el preset puede haber cambiado
            RefreshHealthBar();
            RefreshManaBar();
            RefreshAllMagicSlots();

            // Debug.Log($"[PlayerHUDV2] ✅ HUD refrescado completamente tras aplicar preset (Mana: {_manaPool.Current}/{_manaPool.Max})");
        }

        private void OnCharacterSwitched(int _)
        {
            if (!_hasStarted || _magicCaster == null) return;
            RefreshAllMagicSlots();
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
            
            // Cancelar tweens previos: el fillAmount (sobre la Image) Y el punch de escala anterior
            // (sobre el Transform, un target DISTINTO — DOKill() en la Image no lo mataba, así que
            // si un golpe llegaba antes de que el punch anterior (0.3s) terminase, se apilaban dos
            // tweens de escala a la vez y la barra se quedaba desajustada/creciendo fuera de su caja).
            healthFillImage.DOKill();
            healthFillImage.transform.DOKill(true); // true = completa el punch en curso (vuelve a escala 1) antes de matarlo
            healthFillImage.transform.localScale = Vector3.one; // red de seguridad por si ya venía desajustada de antes de este fix
            
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
        // Objetivo de fillAmount para la regeneración. Se actualiza cada vez que ManaPool
        // notifica un cambio, pero quien realmente hace avanzar la interpolación es
        // TickManaRegen() desde Update() (ver comentario ahí abajo).
        private float _manaTargetFillAmount = 1f;

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

            _manaTargetFillAmount = targetFillAmount;

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
                // REGENERACIÓN: solo activamos la bandera aquí. La interpolación cuadro a cuadro
                // ocurre en TickManaRegen() (llamado desde Update()), NO en este método.
                //
                // FIX: antes el propio Lerp vivía aquí dentro, así que solo avanzaba un paso cada
                // vez que ManaPool disparaba OnManaChanged. En cuanto el maná llegaba al máximo,
                // ManaPool.Update() dejaba de notificar (early-return con "current >= max") y ese
                // último paso de Lerp casi nunca aterrizaba exactamente en 1 (un Lerp es asintótico:
                // se acerca pero no llega). Con fillAmount < 1 el Image tipo "Filled" recorta el
                // sprite de la barra por su borde derecho, así que la puntita redondeada del arte
                // (que solo existe completa en el sprite sin recortar, ver mp_bar_fill.png) se
                // quedaba mostrando un corte recto para siempre, aunque el maná ya estuviera lleno.
                // Al mover el Lerp a Update(), sigue avanzando cuadro a cuadro aunque no lleguen más
                // eventos de ManaPool, así que sí converge y encaja en el valor objetivo.
                if (!_isRegenerating)
                {
                    // Primera vez regenerando - cancelar tweens previos
                    manaFillImage.DOKill();
                    _isRegenerating = true;
                    _manaRegenStartTime = Time.time;
                }
            }
            else
            {
                // Sin cambio o ya completo
                _isRegenerating = false;
                manaFillImage.fillAmount = targetFillAmount;
            }

            _lastManaFillAmount = targetFillAmount;

            // Actualizar texto si existe
            if (manaText != null)
            {
                manaText.text = $"{Mathf.CeilToInt(currentMana)}/{Mathf.CeilToInt(maxMana)}";
            }
        }

        /// <summary>
        /// Avanza la interpolación de regeneración de maná un cuadro. Vive en Update() (no dentro
        /// de RefreshManaBar) precisamente para no depender de que ManaPool siga disparando
        /// OnManaChanged: así la barra siempre termina de converger a su valor final, incluyendo
        /// el "encaje" a fillAmount exacto que hace falta para que el sprite muestre su puntita
        /// redondeada completa. Ver el FIX comentado en RefreshManaBar().
        /// </summary>
        private void TickManaRegen()
        {
            if (!_isRegenerating || manaFillImage == null) return;

            float currentFillAmount = manaFillImage.fillAmount;
            float lerpSpeed = 5f; // Velocidad de interpolación
            float newFillAmount = Mathf.Lerp(currentFillAmount, _manaTargetFillAmount, lerpSpeed * Time.deltaTime);

            if (Mathf.Abs(_manaTargetFillAmount - newFillAmount) < 0.001f)
            {
                newFillAmount = _manaTargetFillAmount;
                _isRegenerating = false;
            }

            manaFillImage.fillAmount = newFillAmount;
        }

        #endregion
        
        #region Actualización de Slots de Magia
        
        private void Update()
        {
            // Actualizar cooldowns cada frame
            UpdateMagicSlotCooldowns();

            // Sigue interpolando la barra de maná hacia su objetivo aunque ManaPool ya no esté
            // notificando cambios (ver comentario en RefreshManaBar() / TickManaRegen()).
            TickManaRegen();
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
            
            // Limpiar el estado del slot PRIMERO
            slotState.hasSpell = false;
            slotState.equippedSprite = null;
            
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
                
                // Overlay siempre visible para slots con hechizo
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.enabled = true;
                    slotState.cooldownOverlay.gameObject.SetActive(true);
                    slotState.cooldownOverlay.fillAmount = 1f; // Empezar lleno (disponible)
                }
            }
            else
            {
                // Slot vacío
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
                
                // Ocultar overlay de cooldown para slots vacíos
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.enabled = false;
                    slotState.cooldownOverlay.gameObject.SetActive(false);
                    slotState.cooldownOverlay.fillAmount = 0f;
                }
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
            
            // No procesar slots vacíos
            if (!slotState.hasSpell)
            {
                // Asegurar que el overlay esté oculto
                if (slotState.cooldownOverlay != null)
                {
                    if (slotState.cooldownOverlay.gameObject.activeSelf || slotState.cooldownOverlay.enabled)
                    {
                        slotState.cooldownOverlay.enabled = false;
                        slotState.cooldownOverlay.gameObject.SetActive(false);
                        slotState.cooldownOverlay.fillAmount = 0f;
                    }
                }
                return;
            }
            
            // Verificar que el spell sigue existiendo
            MagicSpellSO spell = _magicCaster.GetSpellForSlot(slotType);
            if (spell == null)
            {
                slotState.hasSpell = false;
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.enabled = false;
                    slotState.cooldownOverlay.gameObject.SetActive(false);
                    slotState.cooldownOverlay.fillAmount = 0f;
                }
                return;
            }
            
            // El slot tiene un hechizo real
            float cooldownRemaining = _magicCaster.GetCooldownTime(slotType);
            bool canCast = _magicCaster.CanCastSpell(slotType, spell, out string reason);
            
            // El overlay debe estar visible para slots con hechizo
            if (slotState.cooldownOverlay != null && !slotState.cooldownOverlay.gameObject.activeSelf)
            {
                slotState.cooldownOverlay.enabled = true;
                slotState.cooldownOverlay.gameObject.SetActive(true);
            }
            
            // Actualizar visual del slot
            if (cooldownRemaining > 0f)
            {
                // EN COOLDOWN: El overlay se va RELLENANDO de 0 a 1
                if (slotState.cooldownOverlay != null)
                {
                    float progress = 1f - Mathf.Clamp01(cooldownRemaining / spell.cooldown);
                    slotState.cooldownOverlay.fillAmount = progress;
                }
                
                if (slotState.slotImage != null)
                {
                    slotState.slotImage.color = cooldownColor;
                }
            }
            else if (!canCast && reason.Contains("mana"))
            {
                // Sin maná
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.fillAmount = 1f;
                }
                
                if (slotState.slotImage != null)
                {
                    slotState.slotImage.color = noManaColor;
                }
            }
            else
            {
                // Disponible
                if (slotState.cooldownOverlay != null)
                {
                    slotState.cooldownOverlay.fillAmount = 1f;
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
        
        /// <summary>
        /// Oculta el HUD con un fade suave usando DOTween.
        /// FIX: 7 sistemas independientes (DialogueManager, CinematicSequencerBase,
        /// SimpleCinematicDirector, BossIntroPresentation, DramaticTextOverlayUI,
        /// PlayerEquipmentMenuController, DialogueCinematicController) llaman a HideHUD/ShowHUD
        /// sobre este mismo singleton sin coordinarse entre sí. Con el booleano _isVisible antiguo,
        /// si el sistema A ocultaba el HUD y luego el sistema B (anidado, ej. un bocadillo dentro de
        /// una cinemática) hacía su propio Show antes de que A terminara, el HUD reaparecía a mitad
        /// de secuencia y nada volvía a ocultarlo hasta el EndCinematic() de A — el bug de "el HUD
        /// se queda/reaparece en las secuencias". _hideRequestCount convierte esto en un contador de
        /// referencias: el HUD solo se muestra cuando TODOS los que pidieron ocultarlo han pedido
        /// mostrarlo de vuelta. La firma pública no cambia, así que ningún call site necesita tocarse.
        /// </summary>
        public void HideHUD(float duration = -1f)
        {
            _hideRequestCount++;
            if (!_isVisible) return; // Ya está oculto (por este u otro sistema)

            _isVisible = false;
            float useDuration = duration > 0 ? duration : fadeDuration;

            // Matar tween anterior si existe
            _currentFadeTween?.Kill();

            // Si _canvasGroup es null, intentar obtenerlo
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return; // Si sigue siendo null, salir

            // Fade out suave
            _currentFadeTween = _canvasGroup.DOFade(0f, useDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true) // Ignora timeScale
                .OnComplete(() =>
                {
                    if (_canvasGroup != null)
                    {
                        _canvasGroup.interactable = false;
                        _canvasGroup.blocksRaycasts = false;
                    }
                });
        }

        /// <summary>
        /// Muestra el HUD con un fade suave usando DOTween. Ver comentario de HideHUD(): solo
        /// revela el HUD de verdad cuando ningún otro sistema sigue reclamándolo oculto.
        /// </summary>
        public void ShowHUD(float duration = -1f)
        {
            if (_hideRequestCount > 0) _hideRequestCount--;
            if (_hideRequestCount > 0) return; // Otro sistema todavía lo quiere oculto
            if (_isVisible) return; // Ya está visible

            _isVisible = true;
            float useDuration = duration > 0 ? duration : fadeDuration;

            // Matar tween anterior si existe
            _currentFadeTween?.Kill();

            // Si _canvasGroup es null, intentar obtenerlo
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) return; // Si sigue siendo null, salir

            // Restaurar interactividad antes del fade in
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;

            // Fade in suave
            _currentFadeTween = _canvasGroup.DOFade(1f, useDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true); // Ignora timeScale
        }
        
        /// <summary>
        /// Verifica si el HUD está visible
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Reinicia por completo el estado de "ocultado" del HUD (contador de referencias, tween
        /// de fade y visibilidad), forzándolo a visible.
        ///
        /// FIX: HideHUD()/ShowHUD() usan un contador de referencias (_hideRequestCount) para
        /// coordinar los ~8 sistemas independientes que ocultan el HUD (diálogos, cinemáticas,
        /// menú de inventario...). Como este componente vive en Start.unity (DontDestroyOnLoad),
        /// su Awake() —que es lo único que reinicia el contador a 0— solo se ejecuta UNA VEZ por
        /// sesión de la aplicación, nunca al "cargar partida" ni al "salir al menú principal". Si
        /// cualquiera de esos sistemas queda interrumpido antes de poder emparejar su HideHUD()
        /// con el ShowHUD() correspondiente (p.ej. la escena cambia a mitad de un diálogo o justo
        /// al confirmar "Salir al menú principal" desde el inventario), el contador se queda
        /// colgado para siempre y el HUD no vuelve a aparecer en lo que dure la sesión, aunque se
        /// cargue una partida nueva (repro: cargar partida → abrir inventario → salir al menú
        /// principal → cargar partida de nuevo → el HUD ya no aparece).
        ///
        /// Se llama desde GameBootService.ResetTransientSessionState(), el mismo punto de entrada
        /// seguro de sesión que ya limpia GameState/CameraDirectorService/SimpleCinematicDirector/
        /// TeleportService por el mismo motivo — ver el comentario de ese método.
        /// </summary>
        public static void ForceResetHideState()
        {
            var hud = Instance;
            if (hud == null) return;

            hud._currentFadeTween?.Kill();
            hud._hideRequestCount = 0;
            hud._isVisible = true;

            if (hud._canvasGroup == null) hud._canvasGroup = hud.GetComponent<CanvasGroup>();
            if (hud._canvasGroup != null)
            {
                hud._canvasGroup.alpha = 1f;
                hud._canvasGroup.interactable = true;
                hud._canvasGroup.blocksRaycasts = true;
            }
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
            // Debug.Log("[PlayerHUDV2] ✅ HUD actualizado manualmente");
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
