using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Crea, reutilizando arte YA COMPRADO en el proyecto (nada generado desde cero — no hay
/// herramienta de generación de modelos 3D disponible en esta sesión), un primer vestido del hub
/// de "El Sendero de las Estrellas": el altar, las tres puertas de prueba y un camino de conexión.
/// Ver `claude/escena-wiring-localizacion-2026-08-30.md` (sección 3) en el proyecto de Cowork para
/// el porqué de cada pieza (canon: `biblia-del-universo.md` + novela cap. XVII).
///
/// TODO es un punto de partida en greybox-con-arte-real (mismo criterio ya aceptado para `Floor`
/// en esta escena), NO una composición final — colores, escalas y posiciones están pensados para
/// verse razonablemente bien de primeras, pero conviene un pase visual en el Editor.
///
/// Piezas y de dónde salen (ver también informe de investigación de esta sesión):
///   - Altar: `FlameEmissionEffect.prefab` (100BestEffectPack) — una llama flotante/fuego fatuo,
///     no un mueble de piedra (rediseñado 30/08/2026 a petición de Raúl, ver comentario junto a
///     CreateAltar más abajo para el porqué de la elección frente a otros VFX candidatos).
///   - Las 3 puertas: `Portal01/02/03.prefab` de "RPG Tiny Fantasy World 01 PBR" — ya vienen con un
///     shader de portal animado (remolino) en su propio material. Se duplica ese material (nunca
///     se toca el compartido, mismo criterio que el resto de VFX de esta sesión) y se retinta:
///     dorado para la puerta de Will (confirmado "dorada" en la novela), rosa para la de Estela
///     (confirmado "rosa"/Chuchelandia), y un tono espejo/plateado-teal para la de Liam — ESTE
///     ÚLTIMO es una elección creativa mía, la novela no confirma un color para su puerta (solo
///     describe un laberinto de espejos oscuro), cámbialo si tienes otra idea.
///   - Camino: 4 tramos de `RoadA01.prefab` (mismo pack) entre la zona de puertas y el altar.
///
/// Requiere que `SenderoFinalSceneWiring` ya se haya ejecutado (usa `BATTLE ARENA`/`BossSpawn` como
/// referencia de posición) — si no, avisa y no hace nada.
/// </summary>
public static class SenderoHubSetDressing
{
    const string PackRoot = "Assets/Art/World/RPG Tiny Fantasy World 01 PBR";
    const string OutputMatFolder = "Assets/_SET_DRESSING/SenderoHub/Materials";

    [MenuItem("El Sendero/Escena/Crear Set Dressing del Hub (Altar, Puertas, Camino)")]
    public static void CreateSetDressing()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Sendero")
        {
            Debug.LogWarning("[SenderoHubSetDressing] La escena activa no es 'Sendero'. Abre Assets/Scenes/Worlds/Sendero.unity y vuelve a ejecutar.");
            return;
        }

        var battleArenaRoot = GameObject.Find("BATTLE ARENA");
        var bossSpawn = GameObject.Find("BossSpawn");
        if (battleArenaRoot == null || bossSpawn == null)
        {
            Debug.LogError("[SenderoHubSetDressing] Falta 'BATTLE ARENA' y/o 'BossSpawn' — ejecuta primero 'El Sendero/Escena/Rellenar Referencias de la Batalla Final'.");
            return;
        }
        Vector3 center = bossSpawn.transform.position;

        EnsureFolder(OutputMatFolder);

        var container = GameObject.Find("SET_DRESSING_Hub");
        if (container == null)
        {
            container = new GameObject("SET_DRESSING_Hub");
            Undo.RegisterCreatedObjectUndo(container, "Crear SET_DRESSING_Hub");
            container.transform.SetParent(battleArenaRoot.transform, false);
        }

        int created = 0;
        created += CreateAltar(container.transform, center) ? 1 : 0;
        created += CreateDoor(container.transform, "Puerta_Will_Dorada", "Portal01", center + new Vector3(0f, 0f, -9f),
            new Color(1f, 0.75f, 0.15f) * 3f, "confirmado 'dorada' en la novela") ? 1 : 0;
        created += CreateDoor(container.transform, "Puerta_Estela_Rosa", "Portal02", center + new Vector3(-4.5f, 0f, -8.5f),
            new Color(1f, 0.25f, 0.55f) * 3f, "confirmado 'rosa' (Chuchelandia) en la novela") ? 1 : 0;
        created += CreateDoor(container.transform, "Puerta_Liam_Espejo", "Portal03", center + new Vector3(4.5f, 0f, -8.5f),
            new Color(0.45f, 0.9f, 0.95f) * 2.5f, "SIN confirmar en canon — elección creativa (tono espejo/laberinto oscuro)") ? 1 : 0;
        created += CreatePath(container.transform, center) ? 1 : 0;

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SenderoHubSetDressing] Set dressing del hub: {created} piezas creadas (o ya existían, sin tocar). " +
            "Es un primer vestido con arte real reutilizado del proyecto, NO una composición final — revisa a ojo " +
            "posiciones, escalas y el color de la puerta de Liam (sin confirmar en canon). Guarda la escena (Ctrl+S).");
    }

    // ── Altar (rediseñado 30/08/2026) ───────────────────────────────────────────
    // Raúl, tras ver el altar de piedra+orbe original: "he pensado que el altar puede ser como
    // una llamita flotando o un fuego fatuo o algo que tu puedas crear". Se sustituye el podio de
    // piedra (podium.fbx) + la esfera emisiva por una única llama flotante — encaja mejor con la
    // naturaleza del propio Sendero ("un espacio suspendido entre estrellas... suelo de luz
    // condensada", biblia-del-universo.md): un fuego fatuo ingrávido en vez de un mueble de piedra.
    //
    // VFX: Assets/VFX/100BestEffectPack/Effects/FlameEmissionEffect/FlameEmissionEffect.prefab —
    // revisado a fondo antes de elegirlo (ver claude/escena-wiring-localizacion-2026-08-30.md,
    // sección 6): un GameObject raíz autocontenido con una partícula de humo/llama
    // ("FlameEmission-Smoke") y una Point Light naranja cálida (Intensity 8.6, Range 20) como
    // hijos — pensado de origen para arder QUIETO en un sitio fijo, a diferencia del otro
    // candidato revisado (fireBallEffect.prefab, el VFX de "Llama Astral"/hechizo de Will) que
    // lleva un mesh de distorsión de aire con una orientación direccional (-90/90 en Y/Z) pensada
    // para un proyectil en movimiento, no para quedarse flotando en un punto.
    // Instancia directa del prefab compartido (no una variante nueva): no se retoca ningún
    // material ni se necesita ningún override, así que no aplica el criterio de "nunca tocar un
    // asset compartido, duplicar primero" que sí se ha seguido con el resto de VFX de esta sesión.
    //
    // Movimiento: BalloonFloatingMotion (Assets/Scripts/World/BalloonFloatingMotion.cs) — ya
    // existe en el proyecto (para los globos) y, pese al nombre, es un componente 100% genérico de
    // oscilación vertical/horizontal + rotación suave, así que se reutiliza tal cual en vez de
    // escribir un script nuevo. Amplitudes reducidas respecto a sus valores por defecto (pensados
    // para un globo grande) para que la llama "tiemble/derive" como un fuego fatuo, no que oscile
    // como un globo.
    const string FlameVfxPath = "Assets/VFX/100BestEffectPack/Effects/FlameEmissionEffect/FlameEmissionEffect.prefab";

    static bool CreateAltar(Transform parent, Vector3 center)
    {
        if (parent.Find("Altar") != null) return false;

        var altar = new GameObject("Altar");
        Undo.RegisterCreatedObjectUndo(altar, "Crear Altar");
        altar.transform.SetParent(parent, false);
        altar.transform.position = center + new Vector3(0f, 1.1f, 1.5f); // altura de "flotando", no a ras de suelo

        var flameSrc = AssetDatabase.LoadAssetAtPath<GameObject>(FlameVfxPath);
        if (flameSrc != null)
        {
            var flame = (GameObject)PrefabUtility.InstantiatePrefab(flameSrc, altar.transform);
            flame.name = "AltarFlame";
            flame.transform.localPosition = Vector3.zero;
            flame.transform.localRotation = Quaternion.identity;

            var bob = flame.AddComponent<BalloonFloatingMotion>();
            var so = new SerializedObject(bob);
            so.FindProperty("verticalAmplitude").floatValue = 0.12f;
            so.FindProperty("verticalSpeed").floatValue = 0.9f;
            so.FindProperty("horizontalAmplitude").floatValue = 0.06f;
            so.FindProperty("horizontalSpeed").floatValue = 0.6f;
            so.FindProperty("rotationAmount").floatValue = 6f;
            so.FindProperty("rotationSpeed").floatValue = 0.4f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning($"[SenderoHubSetDressing] No se encontró {FlameVfxPath} — Altar creado vacío (sin llama).");
        }

        return true;
    }

    static bool CreateDoor(Transform parent, string goName, string portalPrefabName, Vector3 position, Color hdrColor, string colorNote)
    {
        if (parent.Find(goName) != null) return false;

        var prefabPath = $"{PackRoot}/Prefab/BuildingUtilityDeco/{portalPrefabName}.prefab";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (src == null)
        {
            Debug.LogWarning($"[SenderoHubSetDressing] No se encontró {prefabPath} — no se crea {goName}.");
            return false;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        instance.name = goName;
        instance.transform.position = position;

        var origMatPath = $"{PackRoot}/Material/Special/{portalPrefabName}.mat";
        var origMat = AssetDatabase.LoadAssetAtPath<Material>(origMatPath);
        var tintedMat = GetOrCreateMaterial($"Mat_{goName}", null, hdrColor, origMat);
        if (origMat != null && tintedMat != null)
        {
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == origMat) { mats[i] = tintedMat; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        Debug.Log($"[SenderoHubSetDressing] {goName}: color — {colorNote}.");
        return true;
    }

    static bool CreatePath(Transform parent, Vector3 center)
    {
        if (parent.Find("Camino") != null) return false;

        var pathRoot = new GameObject("Camino");
        Undo.RegisterCreatedObjectUndo(pathRoot, "Crear Camino");
        pathRoot.transform.SetParent(parent, false);

        var segPath = $"{PackRoot}/Prefab/RiverRoadLakeFall/RoadA01.prefab";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(segPath);
        if (src == null)
        {
            Debug.LogWarning($"[SenderoHubSetDressing] No se encontró {segPath} — Camino creado sin tramos.");
            return true;
        }

        float[] zOffsets = { -8f, -5f, -2f, 1f };
        for (int i = 0; i < zOffsets.Length; i++)
        {
            var seg = (GameObject)PrefabUtility.InstantiatePrefab(src, pathRoot.transform);
            seg.name = $"CaminoTramo_{i + 1}";
            seg.transform.position = center + new Vector3(0f, 0f, zOffsets[i]);
        }
        return true;
    }

    static Material GetOrCreateMaterial(string name, Color? baseColor, Color emissionOrTint, Material sourceToCopy = null)
    {
        var path = $"{OutputMatFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Material mat;
        if (sourceToCopy != null)
        {
            var sourcePath = AssetDatabase.GetAssetPath(sourceToCopy);
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                Debug.LogWarning($"[SenderoHubSetDressing] No se pudo duplicar {sourcePath} -> {path}.");
                return null;
            }
            mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            mat.SetColor("_Color", emissionOrTint);
        }
        else
        {
            var shader = Shader.Find("Quibli/Stylized Lit");
            if (shader == null)
            {
                Debug.LogWarning("[SenderoHubSetDressing] No se encontró el shader 'Quibli/Stylized Lit'.");
                return null;
            }
            mat = new Material(shader);
            if (baseColor.HasValue && mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor.Value);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionOrTint);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parts = folder.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
