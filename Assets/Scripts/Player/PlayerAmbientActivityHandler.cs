using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Permite al jugador usar NPCWorldPoints (bancos, mesas, camas...) igual que los NPCs.
/// Se coloca en el prefab del player. El Interactable del objeto llama StartActivity().
/// </summary>
[DisallowMultipleComponent]
public class PlayerAmbientActivityHandler : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Acción de cancelar (B / Circle) para que el player abandone la actividad.")]
    [SerializeField] private InputActionReference cancelAction;

    private Animator _animator;
    private PlayerActionManager _actionManager;
    private PlayerFlyingController _flyingController;
    private CharacterController _cc;
    private Rigidbody _rb;
    private Invector.vCharacterController.vThirdPersonMotor _motor;
    private Invector.vCharacterController.vThirdPersonInput _invectorInput;
    private bool _ccWasEnabled;
    private bool _rbWasKinematic;
    private bool _motorWasLocked;
    private bool _suppressWasActive;
    private Vector3 _preSnapPosition;
    private NPCWorldPoint _currentWorldPoint;
    private Coroutine _activityCoroutine;
    private Dictionary<string, float> _clipLengthCache = new();

    // Hash cacheado para no usar StringToHash en runtime
    private static readonly int _groundDistanceHash = Animator.StringToHash("GroundDistance");

    void Awake()
    {
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _actionManager = GetComponent<PlayerActionManager>();
        _flyingController = GetComponent<PlayerFlyingController>();
        _cc = GetComponent<CharacterController>() ?? GetComponentInChildren<CharacterController>();
        _rb = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();
        _motor = GetComponent<Invector.vCharacterController.vThirdPersonMotor>();
        _invectorInput = GetComponent<Invector.vCharacterController.vThirdPersonInput>();
    }

    void OnEnable()
    {
        if (cancelAction?.action != null)
            cancelAction.action.performed += OnCancel;
    }

    void OnDisable()
    {
        if (cancelAction?.action != null)
            cancelAction.action.performed -= OnCancel;

        // Seguridad: si se desactiva mientras hay actividad, limpiar estado
        if (_currentWorldPoint != null)
            ForceRelease();
    }

    // Invector sobreescribe IsGrounded/GroundDistance en Update() cada frame.
    // LateUpdate se ejecuta después y restaura los valores correctos mientras hay actividad.
    void LateUpdate()
    {
        if (_currentWorldPoint == null || _animator == null) return;

        try
        {
            _animator.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, true);
            _animator.SetFloat(_groundDistanceHash, 0f);
        }
        catch { }

        // Evitar que el Rigidbody caiga mientras el motor de Invector cree que está en el aire.
        if (_rb != null && !_rb.isKinematic && _rb.linearVelocity.y < 0f)
        {
            var v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;
        }
    }

    // ── API pública ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia la actividad del WorldPoint. El Interactable ya llamó TryOccupy() antes de llamar aquí.
    /// </summary>
    public void StartActivity(NPCWorldPoint worldPoint)
    {
        if (_currentWorldPoint != null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[PlayerAmbientActivityHandler] Ya hay una actividad activa, ignorando nueva petición.");
#endif
            return;
        }

        _currentWorldPoint = worldPoint;

        // Cancelar cualquier "doble salto armado" para que no dispare vuelo al levantarse
        _flyingController?.CancelFlightArming();

        // Bloquear movimiento, salto, ataques, etc.
        _actionManager?.PushMode(ActionMode.UsingWorldPoint);

        if (worldPoint.overrideFacing)
            transform.rotation = worldPoint.InteractionRotation;

        // Snap al interactionPoint y desactivar CC durante toda la actividad
        // (si se re-activa antes de levantarse, Invector detecta "en el aire" y activa caída)
        SnapToSeat(worldPoint);

        // Adjuntar prop a la mano (si el WorldPoint tiene uno configurado)
        worldPoint.AttachPropToOccupant(_animator);

        // Forzar IsGrounded=true para que el animator no entre en falling mientras el CC está off
        if (_animator != null)
        {
            try { _animator.SetBool(Invector.vCharacterController.vAnimatorParameters.IsGrounded, true); } catch { }
            try { _animator.SetFloat(Invector.vCharacterController.vAnimatorParameters.InputMagnitude, 0f); } catch { }
        }

        if (_activityCoroutine != null) StopCoroutine(_activityCoroutine);
        _activityCoroutine = StartCoroutine(ActivityRoutine(worldPoint.activityType));
    }

    /// <summary>
    /// El jugador abandona la actividad (vía botón B o interrupción externa).
    /// </summary>
    public void StopActivity()
    {
        if (_currentWorldPoint == null) return;

        if (_activityCoroutine != null)
        {
            StopCoroutine(_activityCoroutine);
            _activityCoroutine = null;
        }

        var leaving = _currentWorldPoint;
        _currentWorldPoint = null;

        leaving.Release(transform);
        leaving.DetachProp();

        string exitState = GetActivityExitState(leaving.activityType);
        if (!string.IsNullOrEmpty(exitState) && HasState(exitState))
            StartCoroutine(PlayExitAndUnlock(exitState));
        else
            StartCoroutine(ReturnToGroundAndUnlock());
    }

    // ── Internals ────────────────────────────────────────────────────────────────

    private void SnapToSeat(NPCWorldPoint worldPoint)
    {
        // Guardar posición de pie para poder volver ahí al levantarse
        _preSnapPosition = transform.position;

        _ccWasEnabled = _cc != null && _cc.enabled;
        if (_ccWasEnabled) _cc.enabled = false;

        if (_rb != null)
        {
            _rbWasKinematic = _rb.isKinematic;
            _rb.isKinematic = true;
            _rb.linearVelocity = Vector3.zero;
        }

        // Bloquear el motor de Invector directamente, sin cambiar el mapa de input.
        // Así la cámara sigue disponible mientras el player está sentado o durmiendo.
        if (_motor != null)
        {
            _motorWasLocked = _motor.lockMovement;
            _motor.lockMovement = true;
        }
        if (_invectorInput != null)
        {
            _suppressWasActive = _invectorInput.SuppressMoveInput;
            _invectorInput.SuppressMoveInput = true;
            _invectorInput.MoveInput();
        }

        // Posición exacta del GO vacío definido en el inspector: sin ajuste de altura por raycast
        transform.position = worldPoint.InteractionPosition;
    }

    private void RestoreCC()
    {
        if (_invectorInput != null)
            _invectorInput.SuppressMoveInput = _suppressWasActive;

        if (_motor != null)
            _motor.lockMovement = _motorWasLocked;

        if (_ccWasEnabled && _cc != null)
        {
            _cc.enabled = true;
            _ccWasEnabled = false;
        }

        if (_rb != null)
        {
            _rb.isKinematic = _rbWasKinematic;
            _rbWasKinematic = false;
        }

        // Grace period para evitar que el salto se dispare en el frame de restauración
        // (mismo mecanismo que PlayerLockService.ReleaseHardLock)
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);
    }

    private void OnCancel(InputAction.CallbackContext ctx) => StopActivity();

    private IEnumerator ActivityRoutine(NPCAmbientActivity activity)
    {
        string beginState = GetActivityBeginState(activity);
        string loopState  = GetActivityLoopState(activity);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[PlayerAmbientActivityHandler] Actividad: {activity} | Begin='{beginState}' (existe={HasState(beginState)}) | Loop='{loopState}' (existe={HasState(loopState)}) | Animator={_animator?.name}");
        if (_animator != null)
        {
            var sb = new System.Text.StringBuilder("[PlayerAmbientActivityHandler] Estados layer 0 disponibles: ");
            for (int i = 0; i < _animator.layerCount; i++)
            {
                var info = _animator.GetCurrentAnimatorStateInfo(i);
                sb.Append($"Layer{i}={info.shortNameHash} ");
            }
            Debug.Log(sb.ToString());
        }
#endif

        if (!string.IsNullOrEmpty(beginState) && HasState(beginState))
        {
            CrossFade(beginState, 0.2f);
            yield return null;
            yield return new WaitForSeconds(Mathf.Max(0.1f, GetClipLength(beginState)));
        }

        if (!string.IsNullOrEmpty(loopState) && HasState(loopState))
            CrossFade(loopState, 0.15f);

        _activityCoroutine = null;
    }

    private IEnumerator PlayExitAndUnlock(string exitState)
    {
        CrossFade(exitState, 0.2f);
        yield return null;
        yield return new WaitForSeconds(Mathf.Max(0.1f, GetClipLength(exitState)));
        yield return ReturnToGroundAndUnlock();
    }

    // Vuelve suavemente a la posición de pie para que la cámara no dé un golpe seco
    // al teleportar a Will de la altura del asiento al suelo.
    private IEnumerator ReturnToGroundAndUnlock()
    {
        const float duration = 0.12f;
        var startPos = transform.position;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(startPos, _preSnapPosition, t / duration);
            yield return null;
        }
        transform.position = _preSnapPosition;
        RestoreCC();
        _actionManager?.PopMode(ActionMode.UsingWorldPoint);
    }

    private void ForceRelease()
    {
        if (_activityCoroutine != null) { StopCoroutine(_activityCoroutine); _activityCoroutine = null; }
        _currentWorldPoint?.Release(transform);
        _currentWorldPoint?.DetachProp();
        _currentWorldPoint = null;
        transform.position = _preSnapPosition;
        RestoreCC();
        _actionManager?.PopMode(ActionMode.UsingWorldPoint);
    }

    private void CrossFade(string stateName, float duration)
    {
        if (_animator == null) return;
        _animator.CrossFadeInFixedTime(Animator.StringToHash(stateName), duration, 0);
    }

    private bool HasState(string stateName)
        => _animator != null && _animator.HasState(0, Animator.StringToHash(stateName));

    private float GetClipLength(string stateName)
    {
        if (_clipLengthCache.TryGetValue(stateName, out float cached)) return cached;
        if (_animator?.runtimeAnimatorController != null)
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains(stateName))
                {
                    _clipLengthCache[stateName] = clip.length;
                    return clip.length;
                }
            }
        }
        return 1f;
    }

    // ── Mapeo actividad → estados del animator ───────────────────────────────────
    // Los nombres son idénticos a los usados en NPCSimpleAnimator.

    private static string GetActivityBeginState(NPCAmbientActivity a) => a switch
    {
        // Sentarse — pendiente de importar los clips (nombres reservados)
        NPCAmbientActivity.SitGround  => "SitGround_Begin",
        NPCAmbientActivity.SitLow     => "SitLow_Begin",
        NPCAmbientActivity.SitMedium  => "SitMedium_Begin",
        NPCAmbientActivity.SitHigh    => "SitHigh_Begin",
        // Comer — pendiente de importar los clips
        NPCAmbientActivity.Eat        => "Eat_Begin",
        // Beber: usa DrinkPotion_NoWeapon como one-shot
        NPCAmbientActivity.Drink      => "DrinkPotion_NoWeapon",
        // Dormir: sin transición de entrada, va directo al loop
        NPCAmbientActivity.Sleep      => string.Empty,
        _                             => string.Empty
    };

    private static string GetActivityLoopState(NPCAmbientActivity a) => a switch
    {
        // Sentarse — pendiente de importar los clips
        NPCAmbientActivity.SitGround  => "SitGround_Loop",
        NPCAmbientActivity.SitLow     => "SitLow_Loop",
        NPCAmbientActivity.SitMedium  => "SitMedium_Loop",
        NPCAmbientActivity.SitHigh    => "SitHigh_Loop",
        // Comer — pendiente
        NPCAmbientActivity.Eat        => "Eat_Loop",
        // Beber: sin loop, el one-shot es la animación completa
        NPCAmbientActivity.Drink      => string.Empty,
        // Dormir: loop real del personaje durmiendo
        NPCAmbientActivity.Sleep      => "Sleeping_NoWeapon",
        _                             => string.Empty
    };

    private static string GetActivityExitState(NPCAmbientActivity a) => a switch
    {
        // Sentarse — pendiente de importar los clips
        NPCAmbientActivity.SitGround  => "SitGround_Exit",
        NPCAmbientActivity.SitLow     => "SitLow_Exit",
        NPCAmbientActivity.SitMedium  => "SitMedium_Exit",
        NPCAmbientActivity.SitHigh    => "SitHigh_Exit",
        // Dormir, beber y comer: sin animación de salida, vuelven directo a idle
        _                             => string.Empty
    };
}
