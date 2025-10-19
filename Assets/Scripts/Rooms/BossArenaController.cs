using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

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

    [Header("Refs")]
    public RoomGoal roomGoal;
    public string playerTag = "Player";
    public AudioSource musicBoss;    // opcional

    [Header("Progreso")]
    [Tooltip("ID único del boss para registrar su derrota en el guardado (ej: 'DEMONIO_01').")]
    [SerializeField] private string bossId;

    [Header("Eventos")]
    [SerializeField] private UnityEvent onBossDefeated;

    [Header("Bloqueos durante batalla")]
    [Tooltip("Lista de objetos a desactivar al iniciar la batalla y restaurar al finalizar.")]
    [SerializeField] private GameObject[] toDisableDuringBattle;

    private readonly Dictionary<GameObject, bool> _prevActiveStates = new();

    bool started = false;
    bool _bossDefeatHandled = false;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void Awake()
    {
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
    }

    void OnEnable()
    {
        BossProgressTracker.OnProgressRestored += HandleBossProgressRestored;
    }

    void OnDisable()
    {
        BossProgressTracker.OnProgressRestored -= HandleBossProgressRestored;
    }

    void Start()
    {
        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (started || _bossDefeatHandled) return;
        if (!other.CompareTag(playerTag)) return;

        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false);
            return;
        }

        started = true;

        // Según el modo, activar puertas o barrera
        if (useDoorMode)
        {
            // Modo puertas: cerrar la entrada
            if (doorWest) doorWest.Close();
        }
        else
        {
            // Modo área: activar la barrera y bloquear salida
            LockArea();
        }

        // Desactivar objetos de la lista durante la batalla
        ApplyBattleDisables();

        if (musicBoss) musicBoss.Play();

        GameObject boss = null;

        if (bossPrefab && bossSpawn)
            boss = Instantiate(bossPrefab, bossSpawn.position, bossSpawn.rotation, transform.parent);
        else
            boss = FindExistingBossInRoom(); // por si ya lo dejaste colocado

        if (!boss)
        {
            Debug.LogError("[BossArenaController] No hay boss para esta sala.");
            return;
        }

        // Asegura un EnemyMarker para detectar la muerte (se dispara en OnDestroy)
        var marker = boss.GetComponent<EnemyMarker>();
        if (!marker) marker = boss.AddComponent<EnemyMarker>();
        marker.onEnemyGone += OnBossDead;
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
        {
            marker.onEnemyGone -= OnBossDead;
        }

        ApplyBossClearedState(invokeUnityEvents: true, markDefeatedInTracker: true);
    }

    void HandleBossProgressRestored()
    {
        if (_bossDefeatHandled) return;
        if (IsBossAlreadyDefeated())
        {
            ApplyBossClearedState(invokeUnityEvents: false, markDefeatedInTracker: false);
        }
    }

    bool IsBossAlreadyDefeated()
    {
        if (string.IsNullOrEmpty(bossId)) return false;
        if (BossProgressTracker.TryGetInstance(out var tracker))
        {
            return tracker.IsDefeated(bossId);
        }
        return false;
    }

    void ApplyBossClearedState(bool invokeUnityEvents, bool markDefeatedInTracker)
    {
        if (_bossDefeatHandled) return;
        _bossDefeatHandled = true;
        started = true;

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

        RestoreBattleDisables();
        SpawnExitPortalIfNeeded();
    }

    void SpawnExitPortalIfNeeded()
    {
        if (!portalPrefab || !portalSpawn) return;
        if (HasExistingPortal()) return;

        Instantiate(portalPrefab, portalSpawn.position, portalSpawn.rotation, transform.parent);
    }

    bool HasExistingPortal()
    {
        if (!portalPrefab) return false;
        var parent = transform.parent;
        if (!parent) return false;

        string prefabName = portalPrefab.name;
        foreach (Transform child in parent)
        {
            if (!child) continue;
            if (child.name.StartsWith(prefabName, StringComparison.Ordinal))
                return true;
        }
        return false;
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

    private void RestoreBattleDisables()
    {
        if (_prevActiveStates.Count == 0) return;
        foreach (var kvp in _prevActiveStates)
        {
            var go = kvp.Key;
            if (!go) continue;
            go.SetActive(kvp.Value);
        }
        _prevActiveStates.Clear();
    }

    void OnDestroy()
    {
        // Restaurar objetos desactivados si quedara algo pendiente
        RestoreBattleDisables();

        // Limpiar la barrera visual si fue creada
        if (_barrierVisual != null)
        {
            Destroy(_barrierVisual);
        }
    }
}


