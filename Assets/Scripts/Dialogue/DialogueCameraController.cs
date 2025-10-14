using UnityEngine;
using System.Collections;

/// <summary>
/// Controla la cámara durante los diálogos para mostrar al player de perfil mirando al NPC
/// </summary>
public class DialogueCameraController : MonoBehaviour
{
    public static DialogueCameraController Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private vThirdPersonCamera thirdPersonCamera;

    [Header("Configuración de Cámara de Diálogo")]
    [Tooltip("Si está activo, mueve la cámara para mostrar al player de perfil")]
    [SerializeField] private bool enableDialogueCamera = true;
    
    [Tooltip("Distancia lateral de la cámara respecto al player")]
    [SerializeField] private float sideDistance = 2.5f;
    
    [Tooltip("Distancia hacia atrás desde la posición lateral")]
    [SerializeField] private float backDistance = 1.5f;
    
    [Tooltip("Altura adicional de la cámara")]
    [SerializeField] private float heightOffset = 0.3f;
    
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
            thirdPersonCamera = FindFirstObjectByType<vThirdPersonCamera>();
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

        // Buscar el player
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
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
            thirdPersonCamera = FindFirstObjectByType<vThirdPersonCamera>();
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

        // Posición de la cámara: a un lado del player, un poco atrás y elevada
        Vector3 sidePosition = player.position + rightDirection * sideDistance;
        Vector3 backOffset = -directionToNPC * backDistance;
        Vector3 heightPosition = Vector3.up * (player.position.y + heightOffset + 1.5f);

        targetCameraPosition = sidePosition + backOffset;
        targetCameraPosition.y = heightPosition.y;

        // La cámara mira hacia el punto medio entre el player y el NPC
        Vector3 lookAtPoint = (player.position + currentNPC.position) / 2f;
        lookAtPoint.y += 1.5f; // A la altura de las caras

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
