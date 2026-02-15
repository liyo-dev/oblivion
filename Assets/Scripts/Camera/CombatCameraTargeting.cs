using UnityEngine;
using System.Collections.Generic;
using Core;

/// <summary>
/// Sistema de targeting de cámara para combate.
/// Hace lock automático al enemigo más cercano cuando entras en combate.
/// Permite cambiar de objetivo con D-Pad Left/Right y se desactiva al salir de combate.
/// </summary>
public class CombatCameraTargeting : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private vThirdPersonCamera thirdPersonCamera;
    [SerializeField] private Transform playerTransform;
    
    [Header("Integración con Sistema de Proyectiles")]
    [Tooltip("Si está asignado, sincroniza el targeting de cámara con el sistema de proyectiles")]
    [SerializeField] private PlayerTargeting playerTargeting;
    [Tooltip("Sincronizar automáticamente: el target de cámara se usa también para proyectiles")]
    [SerializeField] private bool syncWithProjectileTargeting = true;
    
    [Header("Configuración")]
    [Tooltip("Distancia máxima para hacer lock a un enemigo")]
    [SerializeField] private float maxLockDistance = 30f;
    
    [Tooltip("Velocidad de rotación de la cámara hacia el objetivo")]
    [SerializeField] private float targetingRotationSpeed = 8f;
    
    [Tooltip("Offset vertical para el punto de mira (ajustar según altura del enemigo)")]
    [SerializeField] private float targetHeightOffset = 1.5f;
    
    [Header("Visual")]
    [Tooltip("Prefab del indicador visual de lock (opcional)")]
    [SerializeField] private GameObject lockIndicatorPrefab;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Estado
    private GameObject currentTarget;
    private GameObject lockIndicatorInstance;
    private bool isLockActive;
    private float originalCameraSensitivity;
    private bool wasInCombatLastFrame;
    
    private void Awake()
    {
        // Auto-referencias
        if (thirdPersonCamera == null)
            thirdPersonCamera = GetComponent<vThirdPersonCamera>();
        
        // Obtener referencia al jugador usando PlayerService
        if (playerTransform == null)
        {
            if (PlayerService.TryGetPlayer(out var player))
                playerTransform = player.transform;
        }
        
        // Auto-detectar PlayerTargeting si no está asignado
        if (playerTargeting == null && playerTransform != null)
            playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
    }
    
    private void OnEnable()
    {
        ActiveCombatRegistry.OnNPCEnteredCombat += OnNPCEnteredCombat;
        ActiveCombatRegistry.OnNPCExitedCombat += OnNPCExitedCombat;
        
        // Suscribirse al sistema de inputs del gamepad
        GamepadInputReader.OnInput += HandleGamepadInput;
    }
    
    private void OnDisable()
    {
        ActiveCombatRegistry.OnNPCEnteredCombat -= OnNPCEnteredCombat;
        ActiveCombatRegistry.OnNPCExitedCombat -= OnNPCExitedCombat;
        
        // Desuscribirse del sistema de inputs
        GamepadInputReader.OnInput -= HandleGamepadInput;
        
        ReleaseLock();
    }
    
    private void Update()
    {
        // Intentar obtener playerTransform si todavía es null
        if (playerTransform == null)
        {
            if (PlayerService.TryGetPlayer(out var player))
            {
                playerTransform = player.transform;
                Debug.Log($"[CombatCameraTargeting] ✅ PlayerTransform obtenido tardíamente: {player.name}");
                
                // También intentar obtener PlayerTargeting
                if (playerTargeting == null)
                    playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
            }
            else
            {
                return; // Sin player, no podemos hacer nada
            }
        }
        
        // Verificar estado de combate
        bool isInCombat = ActiveCombatRegistry.Count > 0;
        
        // Si acabamos de entrar en combate, hacer lock automático
        if (isInCombat && !wasInCombatLastFrame)
        {
            OnEnterCombat();
        }
        // Si salimos de combate, liberar lock
        else if (!isInCombat && wasInCombatLastFrame)
        {
            OnExitCombat();
        }
        
        wasInCombatLastFrame = isInCombat;
        
        // Si estamos en combate pero no tenemos lock, intentar hacer lock periódicamente
        if (isInCombat && !isLockActive)
        {
            TryAutoLock();
        }
        
        // Si estamos en lock activo
        if (isLockActive && currentTarget != null)
        {
            HandleLockRotation();
            UpdateLockIndicator();
        }
    }
    
    /// <summary>
    /// Intenta hacer lock automático al enemigo más cercano si no hay lock activo
    /// </summary>
    private float _lastAutoLockAttempt;
    private const float AUTO_LOCK_CHECK_INTERVAL = 0.5f; // Verificar cada 0.5 segundos
    
    private void TryAutoLock()
    {
        // Throttle para no verificar cada frame
        if (Time.time - _lastAutoLockAttempt < AUTO_LOCK_CHECK_INTERVAL)
            return;
        
        _lastAutoLockAttempt = Time.time;
        
        if (playerTransform == null) return;
        
        GameObject closestEnemy = ActiveCombatRegistry.GetClosestCombatNPC(playerTransform.position, maxLockDistance);
        
        if (closestEnemy != null)
        {
            Log($"🎯 Auto-lock tardío: Enemigo '{closestEnemy.name}' ahora está lo suficientemente cerca");
            SetTarget(closestEnemy);
        }
    }
    
    /// <summary>
    /// Maneja los inputs del gamepad (D-Pad Left/Right para cambiar de objetivo)
    /// </summary>
    private void HandleGamepadInput(GamepadInputReader.InputEvent inputEvent)
    {
        // DEBUG TEMPORAL: Log INCONDICIONAL para verificar si llegan los eventos de D-Pad
        if (inputEvent.Type == GamepadInputReader.InputEventType.DpadLeft || 
            inputEvent.Type == GamepadInputReader.InputEventType.DpadRight)
        {
            Debug.Log($"[CombatCameraTargeting] 🎮 D-Pad RECIBIDO: {inputEvent.Type}, Phase: {inputEvent.Phase}, isLockActive: {isLockActive}, currentTarget: {(currentTarget != null ? currentTarget.name : "NULL")}, ActiveCombatCount: {ActiveCombatRegistry.Count}");
        }
        
        // Solo procesar inputs si estamos en lock activo
        if (!isLockActive || currentTarget == null)
        {
            return;
        }
        
        // Solo responder a eventos "performed" (pulsación)
        if (inputEvent.Phase != UnityEngine.InputSystem.InputActionPhase.Performed)
            return;
        
        switch (inputEvent.Type)
        {
            case GamepadInputReader.InputEventType.DpadRight:
                Log("🎮 D-Pad Right presionado - Cambiando al siguiente enemigo");
                SwitchToNextTarget();
                break;
                
            case GamepadInputReader.InputEventType.DpadLeft:
                Log("🎮 D-Pad Left presionado - Cambiando al enemigo anterior");
                SwitchToPreviousTarget();
                break;
        }
    }
    
    private void OnEnterCombat()
    {
        Debug.Log($"[CombatCameraTargeting] 🎯 Entrando en combate - Buscando objetivo para lock. ActiveCombatRegistry.Count = {ActiveCombatRegistry.Count}");
        
        // Listar todos los NPCs en combate para debug
        var allInCombat = ActiveCombatRegistry.GetAllInCombat();
        foreach (var npc in allInCombat)
        {
            Debug.Log($"[CombatCameraTargeting]   - NPC en combate: {(npc != null ? npc.name : "NULL")}");
        }
        
        // Buscar enemigo más cercano
        GameObject closestEnemy = ActiveCombatRegistry.GetClosestCombatNPC(playerTransform.position, maxLockDistance);
        
        if (closestEnemy != null)
        {
            Debug.Log($"[CombatCameraTargeting] ✅ Lock establecido en: {closestEnemy.name}");
            SetTarget(closestEnemy);
        }
        else
        {
            Debug.Log("[CombatCameraTargeting] ⚠️ No hay enemigos lo suficientemente cerca para lock automático");
        }
    }
    
    private void OnExitCombat()
    {
        Log("🏳️ Saliendo de combate - Liberando lock");
        ReleaseLock();
    }
    
    private void OnNPCEnteredCombat(GameObject npc)
    {
        // Si no tenemos target y un enemigo entra en combate cerca, hacer lock
        if (currentTarget == null && playerTransform != null)
        {
            float distance = Vector3.Distance(npc.transform.position, playerTransform.position);
            if (distance <= maxLockDistance)
            {
                Log($"🎯 Nuevo enemigo '{npc.name}' entró en combate cerca - Haciendo lock");
                SetTarget(npc);
            }
        }
    }
    
    private void OnNPCExitedCombat(GameObject npc)
    {
        // Si era nuestro target actual, buscar otro
        if (currentTarget == npc)
        {
            Log($"⚠️ Target actual '{npc.name}' salió de combate - Buscando nuevo objetivo");
            SwitchToNextTarget();
        }
    }
    
    /// <summary>
    /// Establece un nuevo objetivo para el lock de cámara
    /// </summary>
    private void SetTarget(GameObject newTarget)
    {
        if (newTarget == null)
        {
            ReleaseLock();
            return;
        }
        
        // Guardar sensibilidad original ANTES de activar el lock (solo la primera vez)
        bool wasLockActive = isLockActive;
        
        currentTarget = newTarget;
        isLockActive = true;
        
        // Reducir sensibilidad de la cámara durante el lock
        if (thirdPersonCamera != null)
        {
            if (!wasLockActive) // Solo guardar la primera vez (cuando no estaba en lock)
            {
                originalCameraSensitivity = thirdPersonCamera.xMouseSensitivity;
            }
            thirdPersonCamera.xMouseSensitivity = originalCameraSensitivity * 0.3f; // Reducir movimiento horizontal
        }
        
        // ✅ SINCRONIZAR con PlayerTargeting para proyectiles/hechizos
        // El marker de PlayerTargeting será el único indicador visual (unificado)
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.SetManualTarget(newTarget.transform);
            Log($"🎯 PlayerTargeting sincronizado con: {newTarget.name}");
        }
        
        // ✅ NO crear indicador visual propio - usamos el de PlayerTargeting
        // CreateLockIndicator(); // Desactivado: usamos el marker de PlayerTargeting
        
        Log($"🎯 Lock establecido en: {newTarget.name}");
    }
    
    /// <summary>
    /// Cambia al siguiente enemigo en combate (D-Pad Right)
    /// </summary>
    private void SwitchToNextTarget()
    {
        List<GameObject> enemies = ActiveCombatRegistry.GetAllInCombat();
        
        if (enemies.Count == 0)
        {
            Log("No hay enemigos en combate");
            ReleaseLock();
            return;
        }
        
        if (enemies.Count == 1)
        {
            Log("Solo hay un enemigo, manteniendo lock actual");
            return;
        }
        
        // Encontrar el siguiente enemigo
        int currentIndex = enemies.IndexOf(currentTarget);
        int nextIndex = (currentIndex + 1) % enemies.Count;
        
        GameObject nextTarget = enemies[nextIndex];
        
        if (nextTarget != null)
        {
            Log($"🔄 D-Pad Right: Cambiando target → {nextTarget.name}");
            SetTarget(nextTarget);
        }
    }
    
    /// <summary>
    /// Cambia al enemigo anterior en combate (D-Pad Left)
    /// </summary>
    private void SwitchToPreviousTarget()
    {
        List<GameObject> enemies = ActiveCombatRegistry.GetAllInCombat();
        
        if (enemies.Count == 0)
        {
            Log("No hay enemigos en combate");
            ReleaseLock();
            return;
        }
        
        if (enemies.Count == 1)
        {
            Log("Solo hay un enemigo, manteniendo lock actual");
            return;
        }
        
        // Encontrar el enemigo anterior
        int currentIndex = enemies.IndexOf(currentTarget);
        int previousIndex = (currentIndex - 1 + enemies.Count) % enemies.Count;
        
        GameObject previousTarget = enemies[previousIndex];
        
        if (previousTarget != null)
        {
            Log($"🔄 D-Pad Left: Cambiando target → {previousTarget.name}");
            SetTarget(previousTarget);
        }
    }
    
    /// <summary>
    /// Libera el lock de cámara
    /// </summary>
    private void ReleaseLock()
    {
        if (!isLockActive) return;
        
        Log("🔓 Lock de cámara liberado");
        
        isLockActive = false;
        currentTarget = null;
        
        // Restaurar sensibilidad original
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.xMouseSensitivity = originalCameraSensitivity;
        }
        
        // ✅ LIBERAR target manual de PlayerTargeting para volver a targeting automático
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.ClearManualTarget();
            Log("🔓 PlayerTargeting: target manual liberado");
        }
        
        // Destruir indicador (si existiera)
        DestroyLockIndicator();
    }
    
    /// <summary>
    /// Maneja la rotación de la cámara hacia el objetivo
    /// </summary>
    private void HandleLockRotation()
    {
        if (currentTarget == null || playerTransform == null || thirdPersonCamera == null)
        {
            ReleaseLock();
            return;
        }
        
        // Calcular punto de mira con offset vertical
        Vector3 targetPoint = currentTarget.transform.position + Vector3.up * targetHeightOffset;
        
        // Usar el nuevo método de vThirdPersonCamera para mirar al objetivo
        thirdPersonCamera.LookAtPosition(targetPoint, smooth: true);
    }
    
    /// <summary>
    /// Crea el indicador visual de lock
    /// </summary>
    private void CreateLockIndicator()
    {
        DestroyLockIndicator(); // Limpiar anterior
        
        if (lockIndicatorPrefab != null && currentTarget != null)
        {
            lockIndicatorInstance = Instantiate(lockIndicatorPrefab, currentTarget.transform);
            lockIndicatorInstance.transform.localPosition = Vector3.up * targetHeightOffset;
        }
    }
    
    /// <summary>
    /// Actualiza la posición del indicador visual
    /// </summary>
    private void UpdateLockIndicator()
    {
        if (lockIndicatorInstance != null && currentTarget != null)
        {
            // El indicador ya es hijo del target, se mueve automáticamente
            // Pero podemos añadir efectos adicionales aquí (rotación, escala, etc.)
            lockIndicatorInstance.transform.Rotate(Vector3.up, 90f * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Destruye el indicador visual
    /// </summary>
    private void DestroyLockIndicator()
    {
        if (lockIndicatorInstance != null)
        {
            Destroy(lockIndicatorInstance);
            lockIndicatorInstance = null;
        }
    }
    
    /// <summary>
    /// Log con toggle de debug
    /// </summary>
    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[CombatCameraTargeting] {message}");
        }
    }
    
    // ========== API Pública ==========
    
    /// <summary>
    /// Verifica si hay un lock activo
    /// </summary>
    public bool IsLocked => isLockActive && currentTarget != null;
    
    /// <summary>
    /// Obtiene el objetivo actual
    /// </summary>
    public GameObject CurrentTarget => currentTarget;
    
    /// <summary>
    /// Fuerza un lock a un objetivo específico
    /// </summary>
    public void ForceLockTarget(GameObject target)
    {
        if (target != null)
        {
            SetTarget(target);
        }
    }
    
    /// <summary>
    /// Fuerza la liberación del lock
    /// </summary>
    public void ForceReleaseLock()
    {
        ReleaseLock();
    }
}
