using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using UnityEngine.AI;

public class BossArenaController : MonoBehaviour
{
    [Header("Modo de Arena")]
    [Tooltip("Si es true, usa puertas. Si es false, usa un área delimitada por collider")]
    [SerializeField] private bool useDoorMode = true;

    [Header("Puertas (solo si useDoorMode = true)")]
    public DoorGate doorWest;   // Door_W/Barrier
    public DoorGate doorEast;   // Door_E/Barrier

    [Header("Área Delimitada (solo si useDoorMode = false)")]
    [Tooltip("Collider que delimita el área. Debe ser trigger. Si es null, se usa el collider de este GameObject")]
    [SerializeField] private Collider areaBarrierCollider;
    [Tooltip("Altura de la barrera visual. Si es 0, se calcula automáticamente")]
    [SerializeField] private float barrierHeight = 10f;
    [Tooltip("Grosor de la barrera visual")]
    [SerializeField] private float barrierThickness = 0.5f;

    [Header("Visual (Build-safe)")]
    [Tooltip("Material asset con tu shader URP (precompilado). No generar shaders en runtime.")]
    [SerializeField] private Material barrierMaterial;   // Asigna tu .mat
    [SerializeField] private Color barrierColor = new(0.3f, 0.5f, 1f, 0.25f);
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float pulseIntensity = 0.3f;
    [SerializeField] private float rimPower = 2f;
    [SerializeField] private float rimStrength = 1.5f;
    [SerializeField] private Vector2 noiseTiling = new(2, 2);
    [SerializeField] private Vector2 noiseSpeed = new(0.2f, 0f);
    [SerializeField] private float noiseStrength = 1f;
    [SerializeField] private float globalAlpha = 1f;

    private GameObject _barrierVisual;
    private BossArenaBarrier _barrierEffect; // referencia al primero para batch (Show/Hide)
    private bool _areaLocked = false;

    [Header("Spawn")]
    public Transform bossSpawn;
    public GameObject bossPrefab;   // tu boss
    public Transform portalSpawn;
    public GameObject portalPrefab; // PF_PortalExit
    [Tooltip("Layer del suelo para colocar al boss después de la presentación.")]
    [SerializeField] private LayerMask floorLayer = 1 << 6; // Floor por defecto
    
    [Header("VFX de Aparición del Boss")]
    [Tooltip("Prefab de VFX para la aparición del boss (portal, humo, etc). Se destruye automáticamente.")]
    [SerializeField] private GameObject bossSpawnVFXPrefab;
    [Tooltip("Duración en segundos del VFX de aparición antes de destruirse.")]
    [SerializeField] private float bossSpawnVFXDuration = 2f;
    [Tooltip("Offset vertical para el VFX de aparición (por defecto 0).")]
    [SerializeField] private float bossSpawnVFXHeightOffset = 0f;
    
    [Header("Boss Presentation")]
    [Tooltip("Componente opcional para presentación cinemática del boss")]
    [SerializeField] private BossIntroPresentation bossIntroPresentation;
    
    [Tooltip("Nombre del boss para mostrar en la presentación (ej: 'DEMONIO')")]
    [SerializeField] private string bossDisplayName = "DEMONIO";

    [Header("Refs")]
    public RoomGoal roomGoal;
    public string playerTag = "Player";
    public AudioSource musicBoss;    // opcional

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    [Header("Progreso")]
    [Tooltip("ID único del boss para registrar su derrota en el guardado (ej: 'DEMONIO_01').")]
    [SerializeField] private string bossId;

    [Header("Battle Identification")]
    [Tooltip("ID único de la arena/batalla para poder activarla por referencia (ej: 'BATTLE_DEMON_01'). Si está vacío, no se registrará.")]
    [SerializeField] private string battleId;

    // Si no quieres mantener dos ids, por defecto usamos bossId como battleId cuando éste está vacío.
#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(battleId) && !string.IsNullOrEmpty(bossId))
        {
            battleId = bossId;
        }
    }
