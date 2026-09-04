using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba de Estela en el Sendero (GDD/biblia-del-universo.md — puerta rosa, Chuchelandia): la
/// corte la corona "Reina de las Chuches" a la fuerza, el Duque de Regaliz exige alegría fingida y
/// tiene encerrada a una criatura de mazapán por no sonreír, Estela se niega a fingir y libera al
/// reino siendo exactamente quien es. Will y Liam están presentes y participan — la corrección de
/// canon del 30 ago 2026 confirma que esta prueba (y la de Will) las vive el grupo entero, no
/// Estela sola (ver GDD.md línea ~225). Liam concretamente hace de cortesano ridículo y resulta
/// sorprendentemente bueno en el papel (biblia-del-universo.md, ficha de Liam).
///
/// Ninguno de los ~150 NPCs ambientales de Sweet_Land ya presentes en CandyLand.unity es
/// obviamente "el Duque de Regaliz" ni "una criatura de mazapán enjaulada"
/// (propuesta-interaccion-npcs-candyland-2026-08-27.md). Esta herramienta los resuelve por reskin:
/// - Duque de Regaliz = Candy_King_01 (ya es un personaje "rey", el que más se acerca por porte/rol)
/// - Criatura enjaulada = Marshie_01 (forma blanda y redonda, la más cercana a mazapán moldeado)
/// recoloreando SUS MATERIALES YA EXISTENTES (duplicados, nunca los originales — así no afecta a
/// las ~150 instancias ambientales que siguen usando esos mismos materiales) en vez de encargar
/// arte nuevo. Es un reskin, no un personaje nuevo modelado — encaja con "meter cualquier cosilla
/// de prueba" de Raúl; si el resultado no convence a ojo, es fácil de ajustar (los colores están
/// todos en DuqueRegalizPalette/MazapanPalette, arriba del todo del archivo).
///
/// Deliberadamente NO usa NPCInteractiveNarrativeExecutor ni el grafo narrativo real — ver el
/// comentario de CandylandCoronationTrigger.cs. El diálogo es un DialogueAsset de grupo
/// (isGroupConversation = true) usando el sistema de foco de mirada ya construido
/// (sistema-foco-mirada-dialogos-grupales-2026-09-01.md).
///
/// De paso arregla las dos incidencias menores ya diagnosticadas
/// (incidencia-candyland-suelo-blanco-2026-09-01.md): el normal map de Ground.mat sin asignar
/// (suelo quemado a blanco) y el DayNightCycle.directionalLight sin enganchar en esta escena.
///
/// Idempotente: se puede volver a ejecutar sin duplicar nada ni perder ajustes manuales de
/// posición/rotación ya hechos por Raúl sobre el Duque, la criatura o la jaula (solo se tocan la
/// primera vez que se crean).
///
/// *** COLOCACIÓN EN LA ESCENA ***
/// No hay forma de ver el layout real de CandyLand.unity desde aquí, así que todo el conjunto se
/// crea en el origen de la escena (0,0,0), bajo un único GameObject
/// "CANDYLAND_CLIMAX_CORONACION". Arrastra ESE objeto una vez a la plaza/corte donde quieras la
/// escena — todo lo demás (Duque, criatura, jaula, trigger) son hijos suyos y se mueven juntos.
/// </summary>
public static class CandylandClimaxBuilder
{
    private const string ScenePath = "Assets/Scenes/Worlds/CandyLand.unity";
    private const string RootName = "CANDYLAND_CLIMAX_CORONACION";

    private const string CandyKingPath = "Assets/Art/World/ithappy/Sweet_Land/Characters/Prefabs/Candy_King_01.prefab";
    private const string MarshiePath = "Assets/Art/World/ithappy/Sweet_Land/Characters/Prefabs/Marshie_01.prefab";
    private const string GatePath = "Assets/Art/World/ithappy/Sweet_Land/Prefabs/Interaction/Gate_01.prefab";

    private const string MaterialsFolder = "Assets/_NPCs/Candyland/Materials";
    private const string DialogueFolder = "Assets/_DIALOGUES/DIALOGUE NPCS/Candyland";
    private const string DialogueAssetPath = DialogueFolder + "/DG_CANDYLAND_CORONACION.asset";

