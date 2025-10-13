using UnityEngine;

/// <summary>
/// Componente que permite entregar un item específico a un NPC para completar un paso de quest.
/// Se coloca en el mismo GameObject que SimpleQuestNPC.
/// </summary>
public class QuestItemDelivery : MonoBehaviour
{
    [Header("Configuración de Entrega")]
    [Tooltip("ID de la quest que requiere la entrega")]
    [SerializeField] private string questId;
    
    [Tooltip("Índice del paso que se completa al entregar (0 por defecto)")]
    [SerializeField] private int deliveryStepIndex;
    
    [Tooltip("Tag del item que se debe entregar (ej: 'QuestItem', 'Package')")]
    [SerializeField] private string requiredItemTag = "QuestItem";
    
    [Tooltip("Nombre del item para mostrar en mensajes (opcional)")]
    [SerializeField] private string itemDisplayName = "la caja";
    
    [Header("Feedback")]
    [Tooltip("Mensaje cuando el jugador tiene el item")]
    [SerializeField] private string hasItemMessage = "Tienes {item} para entregar";
    
    [Tooltip("Mensaje cuando el jugador NO tiene el item")]
    [SerializeField] private string needsItemMessage = "Necesitas encontrar {item} primero";
    
    [Tooltip("Mensaje cuando se entrega el item")]
    [SerializeField] private string deliveredMessage = "¡{item} entregada!";
    
    [Header("Referencias (Opcional)")]
    [SerializeField] private DialogueAsset dialogueWhenHasItem;
    [SerializeField] private DialogueAsset dialogueWhenNeedsItem;
    [SerializeField] private DialogueAsset dialogueAfterDelivery;
    
    private SimpleQuestNPC _questNpc;
    
    void Awake()
    {
        _questNpc = GetComponent<SimpleQuestNPC>();
        if (_questNpc == null)
        {
            Debug.LogError($"[QuestItemDelivery] {name} necesita un componente SimpleQuestNPC.");
        }
    }
    
    /// <summary>
    /// Método público para verificar y entregar el item.
    /// Se puede llamar desde SimpleQuestNPC.Interact() o desde un Unity Event.
    /// </summary>
    public void TryDeliverItem()
    {
        var qm = QuestManager.Instance;
        if (qm == null)
        {
            Debug.LogWarning("[QuestItemDelivery] QuestManager no disponible");
            return;
        }
        
        // Verificar si la quest está activa
        var questState = qm.GetState(questId);
        if (questState != QuestState.Active)
        {
            Debug.Log($"[QuestItemDelivery] Quest '{questId}' no está activa");
            return;
        }
        
        // Verificar si el jugador tiene el item
        if (PlayerHasRequiredItem())
        {
            DeliverItem(qm);
        }
        else
        {
            ShowNeedsItemFeedback();
        }
    }
    
    /// <summary>
    /// Verifica si el jugador tiene el item requerido en su inventario
    /// </summary>
    private bool PlayerHasRequiredItem()
    {
        // Buscar en la jerarquía del jugador si tiene un objeto con el tag requerido
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;
        
        // Buscar en hijos del jugador (inventario, bolsa, etc.)
        var itemsInPlayer = player.GetComponentsInChildren<Transform>();
        foreach (var item in itemsInPlayer)
        {
            if (item.CompareTag(requiredItemTag))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Entrega el item y marca el paso de quest como completado
    /// </summary>
    private void DeliverItem(QuestManager qm)
    {
        // Marcar el paso como completado
        qm.MarkStepDone(questId, deliveryStepIndex);
        
        // Destruir el item del inventario del jugador
        RemoveItemFromPlayer();
        
        // Mostrar feedback
        ShowDeliveredFeedback();
        
        Debug.Log($"[QuestItemDelivery] Item '{itemDisplayName}' entregado. Paso {deliveryStepIndex} de quest '{questId}' completado.");
        
        // Si todos los pasos están completados, el SimpleQuestNPC se encargará de completar la quest
    }
    
    /// <summary>
    /// Elimina el item del jugador
    /// </summary>
    private void RemoveItemFromPlayer()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        // Buscar y destruir el item
        var itemsInPlayer = player.GetComponentsInChildren<Transform>();
        foreach (var item in itemsInPlayer)
        {
            if (item.CompareTag(requiredItemTag))
            {
                Destroy(item.gameObject);
                Debug.Log($"[QuestItemDelivery] Item '{item.name}' eliminado del jugador");
                break; // Solo eliminar el primero que encuentre
            }
        }
    }
    
    /// <summary>
    /// Muestra feedback cuando el jugador tiene el item
    /// </summary>
    private void ShowHasItemFeedback()
    {
        string message = hasItemMessage.Replace("{item}", itemDisplayName);
        Debug.Log($"[QuestItemDelivery] {message}");
        
        if (dialogueWhenHasItem != null)
        {
            PlayDialogue(dialogueWhenHasItem);
        }
        else
        {
            ShowMessage(message);
        }
    }
    
    /// <summary>
    /// Muestra feedback cuando el jugador NO tiene el item
    /// </summary>
    private void ShowNeedsItemFeedback()
    {
        string message = needsItemMessage.Replace("{item}", itemDisplayName);
        Debug.Log($"[QuestItemDelivery] {message}");
        
        if (dialogueWhenNeedsItem != null)
        {
            PlayDialogue(dialogueWhenNeedsItem);
        }
        else
        {
            ShowMessage(message);
        }
    }
    
    /// <summary>
    /// Muestra feedback cuando se entrega el item
    /// </summary>
    private void ShowDeliveredFeedback()
    {
        string message = deliveredMessage.Replace("{item}", itemDisplayName);
        Debug.Log($"[QuestItemDelivery] {message}");
        
        if (dialogueAfterDelivery != null)
        {
            PlayDialogue(dialogueAfterDelivery);
        }
        else
        {
            ShowMessage(message);
        }
    }
    
    /// <summary>
    /// Reproduce un diálogo si hay DialogueManager disponible
    /// </summary>
    private void PlayDialogue(DialogueAsset dialogue)
    {
        var dialogueManager = FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null && dialogue != null)
        {
            dialogueManager.StartDialogue(dialogue);
        }
    }
    
    /// <summary>
    /// Muestra un mensaje simple (puedes conectarlo con tu sistema de UI)
    /// </summary>
    private void ShowMessage(string message)
    {
        // Aquí puedes integrar con tu sistema de notificaciones/UI
        // Por ahora solo usa Debug.Log
        Debug.Log($"[QuestItemDelivery] 💬 {message}");
    }
    
    /// <summary>
    /// Verifica el estado del item para debugging
    /// </summary>
    public void CheckItemStatus()
    {
        bool hasItem = PlayerHasRequiredItem();
        Debug.Log($"[QuestItemDelivery] Jugador tiene '{itemDisplayName}': {hasItem}");
    }
}
