using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;

/// <summary>
/// Prueba de Will en el Sendero (GDD escena 17): laberinto procedural sobre recuerdos/miedos de
/// Will, generado con el kit modular "Fantasy_Kingdom_Pack" (Interior/Room). NO tiene nada que ver
/// con el laberinto de espejos — ese es la prueba de Liam (parque de atracciones) y de momento no
/// se toca (confirmado por Raúl el 1 sep 2026).
///
/// Algoritmo: laberinto perfecto (árbol de expansión, sin ciclos) por backtracking recursivo sobre
/// una rejilla NxN. La celda de salida es la más lejana de la celda de inicio (BFS), para que el
/// recorrido sea largo de verdad. 2-3 celdas sin salida (dead-ends) que no sean inicio/meta se
/// convierten en "ecos" de Will — encuentros de combate reutilizando enemigos ya existentes en el
/// proyecto (Demon/Demon2/Spider1), sin tocar su configuración: ya vienen listos para pelear.
///
/// *** ÚNICO PUNTO QUE HACE FALTA VERIFICAR A OJO EN EL EDITOR ***
/// El pack trae 8 variantes por cada sala (Room01..04_a01 .. _h01) que casi seguro codifican qué
/// lados tienen puerta, pero no hay forma de comprobar desde aquí (sin abrir Unity) cuál letra es
/// cuál puerta exactamente. RoomLetterByShape de abajo es mi mejor suposición razonada (convención
/// habitual en kits modulares: a=cerrada, b/g=1 puerta, c=recta, d=esquina, e=T, f/h=cruz/abierta).
/// Si al entrar en la escena las puertas no encajan entre celdas vecinas: abre Room01_a01..h01 en
/// el Editor, mira qué lado tiene el hueco, y corrige el bitmask de esa letra en la tabla — el
/// resto del generador (grafo del laberinto, semilla, ecos, meta) no cambia, es determinista.
///
/// Regenerar (mismo menú) borra y reconstruye todo el contenedor "PRUEBA_WILL_LABERINTO" desde
/// cero — no es idempotente celda a celda como el Guardián, porque el laberinto es un conjunto.
/// Si Raúl edita algo a mano dentro del contenedor, se perderá al regenerar.
/// </summary>
public static class WillTrialMazeBuilder
{
    private const string ScenePath = "Assets/Scenes/Worlds/Sendero_PruebaWill.unity";
    private const string RootName = "PRUEBA_WILL_LABERINTO";

    private const int MazeWidth = 7;
    private const int MazeHeight = 7;
    private const int Seed = 20260901;
    private const int EchoBattleCount = 3;
    private const float FallbackCellSize = 6f;

    private static readonly string[] RoomBaseNames = { "Room01", "Room02", "Room03", "Room04" };
    private static readonly string RoomFolder = "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Interior/Room";

    private static readonly string[] EchoEnemyPaths =
    {
        "Assets/Prefabs/Enemy/Demon.prefab",
        "Assets/Prefabs/Enemy/Demon2.prefab",
        "Assets/Prefabs/Enemy/Spider1.prefab",
    };

    // Direcciones en orden N=0, E=1, S=2, W=3. Bit i -> 1<<i.
    private static readonly int[] DX = { 0, 1, 0, -1 };
    private static readonly int[] DZ = { 1, 0, -1, 0 };

    // Bitmask "canónico" (a rotación 0) de cada letra, mejor suposición razonada — ver aviso arriba.
    // N=1, E=2, S=4, W=8. 'a' (0 puertas) no se usa nunca para celdas del laberinto.
    private static readonly Dictionary<char, int> RoomLetterCanonicalBitmask = new Dictionary<char, int>
    {
        { 'a', 0b0000 }, // cerrada, 0 puertas (no se usa en celdas del recorrido)
        { 'b', 0b0001 }, // 1 puerta (Norte) — dead end, estilo 1
        { 'g', 0b0001 }, // 1 puerta (Norte) — dead end, estilo alternativo
        { 'c', 0b0101 }, // 2 puertas opuestas (N+S) — pasillo recto
        { 'd', 0b0011 }, // 2 puertas contiguas (N+E) — esquina
        { 'e', 0b0111 }, // 3 puertas (N+E+S, falta O) — cruce en T
        { 'f', 0b1111 }, // 4 puertas — cruce abierto, estilo 1
        { 'h', 0b1111 }, // 4 puertas — cruce abierto, estilo alternativo (más grande/distinto)
    };

