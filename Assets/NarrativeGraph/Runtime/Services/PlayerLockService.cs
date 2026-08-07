using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Centraliza el bloqueo de movimiento del jugador con referencia por solicitante.
/// Deshabilita acciones de gameplay, CharacterController, Rigidbody y script de locomoción.
/// Usa el sistema centralizado de PlayerInputManager para gestionar UI/Gameplay.
/// </summary>
[DefaultExecutionOrder(-275)]
public class PlayerLockService : MonoBehaviour
{
    static PlayerLockService _instance;
    static bool _isShuttingDown;
    public static bool HasInstance => _instance != null;
    public static PlayerLockService Instance
    {
        get
        {
            // No recrear la instancia si el juego/escena ya está cerrando: crear un
            // GameObject nuevo en ese momento es exactamente lo que dispara el warning
            // de Unity "Some objects were not cleaned up when closing the scene"
            // (el nuevo GO queda huérfano porque DontDestroyOnLoad no llega a tiempo).
            if (_isShuttingDown)
                return _instance;

            if (_instance == null)
            {
                var go = new GameObject("PlayerLockService");
                _instance = go.AddComponent<PlayerLockService>();
                DontDestroyOnLoad(go);
                ServiceLocator.Register(_instance);
            }
            return _instance;
        }
    }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
        _isShuttingDown = false;
    }
