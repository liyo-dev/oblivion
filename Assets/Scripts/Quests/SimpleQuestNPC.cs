using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SimpleQuestNPC : MonoBehaviour
{
    public enum QuestCompletionMode
    {
        Manual,                    // Requiere completar pasos manualmente
        AutoCompleteOnTalk,        // Se completa automáticamente al hablar
        CompleteOnTalkIfStepsReady // Solo se completa al hablar si todos los pasos están listos
    }

    [System.Serializable]
    public class QuestChainEntry
    {
        [Tooltip("Quest de esta etapa de la cadena")]
        public QuestData questData;
        
        [Tooltip("Modo de completado de esta quest")]
        public QuestCompletionMode completionMode = QuestCompletionMode.Manual;
        
        [Tooltip("Índice del paso donde se habla con este NPC (0 por defecto)")]
        public int talkStepIndex;
        
        [Header("Detección de Items")]
        [Tooltip("Si está activado, detecta automáticamente cuando el jugador suelta items en el rango")]
        public bool autoDetectItemDelivery;
        
        [Tooltip("Índice del paso que se completa al detectar el item")]
        public int itemDeliveryStepIndex = 1;
        
        [Tooltip("Tag del item a detectar (ej: 'QuestItem')")]
        public string itemTag = "QuestItem";
        
        [Header("Diálogos de esta Quest")]
        public DialogueAsset dlgBefore;
        public DialogueAsset dlgInProgress;
        public DialogueAsset dlgTurnIn;
        public DialogueAsset dlgCompleted;
        
        [Header("Eventos")]
        [Tooltip("Evento que se ejecuta cuando esta quest se completa")]
        public UnityEvent OnQuestCompleted;
    }

    [Header("Cadena de Quests")]
    [SerializeField] private List<QuestChainEntry> questChain = new List<QuestChainEntry>();

    [Header("Detección de Items")]
    [Tooltip("Radio de detección para items soltados")]
    [SerializeField] private float detectionRadius = 3f;
    
    [Tooltip("Ángulo de visión para detectar items (0-180)")]
    [SerializeField] private float detectionAngle = 90f;
    
    [Tooltip("Frecuencia de detección (segundos)")]
    [SerializeField] private float detectionInterval = 0.5f;
    
    [Tooltip("Mostrar gizmos de detección en la escena")]
    [SerializeField] private bool showDetectionGizmos = true;

    private Interactable _interactable;
    private bool _isChaining = false; // Flag para evitar parpadeo del hint durante encadenamiento
    private HashSet<GameObject> _detectedItems = new HashSet<GameObject>();

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
        if (_interactable == null)
        {
            Debug.LogError($"[SimpleQuestNPC] {name} necesita un componente Interactable.");
        }
    }

    void Start()
    {
        // Iniciar detección periódica de items
        StartCoroutine(DetectItemsRoutine());
    }

    public void Interact()
    {
        var qm = QuestManager.Instance;
        if (qm == null || questChain == null || questChain.Count == 0) return;

        int activeQuestIndex = FindActiveQuestIndex(qm);
        
        if (activeQuestIndex >= 0)
        {
            var entry = questChain[activeQuestIndex];
            HandleQuestState(entry, qm, activeQuestIndex);
        }
        else
        {
            if (questChain.Count > 0)
            {
                var entry = questChain[0];
                PlayDialogue(entry.dlgBefore);
            }
        }
    }

    /// <summary>
    /// Inicia una quest por ID. Útil para Unity Events.
    /// </summary>
    /// <param name="questId">ID de la quest a iniciar</param>
    public void StartQuestById(string questId)
    {
        var qm = QuestManager.Instance;
        if (qm == null)
        {
            Debug.LogError($"[SimpleQuestNPC] QuestManager no disponible");
            return;
        }

        if (string.IsNullOrEmpty(questId))
        {
            Debug.LogError($"[SimpleQuestNPC] Quest ID vacío");
            return;
        }

        // Buscar la quest en la cadena
        var entry = questChain.FirstOrDefault(e => e.questData != null && e.questData.questId == questId);
        if (entry?.questData == null)
        {
            Debug.LogError($"[SimpleQuestNPC] Quest '{questId}' no encontrada en la cadena de {name}");
            return;
        }

        // Agregar y activar la quest
        qm.AddQuest(entry.questData);
        qm.StartQuest(questId);
        
        Debug.Log($"[SimpleQuestNPC] Quest '{questId}' iniciada por Unity Event");

        // SOLO mostrar diálogo de oferta si está configurado, NO procesar la quest automáticamente
        if (entry.dlgBefore != null)
        {
            PlayDialogue(entry.dlgBefore);
        }
    }

    private int FindActiveQuestIndex(QuestManager qm)
    {
        for (int i = questChain.Count - 1; i >= 0; i--)
        {
            var entry = questChain[i];
            if (entry.questData == null) continue;
            
            var state = qm.GetState(entry.questData.questId);
            if (state == QuestState.Active || state == QuestState.Completed)
            {
                return i;
            }
        }
        return -1;
    }

    private void HandleQuestState(QuestChainEntry entry, QuestManager qm, int currentIndex)
    {
        if (entry.questData == null) return;
        
        var state = qm.GetState(entry.questData.questId);
        string questId = entry.questData.questId;

        switch (state)
        {
            case QuestState.Inactive:
                PlayDialogue(entry.dlgBefore);
                break;

            case QuestState.Active:
                HandleActiveQuest(entry, qm, questId, currentIndex);
                break;

            case QuestState.Completed:
                PlayDialogue(entry.dlgCompleted);
                break;
        }
    }

    private void HandleActiveQuest(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        switch (entry.completionMode)
        {
            case QuestCompletionMode.AutoCompleteOnTalk:
                CompleteQuestAutomatically(entry, qm, questId, currentIndex);
                break;

            case QuestCompletionMode.CompleteOnTalkIfStepsReady:
                CompleteQuestIfReady(entry, qm, questId, currentIndex);
                break;

            case QuestCompletionMode.Manual:
            default:
                CompleteQuestManually(entry, qm, questId, currentIndex);
                break;
        }
    }

    private void CompleteQuestAutomatically(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        // Completar todos los pasos
        var runtimeQuest = qm.GetAll().FirstOrDefault(rq => rq.Id == questId);
        if (runtimeQuest != null)
        {
            for (int i = 0; i < runtimeQuest.Steps.Length; i++)
            {
                if (!runtimeQuest.Steps[i].completed)
                {
                    qm.MarkStepDone(questId, i);
                }
            }
        }

        qm.CompleteQuest(questId);
        Debug.Log($"[SimpleQuestNPC] Quest {questId} completada automáticamente.");
        
        // Ejecutar evento de quest completada
        entry.OnQuestCompleted?.Invoke();

        // Encadenar con la siguiente quest si existe
        ChainToNextQuest(entry, qm, questId, currentIndex);
    }

    private void CompleteQuestIfReady(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        if (qm.AreAllStepsCompleted(questId))
        {
            qm.CompleteQuest(questId);
            
            // Ejecutar evento de quest completada
            entry.OnQuestCompleted?.Invoke();
            
            // Encadenar con la siguiente quest
            ChainToNextQuest(entry, qm, questId, currentIndex);
        }
        else
        {
            PlayDialogue(entry.dlgInProgress);
        }
    }

    private void CompleteQuestManually(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        // En modo Manual, solo mostramos el diálogo correspondiente según el estado
        // NO autocompletamos la quest, solo marcamos el paso de hablar
        
        if (qm.AreAllStepsCompleted(questId))
        {
            // Todos los pasos están completados manualmente por el jugador
            qm.CompleteQuest(questId);
            
            // Ejecutar evento de quest completada
            entry.OnQuestCompleted?.Invoke();
            
            // Encadenar con la siguiente quest
            ChainToNextQuest(entry, qm, questId, currentIndex);
        }
        else
        {
            // Faltan pasos por completar manualmente
            PlayDialogue(entry.dlgInProgress);
        }
    }

    /// <summary>
    /// Encadena automáticamente con la siguiente quest en la cadena
    /// </summary>
    private void ChainToNextQuest(QuestChainEntry completedEntry, QuestManager qm, string completedQuestId, int currentIndex)
    {
        // Marcar que estamos encadenando para evitar parpadeo del hint
        _isChaining = true;
        
        // Mostrar diálogo de Turn In
        PlayDialogue(completedEntry.dlgTurnIn, () =>
        {
            // Usar coroutine para dar un frame al DialogueManager antes de abrir el siguiente diálogo
            StartCoroutine(StartNextQuestAfterDelay(qm, currentIndex));
        });
    }

    private IEnumerator StartNextQuestAfterDelay(QuestManager qm, int currentIndex)
    {
        // Esperar un frame para que el DialogueManager termine de procesar el cierre
        yield return null;

        // Iniciar la siguiente quest
        int nextIndex = currentIndex + 1;
        if (nextIndex < questChain.Count)
        {
            var nextEntry = questChain[nextIndex];
            if (nextEntry.questData != null)
            {
                // Agregar y activar la siguiente quest
                qm.AddQuest(nextEntry.questData);
                qm.StartQuest(nextEntry.questData.questId);
                
                Debug.Log($"[SimpleQuestNPC] Encadenando a siguiente quest: {nextEntry.questData.questId}");
                
                // Mostrar el diálogo de oferta de la siguiente quest
                if (nextEntry.dlgBefore != null)
                {
                    PlayDialogue(nextEntry.dlgBefore, () =>
                    {
                        // Termina el encadenamiento
                        _isChaining = false;
                    });
                }
                else
                {
                    _isChaining = false;
                }
            }
            else
            {
                _isChaining = false;
            }
        }
        else
        {
            // No hay más quests en la cadena
            _isChaining = false;
        }
    }

    private void PlayDialogue(DialogueAsset dlg, System.Action onComplete = null)
    {
        if (dlg == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (_interactable != null)
        {
            _interactable.SetDialogue(dlg);
            
            // SIEMPRE disparar OnStarted para que el NPC se gire hacia el jugador
            _interactable.OnStarted?.Invoke();
            
            DialogueManager.Instance.StartDialogue(dlg, () =>
            {
                // SIEMPRE disparar OnFinished para que el NPC vuelva a su estado normal
                _interactable.OnFinished?.Invoke();
                onComplete?.Invoke();
            });
        }
        else
        {
            DialogueManager.Instance.StartDialogue(dlg, onComplete);
        }
    }

    #region Item Detection

    private IEnumerator DetectItemsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(detectionInterval);
            CheckForItemsInRange();
        }
    }

    private void CheckForItemsInRange()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        // Buscar quest activa que requiera detección de items
        int activeQuestIndex = FindActiveQuestIndex(qm);
        if (activeQuestIndex < 0) return;

        var entry = questChain[activeQuestIndex];
        if (!entry.autoDetectItemDelivery) return;

        var questState = qm.GetState(entry.questData.questId);
        if (questState != QuestState.Active) return;

        // Buscar objetos con el tag especificado en el rango
        var itemsInScene = GameObject.FindGameObjectsWithTag(entry.itemTag);
        
        foreach (var item in itemsInScene)
        {
            // Evitar detectar el mismo item múltiples veces
            if (_detectedItems.Contains(item)) continue;

            // Verificar si está en el rango
            if (IsItemInDetectionRange(item))
            {
                // Verificar si el item NO está siendo sostenido por el jugador
                if (!IsItemHeldByPlayer(item))
                {
                    OnItemDetected(item, entry, qm);
                }
            }
        }
    }

    private bool IsItemInDetectionRange(GameObject item)
    {
        Vector3 directionToItem = item.transform.position - transform.position;
        float distanceToItem = directionToItem.magnitude;

        // Verificar distancia
        if (distanceToItem > detectionRadius)
            return false;

        // Verificar ángulo (campo de visión)
        Vector3 forward = transform.forward;
        float angleToItem = Vector3.Angle(forward, directionToItem);
        
        return angleToItem <= detectionAngle * 0.5f;
    }

    private bool IsItemHeldByPlayer(GameObject item)
    {
        // Verificar si el item es hijo del jugador
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        Transform currentParent = item.transform.parent;
        while (currentParent != null)
        {
            if (currentParent.gameObject == player)
                return true;
            currentParent = currentParent.parent;
        }

        return false;
    }

    private void OnItemDetected(GameObject item, QuestChainEntry entry, QuestManager qm)
    {
        // Marcar como detectado para no procesarlo de nuevo
        _detectedItems.Add(item);

        Debug.Log($"[SimpleQuestNPC] Item '{item.name}' detectado en rango.");

        // Destruir el item
        Destroy(item);

        // Si la quest NO tiene pasos (quest simple), completarla directamente
        var runtimeQuest = qm.GetAll().FirstOrDefault(rq => rq.Id == entry.questData.questId);
        if (runtimeQuest != null)
        {
            Debug.Log($"[SimpleQuestNPC] Quest '{entry.questData.questId}' tiene {runtimeQuest.Steps.Length} pasos.");
            
            if (runtimeQuest.Steps.Length == 0)
            {
                // Quest sin pasos - completar directamente
                Debug.Log($"[SimpleQuestNPC] Quest sin pasos detectada. Completando directamente.");
                CompleteQuestWithItem(entry, qm, FindActiveQuestIndex(qm));
            }
            else
            {
                // Quest con pasos - determinar qué paso completar
                int stepToComplete = entry.itemDeliveryStepIndex;
                
                // Si el índice configurado no existe, usar el último paso disponible
                if (stepToComplete >= runtimeQuest.Steps.Length)
                {
                    stepToComplete = runtimeQuest.Steps.Length - 1;
                    Debug.LogWarning($"[SimpleQuestNPC] itemDeliveryStepIndex ({entry.itemDeliveryStepIndex}) fuera de rango. Usando último paso disponible: {stepToComplete}");
                }
                
                Debug.Log($"[SimpleQuestNPC] Completando paso {stepToComplete}");
                qm.MarkStepDone(entry.questData.questId, stepToComplete);
                
                // DEBUG: Verificar estado de todos los pasos
                Debug.Log($"[SimpleQuestNPC] Estado de pasos para '{entry.questData.questId}':");
                for (int i = 0; i < runtimeQuest.Steps.Length; i++)
                {
                    Debug.Log($"  Step {i}: {runtimeQuest.Steps[i].description} - Completado: {runtimeQuest.Steps[i].completed}");
                }
                
                // Verificar si todos los pasos están completados
                bool allStepsCompleted = qm.AreAllStepsCompleted(entry.questData.questId);
                Debug.Log($"[SimpleQuestNPC] ¿Todos los pasos completados? {allStepsCompleted}");
                
                if (allStepsCompleted)
                {
                    CompleteQuestWithItem(entry, qm, FindActiveQuestIndex(qm));
                }
                else
                {
                    Debug.LogWarning($"[SimpleQuestNPC] La quest '{entry.questData.questId}' aún tiene pasos pendientes.");
                }
            }
        }
    }

    private void CompleteQuestWithItem(QuestChainEntry entry, QuestManager qm, int currentIndex)
    {
        qm.CompleteQuest(entry.questData.questId);
        
        Debug.Log($"[SimpleQuestNPC] Quest {entry.questData.questId} completada por entrega de item.");
        
        // Ejecutar evento de quest completada
        entry.OnQuestCompleted?.Invoke();

        // Mostrar diálogo de entrega y encadenar
        ChainToNextQuest(entry, qm, entry.questData.questId, currentIndex);
    }

    #endregion

    #region Gizmos

    void OnDrawGizmosSelected()
    {
        if (!showDetectionGizmos) return;

        // Dibujar el radio de detección
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Dibujar el cono de visión
        Vector3 forward = transform.forward;
        float halfAngle = detectionAngle * 0.5f;

        // Líneas del cono
        Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * forward * detectionRadius;
        Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * forward * detectionRadius;

        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        
        // Arco del cono
        Vector3 previousPoint = transform.position + leftBoundary;
        int segments = 20;
        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfAngle + (detectionAngle * i / segments);
            Vector3 point = transform.position + Quaternion.Euler(0, angle, 0) * forward * detectionRadius;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }

    #endregion
}
