using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de Editor que crea (o repara) una escena de "estudio de grabación" reutilizable para
/// la serie de vídeos promo "en personaje" (Will, Liam y Estela dirigiéndose a cámara para
/// redes/itch.io), y monta encima el rig de cinemática del vídeo #1 usando el sistema propio del
/// proyecto (CinematicSequencerBase / CinematicCameraDriver / CinematicShot), el mismo que ya usan
/// todas las cinemáticas del juego.
///
/// Deja montado:
///   · Fondo neutro de estudio (suelo + panel), iluminación key + relleno y cámara principal fija.
///   · Los 3 protagonistas en su blocking, instanciados desde sus prefabs jugables reales pero
///     saneados a "solo visual" (ver StripToVisualOnly más abajo).
///   · Un CinematicCameraDriver, los 4 CinematicShot del guion, las marcas del plano de grupo y el
///     GameObject con el componente PromoVideo01Sequencer, con TODAS sus referencias de Inspector
///     que se pueden enlazar por código ya enlazadas.
///   · Un PromoStudioUISuppressor sobre el mismo contenedor, que oculta el botón global de "mantén
///     pulsado para saltar" (persistente en Start.unity) mientras dura esta escena — no tiene
///     sentido en un plató de grabación y arruinaría cualquier vídeo si aparece en pantalla.
///
/// Lo único que queda a mano en el Inspector tras ejecutar esto: duraciones por línea (ajuste fino
/// de ritmo) y el panel de CTA ("DEMO GRATIS · itch.io"). El encuadre de los 4 CinematicShot YA NO
/// se pone a ojo: se CALCULA midiendo la geometría real de cada personaje ya instanciado (bounds de
/// sus renderers + huesos de su avatar Humanoid) y despejando la distancia de cámara con la fórmula
/// de perspectiva — ver la sección "Medición real del personaje y cálculo de encuadre" más abajo.
/// Las claves de animación ya vienen con un valor por defecto real (estados del Base
/// Layer del controller Invector@BasicLocomotion, que comparten los 3 prefabs) — solo hay que
/// revisarlas a ojo, no rellenarlas. Ver el log final.
///
/// Blocking del guion (vídeo #1, 3 partes — ver comentarios junto a cada posición más abajo):
///   Parte 1: Estela sola, centrada, de cara a cámara.
///   Parte 2: Liam YA está en su sitio, fuera del encuadre cerrado de Estela; no camina — lo
///            descubre la cámara al abrir al plano medio. Es el enfoque más simple y el que ya usa
///            LiamCrystalBallSequencer (solo diálogo + cortes de cámara, sin locomoción en vivo).
///   Parte 3: Will, al fondo/lateral, practica una pose con la espada de espaldas y fuera de
///            encuadre — lo descubre un whip-pan (MoveTo rápido con Ease.Linear) del sequencer.
///
/// Fondo neutro de "estudio" (suelo + panel de fondo, gris claro, sin decorado de mundo real),
/// iluminación de 2 luces direccionales (key + relleno) pensada para no dejar sombras duras sobre
/// los personajes, y una cámara fija encuadrando al grupo en plano medio/americano.
///
/// Los 3 protagonistas se instancian desde sus prefabs JUGABLES reales (Assets/Prefabs/_ESTELA.prefab,
/// _LIAM.prefab, _WILL.prefab) pero en modo "solo visual": la técnica (instanciar como hijo de un
/// contenedor temporal desactivado y luego destruir todo MonoBehaviour/Collider/Rigidbody/Camera/
/// AudioListener antes de activarlos — salvo los tipos de la allowlist PreservedBehaviourTypes, hoy
/// solo NPCEmotionController, que es el que cambia las caras y el sequencer necesita vivo) está
/// adaptada de InstantiatePrefabVisualOnly/StripToVisualOnly
/// en MainMenuStylingBuilder.cs — MISMOS prefabs, mismo problema: son personajes Invector + sistemas
/// propios del proyecto (PlayerInputManager, PlayerHealthSystem, PlayerPresetService,
/// WardrobeInventory...) que se registran como singleton/servicio en su propio Awake/OnEnable. Como
/// AutoBootstrapOnPlay.cs carga Start.unity (con ServiceLocator, PlayerService, etc.) de forma
/// aditiva incluso al dar Play directamente sobre ESTA escena de estudio, sin este saneado los 3
/// prefabs competirían con los sistemas reales de la partida en cuanto se instancian. Se duplica la
/// lógica en vez de reutilizar el método privado de MainMenuStylingBuilder.cs para no tener que tocar
/// ese archivo existente (este encargo es solo aditivo, archivos nuevos).
///
/// Reparador, no solo creador: si la escena/los GameObjects ya existen, no los duplica — reaplica
/// posición/configuración. Con los 3 personajes hay un matiz importante: por defecto solo se
/// reposicionan (no se regeneran), PERO antes de darlos por buenos se comprueba que conserven TODOS
/// los componentes de la allowlist PreservedBehaviourTypes; si a alguno le falta alguno de esos
/// componentes es que viene de una ejecución antigua del menú, anterior a que ese componente entrase
/// en la allowlist, así que se destruye y se vuelve a instanciar limpio desde el prefab (ver
/// PlaceCharacter/ComponentesPreservadosQueFaltan). Ya no hace falta borrarlos a mano en la Hierarchy.
/// Lo mismo con el rig de cinemática: los planos y las referencias del sequencer se reaplican, pero
/// los valores que Raúl toca a mano (duraciones, claves de animación, panel de CTA, y las señales
/// de entrada/salida si las ha cambiado) NO se pisan al re-ejecutar.
///
/// Uso: menú "El Sendero → Marketing → Crear Escena de Estudio (Vídeos Promo)".
/// </summary>
public static class PromoStudioSceneBuilder
{
    const string SceneFolder = "Assets/Scenes/Marketing";
    const string ScenePath = SceneFolder + "/PromoEstudio.unity";

    const string EstelaPrefabPath = "Assets/Prefabs/_ESTELA.prefab";
    const string LiamPrefabPath = "Assets/Prefabs/_LIAM.prefab";
    const string WillPrefabPath = "Assets/Prefabs/_WILL.prefab";

    const string BackdropContainerName = "Fondo_Estudio";
    const string LightingContainerName = "Iluminacion_Estudio";
    const string CameraGoName = "Camara_Estudio";
    const string CharactersContainerName = "Personajes_Estudio";

    // ── Rig de cinemática (vídeo #1) ───────────────────────────────────────────────────────────
    const string CinematicsContainerName = "Cinematica_Estudio";
    const string CameraDriverGoName = "CinematicCameraDriver";
    const string ShotsContainerName = "Planos_Camara";
    const string GroupMarksContainerName = "Marcas_Plano_Grupo";
    const string SequencerGoName = "PromoVideo01_Sequencer";

    const string ShotEstelaSoloName = "Plano_01_Estela_Solo";
    const string ShotEstelaLiamName = "Plano_02_Medio_Estela_Liam";
    const string ShotWillRevealName = "Plano_03_Revelacion_Will";
    const string ShotGroupName = "Plano_04_Grupo_Final";

    const string MarkEstelaName = "Marca_Estela";
    const string MarkLiamName = "Marca_Liam";
    const string MarkWillName = "Marca_Will";

    // Claves genéricas del bus DefaultNarrativeSignals. NO son del grafo narrativo real ni de
    // ninguna quest: nada más en el proyecto las escucha, solo este sequencer.
    const string PromoSignalIn = "PROMO_VIDEO_01_START";
    const string PromoSignalOut = "PROMO_VIDEO_01_END";

    // Gris claro neutro (no blanco puro, para no quemar highlights/exposición al grabar).
    static readonly Color StudioNeutralColor = new Color(0.82f, 0.82f, 0.82f);

    // ── Cámara: fija, plano medio/americano, centrada en el grupo ──────────────────────────────
    static readonly Vector3 CameraPosition = new Vector3(0f, 1.55f, -3.6f);
    // Altura pecho, ligeramente por delante de Estela hacia el resto del grupo — punto de partida
    // razonable para encuadrar de pie a los 3 personajes; el encuadre fino de cada parte del guion
    // (planos más cerrados, giros, etc.) lo hace Raúl a mano por encima de este punto de partida.
    static readonly Vector3 CameraLookAtPoint = new Vector3(0f, 1.15f, 0.5f);
    const float CameraFieldOfView = 34f;

    // ── Blocking de personajes (posiciones/orientaciones de partida del guion, ver cabecera) ───
    // Estela: centrada, de cara a cámara (Parte 1).
    static readonly Vector3 EstelaPosition = Vector3.zero;
    // Liam: fuera del encuadre CERRADO de Estela (Parte 1) pero dentro del plano medio que se abre
    // en la Parte 2. No camina: es la cámara la que lo descubre al abrir el plano.
    static readonly Vector3 LiamStartPosition = new Vector3(-2.7f, 0f, -0.3f);
    // Will: al fondo y al otro lateral, apartado del resto y FUERA del plano medio de Estela+Liam,
    // para que su aparición sea de verdad una revelación cuando el whip-pan del sequencer lo
    // descubre (Parte 3).
    //
    // AJUSTE (esta pasada): estaba en (1.8, 0, 1.6), lo bastante cerca del grupo como para colarse
    // por el borde derecho del plano medio Estela+Liam y destripar el gag antes de tiempo. Se
    // aparta a (3.2, 0, 2.0), comprobado contra el encuadre de los 4 CinematicShot definidos más
    // abajo: queda fuera del Plano 01 y del Plano 02, y sigue delante del panel de fondo (z=3.2) y
    // dentro del suelo.
    static readonly Vector3 WillPosition = new Vector3(3.2f, 0f, 2.0f);

    // ── Planos de cámara del vídeo #1 ──────────────────────────────────────────────────────────
    //
    // ⚠ IMPORTANTE (cambio de esta pasada): las constantes Pos/LookAt de los 4 planos YA NO SE USAN
    // COMO POSICIÓN FINAL. Dos rondas de ajuste "a ojo" con posiciones/FOV fijos salieron mal (el
    // plano 01 acabó tan cerca que solo se veía la parte de arriba de la cabeza de Estela, con las
    // coletas cortadas por los bordes) porque asumían proporciones de humano adulto (~1.7 m, cabeza
    // ~1.5 m) y estos personajes son un modelo estilizado tipo chibi (RPG Tiny Hero Duo): cabeza
    // enorme, cuerpo pequeño. Cualquier constante en metros calculada a ojo contra ese supuesto está
    // rota de partida.
    //
    // Ahora el encuadre se CALCULA en tiempo de ejecución de la herramienta a partir de la geometría
    // REAL del personaje ya instanciado y colocado en su marca (ver MedirPersonaje/CalcularEncuadre
    // más abajo): se miden bounds de renderers + huesos del avatar Humanoid, y se despeja la
    // distancia de cámara con la fórmula de cámara en perspectiva.
    //
    // De estas constantes se sigue usando:
    //   · La DIRECCIÓN cámara→sujeto en el plano XZ (el ángulo artístico de cada plano: frontal,
    //     3/4, lateral...), que se conserva tal cual — solo se recalcula la DISTANCIA y la ALTURA.
    //   · El FOV vertical (todos entre 30° y 40°, ver más abajo).
    //   · El valor completo como FALLBACK si la medición falla (personaje ausente, sin renderers...).
    // El ajuste fino final se sigue pudiendo hacer a ojo en la Scene View moviendo estos GameObjects.

