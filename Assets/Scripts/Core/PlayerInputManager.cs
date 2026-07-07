using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core
{
    /// <summary>
    /// Núcleo de entrada del jugador. Expone una instancia compartida de <see cref="PlayerControls"/>
    /// y gestiona centralizadamente el cambio entre input de Gameplay y UI.
    /// Registra el servicio en <see cref="ServiceLocator"/> para que el resto de sistemas pueda
    /// obtener el mismo set de acciones sin crear copias adicionales del asset de Input System.
    /// 
    /// RESPONSABILIDADES:
    /// - Mantener la instancia única de PlayerControls
    /// - Gestionar el cambio entre modo UI y modo Gameplay
    /// - Enrutar permisos de acciones hacia PlayerActionManager
    /// </summary>
    [DefaultExecutionOrder(-250)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputManager : MonoBehaviour
    {
        public static PlayerInputManager Instance { get; private set; }

        #if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
        #endif

        [Header("Comportamiento")]
        [SerializeField] private bool dontDestroyOnLoad = true;
        
#if UNITY_EDITOR
        [SerializeField] private bool debugLogs = true;
#endif

        private PlayerControls _controls;
        private bool _ownsControlsInstance;
        private bool _isInUIMode;
        private int _uiModeRefCount;

        /// <summary>Acceso único al asset de acciones de gameplay.</summary>
        public PlayerControls Controls => _controls;

        /// <summary>Indica si actualmente estamos en modo UI (inputs de gameplay deshabilitados)</summary>
        public bool IsInUIMode => _isInUIMode;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad)
                UnityEngine.Object.DontDestroyOnLoad(gameObject);

            InitializeControls();
            ServiceLocator.Register(this);

#if UNITY_EDITOR
            if (debugLogs)
                Debug.Log("[PlayerInputManager] Inicializado");
#endif
        }

        private void InitializeControls()
        {
            _controls = new PlayerControls();
            _ownsControlsInstance = true;


            // Iniciar en modo Gameplay por defecto
            _controls.GamePlay.Enable();
            _controls.UI.Disable();
            _isInUIMode = false;
            _uiModeRefCount = 0;

#if UNITY_EDITOR
            if (debugLogs)
            {
                Debug.Log("[PlayerInputManager] Controls initialized in Gameplay mode");
            }
#endif
        }

        /// <summary>
        /// Comprueba si la acción está permitida.
        /// Por ahora siempre retorna true ya que no hay sistema de bloqueo de acciones activo.
        /// </summary>
        public bool CanProcess(PlayerAbility ability)
        {
            // TODO: Si necesitas un sistema de bloqueo de acciones, implementarlo aquí
            return true;
        }

        /// <summary>
        /// Cambia a modo UI: habilita inputs de UI y deshabilita inputs de Gameplay.
        /// Usa un contador de referencias para soportar contextos anidados (ej: diálogo dentro de menú).
        /// </summary>
        public void PushUIMode()
        {
            _uiModeRefCount++;
            
#if UNITY_EDITOR
            if (debugLogs)
                Debug.Log($"[PlayerInputManager] PushUIMode() llamado. RefCount: {_uiModeRefCount}");
#endif
            
            if (_uiModeRefCount == 1) // Primera llamada
            {
                _isInUIMode = true;
                
                _controls.GamePlay.Disable();
                _controls.UI.Enable();

#if UNITY_EDITOR
                if (debugLogs)
                    Debug.Log("[PlayerInputManager] Modo UI ACTIVADO (refCount=1)");
#endif
            }
#if UNITY_EDITOR
            else
            {
                if (debugLogs)
                    Debug.Log($"[PlayerInputManager] Modo UI ya activo (refCount={_uiModeRefCount})");
            }
#endif
        }

        /// <summary>
        /// Sale del modo UI. Solo restaura el modo Gameplay cuando el contador llega a 0.
        /// </summary>
        public void PopUIMode()
        {
            if (_uiModeRefCount <= 0)
            {
#if UNITY_EDITOR
                if (debugLogs)
                    Debug.LogWarning("[PlayerInputManager] PopUIMode llamado sin PushUIMode previo");
#endif
                return;
            }

            _uiModeRefCount--;

            if (_uiModeRefCount == 0) // Última llamada
            {
                _isInUIMode = false;
                _controls.UI.Disable();
                _controls.GamePlay.Enable();

#if UNITY_EDITOR
                if (debugLogs)
                    Debug.Log("[PlayerInputManager] Modo GAMEPLAY restaurado (refCount=0)");
#endif
            }
#if UNITY_EDITOR
            else if (debugLogs)
            {
                Debug.Log($"[PlayerInputManager] Modo UI aún activo (refCount={_uiModeRefCount})");
            }
#endif
        }

        /// <summary>
        /// Fuerza el reseteo del modo de input a Gameplay independientemente del contador.
        /// Útil cuando el stack queda desbalanceado por un error en el flujo de cierre de un menú.
        /// Llamar solo como último recurso desde sistemas de recuperación de emergencia.
        /// </summary>
        public void ForceRestoreGameplayMode()
        {
            if (_uiModeRefCount > 0)
            {
                Debug.LogWarning($"[PlayerInputManager] ForceRestoreGameplayMode: resetando refCount {_uiModeRefCount} → 0. " +
                                  "Algún PushUIMode no tuvo su PopUIMode correspondiente.");
            }
            _uiModeRefCount = 0;
            _isInUIMode = false;
            if (_controls != null)
            {
                _controls.UI.Disable();
                _controls.GamePlay.Enable();
            }
        }

        void OnEnable()
        {
            EnableControls();
        }

        private void EnableControls()
        {
            if (_controls == null) return;

            // Restaurar el modo correcto según el estado
            if (_isInUIMode)
            {
                _controls.UI.Enable();
                _controls.GamePlay.Disable();
            }
            else
            {
                _controls.GamePlay.Enable();
                _controls.UI.Disable();
            }
        }

        void OnDisable()
        {
            DisableControls();
        }

        private void DisableControls()
        {
            _controls?.Disable();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                ServiceLocator.Unregister(this);
                Instance = null;
            }

            DisposeControls();
        }

        private void DisposeControls()
        {
            if (_ownsControlsInstance)
            {
                _controls?.Dispose();
            }
        }


        /// <summary>Acceso cómodo para obtener una acción de gameplay de forma segura.</summary>
        public InputAction GetGameplayAction(Func<PlayerControls.GamePlayActions, InputAction> selector)
        {
            if (_controls == null || selector == null) return null;
            return selector.Invoke(_controls.GamePlay);
        }

        /// <summary>
        /// Devuelve la instancia compartida de <see cref="PlayerControls"/> si existe.
        /// Si no hay <see cref="PlayerInputManager"/>, crea una nueva instancia y
        /// devuelve la propiedad de eliminación al llamador mediante <paramref name="ownsInstance"/>.
        /// </summary>
        public static PlayerControls GetSharedOrNew(out bool ownsInstance)
        {
            if (Instance != null && Instance._controls != null)
            {
                ownsInstance = false;
                return Instance._controls;
            }

            ownsInstance = true;
            return new PlayerControls();
        }
    }
}