    private const string GroundMatPath = "Assets/Art/World/ithappy/Sweet_Land/Materials/Ground.mat";
    private const string GroundNormalMapPath = "Assets/Art/World/ithappy/Sweet_Land/Textures/Ground_Normal.png";

    // Paleta del Duque de Regaliz: negro regaliz / rojo vino oscuro / blanco hueso (detalles).
    // Se reparte por índice de material (módulo) — ajusta aquí si el resultado no convence a ojo.
    private static readonly Color[] DuqueRegalizPalette =
    {
        new Color(0.05f, 0.03f, 0.04f),
        new Color(0.42f, 0.05f, 0.08f),
        new Color(0.88f, 0.86f, 0.80f),
    };

    // Paleta "después" del Duque (cuando su reino de mentiras se resquebraja): tonos apagados/grises.
    private static readonly Color[] DuqueRegalizPaletteAfter =
    {
        new Color(0.15f, 0.14f, 0.14f),
        new Color(0.30f, 0.28f, 0.27f),
        new Color(0.55f, 0.53f, 0.50f),
    };

    // Paleta de la criatura de mazapán: crema / tostado almendra.
    private static readonly Color[] MazapanPalette =
    {
        new Color(0.93f, 0.86f, 0.72f),
        new Color(0.80f, 0.65f, 0.45f),
    };

    [MenuItem("El Sendero/Escena/Construir Clímax de Candyland (Duque de Regaliz)")]
    public static void BuildClimax()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError($"[CandylandClimaxBuilder] No existe {ScenePath}. Abortando.");
            return;
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var logLines = new List<string>();
        logLines.Add("=== Clímax de Candyland (coronación / Duque de Regaliz) ===");

        FixGroundNormalMap(logLines);
        FixDayNightCycleLight(logLines);

        GameObject root = GameObject.Find(RootName);
        bool rootIsNew = root == null;
        if (rootIsNew)
        {
            root = new GameObject(RootName);
            logLines.Add($"Creado {RootName} en el origen — muévelo a la plaza de Chuchelandia (ver aviso arriba del archivo).");
        }

        EnsureFolder(MaterialsFolder);
        EnsureFolder(DialogueFolder);

        // --- Duque de Regaliz (reskin de Candy_King_01) ---
        Transform duqueTransform = root.transform.Find("Duque_de_Regaliz");
        GameObject duqueGO;
        if (duqueTransform == null)
        {
            GameObject candyKingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CandyKingPath);
            if (candyKingPrefab == null)
            {
                Debug.LogError($"[CandylandClimaxBuilder] No se encontró {CandyKingPath}. Abortando Duque.");
                return;
            }
            duqueGO = (GameObject)PrefabUtility.InstantiatePrefab(candyKingPrefab, root.transform);
            duqueGO.name = "Duque_de_Regaliz";
            duqueGO.transform.localPosition = new Vector3(-2f, 0f, 3f);

