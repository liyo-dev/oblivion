﻿using UnityEngine;
using System.Collections;

/// <summary>
/// Controla la cámara durante los diálogos para mostrar al player de perfil mirando al NPC
/// </summary>
public class DialogueCameraController : MonoBehaviour
{
    public static DialogueCameraController Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif

    [Header("Referencias")]
    [SerializeField] private vThirdPersonCamera thirdPersonCamera;

    [Header("Configuración de Cámara de Diálogo")]
    [Tooltip("Si está activo, mueve la cámara para mostrar al player de perfil")]
    [SerializeField] private bool enableDialogueCamera = true;
    
    [Tooltip("Distancia lateral de la cámara respecto al punto medio entre player y NPC")]
    [SerializeField] private float sideDistance = 1.8f;
    
    [Tooltip("Distancia hacia atrás desde la posición lateral")]
    [SerializeField] private float backDistance = 0.8f;
    
    [Tooltip("Altura de la cámara respecto al suelo")]
    [SerializeField] private float cameraHeight = 0.4f;
    
    [Tooltip("Altura del punto al que mira la cámara (altura de las caras)")]
    [SerializeField] private float lookAtHeight = 0.5f;
    
    [Tooltip("Velocidad de transición de la cámara")]
    [SerializeField] private float transitionSpeed = 2f;
    
    [Tooltip("Si está activo, hace que el player rote para mirar al NPC")]
    [SerializeField] private bool rotatePlayerToNPC = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    // Estado
    private bool isInDialogueMode = false;
    private Transform currentNPC = null;
    private Transform player = null;
    private Camera mainCamera = null;
    private Vector3 targetCameraPosition;
    private Quaternion targetCameraRotation;
    private Vector3 originalCameraLocalPosition;
    private Quaternion originalPlayerRotation;
    private bool wasLocked = false;
    private Coroutine transitionCoroutine;

    void Awake()
    {
        if (Instance != null) 
        { 
            Destroy(gameObject); 
            return; 
        }
        Instance = this;
    }

    void Start()
    {
        // Buscar la cámara si no está asignada
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        }
        
