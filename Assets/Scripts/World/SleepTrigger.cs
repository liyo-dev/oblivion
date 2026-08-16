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

    [Header("Expresión facial")]
    [Tooltip("Expresión de la cara mientras duerme. None mantiene la expresión actual.")]
    public NPCEmotion sleepEmotion = NPCEmotion.Tired;

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

    [Header("Uso único")]
    [Tooltip("Si true, este trigger solo puede activarse una vez. El flag se guarda en el preset.")]
    public bool playOnlyOnce = false;
    [Tooltip("ID único para recordar si este trigger ya se ejecutó (requerido con playOnlyOnce).")]
    public string persistenceId = "";

    private bool isSleeping = false;
    private float _sleepStartTime = -999f;

    private Animator playerAnimator;
    private PlayerActionManager playerActionManager;
    private NPCEmotionController _playerEmotion;

    private vThirdPersonInput _playerInput;

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
        if (playOnlyOnce && AlreadyPlayed()) return;
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
        if (playOnlyOnce && AlreadyPlayed()) return;

        // FIX (15 ago 2026, prioridad demo): "Will cayendo/de pie en vez de dormido" — causa real
        // confirmada con el diagnóstico de esta misma sesión (log: "CC[sin CharacterController]").
        // El fallback a other.gameObject cuando GetComponentInParent<CharacterController>() no
        // encontraba nada operaba sobre el GameObject del propio collider del trigger — casi nunca
        // la raíz real del jugador — así que todo lo que hacía SetupSleep()/WakeUp() (teleport,
        // Play() de la animación, PushMode) se aplicaba a un objeto que nadie ve, mientras el Will
        // real seguía bajo el control normal de Invector (por eso "cae": su gravedad de siempre
        // seguía activa). PlayerService.Player es la misma referencia central que ya usa
        // ForceSleepNextFrame() más abajo en este archivo — se prioriza aquí también en vez de
        // fiarse de un fallback silencioso.
        var playerGO = PlayerService.Player != null
            ? PlayerService.Player
            : other.GetComponentInParent<CharacterController>()?.gameObject;

        if (playerGO == null)
        {
            Debug.LogError($"[SleepTrigger] '{name}': no se pudo resolver el GameObject real del " +
                $"jugador (ni PlayerService.Player ni CharacterController en los padres de " +
                $"'{other.name}'). Abortando para no operar sobre el objeto equivocado.", this);
            return;
        }

        player = playerGO;
        SetupSleep(playerGO);
    }

    void SetupSleep(GameObject playerGO)
    {
        // FIX: en el rig del jugador (Invector) el Animator vive en un hijo ("model"), no en la
        // raíz — igual que _playerEmotion ya resolvía con GetComponentInChildren. Con
        // GetComponent() (solo raíz) esto devolvía null en el rig real, así que
        // `playerAnimator.Play(sleepAnimationState)` de abajo nunca llegaba a ejecutarse (el guard
        // `if (playerAnimator != null)` lo saltaba en silencio) y Will se quedaba en la pose en la
        // que estuviera (de pie) en vez de tumbarse. Antes esto quedaba tapado por el bug de AABB de
        // culling ya corregido (el personaje se veía "flotando/roto" de cualquier forma); al arreglar
        // ese bug quedó a la vista que la animación de dormir tampoco se estaba aplicando nunca.
        playerAnimator      = playerGO.GetComponentInChildren<Animator>(true);
        playerActionManager = playerGO.GetComponent<PlayerActionManager>();
        _playerEmotion      = playerGO.GetComponentInChildren<NPCEmotionController>();
        _playerInput         = playerGO.GetComponent<vThirdPersonInput>();

        // PushMode PRIMERO: PlayerLockService capturará CC=true y lockMovement=false como estado previo,
        // y los bloqueará. Si hacemos push después de bloquearlos manualmente, capturaría el estado
        // ya bloqueado y al despertar los "restauraría" en estado bloqueado.
        playerActionManager?.PushMode(ActionMode.Cinematic);

        // FIX (15 ago 2026): "se acuesta y enseguida se pone con la animación de caer" — causa real,
        // no la del intento anterior (el LateUpdate de más abajo forzando IsGrounded=true, que se
        // quedó corto). vThirdPersonInput.Update() llama cada frame a cc.UpdateMotor()/UpdateAnimator(),
        // que reescribe el parámetro IsGrounded del Animator con el resultado del ground-check propio
        // de Invector — falso, porque PlayerLockService ya desactivó el CharacterController al hacer
        // PushMode de arriba. Unity evalúa las transiciones del Animator justo después de Update() y
        // ANTES de LateUpdate(): por eso corregir IsGrounded=true en LateUpdate() (más abajo en este
        // mismo archivo) siempre llega un paso tarde — la transición hacia el estado de caída ya se
        // disparó ESE MISMO FRAME con el valor falso que Invector acaba de escribir en Update(). Único
        // arreglo real: que Invector deje de escribir el parámetro mientras se duerme. Desactivar el
        // componente entero detiene su Update() (y con él ese push erróneo); se reactiva en WakeUp().
        if (_playerInput != null) _playerInput.enabled = false;

        // Snap a la posición de cama (CC ya desactivado por PlayerLockService vía PushMode)
        if (bedPosition != null)
        {
            playerGO.transform.position = bedPosition.position;
            playerGO.transform.rotation = bedPosition.rotation;
        }

        // FIX (15 ago 2026, prioridad demo): "Will sale de pie en vez de dormido" — Play() con un
        // string NO avisa si ese estado no existe en el controller actualmente activo, se queda
        // callado y sin hacer nada (por eso no salía ninguna excepción en los logs). Encontrado por
        // datos, no adivinado: el clip "Sleeping_NoWeapon" solo existe en
        // NoWeaponStanceExtraAnim.controller — NoWeaponStance.controller (el otro candidato "sin
        // arma") no lo tiene. Si el Animator activo de Will está usando el controller que no tiene
        // el estado, esto es la causa exacta. HasState() lo confirma en el acto la próxima vez que
        // se pruebe, con el nombre real del controller puesto — no hace falta adivinar más.
        if (playerAnimator != null)
        {
            int stateHash = Animator.StringToHash(sleepAnimationState);
            if (!playerAnimator.HasState(0, stateHash))
            {
                Debug.LogError($"[SleepTrigger] El Animator Controller activo en '{playerGO.name}' " +
                    $"('{playerAnimator.runtimeAnimatorController?.name ?? "ninguno"}') NO tiene un " +
                    $"estado llamado '{sleepAnimationState}' en el layer 0 — por eso Will se queda de " +
                    $"pie en vez de tumbarse. El clip existe en NoWeaponStanceExtraAnim.controller; " +
                    $"revisa si es ese el controller que debería estar asignado aquí.", playerGO);
            }
            playerAnimator.Play(sleepAnimationState);
        }

        if (sleepEmotion != NPCEmotion.None)
            _playerEmotion?.SetEmotion(sleepEmotion);

        // FIX "personaje flotando/desencajado en la cama" (bug reportado en el prólogo — Estela/
        // Will apareciendo flotando sobre la cama en vez de Will dormido normalmente): mismo AABB
        // de culling atascado que documenta ModularAutoBuilder.RefreshRendererBoundsAfterAppearanceChange.
        // Aquí el teleport a bedPosition + el Play() forzado de la animación de dormir cambian de
        // golpe la posición Y la pose del rig, justo después de que WorldBootstrap haya podido
        // aplicar la apariencia del personaje activo — sin este refresco, el bounds de culling de
        // cada SkinnedMeshRenderer puede quedarse calculado con la pose/posición ANTERIOR hasta que
        // algo más lo fuerce, dando la sensación de que el personaje "vuela" sobre la cama.
        RefreshPlayerRendererBounds(playerGO);

        MoveCameraToSleepAnchor();

        _sleepStartTime = Time.time;
        isSleeping = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // DIAGNÓSTICO TEMPORAL (15 ago 2026) — bug "Will cayendo encima de la cama": ninguna de las
        // pruebas hechas hasta ahora dejó una excepción asociada en el log, así que es un glitch
        // visual silencioso (no hay nada que ya quede registrado para diagnosticarlo con evidencia
        // real). Esto vuelca posición Y / estado de grounded frame a frame justo después de
        // SetupSleep(), para que la PRÓXIMA repro sí deje rastro en el log. Quitar en cuanto se
        // confirme la causa — no debe quedarse en el proyecto a largo plazo.
        StartCoroutine(DiagnosticLogSleepFrames(playerGO));
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private IEnumerator DiagnosticLogSleepFrames(GameObject playerGO)
    {
        var cc = playerGO.GetComponent<CharacterController>();
        for (int i = 0; i < 20; i++)
        {
            string ccState = cc != null ? $"enabled={cc.enabled} isGrounded={(cc.enabled ? cc.isGrounded.ToString() : "n/a (disabled)")}" : "sin CharacterController";
            string animGrounded = playerAnimator != null
                ? playerAnimator.GetBool(vAnimatorParameters.IsGrounded).ToString()
                : "sin animator";
            Debug.Log($"[SleepTrigger:DIAG] frame={i} y={playerGO.transform.position.y:F3} CC[{ccState}] anim.IsGrounded={animGrounded}");
            yield return null;
        }
    }
#endif

    /// <summary>
    /// Fuerza el recálculo del AABB de culling de cada SkinnedMeshRenderer del rig del jugador —
    /// mismo patrón que ModularAutoBuilder.RefreshRendererBoundsAfterAppearanceChange() y los otros
    /// call sites documentados en ActiveCharacterSwapper. Se llama tras cualquier teleport+cambio
    /// de pose brusco de este trigger (SetupSleep/WakeUp) para evitar que el personaje se vea
    /// "flotando" un instante hasta que algo más refresque sus bounds.
    /// </summary>
    private static void RefreshPlayerRendererBounds(GameObject playerGO)
    {
        if (playerGO == null) return;
        var animator = playerGO.GetComponentInChildren<Animator>(true);
        if (animator != null) animator.Update(0f);

        foreach (var smr in playerGO.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr.updateWhenOffscreen) smr.updateWhenOffscreen = true;
            if (smr.gameObject.activeInHierarchy) _ = smr.bounds;
        }
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

        // Mismo fix que en SetupSleep(): otro teleport brusco del rig, misma necesidad de refrescar
        // el bounds de culling después.
        RefreshPlayerRendererBounds(player);

        _playerEmotion?.ForceReset();

        // PopMode → PlayerLockService.Release → ReleaseHardLock:
        // restaura CC=true, lockMovement=false, SuppressMoveInput=false, PopUIMode, IgnoreJumpButton
        playerActionManager?.PopMode(ActionMode.Cinematic);

        // Reactivar vThirdPersonInput (desactivado en SetupSleep) DESPUÉS de que PopMode ya haya
        // restaurado el CharacterController — así su primer Update() con ground-check real encuentra
        // el CC ya activo, en vez de un frame intermedio con el CC todavía desactivado.
        if (_playerInput != null) _playerInput.enabled = true;

        // El motor corre con lockMovement=false → CrossFade funciona correctamente
        if (playerAnimator != null)
            playerAnimator.CrossFadeInFixedTime("Free Locomotion", 0.2f, 0);

        TutorialPromptUI.Instance?.Hide();

        if (!string.IsNullOrEmpty(wakeNarrativeEvent))
            DefaultNarrativeSignals.Instance?.RaiseCustom(wakeNarrativeEvent, name);

        if (playOnlyOnce)
            MarkAsPlayed();
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

    // --- Uso único ---

    private string FlagKey() => $"SLEEP_DONE:{persistenceId}";

    private bool AlreadyPlayed()
    {
        if (string.IsNullOrEmpty(persistenceId))
        {
            Debug.LogWarning($"[SleepTrigger] '{name}' tiene playOnlyOnce=true pero persistenceId está vacío. El trigger no se desactivará.", this);
            return false;
        }
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        return preset?.flags != null && preset.flags.Contains(FlagKey());
    }

    private void MarkAsPlayed()
    {
        if (string.IsNullOrEmpty(persistenceId)) return;
        var preset = GameBootService.Profile?.GetActivePresetResolved();
        if (preset == null) return;
        preset.flags ??= new System.Collections.Generic.List<string>();
        if (!preset.flags.Contains(FlagKey()))
            preset.flags.Add(FlagKey());
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