            ReskinRenderers(duqueGO, DuqueRegalizPalette, "DuqueRegaliz", logLines);
            logLines.Add("Duque de Regaliz creado (reskin de Candy_King_01).");
        }
        else
        {
            duqueGO = duqueTransform.gameObject;
            logLines.Add("Duque de Regaliz ya existía — no se toca su posición ni su reskin actual.");
        }
        // Los materiales "después" (reino resquebrajado) se calculan siempre a partir del reskin
        // ACTUAL del Duque, exista ya o se acabe de crear — así siguen coincidiendo si Raúl ajusta
        // a mano los colores del reskin "antes" y vuelve a ejecutar esta herramienta.
        Dictionary<Renderer, Material[]> duqueAfterByRenderer = BuildAfterMaterialsPerRenderer(duqueGO, DuqueRegalizPaletteAfter, "DuqueRegaliz_Resquebrajado");

        // --- Criatura de mazapán enjaulada (reskin de Marshie_01) ---
        Transform criaturaTransform = root.transform.Find("Criatura_de_Mazapan");
        GameObject criaturaGO;
        if (criaturaTransform == null)
        {
            GameObject marshiePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarshiePath);
            if (marshiePrefab == null)
            {
                Debug.LogError($"[CandylandClimaxBuilder] No se encontró {MarshiePath}. Abortando criatura.");
                return;
            }
            criaturaGO = (GameObject)PrefabUtility.InstantiatePrefab(marshiePrefab, root.transform);
            criaturaGO.name = "Criatura_de_Mazapan";
            criaturaGO.transform.localPosition = new Vector3(2.5f, 0f, 3f);

            ReskinRenderers(criaturaGO, MazapanPalette, "Mazapan", logLines);
            logLines.Add("Criatura de mazapán creada (reskin de Marshie_01).");
        }
        else
        {
            criaturaGO = criaturaTransform.gameObject;
            logLines.Add("Criatura de mazapán ya existía — no se toca.");
        }

        // --- Jaula (un Gate de Sweet_Land delante de la criatura) ---
        Transform jaulaTransform = root.transform.Find("Jaula");
        GameObject jaulaGO;
        if (jaulaTransform == null)
        {
            GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePath);
            if (gatePrefab == null)
            {
                Debug.LogWarning($"[CandylandClimaxBuilder] No se encontró {GatePath}, se omite la jaula visual.");
                jaulaGO = null;
            }
            else
            {
                jaulaGO = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab, root.transform);
                jaulaGO.name = "Jaula";
                jaulaGO.transform.position = criaturaGO.transform.position + new Vector3(0f, 0f, -1f);
                jaulaGO.transform.rotation = Quaternion.identity;
                logLines.Add("Jaula colocada delante de la criatura (Gate_01 de Sweet_Land).");
            }
        }
        else
        {
            jaulaGO = jaulaTransform.gameObject;
            logLines.Add("Jaula ya existía — no se toca.");
        }

        // --- Diálogo de grupo (coronación → negativa de Estela → liberación) ---
        DialogueAsset dialogueAsset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(DialogueAssetPath);
        if (dialogueAsset == null)
        {
            dialogueAsset = BuildDialogueAsset();
            AssetDatabase.CreateAsset(dialogueAsset, DialogueAssetPath);
            logLines.Add($"Diálogo creado: {DialogueAssetPath} (17 líneas, conversación de grupo).");
        }
        else
        {
            logLines.Add("El diálogo ya existía — no se sobrescribe (edítalo a mano o bórralo para regenerarlo).");
        }

        // --- Trigger de disparo ---
        Transform triggerTransform = root.transform.Find("Trigger_Coronacion");
        if (triggerTransform == null)
        {
            var triggerGO = new GameObject("Trigger_Coronacion");
            triggerGO.transform.SetParent(root.transform);
            triggerGO.transform.localPosition = new Vector3(0f, 1f, -2f);
            var box = triggerGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(6f, 3f, 4f);

            var triggerComp = triggerGO.AddComponent<CandylandCoronationTrigger>();
            var so = new SerializedObject(triggerComp);
            so.FindProperty("dialogue").objectReferenceValue = dialogueAsset;
            so.FindProperty("cageObject").objectReferenceValue = jaulaGO;

            var swapsProp = so.FindProperty("duqueAfterSwaps");
            swapsProp.arraySize = duqueAfterByRenderer.Count;
            int swapIndex = 0;
            foreach (var kvp in duqueAfterByRenderer)
            {
                var swapElement = swapsProp.GetArrayElementAtIndex(swapIndex);
                swapElement.FindPropertyRelative("renderer").objectReferenceValue = kvp.Key;
                var afterMatsProp = swapElement.FindPropertyRelative("afterMaterials");
                afterMatsProp.arraySize = kvp.Value.Length;
                for (int i = 0; i < kvp.Value.Length; i++) afterMatsProp.GetArrayElementAtIndex(i).objectReferenceValue = kvp.Value[i];
                swapIndex++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            logLines.Add("Trigger_Coronacion creado y enganchado al diálogo + jaula + materiales 'después' del Duque.");
        }
        else
        {
            logLines.Add("Trigger_Coronacion ya existía — no se toca.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        logLines.Add("--- Revisa a ojo: posición de la escena, colores del reskin, y las 17 líneas del diálogo (Assets/_DIALOGUES/DIALOGUE NPCS/Candyland/) ---");
        Debug.Log(string.Join("\n", logLines));
    }

    private static void FixGroundNormalMap(List<string> log)
    {
        Material groundMat = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
        if (groundMat == null)
        {
            log.Add($"AVISO: no se encontró {GroundMatPath}, no se pudo comprobar el bug del suelo.");
            return;
        }
        if (!groundMat.HasProperty("_BumpMap"))
        {
            log.Add("Ground.mat no tiene _BumpMap (¿shader inesperado?) — omitido.");
            return;
        }
        if (groundMat.GetTexture("_BumpMap") != null)
        {
            log.Add("Ground.mat ya tenía normal map asignado — bug ya resuelto, no se toca.");
            return;
        }
        Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundNormalMapPath);
        if (normalMap == null)
        {
            log.Add($"AVISO: no se encontró {GroundNormalMapPath} para arreglar el suelo.");
            return;
        }
        groundMat.SetTexture("_BumpMap", normalMap);
        groundMat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(groundMat);
        log.Add("FIX: Ground.mat — normal map asignado (INC del suelo blanco quemado resuelta).");
    }

    private static void FixDayNightCycleLight(List<string> log)
    {
        var cycle = Object.FindFirstObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            log.Add("AVISO: no se encontró ningún DayNightCycle en la escena.");
            return;
        }
        var so = new SerializedObject(cycle);
        var lightProp = so.FindProperty("directionalLight");
        if (lightProp.objectReferenceValue != null)
        {
            log.Add("DayNightCycle.directionalLight ya estaba asignado — no se toca.");
            return;
        }
        GameObject lightGO = GameObject.Find("Directional light");
        Light light = lightGO != null ? lightGO.GetComponent<Light>() : null;
        if (light == null)
        {
            log.Add("AVISO: no se encontró la 'Directional light' de la escena para enganchar al DayNightCycle.");
            return;
        }
        lightProp.objectReferenceValue = light;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("FIX: DayNightCycle.directionalLight enganchado a 'Directional light'.");
    }

    // Duplica (nunca reutiliza) cada material único de los renderers del GameObject, lo recolorea
    // con la paleta dada (por índice, cíclico) y lo reasigna — así las ~150 instancias ambientales
    // de Sweet_Land que comparten esos materiales originales no se ven afectadas.
    private static Material[] ReskinRenderers(GameObject go, Color[] palette, string namePrefix, List<string> log)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var cache = new Dictionary<Material, Material>();
        var applied = new List<Material>();
        int paletteIndex = 0;

        foreach (var renderer in renderers)
        {
            var mats = renderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material original = mats[i];
                if (original == null) continue;
                if (!cache.TryGetValue(original, out Material dup))
                {
                    dup = new Material(original) { name = $"{namePrefix}_{original.name}" };
                    if (dup.HasProperty("_BaseColor"))
                    {
                        dup.SetColor("_BaseColor", palette[paletteIndex % palette.Length]);
                    }
                    else if (dup.HasProperty("_Color"))
                    {
                        dup.SetColor("_Color", palette[paletteIndex % palette.Length]);
                    }
                    paletteIndex++;
                    string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{MaterialsFolder}/{dup.name}.mat");
                    AssetDatabase.CreateAsset(dup, assetPath);
                    cache[original] = dup;
                    applied.Add(dup);
                }
                mats[i] = dup;
            }
            renderer.sharedMaterials = mats;
        }
        log.Add($"{namePrefix}: {cache.Count} material(es) duplicado(s) y recoloreado(s) bajo {MaterialsFolder}.");
        return applied.ToArray();
    }

    // Construye (sin asignar todavía) los materiales "después" del Duque a partir de sus
    // materiales ACTUALES (ya reskineados), para el beat de "el reino se resquebraja". Devuelve
    // un array COMPLETO por renderer (mismo número de slots que su sharedMaterials actual), para
    // poder reasignarlo de golpe con Renderer.sharedMaterials sin desajustar índices.
    private static Dictionary<Renderer, Material[]> BuildAfterMaterialsPerRenderer(GameObject duqueGO, Color[] afterPalette, string namePrefix)
    {
        var renderers = duqueGO.GetComponentsInChildren<Renderer>();
        var result = new Dictionary<Renderer, Material[]>();
        var cache = new Dictionary<Material, Material>();
        int paletteIndex = 0;

        foreach (var renderer in renderers)
        {
            var currentMats = renderer.sharedMaterials;
            var afterMats = new Material[currentMats.Length];
            for (int i = 0; i < currentMats.Length; i++)
            {
                Material mat = currentMats[i];
                if (mat == null) continue;
                if (!cache.TryGetValue(mat, out Material afterMat))
                {
                    string afterName = $"{namePrefix}_{paletteIndex}";
                    string afterPath = $"{MaterialsFolder}/{afterName}.mat";
                    afterMat = AssetDatabase.LoadAssetAtPath<Material>(afterPath);
                    if (afterMat == null)
                    {
                        afterMat = new Material(mat) { name = afterName };
                        Color c = afterPalette[paletteIndex % afterPalette.Length];
                        if (afterMat.HasProperty("_BaseColor")) afterMat.SetColor("_BaseColor", c);
                        else if (afterMat.HasProperty("_Color")) afterMat.SetColor("_Color", c);
                        AssetDatabase.CreateAsset(afterMat, afterPath);
                    }
                    paletteIndex++;
                    cache[mat] = afterMat;
                }
                afterMats[i] = afterMat;
            }
            result[renderer] = afterMats;
        }
        return result;
    }

    private static DialogueAsset BuildDialogueAsset()
    {
        var asset = ScriptableObject.CreateInstance<DialogueAsset>();
        asset.isGroupConversation = true;

        var lines = new List<DialogueLine>();
        AddLine(lines, "CHAR_DUQUE_REGALIZ", "DLG_CANDYLAND_01", NPCEmotion.Happy, false, null);
        AddLine(lines, "CHAR_LIAM", "DLG_CANDYLAND_02", NPCEmotion.Smirk, false, null);
        AddLine(lines, "CHAR_WILL", "DLG_CANDYLAND_03", NPCEmotion.Surprised, true, "CHAR_LIAM");
        AddLine(lines, "CHAR_LIAM", "DLG_CANDYLAND_04", NPCEmotion.Neutral, false, null);
        AddLine(lines, "CHAR_DUQUE_REGALIZ", "DLG_CANDYLAND_05", NPCEmotion.Angry, false, "CHAR_ESTELA");
        AddLine(lines, "CHAR_ESTELA", "DLG_CANDYLAND_06", NPCEmotion.Smirk, false, "CHAR_DUQUE_REGALIZ");
        AddLine(lines, "CHAR_DUQUE_REGALIZ", "DLG_CANDYLAND_07", NPCEmotion.Angry, false, "CHAR_ESTELA");
        AddLine(lines, "CHAR_MAZAPAN", "DLG_CANDYLAND_08", NPCEmotion.Scared, false, null);
        AddLine(lines, "CHAR_ESTELA", "DLG_CANDYLAND_09", NPCEmotion.Angry, false, "CHAR_DUQUE_REGALIZ");
        AddLine(lines, "CHAR_WILL", "DLG_CANDYLAND_10", NPCEmotion.Happy, true, "CHAR_ESTELA");
        AddLine(lines, "CHAR_LIAM", "DLG_CANDYLAND_11", NPCEmotion.Neutral, false, null);
        AddLine(lines, "CHAR_DUQUE_REGALIZ", "DLG_CANDYLAND_12", NPCEmotion.Angry, false, "CHAR_ESTELA");
        AddLine(lines, "CHAR_ESTELA", "DLG_CANDYLAND_13", NPCEmotion.Smirk, false, null);
        AddLine(lines, "CHAR_MAZAPAN", "DLG_CANDYLAND_14", NPCEmotion.Happy, false, null);
        AddLine(lines, "CHAR_DUQUE_REGALIZ", "DLG_CANDYLAND_15", NPCEmotion.Sad, false, null);
        AddLine(lines, "CHAR_ESTELA", "DLG_CANDYLAND_16", NPCEmotion.Happy, false, null);
        AddLine(lines, "CHAR_LIAM", "DLG_CANDYLAND_17", NPCEmotion.Neutral, false, null);

        asset.lines = lines.ToArray();
        return asset;
    }

    private static void AddLine(List<DialogueLine> lines, string speakerId, string textId, NPCEmotion emotion, bool isPlayer, string lookAtOverride)
    {
        lines.Add(new DialogueLine
        {
            speakerNameId = speakerId,
            textId = textId,
            text = "",
            portrait = null,
            isPlayerSpeaking = isPlayer,
            lookAtOverrideId = lookAtOverride,
            emotion = emotion,
        });
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