#endif

    readonly HashSet<object> _owners = new HashSet<object>();

    bool _hardLockActive; // true solo si ApplyHardLock() hizo PushUIMode — para parear el Pop exacto
    CharacterController _charController;
    bool _charControllerWasEnabled;
    Rigidbody _rb;
    MonoBehaviour _movementScript;
    bool _movementScriptWasEnabled;
    Invector.vCharacterController.vThirdPersonMotor _lockedMotor;
    bool _motorWasLocked;
    Invector.vCharacterController.vThirdPersonInput _suppressedInput;

    public bool IsLocked => _owners.Count > 0;

    /// <summary>
    /// Método de emergencia para forzar el desbloqueo del player.
    /// Solo usar en debug si el player queda bloqueado por un bug.
    /// </summary>
    public void ForceUnlock()
    {
        if (_owners.Count == 0)
        {
            Debug.LogWarning("[PlayerLockService] ⚠️ ForceUnlock() llamado pero no hay locks activos");
            return;
        }

        Debug.LogWarning($"[PlayerLockService] 🚨 FORCE UNLOCK - Limpiando {_owners.Count} locks forzadamente");
        _owners.Clear();
        _lockedMotor = null; // evitar restaurar lockMovement al estado bloqueado
        ReleaseHardLock();
    }

    public void Acquire(object owner)
    {
        if (owner == null) owner = this;
        if (_owners.Contains(owner))
        {
            Debug.LogWarning($"[PlayerLockService] ⚠️ Owner ya tenía un lock: {owner?.GetType().Name ?? "null"}");
            return;
        }
        
        _owners.Add(owner);
        Debug.Log($"[PlayerLockService] 🔒 Acquire de {owner?.GetType().Name ?? "null"}. Total locks: {_owners.Count}");

        if (_owners.Count == 1)
        {
            Debug.Log("[PlayerLockService] 🚫 Primer lock - Deshabilitando movimiento del jugador");
            ApplyHardLock();
        }
    }

    public void Release(object owner)
    {
        if (owner == null) owner = this;
        
        if (!_owners.Contains(owner))
        {
            Debug.LogWarning($"[PlayerLockService] ⚠️ Intento de Release de owner no registrado: {owner?.GetType().Name ?? "null"}");
            return;
        }
        
        _owners.Remove(owner);
        Debug.Log($"[PlayerLockService] 🔓 Release de {owner?.GetType().Name ?? "null"}. Locks restantes: {_owners.Count}");

        if (_owners.Count == 0)
        {
            Debug.Log("[PlayerLockService] ✅ Todos los locks liberados - Reactivando movimiento del jugador");
            ReleaseHardLock();
        }
    }

    /// <summary>
    /// Atajo para el patrón "puente de un trigger hasta que el sistema narrativo tome el
    /// control": adquiere el lock YA (freeze inmediato) y lo libera en cuanto
    /// ActionMode.Cinematic esté activo en PlayerActionManager, o tras maxFramesSafety frames
    /// si el grafo nunca llega a empujar ese modo (para no dejar al jugador congelado para
    /// siempre por un evento sin nodo de bloqueo después, o un WaitCustomEventNode que tarda
    /// varios frames en encadenar hasta el nodo que realmente hace PushMode).
    ///
    /// FIX (Agosto 2026): antes cada trigger (KingdomBoundaryTrigger, TriggerPlayerStop.
    /// IniciarParadaMomentanea) liberaba su propio lock "un frame después" con una corrutina
    /// alojada en SU PROPIO GameObject. Dos problemas:
    /// 1) El grafo narrativo (NarrativeRunner.RunSubGraph) avanza nodo a nodo mediante
    ///    `yield return new WaitUntil(...)`, que SIEMPRE cede como mínimo 1 frame por nodo aunque
    ///    ese nodo resuelva su `ready` de forma síncrona. Si entre el WaitCustomEventNode que
    ///    consume el evento del trigger y el nodo que hace PushMode(ActionMode.Cinematic)
    ///    (LockPlayerNode, o el LockCinematic() interno de un CinematicSequencerBase) hay más de
    ///    un salto, el freeze de "1 frame fijo" se soltaba ANTES de que el grafo tomara el
    ///    control real — el jugador recuperaba el movimiento libre durante uno o más frames y
    ///    quedaba mal ubicado para la secuencia.
    /// 2) Triggers con DestroyElement=1 en OnTriggerEnter_Event (EXIT_FROM_WOODS_ESTELA,
    ///    FUEGO_FATUO) destruían su propio GameObject el mismo frame en que emitían el evento;
    ///    al destruirse, la corrutina "liberar el siguiente frame" (alojada en ese mismo objeto)
    ///    se abortaba y el lock se soltaba en el acto vía OnDestroy(), sin ni siquiera llegar a
    ///    esperar ese frame.
    /// Alojar la corrutina aquí (PlayerLockService es DontDestroyOnLoad) resuelve ambos: sobrevive
    /// a que el trigger que la pidió se destruya, y espera de verdad a que Cinematic esté activo
    /// en vez de asumir que 1 frame siempre alcanza.
    /// </summary>
    public void AcquireBridgeUntilCinematic(object owner, int maxFramesSafety = 60)
    {
        Acquire(owner);
        StartCoroutine(Co_ReleaseWhenCinematicOrTimeout(owner, maxFramesSafety));
    }

    IEnumerator Co_ReleaseWhenCinematicOrTimeout(object owner, int maxFramesSafety)
    {
        var pam = ServiceLocator.Get<PlayerActionManager>(logIfMissing: false);
        int frames = 0;
        yield return null; // como mínimo 1 frame, igual que el comportamiento anterior
        while (frames < maxFramesSafety && (pam == null || !pam.IsInMode(ActionMode.Cinematic)))
        {
            frames++;
            yield return null;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (pam != null && !pam.IsInMode(ActionMode.Cinematic))
        {
            Debug.LogWarning($"[PlayerLockService] Puente de {owner?.GetType().Name ?? "null"} liberado por timeout " +
                              $"({maxFramesSafety} frames) sin que el grafo narrativo activara ActionMode.Cinematic. " +
                              "¿Falta un LockPlayerNode/CinematicSequencerBase tras el WaitCustomEventNode de este evento?");
        }
#endif
        Release(owner);
    }

    void ApplyHardLock()
    {
        if (!PlayerService.TryGetPlayer(out var player, true) || player == null)
            return;

        // Cambiar a modo UI usando el sistema centralizado
        if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
        {
            pim.PushUIMode();
            _hardLockActive = true;
        }

        _charController = player.GetComponent<CharacterController>();
        if (_charController != null)
        {
            _charControllerWasEnabled = _charController.enabled;
            _charController.enabled = false;
        }

        _rb = player.GetComponent<Rigidbody>();
        if (_rb != null)
        {
            // Solo modificar velocidad si NO es kinematic
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            
            // NO PONER EN KINEMATIC - dejar que los scripts de Invector se deshabiliten
            // _rb.isKinematic = true; // ❌ ESTO CAUSA LOS WARNINGS
        }

        // Bloquear movimiento sin deshabilitar el controller completo.
        // Deshabilitar vThirdPersonController para UpdateMotor() que gestiona
        // CheckGround() y ControlMaterialPhysics(). Sin esto el CapsuleCollider
        // queda con slippyPhysics (fricción 0) y el player cae a través del suelo.
        _lockedMotor = player.GetComponent<Invector.vCharacterController.vThirdPersonMotor>();
        if (_lockedMotor != null)
        {
            _motorWasLocked = _lockedMotor.lockMovement;
            _lockedMotor.lockMovement = true;

            // FIX: inputSmooth/moveDirection son 'internal' en vThirdPersonMotor y NO se
            // resetean al activar lockMovement (el comentario del propio Invector lo dice:
            // "lock the movement of the controller, not the animation"). Si el jugador estaba
            // sprintando justo cuando se adquiere el lock (abrir menú de equipo/pausa, entrar
            // en diálogo/tienda), estos valores quedan congelados en su magnitud de sprint.
            // ControlAnimatorRootMotion() (OnAnimatorMove) NO comprueba lockMovement, así que
            // mientras el lock esté activo el root motion de la animación de sprint se sigue
            // acumulando en animator.rootPosition sin reflejarse en transform.position — el
            // snap-sync ("transform.position = animator.rootPosition") solo ocurre cuando
            // inputSmooth == Vector3.zero exactamente. Al soltar el lock, inputSmooth tarda
            // varios frames en decaer a cero, y ese frame vuelca de golpe todo el desfase
            // acumulado: el player "salta" hacia delante y la cámara (recién reactivada, con
            // su propio suavizado de reconexión) tiene que perseguirlo, dando el efecto de
            // quedarse atrás al reanudar el sprint tras pausa/tienda. Resetear aquí evita que
            // el desfase se acumule mientras el lock está activo.
            _lockedMotor.ResetInputSmoothing();
            Debug.Log("[PlayerLockService] lockMovement=true en vThirdPersonMotor (inputSmooth/moveDirection reseteados)");
        }
        else
        {
            // Fallback: deshabilitar el script si no se encuentra vThirdPersonMotor
            _movementScript = player.GetComponents<MonoBehaviour>()
                .FirstOrDefault(m => m != null && m.enabled && m != this && !(m is PlayerActionManager) && (
                    m.GetType().Name == "vThirdPersonController" ||
                    m.GetType().Name == "vThirdPersonInput" ||
                    m.GetType().Name == "ThirdPersonController" ||
                    m.GetType().Name == "ThirdPersonInput"
                ));
            if (_movementScript != null)
            {
                _movementScriptWasEnabled = _movementScript.enabled;
                _movementScript.enabled = false;
                Debug.Log($"[PlayerLockService] Fallback: script '{_movementScript.GetType().Name}' DESHABILITADO");
            }
            else
            {
                Debug.LogWarning("[PlayerLockService] No se encontró vThirdPersonMotor ni script de movimiento");
            }
        }

        // Pone cc.input a cero de inmediato y bloquea jump/sprint en vThirdPersonInput.
        // Llamar MoveInput() explícitamente para zerear cc.input en este frame sin esperar al
        // próximo Update() — evita que un FixedUpdate intermedio aplique movimiento residual.
        _suppressedInput = player.GetComponent<Invector.vCharacterController.vThirdPersonInput>();
        if (_suppressedInput != null)
        {
            _suppressedInput.SuppressMoveInput = true;
            _suppressedInput.MoveInput();
        }
    }

    void ReleaseHardLock()
    {
        // Restaurar modo Gameplay usando el sistema centralizado — solo si PushUIMode fue emitido
        if (_hardLockActive)
        {
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();
            _hardLockActive = false;
        }

        if (_charController != null)
        {
            _charController.enabled = _charControllerWasEnabled;
        }
        _charController = null;

        // NO restaurar isKinematic ya que nunca lo cambiamos
        // if (_rb != null)
        // {
        //     _rb.isKinematic = _rbWasKinematic;
        // }
        _rb = null;

        if (_lockedMotor != null)
        {
            _lockedMotor.lockMovement = _motorWasLocked;
            Debug.Log("[PlayerLockService] lockMovement restaurado en vThirdPersonMotor");
            _lockedMotor = null;
        }
        else if (_movementScript != null)
        {
            _movementScript.enabled = _movementScriptWasEnabled;
            Debug.Log($"[PlayerLockService] Script de movimiento '{_movementScript.GetType().Name}' RESTAURADO");
        }
        _movementScript = null;

        // Restaurar SuppressMoveInput y añadir gracia para evitar salto al cerrar UI
        if (_suppressedInput != null)
        {
            _suppressedInput.SuppressMoveInput = false;
            _suppressedInput = null;
        }
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);
    }


    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _isShuttingDown = false;

        // Suscribirse a cambios de escena para auto-limpieza
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (_instance == this)
        {
            _instance = null;
            // FIX (Agosto 2026): antes esto ponía _isShuttingDown = true también aquí. Pero
            // _isShuttingDown solo se resetea a false en Awake() — y si _isShuttingDown es true,
            // Instance devuelve _instance (ya null) SIN crear uno nuevo. Resultado: si este
            // singleton (DontDestroyOnLoad) se destruía por CUALQUIER motivo que no fuera un
            // cierre real de la aplicación (un bug en otro sitio, un caso límite de recarga de
            // escena en modo testeo, etc.), _isShuttingDown quedaba atascado en true para el
            // resto de la sesión — nadie volvía a poder resetearlo porque Awake() nunca se
            // volvía a ejecutar (nada crea una instancia nueva mientras el flag esté activo).
            // A partir de ahí, PlayerLockService.Instance devolvía null en silencio (sin logs, sin
            // errores — cada `lockService?.Acquire(...)` de cada trigger del juego se convertía en
            // un no-op) y NINGÚN freeze de jugador volvía a funcionar en lo que quedaba de partida.
            // Esto es lo que estaba pasando: KingdomBoundaryTrigger llamaba a
            // AcquireBridgeUntilCinematic() correctamente, pero Instance ya devolvía null, así que
            // el freeze nunca llegaba a intentarse. Ahora _isShuttingDown solo se marca en
            // OnApplicationQuit() (cierre real), que es el único caso que el comentario de más
            // arriba (evitar el warning "Some objects were not cleaned up") necesitaba cubrir.
            // Así, si el singleton se destruye por cualquier otro motivo, Instance puede
            // recrearlo la próxima vez que se necesite en vez de quedar inutilizado para siempre.
        }
    }

    void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }
    
    /// <summary>
    /// Al cargar una nueva escena, limpiar locks huérfanos (de objetos destruidos).
    /// Esto previene que el player quede bloqueado en escenas de testeo.
    /// </summary>
    void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // Solo limpiar en carga normal (no aditiva)
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
        {
            Debug.Log($"[PlayerLockService] 🔍 Escena cargada '{scene.name}' - Verificando locks...");
            
            // Verificar si hay owners destruidos/huérfanos
            var deadOwners = new List<object>();
            foreach (var owner in _owners)
            {
                // Si el owner es un MonoBehaviour/GameObject destruido, marcarlo
                if (owner is UnityEngine.Object unityObj && unityObj == null)
                {
                    deadOwners.Add(owner);
                }
            }
            
            if (deadOwners.Count > 0)
            {
                Debug.LogWarning($"[PlayerLockService] 🧹 Limpiando {deadOwners.Count} locks huérfanos al cargar escena '{scene.name}'");
                foreach (var dead in deadOwners)
                {
                    _owners.Remove(dead);
                }
            }
            
            // NUEVO: En modo testeo o cuando no hay cinemáticas aditivas, limpiar todos los locks
            // Esto previene que el player quede bloqueado cuando se skipean cinemáticas en grafos narrativos
            bool isTestingMode = GameBootService.IsAvailable && 
                                 GameBootService.Profile != null && 
                                 GameBootService.Profile.ShouldBootFromPreset();
            
            if (isTestingMode && _owners.Count > 0)
            {
                Debug.LogWarning($"[PlayerLockService] 🧪 Modo testeo detectado - Limpiando {_owners.Count} locks al cargar escena '{scene.name}'");
                _owners.Clear();
            }
            
            // Si ya no quedan locks, liberar el player
            if (_owners.Count == 0)
            {
                Debug.Log("[PlayerLockService] ✅ Todos los locks limpiados - Reactivando movimiento del jugador");
                ReleaseHardLock();
            }
            else if (_owners.Count > 0)
            {
                Debug.Log($"[PlayerLockService] ⚠️ {_owners.Count} locks aún activos tras limpieza");
            }
        }
    }
}
