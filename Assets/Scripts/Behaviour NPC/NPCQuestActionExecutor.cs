﻿﻿﻿using System.Collections;
using UnityEngine;
using Game.NPC.Modules;

namespace Game.NPC
{
    /// <summary>
    /// Ejecutor de acciones post-quest integrado con el sistema de quests.
    /// Lee la configuración directamente del QuestChainEntry y ejecuta la acción correspondiente.
    /// </summary>
    public class NPCQuestActionExecutor : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Manager del NPC (auto-detectado si está vacío)")]
        [SerializeField] private NPCBehaviourManagerV2 npcManager;
        
        [Header("Anchor System")]
        [Tooltip("¿Usar sistema de anchors para teletransporte?")]
        [SerializeField] private bool useAnchorSystem = true;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        
        void Awake()
        {
            if (npcManager == null)
            {
                npcManager = GetComponent<NPCBehaviourManagerV2>();
                
                if (npcManager == null)
                {
                    Debug.LogError($"[NPCQuestActionExecutor:{name}] No se encontró NPCBehaviourManagerV2");
                }
            }
        }
        
        void Start()
        {
            Debug.Log($"[NPCQuestActionExecutor:{name}] Start - Inicializando componente");
            
            // Suscribirse al evento global de QuestManager para detectar cuando se completan quests
            SubscribeToQuestManager();
            
            Debug.Log($"[NPCQuestActionExecutor:{name}] Start completado - npcManager={(npcManager != null ? "OK" : "NULL")}");
        }
        
