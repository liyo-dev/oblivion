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

    // FIX M7 (auditoría 2026-08-07): FindNearest() corría cada frame sin throttle (OverlapSphereNonAlloc
    // + un SphereCast por candidato). PlayerTargeting ya usa 10 Hz para su propio escaneo; mismo criterio aquí.
    [Header("Rendimiento")]
    [SerializeField] private float updatesPerSecond = 10f;
    private float _nextScan;

    private Interactable current;
    private Interactable _lastFoundNearest;
    private PlayerCarrySystem _carrySystem;
    private PlayerActionManager _actionManager;
    private Game.Player.PlayerBattleModeController _battleModeController;
    private bool _wasBlockedLastFrame;
    private float _resumeAfterBlockAt;

    // ✅ OPTIMIZACIÓN FASE 2: Buffer reutilizable para Physics queries
    private Collider[] _interactableBuffer = new Collider[16];

    // FIX (2026-08-11): la máscara de obstrucción del SphereCast de FindNearest() usaba
    // ~interactableMask, que solo excluye la capa "Interactable". Eso hace que el propio
    // collider del jugador (capa "Player"/"Default", según el rig) y el suelo (capa "Floor")
    // cuenten como obstrucción, bloqueando el hint en casi cualquier punto de guardado/NPC.
    // Cacheado en Awake (regla del proyecto: nunca LayerMask en Update).
    private int _obstructionMask;

    private void Awake()
    {
        _carrySystem = GetComponent<PlayerCarrySystem>();
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();
        _battleModeController = GetComponent<Game.Player.PlayerBattleModeController>() ?? GetComponentInParent<Game.Player.PlayerBattleModeController>();

        int floorLayer = LayerMask.NameToLayer("Floor");
        int excludeBits = interactableMask.value | (1 << gameObject.layer);
        if (floorLayer >= 0) excludeBits |= 1 << floorLayer;
        _obstructionMask = ~excludeBits;
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

        // FIX M7 (auditoría 2026-08-07): throttle a updatesPerSecond en vez de escanear cada
        // frame. SetCurrent() se sigue llamando cada frame con el último resultado conocido —
        // sigue sincronizando el hint aunque no toque re-escanear este frame (ver comentario en
        // SetCurrent).
        if (updatesPerSecond <= 0f || Time.unscaledTime >= _nextScan)
        {
            _lastFoundNearest = FindNearest();
            _nextScan = Time.unscaledTime + 1f / Mathf.Max(0.01f, updatesPerSecond);
        }
        SetCurrent(_lastFoundNearest);
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
        bool cinematicPlaying = CinematicSequencerBase.AnySequenceActive;
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

            // FIX (2026-08-12): la línea de visión se comprobaba contra it.transform.position,
            // que en un NPC es la base/los pies. Cualquier mueble de altura media entre el player
            // y el NPC (mostrador de tienda, mesa, valla baja...) queda literalmente a esa altura
            // y se detecta como obstrucción real, aunque el jugador vea perfectamente la cabeza y
            // el torso del NPC por encima — el hint nunca llega a mostrarse en ningún vendedor con
            // mostrador delante. En vez de re-etiquetar cada mueble de cada tienda (fragil: hay que
            // acordarse de hacerlo por cada objeto nuevo, y mover objetos de layer tiene efectos
            // secundarios en renderizado/física que no queremos tocar), se apunta la comprobación
            // al mismo punto donde ya se muestra el icono de interactuar (Interactable.SightPoint,
            // por defecto la posición del propio hint, que está a la altura de la cabeza/encima).
            // Si el jugador puede ver ese punto, el mostrador ya no cuenta como obstrucción.
            Vector3 sightPoint = it.SightPoint;
            float d = Vector3.Distance(origin, it.transform.position);
            float sightDistance = Vector3.Distance(origin, sightPoint);
            if (d < best)
            {
                // FIX M7 (auditoría 2026-08-07): la comprobación estaba invertida. Antes se
                // ACEPTABA el candidato cuando el SphereCast SÍ golpeaba algo (incluida una pared
                // de por medio → "interactuar a través de muros") y se RECHAZABA cuando no golpeaba
                // nada (p.ej. un interactuable solo-trigger en espacio abierto, que
                // QueryTriggerInteraction.Ignore nunca detecta → nunca seleccionable). Ahora un
                // impacto sólido se trata como obstrucción real (se descarta el candidato) y la
                // ausencia de impacto significa línea de visión libre (se acepta).
                // FIX (2026-08-11): se usaba ~interactableMask, que solo excluye la propia capa
                // "Interactable" — el SphereCast se autogolpeaba con el collider del jugador y con
                // el suelo ("Floor"), marcando el interactuable como obstruido siempre. Se usa
                // _obstructionMask (cacheado en Awake), que además excluye la capa del propio
                // jugador y "Floor", pero SIGUE incluyendo muros/puertas/obstáculos.
                Vector3 dir = (sightPoint - origin).normalized;
                bool obstructed = Physics.SphereCast(origin, focusRadius, dir, out _, sightDistance, _obstructionMask, QueryTriggerInteraction.Ignore);
                if (!obstructed)
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
