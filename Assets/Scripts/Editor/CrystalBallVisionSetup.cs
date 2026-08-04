using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// Genera todos los assets y GOs necesarios para el efecto de visión en la bola de cristal.
/// Menú: Tools > El Sendero > Setup Bola de Cristal — Visión del Jugador
public static class CrystalBallVisionSetup
{
    private const string AssetFolder  = "Assets/Art/Cinematics";
    private const string RTPath       = AssetFolder + "/RT_CrystalBallVision.renderTexture";
    private const string MatPath      = AssetFolder + "/Mat_CrystalBallVision.mat";
    private const string CamGoName    = "CrystalBallVisionCamera";

    [MenuItem("Tools/El Sendero/Setup Bola de Cristal — Visión del Jugador")]
    public static void Run()
    {
        EnsureFolder();

        var rt  = GetOrCreateRenderTexture();
        var mat = GetOrCreateMaterial(rt);
        var camGO = GetOrCreateCameraGO(rt);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TryAutoAssignSequencer(camGO);

        // Seleccionar los tres assets para que el usuario los tenga a mano
        Selection.objects = new Object[] { rt, mat, camGO };
        EditorGUIUtility.PingObject(camGO);

        Debug.Log(
            "[CrystalBallVision] Setup completo.\n" +
            "Quedan 3 asignaciones manuales en LiamCrystalBallSequencer:\n" +
            "  • playerTransform   → el root del jugador (Will)\n" +
            "  • crystalBallRenderer → el MeshRenderer de la esfera de la bola\n" +
            "  • crystalBallRenderer necesita el material Mat_CrystalBallVision (o usa el tuyo con Emission ON + RT asignado)");
    }

    // ── Carpeta ───────────────────────────────────────────────────────────────

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Cinematics");
    }

    // ── RenderTexture ─────────────────────────────────────────────────────────

    private static RenderTexture GetOrCreateRenderTexture()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RTPath);
        if (existing != null) return existing;

        var rt = new RenderTexture(512, 512, 16, RenderTextureFormat.DefaultHDR)
        {
            name        = "RT_CrystalBallVision",
            wrapMode    = TextureWrapMode.Clamp,
            filterMode  = FilterMode.Bilinear,
            antiAliasing = 1
        };
        rt.Create();
        AssetDatabase.CreateAsset(rt, RTPath);
        Debug.Log($"[CrystalBallVision] RenderTexture creado en {RTPath}");
        return rt;
    }

    // ── Material ──────────────────────────────────────────────────────────────

    private static Material GetOrCreateMaterial(RenderTexture rt)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (existing != null)
        {
            // Actualizar el RT por si se recreó
            existing.SetTexture("_EmissionMap", rt);
            EditorUtility.SetDirty(existing);
            Debug.Log("[CrystalBallVision] Material existente actualizado.");
            return existing;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogError("[CrystalBallVision] Shader 'Universal Render Pipeline/Lit' no encontrado. ¿URP instalado?");
            return null;
        }

        var mat = new Material(shader) { name = "Mat_CrystalBallVision" };

        // Color base: cristal oscuro ahumado
        mat.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.12f, 1f));
        mat.SetFloat("_Metallic",   0f);
        mat.SetFloat("_Smoothness", 0.92f);

        // Emission: mapa apuntando al RT, color negro (el sequencer lo anima en runtime)
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", rt);
        mat.SetColor("_EmissionColor", Color.black);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        AssetDatabase.CreateAsset(mat, MatPath);
        Debug.Log($"[CrystalBallVision] Material creado en {MatPath}");
        return mat;
    }

    // ── GO de cámara en escena ────────────────────────────────────────────────

    private static GameObject GetOrCreateCameraGO(RenderTexture rt)
    {
        // Reutilizar si ya existe en la escena activa
        var existing = GameObject.Find(CamGoName);
        if (existing != null)
        {
            AssignRT(existing, rt);
            return existing;
        }

        var go = new GameObject(CamGoName);
        Undo.RegisterCreatedObjectUndo(go, "Crear CrystalBallVisionCamera");

        // Camera
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask     = ~0;           // todos los layers
        cam.fieldOfView     = 45f;
        cam.nearClipPlane   = 0.1f;
        cam.farClipPlane    = 150f;
        cam.targetTexture   = rt;
        cam.enabled         = false;        // el script lo activa en runtime

        // Script de seguimiento
        var vision = go.AddComponent<CrystalBallVisionCamera>();
        var so = new SerializedObject(vision);
        so.FindProperty("followOffset").vector3Value  = new Vector3(0f, 1.8f, -2.8f);
        so.FindProperty("lookAtHeight").floatValue    = 1.1f;
        so.FindProperty("followSpeed").floatValue     = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[CrystalBallVision] GO '{CamGoName}' creado en la escena activa.");
        return go;
    }

    private static void AssignRT(GameObject go, RenderTexture rt)
    {
        var cam = go.GetComponent<Camera>();
        if (cam != null && cam.targetTexture != rt)
        {
            Undo.RecordObject(cam, "Actualizar RT CrystalBallVisionCamera");
            cam.targetTexture = rt;
        }
    }

    // ── Auto-asignación en el sequencer ───────────────────────────────────────

    private static void TryAutoAssignSequencer(GameObject camGO)
    {
        var sequencer = Object.FindAnyObjectByType<LiamCrystalBallSequencer>();
        if (sequencer == null)
        {
            Debug.Log("[CrystalBallVision] LiamCrystalBallSequencer no encontrado en escena. Abre la escena correcta y vuelve a ejecutar, o asigna manualmente.");
            return;
        }

        Undo.RecordObject(sequencer, "Auto-asignar CrystalBallVisionCamera");
        var so = new SerializedObject(sequencer);

        var camProp = so.FindProperty("crystalVisionCamera");
        if (camProp != null)
            camProp.objectReferenceValue = camGO.GetComponent<CrystalBallVisionCamera>();

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(sequencer);
        EditorSceneManager.MarkSceneDirty(sequencer.gameObject.scene);

        Debug.Log("[CrystalBallVision] crystalVisionCamera auto-asignado en LiamCrystalBallSequencer.");
    }
}
