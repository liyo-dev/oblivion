using UnityEngine;
using UnityEngine.InputSystem;
using Core;
using System.Collections;
using Invector.vCharacterController;

public class SleepTrigger : MonoBehaviour
{
    [Header("Referencia al jugador")]
    public GameObject player;
    [Header("Nombre del estado de animación de dormir")]
    public string sleepAnimationState = "Sleeping_NoWeapon";
    [Header("Anchor de posición en la cama")]
    public Transform bedPosition;

    [Header("Cámara cenital")]
    [Tooltip("Posición y rotación que adoptará la cámara mientras duerme. Déjalo vacío para no mover la cámara.")]
    public Transform sleepCameraAnchor;
    [Tooltip("Velocidad de transición de la cámara al dormir/despertar.")]
    public float cameraTransitionSpeed = 2f;

    [Header("Despertar")]
    [Tooltip("Posición en el suelo donde Will se coloca al despertar (pie de la cama). Sin esto, permanece en bedPosition.")]
    public Transform wakeUpPosition;
    [Tooltip("Rotación de referencia para la cámara al despertar (solo se usa euler.y → horizontal, euler.x → vertical). Evita que la cámara aparezca detrás de una pared.")]
    public Transform wakeUpCameraAnchor;

    [Header("Narrativa")]
    [Tooltip("Si true, Will empieza dormido en esta cama al arrancar la escena sin necesidad de entrar al trigger.")]
    public bool sleepOnStart = false;
    [Tooltip("Evento que se dispara al despertar. Compatible con WaitCustomEventNode.")]
    public string wakeNarrativeEvent = "";

    private bool isSleeping = false;
    private float _sleepStartTime = -999f;

    private Animator playerAnimator;
    private PlayerActionManager playerActionManager;

    private vThirdPersonCamera _tpsCamera;
    private Camera _mainCamera;
    private bool _wasCameraLocked;
    private Coroutine _cameraCoroutine;

