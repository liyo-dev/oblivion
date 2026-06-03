using UnityEngine;
using UnityEngine.SceneManagement;
using Invector.vCharacterController;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    public string targetAnchorId;
    [Tooltip("(Opcional) Nombre de la escena destino. Si está vacío, el portal teletransporta dentro de la misma escena.")]
    public string targetSceneName;
    [Tooltip("(Opcional) Nombre de la escena overlay que muestra la pantalla de carga. Si se asigna, se usará al cargar la escena destino.")]
    public string loadingOverlayScene;
    public string requiredFlag;
    public string setFlagOnEnter;

    [Tooltip("Desactivar gravedad durante el teletransporte (útil para escaleras/desniveles). Dejar en false para puertas normales.")]
    public bool disableGravityDuringTeleport = false;

    [Header("Condición de ítem (opcional)")]
    [Tooltip("Si se asigna, el portal solo se activa si el jugador tiene este ítem en el inventario.")]
    public ItemData requiredItem;
    [Min(1)]
    public int requiredItemAmount = 1;
    [Tooltip("Si está activo, el ítem se consume al entrar al portal.")]
    public bool consumeItemOnEnter = false;

    private bool _pendingUse;
    private bool _waitingForSceneLoad;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(PortalTrigger));
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_pendingUse)
        {
            var player = PlayerService.Player;
            if (player != null)
            {
                ProcessPortal(player);
            }
            _pendingUse = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (GameBootService.IsAvailable)
        {
            ProcessPortal(other.gameObject);
        }
        else
        {
            // Diferir hasta que el GameBootProfile esté listo
            _pendingUse = true;
        }
    }

    private void ProcessPortal(GameObject player)
    {
        if (string.IsNullOrEmpty(targetAnchorId))
        {
            Debug.LogWarning("[PortalTrigger] targetAnchorId vacío");
            return;
        }

        var bootProfile = GameBootService.Profile;
        if (bootProfile == null)
        {
            Debug.LogError("[PortalTrigger] GameBootProfile no disponible en GameBootService");
            return;
        }

        var preset = bootProfile.GetActivePresetResolved();
        if (preset == null)
        {
            Debug.LogError("[PortalTrigger] No hay preset activo");
            return;
        }

        // Verificar flag requerida
        if (!string.IsNullOrEmpty(requiredFlag))
        {
            if (preset.flags == null || !preset.flags.Contains(requiredFlag))
            {
                Debug.Log($"[PortalTrigger] Flag requerida '{requiredFlag}' no encontrada. Portal bloqueado.");
                return;
            }
        }

        // Verificar ítem requerido
        if (requiredItem != null)
        {
            if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
            {
                Debug.LogWarning("[PortalTrigger] Inventario no encontrado en el jugador. Portal bloqueado.");
                return;
            }

            if (!inventory.HasItem(requiredItem, requiredItemAmount))
            {
                Debug.Log($"[PortalTrigger] Ítem requerido '{requiredItem.itemId}' (x{requiredItemAmount}) no encontrado. Portal bloqueado.");
                return;
            }

            if (consumeItemOnEnter)
            {
                if (!inventory.TryConsume(requiredItem, requiredItemAmount))
                    Debug.LogWarning($"[PortalTrigger] No se pudo consumir '{requiredItem.itemId}' pese a que el conteo era suficiente.");
            }
        }

        // Establecer flag al entrar
        if (!string.IsNullOrEmpty(setFlagOnEnter))
        {
            if (preset.flags == null)
                preset.flags = new System.Collections.Generic.List<string>();
            
            if (!preset.flags.Contains(setFlagOnEnter))
            {
                preset.flags.Add(setFlagOnEnter);
                Debug.Log($"[PortalTrigger] Flag '{setFlagOnEnter}' establecida");
            }
        }

        // 1) Congelar movimiento inmediatamente
        FreezePlayerMovement(player);

        // 2) Suscribir para restaurar al terminar el teletransporte
        System.Action onEnd = null;
        onEnd = () =>
        {
            TeleportService.OnTeleportEnded -= onEnd;
            RestorePlayerMovement(player);
        };
        TeleportService.OnTeleportEnded += onEnd;

        // 3) Si se pidió cambiar de escena, iniciar la carga y esperar a que termine
        if (!string.IsNullOrEmpty(targetSceneName) && !string.Equals(SceneManager.GetActiveScene().name, targetSceneName, System.StringComparison.OrdinalIgnoreCase))
        {
            if (_waitingForSceneLoad)
            {
                Debug.LogWarning("[PortalTrigger] Ya esperando carga de escena. Ignorando trigger adicional.");
                return;
            }

            _waitingForSceneLoad = true;

            // Establecer el anchor deseado antes de cargar para persistir en runtimePreset
            SpawnManager.SetCurrentAnchor(targetAnchorId);

            // Suscribir al evento sceneLoaded para ejecutar el teleport cuando la escena esté activa
            void OnSceneLoaded(Scene sc, LoadSceneMode mode)
            {
                if (!string.Equals(sc.name, targetSceneName, System.StringComparison.OrdinalIgnoreCase))
                    return;

                SceneManager.sceneLoaded -= OnSceneLoaded;
                _waitingForSceneLoad = false;

                // Asegurar que el anchor actual está establecido
                SpawnManager.SetCurrentAnchor(targetAnchorId);

                // Esperar un frame para que la nueva escena inicialice sus objetos, luego teletransportar
                var runner = new GameObject("_PortalTriggerTeleportRunner").AddComponent<PortalTriggerTeleportRunner>();
                runner.Init(targetAnchorId);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;

            // Iniciar carga de la escena con overlay de carga si es posible
            var overlayScene = !string.IsNullOrEmpty(loadingOverlayScene)
                ? loadingOverlayScene
                : (!string.IsNullOrEmpty(SceneTransitionLoader.DefaultOverlayScene)
                    ? SceneTransitionLoader.DefaultOverlayScene
                    : "LoadingScreen");

            SceneTransitionLoader.LoadWithOverlay(targetSceneName, overlayScene);
        }
        else
        {
            // Mismo escena: teletransportar inmediatamente
            SpawnManager.SetCurrentAnchor(targetAnchorId);
            SpawnManager.TeleportTo(targetAnchorId, true);
        }

        // NO guardar aquí
    }

    // Small helper MonoBehaviour to delay one frame then teleport (so scene has chance to init)
    class PortalTriggerTeleportRunner : MonoBehaviour
    {
        string _anchorId;
        public void Init(string anchorId) { _anchorId = anchorId; DontDestroyOnLoad(gameObject); StartCoroutine(Run()); }
        System.Collections.IEnumerator Run()
        {
            yield return null; // wait a frame
            SpawnManager.TeleportTo(_anchorId, true);
            Destroy(gameObject);
        }
    }

    // ---- Helpers de freeze/restore ----
    private void FreezePlayerMovement(GameObject player)
    {
        if (!player) return;

        // Forzar animación a Idle antes de congelar
        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator)
        {
            animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, 0f);
            animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputHorizontal, 0f);
            animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputVertical, 0f);
            animator.SetBool(Invector.vCharacterController.vAnimatorParameters.IsSprinting, false);
        }

        var input = player.GetComponent<vThirdPersonInput>() ?? player.GetComponentInChildren<vThirdPersonInput>(true);
        if (input) input.enabled = false;

        var agent = player.GetComponent<NavMeshAgent>() ?? player.GetComponentInChildren<NavMeshAgent>(true);
        if (agent) agent.isStopped = true;

        var rb = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>(true);
        if (rb)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            if (disableGravityDuringTeleport)
                rb.useGravity = false; // Solo desactivar gravedad si está configurado
        }

        var cc = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        if (cc) cc.enabled = false; // evita empujes durante el lapso hasta el cut
    }

    private void RestorePlayerMovement(GameObject player)
    {
        if (!player) return;
        // Forzar posición exacta tras teleport
        var cc = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        if (cc) {
            cc.enabled = false;
            cc.transform.position = player.transform.position;
        }

        // Resetear variables de movimiento en vThirdPersonController si existen
        var controller = player.GetComponent<vThirdPersonController>() ?? player.GetComponentInChildren<vThirdPersonController>(true);
        if (controller != null)
        {
            var type = controller.GetType();
            var moveDirField = type.GetField("moveDirection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (moveDirField != null) moveDirField.SetValue(controller, Vector3.zero);
            var extraImpulseField = type.GetField("extraImpulse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (extraImpulseField != null) extraImpulseField.SetValue(controller, Vector3.zero);
        }

        // Resetear input y valores internos en vThirdPersonInput
        var input = player.GetComponent<vThirdPersonInput>() ?? player.GetComponentInChildren<vThirdPersonInput>(true);
        if (input != null)
        {
            // Deshabilitar y habilitar el InputActionAsset
            var inputActionsField = input.GetType().GetField("inputActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inputActions = inputActionsField?.GetValue(input) as UnityEngine.InputSystem.InputActionAsset;
            input.enabled = false;
            input.enabled = true;
            if (inputActions != null)
            {
                inputActions.Disable();
                inputActions.Enable();
            }
            // Resetear campos privados relevantes
            var moveInputField = input.GetType().GetField("moveInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (moveInputField != null) moveInputField.SetValue(input, Vector2.zero);
            var cameraInputField = input.GetType().GetField("cameraInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cameraInputField != null) cameraInputField.SetValue(input, Vector2.zero);
            var jumpPressedField = input.GetType().GetField("jumpPressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (jumpPressedField != null) jumpPressedField.SetValue(input, false);
            var sprintHeldField = input.GetType().GetField("sprintHeld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (sprintHeldField != null) sprintHeldField.SetValue(input, false);
            var strafePressedField = input.GetType().GetField("strafePressed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (strafePressedField != null) strafePressedField.SetValue(input, false);
        }

        // Esperar un frame antes de reactivar movimiento
        StartCoroutine(RestoreMovementCoroutine(player, cc));
    }

    private System.Collections.IEnumerator RestoreMovementCoroutine(GameObject player, CharacterController cc)
    {
        yield return null; // esperar un frame

        // Primero reactivar CharacterController para que detecte el suelo
        if (cc && !cc.enabled)
        {
            cc.enabled = true;
            // Forzar SimpleMove para que el CC actualice isGrounded
            cc.SimpleMove(Vector3.zero);
        }

        // Esperar otro frame para que el CharacterController procese colisiones
        yield return null;

        var rb = player.GetComponent<Rigidbody>() ?? player.GetComponentInChildren<Rigidbody>(true);
        if (rb) {
            if (disableGravityDuringTeleport)
                rb.useGravity = true;   // Solo reactivar si fue desactivada
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        var input = player.GetComponent<vThirdPersonInput>() ?? player.GetComponentInChildren<vThirdPersonInput>(true);
        if (input && !input.enabled) input.enabled = true;
        if (input) {
            input.enabled = false;
            input.enabled = true;
        }

        var agent = player.GetComponent<NavMeshAgent>() ?? player.GetComponentInChildren<NavMeshAgent>(true);
        if (agent) {
            agent.isStopped = false;
            agent.velocity = Vector3.zero;
        }

        // Resetear parámetros de animación
        var animator = player.GetComponentInChildren<Animator>(true);
        if (animator)
        {
            animator.SetFloat("InputMagnitude", 0f);
            animator.SetFloat("InputHorizontal", 0f);
            animator.SetFloat("InputVertical", 0f);
            animator.SetBool("IsSprinting", false);
        }

        Debug.Log($"[PortalTrigger] RestorePlayerMovement ejecutado. Posición: {player.transform.position}, Velocidad Rigidbody: {(rb ? rb.linearVelocity : Vector3.zero)}");
    }
}