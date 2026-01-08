using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PressureRoomBuilder : MonoBehaviour
{
    [Header("Puertas (si no asignas, se buscan por nombre)")]
    public Transform doorW; // "Door_W"
    public Transform doorE; // "Door_E"

    [Header("Prefabs")]
    public GameObject platePrefab;            // placa con o sin PressurePlate (se añade si falta)
    public GameObject pushablePrefab;         // tronco/roca empujable (opcional)

    [Header("Contenedores (hijos opc.)")]
    public Transform platesContainer;         // se creará "Plates" si está vacío
    public Transform propsContainer;          // se creará "Props" si está vacío

    [Header("Distribución de placas")]
    [Range(1,6)] public int plateCount = 2;
    public float corridorWidth = 8f;          // ancho útil (perpendicular a W→E)
    public float doorClearance = 3f;          // margen desde cada puerta
    public float minSeparation = 3.0f;        // distancia mínima entre placas
    public bool snapToGround = true;
    public float raycastHeight = 6f;
    public LayerMask groundMask = ~0;

    [Header("Placas - parámetros por defecto")]
    public bool onlyPickupObjects = true;        // Nueva API
    public float minimumMass = 0.1f;             // Nueva API
    public float sinkAmount = 0.2f;              // Nueva API
    public float animationSpeed = 5f;            // Nueva API

    [Header("Empujables (opcional)")]
    public bool spawnPushables = false;
    public int pushableCount = 2;
    public float pushableMass = 20f;
    public bool freezePushableRotXZ = true;

    [Header("Auto-enganche de lógica")]
    public PressurePuzzleController puzzleController; // se busca/añade si está vacío
    public RoomGoal roomGoal;                         // se busca si está vacío
    public DoorGate exitDoorOverride;                 // si está vacío, busca Door_E/Barrier

    [Header("Auto")]
    public bool buildOnStart = true;

    readonly List<GameObject> spawned = new();

    void OnEnable()
    {
        if (Application.isPlaying && buildOnStart)
            StartCoroutine(DelayedBuild());
    }
    IEnumerator DelayedBuild()
    {
        yield return null;
#if UNITY_EDITOR
        var sel = UnityEditor.Selection.activeTransform;
        if (sel && ((platesContainer && sel.IsChildOf(platesContainer)) ||
                    (propsContainer  && sel.IsChildOf(propsContainer))))
            UnityEditor.Selection.activeObject = null;
#endif
#if UNITY_EDITOR
        Rebuild();
#endif

    }


#if UNITY_EDITOR
    [ContextMenu("Rebuild (Editor)")]
    public void Rebuild() => BuildInternal();

    [ContextMenu("Clear (Editor)")]
    public void Clear() => ClearContainers();
#endif

    void BuildInternal()
    {
        // --- Buscar refs ---
        if (!doorW) doorW = transform.Find("Door_W");
        if (!doorE) doorE = transform.Find("Door_E");
        if (!platesContainer) platesContainer = EnsureChild("Plates");
        if (!propsContainer)  propsContainer  = EnsureChild("Props");
        if (!roomGoal)        roomGoal        = GetComponent<RoomGoal>();
        if (!puzzleController) puzzleController = GetComponentInChildren<PressurePuzzleController>(true);

        if (!doorW || !doorE) { Debug.LogError("[PressureRoomBuilder] Falta Door_W/Door_E"); return; }
        if (!platePrefab)     { Debug.LogError("[PressureRoomBuilder] Falta platePrefab"); return; }

        ClearContainers();

        // --- Geometría básica ---
        Vector3 a = doorW.position, b = doorE.position;
        a.y = b.y = transform.position.y;
        Vector3 dir = (b - a); dir.y = 0f;
        float dist = dir.magnitude;
        if (dist < 0.1f) { Debug.LogWarning("[PressureRoomBuilder] Puertas demasiado juntas."); return; }
        dir.Normalize();
        Vector3 right = new Vector3(-dir.z, 0f, dir.x);

        float usable = Mathf.Max(0f, dist - doorClearance * 2f);
        Vector3 origin = a + dir * doorClearance;   // inicio del corredor útil

        // --- Muestreamos posiciones válidas ---
        var placed = new List<Vector3>();
        int attempts = 0;
        int maxAttempts = 100 * plateCount;

        while (placed.Count < plateCount && attempts++ < maxAttempts)
        {
            float t = Random.Range(0.15f, 0.85f);                  // a lo largo del corredor
            float lateral = Random.Range(-corridorWidth * 0.5f, corridorWidth * 0.5f);

            Vector3 p = origin + dir * (t * usable) + right * lateral;

            if (snapToGround)
            {
                var o = p + Vector3.up * raycastHeight;
                if (Physics.Raycast(o, Vector3.down, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                    p = hit.point;
            }

            bool ok = true;
            foreach (var q in placed)
                if ((q - p).sqrMagnitude < minSeparation * minSeparation) { ok = false; break; }

            if (!ok) continue;

            // Instanciar placa
            var plate = Instantiate(platePrefab, p, Quaternion.identity, platesContainer);
            spawned.Add(plate);

            // Asegurar PressurePlate (nueva API)
            var pp = plate.GetComponent<PressurePlate>();
            if (!pp) pp = plate.AddComponent<PressurePlate>();
            
            // Configurar propiedades de la nueva API (via reflexión o propiedades públicas)
            // Nota: La nueva PressurePlate usa campos SerializeField privados
            // Se configurarán desde el prefab o manualmente en el inspector
            
            // Collider trigger por si acaso
            var col = plate.GetComponent<Collider>();
            if (!col) col = plate.AddComponent<BoxCollider>();
            col.isTrigger = true;

            placed.Add(p);
        }

        // --- Empujables opcionales ---
        if (spawnPushables && pushablePrefab)
        {
            int count = Mathf.Min(pushableCount, plateCount);
            for (int i = 0; i < count; i++)
            {
                // cerca de cada placa pero desplazado
                Vector3 baseP = placed[i] - right * 1.5f;
                if (snapToGround)
                {
                    var o = baseP + Vector3.up * raycastHeight;
                    if (Physics.Raycast(o, Vector3.down, out var hit, raycastHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                        baseP = hit.point;
                }

                var go = Instantiate(pushablePrefab, baseP, Quaternion.identity, propsContainer);
                spawned.Add(go);

                var rb = go.GetComponent<Rigidbody>();
                if (!rb) rb = go.AddComponent<Rigidbody>();
                rb.mass = pushableMass;
                if (freezePushableRotXZ)
                {
                    rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
            }
        }

        // --- Auto-wire puzzleController ---
        var plates = platesContainer.GetComponentsInChildren<PressurePlate>(true);
        if (!puzzleController)
            puzzleController = gameObject.AddComponent<PressurePuzzleController>();

        puzzleController.plates = plates;
        puzzleController.roomGoal = roomGoal ? roomGoal : GetComponent<RoomGoal>();
        if (!puzzleController.exitDoor)
            puzzleController.exitDoor = exitDoorOverride ? exitDoorOverride : FindExitDoorGate(doorE);

#if UNITY_EDITOR
        if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    DoorGate FindExitDoorGate(Transform doorETransform)
    {
        if (!doorETransform) return null;
        var gate = doorETransform.GetComponentInChildren<DoorGate>(true);
        if (!gate)
        {
            var barrier = doorETransform.Find("Barrier");
            if (barrier) gate = barrier.GetComponent<DoorGate>();
        }
        return gate;
    }

    Transform EnsureChild(string name)
    {
        var t = transform.Find(name);
        if (t) return t;
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    void ClearContainers()
    {
#if UNITY_EDITOR
        var sel = UnityEditor.Selection.activeTransform;
        if (sel && ((platesContainer && sel.IsChildOf(platesContainer)) ||
                    (propsContainer  && sel.IsChildOf(propsContainer))))
            UnityEditor.Selection.activeObject = null;
#endif
        void Clear(Transform t)
        {
            if (!t) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var go = t.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(go);
#if UNITY_EDITOR
                else UnityEditor.Undo.DestroyObjectImmediate(go);
#else
            else DestroyImmediate(go);
#endif
            }
        }
        Clear(platesContainer);
        Clear(propsContainer);
    }

}
