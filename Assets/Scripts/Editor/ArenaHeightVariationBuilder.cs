using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Herramienta de Editor para INC-106: añadir variación de altura alrededor de las arenas de
/// combate (montículos de hierba / rocas elevadas) sin tocar el suelo jugable ni el NavMesh.
///
/// Motivo del enfoque — feedback textual de u/Kyy7 en r/Unity3D (26 ago 2026, ver
/// contexto-proyecto.md): "variación de altura en la geometría del nivel (zonas de hierba/roca
/// elevadas) AUNQUE LA ARENA SIGA SIENDO BÁSICAMENTE PLANA". Es decir: no hace falta esculpir el
/// suelo de combate (arriesgado: rompería colisión/NavMesh de jugador y enemigos ya afinados en
/// otras incidencias), basta con romper la silueta alrededor del área jugable.
///
/// Qué hace: toma el BoxCollider (trigger) que ya usa BossArenaController para delimitar el área
/// de batalla — el mismo campo "Área Delimitada" — y esparce, en un anillo FUERA de ese área,
/// prefabs de Hill/Rock/Stone ya incluidos en el proyecto (Fantasy_Kingdom_Pack, mismo pack que
/// el resto del terreno del mundo, así que encajan visualmente sin arte nuevo). Cada punto se
/// ajusta en altura con un Raycast hacia abajo contra la capa de suelo real, así que sigue la
/// pendiente del terreno de cada escena en vez de asumir una altura fija.
///
/// Todo lo generado cuelga de un único hijo "HeightVariationProps" bajo el objeto seleccionado —
/// volver a pulsar "Generar" borra y recrea ese hijo (idempotente, mismo criterio que
/// LiamSpellBuilder.cs / MainMenuVersionLabelBuilder.cs), así que es seguro tantear semillas y
/// parámetros distintos hasta que la arena deje de verse genérica.
///
/// Pendiente a mano en el Editor tras usar la herramienta (esto es dressing visual, no se puede
/// automatizar del todo):
///   1) Abrir cada escena de arena, seleccionar el GameObject raíz de la arena (el que lleva
///      BossArenaController + el BoxCollider trigger) y ejecutar "El Sendero → Entorno →
///      Variación de Altura de Arena...".
///   2) Revisar que el Raycast haya encontrado suelo en todos los puntos (el log avisa de cuántos
///      puntos se descartaron por no encontrar la capa de suelo) — si una arena tiene un layout
///      raro, puede hacer falta subir "Intentos por Punto" o ajustar el margen.
///   3) Playtest: confirmar que ningún rock/hill queda visible DENTRO del área jugable ni bloquea
///      cámara/puertas, y que la silueta se lee bien desde el punto de vista de combate real.
/// </summary>
public class ArenaHeightVariationBuilder : EditorWindow
{
    private const string ContainerName = "HeightVariationProps";

    // Prefabs ya presentes en el proyecto (Fantasy_Kingdom_Pack) — mismo pack que ya viste el
    // resto del terreno del mundo, para no introducir un estilo de arte distinto.
    private static readonly string[] HillPrefabPaths =
    {
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill01_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill01_b01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill01_c01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill01_d01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill02_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill02_b01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill02_c01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Hill/Hill02_d01.prefab",
    };

    private static readonly string[] RockPrefabPaths =
    {
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock01_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock01_a03.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock02_a02.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock02_a05.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock03_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock03_a04.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Rock/Rock04_a02.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Engineering/Stone01_a01.prefab",
        "Assets/Art/World/Fantasy_Kingdom_Pack/Perfabs/Props/Engineering/Stone02_a01.prefab",
    };

    private GameObject _target;
    private float _ringMargin = 1.5f;   // separación entre el borde del área jugable y el anillo
    private float _ringThickness = 6f;  // grosor del anillo donde se coloca hierba/roca
    private int _hillCount = 5;
    private int _rockCount = 10;
    private float _minScale = 0.85f;
    private float _maxScale = 1.35f;
    private int _seed = 12345;
    private LayerMask _floorLayer = 1 << 6; // "Floor" — mismo layer por defecto que BossArenaController.floorLayer
    private int _raycastAttemptsPerPoint = 6;

