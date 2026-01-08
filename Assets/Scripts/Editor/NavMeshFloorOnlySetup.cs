#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine.AI;

/// <summary>
/// Herramienta para configurar correctamente el NavMesh donde solo Floor es walkable.
/// Marca automáticamente todos los objetos con colliders (que no sean Floor) como Not Walkable.
/// </summary>
public class NavMeshFloorOnlySetup : EditorWindow
{
    private string floorLayerName = "Floor";
    private bool markStaticObjects = true;

    [MenuItem("Tools/NavMesh/Setup Floor-Only NavMesh")]
    public static void ShowWindow()
    {
        GetWindow<NavMeshFloorOnlySetup>("NavMesh Floor Setup");
    }

    void OnGUI()
    {
        GUILayout.Label("Configurar NavMesh (Solo Floor Walkable)", EditorStyles.boldLabel);
        GUILayout.Space(10);

        floorLayerName = EditorGUILayout.TextField("Nombre de Layer Floor", floorLayerName);
        markStaticObjects = EditorGUILayout.Toggle("Marcar objetos como Static", markStaticObjects);

        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "Este script configurará:\n" +
            "1. Objetos en layer 'Floor' → Navigation Static + Walkable\n" +
            "2. Otros objetos con Collider → Navigation Static + Not Walkable\n\n" +
            "Después de ejecutar, ve a Window → AI → Navigation y haz Bake.",
            MessageType.Info);

        GUILayout.Space(10);

        if (GUILayout.Button("1. Configurar Navigation Areas", GUILayout.Height(30)))
        {
            ConfigureNavigationAreas();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("2. Abrir ventana de Navigation para Bake", GUILayout.Height(25)))
        {
            EditorApplication.ExecuteMenuItem("Window/AI/Navigation");
        }

        GUILayout.Space(20);
        
        EditorGUILayout.HelpBox(
            "ALTERNATIVA RÁPIDA:\n" +
            "Si no quieres rebakear, usa 'Setup Obstacles from Layer' para añadir NavMeshObstacle dinámicos a los obstáculos.",
            MessageType.Warning);
    }

    void ConfigureNavigationAreas()
    {
        int floorLayer = LayerMask.NameToLayer(floorLayerName);
        if (floorLayer == -1)
        {
            EditorUtility.DisplayDialog("Error", $"No se encontró la layer '{floorLayerName}'", "OK");
            return;
        }

        var allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int floorCount = 0;
        int obstacleCount = 0;

        Undo.SetCurrentGroupName("Configure NavMesh Areas");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (var obj in allObjects)
        {
            // Solo objetos con colliders
            var collider = obj.GetComponent<Collider>();
            if (collider == null) continue;
            
            // Ignorar triggers
            if (collider.isTrigger) continue;

            Undo.RecordObject(obj, "Set Navigation Static");

            // Obtener o crear el componente de flags estáticos
            var flags = GameObjectUtility.GetStaticEditorFlags(obj);

            if (obj.layer == floorLayer)
            {
                // Floor: Navigation Static + Walkable
                if (markStaticObjects)
                {
                    flags |= StaticEditorFlags.NavigationStatic;
                    GameObjectUtility.SetStaticEditorFlags(obj, flags);
                }
                
                // Marcar como Walkable (area 0)
                GameObjectUtility.SetNavMeshArea(obj, 0); // 0 = Walkable
                floorCount++;
            }
            else
            {
                // Otros: Navigation Static + Not Walkable
                if (markStaticObjects)
                {
                    flags |= StaticEditorFlags.NavigationStatic;
                    GameObjectUtility.SetStaticEditorFlags(obj, flags);
                }
                
                // Marcar como Not Walkable (area 1)
                GameObjectUtility.SetNavMeshArea(obj, 1); // 1 = Not Walkable
                obstacleCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("NavMesh Setup Complete", 
            $"Configurados:\n" +
            $"- {floorCount} objetos como Walkable (Floor)\n" +
            $"- {obstacleCount} objetos como Not Walkable (Obstáculos)\n\n" +
            $"Ahora abre Window → AI → Navigation y haz click en 'Bake'.",
            "OK");
        
        Debug.Log($"[NavMesh Setup] Floor: {floorCount}, Obstacles: {obstacleCount}");
    }
}
#endif

