using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controlador cinematográfico avanzado para diálogos usando Cinemachine.
/// Gestiona múltiples cámaras virtuales y transiciones entre ellas.
/// </summary>
public class DialogueCinematicController : MonoBehaviour
{
    public static DialogueCinematicController Instance { get; private set; }

    [Header("Configuración")]
    [SerializeField] private DialogueCinematicProfile defaultProfile;
    [SerializeField] private bool showDebugInfo;
    [SerializeField] private int maxPooledCameras = 8;

    // Estado actual
    private bool isInCinematicMode;
    private Transform currentPlayer;
    private Transform currentNPC;
    private DialogueCinematicProfile activeProfile;
    private CinemachineCamera currentVirtualCamera;
    private int currentLineIndex;
    private int nextCutAtLine;

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
    private string originalDialogueCameraTag;
    
    // Referencia a la cámara principal de gameplay de Unity (no la virtual de Cinemachine)
    private Camera mainUnityCamera;
    private bool mainUnityCameraWasEnabled;
    
    // Ocultación del player durante cinematográficas
    private Renderer[] playerRenderers;
    private bool[] playerRenderersWereEnabled;

    void Awake()
    {
        Debug.Log($"[DialogueCinematicController] ⚡ Awake iniciado en GameObject: {gameObject.name}");
        
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
        Debug.Log($"[DialogueCinematicController] ✅ Instancia marcada como DontDestroyOnLoad");

        // Crear rig para las cámaras de diálogo
        GameObject rigObj = new GameObject("DialogueCinematicRig");
        rigObj.transform.SetParent(transform);
        cameraRig = rigObj.transform;

        // Crear cámara dedicada para diálogos (separada de Camera.main)
        CreateDialogueCamera();
        
        Debug.Log($"[DialogueCinematicController] Sistema inicializado - DialogueCamera: {(dialogueCameraObject != null ? "OK" : "NULL")}");
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
                Debug.Log($"[DialogueCinematicController] URP Camera Data configurada - RenderType: Base, PostProcessing: {dialogueCameraData.renderPostProcessing}");
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
        
        Debug.Log($"[DialogueCinematicController] ✅ Cámara de diálogo creada - GameObject: {dialogueCameraObject.activeSelf}, Camera.enabled: {dialogueCamera.enabled}, Depth: {dialogueCamera.depth}, CullingMask incluye UI: {((dialogueCamera.cullingMask & (1 << LayerMask.NameToLayer("UI"))) != 0)}");
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
            Debug.Log($"[DialogueCinematicController] Cámara de gameplay encontrada: {mainGameplayCamera.name} (Priority original: {originalGameplayCameraPriority})");
        }
        else
        {
            Debug.LogWarning("[DialogueCinematicController] No se encontró una CinemachineCamera de gameplay en la escena");
        }
        
        // Desactivar todas las cámaras del pool inicialmente
        DisableAllDialogueCameras();
        
