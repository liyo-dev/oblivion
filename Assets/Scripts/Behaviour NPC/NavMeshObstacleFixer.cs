using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Script para configurar correctamente los NavMeshObstacle y evitar que empujen a los agentes.
/// EJECUTAR UNA VEZ para arreglar todos los árboles del bosque.
/// </summary>
public class NavMeshObstacleFixer : MonoBehaviour
{
    [Header("Configuración Recomendada")]
    [Tooltip("Usa Carve para obstáculos estáticos (árboles). Desactiva para obstáculos dinámicos.")]
    [SerializeField] private bool useCarving = true;
    
    [Tooltip("Move Threshold: Distancia que debe moverse antes de actualizar. MUY ALTO para árboles estáticos.")]
    [SerializeField] private float moveThreshold = 1000f; // Casi nunca se actualiza
    
    [Tooltip("Carve Only Stationary: Solo hace carve cuando está quieto (ideal para árboles).")]
    [SerializeField] private bool carveOnlyStationary = true;
    
    [Tooltip("Time To Stationary: Tiempo para considerar que está quieto. BAJO para árboles.")]
    [SerializeField] private float timeToStationary = 0.1f;
    
    [Header("Ejecución")]
    [Tooltip("Ejecutar al iniciar (útil para runtime). Desactivar si solo usas el botón manual.")]
    [SerializeField] private bool fixOnStart = false;
    
    [Tooltip("Buscar solo en este GameObject y sus hijos. Si es null, busca en toda la escena.")]
    [SerializeField] private GameObject searchRoot;
    
    private void Start()
    {
        if (fixOnStart)
        {
            FixAllObstacles();
        }
    }
    
    /// <summary>
    /// Arregla todos los NavMeshObstacle en la escena o en el searchRoot.
    /// </summary>
    [ContextMenu("Fix All NavMeshObstacles")]
    public void FixAllObstacles()
    {
        NavMeshObstacle[] obstacles;
        
        if (searchRoot != null)
        {
            obstacles = searchRoot.GetComponentsInChildren<NavMeshObstacle>(true);
            Debug.Log($"[ObstacleFixer] 🔍 Buscando en '{searchRoot.name}' y sus hijos...");
        }
        else
        {
            obstacles = FindObjectsByType<NavMeshObstacle>();
            Debug.Log($"[ObstacleFixer] 🔍 Buscando en toda la escena...");
        }
        
        if (obstacles.Length == 0)
        {
            Debug.LogWarning("[ObstacleFixer] ⚠️ No se encontraron NavMeshObstacle.");
            return;
        }
        
        int fixedCount = 0;
        
        foreach (var obstacle in obstacles)
        {
            if (FixObstacle(obstacle))
            {
                fixedCount++;
            }
        }
        
        Debug.Log($"[ObstacleFixer] ✅ {fixedCount}/{obstacles.Length} NavMeshObstacle configurados correctamente.");
    }
    
    /// <summary>
    /// Configura un NavMeshObstacle individual correctamente.
    /// </summary>
    private bool FixObstacle(NavMeshObstacle obstacle)
    {
        if (obstacle == null) return false;
        
        bool needsFix = false;
        string changes = "";
        
        // 1. Carving
        if (obstacle.carving != useCarving)
        {
            obstacle.carving = useCarving;
            needsFix = true;
            changes += $" carving={useCarving}";
        }
        
        // 2. Move Threshold (CRÍTICO para evitar updates constantes)
        if (Mathf.Abs(obstacle.carvingMoveThreshold - moveThreshold) > 0.01f)
        {
            obstacle.carvingMoveThreshold = moveThreshold;
            needsFix = true;
            changes += $" moveThreshold={moveThreshold}";
        }
        
        // 3. Carve Only Stationary
        if (obstacle.carveOnlyStationary != carveOnlyStationary)
        {
            obstacle.carveOnlyStationary = carveOnlyStationary;
            needsFix = true;
            changes += $" carveOnlyStationary={carveOnlyStationary}";
        }
        
        // 4. Time To Stationary
        if (Mathf.Abs(obstacle.carvingTimeToStationary - timeToStationary) > 0.01f)
        {
            obstacle.carvingTimeToStationary = timeToStationary;
            needsFix = true;
            changes += $" timeToStationary={timeToStationary}";
        }
        
        if (needsFix)
        {
            Debug.Log($"[ObstacleFixer] 🔧 {obstacle.gameObject.name}:{changes}");
            
            // Marcar como dirty para guardar cambios
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(obstacle);
            #endif
        }
        
        return needsFix;
    }
    
