using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering.Universal;

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

        // Crear rig para las cámaras de diálogo
        GameObject rigObj = new GameObject("DialogueCinematicRig");
        rigObj.transform.SetParent(transform);
        cameraRig = rigObj.transform;

        // Crear cámara dedicada para diálogos (separada de Camera.main)
        CreateDialogueCamera();
        
        // Debug.Log($"[DialogueCinematicController] Sistema inicializado - DialogueCamera: {(dialogueCameraObject != null ? "OK" : "NULL")}");
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
            Vector3 npcToPlayer = currentPlayer.position - currentNPC.position;
            npcToPlayer.y = 0f;
            _groupCamBaseDir = npcToPlayer.sqrMagnitude > 0.01f
                ? npcToPlayer.normalized
                : Vector3.back;

            // Asegurar que todos los party members tengan sus renderers activos al inicio
            // (pueden haber quedado desactivados por diálogos previos donde eran currentNPC)
            // EXCEPTO el NPC oculto (controlado por el jugador vía character swap)
            if (Game.NPC.PlayerParty.HasInstance)
            {
                var hiddenNpc = ActiveCharacterSwapper.Instance?.HiddenNpc;
                foreach (var m in Game.NPC.PlayerParty.Instance.Members)
                {
                    if (m == null || m == hiddenNpc) continue;
                    foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                        if (r != null) r.enabled = true;
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
        /// Notifica que ha avanzado una línea de diálogo
        /// </summary>
        public void OnDialogueLineAdvanced(int lineIndex, int totalLines, DialogueLine currentLine)
        {
            if (!isInCinematicMode) return;

            currentLineIndex = lineIndex;

            // IMPORTANTE: El lineIndex 0 ya tiene el openingShot aplicado desde StartCinematic
            // No debemos cambiar de plano inmediatamente, el opening shot debe mantenerse
            // hasta que llegue el momento del primer corte automático
            
            // Si es la primera línea (lineIndex == 0), el openingShot ya está activo
            // Solo calcular cuándo será el primer corte
            if (lineIndex == 0)
            {
                nextCutAtLine = CalculateNextCutLine();
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] Línea 0 - Opening shot activo, próximo corte en línea {nextCutAtLine}");
                
                // ✅ Pero verificar si el speaker cambió y ajustar cámara
                CheckAndAdjustCameraForSpeaker(currentLine, lineIndex, totalLines);
                return;
            }

            // ✅ PRIORIDAD 1: Verificar si cambió el speaker (Player, NPC, Party Member)
            bool speakerChanged = CheckAndAdjustCameraForSpeaker(currentLine, lineIndex, totalLines);
            
            // Si ya cambiamos la cámara por cambio de speaker, no hacer corte automático
            if (speakerChanged)
            {
                // Recalcular próximo corte desde este punto
                nextCutAtLine = lineIndex + CalculateNextCutLine();
                return;
            }

            // PRIORIDAD 2: Determinar si es momento de cortar automáticamente
            bool shouldCut = lineIndex >= nextCutAtLine;

            if (shouldCut)
            {
                // Obtener el siguiente plano apropiado
                CinematicCameraShot nextShot = activeProfile.GetNextShot(lineIndex, totalLines);

                // Usar ApplyShotWithContext para gestionar visibilidad del player según el tipo de plano
                ApplyShotWithContext(nextShot, false);

                // Calcular cuándo será el próximo corte
                nextCutAtLine = CalculateNextCutLine();
                
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] Corte aplicado en línea {lineIndex}, próximo corte en línea {nextCutAtLine}");
            }
        }

        /// <summary>
        /// Verifica si cambió el speaker y ajusta la cámara en consecuencia
        /// </summary>
        /// <returns>True si cambió el speaker y se ajustó la cámara</returns>
        private bool CheckAndAdjustCameraForSpeaker(DialogueLine line, int lineIndex, int totalLines)
        {
            // Determinar el ID del speaker de esta línea
            string newSpeakerId = DetermineSpeakerId(line);
            
            // Si es el mismo speaker que antes, no hacer nada
            if (newSpeakerId == currentSpeakerId)
                return false;
            
            // Speaker cambió - buscar o determinar su Transform
            Transform newSpeakerTransform = FindSpeakerTransform(newSpeakerId, line);
            
            if (newSpeakerTransform == null)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[DialogueCinematicController] No se encontró Transform para speaker '{newSpeakerId}', usando NPC por defecto");
                newSpeakerTransform = currentNPC;
            }
            
            // Actualizar speaker actual
            string previousSpeakerId = currentSpeakerId;
            currentSpeakerId = newSpeakerId;
            currentSpeaker = newSpeakerTransform;
            
            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] 🎬 Speaker cambió: '{previousSpeakerId}' → '{newSpeakerId}' ({newSpeakerTransform.name})");
            
            // Aplicar plano apropiado para este speaker
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
        /// Encuentra el Transform del speaker basándose en su ID
        /// </summary>
        private Transform FindSpeakerTransform(string speakerId, DialogueLine line)
        {
            // Verificar cache primero
            if (speakerCache.ContainsKey(speakerId))
                return speakerCache[speakerId];
            
            Transform speakerTransform = null;
            
            // Caso 1: Es el jugador
            if (speakerId == "Player" || line.isPlayerSpeaking)
            {
                speakerTransform = currentPlayer;
            }
            // Caso 2: Es el NPC principal (con quien iniciamos el diálogo)
            else if (speakerId == "MainNPC" || speakerId == currentNPC.name)
            {
                speakerTransform = currentNPC;
            }
            // Caso 3: Buscar en party members
            else if (Game.NPC.PlayerParty.HasInstance)
            {
                var party = Game.NPC.PlayerParty.Instance;
                Game.NPC.NPCPartyMember partyMember = null;
                
                foreach (var m in party.Members)
                {
                    if (m.NPCManager?.Configuration?.interactiveNarrativeConfig != null)
                    {
                        var config = m.NPCManager.Configuration.interactiveNarrativeConfig;
                        
                        // ✅ PRIORIDAD 1: Comparar con dialogueCharacterId (nuevo campo específico para diálogos)
                        if (!string.IsNullOrEmpty(config.dialogueCharacterId) && config.dialogueCharacterId == speakerId)
                        {
                            partyMember = m;
                            if (showDebugInfo)
                                Debug.Log($"[DialogueCinematicController] 🎯 Match por dialogueCharacterId: '{config.dialogueCharacterId}' == '{speakerId}'");
                            break;
                        }
                        
                        // PRIORIDAD 2: Comparar con persistenceId (compatibilidad hacia atrás)
                        if (config.persistenceId == speakerId)
                        {
                            partyMember = m;
                            if (showDebugInfo)
                                Debug.Log($"[DialogueCinematicController] 🎯 Match por persistenceId: '{config.persistenceId}' == '{speakerId}'");
                            break;
                        }
                        
                        // PRIORIDAD 3: Comparar con narrativeID (usa interactiveNarrativeConfig.persistenceId como fallback)
                        if (m.NPCManager.Configuration.interactiveNarrativeConfig?.persistenceId == speakerId)
                        {
                            partyMember = m;
                            if (showDebugInfo)
                                Debug.Log($"[DialogueCinematicController] 🎯 Match por persistenceId (p3): '{m.NPCManager.Configuration.interactiveNarrativeConfig.persistenceId}' == '{speakerId}'");
                            break;
                        }
                    }
                    
                    // PRIORIDAD 4: GameObject.name (último recurso)
                    if (m.gameObject.name == speakerId)
                    {
                        partyMember = m;
                        if (showDebugInfo)
                            Debug.Log($"[DialogueCinematicController] 🎯 Match por GameObject.name: '{m.gameObject.name}' == '{speakerId}'");
                        break;
                    }
                }
                
                if (partyMember != null)
                {
                    speakerTransform = partyMember.transform;
                    if (showDebugInfo)
                        Debug.Log($"[DialogueCinematicController] 👥 Speaker '{speakerId}' encontrado en party: {partyMember.name}");
                }
            }

            // Caso 3.5: En diálogos no grupales, si no hay coincidencia en party, el speaker
            // es siempre el NPC con el que se inició el diálogo (evita falsos positivos en Caso 4)
            if (speakerTransform == null && !_isGroupConversation)
                speakerTransform = currentNPC;

            // Caso 4: Buscar por dialogueCharacterId en todos los NPCs de la escena (fallback)
            if (speakerTransform == null)
            {
                var allNPCs = GameObject.FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>(FindObjectsSortMode.None);
                foreach (var npc in allNPCs)
                {
                    if (npc.Configuration?.interactiveNarrativeConfig != null)
                    {
                        var config = npc.Configuration.interactiveNarrativeConfig;
                        
                        // Buscar por dialogueCharacterId primero
                        if (!string.IsNullOrEmpty(config.dialogueCharacterId) && config.dialogueCharacterId == speakerId)
                        {
                            speakerTransform = npc.transform;
                            if (showDebugInfo)
                                Debug.Log($"[DialogueCinematicController] 🔍 Speaker '{speakerId}' encontrado en escena por dialogueCharacterId: {npc.name}");
                            break;
                        }
                        
                        // Luego por persistenceId
                        if (config.persistenceId == speakerId)
                        {
                            speakerTransform = npc.transform;
                            if (showDebugInfo)
                                Debug.Log($"[DialogueCinematicController] 🔍 Speaker '{speakerId}' encontrado en escena por persistenceId: {npc.name}");
                            break;
                        }
                    }
                    
                    // Por último por GameObject.name
                    if (npc.gameObject.name == speakerId)
                    {
                        speakerTransform = npc.transform;
                        if (showDebugInfo)
                            Debug.Log($"[DialogueCinematicController] 🔍 Speaker '{speakerId}' encontrado en escena por nombre: {npc.name}");
                        break;
                    }
                }
            }
            
            // Cachear el resultado si se encontró
            if (speakerTransform != null)
            {
                speakerCache[speakerId] = speakerTransform;
            }
            
            return speakerTransform;
        }
        
        /// <summary>
        /// Aplica un plano cinematográfico apropiado para el speaker actual
        /// </summary>
        private void ApplyShotForSpeaker(Transform speaker, DialogueLine line, int lineIndex, int totalLines)
        {
            // TODO: Implementar forcedShotType cuando el enum esté alineado
            // Por ahora usar el sistema automático
            
            // Determinar plano apropiado basándose en quién habla
            CinematicCameraShot appropriateShot = null;
            
            // Usar el sistema existente del profile para obtener el shot
            if (activeProfile != null && activeProfile.npcShots != null && activeProfile.npcShots.Length > 0)
            {
                // Alternar entre los shots disponibles
                int shotIndex = lineIndex % activeProfile.npcShots.Length;
                appropriateShot = activeProfile.npcShots[shotIndex];
                
                // ✅ Crear un nuevo shot con distancia ajustada según el speaker
                if (appropriateShot != null)
                {
                    appropriateShot = CreateShotWithAdjustedDistance(appropriateShot, speaker);
                }
            }
            
            // Fallback al opening shot si no hay otros disponibles
            if (appropriateShot == null && activeProfile != null)
            {
                appropriateShot = activeProfile.openingShot;
                if (appropriateShot != null)
                {
                    appropriateShot = CreateShotWithAdjustedDistance(appropriateShot, speaker);
                }
            }
            
            if (appropriateShot != null)
            {
                ApplyShotWithContext(appropriateShot, false, speaker);
            }
        }
        
        /// <summary>
        /// Crea un nuevo shot con distancia ajustada según el tipo de speaker
        /// </summary>
        private CinematicCameraShot CreateShotWithAdjustedDistance(CinematicCameraShot original, Transform speaker)
        {
            // Determinar multiplicador según el speaker
            float distanceMultiplier = 1.0f;
            
            // ✅ AUMENTADO: Si es el player o un party member, usar distancia MUCHO mayor
            if (speaker == currentPlayer)
            {
                distanceMultiplier = 2.5f; // Player necesita MUCHA más distancia (aumentado de 1.5f a 2.5f)
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] 📷 Ajustando distancia para Player (x{distanceMultiplier})");
            }
            else if (Game.NPC.PlayerParty.HasInstance)
            {
                // Verificar si es un party member (como Estela)
                var party = Game.NPC.PlayerParty.Instance;
                bool isPartyMember = party.Members.Any(m => m.transform == speaker);
                
                if (isPartyMember)
                {
                    distanceMultiplier = 2.5f; // Party members necesitan MUCHA más distancia (aumentado de 1.5f a 2.5f)
                    if (showDebugInfo)
                        Debug.Log($"[DialogueCinematicController] 📷 Ajustando distancia para Party Member (x{distanceMultiplier})");
                }
            }
            
            // Si no necesita ajuste, devolver el original
            if (Mathf.Approximately(distanceMultiplier, 1.0f))
            {
                return original;
            }
            
            // Crear un nuevo shot ajustado usando AdjustedCameraShot
            return new AdjustedCameraShot(original, distanceMultiplier);
        }
    
    /// <summary>
    /// Wrapper para CinematicCameraShot que ajusta la distancia
    /// </summary>
    public class AdjustedCameraShot : CinematicCameraShot
    {
        private readonly CinematicCameraShot _original;
        private readonly float _distanceMultiplier;
        
        public AdjustedCameraShot(CinematicCameraShot original, float distanceMultiplier)
        {
            _original = original;
            _distanceMultiplier = distanceMultiplier;
            
            // Copiar el tipo de shot
            this.shotType = original.shotType;
            this.minimumDuration = original.minimumDuration;
        }
        
        // Override de las propiedades para aplicar el multiplicador
        public new float Distance => _original.Distance * _distanceMultiplier;
        public new float Height => _original.Height;
        public new Vector3 LookAtOffset => _original.LookAtOffset;
        public new float LateralOffset => _original.LateralOffset;
        public new float VerticalAngle => _original.VerticalAngle;
        public new float DutchAngle => _original.DutchAngle;
        public new float FieldOfView => _original.FieldOfView;
    }
        
        /// <summary>
        /// Crea un CinematicCameraShot basándose en un DialogueShotType
        /// </summary>
        private CinematicCameraShot CreateShotFromDialogueType(DialogueShotType shotType)
        {
            if (activeProfile == null || activeProfile.npcShots == null || activeProfile.npcShots.Length == 0)
                return activeProfile?.openingShot;
            
            // Buscar un shot que coincida con el tipo solicitado
            foreach (var shot in activeProfile.npcShots)
            {
                if (shot.shotType == shotType)
                    return shot;
            }
            
            // Si no se encuentra, usar el primero disponible o el opening
            return activeProfile.npcShots.Length > 0 ? activeProfile.npcShots[0] : activeProfile.openingShot;
        }

        /// <summary>
        /// Aplica un plano cinematográfico específico
        /// </summary>
        private void ApplyShot(CinematicCameraShot shot, Transform target)
        {
            if (shot == null || target == null) return;

        // Obtener una cámara virtual del pool
        CinemachineCamera vcam = GetPooledCamera();
        
        // Activar el GameObject de la cámara
        vcam.gameObject.SetActive(true);
        
        // Configurar posición y rotación
        ConfigureCameraForShot(vcam, shot, target);

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
        private void ConfigureCameraForShot(CinemachineCamera vcam, CinematicCameraShot shot, Transform target)
        {
            // Calcular posición base según el tipo de plano
            Vector3 position = CalculateCameraPosition(shot, target);

            // En modo grupal: la cámara mira al CENTRO DEL GRUPO a altura de torso (no al speaker)
            // Esto da un ángulo horizontal suave y encuadra a todos los participantes.
            Vector3 lookAtPos;
            if (_isGroupConversation && currentPlayer != null)
            {
                lookAtPos = _cachedGroupCenter;
                lookAtPos.y = _cachedGroupCenter.y + groupLookAtHeight;
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
                
                // Si el cambio de ángulo es mayor a 90°, usar corte instantáneo para evitar giro feo
                if (angleChange > 90f)
                {
                    dialogueBrain.DefaultBlend.Time = 0f; // Corte instantáneo
                    if (showDebugInfo)
                        Debug.Log($"[DialogueCinematicController] Cambio de ángulo grande ({angleChange:F1}°) - usando corte instantáneo");
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

                // Dirección base: fija desde el inicio del diálogo (no salta entre speakers ni al rotar el player)
                Vector3 baseDir = _groupCamBaseDir;

                // Ligero sesgo hacia la posición opuesta del speaker para ver su cara
                Vector3 toSpeaker = target.position - groupCenter;
                toSpeaker.y = 0f;
                if (toSpeaker.sqrMagnitude > 0.01f)
                {
                    // Mover la cámara ligeramente al lado opuesto del speaker para ver su cara
                    baseDir = Vector3.Lerp(baseDir, -toSpeaker.normalized, groupSpeakerBias).normalized;
                }

                float rad = groupElevationAngle * Mathf.Deg2Rad;
                float horizontalDist = groupCameraDistance * Mathf.Cos(rad);
                float height         = groupCameraDistance * Mathf.Sin(rad);

                Vector3 groupCamPos = groupCenter + baseDir * horizontalDist;
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

            if (showDebugInfo)
            {
                Debug.Log($"[DialogueCinematicController] Shot {shot.shotType}: Target={target.name}, CamPos={camPos}, TargetPos={basePos}");
            }

            return camPos;
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
        // Valores predefinidos (mismos que en DialogueCinematicProfile)
        const int baseLines = 2;  // Cambiar cada 2 líneas
        const int variation = 1;  // Variación de +/- 1 línea
        
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
    private void ApplyShotWithContext(CinematicCameraShot shot, bool isOpening = false, Transform targetOverride = null)
    {
        if (shot == null) return;
        
        // Determinar el target efectivo (quien habla)
        Transform effectiveTarget = targetOverride ?? currentNPC;
        
        // ✅ CORREGIDO: Determinar quién debe ser ocultado según quién habla
        bool isPlayerOrPartySpeaking = (effectiveTarget == currentPlayer) || (IsPartyMember(effectiveTarget) && effectiveTarget != currentNPC);
        bool isNPCSpeaking = effectiveTarget == currentNPC;
        
        // En conversaciones grupales: todos visibles siempre (player, NPC y party members)
        if (_isGroupConversation)
        {
            ShowNPC();
            ShowPlayer();
            ApplyShot(shot, effectiveTarget);
            return;
        }

        // Lógica de ocultación mejorada (solo en diálogos 1:1):
        // - Si habla el Player o Party Member → Ocultar al NPC para ver solo al speaker
        // - Si habla el NPC → Ocultar al Player SOLO en planos cerrados
        // - En planos Wide/OverShoulder → Mostrar ambos

        bool isCloseUpShot = (shot.shotType == DialogueShotType.CloseUpNPC ||
                             shot.shotType == DialogueShotType.MediumNPC);
        bool isWideShot = (shot.shotType == DialogueShotType.Wide ||
                          shot.shotType == DialogueShotType.OverShoulderPlayer ||
                          shot.shotType == DialogueShotType.OverShoulderNPC);

        if (isPlayerOrPartySpeaking)
        {
            // Cuando habla el Player o Party Member: SIEMPRE ocultar al NPC para evitar que salga en el encuadre
            HideNPC();
            ShowPlayer();

            // ✅ HACER QUE EL SPEAKER MIRE AL NPC
            MakeSpeakerLookAtNPC(effectiveTarget);

            if (showDebugInfo)
                Debug.Log($"[DialogueCinematicController] 👁️ Player/Party hablando → NPC oculto, Player visible");
        }
        else if (isNPCSpeaking)
        {
            // Cuando habla el NPC: Ocultar Player solo en planos cerrados
            ShowNPC();
            if (isCloseUpShot)
            {
                HidePlayer();
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] 👁️ NPC hablando (CloseUp) → Player oculto");
            }
            else
            {
                ShowPlayer();
                if (showDebugInfo)
                    Debug.Log($"[DialogueCinematicController] 👁️ NPC hablando (Wide) → Ambos visibles");
            }
        }
        else
        {
            // Caso genérico: mostrar ambos
            ShowNPC();
            ShowPlayer();
        }
        
        // Aplicar el plano cinematográfico
        ApplyShot(shot, effectiveTarget);
    }
    
    /// <summary>
    /// Verifica si un transform es un party member
    /// </summary>
    private bool IsPartyMember(Transform t)
    {
        if (!Game.NPC.PlayerParty.HasInstance) return false;
        
        var party = Game.NPC.PlayerParty.Instance;
        return party.Members.Any(m => m != null && m.transform == t);
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