    private class Cell
    {
        public bool visited;
        public int connections; // bitmask N/E/S/W
    }

    [MenuItem("El Sendero/Escena/Generar Laberinto de la Prueba de Will")]
    public static void GenerateMaze()
    {
        Scene scene = OpenOrCreateScene();

        // --- Limpiar generación anterior (regenerar = borrar y reconstruir) ---
        GameObject root = GameObject.Find(RootName);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        root = new GameObject(RootName);

        var log = new StringBuilder();
        log.AppendLine("=== Generador Laberinto Prueba de Will ===");
        log.AppendLine($"Rejilla {MazeWidth}x{MazeHeight}, semilla {Seed}");

        // --- 1) Generar el grafo del laberinto (backtracking recursivo, iterativo con pila) ---
        var rng = new System.Random(Seed);
        Cell[,] grid = new Cell[MazeWidth, MazeHeight];
        for (int x = 0; x < MazeWidth; x++)
            for (int z = 0; z < MazeHeight; z++)
                grid[x, z] = new Cell();

        var stack = new Stack<Vector2Int>();
        var start = new Vector2Int(0, 0);
        grid[start.x, start.y].visited = true;
        stack.Push(start);

        while (stack.Count > 0)
        {
            Vector2Int current = stack.Peek();
            var unvisitedDirs = new List<int>();
            for (int dir = 0; dir < 4; dir++)
            {
                int nx = current.x + DX[dir];
                int nz = current.y + DZ[dir];
                if (nx < 0 || nx >= MazeWidth || nz < 0 || nz >= MazeHeight) continue;
                if (!grid[nx, nz].visited) unvisitedDirs.Add(dir);
            }

            if (unvisitedDirs.Count == 0)
            {
                stack.Pop();
                continue;
            }

            int chosenDir = unvisitedDirs[rng.Next(unvisitedDirs.Count)];
            int nx2 = current.x + DX[chosenDir];
            int nz2 = current.y + DZ[chosenDir];
            int opposite = (chosenDir + 2) % 4;

            grid[current.x, current.y].connections |= (1 << chosenDir);
            grid[nx2, nz2].connections |= (1 << opposite);
            grid[nx2, nz2].visited = true;
            stack.Push(new Vector2Int(nx2, nz2));
        }

        // --- 2) Meta = celda más lejana del inicio (BFS), para un recorrido largo de verdad ---
        Vector2Int goal = FindFarthestCell(grid, start);
        log.AppendLine($"Inicio: {start} — Meta: {goal}");

        // --- 3) Elegir celdas "eco de Will" (dead-ends, ni inicio ni meta) ---
        var deadEnds = new List<Vector2Int>();
        for (int x = 0; x < MazeWidth; x++)
            for (int z = 0; z < MazeHeight; z++)
            {
                var pos = new Vector2Int(x, z);
                if (pos == start || pos == goal) continue;
                if (PopCount(grid[x, z].connections) == 1) deadEnds.Add(pos);
            }
        Shuffle(deadEnds, rng);
        var echoCells = new HashSet<Vector2Int>();
        for (int i = 0; i < Mathf.Min(EchoBattleCount, deadEnds.Count); i++) echoCells.Add(deadEnds[i]);

        // --- 4) Instanciar la celda (0,0) primero para medir el tamaño real de la pieza ---
        float cellSize = FallbackCellSize;
        GameObject firstPiece = InstantiateCell(grid[start.x, start.y].connections, start, rng, root, out bool startedOk);
        if (firstPiece != null)
        {
            var renderers = firstPiece.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                cellSize = Mathf.Max(b.size.x, b.size.z);
                log.AppendLine($"Tamaño de celda auto-detectado: {cellSize:0.00}u (a partir de {firstPiece.name})");
            }
            else
            {
                log.AppendLine($"AVISO: no se encontró Renderer en {firstPiece.name}, usando tamaño de celda por defecto ({FallbackCellSize}u).");
            }
            firstPiece.transform.position = GridToWorld(start.x, start.y, cellSize);
        }

