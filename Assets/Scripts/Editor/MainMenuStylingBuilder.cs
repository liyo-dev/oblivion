using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Herramienta de Editor que da una primera pasada de "vida" al MainMenu: fondo de cristal en las
/// filas del menú (Continuar/Nueva Partida/Configuración/Controles), deriva de cámara sobre el
/// reino (MainMenuWorldCameraDrift), nubes de Assets/Prefabs/Clouds (shader Quibli Cloud3D)
/// derivando por el cielo, y los 3 héroes (_LIAM/_ESTELA/_WILL) anclados en pantalla con
/// MainMenuFlyingCompanion, en pose de vuelo.
///
/// IMPORTANTE — esto es un punto de partida, no un resultado final: las posiciones de nubes y
/// personajes se calculan relativas a la cámara actual (para no depender de coordenadas absolutas
/// que no tengo forma de verificar sin ver la escena renderizada), pero los valores concretos
/// (distancia, altura, escala, velocidad) son estimaciones razonables, no medidas. Después de
/// ejecutar esto, entra en la Scene view y ajusta a ojo lo que no encaje — todo está expuesto en
/// el Inspector de cada componente (MainMenuWorldCameraDrift, MainMenuFlyingCompanion, CloudDrift).
///
/// Uso: Assets → menú "El Sendero → Controles → Estilizar Main Menu (nubes, cámara, personajes,
/// botones)". Requiere que MainMenuWorldCameraDrift.cs, MainMenuFlyingCompanion.cs y CloudDrift.cs
/// ya estén en el proyecto y compilando.
/// </summary>
public static class MainMenuStylingBuilder
{
    const string ScenePath = "Assets/Scenes/Systems/MainMenu.unity";
    const string GlassRowSpritePath = "Assets/Art/UI/Menu/menu_row_glass.png";

    static readonly string[] CloudPrefabPaths =
    {
        "Assets/Prefabs/Clouds/Cloud3D-MeshCarrier_Cloud_01.prefab",
        "Assets/Prefabs/Clouds/Cloud3D-MeshCarrier_Cloud_02.prefab",
        "Assets/Prefabs/Clouds/Cloud3D-MeshCarrier_Cloud_03.prefab",
    };

    // path, offset X (izq/der en pantalla), offset Z (profundidad delante de cámara), escala
    // zOffset reducido para acercarlos a cámara (pedido del usuario tras ver el primer resultado).
    static readonly (string path, float xOffset, float zOffset, float scale)[] Companions =
    {
        ("Assets/Prefabs/_LIAM.prefab", -2.4f, 4.5f, 0.5f),
        ("Assets/Prefabs/_ESTELA.prefab", 0f, 6f, 0.45f),
        ("Assets/Prefabs/_WILL.prefab", 2.4f, 4.5f, 0.5f),
    };