        void OnDestroy()
        {
            // Desuscribirse del QuestManager
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.OnQuestCompleted -= HandleQuestCompleted;
            }
        }
        
        /// <summary>
        /// Suscribe al evento global de QuestManager para detectar cuando se completan quests
        /// </summary>
        private void SubscribeToQuestManager()
        {
            var questManager = QuestManager.Instance;
            if (questManager == null)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] QuestManager.Instance no disponible, no se pueden detectar quest completadas");
                return;
            }
            
            questManager.OnQuestCompleted += HandleQuestCompleted;
            
            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Suscrito a QuestManager.OnQuestCompleted");
        }
        
        /// <summary>
        /// Maneja el evento global cuando se completa cualquier quest
        /// </summary>
        private void HandleQuestCompleted(string questId)
        {
            Debug.Log($"[NPCQuestActionExecutor:{name}] HandleQuestCompleted recibido para quest '{questId}'");
            
            if (npcManager == null || npcManager.Configuration == null)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] NPC Manager o Configuration es NULL");
                return;
            }
            
            var questConfig = npcManager.Configuration.questConfig;
            if (questConfig == null || questConfig.questChain == null)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] QuestConfig o questChain es NULL");
                return;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor:{name}] Buscando quest '{questId}' en {questConfig.questChain.Length} entradas");
            
            // Buscar si esta quest pertenece a este NPC
            for (int i = 0; i < questConfig.questChain.Length; i++)
            {
                var entry = questConfig.questChain[i];
                
                if (entry.questData == null)
                {
                    Debug.LogWarning($"[NPCQuestActionExecutor:{name}] Entry {i} tiene questData NULL");
                    continue;
                }
                
                // Verificar si es la quest que se completó
                if (entry.questData.questId == questId)
                {
                    if (debugMode)
                        Debug.Log($"[NPCQuestActionExecutor:{name}] Quest '{questId}' encontrada en entry {i}");
                    
                    // Verificar si tiene post-action configurada
                    if (entry.postAction == null)
                    {
                        if (debugMode)
                            Debug.Log($"[NPCQuestActionExecutor:{name}] Quest '{questId}' NO tiene postAction configurada");
                        break;
                    }
                    
                    if (entry.postAction.actionType == QuestActionType.None)
                    {
                        if (debugMode)
                            Debug.Log($"[NPCQuestActionExecutor:{name}] Quest '{questId}' tiene postAction.actionType = None");
                        break;
                    }
                    
                    if (debugMode)
                        Debug.Log($"[NPCQuestActionExecutor:{name}] Ejecutando acción post-quest: {entry.postAction.actionType}");
                    
                    ExecutePostQuestAction(i);
                    break; // Ya encontramos la quest
                }
            }
        }
        
        /// <summary>
        /// Ejecuta la acción post-quest configurada en el QuestChainEntry.
        /// Llama esto desde onQuestCompleted del QuestChainEntry.
        /// </summary>
        public void ExecutePostQuestAction(int questIndex)
        {
            if (npcManager == null || npcManager.Configuration == null)
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] NPC Manager o Configuration no disponible");
                return;
            }
            
            var questConfig = npcManager.Configuration.questConfig;
            if (questConfig == null || questConfig.questChain == null)
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] Quest Config no disponible");
                return;
            }
            
            if (questIndex < 0 || questIndex >= questConfig.questChain.Length)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] Quest index {questIndex} fuera de rango");
                return;
            }
            
            var entry = questConfig.questChain[questIndex];
            var action = entry.postAction;
            
            if (action == null)
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] Quest {questIndex} tiene postAction NULL");
                return;
            }
            
            if (action.actionType == QuestActionType.None)
            {
                if (debugMode)
                    Debug.LogWarning($"[NPCQuestActionExecutor:{name}] Quest {questIndex} tiene actionType = None");
                return;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor:{name}] ExecutePostQuestAction - questIndex={questIndex}, actionType={action.actionType}");
            
            // Ejecutar directamente la acción
            StartCoroutine(ExecuteActionCoroutine(action, questIndex));
        }
        
        private IEnumerator ExecuteActionCoroutine(QuestPostAction action, int questIndex)
        {
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor:{name}] ExecuteActionCoroutine - actionType={action.actionType}");
            
            // 1. Diálogo pre-acción
            if (action.dialogueBeforeAction != null)
            {
                if (debugMode)
                    Debug.Log($"[NPCQuestActionExecutor] Reproduciendo diálogo pre-acción");
                
                var dialogueManager = DialogueManager.Instance;
                if (dialogueManager != null)
                {
                    dialogueManager.StartDialogue(action.dialogueBeforeAction);
                    
                    while (dialogueManager.IsOpen)
                    {
                        yield return null;
                    }
                }
            }
            
            // 2. Espera opcional
            if (action.delayBeforeAction > 0f)
            {
                if (debugMode)
                    Debug.Log($"[NPCQuestActionExecutor] Esperando {action.delayBeforeAction}s");
                
                yield return new WaitForSeconds(action.delayBeforeAction);
            }
            
            // 3. Ejecutar acción según tipo
            switch (action.actionType)
            {
                case QuestActionType.Move:
                    yield return ExecuteMoveAction(action);
                    break;
                
                case QuestActionType.Teleport:
                    yield return ExecuteTeleportAction(action);
                    break;
                
                case QuestActionType.StartCombat:
                    yield return ExecuteCombatAction(action);
                    break;
                
                case QuestActionType.Dialogue:
                    yield return ExecuteDialogueAction(action);
                    break;
                
                case QuestActionType.Custom:
                    action.customAction?.Invoke();
                    break;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor] Acción post-quest {questIndex} completada");
        }
        
        private IEnumerator ExecuteMoveAction(QuestPostAction action)
        {
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor:{name}] ExecuteMoveAction iniciado");
            
            Vector3 targetPosition = GetTargetPosition(action);
            
            if (targetPosition == Vector3.zero)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor] No se pudo obtener posición de destino para Move");
                yield break;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor:{name}] Moviendo a {targetPosition}");
            
            // Esperar un frame para asegurar que cualquier cinemática en curso ha terminado
            yield return null;
            
            // Forzar salida de cinemática si el NPC está en ella
            if (npcManager.Context != null && npcManager.Context.IsInCinematic)
            {
                if (debugMode)
                    Debug.Log($"[NPCQuestActionExecutor:{name}] NPC está en cinemática, forzando salida");
                
                npcManager.ExitCinematic();
                yield return new WaitForSeconds(0.2f); // Esperar a que se procese la transición
            }
            
            // Crear secuencia de movimiento
            var moveSequence = new States.MoveToPoscionSequence(
                targetPosition,
                action.maxMovementDuration,
                action.turnAroundOnArrival
            );
            
            // Iniciar movimiento usando la secuencia cinemática
            npcManager.StartCinematicSequence(moveSequence);
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor:{name}] Secuencia de movimiento iniciada, esperando completación...");
            
            // Esperar a que llegue (con timeout)
            float timeout = action.maxMovementDuration + 2f;
            float elapsed = 0f;
            
            while (!moveSequence.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] Movimiento alcanzó timeout de {timeout}s");
            }
            else if (debugMode)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] Movimiento completado en {elapsed:F2}s");
            }
        }
        
        private IEnumerator ExecuteTeleportAction(QuestPostAction action)
        {
            Vector3 targetPosition = GetTargetPosition(action);
            
            if (targetPosition == Vector3.zero)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor] No se pudo obtener posición de destino para Teleport");
                yield break;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor] Teletransportando a {targetPosition}");
            
            // Teletransporte instantáneo
            transform.position = targetPosition;
            
            if (action.turnAroundOnArrival)
            {
                transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
            }
            
            yield return null;
        }
        
        private IEnumerator ExecuteCombatAction(QuestPostAction action)
        {
            if (action.combatTarget == null)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor] Combat target no asignado");
                yield break;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor] Iniciando combate con {action.combatTarget.name}");
            
            // Entrar en modo combate
            npcManager.EnterCombat();
            
            // TODO: Aquí puedes añadir lógica específica de combate
            // Por ejemplo, moverse hacia el target, activar AI de combate, etc.
            
            yield return null;
        }
        
        private IEnumerator ExecuteDialogueAction(QuestPostAction action)
        {
            if (action.dialogueToPlay == null)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor] Dialogue to play no asignado");
                yield break;
            }
            
            if (debugMode)
                Debug.Log($"[NPCQuestActionExecutor] Reproduciendo diálogo");
            
            var dialogueManager = DialogueManager.Instance;
            if (dialogueManager != null)
            {
                dialogueManager.StartDialogue(action.dialogueToPlay);
                
                while (dialogueManager.IsOpen)
                {
                    yield return null;
                }
            }
        }
        
        /// <summary>
        /// Obtiene la posición de destino desde anchor o transform
        /// </summary>
        private Vector3 GetTargetPosition(QuestPostAction action)
        {
            // Prioridad 1: SpawnAnchor por ID (sistema existente)
            if (useAnchorSystem && !string.IsNullOrEmpty(action.targetAnchorName))
            {
                var spawnAnchor = SpawnAnchor.FindById(action.targetAnchorName);
                if (spawnAnchor != null)
                {
                    if (debugMode)
                        Debug.Log($"[NPCQuestActionExecutor] SpawnAnchor encontrado: {action.targetAnchorName}");
                    return spawnAnchor.transform.position;
                }
                else
                {
                    Debug.LogWarning($"[NPCQuestActionExecutor] SpawnAnchor con ID '{action.targetAnchorName}' no encontrado en AnchorRegistry");
                }
            }
            
            // Prioridad 2: Transform directo
            if (action.targetTransform != null)
            {
                if (debugMode)
                    Debug.Log($"[NPCQuestActionExecutor] Usando Transform directo: {action.targetTransform.name}");
                return action.targetTransform.position;
            }
            
            Debug.LogError($"[NPCQuestActionExecutor] No se pudo obtener target position (anchor y transform son NULL)");
            return Vector3.zero;
        }
    }
}

