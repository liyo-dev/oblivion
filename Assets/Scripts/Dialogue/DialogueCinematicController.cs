using UnityEngine;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using System.Collections;
using System.Collections.Generic;
using Sendero.Core.Feedback;

/// <summary>
/// Controlador cinematográfico avanzado para diálogos usando Cinemachine.
/// Gestiona múltiples cámaras virtuales y transiciones entre ellas.
/// </summary>
public class DialogueCinematicController : MonoBehaviour
{
    public static DialogueCinematicController Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif

    [Header("Configuración")]
    [SerializeField] private DialogueCinematicProfile defaultProfile;
    [SerializeField] private bool showDebugInfo;
    [SerializeField] private int maxPooledCameras = 4;  // Reducido de 8 a 4 - suficiente para alternar entre shots

    // Estado actual - expuesto para detectar diálogos encadenados
    private bool isInCinematicMode;
    public bool IsInCinematicMode => isInCinematicMode;
    
    // Sistema de delay para encadenamiento de diálogos
    private Coroutine pendingEndCinematicCoroutine;
    private bool isPendingEnd;
    [SerializeField] private float chainDialogueGracePeriod = 0.2f; // Tiempo para detectar encadenamiento
    
    private Transform currentPlayer;
    private Transform currentNPC;
    private DialogueCinematicProfile activeProfile;
    private CinemachineCamera currentVirtualCamera;
    private int currentLineIndex;
    private int nextCutAtLine;

    // Modo conversación grupal
    private bool _isGroupConversation;
    private Vector3 _groupCamBaseDir = Vector3.back; // dirección fija calculada al inicio del diálogo
    private Vector3 _cachedGroupCenter;              // centro del grupo (actualizado en CalculateCameraPosition)
    [Header("Conversación Grupal")]
    [Tooltip("Distancia total de la cámara al centro del grupo (metros). Aumenta para ver a más personajes.")]
    [SerializeField] private float groupCameraDistance = 5f;
    [Tooltip("Ángulo de elevación de la cámara (grados sobre horizontal). " +
             "0 = horizontal, 90 = cenital. " +
             "30-45° = vista cinematográfica elevada que ve a varios NPCs sin ser cenital.")]
    [Range(10f, 80f)]
    [SerializeField] private float groupElevationAngle = 38f;
    [Tooltip("Altura del punto al que mira la cámara dentro del grupo (0 = suelo, 1 = torso, 1.5 = cabeza)")]
    [SerializeField] private float groupLookAtHeight = 1.1f;
    [Tooltip("FOV para el plano grupal. Aumenta para un plano más abierto que el diálogo normal (ej: 60-70)")]
    [SerializeField] private float groupFieldOfView = 65f;
    [Tooltip("Sesgo de la cámara hacia el speaker actual (0 = cámara fija, 1 = cámara centrada en speaker)")]
    [Range(0f, 0.6f)]
    [SerializeField] private float groupSpeakerBias = 0.25f;
    [Tooltip("Tiempo de suavizado para la interpolación del lookAt grupal (segundos)")]
    [SerializeField] private float groupLookAtSmoothTime = 0.6f;
    [Tooltip("Distancia mínima libre requerida entre la cámara y el centro del grupo (metros)")]
    [SerializeField] private float groupMinClearance = 2.5f;
    [Tooltip("Radio de la esfera usada para detectar obstrucciones cerca de la cámara")]
    [SerializeField] private float groupCameraProbeRadius = 0.4f;
    [Tooltip("Modo 1:1: margen inicial del SphereCast de obstrucción respecto a la cabeza del personaje, para no detectar sus propios colliders/props como obstáculo (metros)")]
    [SerializeField] private float dialogueCameraSelfClearance = 0.5f;
    [Tooltip("Modo 1:1: distancia mínima a la que se permite acercar la cámara al esquivar una obstrucción. Los planos normales están entre 3 y 5m; bajarla demasiado produce un primerísimo plano roto (metros)")]
    [SerializeField] private float dialogueCameraMinDistance = 2.4f;

    [Header("Anti-intrusión (NPCs ajenos cruzándose en cámara)")]
    [Tooltip("Radio (m) alrededor del punto medio de la conversación en el que se detiene la navegación de NPCs ambientales ajenos al diálogo (no el NPC principal, no el player, no party) mientras dura la escena.")]
    [SerializeField] private float bystanderFreezeRadius = 9f;
    [Tooltip("Cada cuánto (segundos) se reescanea el radio de congelado, por si un NPC nuevo entra en él o alguno reanuda su ruta entre reescaneos")]
    [SerializeField] private float bystanderRescanInterval = 0.6f;
    [Tooltip("Cada cuánto (segundos) se comprueba si algún personaje se ha metido entre la cámara y quien habla (red de seguridad final, independiente del congelado preventivo)")]
    [SerializeField] private float intruderCheckInterval = 0.12f;
    [Tooltip("Radio del SphereCast usado para detectar intrusos entre la cámara y la cara de quien habla")]
    [SerializeField] private float intruderProbeRadius = 0.3f;

    // ── Anti-intrusión: NPCs ambientales que caminan dentro del encuadre del diálogo ──
    // Contexto (Agosto 2026): la cámara de diálogo solo evitaba geometría estática (paredes) y
    // ocultaba party members no-hablantes; cualquier NPC ambiental ajeno a la conversación podía
    // cruzarse libremente delante de cámara sin que nada lo detectara. Doble red, sin tocar la FSM:
    //  1) Preventiva (RescanAndFreezeBystanders): HardStop periódico sobre el NavMeshAgent de los
    //     NPCs ambientales cercanos que no participan en el diálogo. Solo actúa a nivel de agente
    //     (no cambia su estado ni su lógica), así que no hay riesgo de interferir con quests o el
    //     grafo narrativo — en el peor caso el NPC retoma su ruta con normalidad al siguiente tick.
    //  2) Reactiva (CheckAndHideCameraIntruders): si aun así algo se cruza en la línea cámara→cara
    //     del speaker, se oculta su renderer (mismo patrón que HideNonSpeakingPartyMembers) hasta
    //     que deje de bloquear. Esta es la garantía real: aunque el congelado falle o llegue tarde,
    //     nunca se ve a un personaje tapando la cámara en primerísimo plano.
    //
    // Incidencia (Agosto 2026): "el NPC que habla desaparece, tapado por algo, y coincide siempre
    // con el turno de hablar". Causa real: la capa reactiva (2) solo trataba como intruso a un
    // collider si su root ya estaba en _bystanderRootTransforms (un NPC ambiental fichado por la
    // capa 1). Objetos de escenario que NO son NPCs — típicamente una puerta abriéndose/cerrándose
    // junto a la conversación — nunca entraban en esa lista, así que si la hoja de la puerta
    // empezaba a moverse DESPUÉS de fijar el plano (CalculateCameraPosition solo evita obstrucciones
    // que ya existían en ESE instante, no vuelve a comprobar mientras la línea sigue en pantalla),
    // terminaba cruzándose en la línea cámara→cara sin que nada la detectara. Encima, si no había
    // ningún NPC ambiental cerca (caso típico: solo player + NPC + puerta en la sala),
    // _bystanderRootTransforms.Count era 0 y CheckAndHideCameraIntruders ni siquiera llegaba a
    // lanzar el SphereCast — la red de seguridad estaba deshabilitada del todo en esos diálogos.
    // Fix: la capa reactiva ya no depende de la lista de bystanders fichados; cualquier collider
    // que se cruce en esa línea (que no sea el propio speaker/player/NPC/party) se trata como
    // intruso, sea o no un NPC conocido.
    private readonly List<Game.NPC.NPCBehaviourManagerV2> _nearbyBystanderBuffer = new List<Game.NPC.NPCBehaviourManagerV2>();
    private readonly HashSet<Game.NPC.NPCBehaviourManagerV2> _frozenBystanders = new HashSet<Game.NPC.NPCBehaviourManagerV2>();
    private readonly HashSet<Transform> _bystanderRootTransforms = new HashSet<Transform>();
    private float _bystanderRescanTimer;

    private readonly RaycastHit[] _intruderHitsBuffer = new RaycastHit[8];
    // Buffer para la verificación de "cámara embebida" (ver CameraPositionIsEmbedded)
    private readonly Collider[] _camEmbedCheckBuffer = new Collider[8];
    private readonly HashSet<Transform> _hiddenIntruders = new HashSet<Transform>();
    private readonly HashSet<Transform> _frameIntruders = new HashSet<Transform>();
    private readonly List<Transform> _intrudersToRestoreBuffer = new List<Transform>();
    private readonly Dictionary<Renderer, bool> _hiddenIntruderOrigState = new Dictionary<Renderer, bool>();
    private float _intruderCheckTimer;

    // Interpolación suave del lookAt en modo grupal
    private Vector3 _groupLookAtTarget;
    private Vector3 _groupLookAtVelocity;

    // Máscara de colisión para obstrucciones de cámara (cacheada en Awake)
    private int _camObstructionMask;

    // Buffer cacheado para los probes de cámara grupal (evita alloc por SphereCastNonAlloc).
    // Los personajes comparten capa Default con la geometría, así que los probes deben
    // descartar hits contra cualquier root con NPCSimpleAnimator (mismo criterio que
    // PlayerParty.FindClearDialogueFormationPosition): un compañero de pie no es una pared.
    private readonly RaycastHit[] _groupCamProbeBuffer = new RaycastHit[16];

    // ✅ Sistema de tracking de speakers para cambios de cámara
    private string currentSpeakerId;     // ID del speaker actual (speakerNameId o "Player")
    private Transform currentSpeaker;     // Transform del speaker actual
    private Dictionary<string, Transform> speakerCache = new Dictionary<string, Transform>(); // Cache de speakers

    // Pool de cámaras virtuales
    private List<CinemachineCamera> cameraPool = new List<CinemachineCamera>();
    private int poolIndex;
    
    // Referencias internas (se encuentran automáticamente)
    private Transform cameraRig;
    private CinemachineCamera mainGameplayCamera;
    private int originalGameplayCameraPriority;

    // Cámara dedicada para diálogos (separada de la main camera)
    private GameObject dialogueCameraObject;
    private Camera dialogueCamera;
    private CinemachineBrain dialogueBrain;
    
    // Control de rendering forzado
    private bool forceRenderingActive;
    
    // Referencia a la cámara principal de gameplay de Unity (no la virtual de Cinemachine)
    private Camera mainUnityCamera;
    private bool mainUnityCameraWasEnabled;
    
    // Ocultación del player durante cinematográficas
    private Renderer[] playerRenderers;
    private bool[] playerRenderersWereEnabled;

    // Sistema de efectos cinematográficos por línea (CloseUp / Dramatic / Impact)
    private Coroutine _effectsCoroutine;
    private bool _effectActive;

    // Ocultación de party members no-hablantes en modo individual
    private readonly List<Renderer> _hiddenPartyRenderers = new List<Renderer>();
    private readonly List<bool> _hiddenPartyRenderersOrigState = new List<bool>();

    // Rotación suave del player y del NPC principal en modo grupal (mirar al speaker)
    private Coroutine _playerRotationCoroutine;
    private Coroutine _npcRotationCoroutine;
    [Tooltip("Duración de la rotación suave del player hacia el interlocutor en modo grupal")]
    [SerializeField] private float groupPlayerRotationDuration = 0.35f;

    // ── Head-look (mirada de cabeza estilo IK) en modo grupal ──
    // Componentes DialogueHeadLook añadidos bajo demanda a los participantes; cacheados
    // para no hacer GetComponent por línea. Se limpian (fade out) al terminar la cinemática.
    private readonly Dictionary<Transform, DialogueHeadLook> _headLookCache = new Dictionary<Transform, DialogueHeadLook>();

