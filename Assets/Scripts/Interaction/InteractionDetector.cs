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

    private Interactable current;
    private PlayerCarrySystem _carrySystem;

    private void Awake()
    {
        _carrySystem = GetComponent<PlayerCarrySystem>();
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
        // Si hay UI bloqueante (pausa/menús/diálogo/saveprompt/cinemáticas), no enfocamos nada nuevo
        bool dialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.IsOpen;
        bool choicePromptActive = GameState.Is(GamePhase.SavePrompt);
        bool menusBlock = !GameState.CanInteractGlobally; // incluye PauseMenu y MainMenu
        bool cinematicPlaying = AdditiveSceneCinematic.IsAnyAdditiveCinematicPlaying;
        
        if (dialogueActive || choicePromptActive || menusBlock || cinematicPlaying)
        {
            // Log solo cuando cambia el estado
            if (current != null)
            {
                if (dialogueActive)
                    Debug.Log("[InteractionDetector] 💬 Diálogo activo, desenfocando interactable");
                else if (choicePromptActive)
                    Debug.Log("[InteractionDetector] ⚠️ SavePrompt activo, desenfocando interactable");
                else if (menusBlock)
                    Debug.Log("[InteractionDetector] 🚫 Menús bloqueando (CanInteractGlobally=false), desenfocando interactable");
                else if (cinematicPlaying)
                    Debug.Log("[InteractionDetector] 🎬 Cinemática activa, desenfocando interactable");
            }
            
            SetCurrent(null);
            
            // CRÍTICO: Deshabilitar completamente la acción de interact durante cinemáticas
            // para evitar que interfiera con el HoldToSkipUI
            if (cinematicPlaying)
            {
                EnableInteractAction(false);
            }
            
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

    private void OnInteract(InputAction.CallbackContext _)
    {
        Debug.Log($"[InteractionDetector] 🔘 OnInteract llamado - IsCarrying={_carrySystem?.IsCarrying}, current={current?.name}");
        
        // Si está cargando algo, soltar
        if (_carrySystem != null && _carrySystem.IsCarrying)
        {
            _carrySystem.DropObject();
            Debug.Log($"[InteractionDetector] 📦 Objeto soltado - bloqueando interacciones por cooldown");
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
        if (current == next) return;

        if (current) current.SetHintVisible(false);
        current = next;
        if (current) current.SetHintVisible(true);

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

    private Interactable FindNearest()
    {
        var t = aimSource ? aimSource : transform;
        Vector3 origin = t.position + Vector3.up * 1.1f;

        var cols = Physics.OverlapSphere(origin, range, interactableMask, QueryTriggerInteraction.Collide);
        if (cols == null || cols.Length == 0) return null;

        float best = float.MaxValue;
        Interactable winner = null;

        foreach (var c in cols)
        {
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
