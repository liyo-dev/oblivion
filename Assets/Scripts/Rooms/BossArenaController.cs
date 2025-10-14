using UnityEngine;
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
    
    private GameObject _barrierVisual;
    private BossArenaBarrier _barrierEffect;
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

    [Header("Bloqueos durante batalla")]
    [Tooltip("Lista de objetos a desactivar al iniciar la batalla y restaurar al finalizar.")]
    [SerializeField] private GameObject[] toDisableDuringBattle;

    private readonly Dictionary<GameObject, bool> _prevActiveStates = new Dictionary<GameObject, bool>();

    bool started = false;

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

    void OnTriggerEnter(Collider other)
    {
        if (started) return;
        if (!other.CompareTag(playerTag)) return;

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

    void OnBossDead(EnemyMarker _)
    {
        // Según el modo, abrir puertas o desactivar barrera
        if (useDoorMode)
        {
            if (doorEast) doorEast.Open();
        }
        else
        {
            UnlockArea();
        }

        if (roomGoal) roomGoal.MarkCleared();

        // Restaurar objetos desactivados durante la batalla
        RestoreBattleDisables();

        // Crea el portal de salida
        if (portalPrefab && portalSpawn)
            Instantiate(portalPrefab, portalSpawn.position, portalSpawn.rotation, transform.parent);

        // Limpieza del evento
        _.onEnemyGone -= OnBossDead;
    }

    private void CreateBarrierVisual()
    {
        if (areaBarrierCollider == null) return;

        // Crear GameObject para la barrera visual
        _barrierVisual = new GameObject("BossArenaBarrier_Visual");
        _barrierVisual.transform.SetParent(transform);
        _barrierVisual.transform.localPosition = Vector3.zero;
        _barrierVisual.transform.localRotation = Quaternion.identity;

        // Obtener dimensiones del collider
        Bounds bounds = areaBarrierCollider.bounds;
        Vector3 size = bounds.size;
        Vector3 center = areaBarrierCollider.transform.InverseTransformPoint(bounds.center);

        // Calcular altura si no está especificada
        float height = barrierHeight > 0 ? barrierHeight : size.y;

        // Crear las 4 paredes de la barrera
        if (areaBarrierCollider is BoxCollider)
        {
            CreateBoxBarrierWalls(center, size, height);
        }
        else
        {
            Debug.LogWarning("[BossArenaController] Solo se soportan BoxCollider para las barreras visuales. Creando barrera genérica.");
            CreateGenericBarrier(bounds, height);
        }
    }

    private void CreateBoxBarrierWalls(Vector3 center, Vector3 size, float height)
    {
        float halfWidth = size.x / 2f;
        float halfDepth = size.z / 2f;

        // Pared Norte (Z+)
        CreateWall("Wall_North", 
            new Vector3(center.x, center.y + height / 2f, center.z + halfDepth),
            new Vector3(size.x, height, barrierThickness));

        // Pared Sur (Z-)
        CreateWall("Wall_South", 
            new Vector3(center.x, center.y + height / 2f, center.z - halfDepth),
            new Vector3(size.x, height, barrierThickness));

        // Pared Este (X+)
        CreateWall("Wall_East", 
            new Vector3(center.x + halfWidth, center.y + height / 2f, center.z),
            new Vector3(barrierThickness, height, size.z));

        // Pared Oeste (X-)
        CreateWall("Wall_West", 
            new Vector3(center.x - halfWidth, center.y + height / 2f, center.z),
            new Vector3(barrierThickness, height, size.z));
    }

    private void CreateWall(string wallName, Vector3 localPosition, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(_barrierVisual.transform);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.transform.localScale = size;

        // Eliminar el collider (no queremos que interfiera)
        Destroy(wall.GetComponent<Collider>());

        // Añadir el efecto visual
        BossArenaBarrier barrierEffect = wall.AddComponent<BossArenaBarrier>();
        
        // Guardar referencia al primer efecto creado
        if (_barrierEffect == null)
        {
            _barrierEffect = barrierEffect;
        }
    }

    private void CreateGenericBarrier(Bounds bounds, float height)
    {
        // Crear una barrera cilíndrica genérica
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wall.name = "GenericBarrier";
        wall.transform.SetParent(_barrierVisual.transform);
        
        Vector3 worldCenter = bounds.center;
        Vector3 localCenter = _barrierVisual.transform.InverseTransformPoint(worldCenter);
        wall.transform.localPosition = localCenter;
        wall.transform.localRotation = Quaternion.identity;
        
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        wall.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);

        // Eliminar el collider
        Destroy(wall.GetComponent<Collider>());

        // Añadir el efecto visual
        _barrierEffect = wall.AddComponent<BossArenaBarrier>();
    }

    private void LockArea()
    {
        _areaLocked = true;

        // Activar la barrera visual
        if (_barrierVisual != null)
        {
            var barriers = _barrierVisual.GetComponentsInChildren<BossArenaBarrier>();
            foreach (var barrier in barriers)
            {
                barrier.Show();
            }
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
            {
                barrier.Hide();
            }
        }

        Debug.Log("[BossArenaController] Área del boss desbloqueada.");
    }

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