    void Awake()
    {
        // Debug.Log($"[DialogueCinematicController] ⚡ Awake iniciado en GameObject: {gameObject.name}");
        
        // CRÍTICO: El GameObject DEBE estar activo para funcionar
        if (!gameObject.activeSelf)
        {
            Debug.LogError($"[DialogueCinematicController] ❌ GameObject '{gameObject.name}' estaba DESACTIVADO - Activándolo automáticamente");
            gameObject.SetActive(true);
        }
        
        // Singleton pattern con DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[DialogueCinematicController] Ya existe una instancia, destruyendo {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // Debug.Log($"[DialogueCinematicController] ✅ Instancia marcada como DontDestroyOnLoad");

        // Cachear máscara de colisión para obstrucciones de cámara grupal
        // Default (0), Floor (10), Obstacle (15) — geometría estática que puede bloquear la vista
        _camObstructionMask = (1 << 0) | (1 << 10) | (1 << 15);

        // Crear rig para las cámaras de diálogo
        GameObject rigObj = new GameObject("DialogueCinematicRig");
        rigObj.transform.SetParent(transform);
        cameraRig = rigObj.transform;

        // Crear cámara dedicada para diálogos (separada de Camera.main)
        CreateDialogueCamera();

        // Los efectos cinematográficos (shake, flash) usan FeedbackService directamente
        // — sin Volume de post-procesado (viñeta/aberración eliminadas por feedback del usuario).
    }

    /// <summary>
    /// Aplica a dialogueCamera los clearFlags correctos según el entorno actual del jugador:
    /// interior con SolidColor, interior con Skybox propio, o exterior con Skybox.
    /// Se llama cada vez que se activa la cámara de diálogo para evitar que el snapshot
    /// de Awake quede desactualizado.
    /// </summary>
    private void SyncClearFlagsToEnvironment()
    {
        if (dialogueCamera == null) return;

        var envCtrl = EnvironmentController.Instance;
        if (envCtrl == null) return;

        if (envCtrl.CurrentMode == EnvironmentMode.Interior)
        {
            var interior = envCtrl.CurrentInterior;

            if (interior != null && interior.useSolidColorBackground)
            {
                dialogueCamera.clearFlags = CameraClearFlags.SolidColor;
                dialogueCamera.backgroundColor = interior.interiorBgColor;
                // Limpiar override de skybox per-cámara si lo hubiera
                var camSkybox = dialogueCamera.GetComponent<Skybox>();
                if (camSkybox != null) camSkybox.material = null;
            }
            else if (interior != null && interior.interiorSkyboxOverride != null)
            {
                // Skybox propio del interior: usar componente por-cámara para no tocar RenderSettings global
                dialogueCamera.clearFlags = CameraClearFlags.Skybox;
                var camSkybox = dialogueCamera.GetComponent<Skybox>();
                if (camSkybox == null) camSkybox = dialogueCamera.gameObject.AddComponent<Skybox>();
                camSkybox.material = interior.interiorSkyboxOverride;
            }
            else
            {
                // Interior sin config → negro sólido como fallback seguro
                dialogueCamera.clearFlags = CameraClearFlags.SolidColor;
                dialogueCamera.backgroundColor = Color.black;
            }
        }
        else
        {
            // Exterior: Skybox normal (RenderSettings.skybox ya está configurado por EnvironmentController)
            dialogueCamera.clearFlags = CameraClearFlags.Skybox;
            // Limpiar override per-cámara que pudiera haber quedado de un interior anterior
            var camSkybox = dialogueCamera.GetComponent<Skybox>();
            if (camSkybox != null) camSkybox.material = null;
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueCinematicController] ClearFlags → {dialogueCamera.clearFlags} (EntornoActual: {envCtrl.CurrentMode})");
    }

    /// <summary>
    /// Crea una cámara separada específicamente para los diálogos
    /// </summary>
    private void CreateDialogueCamera()
    {
        // Crear GameObject para la cámara de diálogo
        dialogueCameraObject = new GameObject("DialogueCamera");
        dialogueCameraObject.transform.SetParent(transform);
        dialogueCameraObject.transform.position = Vector3.zero;
        
        // Añadir componente Camera
        dialogueCamera = dialogueCameraObject.AddComponent<Camera>();
        
        // Copiar settings de Camera.main si existe
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Usar Skybox en lugar de SolidColor para forzar rendering continuo
            dialogueCamera.clearFlags = mainCam.clearFlags;
            dialogueCamera.backgroundColor = mainCam.backgroundColor;
            dialogueCamera.cullingMask = mainCam.cullingMask;
            dialogueCamera.orthographic = mainCam.orthographic;
            dialogueCamera.fieldOfView = mainCam.fieldOfView;
            dialogueCamera.nearClipPlane = mainCam.nearClipPlane;
            dialogueCamera.farClipPlane = mainCam.farClipPlane;
            dialogueCamera.allowHDR = mainCam.allowHDR;
            dialogueCamera.allowMSAA = mainCam.allowMSAA;
            
            // CRÍTICO PARA URP: Copiar la configuración de URP de la cámara principal
            var mainCameraData = mainCam.GetUniversalAdditionalCameraData();
            if (mainCameraData != null)
            {
                var dialogueCameraData = dialogueCamera.GetUniversalAdditionalCameraData();
                dialogueCameraData.renderType = CameraRenderType.Base; // Es una cámara base independiente
                dialogueCameraData.renderPostProcessing = mainCameraData.renderPostProcessing;
                dialogueCameraData.antialiasing = mainCameraData.antialiasing;
                dialogueCameraData.stopNaN = mainCameraData.stopNaN;
                dialogueCameraData.dithering = mainCameraData.dithering;
                dialogueCameraData.renderShadows = mainCameraData.renderShadows;
                dialogueCameraData.requiresDepthTexture = mainCameraData.requiresDepthTexture;
                dialogueCameraData.requiresColorTexture = mainCameraData.requiresColorTexture;
                // Debug.Log($"[DialogueCinematicController] URP Camera Data configurada - RenderType: Base, PostProcessing: {dialogueCameraData.renderPostProcessing}");
            }
        }
        else
        {
            // Fallback si no hay Camera.main - usar Skybox para forzar rendering
            dialogueCamera.clearFlags = CameraClearFlags.Skybox;
            dialogueCamera.cullingMask = ~0; // Renderizar todo
            
            // Configurar URP para cámara independiente
            var dialogueCameraData = dialogueCamera.GetUniversalAdditionalCameraData();
            dialogueCameraData.renderType = CameraRenderType.Base;
        }
        