    [MenuItem("El Sendero/Entorno/Variación de Altura de Arena...")]
    public static void ShowWindow()
    {
        var window = GetWindow<ArenaHeightVariationBuilder>("Variación de Altura (INC-106)");
        window._target = Selection.activeGameObject;
        window.minSize = new Vector2(360, 420);
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Selecciona el GameObject raíz de la arena (el que tiene BossArenaController + el " +
            "BoxCollider trigger del área de batalla) y pulsa Generar. Esparce hills/rocks del " +
            "Fantasy_Kingdom_Pack en un anillo FUERA del área jugable — el suelo de combate y el " +
            "NavMesh no se tocan.",
            MessageType.Info);

        EditorGUILayout.Space();
        _target = (GameObject)EditorGUILayout.ObjectField("Arena (raíz)", _target, typeof(GameObject), true);

        BoxCollider box = FindArenaBoxCollider();
        if (_target != null && box == null)
        {
            EditorGUILayout.HelpBox(
                "No se encontró un BoxCollider en este objeto ni en sus hijos. La herramienta " +
                "necesita el mismo BoxCollider que usa BossArenaController como área delimitada.",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anillo de dressing", EditorStyles.boldLabel);
        _ringMargin = EditorGUILayout.FloatField("Margen desde el borde", _ringMargin);
        _ringThickness = EditorGUILayout.FloatField("Grosor del anillo", _ringThickness);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Cantidad y variedad", EditorStyles.boldLabel);
        _hillCount = EditorGUILayout.IntField("Nº de hills (banda exterior)", _hillCount);
        _rockCount = EditorGUILayout.IntField("Nº de rocas/piedras (banda interior)", _rockCount);
        _minScale = EditorGUILayout.FloatField("Escala mínima", _minScale);
        _maxScale = EditorGUILayout.FloatField("Escala máxima", _maxScale);
        _seed = EditorGUILayout.IntField("Semilla (cambia para otra variación)", _seed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Suelo", EditorStyles.boldLabel);
        _floorLayer = LayerMaskField("Capa de suelo (Raycast)", _floorLayer);
        _raycastAttemptsPerPoint = EditorGUILayout.IntField("Intentos por punto", _raycastAttemptsPerPoint);

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(box == null))
        {
            if (GUILayout.Button("Generar / Regenerar", GUILayout.Height(30)))
            {
                Generate(box);
            }
        }

        Transform existing = _target != null ? _target.transform.Find(ContainerName) : null;
        using (new EditorGUI.DisabledScope(existing == null))
        {
            if (GUILayout.Button("Limpiar (borrar lo generado)"))
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
        }
    }

    private BoxCollider FindArenaBoxCollider()
    {
        if (_target == null) return null;
        var box = _target.GetComponent<BoxCollider>();
        if (box != null) return box;
        return _target.GetComponentInChildren<BoxCollider>();
    }

    private void Generate(BoxCollider box)
    {
        if (box == null) return;

        // Limpia lo generado antes (idempotente).
        Transform existing = _target.transform.Find(ContainerName);
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var hillPrefabs = LoadPrefabs(HillPrefabPaths);
        var rockPrefabs = LoadPrefabs(RockPrefabPaths);
        if (hillPrefabs.Count == 0 && rockPrefabs.Count == 0)
        {
            Debug.LogError("[ArenaHeightVariationBuilder] No se pudo cargar ningún prefab de Hill/Rock — revisa las rutas.");
            return;
        }

        var container = new GameObject(ContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Generar Variación de Altura de Arena");
        container.transform.SetParent(_target.transform, false);

        // Bounds del área jugable en espacio mundo (el BoxCollider puede estar rotado/escalado).
        Bounds worldBounds = box.bounds;
        Vector3 center = worldBounds.center;
        float halfX = worldBounds.extents.x;
        float halfZ = worldBounds.extents.z;

        var rng = new System.Random(_seed);
        int placed = 0, skipped = 0;

        // Banda exterior del anillo: hills (silueta grande, más lejos del área jugable).
        placed += PlaceBand(container.transform, hillPrefabs, _hillCount, center, halfX, halfZ,
            _ringMargin + _ringThickness * 0.5f, _ringMargin + _ringThickness, rng, ref skipped);

        // Banda interior del anillo: rocas/piedras (más cerca del borde, rompen la silueta de cerca).
        placed += PlaceBand(container.transform, rockPrefabs, _rockCount, center, halfX, halfZ,
            _ringMargin, _ringMargin + _ringThickness * 0.5f, rng, ref skipped);

        Debug.Log($"[ArenaHeightVariationBuilder] INC-106: {placed} props colocados alrededor de '{_target.name}' " +
                   $"({skipped} puntos descartados por no encontrar suelo — revisa la capa seleccionada si el número es alto).");
    }

    private int PlaceBand(Transform parent, List<GameObject> prefabs, int count, Vector3 center,
        float halfX, float halfZ, float innerMargin, float outerMargin, System.Random rng, ref int skipped)
    {
        if (prefabs.Count == 0 || count <= 0) return 0;

        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            bool found = false;
            for (int attempt = 0; attempt < _raycastAttemptsPerPoint; attempt++)
            {
                // Rejection sampling dentro de un rectángulo expandido, descartando el rectángulo
                // interior — genera un anillo que sigue la forma real (rectangular) del área
                // jugable en vez de forzar un círculo perfecto sobre una arena que no lo es.
                float outerX = halfX + outerMargin;
                float outerZ = halfZ + outerMargin;
                float x = Lerp(rng, -outerX, outerX);
                float z = Lerp(rng, -outerZ, outerZ);

                float innerX = halfX + innerMargin;
                float innerZ = halfZ + innerMargin;
                if (Mathf.Abs(x) < innerX && Mathf.Abs(z) < innerZ) continue; // demasiado cerca, reintenta

                Vector3 samplePoint = new Vector3(center.x + x, center.y + 50f, center.z + z);
                if (Physics.Raycast(samplePoint, Vector3.down, out RaycastHit hit, 200f, _floorLayer))
                {
                    var prefab = prefabs[rng.Next(prefabs.Count)];
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    Undo.RegisterCreatedObjectUndo(instance, "Generar Variación de Altura de Arena");
                    instance.transform.position = hit.point;
                    instance.transform.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
                    float scale = Lerp(rng, _minScale, _maxScale);
                    instance.transform.localScale = Vector3.one * scale;
                    placed++;
                    found = true;
                    break;
                }
            }
            if (!found) skipped++;
        }
        return placed;
    }

    private static float Lerp(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private static List<GameObject> LoadPrefabs(string[] paths)
    {
        var list = new List<GameObject>();
        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) list.Add(prefab);
            else Debug.LogWarning($"[ArenaHeightVariationBuilder] No se encontró el prefab en: {path}");
        }
        return list;
    }

    private static LayerMask LayerMaskField(string label, LayerMask layerMask)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        int mask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layerIndex = LayerMask.NameToLayer(layers[i]);
            if ((layerMask.value & (1 << layerIndex)) != 0) mask |= 1 << i;
        }
        mask = EditorGUILayout.MaskField(label, mask, layers);
        int result = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            if ((mask & (1 << i)) != 0) result |= 1 << LayerMask.NameToLayer(layers[i]);
        }
        return result;
    }
}