        // --- 5) Instanciar el resto de celdas con el tamaño ya conocido ---
        int roomCount = 1;
        for (int x = 0; x < MazeWidth; x++)
        {
            for (int z = 0; z < MazeHeight; z++)
            {
                var pos = new Vector2Int(x, z);
                if (pos == start) continue; // ya colocada arriba
                GameObject piece = InstantiateCell(grid[x, z].connections, pos, rng, root, out bool ok);
                if (piece == null) continue;
                piece.transform.position = GridToWorld(x, z, cellSize);
                roomCount++;
            }
        }
        log.AppendLine($"Salas instanciadas: {roomCount}/{MazeWidth * MazeHeight}");

        // --- 6) Marcadores de inicio y meta ---
        var startMarker = new GameObject("WILL_MAZE_START");
        startMarker.transform.SetParent(root.transform);
        startMarker.transform.position = GridToWorld(start.x, start.y, cellSize);

        var goalMarker = new GameObject("WILL_MAZE_GOAL");
        goalMarker.transform.SetParent(root.transform);
        goalMarker.transform.position = GridToWorld(goal.x, goal.y, cellSize);
        var goalTrigger = goalMarker.AddComponent<BoxCollider>();
        goalTrigger.isTrigger = true;
        goalTrigger.size = new Vector3(cellSize * 0.6f, 3f, cellSize * 0.6f);
        // Enganche pendiente: aquí es donde Raúl decide cómo se resuelve la prueba de Will
        // (recompensa, señal narrativa, cinemática) cuando conecte esta escena a la narrativa real.
        // De momento es solo el punto marcado — sin lógica, para no tocar el grafo narrativo a ciegas.

