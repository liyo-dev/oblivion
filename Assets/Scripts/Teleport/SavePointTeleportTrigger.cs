using UnityEngine;
using Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Componente que activa el hint global de teletransporte cuando el jugador
/// está en un SavePoint y el sistema de teletransporte está disponible.
/// Añadir a un SavePoint junto con el trigger.
/// </summary>
// Ejecutar antes que vThirdPersonInput (orden 0) para que PushUIMode desactive
// GamePlay antes de que Invector lea AttackMagicSpecialPressed en el mismo frame.
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Collider))]
public class SavePointTeleportTrigger : MonoBehaviour
{
        [Header("Config")]
        [Tooltip("ID del anchor asociado a este SavePoint. Si está vacío, intenta obtenerlo del SpawnAnchor padre.")]
        [SerializeField] private string anchorIdOverride;
        
        [Tooltip("Nombre para mostrar del punto de teletransporte. Si está vacío, usa localización o anchorId.")]
        [SerializeField] private string displayNameOverride;
        
        [Header("Auto-register")]
        [Tooltip("Si true, desbloquea automáticamente este punto cuando el jugador entra al trigger.")]
        [SerializeField] private bool autoUnlockOnEnter = true;
        
        private string _anchorId;
        private bool _playerInRange;
        private bool _hintRequested;
        
        private void Awake()
        {
            // Asegurar que el collider es trigger
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            
            // Resolver anchorId
            _anchorId = anchorIdOverride;
            if (string.IsNullOrEmpty(_anchorId))
            {
                var anchor = GetComponentInParent<SpawnAnchor>() ?? GetComponent<SpawnAnchor>();
                if (anchor != null)
                    _anchorId = anchor.anchorId;
            }
        }
        
        private void Start()
        {
            // Si el punto ya está desbloqueado y tiene displayName configurado,
            // actualizar el nombre en el registro (puede venir de LoadFromSaveData con nombre derivado)
            if (!string.IsNullOrEmpty(_anchorId) && !string.IsNullOrEmpty(displayNameOverride))
                TeleportRegistry.UpdateDisplayNameIfUnlocked(_anchorId, displayNameOverride);
        }

        private void OnEnable()
        {
            TeleportRegistry.OnRegistryChanged += UpdateHintVisibility;
            GameState.OnChanged += UpdateHintVisibility;
        }

        private void OnDisable()
        {
            TeleportRegistry.OnRegistryChanged -= UpdateHintVisibility;
            GameState.OnChanged -= UpdateHintVisibility;
            
            // Liberar hint si estaba activo
            if (_hintRequested)
            {
                _hintRequested = false;
                var hintUI = TeleportHintUI.Instance ?? FindFirstObjectByType<TeleportHintUI>();
                if (hintUI != null)
                    hintUI.RequestHide();
            }
            
            _playerInRange = false;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            _playerInRange = true;
            
            // Auto-desbloquear punto si está configurado
            if (autoUnlockOnEnter && !string.IsNullOrEmpty(_anchorId))
            {
                TeleportRegistry.UnlockPoint(_anchorId, displayNameOverride);
            }
            
            UpdateHintVisibility();
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            
            _playerInRange = false;
            
            // Liberar solicitud de hint
            if (_hintRequested)
            {
                _hintRequested = false;
                var hintUI = TeleportHintUI.Instance ?? FindFirstObjectByType<TeleportHintUI>();
                if (hintUI != null)
                    hintUI.RequestHide();
            }
        }
        
        private void Update()
        {
            if (!_playerInRange) return;
            if (!TeleportRegistry.IsSystemAvailable) return;
            if (!GameState.CanInteractGlobally) return;
            
            // Detectar botón Y directamente
            if (IsYButtonPressed())
            {
                Debug.Log($"[SavePointTeleportTrigger] Botón Y presionado! Abriendo menú de teletransporte...");
                OpenTeleportUI();
            }
        }
        
        private bool IsYButtonPressed()
        {
#if ENABLE_INPUT_SYSTEM
            // Gamepad: buttonNorth es Y en Xbox, △ en PlayStation
            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
                return true;
            
            // También verificar teclado (T para teleport)
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
                return true;
#endif
            return false;
        }
        
        private void OpenTeleportUI()
        {
            // Ocultar hint antes de abrir el menú
            if (_hintRequested)
            {
                _hintRequested = false;
                var hintUI = TeleportHintUI.Instance ?? FindFirstObjectByType<TeleportHintUI>();
                if (hintUI != null)
                    hintUI.RequestHide();
            }
            
            var teleportSystem = TeleportSystem.Instance ?? FindFirstObjectByType<TeleportSystem>();
            if (teleportSystem != null)
            {
                // Pasar el anchorId actual para excluirlo de la lista
                teleportSystem.OpenTeleportMenu(_anchorId);
            }
            else
            {
                Debug.LogWarning("[SavePointTeleportTrigger] TeleportSystem no encontrado.");
            }
        }
        
        private void UpdateHintVisibility()
        {
            bool shouldShow = _playerInRange && 
                              TeleportRegistry.IsSystemAvailable && 
                              GameState.CanInteractGlobally;
            
            // Buscar TeleportHintUI si no existe instancia
            var hintUI = TeleportHintUI.Instance;
            if (hintUI == null)
                hintUI = FindFirstObjectByType<TeleportHintUI>();
            
            if (shouldShow && !_hintRequested)
            {
                _hintRequested = true;
                if (hintUI != null)
                    hintUI.RequestShow();
            }
            else if (!shouldShow && _hintRequested)
            {
                _hintRequested = false;
                if (hintUI != null)
                    hintUI.RequestHide();
            }
        }
}
