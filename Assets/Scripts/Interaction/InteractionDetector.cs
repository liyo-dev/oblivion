using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class InteractionDetector : MonoBehaviour
{
    [Header("Detección")]
    [SerializeField] private float range = 2.2f;
    [SerializeField] private float focusRadius = 0.35f;
    [SerializeField] private LayerMask interactableMask;
    [Tooltip("Opcional: origen/dirección para el ray (p.ej. pivot de cámara). Si está vacío usa el transform del Player.")]
    [SerializeField] private Transform aimSource;

    [Header("Input (Gamepad)")]
    [Tooltip("Acción GamePlay/Interact (mismo botón que Jump: A). Se habilita solo al enfocar.")]
    [SerializeField] private InputActionReference interactAction;
    [Tooltip("Acción GamePlay/Jump (A). Se deshabilita al enfocar para que no salte.")]
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private bool disableJumpWhenFocused = true;
    
    [Header("Hint")]
    [Tooltip("Retardo antes de volver a mostrar iconos tras terminar un estado bloqueante (diálogo/combate/cinemática).")]
    [SerializeField] private float hintReappearDelay = 0.12f;

    private Interactable current;
    private PlayerCarrySystem _carrySystem;
    private PlayerActionManager _actionManager;
    private Game.Player.PlayerBattleModeController _battleModeController;
    private bool _wasBlockedLastFrame;
    private float _resumeAfterBlockAt;
    
    // ✅ OPTIMIZACIÓN FASE 2: Buffer reutilizable para Physics queries
    private Collider[] _interactableBuffer = new Collider[16];

    private void Awake()
    {
        _carrySystem = GetComponent<PlayerCarrySystem>();
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();
        _battleModeController = GetComponent<Game.Player.PlayerBattleModeController>() ?? GetComponentInParent<Game.Player.PlayerBattleModeController>();
    }

    private void OnEnable()
    {
        // Escuchamos Interact, pero la tendremos deshabilitada por defecto.
        if (interactAction?.action != null)
        {
            interactAction.action.performed += OnInteract;
            if (interactAction.action.enabled) interactAction.action.Disable();
        }
    }

    private void OnDisable()
    {
        if (interactAction?.action != null)
        {
            interactAction.action.performed -= OnInteract;
            // Deja Interact deshabilitada al salir
            if (interactAction.action.enabled) interactAction.action.Disable();
        }
        // Asegura que Jump vuelve habilitada
        if (disableJumpWhenFocused && jumpAction?.action != null && !jumpAction.action.enabled)
            jumpAction.action.Enable();
    }

    private void Update()
    {
        // Si hay estado bloqueante (combate/diálogo/UI/cinemática), no enfocamos nada nuevo.
        if (IsInteractionBlocked(out string blockedReason, out bool forceDisableInteractAction))
        {
            if (current != null)
            {
                Debug.Log($"[InteractionDetector] 🚫 {blockedReason}, desenfocando interactable");
            }
            
            SetCurrent(null);

            if (_carrySystem != null && _carrySystem.IsCarrying)
                EnableInteractAction(true);
            else if (forceDisableInteractAction)
                EnableInteractAction(false);

            _wasBlockedLastFrame = true;
            _resumeAfterBlockAt = Time.unscaledTime + Mathf.Max(0f, hintReappearDelay);
            
            return;
        }

        // Evita "flash" del icono justo al salir de diálogo/combate/cinemática.
        if (_wasBlockedLastFrame)
        {
            _wasBlockedLastFrame = false;
            _resumeAfterBlockAt = Time.unscaledTime + Mathf.Max(0f, hintReappearDelay);
        }
        if (Time.unscaledTime < _resumeAfterBlockAt)
        {
            SetCurrent(null);
            EnableInteractAction(false);
            return;
        }

        // Si está cargando algo, mantener el botón A habilitado para soltar
        if (_carrySystem != null && _carrySystem.IsCarrying)
        {
            SetCurrent(null); // No detectar otros objetos mientras carga
            EnableInteractAction(true); // Pero mantener el botón A activo para soltar
            return;
        }

        var nearest = FindNearest();
        SetCurrent(nearest);
    }

    private void OnInteract(InputAction.CallbackContext _context)
    {
        Debug.Log($"[InteractionDetector] 🔘 OnInteract llamado - IsCarrying={_carrySystem?.IsCarrying}, current={current?.name}");
        
        // Si está cargando algo, soltar
        if (_carrySystem != null && _carrySystem.IsCarrying)
        {
            _carrySystem.DropObject();
            Debug.Log($"[InteractionDetector] 📦 Objeto soltado - bloqueando interacciones por cooldown");
            return;
        }

        if (IsInteractionBlocked(out string blockedReason, out bool _ignored))
        {
            Debug.Log($"[InteractionDetector] 🚫 Interacción ignorada: {blockedReason}");
            return;
        }
        if (Time.unscaledTime < _resumeAfterBlockAt)
        {
            Debug.Log("[InteractionDetector] ⏳ Esperando retardo de reactivación del hint/interacción");
            return;
        }

        // CRÍTICO: Verificar si acabamos de soltar un objeto (cooldown activo)
        if (_carrySystem != null && _carrySystem.JustDroppedObject)
        {
            Debug.Log($"[InteractionDetector] ⏳ Cooldown activo después de soltar objeto - ignorando interacción");
            return;
        }

        // Si no está cargando y no hay cooldown, intentar interactuar con objeto enfocado
        if (current != null && current.CanInteract(gameObject))
        {
            Debug.Log($"[InteractionDetector] ✅ Interactuando con: {current.name}");
            current.Interact(gameObject);
        }
        else if (current != null)
        {
            Debug.LogWarning($"[InteractionDetector] ⚠️ {current.name} NO puede interactuar (CanInteract=false)");
        }
        else
        {
            Debug.LogWarning($"[InteractionDetector] ⚠️ No hay objeto enfocado (current=null)");
        }
    }

    private void SetCurrent(Interactable next)
    {
        if (current == next)
        {
            // Mantener el hint sincronizado aunque el foco no cambie.
            // Esto soluciona casos donde se oculta por diálogo/cooldown y no vuelve al terminar.
            if (current)
                current.SetHintVisible(true, gameObject);
            return;
        }

        if (current) current.SetHintVisible(false);
        current = next;
        if (current) current.SetHintVisible(true, gameObject);

        EnableInteractAction(current != null);

        // Deshabilita Jump mientras hay foco (para que A no salte)
        if (disableJumpWhenFocused && jumpAction?.action != null)
        {
            if (current && jumpAction.action.enabled) 
                jumpAction.action.Disable();
            else if (!current && !jumpAction.action.enabled && !(DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)) 
                jumpAction.action.Enable();
        }
    }

    private void EnableInteractAction(bool enable)
    {
        var ia = interactAction?.action;
        if (ia != null)
        {
            if (enable && !ia.enabled) ia.Enable();
            else if (!enable && ia.enabled) ia.Disable();
        }
    }

    private bool IsInteractionBlocked(out string reason, out bool forceDisableInteractAction)
    {
        bool dialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
        bool choicePromptActive = GameState.Is(GamePhase.SavePrompt);
        bool menusBlock = !GameState.CanInteractGlobally; // incluye PauseMenu/MainMenu
        bool cinematicPlaying = AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying;
        bool combatBlocked = IsCombatBlockingInteractions();

        if (dialogueActive)
        {
            reason = "Diálogo activo";
            forceDisableInteractAction = true;
            return true;
        }

        if (choicePromptActive)
        {
            reason = "SavePrompt activo";
            forceDisableInteractAction = true;
            return true;
        }

        if (menusBlock)
        {
            reason = "Menús bloqueando (CanInteractGlobally=false)";
            forceDisableInteractAction = true;
            return true;
        }

        if (cinematicPlaying)
        {
            reason = "Cinemática activa";
            forceDisableInteractAction = true;
            return true;
        }

        if (combatBlocked)
        {
            reason = "Combate/interacción bloqueada por estado actual";
            forceDisableInteractAction = true;
            return true;
        }

        reason = string.Empty;
        forceDisableInteractAction = false;
        return false;
    }

    private bool IsCombatBlockingInteractions()
    {
        if (ActiveCombatRegistry.Count > 0)
        {
            ActiveCombatRegistry.CleanupDestroyedNPCs();
            if (ActiveCombatRegistry.Count > 0)
                return true;
        }

        if (_battleModeController != null && _battleModeController.IsInBattleMode)
            return true;

        if (_actionManager != null && _actionManager.Top == ActionMode.Combat)
            return true;

        return false;
    }

    private Interactable FindNearest()
    {
        var t = aimSource ? aimSource : transform;
        Vector3 origin = t.position + Vector3.up * 1.1f;

        int hitCount = Physics.OverlapSphereNonAlloc(origin, range, _interactableBuffer, interactableMask, QueryTriggerInteraction.Collide); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
        if (hitCount == 0) return null;

        float best = float.MaxValue;
        Interactable winner = null;

        for (int i = 0; i < hitCount; i++)
        {
            var c = _interactableBuffer[i];
            var it = c.GetComponentInParent<Interactable>();
            if (!it || !it.CanInteract(gameObject)) continue;

            float d = Vector3.Distance(origin, it.transform.position);
            if (d < best)
            {
                Vector3 dir = (it.transform.position - origin).normalized;
                if (Physics.SphereCast(origin, focusRadius, dir, out _, d + 0.1f, ~0, QueryTriggerInteraction.Ignore))
                {
                    best = d;
                    winner = it;
                }
            }
        }
        return winner;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var t = aimSource ? aimSource : transform;
        Gizmos.color = new Color(0,1,1,0.35f);
        Gizmos.DrawWireSphere(t.position + Vector3.up * 1.1f, range);
    }
#endif
}