        // IMPORTANTE: Asegurar que la capa UI está incluida en el cullingMask
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            dialogueCamera.cullingMask |= (1 << uiLayer);
        }
        
        // Configurar depth para que se renderice SOBRE la cámara principal
        dialogueCamera.depth = 10; // Mayor que la main camera
        
        // CRÍTICO: Asegurar que renderiza directo a pantalla
        dialogueCamera.targetTexture = null;
        dialogueCamera.targetDisplay = 0;
        
        // IMPORTANTE: Desactivar solo el componente Camera (no el GameObject)
        // Esto permite que CinemachineBrain funcione pero no renderiza hasta activarlo
        dialogueCamera.enabled = false;
        
        // Añadir CinemachineBrain a esta cámara dedicada
        dialogueBrain = dialogueCameraObject.AddComponent<CinemachineBrain>();
        dialogueBrain.DefaultBlend.Style = CinemachineBlendDefinition.Styles.EaseInOut;
        dialogueBrain.DefaultBlend.Time = 0.8f;
        dialogueBrain.ChannelMask = (OutputChannels)1; // Canal 1 para cámaras de diálogo
        
        // Debug.Log($"[DialogueCinematicController] ✅ Cámara de diálogo creada - GameObject: {dialogueCameraObject.activeSelf}, Camera.enabled: {dialogueCamera.enabled}, Depth: {dialogueCamera.depth}, CullingMask incluye UI: {((dialogueCamera.cullingMask & (1 << LayerMask.NameToLayer("UI"))) != 0)}");
    }



    void Start()
    {
        // Inicializar el pool de cámaras
        InitializeCameraPool();

        // Buscar la cámara principal de Cinemachine usando ServiceLocator
        mainGameplayCamera = ServiceLocator.Get<CinemachineCamera>(logIfMissing: false);
        
        if (mainGameplayCamera != null)
        {
            originalGameplayCameraPriority = mainGameplayCamera.Priority.Value;
            // Debug.Log($"[DialogueCinematicController] Cámara de gameplay encontrada: {mainGameplayCamera.name} (Priority original: {originalGameplayCameraPriority})");
        }
        else
        {
            Debug.LogWarning("[DialogueCinematicController] No se encontró una CinemachineCamera de gameplay en la escena");
        }
        
        // Desactivar todas las cámaras del pool inicialmente
        DisableAllDialogueCameras();
        
        // Debug.Log("[DialogueCinematicController] ✅ Start completado - Sistema listo");
    }

    void OnDestroy()
    {
        // ✅ LIMPIEZA: Destruir todas las cámaras del pool y sus objetos temporales
        if (cameraPool != null)
        {
            foreach (var vcam in cameraPool)
            {
                if (vcam != null)
                {
                    // Destruir objetos LookAt temporales
                    if (vcam.Target.LookAtTarget != null && vcam.Target.LookAtTarget.parent == vcam.transform)
                    {
                        Destroy(vcam.Target.LookAtTarget.gameObject);
                    }
                    
                    // Destruir la cámara virtual
                    if (vcam.gameObject != null)
                    {
                        Destroy(vcam.gameObject);
                    }
                }
            }
            cameraPool.Clear();
        }
        


        // Limpiar la instancia del Singleton si es esta instancia
        if (Instance == this)
        {
            Instance = null;
            // Debug.Log("[DialogueCinematicController] Instancia del Singleton limpiada");
        }
    }

    void LateUpdate()
    {
        // CRÍTICO: Asegurar que Unity incluye esta cámara en el rendering
        // Cuando el diálogo está activo, necesitamos que Unity "vea" esta cámara
        if (forceRenderingActive && dialogueCamera != null && dialogueCamera.enabled)
        {
            // Verificar que el componente está realmente habilitado
            if (!dialogueCamera.isActiveAndEnabled)
            {
                // Si por alguna razón se desactivó, reactivarlo
                dialogueCamera.enabled = true;
                Debug.LogWarning("[DialogueCinematicController] DialogueCamera se desactivó inesperadamente - reactivando");
            }
        }

        // ── Anti-intrusión: reescaneo de bystanders + red de seguridad de intrusos en cámara ──
        if (isInCinematicMode)
        {
            _bystanderRescanTimer -= Time.deltaTime;
            if (_bystanderRescanTimer <= 0f)
            {
                _bystanderRescanTimer = bystanderRescanInterval;
                RescanAndFreezeBystanders();
            }

            _intruderCheckTimer -= Time.deltaTime;
            if (_intruderCheckTimer <= 0f)
            {
                _intruderCheckTimer = intruderCheckInterval;
                CheckAndHideCameraIntruders();
            }
        }

        // ── Breathing suave del lookAt en modo grupal ──
        // Recalcula el centro del grupo e interpola el punto de mira hacia el speaker actual.
        if (_isGroupConversation && isInCinematicMode && currentPlayer != null)
        {
            // Recalcular centro del grupo (los personajes pueden haberse movido ligeramente)
            Vector3 groupCenter = currentPlayer.position;
            int count = 1;
            if (currentNPC != null) { groupCenter += currentNPC.position; count++; }
            if (Game.NPC.PlayerParty.HasInstance)
            {
                foreach (var m in Game.NPC.PlayerParty.Instance.Members)
                {
                    if (m != null && m.IsActiveInParty)
                    { groupCenter += m.transform.position; count++; }
                }
            }
            groupCenter /= count;
            _cachedGroupCenter = groupCenter;

            // Destino del lookAt: centro del grupo con sesgo suave hacia el speaker
            Vector3 desiredLookAt = groupCenter + Vector3.up * groupLookAtHeight;
            if (currentSpeaker != null)
            {
                Vector3 toSpeaker = currentSpeaker.position - groupCenter;
                toSpeaker.y = 0f;
                if (toSpeaker.sqrMagnitude > 0.01f)
                    desiredLookAt += toSpeaker * groupSpeakerBias;
            }

            // Suavizar con SmoothDamp
            _groupLookAtTarget = Vector3.SmoothDamp(
                _groupLookAtTarget, desiredLookAt, ref _groupLookAtVelocity, groupLookAtSmoothTime);

            // Actualizar rotación de la cámara virtual activa y su lookAt target
            if (currentVirtualCamera != null)
            {
                Vector3 lookDir = _groupLookAtTarget - currentVirtualCamera.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                    currentVirtualCamera.transform.rotation = Quaternion.LookRotation(lookDir);

                if (currentVirtualCamera.Target.LookAtTarget != null)
                    currentVirtualCamera.Target.LookAtTarget.position = _groupLookAtTarget;
            }
            // Aplicar también a la cámara de diálogo directamente (por si el Brain ya procesó este frame)
            if (dialogueCamera != null && dialogueCamera.enabled)
            {
                Vector3 lookDir = _groupLookAtTarget - dialogueCamera.transform.position;
                if (lookDir.sqrMagnitude > 0.001f)
                    dialogueCamera.transform.rotation = Quaternion.LookRotation(lookDir);
            }
        }
    }

    /// <summary>
    /// Inicia el modo cinematográfico para un diálogo
        /// </summary>
        public void StartCinematic(Transform player, Transform npc, DialogueCinematicProfile profile = null, bool isGroupConversation = false)
        {
            // Si hay un apagado pendiente, cancelarlo (diálogo encadenado)
            if (isPendingEnd && pendingEndCinematicCoroutine != null)
            {
                if (showDebugInfo)
                    Debug.Log("[DialogueCinematicController] Diálogo encadenado detectado - cancelando apagado pendiente");
                
                StopCoroutine(pendingEndCinematicCoroutine);
                pendingEndCinematicCoroutine = null;
                isPendingEnd = false;
                
                // Si ya estábamos en modo cinematográfico con el mismo NPC, solo actualizar
                if (isInCinematicMode && currentNPC == npc && currentPlayer == player)
                {
                    if (showDebugInfo)
                        Debug.Log("[DialogueCinematicController] Reutilizando cinematográfica activa para diálogo encadenado");
                    
                    // Resetear índice de línea para el nuevo diálogo
                    currentLineIndex = 0;
                    nextCutAtLine = CalculateNextCutLine();
                    
                    // Aplicar shot inicial del nuevo diálogo
                    var newProfile = profile ?? defaultProfile;
                    if (newProfile != null)
                    {
                        activeProfile = newProfile;
                        ApplyShotWithContext(activeProfile.openingShot, isOpening: true);
                    }
                    
                    return;
                }
            }
            
            if (isInCinematicMode)
            {
                Debug.LogWarning("[DialogueCinematicController] Ya está en modo cinematográfico");
                return;
            }

            if (player == null || npc == null)
            {
                Debug.LogError("[DialogueCinematicController] Player o NPC es null");
                return;
            }

        currentPlayer = player;
        currentNPC = npc;
        activeProfile = profile ?? defaultProfile;
        isInCinematicMode = true;
        currentLineIndex = 0;
        nextCutAtLine = CalculateNextCutLine();
        _isGroupConversation = isGroupConversation;

        // Calcular dirección base de cámara grupal una sola vez (evita saltos al cambiar de speaker)
        if (isGroupConversation && currentNPC != null)
        {
            // Vista 3/4: perpendicular al eje player-NPC combinada con profundidad.
            // Evaluamos ambos lados (perpendicular y -perpendicular) con raycast para elegir
            // el que tenga más espacio libre (evita colocar la cámara detrás de paredes/puertas).
            Vector3 playerToNpc = currentNPC.position - currentPlayer.position;
            playerToNpc.y = 0f;
            if (playerToNpc.sqrMagnitude > 0.01f)
            {
                playerToNpc.Normalize();
                Vector3 perpendicular = Vector3.Cross(Vector3.up, playerToNpc).normalized;
                _groupCamBaseDir = PickBestGroupCameraDirection(perpendicular, playerToNpc);
            }
            else
            {
                _groupCamBaseDir = Vector3.back;
            }

            // ✅ ESCENARIO TEATRAL: recolocar a los compañeros en un semicírculo en el lado
            // OPUESTO a la cámara, ahora que ya sabemos dónde va a estar (_groupCamBaseDir).
            // Sustituye al posicionamiento genérico de dos arcos (que DialogueManager se salta
            // en diálogos grupales): aquel no conocía la cámara y podía dejar a un compañero
            // entre la cámara y el grupo, de espaldas, tapando el plano entero (bug de Eldran).
            if (Game.NPC.PlayerParty.HasInstance)
            {
                Game.NPC.PlayerParty.Instance.PositionMembersForGroupDialogue(currentNPC, _groupCamBaseDir);
            }

            // Calcular centro del grupo para orientar a los participantes
            Vector3 initGroupCenter = currentPlayer.position + currentNPC.position;
            int initCount = 2;

            var hiddenNpc = ActiveCharacterSwapper.Instance != null
                ? ActiveCharacterSwapper.Instance.HiddenNpc
                : null;

            // Asegurar que todos los party members tengan sus renderers activos al inicio
            // (pueden haber quedado desactivados por diálogos previos donde eran currentNPC)
            if (Game.NPC.PlayerParty.HasInstance)
            {
                foreach (var m in Game.NPC.PlayerParty.Instance.Members)
                {
                    if (m == null || m == hiddenNpc) continue;
                    foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                        if (r != null) r.enabled = true;
                    if (m.IsActiveInParty)
                    { initGroupCenter += m.transform.position; initCount++; }
                }
            }
            initGroupCenter /= initCount;
            _cachedGroupCenter = initGroupCenter;
            _groupLookAtTarget = initGroupCenter + Vector3.up * groupLookAtHeight;
            _groupLookAtVelocity = Vector3.zero;

            // Rotar al NPC para que mire hacia el centro del grupo (evita que esté de espaldas)
            Vector3 npcToCenter = initGroupCenter - currentNPC.position;
            npcToCenter.y = 0f;
            if (npcToCenter.sqrMagnitude > 0.01f)
                currentNPC.rotation = Quaternion.LookRotation(npcToCenter);

            // Rotar al player hacia el centro del grupo
            Vector3 playerToCenter = initGroupCenter - currentPlayer.position;
            playerToCenter.y = 0f;
            if (playerToCenter.sqrMagnitude > 0.01f)
                currentPlayer.rotation = Quaternion.LookRotation(playerToCenter);

            // Rotar a los party members para que miren hacia el centro del grupo
            if (Game.NPC.PlayerParty.HasInstance)
            {
                foreach (var m in Game.NPC.PlayerParty.Instance.Members)
                {
                    if (m == null || !m.IsActiveInParty || m == hiddenNpc) continue;
                    Vector3 memberToCenter = initGroupCenter - m.transform.position;
                    memberToCenter.y = 0f;
                    if (memberToCenter.sqrMagnitude > 0.01f)
                        m.transform.rotation = Quaternion.LookRotation(memberToCenter);
                }
            }
        }

        // ✅ Reiniciar sistema de tracking de speakers
        currentSpeakerId = null;
        currentSpeaker = null;
        speakerCache.Clear();
        
        // Pre-cachear speakers conocidos
        speakerCache["Player"] = currentPlayer;
        speakerCache["MainNPC"] = currentNPC;
        if (!string.IsNullOrEmpty(npc.name))
        {
            speakerCache[npc.name] = currentNPC;
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueCinematicController] Iniciando cinematográfica con {npc.name}");

        // PASO 0: Ocultar el HUD con fade suave
        HideHUD();

        // PASO 1: Capturar referencia a la cámara principal de Unity ANTES de cualquier cambio
        // CRÍTICO: Debe hacerse antes de cambiar tags o activar otras cámaras
        mainUnityCamera = Camera.main;
        
        // PASO 2: Desactivar la cámara principal de Unity PRIMERO (antes de activar la nuestra)
        // Esto evita conflictos de rendering en URP
        if (mainUnityCamera != null)
        {
            mainUnityCameraWasEnabled = mainUnityCamera.enabled;
            mainUnityCamera.enabled = false;
            // Debug.Log($"[DialogueCinematicController] Cámara principal de Unity ({mainUnityCamera.name}) DESACTIVADA temporalmente para evitar conflictos URP");
        }

        // PASO 3: Desactivar la cámara virtual de gameplay de Cinemachine
        if (mainGameplayCamera != null)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Desactivando cámara de gameplay Cinemachine (Priority: {mainGameplayCamera.Priority.Value} → 0)");
            
            mainGameplayCamera.Priority.Value = 0;
        }

        // PASO 4: Ahora activar la cámara de diálogo dedicada
        if (dialogueCamera != null)
        {
            bool wasEnabled = dialogueCamera.enabled;
            dialogueCamera.enabled = true;

            // Sincronizar clearFlags con el entorno actual (interior → solid color/skybox interior, exterior → skybox)
            SyncClearFlagsToEnvironment();

            // Activar rendering forzado
            forceRenderingActive = true;

            // Debug.Log($"[DialogueCinematicController] Componente Camera activado (era: {wasEnabled}, ahora: {dialogueCamera.enabled}, isActiveAndEnabled: {dialogueCamera.isActiveAndEnabled})");
            // Debug.Log($"[DialogueCinematicController] Camera details - Depth: {dialogueCamera.depth}, ClearFlags: {dialogueCamera.clearFlags}, CullingMask: {dialogueCamera.cullingMask}, TargetTexture: {(dialogueCamera.targetTexture == null ? "NULL (pantalla)" : dialogueCamera.targetTexture.name)}, Tag: {dialogueCameraObject.tag}");
        }
        else
        {
            Debug.LogError("[DialogueCinematicController] ❌ dialogueCamera es NULL - no se puede activar!");
        }

        // PASO 4.5: Congelar de inmediato a los NPCs ambientales cercanos que no participan
        // en el diálogo, para que el plano de apertura no arranque con alguien cruzando el encuadre.
        _bystanderRescanTimer = bystanderRescanInterval;
        _intruderCheckTimer = intruderCheckInterval;
        RescanAndFreezeBystanders();

        // PASO 5: Activar el plano de apertura
        ApplyShotWithContext(activeProfile.openingShot, true);
    }

        /// <summary>
        /// Finaliza el modo cinematográfico con delay para permitir encadenamiento
        /// </summary>
        public void EndCinematic()
        {
            if (!isInCinematicMode) return;

            // Iniciar el proceso de apagado con delay para detectar encadenamiento
            if (pendingEndCinematicCoroutine != null)
            {
                StopCoroutine(pendingEndCinematicCoroutine);
            }
            
            pendingEndCinematicCoroutine = StartCoroutine(EndCinematicDelayed());
        }
        
        /// <summary>
        /// Corrutina que espera un momento antes de apagar realmente la cinematográfica
        /// Permite detectar si viene otro diálogo encadenado
        /// </summary>
        private System.Collections.IEnumerator EndCinematicDelayed()
        {
            isPendingEnd = true;
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Iniciando apagado con delay de {chainDialogueGracePeriod}s");
            
            // Esperar el período de gracia
            yield return new WaitForSeconds(chainDialogueGracePeriod);
            
            // Si llegamos aquí, no hubo encadenamiento - apagar realmente
            isPendingEnd = false;
            pendingEndCinematicCoroutine = null;
            
            if (showDebugInfo)
                Debug.Log("[DialogueCinematicController] Finalizando cinematográfica (sin encadenamiento detectado)");

            EndCinematicImmediate();
        }
        
        /// <summary>
        /// Apaga inmediatamente el modo cinematográfico (sin delay)
        /// </summary>
        private void EndCinematicImmediate()
        {
            if (!isInCinematicMode) return;

            if (showDebugInfo)
                Debug.Log("[DialogueCinematicController] Apagando cinematográfica inmediatamente");

        // Desactivar todas las cámaras virtuales del pool
        DisableAllDialogueCameras();

        // Desactivar la cámara de diálogo dedicada (solo el componente Camera)
        if (dialogueCamera != null)
        {
            bool wasEnabled = dialogueCamera.enabled;
            dialogueCamera.enabled = false;
            
            // Ya no cambiamos el tag MainCamera, así que no hay nada que restaurar
            
            // Desactivar rendering forzado
            forceRenderingActive = false;
            
            // Debug.Log($"[DialogueCinematicController] Componente Camera desactivado (era: {wasEnabled}, ahora: {dialogueCamera.enabled})");
        }

        // Reactivar la cámara de gameplay de Cinemachine (si existe)
        if (mainGameplayCamera != null)
        {
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Restaurando cámara de gameplay Cinemachine (Priority: {originalGameplayCameraPriority})");
            
            mainGameplayCamera.Priority.Value = originalGameplayCameraPriority;
            
            // Verificar que se aplicó correctamente
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] ✓ Cámara restaurada. Priority actual: {mainGameplayCamera.Priority.Value}");
        }

        // CRÍTICO PARA URP: Reactivar la cámara principal de Unity
        if (mainUnityCamera != null && mainUnityCameraWasEnabled)
        {
            mainUnityCamera.enabled = true;
            // Debug.Log($"[DialogueCinematicController] Cámara principal de Unity REACTIVADA");
        }
        mainUnityCamera = null;
        
        // Restaurar visibilidad del player y NPC
        ShowPlayer();
        ShowNPC();
        
        // Limpiar rotaciones suaves (player y NPC) si estaban en progreso
        if (_playerRotationCoroutine != null)
        {
            StopCoroutine(_playerRotationCoroutine);
            _playerRotationCoroutine = null;
        }
        if (_npcRotationCoroutine != null)
        {
            StopCoroutine(_npcRotationCoroutine);
            _npcRotationCoroutine = null;
        }

        // Apagar el head-look de todos los participantes (fade out suave hacia la pose animada)
        ClearAllHeadLooks();

        // Limpiar efectos cinematográficos si estaban activos
        if (_effectActive)
        {
            _effectActive = false;
            if (_effectsCoroutine != null)
            {
                StopCoroutine(_effectsCoroutine);
                _effectsCoroutine = null;
            }
        }
        // Restaurar party members ocultos
        ShowAllPartyMembers();

        // Liberar bystanders congelados y restaurar cualquier intruso oculto por la red de seguridad
        UnfreezeBystanders();
        RestoreAllHiddenIntruders();

        // Mostrar el HUD con fade suave
        ShowHUD();

        isInCinematicMode = false;
        currentPlayer = null;
        currentNPC = null;
        currentVirtualCamera = null;
        currentLineIndex = 0;
    }

    /// <summary>
    /// Oculta visualmente al player para que no aparezca en los planos cinematográficos
    /// </summary>
    private void HidePlayer()
    {
        if (currentPlayer == null) return;
        
        // Solo guardar los renderers si aún no están guardados (primera vez que ocultamos)
        if (playerRenderers == null)
        {
            // Obtener todos los renderers del player (incluyendo hijos)
            playerRenderers = currentPlayer.GetComponentsInChildren<Renderer>(true);
            playerRenderersWereEnabled = new bool[playerRenderers.Length];
            
            // Guardar estado original
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                playerRenderersWereEnabled[i] = playerRenderers[i].enabled;
            }
        }
        
        // Desactivar todos los renderers
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            if (playerRenderers[i] != null)
            {
                playerRenderers[i].enabled = false;
            }
        }
        
        if (showDebugInfo)
            Debug.Log($"[DialogueCinematicController] Player ocultado - {playerRenderers.Length} renderers desactivados");
    }
    
    /// <summary>
    /// Restaura la visibilidad del player
    /// </summary>
    private void ShowPlayer()
    {
        // Si tenemos renderers guardados, restaurarlos
        if (playerRenderers != null)
        {
            // Restaurar estado original de cada renderer
            for (int i = 0; i < playerRenderers.Length; i++)
            {
                if (playerRenderers[i] != null)
                {
                    playerRenderers[i].enabled = playerRenderersWereEnabled[i];
                }
            }
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Player restaurado - {playerRenderers.Length} renderers reactivados");
            
            // Limpiar referencias
            playerRenderers = null;
            playerRenderersWereEnabled = null;
        }
        // Si no hay renderers guardados pero hay currentPlayer, activar todos sus renderers
        else if (currentPlayer != null)
        {
            Renderer[] renderers = currentPlayer.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Player restaurado (forzado) - {renderers.Length} renderers activados");
        }
    }
    
    // ✅ Variables para guardar el estado del NPC
    private Renderer[] npcRenderers;
    private bool[] npcRenderersWereEnabled;
    
    /// <summary>
    /// Oculta visualmente al NPC para que no aparezca en los planos del Player/Party
    /// </summary>
    private void HideNPC()
    {
        if (currentNPC == null) return;
        
        // Solo guardar los renderers si aún no están guardados
        if (npcRenderers == null)
        {
            // Obtener todos los renderers del NPC (incluyendo hijos)
            npcRenderers = currentNPC.GetComponentsInChildren<Renderer>(true);
            npcRenderersWereEnabled = new bool[npcRenderers.Length];
            
            // Guardar estado original
            for (int i = 0; i < npcRenderers.Length; i++)
            {
                npcRenderersWereEnabled[i] = npcRenderers[i].enabled;
            }
        }
        
        // Desactivar todos los renderers
        for (int i = 0; i < npcRenderers.Length; i++)
        {
            if (npcRenderers[i] != null)
            {
                npcRenderers[i].enabled = false;
            }
        }
        
        if (showDebugInfo)
            Debug.Log($"[DialogueCinematicController] NPC ocultado - {npcRenderers.Length} renderers desactivados");
    }
    
    /// <summary>
    /// Restaura la visibilidad del NPC
    /// </summary>
    private void ShowNPC()
    {
        // Si tenemos renderers guardados, restaurarlos
        if (npcRenderers != null)
        {
            // Restaurar estado original de cada renderer
            for (int i = 0; i < npcRenderers.Length; i++)
            {
                if (npcRenderers[i] != null)
                {
                    npcRenderers[i].enabled = npcRenderersWereEnabled[i];
                }
            }
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] NPC restaurado - {npcRenderers.Length} renderers reactivados");
            
            // Limpiar referencias
            npcRenderers = null;
            npcRenderersWereEnabled = null;
        }
        // Si no hay renderers guardados pero hay currentNPC, activar todos sus renderers
        else if (currentNPC != null)
        {
            Renderer[] renderers = currentNPC.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] NPC restaurado (forzado) - {renderers.Length} renderers activados");
        }
    }

    /// <summary>
    /// Oculta los renderers de party members que NO son el speaker actual ni el player ni el NPC principal.
    /// Evita que personajes cercanos bloqueen la cámara en modo individual.
    /// </summary>
    private void HideNonSpeakingPartyMembers(Transform speaker)
    {
        // Restaurar los que estaban ocultos antes de ocultar nuevos
        ShowAllPartyMembers();

        if (!Game.NPC.PlayerParty.HasInstance) return;

        foreach (var m in Game.NPC.PlayerParty.Instance.Members)
        {
            if (m == null || !m.IsActiveInParty) continue;
            // No ocultar al speaker, al player ni al NPC principal
            if (m.transform == speaker) continue;
            if (m.transform == currentPlayer) continue;
            if (m.transform == currentNPC) continue;

            var renderers = m.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                _hiddenPartyRenderers.Add(r);
                _hiddenPartyRenderersOrigState.Add(r.enabled);
                r.enabled = false;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (showDebugInfo && _hiddenPartyRenderers.Count > 0)
            Debug.Log($"[DialogueCinematicController] Ocultos {_hiddenPartyRenderers.Count} renderers de party members no-hablantes");
#endif
    }

    /// <summary>
    /// Restaura la visibilidad de todos los party members ocultos por HideNonSpeakingPartyMembers.
    /// </summary>
    private void ShowAllPartyMembers()
    {
        if (_hiddenPartyRenderers.Count == 0) return;

        for (int i = 0; i < _hiddenPartyRenderers.Count; i++)
        {
            if (_hiddenPartyRenderers[i] != null)
                _hiddenPartyRenderers[i].enabled = _hiddenPartyRenderersOrigState[i];
        }
        _hiddenPartyRenderers.Clear();
        _hiddenPartyRenderersOrigState.Clear();
    }

    // ═══════════════════════════════════════════════════════════
    // ANTI-INTRUSIÓN: NPCs ambientales cruzándose en la cámara
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Capa preventiva: detiene (HardStop sobre el NavMeshAgent, sin tocar su FSM) a los NPCs
    /// ambientales dentro del radio de la conversación que no son el NPC principal, el player ni
    /// party members. No interfiere con NPCs ya ocupados en su propio combate/cinemática/interacción.
    /// Se llama al iniciar el diálogo y se refresca periódicamente desde LateUpdate.
    /// </summary>
    private void RescanAndFreezeBystanders()
    {
        if (currentPlayer == null) return;

        Vector3 center = currentNPC != null
            ? (currentPlayer.position + currentNPC.position) * 0.5f
            : currentPlayer.position;

        NPCAmbientRegistry.GetActiveNPCsInRadius(center, bystanderFreezeRadius, _nearbyBystanderBuffer);

        for (int i = 0; i < _nearbyBystanderBuffer.Count; i++)
        {
            var npc = _nearbyBystanderBuffer[i];
            if (npc == null) continue;
            if (npc.transform == currentNPC || npc.transform == currentPlayer) continue;
            if (IsPartyMember(npc.transform)) continue;

            var ctx = npc.Context;
            if (ctx != null && (ctx.IsInCombat || ctx.IsInCinematic || ctx.IsInteracting || ctx.WasDefeatedInCombat))
                continue;

            if (npc.Agent != null)
                Game.NPC.Common.NavMeshAgentUtility.HardStop(npc.Agent);

            if (_frozenBystanders.Add(npc))
                _bystanderRootTransforms.Add(npc.transform);
        }
    }

    /// <summary>
    /// Libera a todos los bystanders congelados. No hace falta "reactivarlos" explícitamente:
    /// HardStop solo tocó el NavMeshAgent, nunca su FSM, así que su propio estado (Wander/Idle)
    /// retoma la navegación con normalidad en cuanto vuelve a evaluarse.
    /// </summary>
    private void UnfreezeBystanders()
    {
        _frozenBystanders.Clear();
        _bystanderRootTransforms.Clear();
    }

    /// <summary>
    /// Capa reactiva (red de seguridad): comprueba si CUALQUIER collider —NPC ambiental, puerta,
    /// mobiliario animado, lo que sea— se ha metido entre la cámara y la cara de quien habla, y si
    /// es así oculta sus renderers hasta que deje de bloquear. Garantiza que, aunque el congelado
    /// preventivo de bystanders (que solo cubre NPCs) falle, llegue tarde, o directamente no
    /// aplique porque el intruso no es un NPC, nunca se vea a alguien tapando la cámara.
    /// Antes de Agosto 2026 esto solo miraba <see cref="_bystanderRootTransforms"/> y ni siquiera
    /// se ejecutaba si no había NPCs ambientales fichados cerca — ver comentario de contexto más
    /// arriba para el bug real que causaba (puertas cruzándose en cámara sin ser detectadas).
    /// </summary>
    private void CheckAndHideCameraIntruders()
    {
        if (currentPlayer == null)
        {
            // Nada que vigilar: si había intrusos ocultos de un chequeo anterior, restaurarlos.
            if (_hiddenIntruders.Count > 0) RestoreAllHiddenIntruders();
            return;
        }

        Transform focusTarget = currentSpeaker != null ? currentSpeaker : currentNPC;
        if (focusTarget == null) return;

        Camera activeCam = (dialogueCamera != null && dialogueCamera.enabled) ? dialogueCamera : null;
        if (activeCam == null) return;

        Vector3 camPos = activeCam.transform.position;
        Vector3 headPos = focusTarget.position + Vector3.up * 1.5f;
        Vector3 toHead = headPos - camPos;
        float dist = toHead.magnitude;
        if (dist < 0.5f) return;

        Vector3 dir = toHead / dist;
        float castDist = Mathf.Max(0.1f, dist - 0.3f); // no llegar hasta la propia cara del speaker

        int hitCount = Physics.SphereCastNonAlloc(camPos, intruderProbeRadius, dir, _intruderHitsBuffer,
            castDist, _camObstructionMask, QueryTriggerInteraction.Ignore);

        _frameIntruders.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            var col = _intruderHitsBuffer[i].collider;
            if (col == null) continue;

            Transform root = col.transform.root;
            if (root == focusTarget || root == currentPlayer || root == currentNPC) continue;
            if (IsPartyMember(root)) continue; // ya gestionados por HideNonSpeakingPartyMembers

            // Cualquier otro collider en la línea cámara→cara es un intruso: sea un NPC ambiental
            // ya fichado o no (puerta, cofre, mobiliario...), se oculta igual. CalculateCameraPosition
            // ya garantizó que no había nada aquí al fijar el plano, así que si algo aparece ahora es,
            // por definición, un objeto que se ha movido durante la línea — el caso que se nos escapaba.
            _frameIntruders.Add(root);
        }

        foreach (var intruder in _frameIntruders)
        {
            if (_hiddenIntruders.Add(intruder))
            {
                HideIntruderRenderers(intruder);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] 🙈 Intruso oculto (se cruzó en cámara): {intruder.name}");
#endif
            }
        }

        if (_hiddenIntruders.Count > 0)
        {
            _intrudersToRestoreBuffer.Clear();
            foreach (var hidden in _hiddenIntruders)
                if (!_frameIntruders.Contains(hidden))
                    _intrudersToRestoreBuffer.Add(hidden);

            for (int i = 0; i < _intrudersToRestoreBuffer.Count; i++)
            {
                RestoreIntruderRenderers(_intrudersToRestoreBuffer[i]);
                _hiddenIntruders.Remove(_intrudersToRestoreBuffer[i]);
            }
        }
    }

    private void HideIntruderRenderers(Transform root)
    {
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || _hiddenIntruderOrigState.ContainsKey(r)) continue;
            _hiddenIntruderOrigState[r] = r.enabled;
            r.enabled = false;
        }
    }

    private void RestoreIntruderRenderers(Transform root)
    {
        if (root == null) return;
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r != null && _hiddenIntruderOrigState.TryGetValue(r, out bool orig))
            {
                r.enabled = orig;
                _hiddenIntruderOrigState.Remove(r);
            }
        }
    }

    /// <summary>
    /// Restaura todos los intrusos ocultos de golpe (fin del diálogo, o el root de alguno se
    /// destruyó/desactivó y ya no se puede resolver por transform).
    /// </summary>
    private void RestoreAllHiddenIntruders()
    {
        foreach (var kvp in _hiddenIntruderOrigState)
        {
            if (kvp.Key != null) kvp.Key.enabled = kvp.Value;
        }
        _hiddenIntruderOrigState.Clear();
        _hiddenIntruders.Clear();
    }

        /// <summary>
        /// Notifica que ha avanzado una línea de diálogo
        /// </summary>
        public void OnDialogueLineAdvanced(int lineIndex, int totalLines, DialogueLine currentLine)
        {
            if (!isInCinematicMode) return;

            currentLineIndex = lineIndex;

            // Pre-resolver el Transform del speaker para que currentSpeaker esté listo antes de
            // los efectos y del breathing grupal. NO actualizar currentSpeakerId aquí.
            {
                string preSpeakerId = DetermineSpeakerId(currentLine);
                if (preSpeakerId != currentSpeakerId)
                {
                    Transform preSpeakerTransform = FindSpeakerTransform(preSpeakerId, currentLine);
                    if (preSpeakerTransform != null)
                        currentSpeaker = preSpeakerTransform;
                }
            }

            // ── MIRADAS EN MODO GRUPAL ──
            // Los personajes que escuchan giran hacia quien habla (y el que habla, hacia su
            // interlocutor natural), como en una conversación real.
            if (_isGroupConversation)
                UpdateGroupFacing();

            // ── EFECTOS CINEMATOGRÁFICOS (CloseUp siempre activo) ──
            // El sistema de cortes de cámara por modo grupal/1:1 fue retirado a favor de esto
            // (ver historial: "CloseUp siempre activo"); el código de cortes quedaba inalcanzable.
            ApplyLineEffect(currentSpeaker);
        }

        /// <summary>
        /// Verifica si cambió el speaker y ajusta la cámara en consecuencia
        /// </summary>
        /// <returns>True si cambió el speaker y se ajustó la cámara</returns>
        private bool CheckAndAdjustCameraForSpeaker(DialogueLine line, int lineIndex, int totalLines)
        {
            string newSpeakerId = DetermineSpeakerId(line);

            if (newSpeakerId == currentSpeakerId)
                return false;

            Transform newSpeakerTransform = FindSpeakerTransform(newSpeakerId, line);

            if (newSpeakerTransform == null)
                newSpeakerTransform = currentNPC;

            currentSpeakerId = newSpeakerId;
            currentSpeaker = newSpeakerTransform;

            ApplyShotForSpeaker(newSpeakerTransform, line, lineIndex, totalLines);
            return true;
        }
        
        /// <summary>
        /// Determina el ID único del speaker basándose en la línea de diálogo
        /// </summary>
        private string DetermineSpeakerId(DialogueLine line)
        {
            // Si tiene speakerNameId, usarlo
            if (!string.IsNullOrEmpty(line.speakerNameId))
                return line.speakerNameId;
            
            // Si no, determinar por isPlayerSpeaking
            if (line.isPlayerSpeaking)
                return "Player";
            
            // Por defecto, asumir que es el NPC principal
            return "MainNPC";
        }
        
        /// <summary>
        /// Encuentra el Transform del speaker basándose en su ID.
        /// Prioridad: cache → "Player" literal → NPC principal → party members → NPCs en escena → fallback por isPlayerSpeaking.
        /// </summary>
        private Transform FindSpeakerTransform(string speakerId, DialogueLine line)
        {
            if (speakerCache.ContainsKey(speakerId))
                return speakerCache[speakerId];

            Transform speakerTransform = null;

            // Solo si el ID es literalmente "Player" (sin speakerNameId específico)
            if (speakerId == "Player")
            {
                speakerTransform = currentPlayer;
            }
            // NPC principal del diálogo
            else if (speakerId == "MainNPC" || speakerId == currentNPC.name)
            {
                speakerTransform = currentNPC;
            }
            else
            {
                // Buscar en party members por dialogueCharacterId / persistenceId / nombre
                if (Game.NPC.PlayerParty.HasInstance)
                {
                    foreach (var m in Game.NPC.PlayerParty.Instance.Members)
                    {
                        if (m == null || m.NPCManager == null) continue;
                        var mgr = m.NPCManager;
                        if ((!string.IsNullOrEmpty(mgr.DialogueCharacterId) && mgr.DialogueCharacterId == speakerId)
                            || mgr.PersistenceId == speakerId
                            || m.gameObject.name == speakerId)
                        {
                            speakerTransform = m.transform;
                            break;
                        }
                    }
                }

                // Buscar en NPCs de la escena (solo en modo grupal para evitar FindObjectsByType en 1:1)
                if (speakerTransform == null && _isGroupConversation)
                {
                    var allNPCs = GameObject.FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>();
                    foreach (var npc in allNPCs)
                    {
                        if ((!string.IsNullOrEmpty(npc.DialogueCharacterId) && npc.DialogueCharacterId == speakerId)
                            || npc.PersistenceId == speakerId
                            || npc.gameObject.name == speakerId)
                        {
                            speakerTransform = npc.transform;
                            break;
                        }
                    }
                }

                // Fallback: si no se encontró el personaje, usar isPlayerSpeaking para decidir lado
                if (speakerTransform == null)
                    speakerTransform = line.isPlayerSpeaking ? currentPlayer : currentNPC;
            }

            if (speakerTransform != null)
                speakerCache[speakerId] = speakerTransform;

            return speakerTransform;
        }
        
        private void ApplyShotForSpeaker(Transform speaker, DialogueLine line, int lineIndex, int totalLines)
        {
            // En modo 1:1, si habla un party member ocultamos el resto para evitar que
            // aparezcan en medio del encuadre. Al volver al NPC/player los restauramos.
            if (!_isGroupConversation)
            {
                if (IsPartyMember(speaker))
                    HideNonSpeakingPartyMembers(speaker);
                else
                    ShowAllPartyMembers();
            }

            CinematicCameraShot appropriateShot = null;

            if (activeProfile != null && activeProfile.npcShots != null && activeProfile.npcShots.Length > 0)
            {
                int shotIndex = lineIndex % activeProfile.npcShots.Length;
                appropriateShot = activeProfile.npcShots[shotIndex];
            }

            if (appropriateShot == null && activeProfile != null)
                appropriateShot = activeProfile.openingShot;

            if (appropriateShot != null)
                ApplyShotWithContext(appropriateShot, false, speaker, forceCut: true);
        }

        // ═══════════════════════════════════════════════════════════
        // EFECTOS CINEMATOGRÁFICOS POR LÍNEA
        // ═══════════════════════════════════════════════════════════

        private static readonly CinematicCameraShot _effectCloseUpShot = new CinematicCameraShot
        {
            shotType = DialogueShotType.CloseUpNPC,
            minimumDuration = 0f
        };

        private void ApplyLineEffect(Transform speaker)
        {
            if (speaker == null) return;

            _effectActive = true;

            if (_isGroupConversation)
            {
                ShowNPC();
                ShowPlayer();
            }
            else
            {
                bool speakerIsPlayerOrParty = (speaker == currentPlayer) || IsPartyMember(speaker);

                if (speakerIsPlayerOrParty)
                {
                    HideNPC();
                    ShowPlayer();
                    MakeSpeakerLookAtNPC(speaker);
                }
                else if (speaker == currentNPC)
                {
                    ShowNPC();
                    HidePlayer();
                }
                else
                {
                    ShowNPC();
                    ShowPlayer();
                }

                HideNonSpeakingPartyMembers(speaker);
            }

            ApplyShot(_effectCloseUpShot, speaker, forceCut: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Efecto CloseUp → speaker: {speaker.name}");
#endif
        }

        private void ClearLineEffect()
        {
            _effectActive = false;

            // Restaurar party members ocultos por el efecto
            ShowAllPartyMembers();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showDebugInfo)
                Debug.Log("[DialogueCinematicController] Efecto limpiado, restaurando plano normal");
#endif
        }

        /// <summary>
        /// Aplica un plano cinematográfico específico
        /// </summary>
        private void ApplyShot(CinematicCameraShot shot, Transform target, bool forceCut = false)
        {
            if (shot == null || target == null) return;

        // Obtener una cámara virtual del pool
        CinemachineCamera vcam = GetPooledCamera();

        // Activar el GameObject de la cámara
        vcam.gameObject.SetActive(true);

        // Configurar posición y rotación
        ConfigureCameraForShot(vcam, shot, target, forceCut);

        // Activar esta cámara con prioridad alta
        vcam.Priority.Value = 100;

            // Desactivar la anterior
            if (currentVirtualCamera != null && currentVirtualCamera != vcam)
            {
                currentVirtualCamera.Priority.Value = 0;
            }

            currentVirtualCamera = vcam;

            if (showDebugInfo)
            {
                Debug.Log($"[DialogueCinematicController] Aplicando plano: {shot.shotType} hacia {target.name}");
            }
        }

        /// <summary>
        /// Configura una cámara virtual según el plano especificado
        /// </summary>
        private void ConfigureCameraForShot(CinemachineCamera vcam, CinematicCameraShot shot, Transform target, bool forceCut = false)
        {
            // Calcular posición base según el tipo de plano
            Vector3 position = CalculateCameraPosition(shot, target);

            // En modo grupal: usar el lookAt interpolado suavemente (breathing hacia el speaker)
            Vector3 lookAtPos;
            if (_isGroupConversation && currentPlayer != null)
            {
                lookAtPos = _groupLookAtTarget;
            }
            else
            {
                lookAtPos = target.position + shot.LookAtOffset;
                if (shot.shotType == DialogueShotType.OverShoulderPlayer ||
                    shot.shotType == DialogueShotType.OverShoulderNPC)
                {
                    lookAtPos.y -= 0.3f;
                }
            }

            // Aplicar posición
            vcam.transform.position = position;

            // Configurar tracking para seguir al objetivo
            vcam.Target.TrackingTarget = target;
            vcam.Target.CustomLookAtTarget = true;
            
            // Crear un objeto temporal para el look-at si es necesario
            Transform lookAtTarget = vcam.Target.LookAtTarget;
            if (lookAtTarget == null)
            {
                GameObject lookAtObj = new GameObject($"LookAt_{vcam.name}");
                lookAtObj.transform.SetParent(vcam.transform);
                lookAtTarget = lookAtObj.transform;
                vcam.Target.LookAtTarget = lookAtTarget;
            }
            lookAtTarget.position = lookAtPos;

            // ✅ CORREGIDO: Calcular rotación usando el camino más corto
            // Esto evita rotaciones de 360° cuando el NPC está en diagonal
            Vector3 lookDirection = (lookAtPos - position).normalized;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                
                // Si la cámara anterior tiene una rotación, usar Slerp para el camino más corto
                // Quaternion.Slerp automáticamente toma el camino más corto
                vcam.transform.rotation = targetRotation;
            }

            // Aplicar ángulos adicionales de forma segura
            if (Mathf.Abs(shot.VerticalAngle) > 0.01f)
            {
                vcam.transform.Rotate(Vector3.right, shot.VerticalAngle, Space.Self);
            }
            if (Mathf.Abs(shot.DutchAngle) > 0.01f)
            {
                vcam.transform.Rotate(Vector3.forward, shot.DutchAngle, Space.Self);
            }

            // Configurar lens (FOV): en modo grupal usar FOV propio más abierto
            vcam.Lens.FieldOfView = _isGroupConversation ? groupFieldOfView : shot.FieldOfView;

            // Configurar blend time en el brain de diálogo
            // IMPORTANTE: Detectar si el cambio de ángulo es muy grande (>90°) para usar corte
            if (dialogueBrain != null)
            {
                float angleChange = 0f;
                if (currentVirtualCamera != null && currentVirtualCamera != vcam)
                {
                    // Calcular diferencia angular entre la cámara anterior y la nueva
                    angleChange = Quaternion.Angle(currentVirtualCamera.transform.rotation, vcam.transform.rotation);
                }
                
                // Corte instantáneo si: (a) forzado por cambio de speaker, (b) ángulo > 90°
                if (forceCut || angleChange > 90f)
                {
                    dialogueBrain.DefaultBlend.Time = 0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (showDebugInfo)
                        Debug.Log($"[DialogueCinematicController] Corte instantáneo (forceCut={forceCut}, ángulo={angleChange:F1}°)");
#endif
                }
                else
                {
                    dialogueBrain.DefaultBlend.Time = activeProfile.blendDuration;
                }
            }
            
            // Asegurar que las cámaras virtuales estén en el canal correcto
            vcam.OutputChannel = (OutputChannels)1; // Canal 1 para diálogos
        }

        /// <summary>
        /// Calcula la posición de la cámara según el tipo de plano
        /// </summary>
        private Vector3 CalculateCameraPosition(CinematicCameraShot shot, Transform target)
        {
            // ── MODO CONVERSACIÓN GRUPAL ──
            // Cámara estable en ángulo 3/4 que encuadra a todo el grupo,
            // con ligero sesgo hacia el speaker actual.
            if (_isGroupConversation && target != null)
            {
                // Centro del grupo incluyendo party members si están disponibles
                Vector3 groupCenter = currentPlayer.position;
                int participantCount = 1;
                if (currentNPC != null) { groupCenter += currentNPC.position; participantCount++; }
                var party = Game.NPC.PlayerParty.HasInstance ? Game.NPC.PlayerParty.Instance : null;
                if (party != null)
                {
                    foreach (var m in party.Members)
                    {
                        if (m != null && m.IsActiveInParty)
                        { groupCenter += m.transform.position; participantCount++; }
                    }
                }
                groupCenter /= participantCount;
                _cachedGroupCenter = groupCenter;

                // Dirección base: fija desde el inicio del diálogo.
                // En modo grupal la posición de la cámara es completamente estable (sin sesgo por speaker).
                // El efecto de "saber quién habla" se consigue con el breathing del lookAt en LateUpdate.
                Vector3 baseDir = _groupCamBaseDir;

                float rad = groupElevationAngle * Mathf.Deg2Rad;
                float horizontalDist = groupCameraDistance * Mathf.Cos(rad);
                float height         = groupCameraDistance * Mathf.Sin(rad);

                // Punto de origen del raycast: centro del grupo a la altura de las cabezas
                Vector3 rayOrigin = groupCenter + Vector3.up * groupLookAtHeight;
                Vector3 horizontalDir = new Vector3(baseDir.x, 0f, baseDir.z).normalized;

                // SphereCast para verificar que no hay obstrucciones entre el grupo y la cámara
                // (ignorando personajes: comparten capa Default pero no son paredes)
                float usableHorizontalDist = horizontalDist;
                float freeDist = GroupCameraFreeDistance(rayOrigin, horizontalDir, horizontalDist);
                if (freeDist < horizontalDist)
                {
                    // Nunca colocar la cámara más allá de la obstrucción
                    usableHorizontalDist = freeDist - 0.5f;
                    if (usableHorizontalDist < groupMinClearance)
                    {
                        // Pared demasiado cerca: compensar subiendo la cámara para ver por encima
                        height = Mathf.Max(height, groupCameraDistance * 0.8f);
                        usableHorizontalDist = Mathf.Max(usableHorizontalDist, 1.5f);
                    }
                }

                Vector3 groupCamPos = groupCenter + baseDir * usableHorizontalDist;
                groupCamPos.y = groupCenter.y + height;
                return groupCamPos;
            }

            Vector3 basePos = target.position;

            // IMPORTANTE: Distancia mínima entre personajes para cálculos de cámara
            // Esto evita problemas cuando el player y NPC están muy juntos
            const float MIN_CHARACTER_DISTANCE = 2.0f;

            // Calcular distancia real entre personajes
            float actualDistance = Vector3.Distance(
                new Vector3(currentPlayer.position.x, 0, currentPlayer.position.z),
                new Vector3(currentNPC.position.x, 0, currentNPC.position.z)
            );

            // Factor de escala para cuando están muy cerca
            float distanceFactor = Mathf.Max(1f, MIN_CHARACTER_DISTANCE / Mathf.Max(actualDistance, 0.1f));
            
            // Calcular dirección desde el otro personaje hacia el target
            // Esto nos da la dirección "frontal" del target respecto al otro personaje
            Vector3 fromOther = Vector3.zero;

            if (target == currentPlayer && currentNPC != null)
            {
                // Dirección desde NPC hacia Player (frente del player respecto al NPC)
                fromOther = (target.position - currentNPC.position).normalized;
            }
            else if (target == currentNPC && currentPlayer != null)
            {
                // Dirección desde Player hacia NPC (frente del NPC respecto al Player)
                fromOther = (target.position - currentPlayer.position).normalized;
            }
            else if (IsPartyMember(target) && currentNPC != null)
            {
                // ✅ NUEVO: Para party members (como Estela), usar la dirección desde el NPC hacia el party member
                // Esto asegura que la cámara vea la CARA del party member (que mira al NPC)
                fromOther = (target.position - currentNPC.position).normalized;
                
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] 🎬 Party member detectado: calculando dirección desde {currentNPC.name} hacia {target.name}");
            }
            else
            {
                // Fallback: usar la dirección forward del target
                fromOther = target.forward;
            }

            fromOther.y = 0; // Mantener en plano horizontal
            if (fromOther.sqrMagnitude < 0.01f)
            {
                // Si están exactamente en el mismo punto, usar el forward del target
                fromOther = target.forward;
                fromOther.y = 0;
                fromOther.Normalize();
            }

            Vector3 camPos;
            
            // ✅ AJUSTE: Aumentar distancia mínima para alejar MUCHO MÁS todos los planos
            // Aumentado especialmente para Player y Party Members (como Estela)
            float effectiveDistance = Mathf.Max(shot.Distance * 2.2f, 3.0f); // Multiplicador 2.2x y mínimo de 3m

            switch (shot.shotType)
            {
                case DialogueShotType.Wide:
                    // Vista general entre ambos personajes - cámara más centrada y alejada
                    {
                        Vector3 midPoint = (currentPlayer.position + currentNPC.position) / 2f;
                        
                        // ✅ CORREGIDO: Calcular dirección desde el punto medio hacia "atrás" de la conversación
                        // Usar la dirección promedio de ambos personajes mirando entre sí
                        Vector3 playerToNPC = (currentNPC.position - currentPlayer.position).normalized;
                        playerToNPC.y = 0;
                        
                        // Usar la perpendicular NEGATIVA para que la cámara vaya hacia la izquierda
                        // y retroceder también para captar a ambos personajes
                        Vector3 sideDir = Vector3.Cross(Vector3.up, playerToNPC).normalized;
                        Vector3 backDir = -playerToNPC; // Dirección hacia atrás desde el punto medio
                        
                        // ✅ Combinar movimiento lateral (más hacia la izquierda) y hacia atrás
                        float wideDistance = Mathf.Max(shot.Distance * 2.0f, 5.0f); // Más alejado: mínimo 5m
                        float lateralAmount = wideDistance * 0.4f; // 40% lateral hacia la izquierda
                        float backAmount = wideDistance * 0.6f; // 60% hacia atrás
                        
                        camPos = midPoint + (-sideDir * lateralAmount) + (backDir * backAmount);
                        camPos.y = midPoint.y + shot.Height;
                    }
                    break;

                case DialogueShotType.OverShoulderPlayer:
                    // Cámara detrás del Player mirando hacia el NPC - CONFIGURACIÓN AJUSTADA
                    {
                        Vector3 playerToNPC = (currentNPC.position - currentPlayer.position).normalized;
                        playerToNPC.y = 0;
                        if (playerToNPC.sqrMagnitude < 0.01f) playerToNPC = currentPlayer.forward;
                        playerToNPC.Normalize();

                        // ✅ CONFIGURACIÓN MEJORADA: Offset lateral más pronunciado para evitar tapar al NPC
                        float lateralOffsetAmount = shot.LateralOffset != 0 ? shot.LateralOffset : 1.3f;
                        Vector3 shoulderOffset = Vector3.Cross(Vector3.up, playerToNPC).normalized * lateralOffsetAmount;

                        // ✅ AUMENTADO: Retroceder MUCHO MÁS del player para mejor perspectiva
                        float behindDistance = Mathf.Max(3.0f, actualDistance * 0.75f); // Aumentado de 2.4f a 3.0f

                        // Avance ligero hacia el NPC para centrar mejor el encuadre
                        float forwardOffset = 0.6f;

                        // Posición base: atrás del player + offset lateral + ligero avance hacia NPC
                        camPos = currentPlayer.position - playerToNPC * behindDistance + shoulderOffset + playerToNPC * forwardOffset;

                        // ✅ ALTURA AUMENTADA: Subir la cámara para ver por encima de la cabeza del player
                        camPos.y = currentPlayer.position.y + shot.Height + 0.8f; // Aumentado a +0.8f
                    }
                    break;

                case DialogueShotType.OverShoulderNPC:
                    // Cámara detrás del NPC mirando hacia el Player - MEJORADO
                    {
                        Vector3 npcToPlayer = (currentPlayer.position - currentNPC.position).normalized;
                        npcToPlayer.y = 0;
                        if (npcToPlayer.sqrMagnitude < 0.01f) npcToPlayer = currentNPC.forward;
                        npcToPlayer.Normalize();

                        // ✅ MEJORADO: Offset lateral más pronunciado
                        float lateralOffsetAmount = shot.LateralOffset != 0 ? shot.LateralOffset : 1.1f;
                        Vector3 shoulderOffset = Vector3.Cross(Vector3.up, npcToPlayer).normalized * lateralOffsetAmount;

                        // ✅ AUMENTADO: Retroceder MUCHO MÁS de la posición del NPC
                        float behindDistance = Mathf.Max(2.5f, actualDistance * 0.6f); // Aumentado de 1.8f a 2.5f

                        camPos = currentNPC.position - npcToPlayer * behindDistance + shoulderOffset;

                        // ✅ MEJORADO: Altura más elevada
                        camPos.y = currentNPC.position.y + shot.Height + 0.5f; // Aumentado a +0.5f
                    }
                    break;

                case DialogueShotType.Profile:
                    // Vista lateral - perpendicular a la línea entre personajes - más alejada
                    Vector3 perpendicular = Vector3.Cross(Vector3.up, fromOther).normalized;
                    camPos = basePos + perpendicular * effectiveDistance;
                    camPos.y = basePos.y + shot.Height;
                    break;

                case DialogueShotType.MediumNPC:
                case DialogueShotType.CloseUpNPC:
                    // ✅ CORREGIDO COMPLETAMENTE: Cámara FRENTE al target viendo su CARA
                    // La cámara debe estar del lado del OTRO personaje mirando hacia el target
                    {
                        // Dirección DESDE el target HACIA el otro personaje
                        // Esto nos da la dirección en la que debemos colocar la cámara
                        Vector3 towardsOther = Vector3.zero;
                        
                        if (target == currentNPC && currentPlayer != null)
                        {
                            // Target es NPC: dirección desde NPC hacia Player
                            towardsOther = (currentPlayer.position - currentNPC.position).normalized;
                        }
                        else if (target == currentPlayer && currentNPC != null)
                        {
                            // Target es Player: dirección desde Player hacia NPC
                            towardsOther = (currentNPC.position - currentPlayer.position).normalized;
                        }
                        else if (IsPartyMember(target) && currentNPC != null)
                        {
                            // ✅ NUEVO: Target es Party Member (Estela, Liam, etc.)
                            // La cámara debe estar del lado del NPC mirando hacia el party member
                            towardsOther = (currentNPC.position - target.position).normalized;
                            
                            if (showDebugInfo)
                                Debug.Log($"[DialogueCinematicController] 📷 CloseUp/Medium para Party Member: cámara desde dirección del NPC hacia {target.name}");
                        }
                        else
                        {
                            // Fallback: frente al target
                            towardsOther = -target.forward;
                        }
                        
                        towardsOther.y = 0;
                        if (towardsOther.sqrMagnitude < 0.01f) towardsOther = -target.forward;
                        towardsOther.Normalize();
                        
                        // Offset lateral para variedad visual
                        Vector3 lateral = Vector3.Cross(Vector3.up, towardsOther).normalized * shot.LateralOffset;
                        
                        // ✅ CORREGIDO: Posicionar cámara FRENTE al target (en dirección al otro personaje)
                        // Esto garantiza que vemos la CARA del target, no su cogote
                        camPos = target.position + towardsOther * effectiveDistance + lateral;
                        camPos.y = target.position.y + shot.Height;
                    }
                    break;

                default:
                    // ✅ CORREGIDO: Fallback debe estar FRENTE al target viendo su cara
                    // Usar la dirección hacia el otro personaje, no desde el otro personaje
                    {
                        Vector3 towardsOther = Vector3.zero;
                        
                        if (target == currentNPC && currentPlayer != null)
                        {
                            towardsOther = (currentPlayer.position - currentNPC.position).normalized;
                        }
                        else if (target == currentPlayer && currentNPC != null)
                        {
                            towardsOther = (currentNPC.position - currentPlayer.position).normalized;
                        }
                        else if (IsPartyMember(target) && currentNPC != null)
                        {
                            // ✅ NUEVO: Party member - cámara desde dirección del NPC
                            towardsOther = (currentNPC.position - target.position).normalized;
                        }
                        else
                        {
                            towardsOther = -target.forward;
                        }
                        
                        towardsOther.y = 0;
                        if (towardsOther.sqrMagnitude < 0.01f) towardsOther = -target.forward;
                        towardsOther.Normalize();
                        
                        camPos = target.position + towardsOther * effectiveDistance;
                        camPos.y = target.position.y + shot.Height;
                    }
                    break;
            }

            // ── Evitar obstrucciones (modo 1:1) ──
            // Si hay geometría (muro, árbol, roca...) entre el personaje y la posición calculada
            // de cámara, acercamos la cámara hasta justo delante del obstáculo. Sin esto, la cámara
            // podía terminar detrás de un objeto y dejar al personaje cortado a medias en pantalla.
            //
            // FIX: el SphereCast arrancaba exactamente en headPos (la cabeza del propio target).
            // Cualquier collider del propio personaje (armas, props, pelo) o una pared a pocos
            // centímetros se detectaba como "obstrucción" a distancia ~0, y el clamp mínimo
            // (antes 0.6m) dejaba la cámara pegada a la barbilla mirando hacia arriba (bug
            // visible en cualquier diálogo 1:1, no solo interiores concretos). Arreglo en dos
            // pasos: (1) el cast arranca ya desplazado fuera del propio volumen del personaje
            // (dialogueCameraSelfClearance), y (2) el mínimo de seguridad (dialogueCameraMinDistance)
            // sube a una distancia acorde a los planos normales (3-5m) en vez de un
            // primerísimo plano roto. Ambos valores son ajustables desde el Inspector.
            Vector3 headPos = target.position + Vector3.up * Mathf.Max(shot.Height, 1.2f);
            Vector3 toCam = camPos - headPos;
            float camposDist = toCam.magnitude;
            if (camposDist > dialogueCameraSelfClearance + 0.05f)
            {
                Vector3 dir = toCam / camposDist;
                Vector3 probeOrigin = headPos + dir * dialogueCameraSelfClearance;
                float probeDist = camposDist - dialogueCameraSelfClearance;

                if (Physics.SphereCast(probeOrigin, groupCameraProbeRadius, dir, out RaycastHit obstructionHit,
                    probeDist, _camObstructionMask, QueryTriggerInteraction.Ignore))
                {
                    float clearDist = Mathf.Max(dialogueCameraSelfClearance + obstructionHit.distance - 0.3f, dialogueCameraMinDistance);
                    camPos = headPos + dir * clearDist;
                    if (showDebugInfo)
                        Debug.Log($"[DialogueCinematicController] 📷 Obstrucción detectada ({obstructionHit.collider.name}) - cámara reubicada a {clearDist:F2}m");
                }
            }

            // ── Verificación lateral final: cámara "encajada" en un vano (puerta, dos pilares...) ──
            // El SphereCast de arriba solo viaja en línea recta cabeza→cámara, así que si el vano
            // es más estrecho que el hueco entre sus marcos, la línea puede pasar limpia (sin hit)
            // aunque los marcos queden pegados a la cámara por los lados, fuera de esa línea recta.
            // Resultado real reportado: plano de diálogo con los dos marcos de una puerta ocupando
            // los bordes del encuadre, totalmente desenfocados en primer plano, tapando al speaker.
            // Aquí no seguimos una línea: comprobamos solape puro (OverlapSphere) en la posición
            // final y, si la cámara está literalmente dentro de un collider que no es el propio
            // speaker/player/NPC/party, la alejamos en pasos cortos hasta liberarse (tope de
            // seguridad para no huir indefinidamente si la sala entera es estrecha).
            {
                const int maxLateralBackoffSteps = 6;
                const float lateralBackoffStep = 0.2f;
                Vector3 escapeDir = camPos - target.position;
                escapeDir.y = 0f;
                if (escapeDir.sqrMagnitude > 0.0001f)
                {
                    escapeDir.Normalize();
                    float embedCheckRadius = dialogueCameraSelfClearance * 0.6f;
                    int lateralSteps = 0;
                    while (lateralSteps < maxLateralBackoffSteps && CameraPositionIsEmbedded(camPos, embedCheckRadius, target))
                    {
                        camPos += escapeDir * lateralBackoffStep;
                        lateralSteps++;
                    }
                    if (showDebugInfo && lateralSteps > 0)
                        Debug.Log($"[DialogueCinematicController] 📷 Cámara encajada en vano - {lateralSteps} paso(s) de retroceso lateral");
                }
            }

            if (showDebugInfo)
            {
                Debug.Log($"[DialogueCinematicController] Shot {shot.shotType}: Target={target.name}, CamPos={camPos}, TargetPos={basePos}");
            }

            return camPos;
        }

        /// <summary>
        /// Comprueba si <paramref name="pos"/> se solapa con geometría sólida que no sea el propio
        /// speaker/player/NPC principal/party (ver comentario en el bloque que la llama, dentro de
        /// CalculateCameraPosition). Usa OverlapSphereNonAlloc sobre el buffer cacheado
        /// _camEmbedCheckBuffer para no allocar.
        /// </summary>
        private bool CameraPositionIsEmbedded(Vector3 pos, float radius, Transform target)
        {
            int count = Physics.OverlapSphereNonAlloc(pos, radius, _camEmbedCheckBuffer,
                _camObstructionMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var col = _camEmbedCheckBuffer[i];
                if (col == null) continue;
                Transform root = col.transform.root;
                if (root == target || root == currentPlayer || root == currentNPC) continue;
                if (IsPartyMember(root)) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Distancia libre desde origin en dirección dir (SphereCast) ignorando personajes:
        /// player, NPCs y compañeros comparten capa Default con la geometría, pero un personaje
        /// de pie no debe contar como "pared" al elegir el lado de la cámara grupal — se movería
        /// o quedará recolocado por la formación semicircular. Identificación por
        /// NPCSimpleAnimator en el root (mismo criterio que DialogueManager.IsActualNPC).
        /// </summary>
        private float GroupCameraFreeDistance(Vector3 origin, Vector3 dir, float maxDist)
        {
            int count = Physics.SphereCastNonAlloc(origin, groupCameraProbeRadius, dir,
                _groupCamProbeBuffer, maxDist, _camObstructionMask, QueryTriggerInteraction.Ignore);

            float free = maxDist;
            for (int i = 0; i < count; i++)
            {
                var hit = _groupCamProbeBuffer[i];
                if (hit.collider == null) continue;

                Transform root = hit.collider.transform.root;
                if (root.GetComponent<NPCSimpleAnimator>() != null) continue; // personaje, no obstrucción

                if (hit.distance < free) free = hit.distance;
            }
            return free;
        }

        /// <summary>
        /// Evalúa ambos lados perpendiculares al eje player-NPC y devuelve la dirección
        /// con más espacio libre (raycast). Combina la perpendicular elegida con un sesgo
        /// de profundidad (0.3 × playerToNpc) para obtener la vista 3/4.
        /// </summary>
        private Vector3 PickBestGroupCameraDirection(Vector3 perpendicular, Vector3 playerToNpc)
        {
            // Evaluar distancia libre en ambas direcciones perpendiculares (ignorando personajes:
            // antes un compañero parado en el probe podía "obstruir" un lado y forzar la cámara
            // justo hacia el lado donde estaba otro compañero)
            Vector3 origin = (currentPlayer.position + currentNPC.position) * 0.5f + Vector3.up * groupLookAtHeight;
            float probeDistance = groupCameraDistance * 1.2f;

            Vector3 dirA = (perpendicular + playerToNpc * 0.3f).normalized;
            Vector3 dirB = (-perpendicular + playerToNpc * 0.3f).normalized;

            float freeA = GroupCameraFreeDistance(origin, dirA, probeDistance);
            float freeB = GroupCameraFreeDistance(origin, dirB, probeDistance);

            // Si ambas están muy obstruidas, intentar dirección opuesta al NPC (detrás del player)
            float minRequired = groupCameraDistance * Mathf.Cos(groupElevationAngle * Mathf.Deg2Rad);
            if (freeA < minRequired && freeB < minRequired)
            {
                Vector3 fallback = (-playerToNpc).normalized;
                float freeFallback = GroupCameraFreeDistance(origin, fallback, probeDistance);
                if (freeFallback > freeA && freeFallback > freeB)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (showDebugInfo)
                        Debug.Log("[DialogueCinematicController] Grupo: ambos lados obstruidos, usando fallback detrás del player");
#endif
                    return fallback;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] Grupo: freeA={freeA:F1}m, freeB={freeB:F1}m → eligiendo {(freeA >= freeB ? "A" : "B")}");
#endif
            return freeA >= freeB ? dirA : dirB;
        }

        /// <summary>
        /// Inicializa el pool de cámaras virtuales
        /// </summary>
        private void InitializeCameraPool()
        {
        for (int i = 0; i < maxPooledCameras; i++)
        {
            GameObject vcamObj = new GameObject($"DialogueVCam_{i}");
            vcamObj.transform.SetParent(cameraRig);
            
            CinemachineCamera vcam = vcamObj.AddComponent<CinemachineCamera>();
            vcam.Priority.Value = 0;
            vcam.OutputChannel = (OutputChannels)(1 << 0); // Canal 0 (primer bit) para que solo las vea el dialogueBrain
            
            // Configuración por defecto
            vcam.Lens.FieldOfView = 50f;
            vcam.Lens.NearClipPlane = 0.1f;
            vcam.Lens.FarClipPlane = 1000f;

            cameraPool.Add(vcam);
        }

        if (showDebugInfo)
            Debug.Log($"[DialogueCinematicController] Pool de {maxPooledCameras} cámaras inicializado en canal 1");
    }

    /// <summary>
    /// Obtiene una cámara del pool (sistema round-robin)
    /// </summary>
    private CinemachineCamera GetPooledCamera()
    {
        CinemachineCamera vcam = cameraPool[poolIndex];
        poolIndex = (poolIndex + 1) % cameraPool.Count;
        return vcam;
    }

    /// <summary>
    /// Calcula en qué línea debe ocurrir el próximo corte
    /// </summary>
    private int CalculateNextCutLine()
    {
        const int baseLines = 4; // corte automático cada 4 líneas (antes: 2)
        const int variation = 1; // variación de +/- 1 → rango 3-5 líneas
        
        int randomVariation = Random.Range(-variation, variation + 1);
        int nextCut = currentLineIndex + baseLines + randomVariation;
        return Mathf.Max(currentLineIndex + 1, nextCut);
    }

    /// <summary>
    /// Aplica un plano cinematográfico con contexto (útil para plano de apertura)
    /// </summary>
    /// <param name="shot">El plano a aplicar</param>
    /// <param name="isOpening">Si es el plano de apertura</param>
    /// <param name="targetOverride">Target específico (Player, NPC, o Party Member). Si es null, usa currentNPC</param>
    private void ApplyShotWithContext(CinematicCameraShot shot, bool isOpening = false, Transform targetOverride = null, bool forceCut = false)
    {
        if (shot == null) return;

        Transform effectiveTarget = targetOverride ?? currentNPC;

        bool isPlayerOrPartySpeaking = (effectiveTarget == currentPlayer)
            || (IsPartyMember(effectiveTarget) && effectiveTarget != currentNPC);
        bool isNPCSpeaking = effectiveTarget == currentNPC;

        // Modo grupal: todos visibles siempre
        if (_isGroupConversation)
        {
            ShowNPC();
            ShowPlayer();
            ApplyShot(shot, effectiveTarget, forceCut);
            return;
        }

        // Modo individual: ocultar según quién habla.
        // OverShoulderPlayer se trata igual que CloseUp: la cámara está detrás del player,
        // así que ocultar el player evita que su cuerpo tape al NPC (el sujeto).
        bool isCloseUpShot = shot.shotType == DialogueShotType.CloseUpNPC
                          || shot.shotType == DialogueShotType.MediumNPC
                          || shot.shotType == DialogueShotType.OverShoulderPlayer;

        // Ocultar party members que no sean el speaker actual para evitar obstrucciones
        HideNonSpeakingPartyMembers(effectiveTarget);

        if (isPlayerOrPartySpeaking)
        {
            HideNPC();
            ShowPlayer();
            MakeSpeakerLookAtNPC(effectiveTarget);
        }
        else if (isNPCSpeaking)
        {
            ShowNPC();
            if (isCloseUpShot) HidePlayer();
            else ShowPlayer();
        }
        else
        {
            ShowNPC();
            ShowPlayer();
        }

        ApplyShot(shot, effectiveTarget, forceCut);
    }
    
    /// <summary>
    /// Verifica si un transform es un party member.
    ///
    /// FIX "Will desaparece en diálogos": el NPC clonado de Will (ActiveCharacterSwapper.SpawnWillNpc)
    /// nunca pasa por NPCPartyMember.JoinParty() a propósito — se puppetea directamente desde
    /// ActiveCharacterSwapper, no vive en PlayerParty.Instance.Members — así que antes de este fix
    /// era invisible para este chequeo. Eso hacía que CheckAndHideCameraIntruders lo tratara como un
    /// intruso cualquiera (puerta, mueble...) y le apagara los renderers en cuanto se cruzaba entre la
    /// cámara de diálogo y la cara de quien habla (cosa frecuente: te sigue de cerca como IA), y que
    /// RescanAndFreezeBystanders le congelara el NavMeshAgent como "viandante de fondo". La ventana de
    /// 0.5s de la red de seguridad de ActiveCharacterSwapper (EnsureWillNpcVisible) no daba abasto
    /// contra estos sistemas re-ocultándolo en cada chequeo mientras siguiera estorbando en cámara.
    /// </summary>
    private bool IsPartyMember(Transform t)
    {
        if (t == null) return false;

        var willNpc = ActiveCharacterSwapper.Instance != null ? ActiveCharacterSwapper.Instance.WillNpcInstance : null;
        if (willNpc != null && willNpc.transform == t) return true;

        if (!Game.NPC.PlayerParty.HasInstance) return false;

        foreach (var m in Game.NPC.PlayerParty.Instance.Members)
        {
            if (m != null && m.transform == t) return true;
        }
        return false;
    }
    
    /// <summary>
    /// Hace que el speaker mire hacia el NPC con quien está hablando
    /// </summary>
    private void MakeSpeakerLookAtNPC(Transform speaker)
    {
        if (speaker == null || currentNPC == null) return;
        
        // Calcular dirección hacia el NPC (solo en plano horizontal)
        Vector3 directionToNPC = currentNPC.position - speaker.position;
        directionToNPC.y = 0;
        
        if (directionToNPC.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToNPC);
            speaker.rotation = targetRotation;
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] 👀 {speaker.name} ahora mira hacia {currentNPC.name}");
        }
    }

    /// <summary>
    /// En modo grupal, hace que todos los participantes miren a quien habla en esta línea:
    /// - Los que ESCUCHAN giran hacia el speaker actual (player, NPC principal o compañero).
    /// - El que HABLA gira hacia su interlocutor natural: el player habla al NPC, el NPC al
    ///   player, y un compañero vuelve a su objetivo por defecto (el NPC del diálogo).
    /// Dos capas, como en los AAA: giro de CUERPO (player/NPC con corrutina suave; compañeros
    /// vía el override de mirada de su DialoguePositionState) + giro de CABEZA anticipado
    /// (DialogueHeadLook, estilo IK: la cabeza apunta al speaker antes y con más rango que el
    /// cuerpo, aplicado en LateUpdate sobre los huesos head/neck_01 encima de la animación).
    /// Se llama desde OnDialogueLineAdvanced, así que el giro ocurre al cambiar de línea.
    /// </summary>
    private void UpdateGroupFacing()
    {
        if (currentSpeaker == null) return;

        // Compañeros del party: mirar al speaker (el propio speaker limpia su override)
        if (Game.NPC.PlayerParty.HasInstance)
        {
            foreach (var m in Game.NPC.PlayerParty.Instance.Members)
            {
                if (m == null || !m.IsActiveInParty) continue;
                bool isSpeaker = m.transform == currentSpeaker;
                m.SetDialogueLookTarget(isSpeaker ? null : currentSpeaker);
                // Cabeza: el compañero que habla mira al NPC del diálogo; los demás, al speaker
                SetHeadLook(m.transform, isSpeaker ? currentNPC : currentSpeaker);
            }
        }

        // Player: la CABEZA sigue al speaker; el CUERPO se queda quieto (anclado mirando al
        // NPC desde el inicio del diálogo) salvo que el speaker quede fuera del alcance de la
        // cabeza. Antes el cuerpo perseguía al speaker en cada línea y el player parecía
        // nervioso — giro completo + head-look encima en cada cambio de turno.
        Transform playerLook = (currentSpeaker == currentPlayer) ? currentNPC : currentSpeaker;
        FaceBodyOnlyIfOutOfHeadRange(currentPlayer, playerLook, ref _playerRotationCoroutine);
        SetHeadLook(currentPlayer, playerLook);

        // NPC principal: misma política que el player (cabeza sí, cuerpo solo si es necesario)
        Transform npcLook = (currentSpeaker == currentNPC) ? currentPlayer : currentSpeaker;
        FaceBodyOnlyIfOutOfHeadRange(currentNPC, npcLook, ref _npcRotationCoroutine);
        SetHeadLook(currentNPC, npcLook);
    }

    [Tooltip("Ángulo (grados) entre el forward del cuerpo y el speaker a partir del cual el cuerpo sí gira (por debajo, solo la cabeza acompaña)")]
    [SerializeField] private float groupBodyTurnThreshold = 100f;

    /// <summary>
    /// Gira el cuerpo hacia el objetivo SOLO si este queda tan lateral/trasero que el head-look
    /// no llega (más de groupBodyTurnThreshold°). En el semicírculo normal todos los
    /// participantes quedan delante, así que el cuerpo casi nunca se mueve — la conversación
    /// se lleva con miradas de cabeza sutiles, no con giros de cuerpo por línea.
    /// </summary>
    private void FaceBodyOnlyIfOutOfHeadRange(Transform who, Transform target, ref Coroutine handle)
    {
        if (who == null || target == null || who == target) return;

        Vector3 dir = target.position - who.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;

        if (Vector3.Angle(who.forward, dir) < groupBodyTurnThreshold)
            return; // la cabeza llega sola: cuerpo quieto

        if (handle != null)
            StopCoroutine(handle);
        handle = StartCoroutine(SmoothRotate(who, Quaternion.LookRotation(dir), groupPlayerRotationDuration));
    }

    /// <summary>
    /// Asigna el objetivo de mirada de cabeza (head-look) a un participante, añadiendo el
    /// componente DialogueHeadLook bajo demanda la primera vez (y cacheándolo). El componente
    /// aplica la rotación en LateUpdate sobre los huesos head/neck_01, encima de la animación,
    /// con límites de ángulo y fade de peso — no necesita rigs ni assets adicionales.
    /// </summary>
    private void SetHeadLook(Transform who, Transform target)
    {
        if (who == null) return;
        if (who == target) target = null; // nunca mirarse a uno mismo

        if (!_headLookCache.TryGetValue(who, out var headLook) || headLook == null)
        {
            headLook = who.GetComponent<DialogueHeadLook>();
            if (headLook == null)
                headLook = who.gameObject.AddComponent<DialogueHeadLook>();
            _headLookCache[who] = headLook;
        }

        headLook.SetTarget(target);
    }

    /// <summary>
    /// Apaga (fade out) el head-look de todos los participantes cacheados. Se llama al terminar
    /// la cinemática; los componentes quedan dormidos (coste cero) hasta el próximo diálogo grupal.
    /// </summary>
    private void ClearAllHeadLooks()
    {
        foreach (var kvp in _headLookCache)
        {
            if (kvp.Value != null)
                kvp.Value.SetTarget(null);
        }
        _headLookCache.Clear();
    }

    private IEnumerator SmoothRotate(Transform target, Quaternion endRot, float duration)
    {
        Quaternion startRot = target.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float smooth = t * t * (3f - 2f * t);
            target.rotation = Quaternion.Slerp(startRot, endRot, smooth);
            elapsed += Time.deltaTime;
            yield return null;
        }
        target.rotation = endRot;
        // NOTA: no se anula aquí el handle (hay dos: player y NPC); StopCoroutine sobre una
        // corrutina ya terminada es un no-op seguro, así que el handle "caducado" no hace daño.
    }

    /// <summary>
    /// Desactiva todas las cámaras del pool de diálogo
    /// </summary>
    private void DisableAllDialogueCameras()
    {
        foreach (var vcam in cameraPool)
        {
            if (vcam != null)
            {
                vcam.Priority.Value = 0;
                // Desactivar también el GameObject para asegurar que no interfiera
                vcam.gameObject.SetActive(false);
            }
        }
        
        if (showDebugInfo)
            Debug.Log("[DialogueCinematicController] Todas las cámaras de diálogo desactivadas");
    }
    
    /// <summary>
    /// Oculta el HUD del jugador con un fade suave
    /// </summary>
    private void HideHUD()
    {
        MenuManager.RegisterOpen(MenuKind.Dialog);
        var hud = Sendero.UI.PlayerHUDV2.Instance;
        if (hud != null)
        {
            hud.HideHUD();
            if (showDebugInfo)
                Debug.Log("[DialogueCinematicController] 🎬 HUD ocultado con fade");
        }
    }
    
    /// <summary>
    /// Muestra el HUD del jugador con un fade suave
    /// </summary>
    private void ShowHUD()
    {
        MenuManager.Close(MenuKind.Dialog);
        var hud = Sendero.UI.PlayerHUDV2.Instance;
        if (hud != null)
        {
            hud.ShowHUD();
            if (showDebugInfo)
                Debug.Log("[DialogueCinematicController] 🎬 HUD mostrado con fade");
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo || !isInCinematicMode) return;
        if (currentPlayer == null || currentNPC == null) return;

        // Dibujar línea entre player y NPC
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(currentPlayer.position + Vector3.up, currentNPC.position + Vector3.up);

        // Dibujar posición de la cámara actual
        if (currentVirtualCamera != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(currentVirtualCamera.transform.position, 0.3f);
            Gizmos.DrawLine(currentVirtualCamera.transform.position, 
                currentVirtualCamera.transform.position + currentVirtualCamera.transform.forward * 2f);
        }
    }
}