        if (thirdPersonCamera != null)
        {
            mainCamera = thirdPersonCamera.GetComponent<Camera>();
        }
    }

    /// <summary>
    /// Inicia el modo de cámara de diálogo mostrando al player de perfil mirando al NPC
    /// </summary>
    public void StartDialogueCamera(Transform npcTransform)
    {
        if (!enableDialogueCamera || npcTransform == null || isInDialogueMode) return;

        // Buscar el player usando PlayerService
        GameObject playerObj = PlayerService.Player;
        if (playerObj == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[DialogueCameraController] No se encontró el player.");
            return;
        }

        player = playerObj.transform;
        currentNPC = npcTransform;
        isInDialogueMode = true;

        // Buscar la cámara si aún no está asignada
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        }

        if (thirdPersonCamera == null)
        {
            if (showDebugLogs)
                Debug.LogWarning("[DialogueCameraController] No se encontró vThirdPersonCamera.");
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = thirdPersonCamera.GetComponent<Camera>();
        }

        // Guardar estado original
        wasLocked = thirdPersonCamera.lockCamera;
        if (mainCamera != null)
        {
            originalCameraLocalPosition = mainCamera.transform.localPosition;
        }
        originalPlayerRotation = player.rotation;
        
        // Bloquear la cámara para que el jugador no pueda moverla
        thirdPersonCamera.lockCamera = true;

        // Rotar el player hacia el NPC si está habilitado
        if (rotatePlayerToNPC)
        {
            Vector3 directionToNPC = (currentNPC.position - player.position).normalized;
            directionToNPC.y = 0; // Mantener rotación horizontal
            if (directionToNPC != Vector3.zero)
            {
                player.rotation = Quaternion.LookRotation(directionToNPC);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[DialogueCameraController] Iniciando cámara de diálogo con {npcTransform.name}");

        // Calcular y aplicar la posición de cámara lateral
        CalculateDialogueCameraTransform();
        
        // Iniciar transición suave
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);
        transitionCoroutine = StartCoroutine(TransitionToDialogueCamera());
    }

    /// <summary>
    /// Finaliza el modo de cámara de diálogo y vuelve al comportamiento normal
    /// </summary>
    public void EndDialogueCamera()
    {
        if (!isInDialogueMode) return;

        if (showDebugLogs)
            Debug.Log("[DialogueCameraController] Finalizando cámara de diálogo");

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.lockCamera = wasLocked;
        }

        // Restaurar posición local de la cámara
        if (mainCamera != null && transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = StartCoroutine(TransitionBackToNormal());
        }
        else
        {
            isInDialogueMode = false;
            currentNPC = null;
            player = null;
        }
    }

    private void CalculateDialogueCameraTransform()
    {
        if (player == null || currentNPC == null) return;

        // Dirección del player al NPC (en el plano horizontal)
        Vector3 directionToNPC = (currentNPC.position - player.position);
        directionToNPC.y = 0;
        directionToNPC.Normalize();

        // Perpendicular a la derecha para posicionar la cámara lateral
        Vector3 rightDirection = Vector3.Cross(Vector3.up, directionToNPC).normalized;

        // Punto medio entre player y NPC (en el suelo)
        Vector3 midPoint = (player.position + currentNPC.position) / 2f;
        
        // Posición de la cámara: a un lado del punto medio, un poco atrás
        Vector3 sidePosition = midPoint + rightDirection * sideDistance;
        Vector3 backOffset = -directionToNPC * backDistance;

        targetCameraPosition = sidePosition + backOffset;
        targetCameraPosition.y = player.position.y + cameraHeight; // Altura fija desde el suelo

        // La cámara mira hacia el punto medio entre el player y el NPC, a la altura de las caras
        Vector3 lookAtPoint = midPoint;
        lookAtPoint.y = player.position.y + lookAtHeight;

        targetCameraRotation = Quaternion.LookRotation(lookAtPoint - targetCameraPosition);

        if (showDebugLogs)
        {
            Debug.DrawLine(player.position, currentNPC.position, Color.yellow, 2f);
            Debug.DrawLine(targetCameraPosition, lookAtPoint, Color.cyan, 2f);
        }
    }

    private IEnumerator TransitionToDialogueCamera()
    {
        if (mainCamera == null) yield break;

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        float elapsed = 0f;
        float duration = 1f / transitionSpeed;

        while (elapsed < duration && isInDialogueMode)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            mainCamera.transform.position = Vector3.Lerp(startPos, targetCameraPosition, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetCameraRotation, t);

            yield return null;
        }

        if (isInDialogueMode)
        {
            mainCamera.transform.position = targetCameraPosition;
            mainCamera.transform.rotation = targetCameraRotation;
        }

        transitionCoroutine = null;
    }

    private IEnumerator TransitionBackToNormal()
    {
        if (mainCamera == null)
        {
            isInDialogueMode = false;
            currentNPC = null;
            player = null;
            yield break;
        }

        Vector3 startPos = mainCamera.transform.position;
        Quaternion startRot = mainCamera.transform.rotation;

        // Calcular posición de retorno basada en la posición original del player
        Vector3 targetPos = player.position - player.forward * 3f + Vector3.up * 2f;
        Quaternion targetRot = Quaternion.LookRotation(player.position - targetPos);

        float elapsed = 0f;
        float duration = 0.8f / transitionSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            mainCamera.transform.position = Vector3.Lerp(startPos, targetPos, t);
            mainCamera.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            yield return null;
        }

        isInDialogueMode = false;
        currentNPC = null;
        player = null;
        transitionCoroutine = null;
    }

    void LateUpdate()
    {
        // Mantener la cámara en posición de diálogo mientras esté activa
        if (isInDialogueMode && currentNPC != null && player != null && mainCamera != null && transitionCoroutine == null)
        {
            // Recalcular por si el player o NPC se movieron
            CalculateDialogueCameraTransform();
            
            // Aplicar con suavizado ligero para compensar movimientos pequeños
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetCameraPosition, Time.deltaTime * 5f);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, targetCameraRotation, Time.deltaTime * 5f);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (isInDialogueMode && currentNPC != null && player != null)
        {
            // Visualizar la configuración de la cámara
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(player.position + Vector3.up, currentNPC.position + Vector3.up);
            
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(targetCameraPosition, 0.3f);
            Gizmos.DrawLine(targetCameraPosition, (player.position + currentNPC.position) / 2f + Vector3.up * 1.5f);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(player.position + Vector3.up * 1.5f, 0.2f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(currentNPC.position + Vector3.up * 1.5f, 0.2f);
        }
    }
}