    // Parte 1 — plano medio de Estela, ligeramente descentrado para que no quede plano.
    //
    // AJUSTE (esta pasada): estaba en (0.55, 1.45, -2.15) con FOV 30°, es decir, apenas 2.2 m de
    // distancia y un objetivo bastante telefoto — un plano medio corto, casi primer plano. Raúl lo
    // vio DEMASIADO CERCA para el plano de presentación del vídeo, así que se abre:
    //   · La cámara retrocede a z = -3.35 (≈3.45 m reales hasta el punto de mira, +55% de distancia).
    //   · El FOV sube de 30° a 35°: plano medio más abierto, sin llegar a gran angular (y en línea
    //     con los otros 3 planos de esta escena, que ya van a 32-36°, ver más abajo).
    //   · La X sube de 0.55 a 0.85 para CONSERVAR el mismo ángulo de 3/4 (~14° fuera del eje) que
    //     tenía a la distancia anterior: dejándola en 0.55, al alejarse el plano se habría vuelto
    //     casi frontal y habría perdido justo lo que buscaba ese descentrado.
    //   · La altura de mira baja de 1.35 a 1.25 (pecho) para recentrar el encuadre vertical con la
    //     nueva distancia, y la cámara sube a 1.50 para quedarse casi a la altura de los ojos.
    // Sigue siendo un punto de partida: el ajuste fino de encuadre se hace a ojo en la Scene View.
    static readonly Vector3 ShotEstelaSoloPos = new Vector3(0.85f, 1.5f, -3.35f);
    static readonly Vector3 ShotEstelaSoloLookAt = new Vector3(0f, 1.25f, 0f);
    const float ShotEstelaSoloFov = 35f;

    // Parte 2 — plano medio con Estela y Liam los dos en cuadro (y Will fuera).
    static readonly Vector3 ShotEstelaLiamPos = new Vector3(-1.4f, 1.5f, -4.9f);
    static readonly Vector3 ShotEstelaLiamLookAt = new Vector3(-1.35f, 1.15f, -0.15f);
    const float ShotEstelaLiamFov = 36f;

    // Parte 2 — destino del whip-pan: Will de cuerpo entero, con el resto del grupo fuera de cuadro.
    static readonly Vector3 ShotWillRevealPos = new Vector3(1.0f, 1.45f, -1.6f);
    static readonly Vector3 ShotWillRevealLookAt = new Vector3(3.2f, 1.2f, 2.0f);
    const float ShotWillRevealFov = 32f;

    // Parte 3 — plano final de grupo, frontal y centrado sobre las marcas de grupo de más abajo.
    static readonly Vector3 ShotGroupPos = new Vector3(0f, 1.55f, -4.2f);
    static readonly Vector3 ShotGroupLookAt = new Vector3(0f, 1.2f, 0.05f);
    const float ShotGroupFov = 36f;

    // ── Parámetros del cálculo automático de encuadre ──────────────────────────────────────────
    // Todo lo de aquí es adimensional (fracciones de la altura REAL medida del personaje), nunca
    // metros: es justo lo que hace que funcione igual con un chibi que con un humano adulto.

    /// Relación de aspecto de referencia para el encaje HORIZONTAL del encuadre (ancho/alto).
    /// No se puede leer la del Game View desde aquí de forma fiable, así que se asume 16:9, que es
    /// el formato en el que se van a grabar los vídeos. Si algún día se graba en vertical (9:16 para
    /// Shorts/TikTok), cambiar esto a 9f/16f: con un aspecto tan estrecho el ancho pasa a ser el
    /// factor que manda y la cámara tendría que retroceder bastante más.
    const float AspectoObjetivo = 16f / 9f;

    /// Margen de seguridad vertical extra (10%) sobre la altura ya calculada, para absorber
    /// diferencias de aspecto, overscan y el hecho de que la pose de idle no es exactamente la pose
    /// de bind con la que se miden los bounds del SkinnedMeshRenderer.
    const float MargenExtraVertical = 0.10f;

    // Composición de cada plano, en "alturas de cabeza" (distancia línea de hombros → alto del pelo),
    // que es la unidad que de verdad escala bien entre proporciones distintas:
    //   · CabezasBajoHombros_X : cuánto entra en cuadro por debajo de la línea de hombros.
    //   · AireSobreCabeza_X    : aire por encima del pelo (headroom), para que la cabeza no quede
    //                            pegada al borde superior — el fallo exacto de la pasada anterior.
    //   · MargenLateral_X      : margen a cada lado, como fracción del ancho medido (coletas, brazos).
    // Con 0.90 abajo + 0.25 arriba la cara cae en torno al 67% de altura del cuadro, es decir, justo
    // en la línea del tercio superior: composición estándar de plano de presentación a cámara.
    const float CabezasBajoHombros_EstelaSolo = 0.90f;
    const float AireSobreCabeza_EstelaSolo = 0.25f;
    const float MargenLateral_EstelaSolo = 0.15f;

    const float CabezasBajoHombros_EstelaLiam = 1.15f;
    const float AireSobreCabeza_EstelaLiam = 0.30f;
    const float MargenLateral_EstelaLiam = 0.15f;

    // Will se revela de cuerpo entero: no se usa la línea de hombros, se encuadra de los pies (con un
    // poco de suelo) al pelo.
    const float AireSobreCabeza_WillReveal = 0.20f;
    const float MargenLateral_WillReveal = 0.15f;

    const float CabezasBajoHombros_Grupo = 1.80f;
    const float AireSobreCabeza_Grupo = 0.30f;
    const float MargenLateral_Grupo = 0.18f;

    // ── Marcas del plano de grupo (Parte 3) ────────────────────────────────────────────────────
    // El sequencer recoloca a los 3 sobre estas marcas EN EL MISMO FRAME en que corta al plano de
    // grupo, así que el salto de posición no se ve. Sin ellas, los personajes se quedarían tan
    // separados como en la Parte 2 y el plano final no sería un plano de grupo de verdad.
    static readonly Vector3 MarkEstelaPosition = new Vector3(0f, 0f, 0.1f);
    static readonly Vector3 MarkLiamPosition = new Vector3(-1.05f, 0f, 0.25f);
    static readonly Vector3 MarkWillPosition = new Vector3(1.05f, 0f, 0.25f);