    /// <summary>
    /// Crea NavMeshObstacle en todos los árboles que tengan "Tree" en el nombre y no tengan uno.
    /// </summary>
    [ContextMenu("Add NavMeshObstacle to Trees")]
    public void AddObstaclesToTrees()
    {
        Transform root = searchRoot != null ? searchRoot.transform : null;
        Transform[] allTransforms = root != null 
            ? root.GetComponentsInChildren<Transform>(true) 
            : FindObjectsByType<Transform>();
        
        int addedCount = 0;
        
        foreach (var t in allTransforms)
        {
            // Buscar objetos con "Tree" en el nombre
            if (t.name.ToLower().Contains("tree"))
            {
                // Si no tiene NavMeshObstacle, añadirlo
                if (t.GetComponent<NavMeshObstacle>() == null)
                {
                    var obstacle = t.gameObject.AddComponent<NavMeshObstacle>();
                    
                    // Configurar correctamente desde el inicio
                    obstacle.carving = useCarving;
                    obstacle.carvingMoveThreshold = moveThreshold;
                    obstacle.carveOnlyStationary = carveOnlyStationary;
                    obstacle.carvingTimeToStationary = timeToStationary;
                    
                    // Intentar ajustar el tamaño automáticamente
                    AutoSizeObstacle(obstacle);
                    
                    addedCount++;
                    Debug.Log($"[ObstacleFixer] ➕ NavMeshObstacle añadido a: {t.name}");
                    
                    #if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(t.gameObject);
                    #endif
                }
            }
        }
        
        Debug.Log($"[ObstacleFixer] ✅ {addedCount} NavMeshObstacle añadidos a árboles.");
    }
    
    /// <summary>
    /// Intenta ajustar el tamaño del obstacle basándose en los colliders del GameObject.
    /// </summary>
    private void AutoSizeObstacle(NavMeshObstacle obstacle)
    {
        var collider = obstacle.GetComponent<Collider>();

        if (collider != null)
        {
            // BUG (fijado): collider.bounds está en espacio del MUNDO, pero
            // NavMeshObstacle.center/size esperan valores en espacio LOCAL
            // (Unity los vuelve a multiplicar por el lossyScale del transform
            // al convertirlos a mundo). Asignar bounds del mundo directamente
            // aquí duplicaba la escala: en un árbol con escala ≈7.9 el centro
            // acababa ~7.9× más alto de lo real (el "carve" quedaba flotando
            // muy por encima del tronco, sin bloquear nada a nivel de suelo) y
            // el tamaño quedaba igual de mal escalado. Convertir explícitamente
            // a espacio local antes de asignar.
            Bounds bounds = collider.bounds; // mundo
            Transform t = obstacle.transform;
            Vector3 lossyScale = t.lossyScale;

            obstacle.center = t.InverseTransformPoint(bounds.center);
            obstacle.size = new Vector3(
                Mathf.Approximately(lossyScale.x, 0f) ? bounds.size.x : bounds.size.x / Mathf.Abs(lossyScale.x),
                Mathf.Approximately(lossyScale.y, 0f) ? bounds.size.y : bounds.size.y / Mathf.Abs(lossyScale.y),
                Mathf.Approximately(lossyScale.z, 0f) ? bounds.size.z : bounds.size.z / Mathf.Abs(lossyScale.z));

            Debug.Log($"[ObstacleFixer]   └─ Auto-sized (local space): center={obstacle.center} size={obstacle.size}");
        }
        else
        {
            // Valores por defecto razonables para un árbol
            obstacle.center = Vector3.up * 2f;
            obstacle.size = new Vector3(1f, 4f, 1f);

            Debug.Log($"[ObstacleFixer]   └─ Default size: (1, 4, 1)");
        }
    }
}

#if UNITY_EDITOR
/// <summary>
/// Editor personalizado para facilitar el uso del fixer.
/// </summary>
[UnityEditor.CustomEditor(typeof(NavMeshObstacleFixer))]
public class NavMeshObstacleFixerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        NavMeshObstacleFixer fixer = (NavMeshObstacleFixer)target;
        
        UnityEditor.EditorGUILayout.Space(10);
        UnityEditor.EditorGUILayout.LabelField("Acciones Rápidas", UnityEditor.EditorStyles.boldLabel);
        
        if (GUILayout.Button("🔧 Arreglar Todos los NavMeshObstacle", GUILayout.Height(30)))
        {
            fixer.FixAllObstacles();
        }
        
        if (GUILayout.Button("➕ Añadir NavMeshObstacle a Árboles", GUILayout.Height(30)))
        {
            fixer.AddObstaclesToTrees();
        }
        
        UnityEditor.EditorGUILayout.HelpBox(
            "1. Asigna 'Search Root' al objeto 'Woods' o 'Trees' del bosque\n" +
            "2. Haz clic en '🔧 Arreglar Todos'\n" +
            "3. ¡Listo! Los árboles ya no empujarán a los NPCs",
            UnityEditor.MessageType.Info);
    }
}
#endif
