using UnityEngine;

/// <summary>
/// Bloquea la salida de una habitación hasta que el jugador haya iniciado al menos una misión.
/// Útil para forzar al jugador a interactuar con objetos críticos (como leer una carta) antes de continuar.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RoomExitBlocker : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Mensaje que se muestra cuando el jugador intenta salir sin haber leído la carta")]
    [SerializeField] private string blockedMessageKey = "ROOM_EXIT_BLOCKED";
    
    [Header("Mensaje por defecto (si no hay localización)")]
    [TextArea(3, 5)]
    [SerializeField] private string defaultBlockedMessage = "Debería revisar mi habitación antes de salir. Tal vez haya algo importante que leer...";
    
    [Header("Diálogo para mostrar el mensaje")]
    [Tooltip("Nombre del emisor del mensaje (aparecerá en el cuadro de diálogo)")]
    [SerializeField] private string messageSpeaker = "Pensamiento";
    
    [Header("Bloqueo Físico")]
    [Tooltip("Si está activado, el collider se vuelve sólido (no trigger) mientras esté bloqueado")]
    [SerializeField] private bool useSolidBlocker = true;
    
    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    
    private Collider _blockingCollider;
    private float _lastMessageTime;
    private const float MessageCooldown = 2f; // Evitar spam de mensajes
    private bool _isBlocked = true; // Estado del bloqueador
    
    private void Awake()
    {
        _blockingCollider = GetComponent<Collider>();
        UpdateBlockerState();
    }
    
    private void OnEnable()
    {
        // Suscribirse al evento del QuestManager
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged += OnQuestsChanged;
        }
    }
    
    private void OnDisable()
    {
        // Desuscribirse del evento del QuestManager
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestsChanged -= OnQuestsChanged;
        }
    }
    
    private void Start()
    {
        // Verificar el estado inicial después de que el QuestManager esté listo
        // Por si el QuestManager no estaba disponible en Awake
        if (QuestManager.Instance != null && !_hasSubscribed)
        {
            QuestManager.Instance.OnQuestsChanged += OnQuestsChanged;
            _hasSubscribed = true;
        }
        
        UpdateBlockerState();
    }
    
    private bool _hasSubscribed = false;
    
    /// <summary>
    /// Callback que se ejecuta cuando cambia el estado de las misiones.
    /// Mucho más eficiente que verificar cada frame.
    /// </summary>
    private void OnQuestsChanged()
    {
        bool shouldBeBlocked = !HasAnyActiveQuest();
        
        if (_isBlocked != shouldBeBlocked)
        {
            _isBlocked = shouldBeBlocked;
            UpdateBlockerState();
            
            if (debugLogs)
            {
                Debug.Log($"[RoomExitBlocker] Estado cambiado a: {(_isBlocked ? "BLOQUEADO" : "DESBLOQUEADO")}");
            }
        }
    }
    
    private void UpdateBlockerState()
    {
        if (_blockingCollider == null) return;
        
        if (useSolidBlocker)
        {
            // Cuando está bloqueado: collider sólido (no trigger)
            // Cuando está desbloqueado: collider trigger (no bloquea físicamente)
            _blockingCollider.isTrigger = !_isBlocked;
        }
        else
        {
            // Modo antiguo: siempre trigger
            _blockingCollider.isTrigger = true;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Solo se usa en modo trigger (cuando useSolidBlocker está desactivado o cuando está desbloqueado)
        if (!other.CompareTag("Player")) return;
        
        // Verificar si ya hay alguna misión activa
        if (HasAnyActiveQuest())
        {
            if (debugLogs)
                Debug.Log("[RoomExitBlocker] El jugador tiene misiones activas. Permitiendo salida.");
            return; // Permitir salir
        }
        
        // Mostrar mensaje (con cooldown para evitar spam)
        if (Time.time - _lastMessageTime > MessageCooldown)
        {
            ShowBlockedMessage();
            _lastMessageTime = Time.time;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        // Se usa cuando el collider es sólido (no trigger)
        if (!collision.gameObject.CompareTag("Player")) return;
        
        if (debugLogs)
            Debug.Log("[RoomExitBlocker] Jugador colisionó con el bloqueador (modo sólido)");
        
        // Mostrar mensaje (con cooldown para evitar spam)
        if (Time.time - _lastMessageTime > MessageCooldown)
        {
            ShowBlockedMessage();
            _lastMessageTime = Time.time;
        }
    }
    
    private bool HasAnyActiveQuest()
    {
        if (QuestManager.Instance == null)
        {
            if (debugLogs)
                Debug.LogWarning("[RoomExitBlocker] QuestManager no está disponible. Bloqueando salida por defecto.");
            return false;
        }
        
        // Verificar si hay alguna misión activa
        foreach (var quest in QuestManager.Instance.GetAll())
        {
            if (quest.State == QuestState.Active || quest.State == QuestState.Completed)
            {
                if (debugLogs)
                    Debug.Log($"[RoomExitBlocker] Misión encontrada: {quest.Id} - Estado: {quest.State}");
                return true;
            }
        }
        
        if (debugLogs)
            Debug.Log("[RoomExitBlocker] No hay misiones activas. Bloqueando salida.");
        
        return false;
    }
    
    private void ShowBlockedMessage()
    {
        if (DialogueManager.Instance == null)
        {
            if (debugLogs)
                Debug.LogWarning("[RoomExitBlocker] DialogueManager no disponible. No se puede mostrar el mensaje.");
            return;
        }
        
        // Verificar que las claves existan en el LocalizationManager
        if (LocalizationManager.Instance == null)
        {
            if (debugLogs)
                Debug.LogWarning("[RoomExitBlocker] LocalizationManager no disponible. No se puede mostrar el mensaje.");
            return;
        }
        
        // Verificar que la clave existe
        string messageTest = LocalizationManager.Instance.Get(blockedMessageKey, "");
        if (string.IsNullOrEmpty(messageTest))
        {
            Debug.LogError($"[RoomExitBlocker] La clave '{blockedMessageKey}' no existe en el LocalizationManager. " +
                         $"Asegúrate de que esté en el archivo ui_{{idioma}}.json y recarga la escena.");
            return;
        }
        
        // Crear un ScriptableObject temporal para el diálogo
        DialogueAsset tempDialogue = ScriptableObject.CreateInstance<DialogueAsset>();
        
        // Usar las claves de localización directamente
        tempDialogue.lines = new[]
        {
            new DialogueLine
            {
                speakerNameId = messageSpeaker,  // Usamos directamente la clave "Pensamiento"
                textId = blockedMessageKey,       // Usamos la clave "ROOM_EXIT_BLOCKED"
                portrait = null
            }
        };
        
        if (debugLogs)
        {
            Debug.Log($"[RoomExitBlocker] Mostrando diálogo con claves: speaker='{messageSpeaker}', text='{blockedMessageKey}'");
            Debug.Log($"[RoomExitBlocker] Texto resuelto: '{messageTest}'");
        }
        
        // Mostrar el diálogo
        DialogueManager.Instance.StartDialogue(tempDialogue, () =>
        {
            if (debugLogs)
                Debug.Log("[RoomExitBlocker] Mensaje de bloqueo cerrado.");
        });
    }
    
    private string GetLocalizedMessage()
    {
        // Intentar obtener mensaje localizado
        if (LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.Get(blockedMessageKey);
            if (!string.IsNullOrEmpty(localized))
                return localized;
        }
        
        // Usar mensaje por defecto si no hay localización
        return defaultBlockedMessage;
    }
    
    private void OnDrawGizmos()
    {
        // Visualizar el área de bloqueo en el editor
        bool blocked = Application.isPlaying ? _isBlocked : !HasAnyActiveQuest();
        Gizmos.color = blocked ? Color.red : Color.green;
        
        if (_blockingCollider != null)
        {
            // Dibujar el collider con el color apropiado
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (_blockingCollider is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
                if (blocked)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
                    Gizmos.DrawCube(box.center, box.size);
                }
            }
            else
            {
                Gizmos.DrawWireCube(Vector3.zero, transform.localScale);
            }
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
        
        // Dibujar flecha indicando dirección de bloqueo
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        Vector3 arrowStart = transform.position;
        Vector3 arrowEnd = transform.position - transform.forward * 2f;
        Gizmos.DrawLine(arrowStart, arrowEnd);
        Gizmos.DrawSphere(arrowEnd, 0.1f);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Mostrar información detallada cuando está seleccionado
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
    }
}