        Debug.Log("[DialogueCinematicController] ✅ Start completado - Sistema listo");
    }

    void OnDestroy()
    {
        // Limpiar la instancia del Singleton si es esta instancia
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("[DialogueCinematicController] Instancia del Singleton limpiada");
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
        public void StartCinematic(Transform player, Transform npc, DialogueCinematicProfile profile = null)
        {
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
            Debug.Log($"[DialogueCinematicController] Cámara principal de Unity ({mainUnityCamera.name}) DESACTIVADA temporalmente para evitar conflictos URP");
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
            
            // Asignar tag MainCamera
            originalDialogueCameraTag = dialogueCameraObject.tag;
            dialogueCameraObject.tag = "MainCamera";
            
            // Activar rendering forzado
            forceRenderingActive = true;

            Debug.Log($"[DialogueCinematicController] Componente Camera activado (era: {wasEnabled}, ahora: {dialogueCamera.enabled}, isActiveAndEnabled: {dialogueCamera.isActiveAndEnabled})");
            Debug.Log($"[DialogueCinematicController] Camera details - Depth: {dialogueCamera.depth}, ClearFlags: {dialogueCamera.clearFlags}, CullingMask: {dialogueCamera.cullingMask}, TargetTexture: {(dialogueCamera.targetTexture == null ? "NULL (pantalla)" : dialogueCamera.targetTexture.name)}, Tag: {dialogueCameraObject.tag}");
        }
        else
        {
            Debug.LogError("[DialogueCinematicController] ❌ dialogueCamera es NULL - no se puede activar!");
        }

        // PASO 5: Activar el plano de apertura
        ApplyShotWithContext(activeProfile.openingShot, true);
    }

        /// <summary>
        /// Finaliza el modo cinematográfico
        /// </summary>
        public void EndCinematic()
        {
            if (!isInCinematicMode) return;

            if (showDebugInfo)
                Debug.Log("[DialogueCinematicController] Finalizando cinematográfica");

        // Desactivar todas las cámaras virtuales del pool
        DisableAllDialogueCameras();

        // Desactivar la cámara de diálogo dedicada (solo el componente Camera)
        if (dialogueCamera != null)
        {
            bool wasEnabled = dialogueCamera.enabled;
            dialogueCamera.enabled = false;
            
            // Restaurar tag original
            if (!string.IsNullOrEmpty(originalDialogueCameraTag))
            {
                dialogueCameraObject.tag = originalDialogueCameraTag;
            }
            
            // Desactivar rendering forzado
            forceRenderingActive = false;
            
            Debug.Log($"[DialogueCinematicController] Componente Camera desactivado (era: {wasEnabled}, ahora: {dialogueCamera.enabled})");
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
            Debug.Log($"[DialogueCinematicController] Cámara principal de Unity REACTIVADA");
        }
        mainUnityCamera = null;
        
        // Restaurar visibilidad del player
        ShowPlayer();
        
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

        /// <summary>
        /// Notifica que ha avanzado una línea de diálogo
        /// </summary>
        public void OnDialogueLineAdvanced(int lineIndex, int totalLines)
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
                return;
            }

            // Determinar si es momento de cortar
            bool shouldCut = false;

            if (activeProfile.enableAutomaticCuts)
            {
                shouldCut = lineIndex >= nextCutAtLine;
            }

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
            Vector3 lookAtPos = target.position + shot.lookAtOffset;

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

            // Aplicar rotación base mirando al objetivo
            vcam.transform.LookAt(lookAtPos);

            // Aplicar ángulos adicionales
            vcam.transform.Rotate(Vector3.right, shot.verticalAngle, Space.Self);
            vcam.transform.Rotate(Vector3.forward, shot.dutchAngle, Space.Self);

            // Configurar lens (FOV)
            vcam.Lens.FieldOfView = shot.fieldOfView;

            // Configurar blend time en el brain de diálogo
            if (dialogueBrain != null)
            {
                dialogueBrain.DefaultBlend.Time = activeProfile.blendDuration;
            }
            
            // Asegurar que las cámaras virtuales estén en el canal correcto
            vcam.OutputChannel = (OutputChannels)1; // Canal 1 para diálogos
        }

        /// <summary>
        /// Calcula la posición de la cámara según el tipo de plano
        /// </summary>
        private Vector3 CalculateCameraPosition(CinematicCameraShot shot, Transform target)
        {
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
            
            // ✅ AJUSTE: Aumentar distancia mínima para alejar todos los planos
            float effectiveDistance = Mathf.Max(shot.distance * 1.5f, 2.0f); // Multiplicador 1.5x y mínimo de 2m

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
                        float wideDistance = Mathf.Max(shot.distance * 1.8f, 4.5f); // Más alejado: mínimo 4.5m
                        float lateralAmount = wideDistance * 0.4f; // 40% lateral hacia la izquierda
                        float backAmount = wideDistance * 0.6f; // 60% hacia atrás
                        
                        camPos = midPoint + (-sideDir * lateralAmount) + (backDir * backAmount);
                        camPos.y = midPoint.y + shot.height;
                    }
                    break;

                case DialogueShotType.OverShoulderPlayer:
                    // Cámara detrás del Player mirando hacia el NPC - MEJORADO
                    {
                        Vector3 playerToNPC = (currentNPC.position - currentPlayer.position).normalized;
                        playerToNPC.y = 0;
                        if (playerToNPC.sqrMagnitude < 0.01f) playerToNPC = currentPlayer.forward;
                        playerToNPC.Normalize();
                        
                        // ✅ MEJORADO: Offset lateral más pronunciado para evitar bloquear la vista con la cabeza
                        // Usar lateralOffset del shot o un valor por defecto mejorado
                        float lateralOffsetAmount = shot.lateralOffset != 0 ? shot.lateralOffset : 0.9f; // Aumentado de 0.5f a 0.9f
                        Vector3 shoulderOffset = Vector3.Cross(Vector3.up, playerToNPC).normalized * lateralOffsetAmount;
                        
                        // ✅ MEJORADO: Retroceder MÁS de la posición del player para mejor encuadre
                        // Mínimo 1.8m, o 50% de la distancia entre personajes
                        float behindDistance = Mathf.Max(1.8f, actualDistance * 0.5f); // Aumentado de 1.2f y 0.4f
                        
                        // Posición base: atrás del player + offset lateral
                        camPos = currentPlayer.position - playerToNPC * behindDistance + shoulderOffset;
                        
                        // ✅ MEJORADO: Altura más elevada para mejor vista sobre el hombro
                        // Añadir 0.3m adicionales a la altura configurada para ver mejor por encima
                        camPos.y = currentPlayer.position.y + shot.height + 0.3f;
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
                        float lateralOffsetAmount = shot.lateralOffset != 0 ? shot.lateralOffset : 0.9f; // Aumentado de 0.5f a 0.9f
                        Vector3 shoulderOffset = Vector3.Cross(Vector3.up, npcToPlayer).normalized * lateralOffsetAmount;
                        
                        // ✅ MEJORADO: Retroceder MÁS de la posición del NPC
                        float behindDistance = Mathf.Max(1.8f, actualDistance * 0.5f); // Aumentado de 1.2f y 0.4f
                        
                        camPos = currentNPC.position - npcToPlayer * behindDistance + shoulderOffset;
                        
                        // ✅ MEJORADO: Altura más elevada
                        camPos.y = currentNPC.position.y + shot.height + 0.3f;
                    }
                    break;

                case DialogueShotType.Profile:
                    // Vista lateral - perpendicular a la línea entre personajes - más alejada
                    Vector3 perpendicular = Vector3.Cross(Vector3.up, fromOther).normalized;
                    camPos = basePos + perpendicular * effectiveDistance;
                    camPos.y = basePos.y + shot.height;
                    break;

                case DialogueShotType.MediumNPC:
                case DialogueShotType.CloseUpNPC:
                    // Cámara FRENTE al NPC (desde la posición del Player hacia el NPC)
                    {
                        // ✅ CORREGIDO: Dirección desde NPC hacia Player (para colocar cámara del lado del player)
                        Vector3 npcToPlayer = (currentPlayer.position - currentNPC.position).normalized;
                        npcToPlayer.y = 0;
                        if (npcToPlayer.sqrMagnitude < 0.01f) npcToPlayer = currentNPC.forward;
                        npcToPlayer.Normalize();
                        
                        // Offset lateral para variedad visual
                        Vector3 lateral = Vector3.Cross(Vector3.up, npcToPlayer).normalized * shot.lateralOffset;
                        
                        // ✅ CORREGIDO: Posicionar cámara en dirección del Player (frente al NPC)
                        // Esto coloca la cámara entre el Player y el NPC, mirando hacia el NPC
                        camPos = currentNPC.position + npcToPlayer * effectiveDistance + lateral;
                        camPos.y = currentNPC.position.y + shot.height;
                    }
                    break;

                default:
                    // Fallback: frente al target
                    camPos = basePos + fromOther * effectiveDistance;
                    camPos.y = basePos.y + shot.height;
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
        int baseLines = activeProfile.linesBetweenCuts;
        int variation = Random.Range(-activeProfile.cutTimingVariation, activeProfile.cutTimingVariation + 1);
        int nextCut = currentLineIndex + baseLines + variation;
        return Mathf.Max(currentLineIndex + 1, nextCut);
    }

    /// <summary>
    /// Aplica un plano cinematográfico con contexto (útil para plano de apertura)
    /// </summary>
    private void ApplyShotWithContext(CinematicCameraShot shot, bool isOpening = false)
    {
        if (shot == null) return;
        
        // Solo ocultar al player en planos de primer plano y medio plano del NPC
        // En planos amplios (Wide), Over-the-shoulder y Profile, el player debe verse
        // de lo contrario parece que el NPC habla con la nada
        bool shouldHidePlayer = shot.shotType == DialogueShotType.CloseUpNPC || shot.shotType == DialogueShotType.MediumNPC;
        
        if (shouldHidePlayer)
        {
            HidePlayer();
        }
        else
        {
            ShowPlayer();
        }
        
        // Para Wide shots o plano de apertura, usar lógica especial
        if (isOpening || shot.shotType == DialogueShotType.Wide)
        {
            ApplyShot(shot, currentNPC); // Usa NPC como referencia pero la cámara se posicionará en medio
        }
        else
        {
            // Para otros planos, usar el target apropiado
            Transform target = currentNPC; // Por defecto el NPC
            ApplyShot(shot, target);
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

