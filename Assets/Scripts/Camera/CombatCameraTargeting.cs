using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Core;
using Game.Interfaces;

/// <summary>
/// Sistema de targeting de cámara para combate.
/// Versión 2.2 - Corregido el cambio de objetivo (Target Switching) con D-Pad.
/// </summary>
public class CombatCameraTargeting : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private vThirdPersonCamera thirdPersonCamera;
    [SerializeField] private Transform playerTransform;
    
    [Header("Integración con Sistema de Proyectiles")]
    [SerializeField] private PlayerTargeting playerTargeting;
    [SerializeField] private bool syncWithProjectileTargeting = true;
    
    [Header("Configuración")]
    [SerializeField] private float maxLockDistance = 30f;
    [SerializeField] private LayerMask enemyLayerMask;
    
    [Header("Visual")]
    [SerializeField] private GameObject lockIndicatorPrefab;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Estado
    private ICombatTarget currentTarget;
    private GameObject lockIndicatorInstance;
    private bool isLockActive;
    private float originalCameraSensitivity;
    private bool wasInCombatLastFrame;
    
    private List<ICombatTarget> potentialTargets = new List<ICombatTarget>();
    
    private void Awake()
    {
        Log("Awake: Inicializando...");
        if (thirdPersonCamera == null)
            thirdPersonCamera = GetComponent<vThirdPersonCamera>();
        
        if (playerTransform == null && PlayerService.TryGetPlayer(out var player))
            playerTransform = player.transform;
        
        if (playerTargeting == null && playerTransform != null)
            playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
        
        if (thirdPersonCamera != null)
            originalCameraSensitivity = thirdPersonCamera.xMouseSensitivity;
    }
    
    private void OnEnable()
    {
        Log("OnEnable: Suscribiendo a eventos.");
        GamepadInputReader.OnInput += HandleGamepadInput;
    }
    
    private void OnDisable()
    {
        Log("OnDisable: Desuscribiendo y limpiando.");
        GamepadInputReader.OnInput -= HandleGamepadInput;
        ReleaseLock();
    }
    
    private void Update()
    {
        if (playerTransform == null)
        {
            if (PlayerService.TryGetPlayer(out var player))
            {
                playerTransform = player.transform;
                Log($"PlayerTransform obtenido en Update: {player.name}");
            }
            else
            {
                if (Time.frameCount % 120 == 0) Log("Esperando referencia del jugador...");
                return;
            }
        }
        
        FindPotentialTargets();
        bool isInCombat = potentialTargets.Count > 0;
        
        if (isInCombat && !wasInCombatLastFrame) OnEnterCombat();
        else if (!isInCombat && wasInCombatLastFrame) OnExitCombat();
        
        wasInCombatLastFrame = isInCombat;
        
        if (isInCombat && !isLockActive) TryAutoLock();
        
        if (isLockActive && currentTarget != null)
        {
            if (!currentTarget.IsTargetable || Vector3.Distance(playerTransform.position, currentTarget.TargetTransform.position) > maxLockDistance)
            {
                Log($"Target '{currentTarget.TargetTransform.name}' ya no es válido. Buscando otro...");
                SwitchToNextTarget();
            }
            else
            {
                HandleLockRotation();
                UpdateLockIndicator();
            }
        }
    }

    private void FindPotentialTargets()
    {
        potentialTargets.Clear();
        if (playerTransform == null) return;

        Collider[] hits = Physics.OverlapSphere(playerTransform.position, maxLockDistance, enemyLayerMask);
        
        foreach (var hit in hits)
        {
            ICombatTarget target = hit.GetComponentInParent<ICombatTarget>();
            if (target != null && target.IsTargetable && !potentialTargets.Contains(target))
            {
                potentialTargets.Add(target);
            }
        }
    }

    private void TryAutoLock()
    {
        if (potentialTargets.Count > 0)
        {
            ICombatTarget closest = potentialTargets.OrderBy(t => Vector3.Distance(playerTransform.position, t.TargetTransform.position)).FirstOrDefault();
            if (closest != null)
            {
                Log($"Auto-lock: Enemigo más cercano es '{closest.TargetTransform.name}'");
                SetTarget(closest);
            }
        }
    }
    
    private void HandleGamepadInput(GamepadInputReader.InputEvent inputEvent)
    {
        if (!isLockActive || currentTarget == null || inputEvent.Phase != UnityEngine.InputSystem.InputActionPhase.Performed)
            return;
        
        if (inputEvent.Type == GamepadInputReader.InputEventType.DpadRight) SwitchToNextTarget();
        else if (inputEvent.Type == GamepadInputReader.InputEventType.DpadLeft) SwitchToPreviousTarget();
    }
    
    private void OnEnterCombat()
    {
        Log($"Entrando en combate - {potentialTargets.Count} objetivos potenciales detectados.");
        TryAutoLock();
    }
    
    private void OnExitCombat()
    {
        Log("Saliendo de combate - Liberando lock");
        ReleaseLock();
    }
    
    private void SetTarget(ICombatTarget newTarget)
    {
        if (newTarget == null || !newTarget.IsTargetable)
        {
            ReleaseLock();
            return;
        }
        
        isLockActive = true;
        currentTarget = newTarget;
        
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.xMouseSensitivity = originalCameraSensitivity * 0.3f;
        }
        
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.SetManualTarget(newTarget.TargetTransform);
        }
        
        CreateLockIndicator();
        Log($"Lock establecido en: {newTarget.TargetTransform.name}");
    }

    private void SwitchToNextTarget()
    {
        Log("Cambiando a siguiente objetivo (derecha)");
        SwitchTarget(1);
    }

    private void SwitchToPreviousTarget()
    {
        Log("Cambiando a objetivo anterior (izquierda)");
        SwitchTarget(-1);
    }

    private void SwitchTarget(int direction)
    {
        if (potentialTargets.Count <= 1)
        {
            Log("No hay otros objetivos para cambiar.");
            return;
        }

        Transform cameraTransform = thirdPersonCamera.transform;
        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;

        // Ordenar la lista de objetivos por su ángulo respecto a la cámara.
        List<ICombatTarget> sortedTargets = potentialTargets.OrderBy(target =>
        {
            Vector3 dirToTarget = (target.TargetTransform.position - playerTransform.position).normalized;
            return Vector3.SignedAngle(cameraForward, dirToTarget, Vector3.up);
        }).ToList();

        int currentIndex = sortedTargets.IndexOf(currentTarget);
        if (currentIndex == -1)
        {
            // Si el target actual no está en la lista (ej. murió), simplemente coge el primero.
            Log("Target actual no encontrado, seleccionando el más cercano.");
            TryAutoLock();
            return;
        }

        int nextIndex = (currentIndex + direction + sortedTargets.Count) % sortedTargets.Count;
        
        Log($"Cambiando de '{sortedTargets[currentIndex].TargetTransform.name}' a '{sortedTargets[nextIndex].TargetTransform.name}'");
        SetTarget(sortedTargets[nextIndex]);
    }
    
    private void ReleaseLock()
    {
        if (!isLockActive) return;
        
        Log("Lock de cámara liberado");
        
        isLockActive = false;
        currentTarget = null;
        
        if (thirdPersonCamera != null)
            thirdPersonCamera.xMouseSensitivity = originalCameraSensitivity;
        
        if (syncWithProjectileTargeting && playerTargeting != null)
            playerTargeting.ClearManualTarget();
        
        DestroyLockIndicator();
    }
    
    private void HandleLockRotation()
    {
        if (currentTarget == null || playerTransform == null || thirdPersonCamera == null)
        {
            ReleaseLock();
            return;
        }
        
        thirdPersonCamera.LookAtPosition(currentTarget.AimPoint, smooth: true);
    }
    
    private void CreateLockIndicator()
    {
        DestroyLockIndicator();
        if (lockIndicatorPrefab != null && currentTarget != null)
        {
            lockIndicatorInstance = Instantiate(lockIndicatorPrefab, currentTarget.TargetTransform);
            lockIndicatorInstance.transform.position = currentTarget.AimPoint;
        }
    }
    
    private void UpdateLockIndicator()
    {
        if (lockIndicatorInstance != null && currentTarget != null)
        {
            lockIndicatorInstance.transform.position = currentTarget.AimPoint;
            lockIndicatorInstance.transform.Rotate(Vector3.up, 90f * Time.deltaTime);
        }
    }
    
    private void DestroyLockIndicator()
    {
        if (lockIndicatorInstance != null)
        {
            Destroy(lockIndicatorInstance);
            lockIndicatorInstance = null;
        }
    }
    
    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[CombatCameraTargeting] {message}");
    }
    
    public bool IsLocked => isLockActive && currentTarget != null;
    public ICombatTarget CurrentTarget => currentTarget;
}