        // --- 7) Ecos de Will (combates reutilizando enemigos ya existentes, sin re-configurarlos) ---
        int echoIndex = 0;
        foreach (var cellPos in echoCells)
        {
            string enemyPath = EchoEnemyPaths[echoIndex % EchoEnemyPaths.Length];
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPath);
            if (enemyPrefab == null)
            {
                log.AppendLine($"AVISO: no se pudo cargar {enemyPath} para el eco #{echoIndex + 1}.");
                echoIndex++;
                continue;
            }
            GameObject enemyInstance = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab, root.transform);
            enemyInstance.name = $"Eco_de_Will_{echoIndex + 1}_{enemyPrefab.name}";
            enemyInstance.transform.position = GridToWorld(cellPos.x, cellPos.y, cellSize);
            log.AppendLine($"Eco de Will #{echoIndex + 1}: {enemyPrefab.name} en celda {cellPos}");
            echoIndex++;
        }

        // --- 8) NavMesh: el proyecto ya usa Unity.AI.Navigation.NavMeshSurface (visto en Sendero.unity) ---
        var navSurface = root.AddComponent<NavMeshSurface>();
        navSurface.collectObjects = CollectObjects.Children;
        float gridSpanX = MazeWidth * cellSize;
        float gridSpanZ = MazeHeight * cellSize;
        navSurface.center = new Vector3(gridSpanX * 0.5f - cellSize * 0.5f, 1f, gridSpanZ * 0.5f - cellSize * 0.5f);
        navSurface.size = new Vector3(gridSpanX + cellSize, 4f, gridSpanZ + cellSize);
        navSurface.BuildNavMesh();
        log.AppendLine("NavMesh horneado sobre el contenedor del laberinto.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine("--- Recuerda: revisa a ojo en el Editor que las puertas encajan entre celdas vecinas (ver aviso al principio de este archivo) ---");
        Debug.Log(log.ToString());
    }

    private static Scene OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        string dir = System.IO.Path.GetDirectoryName(ScenePath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            EnsureFolder(dir);
        }
        EditorSceneManager.SaveScene(newScene, ScenePath);
        return newScene;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static Vector3 GridToWorld(int x, int z, float cellSize)
    {
        return new Vector3(x * cellSize, 0f, z * cellSize);
    }

    private static GameObject InstantiateCell(int bitmask, Vector2Int pos, System.Random rng, GameObject root, out bool ok)
    {
        ok = FindLetterAndRotation(bitmask, rng, out char letter, out int rotationSteps);
        if (!ok)
        {
            Debug.LogWarning($"[WillTrialMazeBuilder] Celda {pos}: bitmask {bitmask} no coincide con ninguna letra conocida — se usa 'f' (cruz abierta) como red de seguridad.");
            letter = 'f';
            rotationSteps = 0;
        }

        string baseName = RoomBaseNames[rng.Next(RoomBaseNames.Length)];
        string prefabPath = $"{RoomFolder}/{baseName}_{letter}01.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[WillTrialMazeBuilder] No se encontró {prefabPath}, celda {pos} omitida.");
            return null;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        instance.name = $"Cell_{pos.x}_{pos.y}_{baseName}{letter}";
        instance.transform.rotation = Quaternion.Euler(0f, 90f * rotationSteps, 0f);
        return instance;
    }

    // Busca qué letra + cuántos pasos de rotación de 90° hacen falta para que el bitmask canónico
    // de esa letra, rotado, coincida con el bitmask requerido por el grafo del laberinto.
    private static bool FindLetterAndRotation(int requiredBitmask, System.Random rng, out char letter, out int rotationSteps)
    {
        // Letras candidatas agrupadas por "forma" (mismo bitmask canónico) para variedad visual.
        var shapeGroups = new List<char[]>
        {
            new[] { 'b', 'g' }, // 1 puerta
            new[] { 'c' },      // recta
            new[] { 'd' },      // esquina
            new[] { 'e' },      // T
            new[] { 'f', 'h' }, // cruz
        };

        foreach (var group in shapeGroups)
        {
            int canonical = RoomLetterCanonicalBitmask[group[0]];
            for (int steps = 0; steps < 4; steps++)
            {
                if (RotateBitmaskCW(canonical, steps) == requiredBitmask)
                {
                    letter = group[rng.Next(group.Length)];
                    rotationSteps = steps;
                    return true;
                }
            }
        }

        letter = 'f';
        rotationSteps = 0;
        return false;
    }

    // Desplazamiento circular de 4 bits: gira el patrón de puertas 90° en sentido horario por paso.
    private static int RotateBitmaskCW(int bitmask, int steps)
    {
        int result = bitmask & 0b1111;
        for (int i = 0; i < steps; i++)
        {
            result = ((result << 1) | (result >> 3)) & 0b1111;
        }
        return result;
    }

    private static int PopCount(int bitmask)
    {
        int count = 0;
        while (bitmask != 0)
        {
            count += bitmask & 1;
            bitmask >>= 1;
        }
        return count;
    }

    private static Vector2Int FindFarthestCell(Cell[,] grid, Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int>();
        var queue = new Queue<Vector2Int>();
        dist[from] = 0;
        queue.Enqueue(from);
        Vector2Int farthest = from;

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            if (dist[current] > dist[farthest]) farthest = current;

            int connections = grid[current.x, current.y].connections;
            for (int dir = 0; dir < 4; dir++)
            {
                if ((connections & (1 << dir)) == 0) continue;
                var next = new Vector2Int(current.x + DX[dir], current.y + DZ[dir]);
                if (dist.ContainsKey(next)) continue;
                dist[next] = dist[current] + 1;
                queue.Enqueue(next);
            }
        }
        return farthest;
    }

    private static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
