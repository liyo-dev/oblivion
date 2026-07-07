using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

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
        private EventSystem _persistentEventSystem;
        private EventSystem _connectedEventSystem;

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
            SceneManager.sceneLoaded += OnSceneLoaded;

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

        void Start()
        {
            ConnectToEventSystemModule();
            PersistEventSystem();
        }

        // Hace el EventSystem de Start.unity DontDestroyOnLoad para que sobreviva
        // la carga no-aditiva hacia MainMenu → MainWorld. Sin esto, MainWorld queda
        // sin EventSystem y la navegación por teclado/gamepad en menús no funciona.
        private void PersistEventSystem()
        {
            var es = EventSystem.current;
            if (es == null) return;

            // Solo persistir si es root (DontDestroyOnLoad requiere root)
            if (es.transform.parent != null) return;

            // Evitar llamar DontDestroyOnLoad dos veces en la misma instancia
            if (_persistentEventSystem == es) return;

            _persistentEventSystem = es;
            DontDestroyOnLoad(es.gameObject);

#if UNITY_EDITOR
            if (debugLogs)
                Debug.Log("[PlayerInputManager] EventSystem marcado como DontDestroyOnLoad");
#endif
        }

        /// <summary>
        /// Reasigna el actionsAsset del InputSystemUIInputModule al asset de controles activo.
        /// Necesario porque el EventSystem puede tener referencias rotas si el .inputactions
        /// fue renombrado o movido (el GUID serializado en la escena ya no existe).
        /// </summary>
        private void ConnectToEventSystemModule()
        {
            var es = EventSystem.current;
            if (es == null)
            {
#if UNITY_EDITOR
                if (debugLogs)
                    Debug.LogWarning("[PlayerInputManager] EventSystem no encontrado en Start() — navegación UI podría no funcionar");
#endif
                return;
            }

            var uiModule = es.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null) return;

            // Deshabilitar antes de reasignar para evitar callbacks durante la transición
            uiModule.enabled = false;

            // Reconectar el asset
            uiModule.actionsAsset = _controls.asset;

            // Las referencias individuales serializadas apuntan al GUID del asset eliminado y tienen
            // prioridad sobre actionsAsset. Hay que reasignarlas explícitamente con el asset activo.
            var ui = _controls.UI;
            uiModule.move                    = InputActionReference.Create(ui.Navigate);
            uiModule.submit                  = InputActionReference.Create(ui.Submit);
            uiModule.cancel                  = InputActionReference.Create(ui.Cancel);
            uiModule.point                   = InputActionReference.Create(ui.Point);
            uiModule.leftClick               = InputActionReference.Create(ui.Click);
            uiModule.middleClick             = InputActionReference.Create(ui.MiddleClick);
            uiModule.rightClick              = InputActionReference.Create(ui.RightClick);
            uiModule.scrollWheel             = InputActionReference.Create(ui.ScrollWheel);
            uiModule.trackedDevicePosition    = InputActionReference.Create(ui.TrackedDevicePosition);
            uiModule.trackedDeviceOrientation = InputActionReference.Create(ui.TrackedDeviceOrientation);

            // Rehabilitar para que el módulo enlace los nuevos bindings
            uiModule.enabled = true;

            // Restaurar el estado correcto: el módulo puede haber habilitado action maps
            EnableControls();

            _connectedEventSystem = es;

#if UNITY_EDITOR
            if (debugLogs)
                Debug.Log("[PlayerInputManager] InputSystemUIInputModule reconectado con referencias explícitas");
#endif
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
                SceneManager.sceneLoaded -= OnSceneLoaded;
                ServiceLocator.Unregister(this);
                Instance = null;
            }

            DisposeControls();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Si la nueva escena no tiene EventSystem propio (ej: MainWorld), EventSystem.current
            // queda null cuando el EventSystem de la escena anterior se destruye. Reactivamos el
            // persistente para que se re-registre como current.
            if (EventSystem.current == null && _persistentEventSystem != null)
            {
                _persistentEventSystem.gameObject.SetActive(false);
                _persistentEventSystem.gameObject.SetActive(true);
            }

            // Solo reconectar el módulo si el EventSystem ha cambiado (ej: nueva escena no-aditiva).
            // Reconectar innecesariamente en cargas aditivas (cinemáticas) deshabilita el UIInputModule
            // brevemente y puede interrumpir el input de gameplay.
            if (EventSystem.current != null && EventSystem.current != _connectedEventSystem)
            {
                ConnectToEventSystemModule();
            }
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