    [MenuItem("El Sendero/Controles/Estilizar Main Menu (nubes, cámara, personajes, botones)")]
    public static void Style()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuStylingBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuStylingBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuStylingBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        try
        {
            StyleButtonRows();
            var cam = AddCameraDrift();
            AddClouds(cam);
            AddFlyingCompanions(cam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MainMenuStylingBuilder] ✅ Primera pasada de estilo aplicada y guardada. Es un punto de partida — " +
                      "entra en la Scene view y ajusta a ojo posiciones/velocidades/escalas (todo expuesto en el Inspector " +
                      "de MainMenuWorldCameraDrift / MainMenuFlyingCompanion / CloudDrift).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MainMenuStylingBuilder] Error durante la construcción (la escena NO se ha guardado): {e}");
        }
    }

    // ── Skybox fijo del menú ─────────────────────────────────────────────
    // El usuario ha añadido Assets/Plugins/Quibli/Demos/City/Materials/City_Skybox.mat al proyecto
    // y quiere que sea SIEMPRE el skybox de MainMenu, sin que nada lo cambie. Confirmado: no hay
    // ningún script en MainMenu.unity que toque RenderSettings.skybox en tiempo de ejecución (el
    // día/noche real, DayNightCycle.cs, solo se usa en escenas de mundo — MainWorld/Sendero/
    // CandyLand/PlayerTest — no está presente en esta escena), así que basta con dejarlo fijado
    // aquí, en el Lighting Settings de la propia escena: no hace falta ningún guardarraíl en
    // código, porque no hay nada que pueda pisarlo.
    const string MenuSkyboxMaterialPath = "Assets/Plugins/Quibli/Demos/City/Materials/City_Skybox.mat";

    [MenuItem("El Sendero/Controles/Fijar Skybox del Menú (City_Skybox)")]
    public static void SetMenuSkybox()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuStylingBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuStylingBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var skyboxMaterial = AssetDatabase.LoadAssetAtPath<Material>(MenuSkyboxMaterialPath);
        if (skyboxMaterial == null)
        {
            Debug.LogError($"[MainMenuStylingBuilder] No se encontró {MenuSkyboxMaterialPath} — confirma que el material ya está importado en el proyecto.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuStylingBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        RenderSettings.skybox = skyboxMaterial;
        DynamicGI.UpdateEnvironment();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[MainMenuStylingBuilder] ✅ Skybox de '{scene.name}' fijado a '{skyboxMaterial.name}' y guardado en el Lighting Settings de la escena. " +
                  "No hay ningún script en esta escena que lo cambie en tiempo de ejecución, así que se queda fijo para siempre.");
    }

    // ── Ajuste de la oscilación de vuelo de los personajes ya colocados ────
    // A diferencia de AddFlyingCompanions() (que es idempotente y se salta todo si
    // 'FlyingCompanions_Menu' ya existe), esto SÍ toca los MainMenuFlyingCompanion ya presentes en
    // la escena: sobrescribe bob/roll/pitch con los valores pedidos, sin tocar posición/cámara/
    // pose de vuelo. Pensado para iterar el "feel" del vuelo tantas veces como haga falta sin tener
    // que borrar y regenerar los personajes cada vez.
    const float SubtleBobAmplitude = 0.008f;
    const float SubtleBobSpeed = 0.45f;
    const float SubtleRollAmplitudeDegrees = 0.15f;
    const float SubtleRollSpeed = 0.4f;
    const float SubtlePitchAmplitudeDegrees = 0.12f;
    const float SubtlePitchSpeed = 0.35f;

    [MenuItem("El Sendero/Controles/Ajustar Oscilación de Vuelo (más sutil)")]
    public static void MakeFlyingMotionMoreSubtle()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[MainMenuStylingBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[MainMenuStylingBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError($"[MainMenuStylingBuilder] No se pudo abrir {ScenePath}.");
            return;
        }

        var container = FindByNameIncludingInactive("FlyingCompanions_Menu");
        if (container == null)
        {
            Debug.LogError("[MainMenuStylingBuilder] No existe 'FlyingCompanions_Menu' en la escena — ejecuta primero " +
                            "'Estilizar Main Menu' para colocar a los personajes.");
            return;
        }

        var companions = container.GetComponentsInChildren<MainMenuFlyingCompanion>(true);
        foreach (var companion in companions)
        {
            var so = new SerializedObject(companion);
            so.FindProperty("bobAmplitude").floatValue = SubtleBobAmplitude;
            so.FindProperty("bobSpeed").floatValue = SubtleBobSpeed;
            so.FindProperty("rollAmplitudeDegrees").floatValue = SubtleRollAmplitudeDegrees;
            so.FindProperty("rollSpeed").floatValue = SubtleRollSpeed;
            so.FindProperty("pitchAmplitudeDegrees").floatValue = SubtlePitchAmplitudeDegrees;
            so.FindProperty("pitchSpeed").floatValue = SubtlePitchSpeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log($"[MainMenuStylingBuilder] ✅ Oscilación de vuelo reducida en {companions.Length} personaje(s) " +
                  $"(bob={SubtleBobAmplitude}, roll={SubtleRollAmplitudeDegrees}°, pitch={SubtlePitchAmplitudeDegrees}°).");
    }

    // ── Fondo de cristal en las filas del menú ──────────────────────────

    static void StyleButtonRows()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlassRowSpritePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[MainMenuStylingBuilder] No se encontró {GlassRowSpritePath} como Sprite (¿está importado " +
                              "como Texture Type = Sprite (2D and UI)?) — se omite el fondo de cristal de los botones.");
            return;
        }

        var buttonPanel = FindByNameIncludingInactive("ButtonPanel");
        if (buttonPanel == null)
        {
            Debug.LogWarning("[MainMenuStylingBuilder] No se encontró 'ButtonPanel' — se omite el estilo de las filas del menú.");
            return;
        }

        var buttons = buttonPanel.GetComponentsInChildren<Button>(true);

        // Medir el tamaño ACTUAL de cada botón antes de tocar nada (mientras el VerticalLayoutGroup
        // todavía los fuerza a la anchura completa del panel, que es justo lo que se quiere encoger)
        // — así el nuevo tamaño es una fracción de lo que el usuario ve ahora mismo, no un valor en
        // píxeles inventado que podría no encajar con la resolución/Canvas Scaler del proyecto.
        var originalSizes = new System.Collections.Generic.Dictionary<Button, Vector2>();
        foreach (var b in buttons)
        {
            var btnRt = (RectTransform)b.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(btnRt);
            originalSizes[b] = new Vector2(btnRt.rect.width, btnRt.rect.height);
        }

        int styled = 0;
        foreach (var b in buttons)
        {
            if (b.transform.Find("RowGlassBG") != null) continue; // ya estilizada (re-ejecución)

            var bg = new GameObject("RowGlassBG", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(b.transform, false);
            bg.transform.SetAsFirstSibling(); // detrás del texto/otros hijos del botón

            var rt = (RectTransform)bg.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            // Sangrado moderado: la primera versión (-28/-10 · 28/10) hacía que los fondos de
            // cristal de filas contiguas casi se tocaran/solapasen si la fila ya tenía poco
            // espaciado, dando sensación de menú apretado.
            rt.offsetMin = new Vector2(-22f, -6f);
            rt.offsetMax = new Vector2(22f, 6f);

            var img = bg.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false; // el hit-test del click lo sigue haciendo el propio Button

            styled++;
        }

        // Más aire entre filas y botones más pequeños/estrechos (pedido del usuario tras ver el
        // primer resultado): con childControlWidth/Height activos (default habitual en un menú de
        // botones apilados) el VerticalLayoutGroup fuerza a cada botón a ocupar el 100% del ancho
        // del panel — hay que desactivarlo para que el LayoutElement de abajo pueda encoger cada
        // botón de verdad, y centrarlos para que no se peguen a la izquierda al estrecharse.
        var layoutGroup = buttonPanel.GetComponent<VerticalLayoutGroup>()
                        ?? buttonPanel.GetComponentInChildren<VerticalLayoutGroup>(true);
        if (layoutGroup != null)
        {
            var soLayout = new SerializedObject(layoutGroup);
            soLayout.FindProperty("m_Spacing").floatValue = 18f;
            soLayout.FindProperty("m_ChildControlWidth").boolValue = false;
            soLayout.FindProperty("m_ChildControlHeight").boolValue = false;
            soLayout.FindProperty("m_ChildForceExpandWidth").boolValue = false;
            soLayout.FindProperty("m_ChildAlignment").enumValueIndex = (int)TextAnchor.UpperCenter;
            soLayout.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogWarning("[MainMenuStylingBuilder] No se encontró VerticalLayoutGroup en 'ButtonPanel' — no se ha podido separar/encoger las filas automáticamente.");
        }

        int shrunk = 0;
        foreach (var b in buttons)
        {
            var existingLE = b.GetComponent<LayoutElement>();
            if (existingLE != null && existingLE.preferredWidth > 0f)
                continue; // ya se encogió en una ejecución anterior (o se ajustó a mano) — no lo tocamos de nuevo

            if (!originalSizes.TryGetValue(b, out var size) || size.x <= 0f || size.y <= 0f)
                continue; // el layout aún no estaba resuelto al medir: mejor no adivinar un tamaño

            var le = existingLE != null ? existingLE : b.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size.x * 0.72f;
            le.preferredHeight = size.y * 0.82f;
            le.minWidth = le.preferredWidth;
            le.minHeight = le.preferredHeight;
            shrunk++;
        }

        Debug.Log($"[MainMenuStylingBuilder] Fondo de cristal añadido a {styled} fila(s), {shrunk} botón(es) " +
                  "encogido(s) y espaciado entre filas ampliado.");
    }

    // ── Cámara ───────────────────────────────────────────────────────────

    static Camera AddCameraDrift()
    {
        var cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
        if (cam == null)
            throw new Exception("No se encontró ninguna Camera en MainMenu.unity.");

        var drift = cam.GetComponent<MainMenuWorldCameraDrift>();
        if (drift == null)
        {
            cam.gameObject.AddComponent<MainMenuWorldCameraDrift>();
            Debug.Log($"[MainMenuStylingBuilder] Añadido MainMenuWorldCameraDrift a '{cam.name}'.");
        }
        else
        {
            Debug.Log($"[MainMenuStylingBuilder] '{cam.name}' ya tenía MainMenuWorldCameraDrift — se reutiliza sin tocarlo.");
        }

        return cam;
    }

    // ── Nubes ────────────────────────────────────────────────────────────

    static void AddClouds(Camera cam)
    {
        if (FindByNameIncludingInactive("Clouds_Menu") != null)
        {
            Debug.Log("[MainMenuStylingBuilder] Ya existe 'Clouds_Menu' — se omite (bórralo a mano en la Hierarchy si quieres regenerarlo).");
            return;
        }

        var container = new GameObject("Clouds_Menu");
        int spawned = 0;

        for (int i = 0; i < CloudPrefabPaths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CloudPrefabPaths[i]);
            if (prefab == null)
            {
                Debug.LogWarning($"[MainMenuStylingBuilder] No se encontró {CloudPrefabPaths[i]} — se omite esta nube.");
                continue;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container.transform);

            // Posición relativa a la cámara (no coordenadas absolutas): delante, a distinta
            // profundidad/altura/lateral cada una para que no queden alineadas en el mismo plano.
            float lateral = (i - (CloudPrefabPaths.Length - 1) / 2f) * 14f;
            instance.transform.position = cam.transform.position
                                         + cam.transform.forward * (18f + i * 6f)
                                         + cam.transform.right * lateral
                                         + cam.transform.up * (6f + i * 2f);

            var drift = instance.AddComponent<CloudDrift>();
            var so = new SerializedObject(drift);
            so.FindProperty("direction").vector3Value = cam.transform.right;
            so.FindProperty("speed").floatValue = 0.4f + i * 0.15f;
            so.FindProperty("wrapDistance").floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo();

            spawned++;
        }

        Debug.Log($"[MainMenuStylingBuilder] {spawned} nube(s) colocadas bajo 'Clouds_Menu', posición relativa a la cámara.");
    }

    // ── Personajes voladores ─────────────────────────────────────────────

    static void AddFlyingCompanions(Camera cam)
    {
        if (FindByNameIncludingInactive("FlyingCompanions_Menu") != null)
        {
            Debug.Log("[MainMenuStylingBuilder] Ya existe 'FlyingCompanions_Menu' — se omite (bórralo a mano en la Hierarchy si quieres regenerarlo).");
            return;
        }

        var container = new GameObject("FlyingCompanions_Menu");
        int placed = 0;

        foreach (var (path, xOffset, zOffset, scale) in Companions)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[MainMenuStylingBuilder] No se encontró {path} — se omite este personaje.");
                continue;
            }

            var instance = InstantiatePrefabVisualOnly(prefab, container.transform);
            instance.name = Path.GetFileNameWithoutExtension(path).TrimStart('_');
            instance.transform.localScale = Vector3.one * scale;

            var companion = instance.AddComponent<MainMenuFlyingCompanion>();
            var so = new SerializedObject(companion);
            so.FindProperty("menuCamera").objectReferenceValue = cam.transform;
            so.FindProperty("cameraLocalOffset").vector3Value = new Vector3(xOffset, -0.8f, zOffset);
            so.FindProperty("animationTimeOffset").floatValue = placed * 0.7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            placed++;
        }

        Debug.Log($"[MainMenuStylingBuilder] {placed} personaje(s) colocados bajo 'FlyingCompanions_Menu', anclados a '{cam.name}'. " +
                  "Si alguno mira 'para atrás' respecto a la cámara, gira su Transform 180° en Y a mano — el ángulo de partida del " +
                  "personaje en su propio prefab es lo único que este script no puede adivinar sin verlo.");
    }

    /// <summary>
    /// Instancia un héroe (_LIAM/_ESTELA/_WILL) dejándolo en modo "solo visual" SIN que sus scripts
    /// de gameplay lleguen a ejecutarse ni una sola vez.
    ///
    /// Por qué no basta con desactivar después de instanciar: estos prefabs son los personajes
    /// JUGABLES de verdad (Invector + sistemas propios del proyecto — PlayerInputManager,
    /// PlayerHealthSystem, PlayerPresetService, WardrobeInventory...), pensados para MainWorld. En
    /// Unity, <c>Awake()</c> y <c>OnEnable()</c> de TODOS los componentes de un prefab se ejecutan
    /// de forma síncrona en el mismo instante en que lo instancias, si el objeto resultante queda
    /// activo — es decir, ANTES de que cualquier línea de código posterior pueda desactivar nada.
    /// Si esos scripts se registran como singleton/servicio (ServiceLocator, un campo estático
    /// "Instance"...) en su Awake/OnEnable, para cuando los desactivas el daño ya está hecho: han
    /// pisado o interferido con los sistemas reales de la partida (esto es justo lo que causaba los
    /// errores de consola sobre PlayerHealthSystem/PlayerPresetService/WardrobeInventory/
    /// PlayerInputManager al probar el menú).
    ///
    /// La solución es instanciar primero como hijo de un contenedor temporal DESACTIVADO: mientras
    /// el padre esté inactivo, Unity no ejecuta Awake/OnEnable de ningún hijo, sea cual sea su
    /// propio estado. Con la instancia todavía "congelada" así, se DESTRUYEN (no solo se
    /// desactivan) todos sus MonoBehaviour, Rigidbody y Collider — así no queda ningún script vivo
    /// capaz de registrarse como sistema global cuando, ya limpio, se reparenta al contenedor real
    /// del menú y se activa. El Animator no es un MonoBehaviour (es un Behaviour interno de Unity),
    /// así que sobrevive intacto — con él, la pose de vuelo de MainMenuFlyingCompanion.
    /// </summary>
    static GameObject InstantiatePrefabVisualOnly(GameObject prefab, Transform finalParent)
    {
        var tempHolder = new GameObject("___TempInactiveHolder");
        tempHolder.SetActive(false); // clave: debe desactivarse ANTES de instanciar el hijo

        try
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, tempHolder.transform);
            StripToVisualOnly(instance);
            instance.transform.SetParent(finalParent, false);
            return instance;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tempHolder);
        }
    }

    static void StripToVisualOnly(GameObject go)
    {
        int destroyedScripts = 0, destroyedPhysics = 0;

        // Varios scripts del rig se declaran dependientes entre sí vía [RequireComponent] (ej.:
        // NPCBehaviourManagerV2 requiere NPCSimpleAnimator, PlayerPickupCollector requiere
        // Inventory). Unity se niega a destruir un componente mientras algo en el mismo GameObject
        // dependa de él así — y un simple "prueba y reintenta" resultó insuficiente (dejaba algunos
        // vivos y activos, disparando su gameplay/IA encima del personaje del menú). En vez de
        // adivinar el orden a base de reintentos, se lee por reflexión el atributo [RequireComponent]
        // de cada script presente y solo se destruyen, en cada pasada, los que YA NO son requeridos
        // por ningún otro script todavía vivo — así el orden correcto (primero el que depende, luego
        // el del que depende) sale solo, sin que Unity tenga que rechazar ni un solo intento.
        var behaviours = new List<MonoBehaviour>(go.GetComponentsInChildren<MonoBehaviour>(true));
        behaviours.RemoveAll(mb => mb == null); // referencias de script roto/perdido

        for (int pass = 0; pass < 8 && behaviours.Count > 0; pass++)
        {
            var stillRequired = new HashSet<Type>();
            foreach (var mb in behaviours)
                foreach (var attr in mb.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    var rc = (RequireComponent)attr;
                    if (rc.m_Type0 != null) stillRequired.Add(rc.m_Type0);
                    if (rc.m_Type1 != null) stillRequired.Add(rc.m_Type1);
                    if (rc.m_Type2 != null) stillRequired.Add(rc.m_Type2);
                }

            int removedThisPass = 0;
            for (int i = behaviours.Count - 1; i >= 0; i--)
            {
                var mb = behaviours[i];
                bool isStillNeeded = false;
                foreach (var t in stillRequired)
                {
                    if (t.IsInstanceOfType(mb)) { isStillNeeded = true; break; }
                }
                if (isStillNeeded) continue; // otro script vivo depende de este todavía: espera a la próxima pasada

                UnityEngine.Object.DestroyImmediate(mb, true);
                behaviours.RemoveAt(i);
                destroyedScripts++;
                removedThisPass++;
            }

            if (removedThisPass == 0) break; // nada más se puede quitar sin romper una dependencia declarada
        }

        if (behaviours.Count > 0)
        {
            // Esto solo puede pasar si dos (o más) scripts se requieren mutuamente entre sí — una
            // dependencia circular real que ni siquiera se podría deshacer a mano desde el Inspector
            // de Unity (el propio motor lo bloquea). Como red de seguridad se desactivan (no pueden
            // destruirse), lo que evita Update/OnEnable, aunque su Awake ya se disparará al activar
            // el personaje si su Awake hace algo delicado (registrar un singleton, etc.) — si ves
            // algo raro proveniente de alguno de estos, la solución real es quitar el
            // [RequireComponent] circular en el propio script.
            var names = new List<string>();
            foreach (var mb in behaviours)
            {
                mb.enabled = false;
                names.Add(mb.GetType().Name);
            }
            Debug.LogWarning($"[MainMenuStylingBuilder] '{go.name}': {behaviours.Count} script(s) con una " +
                              $"dependencia [RequireComponent] circular entre sí no se pudieron eliminar " +
                              $"({string.Join(", ", names)}) — se han desactivado como red de seguridad, pero " +
                              "revísalo si notas algo raro procedente de ellos.");
        }

        foreach (var col in go.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(col, true);
            destroyedPhysics++;
        }

        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            UnityEngine.Object.DestroyImmediate(rb, true);
            destroyedPhysics++;
        }

        // Camera y AudioListener no son MonoBehaviour (son Behaviour/Component internos de Unity),
        // así que el barrido de arriba no los toca — pero los rigs de Invector como el de los
        // héroes suelen traer su propia cámara en tercera persona (ej.: "vThirdPersonCamera",
        // etiquetada MainCamera) colgando del prefab. Si esa cámara se queda activa dentro del
        // menú, compite por renderizar la pantalla con la cámara real del backdrop y puede acabar
        // tapándola por completo (esto causó la pantalla lisa amarilla: era el fondo/skybox de la
        // cámara huérfana de _WILL, sin encuadre válido, dibujándose encima de todo).
        int destroyedAvOutputs = 0;
        foreach (var cam in go.GetComponentsInChildren<Camera>(true))
        {
            UnityEngine.Object.DestroyImmediate(cam, true);
            destroyedAvOutputs++;
        }
        foreach (var listener in go.GetComponentsInChildren<AudioListener>(true))
        {
            UnityEngine.Object.DestroyImmediate(listener, true);
            destroyedAvOutputs++;
        }

        Debug.Log($"[MainMenuStylingBuilder] '{go.name}': {destroyedScripts} script(s), {destroyedPhysics} " +
                  $"componente(s) de física y {destroyedAvOutputs} cámara(s)/listener(s) eliminados antes de " +
                  "activarlo (solo quedan Animator/Renderers).");
    }

    // ── Utilidades ───────────────────────────────────────────────────────

    /// <summary>Busca un GameObject por nombre exacto en TODA la escena cargada, incluidos los inactivos
    /// (GameObject.Find de Unity ignora los inactivos, por eso no se usa aquí directamente).</summary>
    static GameObject FindByNameIncludingInactive(string name)
    {
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        foreach (var t in all)
            if (t.name == name)
                return t.gameObject;
        return null;
    }
}