    [MenuItem("El Sendero/Marketing/Crear Escena de Estudio (Vídeos Promo)")]
    public static void CreatePromoStudioScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[PromoStudioSceneBuilder] Sal de Play Mode antes de ejecutar esto.");
            return;
        }

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty)
            {
                Debug.LogError($"[PromoStudioSceneBuilder] La escena '{open.name}' tiene cambios sin guardar. Guarda (Ctrl+S) antes de ejecutar esto.");
                return;
            }
        }

        bool sceneAlreadyExisted = System.IO.File.Exists(ScenePath);
        Scene scene;

        if (sceneAlreadyExisted)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[PromoStudioSceneBuilder] No se pudo abrir {ScenePath}.");
                return;
            }
        }
        else
        {
            EnsureFolderExists(SceneFolder);
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        try
        {
            SetUpBackdrop();
            SetUpLighting();
            SetUpCamera();
            SetUpCharacters();
            SetUpCinematics(); // debe ir DESPUÉS de SetUpCharacters: enlaza los transforms de los 3

            EditorSceneManager.MarkSceneDirty(scene);
            if (sceneAlreadyExisted)
                EditorSceneManager.SaveScene(scene);
            else
                EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PromoStudioSceneBuilder] ✅ Escena de estudio {(sceneAlreadyExisted ? "reparada" : "creada")} y guardada en {ScenePath}, " +
                      $"con el rig de cinemática del vídeo #1 montado sobre el sistema propio del proyecto.\n" +
                      $"CÓMO PROBARLO: dar Play en esta escena y pulsar F6 (atajo ya asignado en _simulateHotkey) — " +
                      $"o, si prefieres el menú, seleccionar '{SequencerGoName}' en la Hierarchy → click derecho sobre " +
                      "la cabecera del componente PromoVideo01Sequencer en el Inspector → 'Simular secuencia'.\n" +
                      "QUEDA A MANO EN EL INSPECTOR DE ESE COMPONENTE:\n" +
                      "  1) Duraciones por línea (ritmo cómico — imposible acertarlo sin verlo reproducido).\n" +
                      "  2) Claves de animación: YA VIENEN RELLENAS con estados reales del Base Layer de " +
                      "Invector@BasicLocomotion (el controller que comparten los 3 prefabs). Son NOMBRES DE ESTADO, " +
                      "no parámetros Trigger, porque los prefabs vienen saneados a 'solo visual' y no conservan " +
                      "NPCSimpleAnimator. Solo hay que revisarlas a ojo — el Base Layer tiene pocos gestos sociales y " +
                      "algunas son aproximaciones (ver los comentarios del propio PromoVideo01Sequencer).\n" +
                      "  3) El panel de CTA 'DEMO GRATIS · itch.io': construirlo aparte, dejarlo DESACTIVADO en la " +
                      "escena y asignarlo al campo _ctaPanel.\n" +
                      $"  4) Encuadre de los 4 planos: YA VIENE CALCULADO a partir de la geometría real medida de los " +
                      "personajes (busca en esta misma consola las líneas '📏 Medida real de...' y '🎥 ... CALCULADO' — " +
                      "ahí están todos los números: altura del personaje, línea de hombros, altura de cabeza, ventana de " +
                      "encuadre y distancia despejada). Si aun así algo no cuadra, mira PRIMERO esos números antes de " +
                      $"tocar nada, y luego afina moviendo los 4 GameObjects bajo '{CinematicsContainerName}/{ShotsContainerName}' " +
                      "en la Scene View (llevan una Camera desactivada, así que se ven con el preview de Unity al seleccionarlos).\n" +
                      "     ⚠ PARA JUZGAR EL ENCUADRE, mira el PREVIEW del plano (selecciona el GameObject del plano en la " +
                      "Hierarchy y usa el panel 'Camera Preview' de la Scene View) o pulsa F6 para lanzar la secuencia. NO vale dar " +
                      "Play a secas y mirar el Game View: esta escena no está en la lista de exclusión de AutoBootstrapOnPlay, " +
                      "así que al entrar en Play se carga Start.unity de forma aditiva y su cámara de gameplay también va " +
                      "etiquetada como MainCamera — hasta que el sequencer no corta al primer plano, lo que se ve en el Game " +
                      "View puede no ser ninguno de estos 4 planos.\n" +
                      "  5) (Opcional) TransitionSettings de entrada/salida y AudioGraphProfile + id de música, si se " +
                      "quiere fundido y banda sonora.\n" +
                      "  6) El cierre ('Fundido a negro. Logo del juego + enlace de itch.io.') YA NO hace falta " +
                      "montarlo a mano: PromoVideo01Sequencer construye su propia tarjeta de cierre en runtime y " +
                      "_logoSprite se autoasigna desde 'Assets/Art/UI/Menu/logo sendero 4.png'. Solo queda revisar a " +
                      "ojo el texto (_logoLinkTexto) y los tres tiempos (_logoFadeANegroDuracion/_logoFadeInDuracion/" +
                      "_logoHoldDuracion) en el Inspector.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PromoStudioSceneBuilder] Error durante la construcción (la escena puede haber quedado a medio guardar): {e}");
        }
    }

    // ── Fondo neutro de estudio (suelo + panel de fondo) ────────────────────────────────────────

    static void SetUpBackdrop()
    {
        var container = FindByNameIncludingInactive(BackdropContainerName);
        bool created = container == null;
        if (container == null)
            container = new GameObject(BackdropContainerName);

        // Plane por defecto de Unity = 10x10 unidades a escala 1.
        var floor = GetOrCreatePrimitiveChild(container.transform, "Suelo", PrimitiveType.Plane);
        floor.transform.localPosition = new Vector3(0f, 0f, 1f);
        floor.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
        ApplyStudioMaterial(floor);

        // Quad con normal por defecto hacia -Z: con rotación identidad, mirando hacia la cámara
        // (colocada en Z negativo), sin necesidad de rotarlo a mano.
        var backPanel = GetOrCreatePrimitiveChild(container.transform, "PanelFondo", PrimitiveType.Quad);
        backPanel.transform.localPosition = new Vector3(0f, 3f, 3.2f);
        backPanel.transform.localScale = new Vector3(14f, 8f, 1f);
        ApplyStudioMaterial(backPanel);

        Debug.Log($"[PromoStudioSceneBuilder] Fondo de estudio (suelo + panel) {(created ? "creado" : "reparado")} bajo '{BackdropContainerName}'.");
    }

    static GameObject GetOrCreatePrimitiveChild(Transform parent, string name, PrimitiveType type)
    {
        var existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);

        // Puramente decorado visual — sin física, no necesita Collider (evita interferencias con
        // cualquier física/raycast si esta escena llega a compartirse con otras en el futuro).
        var col = go.GetComponent<Collider>();
        if (col != null)
            UnityEngine.Object.DestroyImmediate(col);

        return go;
    }

    static void ApplyStudioMaterial(GameObject go)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[PromoStudioSceneBuilder] No se encontró el shader 'Universal Render Pipeline/Lit' — " +
                              "¿está URP realmente activo en este proyecto? Se deja el material por defecto en " +
                              $"'{go.name}', revísalo a mano.");
            return;
        }

        var mat = renderer.sharedMaterial;
        if (mat == null || mat.shader != shader)
        {
            mat = new Material(shader) { name = $"{go.name}_StudioMat" };
            renderer.sharedMaterial = mat;
        }

        mat.color = StudioNeutralColor;
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.05f); // acabado mate, sin brillos duros de "papel de estudio real"
    }

    // ── Iluminación (key + relleno, sin sombras duras) ──────────────────────────────────────────

    static void SetUpLighting()
    {
        var container = FindByNameIncludingInactive(LightingContainerName);
        bool created = container == null;
        if (container == null)
            container = new GameObject(LightingContainerName);

        var key = GetOrCreateDirectionalLight(container.transform, "Luz_Key");
        key.transform.localRotation = Quaternion.Euler(45f, -35f, 0f);
        key.intensity = 1.15f;
        key.color = Color.white;
        key.shadows = LightShadows.Soft;
        key.shadowStrength = 0.55f; // suave, no negra — pedido: sin sombras duras feas sobre los personajes

        var fill = GetOrCreateDirectionalLight(container.transform, "Luz_Relleno");
        fill.transform.localRotation = Quaternion.Euler(35f, 150f, 0f);
        fill.intensity = 0.45f;
        fill.color = Color.white;
        fill.shadows = LightShadows.None; // solo rellena el lado oscuro, no añade una segunda sombra cruzada

        // Ambiente plano gris claro: evita que el lado sin luz directa de suelo/personajes caiga a negro puro.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);

        Debug.Log($"[PromoStudioSceneBuilder] Iluminación de estudio (key + relleno + ambiente plano) {(created ? "creada" : "reparada")} bajo '{LightingContainerName}'.");
    }

    static Light GetOrCreateDirectionalLight(Transform parent, string name)
    {
        var existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
        }

        var light = go.GetComponent<Light>();
        light.type = LightType.Directional;
        return light;
    }

    // ── Cámara fija (plano medio/americano, centrada en el grupo) ───────────────────────────────

    static void SetUpCamera()
    {
        var existing = FindByNameIncludingInactive(CameraGoName);
        bool created = existing == null;
        var go = existing != null ? existing : new GameObject(CameraGoName, typeof(Camera), typeof(AudioListener));

        var cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        if (go.GetComponent<AudioListener>() == null) go.AddComponent<AudioListener>();

        go.transform.position = CameraPosition;
        go.transform.rotation = Quaternion.LookRotation((CameraLookAtPoint - CameraPosition).normalized);
        go.tag = "MainCamera"; // misma convención que el resto de escenas del proyecto

        cam.fieldOfView = CameraFieldOfView;
        // Solid color de seguridad además del panel de fondo: si el panel no llega a cubrir del
        // todo el encuadre en algún ajuste posterior, no se cuela el skybox por defecto (azul cielo)
        // detrás de los personajes.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = StudioNeutralColor;

        Debug.Log($"[PromoStudioSceneBuilder] Cámara de estudio {(created ? "creada" : "reparada")} ('{CameraGoName}', FOV={CameraFieldOfView}°), encuadrando el centro del grupo. " +
                  "Es un punto de partida — el plano/zoom concreto de cada parte del guion se ajusta a mano encima de esto.");
    }

    // ── Personajes (Will, Liam, Estela) ──────────────────────────────────────────────────────────

    static void SetUpCharacters()
    {
        var container = FindByNameIncludingInactive(CharactersContainerName);
        if (container == null)
            container = new GameObject(CharactersContainerName);

        // Estela: centrada, de cara a cámara (Parte 1 del guion).
        PlaceCharacter(container.transform, EstelaPrefabPath, "Estela", EstelaPosition, CameraPosition);

        // Liam: ya en su marca, orientado hacia Estela. No camina — lo descubre la cámara al abrir
        // al plano medio (Parte 2). El sequencer lo gira a cámara cuando le toca hablar de frente.
        PlaceCharacter(container.transform, LiamPrefabPath, "Liam", LiamStartPosition, EstelaPosition);

        // Will: al fondo/lateral, apartado y DE ESPALDAS al plano de revelación, mirando hacia su
        // propia práctica con la espada — el whip-pan del sequencer lo pilla así (Parte 3).
        var willLookTarget = WillPosition + new Vector3(1.2f, 0f, 0.8f);
        PlaceCharacter(container.transform, WillPrefabPath, "Will", WillPosition, willLookTarget);

        Debug.Log($"[PromoStudioSceneBuilder] Los 3 protagonistas colocados/reposicionados bajo '{CharactersContainerName}' según el blocking del guion.");
    }

    static void PlaceCharacter(Transform parent, string prefabPath, string childName, Vector3 position, Vector3 lookAtPoint)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        var existingChild = parent.Find(childName);
        if (existingChild != null)
        {
            // AUTO-REPARACIÓN — ver ComponentesPreservadosQueFaltan() para el porqué completo.
            var faltantes = ComponentesPreservadosQueFaltan(existingChild, prefab);

            if (faltantes.Count == 0)
            {
                ApplyBlockingTransform(existingChild, position, lookAtPoint);
                Debug.Log($"[PromoStudioSceneBuilder] '{childName}' ya existía en la escena y conserva todos los " +
                          "componentes de la allowlist PreservedBehaviourTypes — se da por bueno y solo se reposiciona.");
                return;
            }

            if (prefab == null)
            {
                Debug.LogError($"[PromoStudioSceneBuilder] A '{childName}' le falta(n) {string.Join(", ", faltantes)} " +
                               $"(instancia obsoleta), pero NO se encuentra el prefab '{prefabPath}' para regenerarlo. " +
                               "Se deja como está y solo se reposiciona: revisa la ruta del prefab.");
                ApplyBlockingTransform(existingChild, position, lookAtPoint);
                return;
            }

            Debug.LogWarning($"[PromoStudioSceneBuilder] ♻ '{childName}' ya existía en la escena PERO le falta(n) " +
                             $"{string.Join(", ", faltantes)} de la allowlist PreservedBehaviourTypes. Es una instancia " +
                             "VIEJA, creada por una versión anterior de este script (de antes de que ese componente " +
                             "entrase en la allowlist del saneado). Reposicionarla no lo arregla nunca, así que se " +
                             "DESTRUYE y se vuelve a instanciar limpia desde el prefab, pasando otra vez por " +
                             "StripToVisualOnly() con la allowlist actual. Las referencias del sequencer a este " +
                             "personaje (Transform, Animator, NPCEmotionController) se reenlazan solas justo después, " +
                             "en SetUpCinematics()/WireSequencer().");
            UnityEngine.Object.DestroyImmediate(existingChild.gameObject);
        }

        if (prefab == null)
        {
            Debug.LogError($"[PromoStudioSceneBuilder] No se encontró el prefab en '{prefabPath}' — no se puede colocar a '{childName}'. Revisa la ruta.");
            return;
        }

        var instance = InstantiatePrefabVisualOnly(prefab, parent);
        instance.name = childName;
        ApplyBlockingTransform(instance.transform, position, lookAtPoint);
    }

    /// <summary>
    /// Devuelve los nombres de los tipos de PreservedBehaviourTypes que el PREFAB sí trae pero que a
    /// la instancia ya existente en la escena le FALTAN. Lista vacía = la instancia está al día y
    /// basta con reposicionarla.
    ///
    /// POR QUÉ EXISTE (bug real, 23 ago 2026 — "las caras no han cambiado ni una vez"): este builder
    /// es "reparador", y hasta ahora eso significaba que si el GameObject de un personaje YA estaba en
    /// la escena se le actualizaba solo posición/rotación y punto — no se volvía a pasar por
    /// InstantiatePrefabVisualOnly()/StripToVisualOnly(). Consecuencia: los 3 personajes de la escena
    /// de Raúl se habían instanciado en una ejecución ANTERIOR del menú, de cuando el saneado todavía
    /// no tenía allowlist y arrasaba también con NPCEmotionController. Al añadir después la allowlist,
    /// el código nuevo NUNCA llegaba a esos GameObjects viejos: re-ejecutar el menú los daba por
    /// buenos, así que seguían sin el componente de las caras, WireSequencer() enlazaba null en
    /// _estelaEmotion/_liamEmotion/_willEmotion y ninguna expresión cambiaba en el vídeo.
    ///
    /// Es la misma familia de bug que el de las 21 claves de animación (código nuevo que no alcanza a
    /// datos viejos ya serializados), pero allí bastaba con rellenar un campo y aquí falta un
    /// componente entero. Se resuelve destruyendo y reinstanciando desde el prefab en vez de intentar
    /// añadir el componente a mano: reinstanciar reproduce EXACTAMENTE lo que trae el prefab, incluida
    /// la peculiaridad de _WILL.prefab (lleva DOS NPCEmotionController en el mismo GameObject, uno con
    /// EmotionProfile y otro sin él), que sería muy fácil recrear mal a mano.
    ///
    /// GENÉRICO A PROPÓSITO: recorre PreservedBehaviourTypes, no NPCEmotionController en concreto. El
    /// día que se preserve un segundo script, las instancias viejas se autorrepararán igual sin tocar
    /// nada aquí.
    ///
    /// Se exige el componente solo si el PREFAB lo tiene: si el prefab tampoco lo lleva, no hay nada
    /// que reparar y regenerar al personaje sería condenarlo a reinstanciarse en CADA ejecución del
    /// menú, para siempre y sin arreglar nada. Ese caso ya lo avisa FindEmotionController().
    /// </summary>
    static System.Collections.Generic.List<string> ComponentesPreservadosQueFaltan(Transform instancia, GameObject prefab)
    {
        var faltantes = new System.Collections.Generic.List<string>();
        if (instancia == null) return faltantes;

        foreach (var t in PreservedBehaviourTypes)
        {
            if (t == null) continue;
            if (prefab != null && prefab.GetComponentInChildren(t, true) == null) continue;

            if (instancia.GetComponentInChildren(t, true) == null)
                faltantes.Add(t.Name);
        }

        return faltantes;
    }

    static void ApplyBlockingTransform(Transform t, Vector3 position, Vector3 lookAtPoint)
    {
        t.position = position;

        Vector3 dir = lookAtPoint - position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(dir.normalized);
    }

    // ── Rig de cinemática del vídeo #1 (sistema propio del proyecto) ────────────────────────────
    // Monta, bajo un único contenedor, todo lo que el sequencer necesita:
    //   Cinematica_Estudio
    //     ├─ CinematicCameraDriver      (mueve Camera.main entre planos; Cut/MoveTo)
    //     ├─ Planos_Camara              (4 GameObjects con CinematicShot: cada uno lleva una Camera
    //     │                              DESACTIVADA — es lo que exige CinematicShot y lo que da el
    //     │                              preview de encuadre en la Scene View)
    //     ├─ Marcas_Plano_Grupo         (3 GameObjects vacíos: blocking del plano final)
    //     └─ PromoVideo01_Sequencer     (el componente PromoVideo01Sequencer, con las referencias
    //                                    ya enlazadas por código vía SerializedObject)

    static void SetUpCinematics()
    {
        var container = FindByNameIncludingInactive(CinematicsContainerName);
        bool created = container == null;
        if (container == null)
            container = new GameObject(CinematicsContainerName);

        // Esta escena carga Start.unity de forma aditiva (ver AutoBootstrapOnPlay.cs), así que el
        // botón global de "mantén pulsado para saltar" (GlobalCinematicSkipController/HoldToSkipUI,
        // persistente en Start) se activaría solo con dar Play y disparar la secuencia — no tiene
        // ningún sentido en un plató de grabación y arruinaría cualquier vídeo si sale en pantalla.
        // Ver PromoStudioUISuppressor.
        if (container.GetComponent<PromoStudioUISuppressor>() == null)
            container.AddComponent<PromoStudioUISuppressor>();

        var driver = GetOrAddComponentOnChild<CinematicCameraDriver>(container.transform, CameraDriverGoName);

        // ⚠ ORDEN DE EJECUCIÓN — NO REORDENAR: las referencias a los 3 personajes se resuelven AQUÍ
        // ARRIBA, antes de crear los planos, porque el encuadre de los 4 CinematicShot se CALCULA a
        // partir de la geometría real medida sobre esas instancias. Eso exige dos cosas:
        //   1) Que SetUpCharacters() ya se haya ejecutado (lo hace: CreatePromoStudioScene lo llama
        //      justo antes de SetUpCinematics, y ya lo advertía el comentario de esa llamada).
        //   2) Que los personajes estén YA EN SU POSICIÓN DE BLOCKING FINAL cuando se mide — lo
        //      están, porque PlaceCharacter/ApplyBlockingTransform los coloca en el mismo paso en
        //      que los instancia. Si alguien mueve el blocking a después de esta llamada, los planos
        //      se calcularían contra el origen y volverían a salir mal.
        var charactersRoot = FindByNameIncludingInactive(CharactersContainerName);
        Transform estela = charactersRoot != null ? charactersRoot.transform.Find("Estela") : null;
        Transform liam = charactersRoot != null ? charactersRoot.transform.Find("Liam") : null;
        Transform will = charactersRoot != null ? charactersRoot.transform.Find("Will") : null;

        // Medición real (bounds + huesos del avatar) de cada personaje ya colocado.
        var medEstela = MedirPersonaje(estela, "Estela");
        var medLiam = MedirPersonaje(liam, "Liam");
        var medWill = MedirPersonaje(will, "Will");

        var shotsRoot = GetOrCreateChild(container.transform, ShotsContainerName);

        var shotEstelaSolo = CrearPlanoEncuadrado(shotsRoot, ShotEstelaSoloName, "Parte 1 — Estela sola",
            new[] { medEstela }, ShotEstelaSoloFov,
            false, CabezasBajoHombros_EstelaSolo, AireSobreCabeza_EstelaSolo, MargenLateral_EstelaSolo,
            ShotEstelaSoloPos, ShotEstelaSoloLookAt);

        var shotEstelaLiam = CrearPlanoEncuadrado(shotsRoot, ShotEstelaLiamName, "Parte 2 — plano medio Estela + Liam",
            new[] { medEstela, medLiam }, ShotEstelaLiamFov,
            false, CabezasBajoHombros_EstelaLiam, AireSobreCabeza_EstelaLiam, MargenLateral_EstelaLiam,
            ShotEstelaLiamPos, ShotEstelaLiamLookAt);

        var shotWillReveal = CrearPlanoEncuadrado(shotsRoot, ShotWillRevealName, "Parte 2 — revelación de Will (whip-pan)",
            new[] { medWill }, ShotWillRevealFov,
            true, 0f, AireSobreCabeza_WillReveal, MargenLateral_WillReveal,
            ShotWillRevealPos, ShotWillRevealLookAt);

        // Plano de grupo: los 3 NO están aquí donde se les va a ver — el sequencer los teletransporta
        // a las marcas de grupo en el mismo frame del corte. Así que se encuadra contra las medidas
        // DESPLAZADAS a esas marcas, no contra su posición de blocking actual. (El giro que también
        // aplican las marcas cambia un pelín los bounds; la diferencia es despreciable frente al
        // margen de seguridad.)
        var medEstelaGrupo = DesplazarMedidas(medEstela, MarkEstelaPosition - EstelaPosition);
        var medLiamGrupo = DesplazarMedidas(medLiam, MarkLiamPosition - LiamStartPosition);
        var medWillGrupo = DesplazarMedidas(medWill, MarkWillPosition - WillPosition);

        var shotGroup = CrearPlanoEncuadrado(shotsRoot, ShotGroupName, "Parte 3 — plano final de grupo",
            new[] { medEstelaGrupo, medLiamGrupo, medWillGrupo }, ShotGroupFov,
            false, CabezasBajoHombros_Grupo, AireSobreCabeza_Grupo, MargenLateral_Grupo,
            ShotGroupPos, ShotGroupLookAt);

        // Comprobación de "quién NO debe verse todavía": el guion depende de que Liam y Will no se
        // cuelen en el plano 01 y de que Will no destripe el gag en el plano 02. Antes esto se
        // comprobaba a ojo contra constantes; ahora se puede verificar de verdad, con el frustum
        // real de los planos ya calculados.
        AvisarSiEntraEnCuadro(shotEstelaSolo, ShotEstelaSoloFov, medLiam, "Liam", ShotEstelaSoloName);
        AvisarSiEntraEnCuadro(shotEstelaSolo, ShotEstelaSoloFov, medWill, "Will", ShotEstelaSoloName);
        AvisarSiEntraEnCuadro(shotEstelaLiam, ShotEstelaLiamFov, medWill, "Will", ShotEstelaLiamName);

        // Marcas del plano de grupo: los 3 mirando al punto donde estará la cámara del plano final.
        // Se usa la posición REAL ya calculada del plano de grupo (no la constante ShotGroupPos), que
        // es donde la cámara va a estar de verdad.
        var marksRoot = GetOrCreateChild(container.transform, GroupMarksContainerName);
        var groupCameraGround = shotGroup != null
            ? new Vector3(shotGroup.position.x, 0f, shotGroup.position.z)
            : new Vector3(ShotGroupPos.x, 0f, ShotGroupPos.z);
        var markEstela = CreateOrUpdateMark(marksRoot, MarkEstelaName, MarkEstelaPosition, groupCameraGround);
        var markLiam = CreateOrUpdateMark(marksRoot, MarkLiamName, MarkLiamPosition, groupCameraGround);
        var markWill = CreateOrUpdateMark(marksRoot, MarkWillName, MarkWillPosition, groupCameraGround);

        var sequencer = GetOrAddComponentOnChild<PromoVideo01Sequencer>(container.transform, SequencerGoName);

        WireSequencer(sequencer, driver, estela, liam, will,
            shotEstelaSolo, shotEstelaLiam, shotWillReveal, shotGroup,
            markEstela, markLiam, markWill);

        Debug.Log($"[PromoStudioSceneBuilder] Rig de cinemática {(created ? "creado" : "reparado")} bajo " +
                  $"'{CinematicsContainerName}' (CinematicCameraDriver + 4 CinematicShot + 3 marcas de grupo + " +
                  $"'{SequencerGoName}' con sus referencias enlazadas + PromoStudioUISuppressor para ocultar " +
                  "el botón global de skip en esta escena).");
    }

    /// Enlaza por código todo lo enlazable del sequencer. Se usa SerializedObject porque los campos
    /// son privados con [SerializeField] (incluidos _signalIn/_signalOut/_cinematicCamera, que están
    /// declarados en la clase base CinematicSequencerBase y no son accesibles desde aquí de otro modo).
    static void WireSequencer(PromoVideo01Sequencer sequencer, CinematicCameraDriver driver,
                              Transform estela, Transform liam, Transform will,
                              Transform shotEstelaSolo, Transform shotEstelaLiam,
                              Transform shotWillReveal, Transform shotGroup,
                              Transform markEstela, Transform markLiam, Transform markWill)
    {
        if (sequencer == null) return;

        var so = new SerializedObject(sequencer);

        // Señales: solo se rellenan si están vacías, para no pisar un cambio manual al re-ejecutar.
        SetStringIfEmpty(so, "_signalIn", PromoSignalIn);
        SetStringIfEmpty(so, "_signalOut", PromoSignalOut);

        // Heredado de la clase base: sin esto, Co_BeginCinematicWithTransition(plano inicial) peta
        // con NullReferenceException al intentar cortar al primer plano.
        SetObjectRef(so, "_cinematicCamera", driver);

        SetObjectRef(so, "_estelaTransform", estela);
        SetObjectRef(so, "_liamTransform", liam);
        SetObjectRef(so, "_willTransform", will);

        // Los Animator se resuelven solos en Awake si se dejan vacíos, pero enlazarlos aquí ahorra
        // ese GetComponentInChildren y deja visible en el Inspector cuál es el Animator real de cada uno.
        SetObjectRef(so, "_estelaAnimator", FindAnimator(estela));
        SetObjectRef(so, "_liamAnimator", FindAnimator(liam));
        SetObjectRef(so, "_willAnimator", FindAnimator(will));

        // Caras: el componente que cambia los meshes de ojos/boca. Sobrevive al saneado gracias a la
        // allowlist de StripToVisualOnly (ver PreservedBehaviourTypes), así que aquí ya existe — y si
        // el personaje venía de una instancia vieja sin él, PlaceCharacter() ya lo habrá detectado y
        // regenerado desde el prefab antes de llegar hasta aquí (ver ComponentesPreservadosQueFaltan).
        // Estas tres referencias se REESCRIBEN siempre (SetObjectRef, no SetStringIfEmpty), así que
        // una regeneración nunca deja el enlace apuntando al GameObject destruido.
        SetObjectRef(so, "_estelaEmotion", FindEmotionController(estela, "Estela"));
        SetObjectRef(so, "_liamEmotion", FindEmotionController(liam, "Liam"));
        SetObjectRef(so, "_willEmotion", FindEmotionController(will, "Will"));

        SetObjectRef(so, "_shotEstelaSolo", shotEstelaSolo);
        SetObjectRef(so, "_shotEstelaLiam", shotEstelaLiam);
        SetObjectRef(so, "_shotRevelacionWill", shotWillReveal);
        SetObjectRef(so, "_shotGrupoFinal", shotGroup);

        SetObjectRef(so, "_marcaGrupoEstela", markEstela);
        SetObjectRef(so, "_marcaGrupoLiam", markLiam);
        SetObjectRef(so, "_marcaGrupoWill", markWill);

        // Atajo de teclado para "Simular secuencia" (pedido por Raúl: ir al Inspector y hacer click
        // derecho cada vez que se quiere previsualizar un cambio es un rollo). F6 en Play Mode dispara
        // exactamente lo mismo que el menú contextual — ver _simulateHotkey en CinematicSequencerBase.
        // Se asigna si sigue en Key.None O si tiene un valor heredado inválido (ver comentario de
        // SetInputKeyIfNoneOrInvalid) — no pisa una tecla real que Raúl ya haya elegido a mano.
        SetInputKeyIfNoneOrInvalid(so, "_simulateHotkey", Key.F6);

        // Logo de cierre (guion: "[Fundido a negro. Logo del juego + enlace de itch.io.]"): se
        // autoasigna el mismo sprite que usa LogoTitulo en MainMenu.unity, pero SOLO si el campo
        // sigue vacío (SetObjectRefIfNull, no SetObjectRef) — no pisa un logo distinto que Raúl haya
        // elegido a mano. La tarjeta en sí (fondo negro + logo + texto) la construye sola en runtime
        // PromoVideo01Sequencer.EnsureLogoCard() — a diferencia del CTA, esta no hace falta montarla
        // a mano en la escena.
        SetObjectRefIfNull(so, "_logoSprite",
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/Menu/logo sendero 4.png"));

        // FIX (23 ago 2026, noche): las claves de animación SÍ tienen valor por defecto en el propio
        // campo [SerializeField] de PromoVideo01Sequencer.cs, pero ese valor de C# solo se aplica la
        // PRIMERA vez que Unity serializa el componente. El GameObject de este sequencer ya existía en
        // la escena de Raúl desde ANTES de que esos valores por defecto se escribieran en el código,
        // así que sus 21 campos quedaron congelados en "" para siempre — recompilar el script no los
        // actualiza retroactivamente, y como WireSequencer() nunca los tocaba, volver a ejecutar el
        // menú tampoco los rellenaba. Por eso Raúl los veía vacíos en el Inspector y ningún personaje
        // animaba nada. Se rellenan aquí con SetStringIfEmpty (mismo patrón que _signalIn/_signalOut):
        // si Raúl ya los ha tocado a mano para cuando esto se re-ejecute, no se pisan.
        SetStringIfEmpty(so, "_animEstela01", "Reverence01");
        SetStringIfEmpty(so, "_animEstelaRugido", "Question02");
        SetStringIfEmpty(so, "_animEstela02", "SenseSomethingStart_NoWeapon");
        // MIGRACIÓN (23 ago 2026): InteractWithPeople_NoWeapon → Talk01/02/03. Antes se creía que
        // Talk0X no existían como estados reales del Base Layer (verificación incompleta, ver
        // corrección en la cabecera de PromoVideo01Sequencer.cs); SÍ existen, y dan variedad frente a
        // repetir siempre el mismo gesto de "hablar". Migra el valor viejo si Raúl no lo ha tocado a
        // mano; si ya lo personalizó a otra cosa, se respeta.
        SetStringIfEmptyOrEquals(so, "_animEstela03", "InteractWithPeople_NoWeapon", "Talk01");
        SetStringIfEmpty(so, "_animEstela04", "HeadShake01");
        SetStringIfEmpty(so, "_animEstela05", "Laugh01");
        SetStringIfEmpty(so, "_animEstela06", "Laugh01");
        SetStringIfEmpty(so, "_animEstela07", "LevelUp_NoWeapon");
        SetStringIfEmpty(so, "_animEstela08", "HeadShake02");
        SetStringIfEmpty(so, "_animConjunta", "FoundSomething_NoWeapon");
        SetStringIfEmpty(so, "_animLiam01", "Reverence01");
        SetStringIfEmptyOrEquals(so, "_animLiam02", "InteractWithPeople_NoWeapon", "Talk02");
        SetStringIfEmpty(so, "_animLiam03", "Idle03");        // Se queda impasible a propósito, ya no es aproximación (ver joke de la "cara seria").
        SetStringIfEmpty(so, "_animLiam04", "Question02");    // Aproximación — sin gesto de "señalar" real, revisar a ojo.
        SetStringIfEmptyOrEquals(so, "_animLiam05", "InteractWithPeople_NoWeapon", "Talk03");
        // Reacción muda de Liam a "Si Liam os cae mal..." — campo nuevo (pedido de Raúl), se rellena
        // aquí igualmente por consistencia con el resto.
        SetStringIfEmpty(so, "_animLiamReaccionPulla", "Greeting01_NoWeapon");
        SetStringIfEmpty(so, "_animLiam06", "HeadShake02");
        // MIGRACIÓN (23 ago 2026): Defend_NoWeapon → Attack2. Attack2 SÍ es un estado real del Base
        // Layer (misma corrección de arriba) y encaja mucho mejor con "practicando un espadazo" que
        // la pose de guardia que se usaba antes.
        SetStringIfEmptyOrEquals(so, "_animWillPracticando", "Defend_NoWeapon", "Attack2");
        SetStringIfEmpty(so, "_animWillPillado", "Fear01");
        SetStringIfEmptyOrEquals(so, "_animWill01", "InteractWithPeople_NoWeapon", "Talk01");
        SetStringIfEmpty(so, "_animWill02", "HeadShake01");
        SetStringIfEmpty(so, "_animWill03", "HandWave02");

        so.ApplyModifiedPropertiesWithoutUndo();

        if (estela == null || liam == null || will == null)
            Debug.LogWarning("[PromoStudioSceneBuilder] Alguno de los 3 personajes no se encontró bajo " +
                             $"'{CharactersContainerName}' (Estela/Liam/Will) — el sequencer se ha creado con esa " +
                             "referencia vacía. Revisa la Hierarchy y vuelve a ejecutar el menú.");
    }

    static Animator FindAnimator(Transform character)
        => character != null ? character.GetComponentInChildren<Animator>() : null;

    /// Busca el NPCEmotionController "bueno" de un personaje: el que TIENE un EmotionProfile
    /// asignado.
    ///
    /// No vale un GetComponentInChildren&lt;NPCEmotionController&gt;() a secas porque _WILL.prefab
    /// lleva DOS componentes NPCEmotionController, y además en el MISMO GameObject
    /// ('_WILL/vBasicController_MaleCharacterPBR'): uno con el EmotionProfile compartido asignado
    /// (el mismo que usan Estela y Liam) y otro con el campo vacío, residuo de alguna variante
    /// anterior. El vacío no rompe nada — SetEmotion() sale por la puerta de atrás si no hay perfil —
    /// pero enlazarlo dejaría a Will como el único de los tres al que no le cambia la cara, y como
    /// están en el mismo GameObject no hay forma de distinguirlos por jerarquía: hay que mirar el
    /// perfil. Estela y Liam solo tienen uno, así que para ellos esto equivale a coger el primero.
    ///
    /// El campo 'emotionProfile' es privado con [SerializeField] y NPCEmotionController no expone
    /// getter público (solo el setter SetEmotionProfile), de ahí el SerializedObject — que aquí no
    /// es problema porque esto es código de Editor.
    static NPCEmotionController FindEmotionController(Transform character, string nombreParaLog)
    {
        if (character == null) return null;

        var candidates = character.GetComponentsInChildren<NPCEmotionController>(true);
        if (candidates.Length == 0)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] '{nombreParaLog}' no tiene ningún NPCEmotionController tras el " +
                             "saneado — su cara no cambiará durante el vídeo. Como la auto-reparación de PlaceCharacter() " +
                             "ya regenera desde el prefab a cualquier personaje al que le falte un componente de la " +
                             "allowlist, llegar aquí significa que el PROPIO PREFAB ya no lleva NPCEmotionController: " +
                             "revísalo en el prefab, no en la escena.");
            return null;
        }

        foreach (var candidate in candidates)
        {
            var candidateSo = new SerializedObject(candidate);
            var prop = candidateSo.FindProperty("emotionProfile");
            if (prop != null && prop.objectReferenceValue != null)
            {
                if (candidates.Length > 1)
                    Debug.Log($"[PromoStudioSceneBuilder] '{nombreParaLog}' lleva {candidates.Length} NPCEmotionController " +
                              $"en el prefab; se enlaza el que sí tiene EmotionProfile asignado ('{prop.objectReferenceValue.name}').");
                return candidate;
            }
        }

        Debug.LogWarning($"[PromoStudioSceneBuilder] Ninguno de los {candidates.Length} NPCEmotionController de " +
                         $"'{nombreParaLog}' tiene EmotionProfile asignado — se enlaza el primero, pero su cara no " +
                         "cambiará hasta que se le asigne un perfil en el prefab.");
        return candidates[0];
    }

    // ── Medición real del personaje y cálculo de encuadre ──────────────────────────────────────
    //
    // POR QUÉ EXISTE ESTO: hasta esta pasada los 4 planos eran constantes Vector3/float puestas a
    // ojo suponiendo un humano adulto. Los personajes del juego son un modelo estilizado tipo chibi
    // (RPG Tiny Hero Duo): la cabeza ocupa una fracción del cuerpo muchísimo mayor que en un humano
    // real, así que "mirar a 1.25 m de altura desde 3.4 m" no encuadra la cara, encuadra la frente.
    // Dos rondas de ajuste a ojo fallaron. La solución es dejar de adivinar y MEDIR: esta
    // herramienta corre dentro del Editor, con los prefabs ya instanciados, así que tiene acceso a
    // los bounds reales de los renderers y al avatar del Animator.
    //
    // CAMINO ELEGIDO: los 3 prefabs (_ESTELA/_LIAM/_WILL) comparten el mismo avatar HUMANOID
    // (el generado desde Idle_Battle_SwordAndShiled.fbx, animationType: 3), así que la línea de
    // hombros se saca de los huesos reales del avatar vía Animator.GetBoneTransform(). El alto del
    // pelo (coronilla) SIEMPRE sale de los bounds de los renderers, porque las coletas de Estela no
    // son huesos del avatar humanoide y son justo lo que se estaba saliendo del cuadro. Si algún día
    // un personaje llega con avatar Generic, hay fallback por bounds (ver MedirPersonaje).

    /// Medidas reales, EN COORDENADAS DE MUNDO, de un personaje ya colocado en su marca de blocking.
    class MedidasPersonaje
    {
        public string nombre;
        public Bounds bounds;            // combinado de sus renderers de malla visibles
        public float yPies;              // bounds.min.y
        public float yCoronilla;         // bounds.max.y — incluye pelo/coletas/sombrero
        public float yHombros;           // línea de hombros (hueso del avatar, o fallback por bounds)
        public Vector3 centroSuelo;      // X/Z del transform del personaje, Y al nivel de los pies
        public bool viaHuesoHumanoid;    // true = medido con el avatar; false = fallback por bounds
        public string detalleMedicion;   // para el log

        public float AlturaTotal => Mathf.Max(0.01f, yCoronilla - yPies);
        /// Unidad de composición: de la línea de hombros al alto del pelo. Escala sola entre
        /// proporciones distintas (chibi vs humano), que es justo lo que faltaba antes.
        public float AlturaCabeza => Mathf.Max(0.01f, yCoronilla - yHombros);
    }

    /// Resultado del cálculo de encuadre de un plano.
    struct EncuadreCalculado
    {
        public bool valido;
        public Vector3 pos;
        public Vector3 lookAt;
        public float distancia;
        public float alturaEncuadre;
        public float anchoEncuadre;
        public float yInferior;
        public float ySuperior;
        public float yHombrosRef;
        public float yCoronillaRef;
        public float alturaCabezaRef;
        public bool mandaElAncho;   // true si la distancia la impuso el encaje horizontal, no el vertical
    }

    /// Mide un personaje ya instanciado y colocado. Devuelve null si no hay nada medible.
    static MedidasPersonaje MedirPersonaje(Transform personaje, string nombre)
    {
        if (personaje == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No se puede medir a '{nombre}' (no está en la escena) — " +
                             "los planos que dependan de él caerán al valor fijo de fallback.");
            return null;
        }

        Bounds? acumulado = AcumularBoundsDeMalla(personaje, false);
        bool incluyendoInactivos = false;
        if (!acumulado.HasValue)
        {
            // Red de seguridad: si por lo que sea los renderers están desactivados en el prefab.
            acumulado = AcumularBoundsDeMalla(personaje, true);
            incluyendoInactivos = acumulado.HasValue;
        }

        if (!acumulado.HasValue)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] '{nombre}' no tiene ningún MeshRenderer/SkinnedMeshRenderer " +
                             "del que sacar bounds — no se puede calcular su encuadre, se usará el valor fijo de fallback.");
            return null;
        }

        Bounds b = acumulado.Value;
        var m = new MedidasPersonaje
        {
            nombre = nombre,
            bounds = b,
            yPies = b.min.y,
            yCoronilla = b.max.y,
            centroSuelo = new Vector3(personaje.position.x, b.min.y, personaje.position.z),
        };

        float alturaTotal = Mathf.Max(0.01f, b.max.y - b.min.y);

        // ── Camino 1 (el bueno): avatar Humanoid ────────────────────────────────────────────────
        var anim = BuscarAnimatorHumanoide(personaje);
        if (anim != null)
        {
            var brazoIzq = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var brazoDer = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var pecho = anim.GetBoneTransform(HumanBodyBones.UpperChest);
            if (pecho == null) pecho = anim.GetBoneTransform(HumanBodyBones.Chest);
            if (pecho == null) pecho = anim.GetBoneTransform(HumanBodyBones.Spine);
            var cabeza = anim.GetBoneTransform(HumanBodyBones.Head);

            if (brazoIzq != null && brazoDer != null)
            {
                m.yHombros = (brazoIzq.position.y + brazoDer.position.y) * 0.5f;
                m.viaHuesoHumanoid = true;
                m.detalleMedicion = "Humanoid, media de LeftUpperArm/RightUpperArm";
            }
            else if (pecho != null)
            {
                m.yHombros = pecho.position.y;
                m.viaHuesoHumanoid = true;
                m.detalleMedicion = "Humanoid, hueso de pecho (UpperChest/Chest/Spine)";
            }
            else if (cabeza != null)
            {
                m.yHombros = cabeza.position.y;
                m.viaHuesoHumanoid = true;
                m.detalleMedicion = "Humanoid, hueso Head (sin huesos de brazo/pecho mapeados)";
            }

            if (m.viaHuesoHumanoid && cabeza != null)
                m.detalleMedicion += $"; hueso Head en Y={cabeza.position.y:F3}";
        }

        // ── Camino 2 (fallback): avatar Generic o huesos sin mapear ─────────────────────────────
        // Se toma el tercio superior de los bounds como "zona de cabeza" (regla acordada), y si hay
        // un hueso reconocible por nombre se coge el MENOR de los dos: equivocarse por abajo abre el
        // plano (feo pero utilizable), equivocarse por arriba lo cierra y decapita al personaje —
        // que es exactamente el fallo que se está corrigiendo.
        if (!m.viaHuesoHumanoid)
        {
            float porBounds = b.min.y + (2f / 3f) * alturaTotal;
            m.yHombros = porBounds;
            m.detalleMedicion = "FALLBACK por bounds (avatar no Humanoid): tercio superior de los bounds";

            var huesoPorNombre = BuscarTransformDeCabezaPorNombre(personaje);
            if (huesoPorNombre != null)
            {
                m.yHombros = Mathf.Min(porBounds, huesoPorNombre.position.y);
                m.detalleMedicion += $"; ajustado con el transform '{huesoPorNombre.name}' (Y={huesoPorNombre.position.y:F3}), " +
                                     "se toma el menor de ambos";
            }
        }

        // Un personaje con la cabeza medida por encima del pelo no tiene sentido: red de seguridad.
        if (m.yHombros >= m.yCoronilla - 0.001f)
        {
            m.yHombros = b.min.y + (2f / 3f) * alturaTotal;
            m.detalleMedicion += " [CORREGIDO: la línea de hombros salía por encima de la coronilla, se vuelve al tercio superior]";
        }

        Debug.Log($"[PromoStudioSceneBuilder] 📏 Medida real de '{nombre}': altura total {m.AlturaTotal:F3} m " +
                  $"(pies Y={m.yPies:F3} → coronilla Y={m.yCoronilla:F3}), hombros Y={m.yHombros:F3}, " +
                  $"altura de cabeza (hombros→pelo) {m.AlturaCabeza:F3} m " +
                  $"= {(m.AlturaCabeza / m.AlturaTotal * 100f):F0}% del cuerpo. " +
                  $"Ancho de bounds X={m.bounds.size.x:F3} / Z={m.bounds.size.z:F3}. " +
                  $"Método: {m.detalleMedicion}." +
                  (incluyendoInactivos ? " ⚠ Se han tenido que incluir renderers INACTIVOS para poder medir." : ""));

        return m;
    }

    /// Combina los bounds de mundo de los renderers de MALLA del personaje. Se dejan fuera
    /// ParticleSystemRenderer/TrailRenderer/LineRenderer/SpriteRenderer y demás: un VFX o un trail
    /// perdido dispararía los bounds y alejaría la cámara sin motivo.
    static Bounds? AcumularBoundsDeMalla(Transform personaje, bool incluirInactivos)
    {
        Bounds? acumulado = null;

        foreach (var r in personaje.GetComponentsInChildren<Renderer>(incluirInactivos))
        {
            if (r == null) continue;
            if (!incluirInactivos && !r.enabled) continue;
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;

            var b = r.bounds;
            if (b.size.sqrMagnitude <= 0.0000001f) continue;

            if (acumulado.HasValue)
            {
                var acc = acumulado.Value;
                acc.Encapsulate(b);
                acumulado = acc;
            }
            else
            {
                acumulado = b;
            }
        }

        return acumulado;
    }

    /// El Animator cuyo avatar es Humanoid. No vale GetComponentInChildren&lt;Animator&gt;() a secas:
    /// estos prefabs llevan Animator adicionales en las mallas de arma ('Bows', 'Arrows') con avatar
    /// Generic, y GetBoneTransform() sobre ellos devuelve null siempre.
    static Animator BuscarAnimatorHumanoide(Transform personaje)
    {
        foreach (var a in personaje.GetComponentsInChildren<Animator>(true))
        {
            if (a == null) continue;
            if (a.avatar == null || !a.avatar.isValid) continue;
            if (a.isHuman) return a;
        }
        return null;
    }

    /// Último recurso para rigs no humanoides: un Transform que se llame como una cabeza.
    static Transform BuscarTransformDeCabezaPorNombre(Transform personaje)
    {
        foreach (var t in personaje.GetComponentsInChildren<Transform>(true))
        {
            if (t == null) continue;
            string n = t.name.ToLowerInvariant();
            if (n == "head" || n == "cabeza" || n == "bip01 head" || n == "mixamorig:head")
                return t;
        }
        return null;
    }

    /// Copia unas medidas trasladadas a otra posición (para el plano de grupo, donde el sequencer
    /// teletransporta a los personajes a las marcas antes de que se les vea).
    static MedidasPersonaje DesplazarMedidas(MedidasPersonaje m, Vector3 delta)
    {
        if (m == null) return null;

        var b = m.bounds;
        b.center += delta;

        return new MedidasPersonaje
        {
            nombre = m.nombre,
            bounds = b,
            yPies = m.yPies + delta.y,
            yCoronilla = m.yCoronilla + delta.y,
            yHombros = m.yHombros + delta.y,
            centroSuelo = m.centroSuelo + delta,
            viaHuesoHumanoid = m.viaHuesoHumanoid,
            detalleMedicion = m.detalleMedicion + " (desplazado a la marca de grupo)",
        };
    }

    /// <summary>
    /// Despeja posición y punto de mira de un plano a partir de las medidas reales de sus sujetos.
    ///
    /// Composición vertical (todo en "alturas de cabeza", nunca en metros absolutos):
    ///   ySuperior = coronilla + aireSobreCabeza·alturaCabeza     ← headroom, para que el pelo no
    ///                                                              toque el borde de arriba
    ///   yInferior = hombros  − cabezasBajoHombros·alturaCabeza   ← pecho/cintura (o los pies si
    ///                                                              cuerpoEntero)
    /// El punto de mira va al centro de esa ventana, con lo que la cara queda por encima del centro
    /// del cuadro (≈ línea del tercio superior con los valores por defecto), no clavada en el medio.
    ///
    /// Distancia: fórmula estándar de cámara en perspectiva, resuelta para las DOS dimensiones y
    /// quedándose con la más exigente:
    ///   alturaVisible = 2·d·tan(fovV/2)  →  d = (alturaVisible/2) / tan(fovV/2)
    ///   anchoVisible  = 2·d·tan(fovH/2), con tan(fovH/2) = tan(fovV/2)·aspecto
    /// (Camera.fieldOfView de Unity es el FOV VERTICAL, de ahí que el horizontal salga del aspecto.)
    ///
    /// La cámara se deja A NIVEL (misma altura Y que el punto de mira, desplazada solo en XZ): así el
    /// cálculo de arriba es exacto (sin keystone) y las verticales del personaje no divergen. La
    /// dirección XZ la marca el llamante, que la saca del plano original — el ángulo artístico
    /// (frontal, 3/4, lateral) se conserva; lo que se recalcula es la distancia y la altura.
    /// </summary>
    static EncuadreCalculado CalcularEncuadre(MedidasPersonaje[] sujetos, float fovVertical,
                                              bool cuerpoEntero, float cabezasBajoHombros,
                                              float aireSobreCabeza, float margenLateral,
                                              Vector3 dirCamaraXZ)
    {
        var res = new EncuadreCalculado { valido = false };

        var validos = new System.Collections.Generic.List<MedidasPersonaje>();
        if (sujetos != null)
            foreach (var s in sujetos)
                if (s != null) validos.Add(s);

        if (validos.Count == 0) return res;

        Bounds grupo = validos[0].bounds;
        float yHombrosRef = validos[0].yHombros;      // el hombro MÁS BAJO: así entran los de todos
        float yCoronillaRef = validos[0].yCoronilla;  // la coronilla MÁS ALTA
        float alturaCabezaRef = validos[0].AlturaCabeza;
        Vector3 centroXZ = new Vector3(validos[0].centroSuelo.x, 0f, validos[0].centroSuelo.z);

        for (int i = 1; i < validos.Count; i++)
        {
            grupo.Encapsulate(validos[i].bounds);
            yHombrosRef = Mathf.Min(yHombrosRef, validos[i].yHombros);
            yCoronillaRef = Mathf.Max(yCoronillaRef, validos[i].yCoronilla);
            alturaCabezaRef = Mathf.Max(alturaCabezaRef, validos[i].AlturaCabeza);
            centroXZ += new Vector3(validos[i].centroSuelo.x, 0f, validos[i].centroSuelo.z);
        }
        centroXZ /= validos.Count;

        float alturaTotalGrupo = Mathf.Max(0.01f, grupo.max.y - grupo.min.y);

        float yInferior = cuerpoEntero
            ? grupo.min.y - 0.06f * alturaTotalGrupo   // un poco de suelo bajo los pies
            : Mathf.Max(grupo.min.y, yHombrosRef - cabezasBajoHombros * alturaCabezaRef);
        float ySuperior = yCoronillaRef + aireSobreCabeza * alturaCabezaRef;

        float alturaUtil = Mathf.Max(0.05f, ySuperior - yInferior) * (1f + MargenExtraVertical);
        float centroY = (ySuperior + yInferior) * 0.5f;

        Vector3 dir = dirCamaraXZ;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.back;

        Vector3 lookAt = new Vector3(centroXZ.x, centroY, centroXZ.z);

        // Ancho realmente visible = extensión de los bounds sobre el eje perpendicular a la cámara
        // (no basta con bounds.size.x: en los planos en 3/4 la cámara ve una mezcla de X y Z).
        Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
        float medioAncho = 0f;
        Vector3 c = grupo.center, e = grupo.extents;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 esquina = c + new Vector3(sx * e.x, sy * e.y, sz * e.z);
                    medioAncho = Mathf.Max(medioAncho, Mathf.Abs(Vector3.Dot(esquina - lookAt, perp)));
                }
        float anchoUtil = Mathf.Max(0.05f, 2f * medioAncho) * (1f + 2f * margenLateral);

        float tanV = Mathf.Tan(fovVertical * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * AspectoObjetivo;
        float distanciaPorAltura = (alturaUtil * 0.5f) / tanV;
        float distanciaPorAncho = (anchoUtil * 0.5f) / tanH;
        float distancia = Mathf.Max(distanciaPorAltura, distanciaPorAncho);

        res.valido = true;
        res.lookAt = lookAt;
        res.pos = lookAt + dir * distancia;   // misma Y que lookAt → cámara a nivel
        res.distancia = distancia;
        res.alturaEncuadre = alturaUtil;
        res.anchoEncuadre = anchoUtil;
        res.yInferior = yInferior;
        res.ySuperior = ySuperior;
        res.yHombrosRef = yHombrosRef;
        res.yCoronillaRef = yCoronillaRef;
        res.alturaCabezaRef = alturaCabezaRef;
        res.mandaElAncho = distanciaPorAncho > distanciaPorAltura;

        return res;
    }

    /// Crea un plano con el encuadre CALCULADO a partir de las medidas reales de sus sujetos.
    /// Si la medición no fue posible, cae al valor fijo de siempre y lo dice bien claro en consola.
    static Transform CrearPlanoEncuadrado(Transform parent, string name, string label,
                                          MedidasPersonaje[] sujetos, float fov,
                                          bool cuerpoEntero, float cabezasBajoHombros,
                                          float aireSobreCabeza, float margenLateral,
                                          Vector3 posPorDefecto, Vector3 lookAtPorDefecto)
    {
        // Del plano original se conserva SOLO el ángulo (dirección sujeto→cámara en XZ).
        Vector3 dirCamaraXZ = posPorDefecto - lookAtPorDefecto;
        dirCamaraXZ.y = 0f;
        if (dirCamaraXZ.sqrMagnitude <= 0.0001f) dirCamaraXZ = Vector3.back;
        dirCamaraXZ = dirCamaraXZ.normalized;

        var enc = CalcularEncuadre(sujetos, fov, cuerpoEntero, cabezasBajoHombros,
                                   aireSobreCabeza, margenLateral, dirCamaraXZ);

        if (!enc.valido)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] ⚠ '{name}': no se pudo medir a ningún personaje del plano, " +
                             $"así que se cae al valor FIJO de fallback (pos {posPorDefecto}, mira {lookAtPorDefecto}, " +
                             $"FOV {fov}°). Ese valor está calculado a ojo y suponiendo proporciones de humano adulto: " +
                             "es muy probable que el encuadre salga mal. Revisa que los 3 personajes estén bajo " +
                             $"'{CharactersContainerName}' y vuelve a ejecutar el menú.");
            return CreateOrUpdateShot(parent, name, label, posPorDefecto, lookAtPorDefecto, fov);
        }

        float anguloFuera = Vector3.Angle(-dirCamaraXZ, Vector3.forward);

        Debug.Log($"[PromoStudioSceneBuilder] 🎥 '{name}' CALCULADO (no adivinado):\n" +
                  $"   · Ventana de encuadre vertical: Y {enc.yInferior:F3} → {enc.ySuperior:F3} " +
                  $"({(enc.ySuperior - enc.yInferior):F3} m; con el {MargenExtraVertical * 100f:F0}% de margen extra, " +
                  $"{enc.alturaEncuadre:F3} m). Referencias: hombros Y={enc.yHombrosRef:F3}, coronilla Y={enc.yCoronillaRef:F3}, " +
                  $"altura de cabeza {enc.alturaCabezaRef:F3} m.\n" +
                  $"   · Ancho a cubrir: {enc.anchoEncuadre:F3} m (margen lateral {margenLateral * 100f:F0}% por lado).\n" +
                  $"   · FOV vertical {fov}° · aspecto de referencia {AspectoObjetivo:F3} → " +
                  $"distancia despejada {enc.distancia:F3} m " +
                  $"({(enc.mandaElAncho ? "MANDA EL ANCHO: el sujeto es más ancho que alto para este cuadro" : "manda la altura, lo normal")}).\n" +
                  $"   · Cámara en {enc.pos} mirando a {enc.lookAt} " +
                  $"(a nivel: misma Y que el punto de mira; {anguloFuera:F1}° fuera del eje frontal, ángulo heredado del plano original).");

        return CreateOrUpdateShot(parent, name, label, enc.pos, enc.lookAt, fov);
    }

    /// Avisa si un personaje que NO debería verse todavía cae dentro del cuadro de un plano.
    /// El guion depende de ello (Will no puede asomar antes de su revelación).
    static void AvisarSiEntraEnCuadro(Transform shot, float fovVertical, MedidasPersonaje intruso,
                                      string nombreIntruso, string nombrePlano)
    {
        if (shot == null || intruso == null) return;

        float tanV = Mathf.Tan(fovVertical * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * AspectoObjetivo;
        Quaternion inv = Quaternion.Inverse(shot.rotation);

        Vector3 c = intruso.bounds.center, e = intruso.bounds.extents;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 esquina = c + new Vector3(sx * e.x, sy * e.y, sz * e.z);
                    Vector3 local = inv * (esquina - shot.position);
                    if (local.z <= 0.01f) continue;
                    if (Mathf.Abs(local.x) <= local.z * tanH && Mathf.Abs(local.y) <= local.z * tanV)
                    {
                        Debug.LogWarning($"[PromoStudioSceneBuilder] ⚠ '{nombreIntruso}' asoma dentro del cuadro de " +
                                         $"'{nombrePlano}' (comprobado con el frustum real: FOV {fovVertical}°, aspecto " +
                                         $"{AspectoObjetivo:F3}). El guion cuenta con que NO se le vea todavía ahí: " +
                                         "aparta su posición de blocking (constantes LiamStartPosition / WillPosition " +
                                         "arriba en este script) y vuelve a ejecutar el menú.");
                        return;
                    }
                }
    }

    /// Crea (o reposiciona) un punto de plano cinemático. CinematicShot exige un componente Camera
    /// en el mismo GameObject ([RequireComponent]) que él mismo deja siempre desactivado: nunca
    /// renderiza, solo sirve como preview de encuadre en la Scene View. Se deja sin tag para que
    /// Camera.main siga siendo la cámara de estudio y no una de estas.
    static Transform CreateOrUpdateShot(Transform parent, string name, string label,
                                        Vector3 position, Vector3 lookAt, float fov)
    {
        var existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        var cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov;
        cam.enabled = false; // CinematicShot también lo fuerza, pero así queda claro desde el minuto 0

        var shot = go.GetComponent<CinematicShot>();
        if (shot == null) shot = go.AddComponent<CinematicShot>();
        shot.label = label;

        go.transform.position = position;
        Vector3 dir = lookAt - position;
        if (dir.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);

        return go.transform;
    }

    /// Marca de blocking del plano de grupo: GameObject vacío, mirando hacia la cámara del plano final.
    static Transform CreateOrUpdateMark(Transform parent, string name, Vector3 position, Vector3 lookAtGroundPoint)
    {
        var mark = GetOrCreateChild(parent, name);
        mark.position = position;

        Vector3 dir = lookAtGroundPoint - position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
            mark.rotation = Quaternion.LookRotation(dir.normalized);

        return mark;
    }

    static Transform GetOrCreateChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static T GetOrAddComponentOnChild<T>(Transform parent, string childName) where T : Component
    {
        var child = GetOrCreateChild(parent, childName);
        var comp = child.GetComponent<T>();
        if (comp == null) comp = child.gameObject.AddComponent<T>();
        return comp;
    }

    static void SetObjectRef(SerializedObject so, string propertyPath, UnityEngine.Object value)
    {
        var prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No existe el campo serializado '{propertyPath}' en " +
                             $"{so.targetObject.GetType().Name} — ¿se ha renombrado? Enlázalo a mano en el Inspector.");
            return;
        }
        prop.objectReferenceValue = value;
    }

    /// Como SetObjectRef, pero solo si el campo sigue vacío — para referencias que Raúl puede querer
    /// elegir a mano (ej: un sprite de logo alternativo) y que no deben pisarse en cada reejecución
    /// del builder, a diferencia de _cinematicCamera/_estelaTransform/etc., que SIEMPRE se regeneran
    /// porque dependen de objetos que el builder también regenera.
    static void SetObjectRefIfNull(SerializedObject so, string propertyPath, UnityEngine.Object value)
    {
        var prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No existe el campo serializado '{propertyPath}' en " +
                             $"{so.targetObject.GetType().Name} — ¿se ha renombrado? Asígnalo a mano en el Inspector.");
            return;
        }
        if (prop.objectReferenceValue == null)
            prop.objectReferenceValue = value;
    }

    static void SetStringIfEmpty(SerializedObject so, string propertyPath, string value)
    {
        var prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No existe el campo serializado '{propertyPath}' en " +
                             $"{so.targetObject.GetType().Name} — ¿se ha renombrado? Rellénalo a mano en el Inspector.");
            return;
        }
        if (string.IsNullOrEmpty(prop.stringValue))
            prop.stringValue = value;
    }

    /// FIX (24 ago 2026): como SetInputKeyIfNone (helper original, retirado por no usarse ya en ningún
    /// sitio — su lógica de "solo si sigue en Key.None" quedó absorbida aquí), pero ADEMÁS corrige un entero que ya no es un
    /// miembro real de Key. Caso real que lo motivó: _simulateHotkey empezó siendo KeyCode (con
    /// KeyCode.F6 = 287 ya grabado en la escena por una ejecución anterior de este mismo builder) y
    /// se migró a Key JUSTO DESPUÉS — Unity no valida un enum al deserializarlo, así que ese 287 se
    /// quedó tal cual en el componente, sin corresponder a ningún Key real. SetInputKeyIfNone no lo
    /// tocaba (no vale Key.None), así que Keyboard.current[key] lanzaba ArgumentOutOfRangeException
    /// en cada Update() hasta que alguien lo corrigiera a mano (ver guard defensivo en
    /// CinematicSequencerBase.Update()). Con este helper, volver a ejecutar el menú lo autocorrige
    /// sin tocar el Inspector — sigue respetando una tecla real que Raúl haya elegido a mano.
    static void SetInputKeyIfNoneOrInvalid(SerializedObject so, string propertyPath, Key value)
    {
        var prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No existe el campo serializado '{propertyPath}' en " +
                             $"{so.targetObject.GetType().Name} — ¿se ha renombrado? Asígnalo a mano en el Inspector.");
            return;
        }
        bool isNone = prop.intValue == (int)Key.None;
        bool isValidKey = System.Enum.IsDefined(typeof(Key), prop.intValue);
        if (isNone || !isValidKey)
        {
            if (!isNone && !isValidKey)
                Debug.LogWarning($"[PromoStudioSceneBuilder] '{propertyPath}' tenía un valor ({prop.intValue}) " +
                                 "que no es una tecla válida (probablemente heredado de KeyCode antes de migrar " +
                                 $"el campo a Key) — corregido a {value}.");
            prop.intValue = (int)value;
        }
    }

    /// Como SetStringIfEmpty, pero además migra un valor ANTIGUO conocido a uno nuevo — para cuando
    /// se corrige el valor por defecto de un campo en el código (23 ago 2026: varios _animXxx pasaron
    /// de aproximaciones a estados reales tras verificar mejor el .controller, ver
    /// PromoVideo01Sequencer.cs) pero el campo ya tiene ese valor antiguo serializado en la escena de
    /// Raúl (SetStringIfEmpty no lo tocaría, porque no está vacío). Solo sobrescribe si el valor
    /// actual es EXACTAMENTE el antiguo conocido — si Raúl ya lo ha personalizado a mano a otra cosa,
    /// se respeta sin tocar.
    static void SetStringIfEmptyOrEquals(SerializedObject so, string propertyPath, string valorAntiguo, string valorNuevo)
    {
        var prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning($"[PromoStudioSceneBuilder] No existe el campo serializado '{propertyPath}' en " +
                             $"{so.targetObject.GetType().Name} — ¿se ha renombrado? Rellénalo a mano en el Inspector.");
            return;
        }
        if (string.IsNullOrEmpty(prop.stringValue) || prop.stringValue == valorAntiguo)
            prop.stringValue = valorNuevo;
    }

    // ── Instanciado "solo visual" de los prefabs jugables (adaptado de MainMenuStylingBuilder.cs) ─
    // Ver el comentario de cabecera del archivo para el porqué: si el GameObject queda activo desde
    // el primer frame, Awake()/OnEnable() de TODOS sus MonoBehaviour (Invector + sistemas propios:
    // PlayerInputManager, PlayerHealthSystem, PlayerPresetService, WardrobeInventory...) se ejecutan
    // de forma síncrona antes de que cualquier código posterior pueda desactivar nada — si alguno se
    // registra como singleton/servicio, para cuando lo desactivas el daño ya está hecho. La solución
    // es instanciar como hijo de un contenedor temporal DESACTIVADO (Unity no ejecuta Awake/OnEnable
    // de ningún hijo mientras el padre esté inactivo), destruir ahí mismo casi todos los MonoBehaviour/
    // Collider/Rigidbody/Camera/AudioListener, y solo entonces reparentar al contenedor real y activar.
    // El Animator no es un MonoBehaviour (es un Behaviour interno de Unity) y sobrevive intacto.

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

    /// <summary>
    /// Allowlist del saneado: scripts que SOBREVIVEN a StripToVisualOnly en vez de ser destruidos.
    ///
    /// Ahora mismo solo NPCEmotionController, el componente que cambia las caras de los personajes
    /// (activa/desactiva los meshes de ojos y boca según la emoción). El sequencer lo necesita vivo
    /// para llamar a SetEmotion() en cada línea, igual que hacen TabernaSequencer y
    /// LiamCrystalBallSequencer con los NPC del juego real. Hasta esta pasada caía en la purga
    /// general junto con el resto de MonoBehaviour y por eso las caras no cambiaban en el vídeo.
    ///
    /// Es seguro mantenerlo vivo aunque desaparezca todo lo demás: en Awake() busca
    /// NPCSimpleAnimator y NPCBehaviourManagerV2 pero SIEMPRE los usa con '?.', así que no revienta
    /// si no existen; y en OnEnable() solo se suscribe a eventos estáticos de DialogueManager, que
    /// en esta escena no se disparan nunca (el sequencer llama a SetEmotion() directamente, sin
    /// pasar por DialogueManager). Tampoco declara ningún [RequireComponent], así que no obliga a
    /// mantener vivo nada más.
    /// </summary>
    static readonly Type[] PreservedBehaviourTypes = { typeof(NPCEmotionController) };

    static void StripToVisualOnly(GameObject go)
    {
        int destroyedScripts = 0, destroyedPhysics = 0, destroyedAvOutputs = 0;

        // Varios scripts del rig se declaran dependientes entre sí vía [RequireComponent] (ej.:
        // NPCBehaviourManagerV2 requiere NPCSimpleAnimator). Unity no deja destruir un componente
        // mientras algo en el mismo GameObject dependa de él, así que se lee por reflexión el
        // [RequireComponent] de cada script presente y en cada pasada solo se destruyen los que ya
        // no son requeridos por ningún otro script todavía vivo — el orden correcto sale solo.
        var behaviours = new System.Collections.Generic.List<MonoBehaviour>(go.GetComponentsInChildren<MonoBehaviour>(true));
        behaviours.RemoveAll(mb => mb == null); // referencias de script roto/perdido

        // Allowlist (ver PreservedBehaviourTypes): se apartan ANTES del barrido, así el bucle de
        // abajo ni los mira. Se guardan en su propia lista en vez de descartarlos del todo porque
        // sus [RequireComponent] tienen que seguir contando al decidir el orden de destrucción del
        // resto: si un script preservado dependiera de otro, ese otro tampoco se puede destruir.
        var preserved = new System.Collections.Generic.List<MonoBehaviour>();
        for (int i = behaviours.Count - 1; i >= 0; i--)
        {
            if (IsPreservedBehaviour(behaviours[i]))
            {
                preserved.Add(behaviours[i]);
                behaviours.RemoveAt(i);
            }
        }

        for (int pass = 0; pass < 8 && behaviours.Count > 0; pass++)
        {
            var stillRequired = new System.Collections.Generic.HashSet<Type>();
            CollectRequiredTypes(behaviours, stillRequired);
            CollectRequiredTypes(preserved, stillRequired);

            int removedThisPass = 0;
            for (int i = behaviours.Count - 1; i >= 0; i--)
            {
                var mb = behaviours[i];
                bool isStillNeeded = false;
                foreach (var t in stillRequired)
                {
                    if (t.IsInstanceOfType(mb)) { isStillNeeded = true; break; }
                }
                if (isStillNeeded) continue;

                UnityEngine.Object.DestroyImmediate(mb, true);
                behaviours.RemoveAt(i);
                destroyedScripts++;
                removedThisPass++;
            }

            if (removedThisPass == 0) break;
        }

        if (behaviours.Count > 0)
        {
            // Solo puede pasar con una dependencia [RequireComponent] circular real (que ni el propio
            // Inspector de Unity dejaría deshacer a mano), o si algún día un script de la allowlist
            // PreservedBehaviourTypes declarase un [RequireComponent] sobre otro script (hoy
            // NPCEmotionController no declara ninguno). Red de seguridad: desactivar en vez de
            // destruir (evita Update/comportamiento posterior, aunque su Awake ya se habrá disparado).
            var names = new System.Collections.Generic.List<string>();
            foreach (var mb in behaviours)
            {
                mb.enabled = false;
                names.Add(mb.GetType().Name);
            }
            Debug.LogWarning($"[PromoStudioSceneBuilder] '{go.name}': {behaviours.Count} script(s) con una dependencia " +
                              $"[RequireComponent] circular entre sí no se pudieron eliminar ({string.Join(", ", names)}) — " +
                              "se han desactivado como red de seguridad, revísalo si notas algo raro procedente de ellos.");
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

        // Camera y AudioListener no son MonoBehaviour, así que el barrido de arriba no los toca —
        // pero estos rigs suelen traer su propia cámara en tercera persona colgando del prefab, que
        // competiría con la Cámara de Estudio real de esta escena si se queda activa.
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

        Debug.Log($"[PromoStudioSceneBuilder] '{go.name}': {destroyedScripts} script(s), {destroyedPhysics} componente(s) " +
                  $"de física y {destroyedAvOutputs} cámara(s)/listener(s) eliminados antes de activarlo " +
                  $"(quedan Animator/Renderers y {preserved.Count} script(s) preservado(s) de la allowlist: " +
                  $"{(preserved.Count > 0 ? string.Join(", ", preserved.ConvertAll(mb => mb.GetType().Name)) : "ninguno")}).");
    }

    static bool IsPreservedBehaviour(MonoBehaviour mb)
    {
        if (mb == null) return false;
        foreach (var t in PreservedBehaviourTypes)
            if (t.IsInstanceOfType(mb)) return true;
        return false;
    }

    /// Vuelca en 'into' todos los tipos que los scripts de 'source' declaran como obligatorios vía
    /// [RequireComponent] (hasta 3 por atributo, que es el máximo que admite Unity).
    static void CollectRequiredTypes(System.Collections.Generic.List<MonoBehaviour> source,
                                     System.Collections.Generic.HashSet<Type> into)
    {
        foreach (var mb in source)
            foreach (var attr in mb.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                var rc = (RequireComponent)attr;
                if (rc.m_Type0 != null) into.Add(rc.m_Type0);
                if (rc.m_Type1 != null) into.Add(rc.m_Type1);
                if (rc.m_Type2 != null) into.Add(rc.m_Type2);
            }
    }

    // ── Utilidades ───────────────────────────────────────────────────────────────────────────────

    static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = System.IO.Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }

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
