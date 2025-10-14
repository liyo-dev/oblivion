using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

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
        [TagField]
        public string itemTag = "Untagged";
        
        [Header("Diálogos de esta Quest")]
        public DialogueAsset dlgBefore;
        public DialogueAsset dlgInProgress;
        public DialogueAsset dlgTurnIn;
        public DialogueAsset dlgCompleted;
        
        [Header("Eventos")]
        [Tooltip("Evento que se ejecuta cuando esta quest se completa")]
        [FormerlySerializedAs("OnQuestCompleted")]
        public UnityEvent onQuestCompleted;
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
    private HashSet<GameObject> _detectedItems = new HashSet<GameObject>();
    private Coroutine _detectionRoutine;

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
        StartDetection();
    }

    void OnEnable()
    {
        // Reiniciar detección si fue desactivado y reactivado (ej: por cinemática)
        if (_detectionRoutine == null && gameObject.activeInHierarchy)
        {
            StartDetection();
        }
    }

    void OnDisable()
    {
        StopDetection();
    }

    private void StartDetection()
    {
        if (_detectionRoutine != null)
        {
            StopCoroutine(_detectionRoutine);
        }
        _detectionRoutine = StartCoroutine(DetectItemsRoutine());
    }

    private void StopDetection()
    {
        if (_detectionRoutine != null)
        {
            StopCoroutine(_detectionRoutine);
            _detectionRoutine = null;
        }
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
                // Al hablar con una quest ya completada, ofrecer/seguir la siguiente de la cadena
                PlayDialogue(entry.dlgCompleted, () =>
                {
                    StartCoroutine(StartNextQuestAfterDelay(qm, currentIndex));
                });
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

    private void PersistQuestCompleted(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        var profile = GameBootService.Profile;
        if (profile == null) return;
        var preset = profile.GetActivePresetResolved();
        if (preset == null) return;
        if (preset.flags == null) preset.flags = new List<string>();
        string flag = $"QUEST_COMPLETED:{questId}";
        if (!preset.flags.Contains(flag)) preset.flags.Add(flag);

        var saveSystem = FindFirstObjectByType<SaveSystem>();
        if (saveSystem != null)
        {
            profile.SaveCurrentGameState(saveSystem);
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

        // Persistir flag + save
        PersistQuestCompleted(questId);
        
        // Ejecutar evento de quest completada
        entry.onQuestCompleted?.Invoke();

        // Encadenar con la siguiente quest si existe
        ChainToNextQuest(entry, qm, questId, currentIndex);
    }

    private void CompleteQuestIfReady(QuestChainEntry entry, QuestManager qm, string questId, int currentIndex)
    {
        if (qm.AreAllStepsCompleted(questId))
        {
            qm.CompleteQuest(questId);
            
            // Persistir flag + save
            PersistQuestCompleted(questId);
            
            // Ejecutar evento de quest completada
            entry.onQuestCompleted?.Invoke();
            
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
            
            // Persistir flag + save
            PersistQuestCompleted(questId);
            
            // Ejecutar evento de quest completada
            entry.onQuestCompleted?.Invoke();
            
            // Encadenar con la siguiente quest
            ChainToNextQuest(entry, qm, questId, currentIndex);
        }
        else
        {
            // Faltan pasos por completar manualmente
            PlayDialogue(entry.dlgInProgress);
        }
    }

    private void CompleteQuestWithItem(QuestChainEntry entry, QuestManager qm, int currentIndex)
    {
        qm.CompleteQuest(entry.questData.questId);
        
        // Persistir flag + save
        PersistQuestCompleted(entry.questData.questId);
        
        entry.onQuestCompleted?.Invoke();
        ChainToNextQuest(entry, qm, entry.questData.questId, currentIndex);
    }

    /// <summary>
    /// Encadena automáticamente con la siguiente quest en la cadena
    /// </summary>
    private void ChainToNextQuest(QuestChainEntry completedEntry, QuestManager qm, string completedQuestId, int currentIndex)
    {
        // Mostrar diálogo de Turn In
        // Usar el ID completado para logging y evitar advertencia de parámetro no usado
        if (!string.IsNullOrEmpty(completedQuestId))
        {
            Debug.Log($"[SimpleQuestNPC] Quest completada: {completedQuestId}. Preparando encadenamiento...");
        }

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

        // Iniciar/ofrecer la primera quest siguiente que no esté COMPLETADA ya
        int nextIndex = currentIndex + 1;
        while (nextIndex < questChain.Count)
        {
            var nextEntry = questChain[nextIndex];
            if (nextEntry?.questData == null)
            {
                nextIndex++;
                continue;
            }

            var nextId = nextEntry.questData.questId;
            var state = qm.GetState(nextId);

            if (state == QuestState.Completed)
            {
                // Saltar quests ya completadas y seguir buscando
                nextIndex++;
                continue;
            }

            if (state == QuestState.Inactive)
            {
                // Agregar y activar la siguiente quest
                qm.AddQuest(nextEntry.questData);
                qm.StartQuest(nextId);
                Debug.Log($"[SimpleQuestNPC] Encadenando a siguiente quest: {nextId}");

                // Mostrar el diálogo de oferta de la siguiente quest
                if (nextEntry.dlgBefore != null)
                {
                    PlayDialogue(nextEntry.dlgBefore);
                }
            }
            else if (state == QuestState.Active)
            {
                // Ya está activa: mostrar diálogo de progreso si lo hay
                if (nextEntry.dlgInProgress != null)
                {
                    PlayDialogue(nextEntry.dlgInProgress);
                }
            }
            // Tras gestionar la siguiente encontrada, terminar
            yield break;
        }

        // No hay más quests en la cadena que ofrecer/seguir
        Debug.Log("[SimpleQuestNPC] Fin de la cadena de quests.");
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
            
            // Pasar el transform de este NPC para la cámara de diálogo
            DialogueManager.Instance.StartDialogue(dlg, transform, () =>
            {
                // SIEMPRE disparar OnFinished para que el NPC vuelva a su estado normal
                _interactable.OnFinished?.Invoke();
                onComplete?.Invoke();
            });
        }
        else
        {
            // Pasar el transform de este NPC para la cámara de diálogo
            DialogueManager.Instance.StartDialogue(dlg, transform, onComplete);
        }
    }

    #region Item Detection

    private IEnumerator DetectItemsRoutine()
    {
        var wait = new WaitForSeconds(detectionInterval);
        while (isActiveAndEnabled)
        {
            yield return wait;
            CheckForItemsInRange();
        }
    }

    private void CheckForItemsInRange()
    {
        var qm = QuestManager.Instance;
        if (qm == null) return;

        int activeQuestIndex = FindActiveQuestIndex(qm);
        if (activeQuestIndex < 0) return;

        var entry = questChain[activeQuestIndex];
        if (!entry.autoDetectItemDelivery) return;

        if (qm.GetState(entry.questData.questId) != QuestState.Active) return;

        // Buscar objetos con el tag especificado
        var itemsInScene = GameObject.FindGameObjectsWithTag(entry.itemTag);
        
        foreach (var item in itemsInScene)
        {
            if (_detectedItems.Contains(item)) continue;
            if (!IsItemInDetectionRange(item)) continue;
            if (IsItemHeldByPlayer(item)) continue;
            
            OnItemDetected(item, entry, qm);
        }
    }

    private bool IsItemInDetectionRange(GameObject item)
    {
        Vector3 directionToItem = item.transform.position - transform.position;
        float distanceToItem = directionToItem.magnitude;

        if (distanceToItem > detectionRadius) return false;

        float angleToItem = Vector3.Angle(transform.forward, directionToItem);
        return angleToItem <= detectionAngle * 0.5f;
    }

    private bool IsItemHeldByPlayer(GameObject item)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        Transform currentParent = item.transform.parent;
        while (currentParent != null)
        {
            if (currentParent.gameObject == player) return true;
            currentParent = currentParent.parent;
        }
        return false;
    }

    private void OnItemDetected(GameObject item, QuestChainEntry entry, QuestManager qm)
    {
        _detectedItems.Add(item);
        Destroy(item);

        var runtimeQuest = qm.GetAll().FirstOrDefault(rq => rq.Id == entry.questData.questId);
        if (runtimeQuest == null) return;

        if (runtimeQuest.Steps.Length == 0)
        {
            CompleteQuestWithItem(entry, qm, FindActiveQuestIndex(qm));
            return;
        }

        int stepToComplete = entry.itemDeliveryStepIndex;
        if (stepToComplete >= runtimeQuest.Steps.Length)
        {
            stepToComplete = runtimeQuest.Steps.Length - 1;
        }
        
        qm.MarkStepDone(entry.questData.questId, stepToComplete);
        
        if (qm.AreAllStepsCompleted(entry.questData.questId))
        {
            CompleteQuestWithItem(entry, qm, FindActiveQuestIndex(qm));
        }
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
