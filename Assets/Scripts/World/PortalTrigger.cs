using UnityEngine;
using Invector.vCharacterController;
using UnityEngine.AI;

[RequireComponent(typeof(Collider))]
public class PortalTrigger : MonoBehaviour
{
    public string targetAnchorId;
    public string requiredFlag;
    public string setFlagOnEnter;

    private bool _pendingUse;

    void Reset(){ GetComponent<Collider>().isTrigger = true; }

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_pendingUse)
        {
            var player = GameObject.FindWithTag("Player");
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

        // 3) Ejecutar teletransporte (mantiene transición por defecto aquí)
        SpawnManager.TeleportTo(targetAnchorId, true);
        // NO guardar aquí
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
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        var cc = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        if (cc) cc.enabled = false; // evita empujes durante el lapso hasta el cut
    }

    private void RestorePlayerMovement(GameObject player)
    {
        if (!player) return;
        var cc = player.GetComponent<CharacterController>() ?? player.GetComponentInChildren<CharacterController>(true);
        if (cc && !cc.enabled) cc.enabled = true;

        var input = player.GetComponent<vThirdPersonInput>() ?? player.GetComponentInChildren<vThirdPersonInput>(true);
        if (input && !input.enabled) input.enabled = true;

        var agent = player.GetComponent<NavMeshAgent>() ?? player.GetComponentInChildren<NavMeshAgent>(true);
        if (agent) agent.isStopped = false;
    }
}