    void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
    }

    void Start()
    {
        if (!sleepOnStart) return;
        StartCoroutine(ForceSleepNextFrame());
    }

    void LateUpdate()
    {
        if (!isSleeping) return;

        if (bedPosition != null && player != null)
        {
            player.transform.position = bedPosition.position;
            player.transform.rotation = bedPosition.rotation;
        }

        if (sleepCameraAnchor != null && _mainCamera != null && _cameraCoroutine == null)
        {
            _mainCamera.transform.position = sleepCameraAnchor.position;
            _mainCamera.transform.rotation = sleepCameraAnchor.rotation;
        }

        // Igual que PlayerAmbientActivityHandler.LateUpdate(): el motor corre con lockMovement=true,
        // pero con el CC desactivado podría registrar isGrounded=false → animación de caída.
        if (playerAnimator != null)
        {
            try { playerAnimator.SetBool(vAnimatorParameters.IsGrounded, true); } catch { }
            try { playerAnimator.SetFloat(vAnimatorParameters.GroundDistance, 0f); } catch { }
        }
    }

    IEnumerator ForceSleepNextFrame()
    {
        yield return null;
        var playerGO = player != null ? player : PlayerService.Player;
        if (playerGO != null) ForceSleep(playerGO);
    }

    /// <summary>Pone a Will a dormir desde código (ej: llamado por el grafo narrativo o sleepOnStart).</summary>
    public void ForceSleep(GameObject playerGO)
    {
        player = playerGO;
        SetupSleep(playerGO);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy) return;
        if (isSleeping) return;
        if (!other.CompareTag("Player")) return;

        var playerGO = other.GetComponentInParent<CharacterController>()?.gameObject ?? other.gameObject;
        player = playerGO;
        SetupSleep(playerGO);
    }

    void SetupSleep(GameObject playerGO)
    {
        playerAnimator      = playerGO.GetComponent<Animator>();
        playerActionManager = playerGO.GetComponent<PlayerActionManager>();

        // PushMode PRIMERO: PlayerLockService capturará CC=true y lockMovement=false como estado previo,
        // y los bloqueará. Si hacemos push después de bloquearlos manualmente, capturaría el estado
        // ya bloqueado y al despertar los "restauraría" en estado bloqueado.
        playerActionManager?.PushMode(ActionMode.Cinematic);

        // Snap a la posición de cama (CC ya desactivado por PlayerLockService vía PushMode)
        if (bedPosition != null)
        {
            playerGO.transform.position = bedPosition.position;
            playerGO.transform.rotation = bedPosition.rotation;
        }

        if (playerAnimator != null) playerAnimator.Play(sleepAnimationState);

        MoveCameraToSleepAnchor();

        _sleepStartTime = Time.time;
        isSleeping = true;
    }

    void WakeUp()
    {
        if (!isSleeping) return;
        isSleeping = false;

        // Forzar ángulo de cámara antes de re-habilitarla (evita que aparezca detrás de la pared)
        if (wakeUpCameraAnchor != null && _tpsCamera != null)
        {
            var a = wakeUpCameraAnchor.eulerAngles;
            _tpsCamera.SetAngles(a.y, a.x);
        }
        RestoreCamera();

        // Teleportar al suelo (CC todavía desactivado — PlayerLockService lo gestiona)
        if (wakeUpPosition != null)
        {
            player.transform.position = wakeUpPosition.position;
            player.transform.rotation = wakeUpPosition.rotation;
        }

        // PopMode → PlayerLockService.Release → ReleaseHardLock:
        // restaura CC=true, lockMovement=false, SuppressMoveInput=false, PopUIMode, IgnoreJumpButton
        playerActionManager?.PopMode(ActionMode.Cinematic);

        // El motor corre con lockMovement=false → CrossFade funciona correctamente
        if (playerAnimator != null)
            playerAnimator.CrossFadeInFixedTime("Free Locomotion", 0.2f, 0);

        TutorialPromptUI.Instance?.Hide();

        if (!string.IsNullOrEmpty(wakeNarrativeEvent))
            DefaultNarrativeSignals.Instance?.RaiseCustom(wakeNarrativeEvent, name);
    }

    void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!isSleeping) return;
        if (input.Phase != InputActionPhase.Performed) return;
        // Aceptar Interact (GamePlay map) y Submit (fallback hardware que siempre emite aunque
        // GamePlay esté deshabilitado, p.ej. en modo Cinematic).
        bool isWakeInput = input.Type == GamepadInputReader.InputEventType.Interact
                        || input.Type == GamepadInputReader.InputEventType.Submit;
        if (!isWakeInput) return;
        // Grace period: ignorar input del primer segundo para evitar despertar inmediato al cargar escena
        if (Time.time - _sleepStartTime < 1f) return;

        WakeUp();
    }

    // --- Cámara ---

    void InitCameraIfNeeded()
    {
        if (_tpsCamera != null) return;
        _tpsCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (_tpsCamera != null)
            _mainCamera = _tpsCamera.GetComponent<Camera>();
    }

    void MoveCameraToSleepAnchor()
    {
        if (sleepCameraAnchor == null) return;
        InitCameraIfNeeded();
        if (_tpsCamera == null || _mainCamera == null) return;

        _wasCameraLocked = _tpsCamera.lockCamera;
        // Deshabilitar el componente completo detiene su LateUpdate, que de lo contrario
        // sobreescribiría la posición de cámara que establece el coroutine cada frame.
        _tpsCamera.enabled = false;

        if (_cameraCoroutine != null) StopCoroutine(_cameraCoroutine);
        _cameraCoroutine = StartCoroutine(TransitionCamera(
            _mainCamera.transform.position, _mainCamera.transform.rotation,
            sleepCameraAnchor.position,     sleepCameraAnchor.rotation));
    }

    void RestoreCamera()
    {
        if (_tpsCamera == null) return;
        _tpsCamera.lockCamera = _wasCameraLocked;
        _tpsCamera.enabled = true;
        if (_cameraCoroutine != null) { StopCoroutine(_cameraCoroutine); _cameraCoroutine = null; }
    }

    IEnumerator TransitionCamera(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
    {
        float elapsed = 0f;
        float duration = 1f / Mathf.Max(0.01f, cameraTransitionSpeed);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _mainCamera.transform.position = Vector3.Lerp(fromPos, toPos, t);
            _mainCamera.transform.rotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        _mainCamera.transform.position = toPos;
        _mainCamera.transform.rotation = toRot;
        _cameraCoroutine = null;
    }
}