#endif

    // Registro estático para buscar arenas por id desde otros sistemas (p.ej. la cinematica)
    private static readonly System.Collections.Generic.Dictionary<string, BossArenaController> s_arenaRegistry = new();

    // Eventos globales para que otros sistemas (ej: minimapa) reaccionen al inicio/fin de batalla
    public static event Action OnAnyBattleStarted;
    public static event Action OnAnyBattleEnded;

    // Exponer el id públicamente de solo lectura
    public string BattleId => battleId;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onBossDefeated;

    [Header("Bloqueos durante batalla")]
    [Tooltip("Lista de objetos a desactivar al iniciar la batalla y restaurar al finalizar.")]
    [SerializeField] private GameObject[] toDisableDuringBattle;

    private readonly Dictionary<GameObject, bool> _prevActiveStates = new();

    [Header("Activacion manual")]
    [Tooltip("Cuando es true, al entrar el jugador se inicia la batalla (verifica si el boss ya fue derrotado).")]
    [SerializeField] private bool startBarrierOnPlayerEnter;

    bool started = false;
    bool _bossDefeatHandled = false;
    bool _bossDeathConfirmed = false;
    bool _pendingStartBattle = false;
    bool _profileReady = false; // ✅ Flag para rastrear si el perfil está listo
    EnemyMarker _activeBossMarker;
    Damageable _activeBossDamageable;

    public void SetStartBarrierOnPlayerEnter(bool value)
    {
        startBarrierOnPlayerEnter = value;
    }

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
        // Runtime fallback: si no hay battleId explícito, usar bossId para registrar la arena
        if (string.IsNullOrEmpty(battleId) && !string.IsNullOrEmpty(bossId))
        {
            battleId = bossId;
        }

        // Si no se especifica un collider de barrera, usar el del propio GameObject
        if (!useDoorMode && areaBarrierCollider == null)
        {
            areaBarrierCollider = GetComponent<Collider>();
        }

        // Crear la barrera visual si estamos en modo área
        if (!useDoorMode && areaBarrierCollider != null)
        {
            CreateBarrierVisual();
        }

        // Registrar en el diccionario por battleId si procede
        if (!string.IsNullOrEmpty(battleId))
        {
            if (s_arenaRegistry.TryGetValue(battleId, out var existing) && existing != this)
            {
                Debug.LogWarning($"[BossArenaController] BattleId '{battleId}' ya registrado por otro BossArenaController. Sobrescribiendo registro.");
            }
            s_arenaRegistry[battleId] = this;
            // Diagnostic log: confirmar registro en runtime (útil para debugging)
            Debug.Log($"[BossArenaController] Registrada arena con BattleId='{battleId}' en scene='{gameObject.scene.name}' (active={gameObject.activeInHierarchy}, enabled={this.enabled}).");
        }
    }

    void OnEnable()
    {
        BossProgressTracker.OnProgressRestored += HandleBossProgressRestored;
        GameBootService.OnProfileReady += HandleProfileReady; // ✅ Suscribirse a OnProfileReady
        ProfileReadyDiagnostics.RegisterSubscriber(nameof(BossArenaController)); // 🔍 Registrar para diagnósticos

        // Asegurar registro en OnEnable (por si el orden de Awake/Start causa que el registro aún no exista
        // cuando otros sistemas intenten activarlo). Normalizamos la clave.
        if (!string.IsNullOrEmpty(battleId) || !string.IsNullOrEmpty(bossId))
        {
            var key = (!string.IsNullOrEmpty(battleId) ? battleId : bossId)?.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                if (s_arenaRegistry.TryGetValue(key, out var existing) && existing != this)
                {
                    Debug.LogWarning($"[BossArenaController] OnEnable: BattleId '{key}' ya registrado por otro BossArenaController. Sobrescribiendo registro.");
                }
                s_arenaRegistry[key] = this;
                //Debug.Log($"[BossArenaController] OnEnable: Registrada arena con BattleId='{key}' (scene='{gameObject.scene.name}', active={gameObject.activeInHierarchy}).");
            }
        }

        // ✅ Verificar si el perfil ya está disponible (caso de reinicio de escena)
        if (GameBootService.IsAvailable && GameBootService.Profile != null)
        {
            HandleProfileReady();
        }

        // Si el inicio de batalla fue solicitado mientras el componente estaba inactivo,
        // arranquemos la batalla ahora que estamos habilitados.
        if (_pendingStartBattle)
        {
            _pendingStartBattle = false;
            if (showDebugLogs) Debug.Log("[BossArenaController] OnEnable: procesando StartBattle pendiente.");
            StartBattleInternal();
        }
    }

    void OnDisable()
    {
        BossProgressTracker.OnProgressRestored -= HandleBossProgressRestored;
        GameBootService.OnProfileReady -= HandleProfileReady; // ✅ Desuscribirse de OnProfileReady
        CleanupBossSubscriptions();

        // Desregistrar si estaba registrado
        if (!string.IsNullOrEmpty(battleId))
        {
            if (s_arenaRegistry.TryGetValue(battleId, out var existing) && existing == this)
            {
                s_arenaRegistry.Remove(battleId);
                Debug.Log($"[BossArenaController] Desregistrada arena con BattleId='{battleId}' en OnDisable.");
            }
        }
    }

    void Start()
    {
        Debug.Log($"[BossArenaController] 🎬 Start() - Arena BattleId='{battleId}', BossId='{bossId}', ProfileReady={_profileReady}");
        
        // ✅ CRÍTICO: Solo verificar el estado del boss si el perfil ya está listo
        // Si no está listo, HandleProfileReady() lo verificará cuando se dispare OnProfileReady
        if (_profileReady)
        {
            CheckBossStateAndApply();
        }
        else
        {
            Debug.Log($"[BossArenaController] ⏳ Esperando a que el perfil esté listo para verificar el estado del boss");
        }
    }

    /// <summary>
    /// Handler para OnProfileReady - se ejecuta cuando el perfil está completamente cargado y listo
    /// </summary>
    void HandleProfileReady()
    {
        if (_profileReady)
        {
            Debug.Log($"[BossArenaController] ⚠️ HandleProfileReady llamado múltiples veces para '{bossId}' - ignorando");
            return;
        }
        
        _profileReady = true;
        Debug.Log($"[BossArenaController] ✅ Perfil listo para '{bossId}' - verificando estado del boss");
        
        // Desuscribirse para no recibir más notificaciones
        GameBootService.OnProfileReady -= HandleProfileReady;
        
        // Verificar el estado del boss ahora que los datos están cargados
        CheckBossStateAndApply();
    }

    /// <summary>
    /// Verifica el estado del boss y aplica la configuración correspondiente
    /// </summary>
    void CheckBossStateAndApply()
    {
        bool isDefeated = IsBossAlreadyDefeated();
        Debug.Log($"[BossArenaController] 🔍 IsBossAlreadyDefeated() = {isDefeated} para BossId='{bossId}'");
        
        if (isDefeated)
        {
            Debug.Log($"[BossArenaController] ✅ Boss '{bossId}' ya fue derrotado - desbloqueando área sin spawnearlo");
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
        }
        else
        {
            Debug.Log($"[BossArenaController] ⚔️ Boss '{bossId}' NO ha sido derrotado - esperando trigger del player");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[BossArenaController] 🚪 OnTriggerEnter - Tag: {other.tag}, Started: {started}, BossDefeated: {_bossDefeatHandled}, StartOnEnter: {startBarrierOnPlayerEnter}");
        
        if (started || _bossDefeatHandled) return;
        if (!other.CompareTag(playerTag)) return;
        if (!startBarrierOnPlayerEnter) 
        {
            Debug.Log($"[BossArenaController] ⏸️ StartBarrierOnPlayerEnter está desactivado - no se inicia batalla automáticamente");
            return;
        }

        bool isDefeated = IsBossAlreadyDefeated();
        Debug.Log($"[BossArenaController] 🔍 Player entró al trigger - IsBossAlreadyDefeated={isDefeated}");
        
        if (isDefeated)
        {
            Debug.Log($"[BossArenaController] ✅ Boss ya derrotado - aplicando estado cleared");
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
            return;
        }

        Debug.Log($"[BossArenaController] ⚔️ Iniciando batalla con boss '{bossId}'");
        // Usamos el método centralizado para iniciar la batalla
        StartBattleInternal();
    }

    // Método público para permitir que la cinemática u otros sistemas inicien la batalla
    // Se puede enlazar directamente al UnityEvent onCinematicFinished (sin parámetros)
    public void TriggerStartBattle()
    {
        if (started || _bossDefeatHandled) return;

        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
            return;
        }

        StartBattleInternal();
    }

    // Lógica centralizada para arrancar la batalla (refactorizada desde OnTriggerEnter)
    private void StartBattleInternal()
    {
        if (started) return;

        // If this component is not active/enabled, defer starting the battle until OnEnable.
        if (!isActiveAndEnabled)
        {
            _pendingStartBattle = true;
            if (showDebugLogs) Debug.Log("[BossArenaController] StartBattleInternal deferred: component inactive; will start on OnEnable.");
            return;
        }

        started = true;
        _bossDeathConfirmed = false;

        OnAnyBattleStarted?.Invoke();

        // Puertas o barrera
        if (useDoorMode)
        {
            if (doorWest) doorWest.Close();
        }
        else
        {
            LockArea();
        }

        // Desactivar objetos durante la batalla
        ApplyBattleDisables();

        var id = !string.IsNullOrEmpty(battleId) ? battleId : bossId;
        if (!string.IsNullOrEmpty(id))
        {
            // Señal (si está a tiempo)
            DefaultNarrativeSignals.Instance?.RaiseCustom($"BATTLE_START:{id}", name);

            // Fallback directo (si el wiring llega tarde)
            if (AudioService.Instance != null)
                AudioService.Instance.BeginBattleById(id);
        }

        SpawnBoss();
    }

    private void SpawnBoss()
    {
        Vector3 spawnPosition = bossSpawn ? bossSpawn.position : transform.position;
        Quaternion spawnRotation = bossSpawn ? bossSpawn.rotation : transform.rotation;

        // Instanciar portal de aparición si está configurado
        GameObject portalVFX = null;
        if (portalPrefab != null && portalSpawn != null)
        {
            Vector3 portalPosition = portalSpawn.position;
            // Calcular posición del portal en el suelo usando raycast
            if (Physics.Raycast(portalSpawn.position, Vector3.down, out RaycastHit hit, 100f, floorLayer))
            {
                portalPosition = hit.point;
                if (showDebugLogs)
                    Debug.Log($"[BossArenaController] Portal de aparición colocado en el suelo: {portalPosition}");
            }
            portalVFX = Instantiate(portalPrefab, portalPosition, portalSpawn.rotation, transform.parent);
            
            if (showDebugLogs)
                Debug.Log($"[BossArenaController] Portal de aparición instanciado en {portalPosition}");
        }

        // Instanciar VFX de aparición adicional si está configurado
        if (bossSpawnVFXPrefab != null)
        {
            Vector3 vfxPosition = spawnPosition + Vector3.up * bossSpawnVFXHeightOffset;
            GameObject vfx = Instantiate(bossSpawnVFXPrefab, vfxPosition, spawnRotation);
            Destroy(vfx, bossSpawnVFXDuration);
            if (showDebugLogs)
                Debug.Log($"[BossArenaController] VFX de aparición instanciado en {vfxPosition}, se destruirá en {bossSpawnVFXDuration}s");
        }

        GameObject boss = null;

        if (bossPrefab)
            boss = Instantiate(bossPrefab, spawnPosition, spawnRotation, transform.parent);
        else
            boss = FindExistingBossInRoom();

        if (!boss)
        {
            Debug.LogError("[BossArenaController] No hay boss para esta sala.");
            started = false;
            
            // Destruir el portal si no hay boss
            if (portalVFX != null)
                Destroy(portalVFX);
            
            return;
        }

        // Destruir el portal después de la duración del VFX
        if (portalVFX != null)
        {
            Destroy(portalVFX, bossSpawnVFXDuration);
            if (showDebugLogs)
                Debug.Log($"[BossArenaController] Portal se destruirá en {bossSpawnVFXDuration}s");
        }

        // Preparar referencias para escuchar la derrota real del boss
        _activeBossMarker = boss.GetComponent<EnemyMarker>() ?? boss.AddComponent<EnemyMarker>();
        _activeBossMarker.onEnemyGone -= OnBossDead;
        _activeBossMarker.onEnemyGone += OnBossDead;

        _activeBossDamageable = boss.GetComponent<Damageable>();
        if (_activeBossDamageable != null)
        {
            _activeBossDamageable.OnDied -= HandleBossDamageableDied;
            _activeBossDamageable.OnDied += HandleBossDamageableDied;
        }

        // Iniciar presentación o colocar directamente en el suelo
        if (bossIntroPresentation != null)
        {
            Debug.Log($"[BossArenaController] ✅ BossIntroPresentation asignado para '{boss.name}'. Iniciando presentación...");
            StartCoroutine(PlayPresentationAndPlaceBoss(boss));
        }
        else
        {
            Debug.Log($"[BossArenaController] ⚠️ No hay BossIntroPresentation asignado para '{boss.name}'. Colocando boss directamente.");
            PlaceBossOnFloor(boss);
            EnableBossCombat(boss);
        }
    }

    private IEnumerator PlayPresentationAndPlaceBoss(GameObject boss)
    {
        // Buscar la cámara hija del boss
        Camera bossCamera = boss.GetComponentInChildren<Camera>(true); // includeInactive = true
        
        if (bossCamera == null)
        {
            Debug.LogWarning($"[BossArenaController] ❌ No se encontró cámara en el boss '{boss.name}' (ni activa ni inactiva). Saltando presentación.");
            PlaceBossOnFloor(boss);
            EnableBossCombat(boss);
            yield break;
        }
        
        Debug.Log($"[BossArenaController] 📷 Cámara encontrada: '{bossCamera.name}' en boss '{boss.name}'");
        
        // Configurar y reproducir presentación
        bossIntroPresentation.SetupBoss(boss.transform, bossCamera, bossDisplayName);
        yield return StartCoroutine(bossIntroPresentation.PlayIntroduction());
        
        // Después de la presentación:
        // 1. Desactivar la cámara del boss
        bossCamera.gameObject.SetActive(false);
        
        // 2. Colocar en el suelo
        PlaceBossOnFloor(boss);
        
        // 3. Activar combate del boss
        EnableBossCombat(boss);
    }

    private void EnableBossCombat(GameObject boss)
    {
        // Activar el combate en el componente de IA del boss (si existe)
        var impDemonAI = boss.GetComponent<ImpDemonAI>();
        if (impDemonAI != null)
        {
            impDemonAI.canStartCombat = true;
            Debug.Log("[BossArenaController] Combate del boss (ImpDemon) activado.");
            return;
        }
        
        var golemBossAI = boss.GetComponent<GolemBossAI>();
        if (golemBossAI != null)
        {
            golemBossAI.canStartCombat = true;
            Debug.Log("[BossArenaController] Combate del boss (Golem) activado.");
            return;
        }
        
        Debug.LogWarning("[BossArenaController] No se encontró componente de IA compatible en el boss.");
    }

    private void PlaceBossOnFloor(GameObject boss)
    {
        // Raycast hacia abajo para encontrar el suelo
        if (Physics.Raycast(boss.transform.position, Vector3.down, out RaycastHit hit, 100f, floorLayer))
        {
            boss.transform.position = hit.point;
        }
        else
        {
            Debug.LogWarning($"[BossArenaController] No se encontró Floor debajo del boss en {boss.transform.position}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Si el área está bloqueada y el jugador intenta salir, empujarlo de vuelta
        if (!useDoorMode && _areaLocked && other.CompareTag(playerTag))
        {
            StartCoroutine(PushPlayerBack(other.transform));
        }
    }

    private System.Collections.IEnumerator PushPlayerBack(Transform player)
    {
        // Esperar un frame para evitar conflictos
        yield return null;

        // Empujar al jugador hacia el centro del área
        if (areaBarrierCollider != null)
        {
            Vector3 center = areaBarrierCollider.bounds.center;
            Vector3 direction = (center - player.position).normalized;

            // Teleportar ligeramente hacia dentro
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc)
            {
                cc.enabled = false;
                player.position += direction * 2f;
                cc.enabled = true;
            }
            else
            {
                player.position += direction * 2f;
            }

            Debug.Log("[BossArenaController] Jugador empujado de vuelta al área del boss.");
        }
    }

    GameObject FindExistingBossInRoom()
    {
        // busca un EnemyMarker ya colocado como hijo de la sala
        var markers = GetComponentsInParent<RoomGoal>(true);
        if (markers.Length > 0)
        {
            var roomRoot = markers[0].transform;
            var existing = roomRoot.GetComponentInChildren<EnemyMarker>(true);
            return existing ? existing.gameObject : null;
        }
        return null;
    }

    void OnBossDead(EnemyMarker marker)
    {
        if (marker != null)
            marker.onEnemyGone -= OnBossDead;

        if (_bossDefeatHandled)
        {
            CleanupBossSubscriptions();
            return;
        }

        if (!_bossDeathConfirmed)
        {
            // El boss fue destruido sin haber sido derrotado (p.ej. cambio de escena tras Game Over).
            CleanupBossSubscriptions();
            started = false;
            return;
        }
        
        ApplyBossClearedState(invokeUnityEvents: true, markDefeatedInTracker: true);
    }

    void HandleBossDamageableDied()
    {
        _bossDeathConfirmed = true;
        ApplyBossClearedState(invokeUnityEvents: true, markDefeatedInTracker: true);
    }

    void CleanupBossSubscriptions()
    {
        if (_activeBossMarker != null)
        {
            _activeBossMarker.onEnemyGone -= OnBossDead;
            _activeBossMarker = null;
        }

        if (_activeBossDamageable != null)
        {
            _activeBossDamageable.OnDied -= HandleBossDamageableDied;
            _activeBossDamageable = null;
        }
    }

    void HandleBossProgressRestored()
    {
        if (_bossDefeatHandled) return;
        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false, raiseSignals: false);
        }
    }

    bool IsBossAlreadyDefeated()
    {
        if (string.IsNullOrEmpty(bossId))
        {
            Debug.Log($"[BossArenaController] ⚠️ IsBossAlreadyDefeated: bossId está vacío para battleId='{battleId}'");
            return false;
        }
        
        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            bool defeated = tracker.IsDefeated(bossId);
            var allDefeated = tracker.DefeatedBossIds;
            Debug.Log($"[BossArenaController] 🔍 Tracker encontrado - BossId='{bossId}', IsDefeated={defeated}, DefeatedBossIds=[{string.Join(", ", allDefeated)}]");
            return defeated;
        }
        
        Debug.Log($"[BossArenaController] ⚠️ BossProgressTracker no encontrado - asumiendo boss NO derrotado");
        return false;
    }

    void ApplyBossClearedState(bool invokeUnityEvents, bool markDefeatedInTracker, bool raiseSignals = true)
    {
        if (_bossDefeatHandled) return;
        _bossDefeatHandled = true;
        started = true;

        CleanupBossSubscriptions();

        if (useDoorMode)
        {
            if (doorWest) doorWest.Open();
            if (doorEast) doorEast.Open();
        }
        else
        {
            UnlockArea();
        }

        if (musicBoss && musicBoss.isPlaying)
        {
            musicBoss.Stop();
        }

        if (markDefeatedInTracker && !string.IsNullOrEmpty(bossId))
        {
            BossProgressTracker.Instance.MarkDefeated(bossId);
        }

        if (roomGoal) roomGoal.MarkCleared();

        if (invokeUnityEvents)
        {
            onBossDefeated?.Invoke();
        }

        // Emitir la señal del grafo para indicar que la batalla/arena ha sido ganada
        // SOLO si raiseSignals=true (evita sonar música al cargar partida)
        if (raiseSignals)
        {
            OnAnyBattleEnded?.Invoke();

            try
            {
                // Usar BattleId (fallback a bossId ya fue aplicado en Awake)
                DefaultNarrativeSignals.Instance?.RaiseBattleWon(BattleId);
                
                if (!string.IsNullOrEmpty(BattleId) && AudioService.Instance != null)
                    AudioService.Instance.EndBattleById(BattleId);

            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BossArenaController] Error notificando BattleWon: {ex.Message}");
            }
        }

        RestoreBattleDisables();
        // Portal ya no se spawneea al final, solo al inicio de la batalla
    }

    // =========================== Visual Barrier ===========================

    private void CreateBarrierVisual()
    {
        if (areaBarrierCollider == null) return;

        // Crear GameObject contenedor
        _barrierVisual = new GameObject("BossArenaBarrier_Visual");
        _barrierVisual.transform.SetParent(transform);
        _barrierVisual.transform.localPosition = Vector3.zero;
        _barrierVisual.transform.localRotation = Quaternion.identity;

        // Dimensiones del collider (en mundo)
        Bounds bounds = areaBarrierCollider.bounds;
        Vector3 size = bounds.size;
        Vector3 centerWorld = bounds.center;
        Vector3 centerLocal = _barrierVisual.transform.InverseTransformPoint(centerWorld);

        // Altura efectiva
        float height = barrierHeight > 0 ? barrierHeight : size.y;

        // Crear paredes según tipo de collider
        if (areaBarrierCollider is BoxCollider)
        {
            CreateBoxBarrierWalls(centerLocal, size, height);
        }
        else
        {
            Debug.LogWarning("[BossArenaController] Solo se soportan BoxCollider para las barreras visuales. Creando barrera genérica.");
            CreateGenericBarrier(bounds, height);
        }
    }

    private void CreateBoxBarrierWalls(Vector3 centerLocal, Vector3 worldSize, float height)
    {
        float halfWidth = worldSize.x * 0.5f;
        float halfDepth = worldSize.z * 0.5f;

        // Pared Norte (Z+)
        CreateWall("Wall_North",
            new Vector3(centerLocal.x, centerLocal.y + height * 0.5f, centerLocal.z + halfDepth),
            new Vector3(worldSize.x, height, barrierThickness));

        // Pared Sur (Z-)
        CreateWall("Wall_South",
            new Vector3(centerLocal.x, centerLocal.y + height * 0.5f, centerLocal.z - halfDepth),
            new Vector3(worldSize.x, height, barrierThickness));

        // Pared Este (X+)
        CreateWall("Wall_East",
            new Vector3(centerLocal.x + halfWidth, centerLocal.y + height * 0.5f, centerLocal.z),
            new Vector3(barrierThickness, height, worldSize.z));

        // Pared Oeste (X-)
        CreateWall("Wall_West",
            new Vector3(centerLocal.x - halfWidth, centerLocal.y + height * 0.5f, centerLocal.z),
            new Vector3(barrierThickness, height, worldSize.z));
    }

    private void CreateWall(string wallName, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(_barrierVisual.transform, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = size;

        // Eliminar collider (no interactúa)
        var col = wall.GetComponent<Collider>();
        if (col) Destroy(col);

        // Efecto visual (build-safe, NO compila shaders en runtime)
        var barrierEffect = wall.AddComponent<BossArenaBarrier>();
        barrierEffect.Setup(
            barrierMaterial,
            barrierColor,
            pulseSpeed,
            pulseIntensity,
            rimPower,
            rimStrength,
            noiseTiling,
            noiseSpeed,
            noiseStrength,
            globalAlpha
        );

        // Guardar el primero para batch de Show/Hide (luego cogemos todos con GetComponentsInChildren)
        if (_barrierEffect == null) _barrierEffect = barrierEffect;
    }

    private void CreateGenericBarrier(Bounds boundsWorld, float height)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wall.name = "GenericBarrier";
        wall.transform.SetParent(_barrierVisual.transform, false);

        Vector3 centerWorld = boundsWorld.center;
        Vector3 localCenter = _barrierVisual.transform.InverseTransformPoint(centerWorld);
        wall.transform.localPosition = localCenter;
        wall.transform.localRotation = Quaternion.identity;

        float radius = Mathf.Max(boundsWorld.extents.x, boundsWorld.extents.z);
        wall.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

        var col = wall.GetComponent<Collider>();
        if (col) Destroy(col);

        _barrierEffect = wall.AddComponent<BossArenaBarrier>();
        _barrierEffect.Setup(
            barrierMaterial,
            barrierColor,
            pulseSpeed,
            pulseIntensity,
            rimPower,
            rimStrength,
            noiseTiling,
            noiseSpeed,
            noiseStrength,
            globalAlpha
        );
    }

    private void LockArea()
    {
        _areaLocked = true;

        // Activar la barrera visual
        if (_barrierVisual != null)
        {
            var barriers = _barrierVisual.GetComponentsInChildren<BossArenaBarrier>();
            foreach (var barrier in barriers)
                barrier.Show();
        }

        Debug.Log("[BossArenaController] Área del boss bloqueada.");
    }

    private void UnlockArea()
    {
        _areaLocked = false;

        // Desactivar la barrera visual
        if (_barrierVisual != null)
        {
            var barriers = _barrierVisual.GetComponentsInChildren<BossArenaBarrier>();
            foreach (var barrier in barriers)
                barrier.Hide();
        }

        Debug.Log("[BossArenaController] Área del boss desbloqueada.");
    }

    // =========================== Battle toggles ===========================

    private void ApplyBattleDisables()
    {
        _prevActiveStates.Clear();
        if (toDisableDuringBattle == null || toDisableDuringBattle.Length == 0) return;
        foreach (var go in toDisableDuringBattle)
        {
            if (!go) continue;
            if (!_prevActiveStates.ContainsKey(go))
                _prevActiveStates.Add(go, go.activeSelf);
            go.SetActive(false);
        }
    }

    private void RestoreBattleDisables(bool skipObjectsInSameScene = false)
    {
        if (_prevActiveStates.Count == 0) return;
        foreach (var kvp in _prevActiveStates)
        {
            var go = kvp.Key;
            if (!go) continue;
            if (skipObjectsInSameScene && go.scene == gameObject.scene)
                continue;
            go.SetActive(kvp.Value);
        }
        _prevActiveStates.Clear();
    }

    void OnDestroy()
    {
        // Restaurar objetos desactivados si quedara algo pendiente.
        // Si la escena se estǭ descargando, los objetos del mismo scene tambiǭn se destruyen,
        // y Unity lanza un error al intentar reactivarlos.
        bool sceneStillLoaded = gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        RestoreBattleDisables(skipObjectsInSameScene: !sceneStillLoaded);
        CleanupBossSubscriptions();

        // Limpiar la barrera visual si fue creada
        if (_barrierVisual != null)
        {
            Destroy(_barrierVisual);
        }

        // Asegurar desregistro final
        if (!string.IsNullOrEmpty(battleId))
        {
            if (s_arenaRegistry.TryGetValue(battleId, out var existing) && existing == this)
            {
                s_arenaRegistry.Remove(battleId);
                Debug.Log($"[BossArenaController] Desregistrada arena con BattleId='{battleId}' en OnDestroy.");
            }
        }
    }

    // =================== Static registry helpers ===================
    public static bool TryTriggerBattleById(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        // Normalizar (quitar espacios que puedan venir de editores o importaciones)
        var key = id.Trim();
        if (string.IsNullOrEmpty(key)) return false;

        // Lookup directo por key normalizada
        if (s_arenaRegistry.TryGetValue(key, out var arena) && arena != null)
        {
            arena.TriggerStartBattle();
            return true;
        }

        // Fallback: intentar búsqueda case-insensitive / trim sobre las keys registradas
        try
        {
            foreach (var kvp in s_arenaRegistry)
            {
                if (string.Equals(kvp.Key?.Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    if (kvp.Value != null)
                    {
                        Debug.LogWarning($"[BossArenaController] TryTriggerBattleById: Lookup directo falló para '{id}', usando fallback con key registrada '{kvp.Key}'.");
                        kvp.Value.TriggerStartBattle();
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BossArenaController] Error durante fallback de búsqueda de BattleId '{id}': {ex.Message}");
        }

        // Diagnostic: si no se encuentra, mostrar los ids registrados (útil para debugging)
        try
        {
            if (s_arenaRegistry.Count == 0)
            {
                Debug.LogWarning($"[BossArenaController] Intento de activar BattleId '{id}' pero el registry está vacío.");
            }
            else
            {
                var keys = string.Join(", ", s_arenaRegistry.Keys);
                Debug.LogWarning($"[BossArenaController] Intento de activar BattleId '{id}' pero no se encontró. Keys registradas: {keys}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BossArenaController] Error al listar ids registrados: {ex.Message}");
        }

        return false;
    }

    public static bool TryGetById(string id, out BossArenaController arena)
    {
        arena = null;
        if (string.IsNullOrEmpty(id)) return false;
        return s_arenaRegistry.TryGetValue(id, out arena) && arena != null;
    }
}
