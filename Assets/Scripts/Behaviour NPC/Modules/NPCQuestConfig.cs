using UnityEngine;
using System.Linq;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de misiones para NPCs.
    /// Aquí se configura la cadena de misiones (Quest Chain) que ofrece el NPC.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Quest_Config", menuName = "NPC/Módulos/Quest Config", order = 4)]
    public class NPCQuestConfig : NPCModuleConfigBase
    {
        [Header("Quest Chain")]
        [Tooltip("Cadena de misiones que ofrece este NPC. Cada elemento representa una quest en orden.")]
        public QuestChainEntry[] questChain = System.Array.Empty<QuestChainEntry>();
        [Header("Item Detection (Global)")]
        [Tooltip("¿Detectar automáticamente cuando el jugador entrega ítems? (Se aplica globalmente)")]
        public bool enableItemDetection = true;
        [Min(0f)]
        [Tooltip("Radio de detección de ítems por defecto")]
        public float detectionRadius = 3f;
        [Range(0f, 180f)]
        [Tooltip("Ángulo de detección de ítemsor defecto")]
        public float detectionAngle = 90f;
        [Tooltip("Layer de detección de ítems")]
        public LayerMask detectionLayer = ~0;
        [Min(0.05f)]
        [Tooltip("Intervalo de escaneo de ítems")]
        public float detectionInterval = 0.33f;
        [Header("Behavior")]
        [Min(0f)]
        [Tooltip("Velocidad de rotación hacia el jugador (grados por segundo) - Ahora manejado por DialogueManager")]
        public float rotationSpeed = 360f;
        
        [Header("Persistent Icon")]
        [Tooltip("Prefab del icono persistente que se muestra cuando hay quests disponibles (ej: signo de exclamación amarillo)")]
        public GameObject questIconPrefab;
        
        [Tooltip("Offset del icono respecto a la posición del NPC")]
        public Vector3 questIconOffset = new Vector3(0, 2.5f, 0);
        
        [Tooltip("¿Mostrar icono cuando hay una quest disponible para iniciar?")]
        public bool showIconWhenQuestAvailable = true;
        
        [Tooltip("¿Mostrar icono cuando hay una quest activa en progreso?")]
        public bool showIconWhenQuestInProgress = true;
        
        [Tooltip("¿Mostrar icono cuando una quest está lista para entregar?")]
        public bool showIconWhenQuestReadyToTurnIn = true;
        
        [Tooltip("Prefab alternativo para cuando la quest está lista para entregar (ej: signo de interrogación). Si es null, usa questIconPrefab.")]
        public GameObject turnInIconPrefab;
        
        [Tooltip("¿Ocultar el icono persistente automáticamente cuando todas las quests se completen?")]
        public bool hideIconWhenAllCompleted = true;

        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            if (questChain == null || questChain.Length == 0)
            {
                errorMessage = "Quest chain no puede estar vacío. Añade al menos una quest.";
                return false;
            }
            for (int i = 0; i < questChain.Length; i++)
            {
                var entry = questChain[i];
                if (entry == null)
                {
                    errorMessage = $"Quest chain entry {i} es null";
                    return false;
                }
                if (entry.questData == null)
                {
                    errorMessage = $"Quest chain entry {i} no tiene questData asignado";
                    return false;
                }
                if (entry.autoDetectItemDelivery && string.IsNullOrEmpty(entry.itemTag))
                {
                    errorMessage = $"Quest {i} tiene autoDetectItemDelivery activado pero itemTag vacío";
                    return false;
                }
                
                // Validar requiredItems (nuevo sistema)
                if (entry.requiredItems != null && entry.requiredItems.Length > 0)
                {
                    for (int j = 0; j < entry.requiredItems.Length; j++)
                    {
                        var itemReq = entry.requiredItems[j];
                        if (itemReq.item == null)
                        {
                            errorMessage = $"Quest {i}, item requirement {j}: item es null";
                            return false;
                        }
                        
                        // ✅ Verificar que el item tenga itemId configurado
                        if (string.IsNullOrEmpty(itemReq.item.itemId))
                        {
                            errorMessage = $"Quest {i}, item requirement {j}: el ItemData '{itemReq.item.name}' no tiene itemId configurado";
                            return false;
                        }
                        
                        // ✅ stepConditionId es opcional - se auto-genera en GetStepConditionId() si es necesario
                        // No necesita validación aquí
                    }
                }
            }
            if (detectionRadius <= 0f)
            {
                errorMessage = "Detection radius debe ser mayor a 0";
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// Estado del icono persistente de quest
        /// </summary>
        public enum QuestIconState
        {
            None,           // No mostrar icono
            Available,      // Quest disponible para iniciar (!)
            InProgress,     // Quest activa en progreso (!)
            ReadyToTurnIn   // Quest lista para entregar (?)
        }
        
        /// <summary>
        /// Obtiene el estado actual del icono basado en las quests de este NPC
        /// </summary>
        public QuestIconState GetCurrentIconState()
        {
            var qm = QuestManager.Instance;
            if (qm == null || questChain == null || questChain.Length == 0)
                return QuestIconState.None;
            
            // Verificar si todas las quests están completadas
            if (hideIconWhenAllCompleted && AreAllQuestsCompleted())
                return QuestIconState.None;
            
            // Buscar quest activa
            foreach (var entry in questChain)
            {
                if (entry?.questData == null) continue;
                
                var state = qm.GetState(entry.questData.questId);
                
                if (state == QuestState.Active)
                {
                    // Verificar si todos los pasos están completos (lista para entregar)
                    if (qm.AreAllStepsCompleted(entry.questData.questId))
                    {
                        if (showIconWhenQuestReadyToTurnIn)
                            return QuestIconState.ReadyToTurnIn;
                    }
                    else
                    {
                        if (showIconWhenQuestInProgress)
                            return QuestIconState.InProgress;
                    }
                    return QuestIconState.None;
                }
            }
            
            // Si no hay quest activa, verificar si hay alguna disponible para iniciar
            var first = GetFirstInactiveQuest();
            if (first != null && showIconWhenQuestAvailable)
                return QuestIconState.Available;
            
            return QuestIconState.None;
        }
        
        /// <summary>
        /// Obtiene el prefab del icono a mostrar según el estado actual
        /// </summary>
        public GameObject GetCurrentIconPrefab()
        {
            var state = GetCurrentIconState();
            
            switch (state)
            {
                case QuestIconState.ReadyToTurnIn:
                    // Usar turnInIconPrefab si existe, sino el normal
                    return turnInIconPrefab != null ? turnInIconPrefab : questIconPrefab;
                    
                case QuestIconState.Available:
                case QuestIconState.InProgress:
                    return questIconPrefab;
                    
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Verifica si debe mostrarse el icono persistente
        /// </summary>
        public bool ShouldShowIcon()
        {
            return GetCurrentIconState() != QuestIconState.None && questIconPrefab != null;
        }
        
        /// <summary>
        /// Verifica si todas las quests de la cadena están completadas
        /// </summary>
        public bool AreAllQuestsCompleted()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return false;
            
            foreach (var entry in questChain)
            {
                if (entry?.questData == null) continue;
                if (qm.GetState(entry.questData.questId) != QuestState.Completed)
                    return false;
            }
            return true;
        }
        
        /// <summary>
        /// Obtiene la primera quest inactiva de la cadena (disponible para iniciar)
        /// </summary>
        private QuestChainEntry GetFirstInactiveQuest()
        {
            var qm = QuestManager.Instance;
            if (qm == null) return null;
            
            foreach (var entry in questChain)
            {
                if (entry?.questData == null) continue;
                if (qm.GetState(entry.questData.questId) == QuestState.Inactive)
                    return entry;
            }
            return null;
        }

        /// <summary>
        /// Procesa la interacción del jugador con el NPC.
        /// Maneja la lógica de quests, completion modes y diálogos.
        /// </summary>
        public bool ProcessInteraction(GameObject interactor, Common.NPCStateContext context)
        {
            Debug.Log($"[NPCQuestConfig.ProcessInteraction] Iniciando interacción con NPC. Interactor: {interactor?.name}, Context válido: {context != null}");
            
            // ✅ Activar animación de hablar/interactuar
            // La rotación ahora la maneja automáticamente DialogueManager.StartDialogue(dialogue, npcTransform, ...)
            Debug.Log($"[NPCQuestConfig.ProcessInteraction] Activando animación de hablar");
            StartTalkingAnimation(context);
            
            var qm = QuestManager.Instance;
            if (qm == null)
            {
                Debug.LogError("[NPCQuestConfig] QuestManager.Instance es null");
                return false;
            }

            if (questChain == null || questChain.Length == 0)
            {
                Debug.LogWarning("[NPCQuestConfig] questChain vacío");
                return false;
            }

            // Buscar quest activa en la cadena (de atrás hacia adelante)
            for (int i = questChain.Length - 1; i >= 0; i--)
            {
                var entry = questChain[i];
                if (entry?.questData == null) continue;

                var questId = entry.questData.questId;
                var state = qm.GetState(questId);

                if (state == QuestState.Active || state == QuestState.Completed)
                {
                    HandleQuestState(qm, entry, questId, state, context);
                    return true;
                }
            }

            // Si no hay quest activa, iniciar la primera
            var first = questChain[0];
            if (first?.questData != null)
            {
                var firstState = qm.GetState(first.questData.questId);
                if (firstState == QuestState.Inactive)
                {
                    // Si tiene dlgBefore, reproducir diálogo primero y luego iniciar quest
                    if (first.dlgBefore != null)
                    {
                        first.onOfferDialogueStarted?.Invoke();
                        PlayDialogueWithCallback(first.dlgBefore, context, () =>
                        {
                            // Callback ejecutado cuando termina el diálogo
                            qm.AddQuest(first.questData);
                            qm.StartQuest(first.questData.questId);
                            first.onOfferDialogueFinished?.Invoke();
                            
                            // ✅ ELIMINADO: CheckAutoCompletedQuest
                            // La quest ya no se auto-completa, el jugador debe volver a hablar con el NPC
                        });
                    }
                    else
                    {
                        // Sin diálogo, NO iniciar automáticamente (se iniciará desde otro lado)
                        Debug.Log($"[NPCQuestConfig] Quest '{first.questData.questId}' sin dlgBefore - no se inicia automáticamente");
                    }
                    return true;
                }
            }

            return false;
        }

        private void HandleQuestState(QuestManager qm, QuestChainEntry entry, string questId, QuestState state, Common.NPCStateContext context)
        {
            switch (state)
            {
                case QuestState.Active:
                    // ✅ Verificar si el jugador tiene los items requeridos en inventario
                    // Esto marca automáticamente los pasos correspondientes como completados
                    CheckAndMarkInventoryRequirements(qm, entry, questId);
                    
                    // ✅ Verificar si el jugador tiene los party members requeridos
                    // Esto marca automáticamente los pasos correspondientes como completados
                    CheckAndMarkPartyMemberRequirements(qm, entry, questId);
                    
                    bool allDone = qm.AreAllStepsCompleted(questId);
                    
                    // ✅ NUEVO: Verificar si tiene todos los items requeridos del inventario
                    bool hasAllRequiredItems = HasAllRequiredItems(entry);

                    switch (entry.completionMode)
                    {
                        case QuestCompletionMode.AutoCompleteOnTalk:
                            // ✅ MODIFICADO: AutoCompleteOnTalk ahora respeta requiredItems
                            // Si hay items requeridos, DEBE tenerlos para completar
                            if (entry.requiredItems != null && entry.requiredItems.Length > 0)
                            {
                                if (hasAllRequiredItems)
                                {
                                    // Tiene los items -> completar quest y consumirlos
                                    ConsumeRequiredItems(entry);
                                    CompleteAllSteps(qm, entry, questId, context);
                                }
                                else
                                {
                                    // No tiene los items -> mostrar diálogo in progress
                                    Debug.Log($"[NPCQuestConfig] Quest '{questId}' - AutoCompleteOnTalk bloqueado: faltan items requeridos");
                                    PlayDialogue(entry.dlgInProgress, context);
                                }
                            }
                            else
                            {
                                // No hay items requeridos -> completar directamente (comportamiento original)
                                CompleteAllSteps(qm, entry, questId, context);
                            }
                            break;

                        case QuestCompletionMode.CompleteOnTalkIfStepsReady:
                        case QuestCompletionMode.Manual:
                            if (allDone && hasAllRequiredItems)
                            {
                                // ✅ Consumir los items requeridos al completar la quest
                                ConsumeRequiredItems(entry);
                                FinishQuest(qm, entry, questId, context);
                            }
                            else
                            {
                                PlayDialogue(entry.dlgInProgress, context);
                            }
                            break;
                    }
                    break;

                case QuestState.Completed:
                    PlayDialogue(entry.dlgCompleted, context);
                    break;
            }
        }
        
        /// <summary>
        /// Verifica si el jugador tiene TODOS los items requeridos en el inventario.
        /// </summary>
        private bool HasAllRequiredItems(QuestChainEntry entry)
        {
            if (entry.requiredItems == null || entry.requiredItems.Length == 0)
                return true; // Sin items requeridos = siempre true
            
            // Obtener el inventario del jugador
            if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
            {
                Debug.LogWarning($"[NPCQuestConfig] No se encontró Inventory del jugador para verificar items");
                return false;
            }
            
            foreach (var req in entry.requiredItems)
            {
                if (req?.item == null) continue;
                
                if (!inventory.HasItem(req.item, req.amount))
                {
                    Debug.Log($"[NPCQuestConfig] ❌ Falta item '{req.item.displayName}' (tiene {inventory.Count(req.item.itemId)}/{req.amount})");
                    return false;
                }
            }
            
            Debug.Log($"[NPCQuestConfig] ✅ Todos los items requeridos están en el inventario");
            return true;
        }
        
        /// <summary>
        /// Verifica si el jugador tiene los items requeridos en el inventario y marca los pasos correspondientes.
        /// </summary>
        private void CheckAndMarkInventoryRequirements(QuestManager qm, QuestChainEntry entry, string questId)
        {
            if (entry.requiredItems == null || entry.requiredItems.Length == 0)
                return;
            
            // Obtener el inventario del jugador
            if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
            {
                Debug.LogWarning($"[NPCQuestConfig] No se encontró Inventory del jugador para verificar items requeridos");
                return;
            }
            
            foreach (var req in entry.requiredItems)
            {
                if (req?.item == null) continue;
                
                // Verificar si tiene suficiente cantidad del item
                bool hasItem = inventory.HasItem(req.item, req.amount);
                
                Debug.Log($"[NPCQuestConfig] Verificando item '{req.item.displayName}' (x{req.amount}): {(hasItem ? "✅ TIENE" : "❌ NO TIENE")}");
                
                if (hasItem)
                {
                    // Determinar qué step marcar como completado
                    int stepToMark = -1;
                    
                    if (req.stepIndex >= 0)
                    {
                        // Usar stepIndex directamente
                        stepToMark = req.stepIndex;
                    }
                    else
                    {
                        // Buscar por conditionId
                        string conditionId = req.GetStepConditionId();
                        if (!string.IsNullOrEmpty(conditionId))
                        {
                            stepToMark = qm.FindStepIndexByConditionId(questId, conditionId);
                        }
                    }
                    
                    if (stepToMark >= 0)
                    {
                        // Verificar si el paso ya está completado
                        var quest = qm.GetAll().FirstOrDefault(q => q.Id == questId);
                        if (quest?.Steps != null && stepToMark < quest.Steps.Length && !quest.Steps[stepToMark].completed)
                        {
                            Debug.Log($"[NPCQuestConfig] ✅ Marcando step {stepToMark} de quest '{questId}' como completado (item '{req.item.displayName}' entregado)");
                            qm.MarkStepDone(questId, stepToMark);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[NPCQuestConfig] No se pudo determinar el step a marcar para item '{req.item.displayName}'");
                    }
                }
            }
        }
        
        /// <summary>
        /// Verifica si el jugador tiene los party members requeridos y marca los pasos correspondientes.
        /// Se llama cuando el jugador habla con el NPC para verificar si los NPCs requeridos están en el party.
        /// </summary>
        private void CheckAndMarkPartyMemberRequirements(QuestManager qm, QuestChainEntry entry, string questId)
        {
            if (entry.requiredPartyMembers == null || entry.requiredPartyMembers.Length == 0)
                return;
            
            // Verificar si PlayerParty está disponible
            if (!Game.NPC.PlayerParty.HasInstance)
            {
                Debug.LogWarning($"[NPCQuestConfig] PlayerParty no está disponible para verificar party members");
                return;
            }
            
            var party = Game.NPC.PlayerParty.Instance;
            Debug.Log($"[NPCQuestConfig] 🔍 Verificando {entry.requiredPartyMembers.Length} party members requeridos (party tiene {party.MemberCount} miembros)");
            
            foreach (var memberReq in entry.requiredPartyMembers)
            {
                if (string.IsNullOrEmpty(memberReq.memberId))
                {
                    Debug.LogWarning($"[NPCQuestConfig] PartyMemberRequirement con memberId vacío en quest '{questId}'");
                    continue;
                }
                
                // Verificar si el miembro está en el equipo
                bool isInParty = party.Members.Any(m => 
                    m.NPCManager?.Configuration?.interactiveNarrativeConfig?.persistenceId == memberReq.memberId ||
                    m.gameObject.name == memberReq.memberId);
                
                Debug.Log($"[NPCQuestConfig] Verificando member '{memberReq.memberId}': {(isInParty ? "✅ EN PARTY" : "❌ NO EN PARTY")}");
                
                if (!isInParty) continue;
                
                // Determinar qué step marcar como completado
                int stepToMark = -1;
                
                if (memberReq.stepIndex >= 0)
                {
                    // Usar stepIndex directamente
                    stepToMark = memberReq.stepIndex;
                }
                else
                {
                    // Buscar por conditionId
                    string conditionId = memberReq.GetStepConditionId();
                    if (!string.IsNullOrEmpty(conditionId))
                    {
                        stepToMark = qm.FindStepIndexByConditionId(questId, conditionId);
                    }
                }
                
                if (stepToMark >= 0)
                {
                    // Verificar si el paso ya está completado
                    var quest = qm.GetAll().FirstOrDefault(q => q.Id == questId);
                    if (quest?.Steps != null && stepToMark < quest.Steps.Length && !quest.Steps[stepToMark].completed)
                    {
                        Debug.Log($"[NPCQuestConfig] ✅ Marcando step {stepToMark} de quest '{questId}' como completado (member '{memberReq.memberId}' está en party)");
                        qm.MarkStepDone(questId, stepToMark);
                    }
                    else if (quest?.Steps != null && stepToMark < quest.Steps.Length && quest.Steps[stepToMark].completed)
                    {
                        Debug.Log($"[NPCQuestConfig] ℹ️ Step {stepToMark} ya estaba completado para member '{memberReq.memberId}'");
                    }
                }
                else
                {
                    Debug.LogWarning($"[NPCQuestConfig] No se pudo determinar el step a marcar para party member '{memberReq.memberId}'");
                }
            }
        }
        
        /// <summary>
        /// Consume los items requeridos del inventario del jugador.
        /// Se llama cuando la quest se completa exitosamente.
        /// </summary>
        private void ConsumeRequiredItems(QuestChainEntry entry)
        {
            if (entry.requiredItems == null || entry.requiredItems.Length == 0)
                return;
            
            if (!PlayerService.TryGetComponent<Inventory>(out var inventory, includeInactive: true, allowSceneLookup: true) || inventory == null)
                return;
            
            foreach (var req in entry.requiredItems)
            {
                if (req?.item == null || !req.consumeOnComplete) continue;
                
                if (inventory.TryConsume(req.item, req.amount))
                {
                    Debug.Log($"[NPCQuestConfig] 🔥 Consumido item '{req.item.displayName}' x{req.amount} del inventario");
                }
                else
                {
                    Debug.LogWarning($"[NPCQuestConfig] ⚠️ No se pudo consumir item '{req.item.displayName}' x{req.amount}");
                }
            }
        }

        private void CompleteAllSteps(QuestManager qm, QuestChainEntry entry, string questId, Common.NPCStateContext context)
        {
            var quest = qm.GetAll().FirstOrDefault(q => q.Id == questId);
            if (quest?.Steps != null)
            {
                for (int i = 0; i < quest.Steps.Length; i++)
                {
                    if (!quest.Steps[i].completed)
                    {
                        qm.MarkStepDone(questId, i);
                    }
                }
            }
            FinishQuest(qm, entry, questId, context);
        }

        private void FinishQuest(QuestManager qm, QuestChainEntry entry, string questId, Common.NPCStateContext context)
        {
            // ✅ IMPORTANTE: Reproducir el diálogo turn in PRIMERO
            // La quest se completa DESPUÉS del diálogo para que el grafo narrativo
            // se ejecute en el momento correcto (después del feedback al jugador)
            Debug.Log($"[NPCQuestConfig.FinishQuest] Iniciando diálogo de turn-in para quest '{questId}'");
            
            PlayDialogueWithCallback(entry.dlgTurnIn, context, () =>
            {
                // Callback ejecutado cuando termina el diálogo turn in
                Debug.Log($"[NPCQuestConfig.FinishQuest] Diálogo de turn-in completado. Completando quest '{questId}'");
                
                // AHORA sí, completar la quest (dispara OnQuestCompleted → grafo narrativo)
                qm.CompleteQuest(questId);
                entry.onQuestCompleted?.Invoke();
                
                Debug.Log($"[NPCQuestConfig.FinishQuest] Quest completada. Intentando iniciar siguiente en la cadena");
                
                // Intentar iniciar la siguiente quest en la cadena
                TryStartNextQuestInChain(questId, context);
            });
        }

        private void TryStartNextQuestInChain(string completedQuestId, Common.NPCStateContext context)
        {
            Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] Buscando siguiente quest después de '{completedQuestId}'");
            
            // Diferir la ejecución para dar tiempo a que el grafo narrativo termine
            // y el sistema de input se estabilice antes de iniciar el siguiente diálogo
            if (context?.Transform != null)
            {
                // Buscar un MonoBehaviour para ejecutar la coroutine
                var behaviourManager = context.Transform.GetComponent<NPCBehaviourManagerV2>();
                if (behaviourManager != null)
                {
                    behaviourManager.StartCoroutine(TryStartNextQuestInChainDelayed(completedQuestId, context));
                }
                else
                {
                    Debug.LogError("[NPCQuestConfig.TryStartNextQuestInChain] No se encontró NPCBehaviourManagerV2 en el NPC");
                    // Ejecutar directamente como fallback
                    TryStartNextQuestInChainImmediately(completedQuestId, context);
                }
            }
            else
            {
                Debug.LogError("[NPCQuestConfig.TryStartNextQuestInChain] Context.Transform es null, no se puede diferir la ejecución");
                // Ejecutar directamente como fallback
                TryStartNextQuestInChainImmediately(completedQuestId, context);
            }
        }

        private System.Collections.IEnumerator TryStartNextQuestInChainDelayed(string completedQuestId, Common.NPCStateContext context)
        {
            // Esperar varios frames para que el grafo narrativo termine completamente
            // Aumentamos a 5 frames para asegurar que todos los callbacks terminen
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            
            Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChainDelayed] Ejecutando búsqueda de siguiente quest después de 5 frames");
            
            TryStartNextQuestInChainImmediately(completedQuestId, context);
        }

        private void TryStartNextQuestInChainImmediately(string completedQuestId, Common.NPCStateContext context)
        {
            // Encontrar índice de la quest completada
            int completedIndex = -1;
            for (int i = 0; i < questChain.Length; i++)
            {
                if (questChain[i]?.questData?.questId == completedQuestId)
                {
                    completedIndex = i;
                    break;
                }
            }

            if (completedIndex < 0 || completedIndex >= questChain.Length - 1)
            {
                Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] No hay siguiente quest (completedIndex={completedIndex}, total={questChain.Length})");
                return; // No hay siguiente quest
            }

            var nextEntry = questChain[completedIndex + 1];
            if (nextEntry?.questData == null)
            {
                Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] Siguiente entrada no tiene questData");
                return;
            }

            var qm = QuestManager.Instance;
            if (qm == null) return;

            var nextState = qm.GetState(nextEntry.questData.questId);
            if (nextState != QuestState.Inactive)
            {
                Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] Quest '{nextEntry.questData.questId}' ya está iniciada (estado={nextState})");
                return; // Ya está iniciada
            }

            Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] Iniciando siguiente quest '{nextEntry.questData.questId}'");

            // Si tiene dlgBefore, reproducir diálogo y luego iniciar quest
            if (nextEntry.dlgBefore != null)
            {
                Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] Reproduciendo diálogo de oferta");
                nextEntry.onOfferDialogueStarted?.Invoke();
                PlayDialogueWithCallback(nextEntry.dlgBefore, context, () =>
                {
                    // Callback ejecutado cuando termina el diálogo
                    Debug.Log($"[NPCQuestConfig.TryStartNextQuestInChain] Diálogo de oferta completado. Añadiendo y arrancando quest");
                    qm.AddQuest(nextEntry.questData);
                    qm.StartQuest(nextEntry.questData.questId);
                    nextEntry.onOfferDialogueFinished?.Invoke();
                    
                    // ✅ ELIMINADO: CheckAutoCompletedQuest
                    // La quest ya no se auto-completa
                });
            }
            // Si NO tiene dlgBefore, NO iniciar automáticamente
            // (se iniciará desde otro lugar: grafo narrativo, etc.)
        }


        private void PlayDialogue(DialogueAsset dialogue, Common.NPCStateContext context)
        {
            if (dialogue == null)
            {
                Debug.LogWarning("[NPCQuestConfig.PlayDialogue] dialogue es null - no se reproduce diálogo");
                return;
            }

            var dm = DialogueManager.Instance;
            if (dm == null)
            {
                Debug.LogError("[NPCQuestConfig] DialogueManager.Instance es null");
                return;
            }

            Debug.Log($"[NPCQuestConfig.PlayDialogue] Iniciando diálogo '{dialogue.name}' con NPC en {context.Transform?.position}. Líneas: {dialogue.lines?.Length ?? 0}");
            
            // Callback para detener animación de hablar cuando termine el diálogo
            dm.StartDialogue(dialogue, context.Transform, () => 
            {
                Debug.Log($"[NPCQuestConfig.PlayDialogue] Diálogo '{dialogue.name}' terminado - deteniendo animación");
                StopTalkingAnimation(context);
            });
        }

        private void PlayDialogueWithCallback(DialogueAsset dialogue, Common.NPCStateContext context, System.Action onFinished)
        {
            if (dialogue == null)
            {
                onFinished?.Invoke();
                return;
            }

            var dm = DialogueManager.Instance;
            if (dm == null)
            {
                Debug.LogError("[NPCQuestConfig] DialogueManager.Instance es null");
                onFinished?.Invoke();
                return;
            }

            // Combinar callback original con detener animación de hablar
            System.Action combinedCallback = () =>
            {
                StopTalkingAnimation(context);
                onFinished?.Invoke();
            };

            // ✅ DialogueManager maneja la rotación del NPC automáticamente
            dm.StartDialogue(dialogue, context.Transform, combinedCallback);
        }
        

        /// <summary>
        /// Activa la animación de hablar en el NPC
        /// </summary>
        private void StartTalkingAnimation(Common.NPCStateContext context)
        {
            if (context?.Animator == null) return;
            
            // Reproducir animación de interacción (saludo/hablar)
            context.Animator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
            
            // Usar el NPCSimpleAnimator para activar la animación de hablar
            context.Animator.SetTalking(true);
        }
        
        /// <summary>
        /// Detiene la animación de hablar en el NPC
        /// </summary>
        private void StopTalkingAnimation(Common.NPCStateContext context)
        {
            if (context?.Animator == null) return;
            
            // Desactivar la animación de hablar
            context.Animator.SetTalking(false);
        }
    }
}