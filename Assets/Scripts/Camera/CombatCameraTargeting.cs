using UnityEngine;
using System.Collections.Generic;
using Core;

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
    [SerializeField] private float visualResyncInterval = 0.1f;
    [SerializeField] private float shoulderSwitchDebounce = 0.12f;
    
    [Header("Ángulo frontal para marcador")]
    [SerializeField] private float facingAngleForMarker = 140f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    
    private GameObject currentTarget;
    private bool isLockActive;
    private bool wasInCombatLastFrame;
    private float _lastAutoLockAttempt;
    private float _lastVisualResync;
    private float _lastShoulderSwitchAt = -999f;
    private bool _isInDialogue;
    private bool _suppressedByLevitation;

    private void Awake()
    {
        if (thirdPersonCamera == null)
            thirdPersonCamera = GetComponent<vThirdPersonCamera>();
        
        if (playerTransform == null && PlayerService.TryGetPlayer(out var player))
            playerTransform = player.transform;
        
        if (playerTargeting == null && playerTransform != null)
            playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
    }
    
    private void OnEnable()
    {
        ActiveCombatRegistry.OnNPCEnteredCombat += OnNPCEnteredCombat;
        ActiveCombatRegistry.OnNPCExitedCombat += OnNPCExitedCombat;
        GamepadInputReader.OnInput += HandleGamepadInput;
        DialogueManager.OnDialogueStarted += OnDialogueStarted;
        DialogueManager.OnDialogueClosed += OnDialogueClosed;
        LevitationTarget.OnAnyLevitationStarted += OnLevitationStarted;
        LevitationTarget.OnAnyLevitationEnded += OnLevitationEnded;
    }

    private void OnDisable()
    {
        ActiveCombatRegistry.OnNPCEnteredCombat -= OnNPCEnteredCombat;
        ActiveCombatRegistry.OnNPCExitedCombat -= OnNPCExitedCombat;
        GamepadInputReader.OnInput -= HandleGamepadInput;
        DialogueManager.OnDialogueStarted -= OnDialogueStarted;
        DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        LevitationTarget.OnAnyLevitationStarted -= OnLevitationStarted;
        LevitationTarget.OnAnyLevitationEnded -= OnLevitationEnded;
        ReleaseLock();
    }

    private void OnDialogueStarted(Transform _) => _isInDialogue = true;

    private void OnDialogueClosed(Transform _)
    {
        _isInDialogue = false;
        // Forzar resync inmediato del marker al cerrar el diálogo
        if (isLockActive && currentTarget != null)
        {
            _lastVisualResync = 0f;
            if (syncWithProjectileTargeting && playerTargeting != null)
            {
                playerTargeting.SetManualTarget(currentTarget.transform);
                playerTargeting.ForceVisualRefresh();
            }
        }
    }
    
    private void Update()
    {
        if (playerTransform == null)
        {
            if (PlayerService.TryGetPlayer(out var player))
            {
                playerTransform = player.transform;
                if (playerTargeting == null)
                    playerTargeting = playerTransform.GetComponentInChildren<PlayerTargeting>();
            }
            else return;
        }
        
        bool isInCombat = ActiveCombatRegistry.Count > 0;
        
        if (isInCombat && !wasInCombatLastFrame) OnEnterCombat();
        else if (!isInCombat && wasInCombatLastFrame) OnExitCombat();
        
        wasInCombatLastFrame = isInCombat;
        
        if (isInCombat && !isLockActive && !_suppressedByLevitation) TryAutoLock();
        if (isLockActive) EnsureVisualLockSync();
    }
    
    private void TryAutoLock()
    {
        if (Time.time - _lastAutoLockAttempt < 0.5f) return;
        _lastAutoLockAttempt = Time.time;
        
        if (playerTransform == null) return;
        
        GameObject closestEnemy = ActiveCombatRegistry.GetClosestCombatNPC(playerTransform.position, maxLockDistance);
        
        if (closestEnemy != null)
        {
            Log($"🎯 Auto-lock: {closestEnemy.name}");
            SetTarget(closestEnemy);
        }
    }
    
    private void HandleGamepadInput(GamepadInputReader.InputEvent inputEvent)
    {
        if (!isLockActive || currentTarget == null || inputEvent.Phase != UnityEngine.InputSystem.InputActionPhase.Performed)
            return;

        if (Time.unscaledTime - _lastShoulderSwitchAt < shoulderSwitchDebounce)
            return;
        
        switch (inputEvent.Type)
        {
            case GamepadInputReader.InputEventType.RightShoulder:
                _lastShoulderSwitchAt = Time.unscaledTime;
                SwitchToNextTarget();
                break;
            case GamepadInputReader.InputEventType.LeftShoulder:
                _lastShoulderSwitchAt = Time.unscaledTime;
                SwitchToPreviousTarget();
                break;
        }
    }
    
    private void OnEnterCombat()
    {
        Log($"🎯 Entrando en combate. Buscando objetivo...");
        GameObject closestEnemy = ActiveCombatRegistry.GetClosestCombatNPC(playerTransform.position, maxLockDistance);
        
        if (closestEnemy != null)
        {
            Log($"✅ Lock inicial en: {closestEnemy.name}");
            SetTarget(closestEnemy);
        }
    }
    
    private void OnExitCombat()
    {
        Log("🏳️ Saliendo de combate - Liberando lock");
        ReleaseLock();
    }
    
    private void OnNPCEnteredCombat(GameObject npc)
    {
        if (currentTarget == null && playerTransform != null)
        {
            if (Vector3.Distance(npc.transform.position, playerTransform.position) <= maxLockDistance)
            {
                Log($"🎯 Nuevo enemigo '{npc.name}' entró en combate - Haciendo lock");
                SetTarget(npc);
            }
        }
    }
    
    private void OnNPCExitedCombat(GameObject npc)
    {
        if (currentTarget == npc)
        {
            Log($"⚠️ Target actual '{npc.name}' salió de combate - Buscando nuevo objetivo");
            SwitchToNextTarget();
        }
    }
    
    private void SetTarget(GameObject newTarget)
    {
        if (newTarget == null)
        {
            ReleaseLock();
            return;
        }

        currentTarget = newTarget;
        isLockActive = true;

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetLockTarget(newTarget.transform);
        }

        // Mientras el lock-on de cámara está activo, el marker/target de PlayerTargeting debe
        // depender EXCLUSIVAMENTE de IsFacingTarget() (aquí y en EnsureVisualLockSync). Si no se
        // suprime el auto-scan de PlayerTargeting, este reengancha el target usando el FOV de la
        // cámara (que durante el lock-on SIEMPRE apunta al enemigo), pisando el gate y provocando
        // que el marker se muestre aunque el jugador ya no esté mirando hacia el enemigo.
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.SetAutoScanSuppressed(true);
        }

        if (syncWithProjectileTargeting && playerTargeting != null && !_isInDialogue && IsFacingTarget())
        {
            playerTargeting.SetManualTarget(newTarget.transform);
            playerTargeting.ForceVisualRefresh();
        }

        _lastVisualResync = 0f; // Forzar resync en el siguiente Update.
        Log($"🎯 Lock establecido en: {newTarget.name}");
    }
    
    private readonly List<GameObject> _orderedEnemiesCache = new();

    private List<GameObject> GetOrderedEnemies()
    {
        _orderedEnemiesCache.Clear();
        if (playerTransform == null) return _orderedEnemiesCache;

        var raw = ActiveCombatRegistry.GetAllInCombat();
        for (int i = 0; i < raw.Count; i++)
        {
            var g = raw[i];
            if (g != null && g.activeInHierarchy)
                _orderedEnemiesCache.Add(g);
        }

        if (_orderedEnemiesCache.Count <= 1) return _orderedEnemiesCache;

        Vector3 playerFwd = playerTransform.forward;
        playerFwd.y = 0;
        playerFwd.Normalize();
        Vector3 origin = playerTransform.position;

        _orderedEnemiesCache.Sort((a, b) =>
        {
            float angleA = Vector3.SignedAngle(playerFwd, (a.transform.position - origin).normalized, Vector3.up);
            float angleB = Vector3.SignedAngle(playerFwd, (b.transform.position - origin).normalized, Vector3.up);
            return angleA.CompareTo(angleB);
        });

        return _orderedEnemiesCache;
    }
    
    private void SwitchToNextTarget()
    {
        var enemies = GetOrderedEnemies();
        if (enemies.Count == 0) { ReleaseLock(); return; }
        if (enemies.Count == 1) { SetTarget(enemies[0]); return; }
        
        int currentIndex = enemies.IndexOf(currentTarget);
        int nextIndex = (currentIndex + 1) % enemies.Count;
        
        Log($"🔄 RB: Cambiando target → {enemies[nextIndex].name}");
        SetTarget(enemies[nextIndex]);
    }
    
    private void SwitchToPreviousTarget()
    {
        var enemies = GetOrderedEnemies();
        if (enemies.Count == 0) { ReleaseLock(); return; }
        if (enemies.Count == 1) { SetTarget(enemies[0]); return; }
        
        int currentIndex = enemies.IndexOf(currentTarget);
        int previousIndex = (currentIndex - 1 + enemies.Count) % enemies.Count;

        Log($"🔄 LB: Cambiando target → {enemies[previousIndex].name}");
        SetTarget(enemies[previousIndex]);
    }
    
    private void ReleaseLock()
    {
        if (!isLockActive) return;
        
        isLockActive = false;
        currentTarget = null;
        
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.ClearLockTarget();
        }
        
        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            playerTargeting.ClearManualTarget();
            playerTargeting.SetAutoScanSuppressed(false);
        }
        Log("🔓 Lock de cámara liberado");
    }

    private void EnsureVisualLockSync()
    {
        if (currentTarget == null || !currentTarget.activeInHierarchy)
        {
            Log("⚠️ Target lock inválido, liberando lock.");
            ReleaseLock();
            return;
        }

        if (Time.time - _lastVisualResync < visualResyncInterval) return;
        _lastVisualResync = Time.time;

        if (thirdPersonCamera != null && thirdPersonCamera.LockTarget != currentTarget.transform)
        {
            thirdPersonCamera.SetLockTarget(currentTarget.transform);
            Log($"🔁 Resync cámara -> {currentTarget.name}");
        }

        // No sincronizar el marcador durante diálogos para evitar que aparezca
        if (_isInDialogue) return;

        if (syncWithProjectileTargeting && playerTargeting != null)
        {
            if (IsFacingTarget())
            {
                if (!playerTargeting.IsManualTargetActive || playerTargeting.CurrentTarget != currentTarget.transform)
                {
                    playerTargeting.SetManualTarget(currentTarget.transform);
                    Log($"🔁 Resync marcador -> {currentTarget.name}");
                }
                else if (Time.frameCount % 30 == 0)
                {
                    playerTargeting.ForceVisualRefresh();
                }
            }
            else
            {
                if (playerTargeting.IsManualTargetActive)
                {
                    playerTargeting.ClearManualTarget();
                    Log($"↩️ Enemigo fuera de ángulo frontal, ocultando marcador");
                }
            }
        }
    }
    
    private void OnLevitationStarted()
    {
        _suppressedByLevitation = true;
        if (isLockActive)
        {
            Log("🪄 Levitación activa → liberando lock de cámara temporalmente");
            ReleaseLock();
        }
    }

    private void OnLevitationEnded()
    {
        _suppressedByLevitation = false;
        // Forzar re-lock inmediato en el siguiente Update si seguimos en combate
        _lastAutoLockAttempt = 0f;
        Log("🪄 Levitación terminada → restaurando lock de cámara");
    }

    private bool IsFacingTarget()
    {
        if (playerTransform == null || currentTarget == null) return false;
        Vector3 toTarget = currentTarget.transform.position - playerTransform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.001f) return true;
        Vector3 fwd = playerTransform.forward;
        fwd.y = 0f;
        return Vector3.Angle(fwd, toTarget.normalized) <= facingAngleForMarker * 0.5f;
    }

    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log($"[CombatCameraTargeting] {message}");
    }
}
