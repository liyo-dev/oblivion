﻿﻿﻿using System.Collections;
using UnityEngine;
using Game.NPC.Modules;
using Game.NPC.States;

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
        [SerializeField] private bool debugMode;

        // Control de ejecución
        private bool _isExecutingPostAction;

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
            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] Start - Inicializando componente");

            // Suscribirse al evento global de QuestManager para detectar cuando se completan quests
            SubscribeToQuestManager();

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] Start completado - npcManager={(npcManager != null ? "OK" : "NULL")}");
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

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Suscrito a QuestManager.OnQuestCompleted");
        }

        /// <summary>
        /// Maneja el evento global cuando se completa cualquier quest
        /// </summary>
        private void HandleQuestCompleted(string questId)
        {
            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] HandleQuestCompleted recibido para quest '{questId}'");

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
                    
                    // Verificar si todas las quests están completadas para ocultar el icono
                    CheckAndHidePersistentIconIfAllCompleted();
                    
                    break; // Ya encontramos la quest
                }
            }
        }
        
        /// <summary>
        /// Verifica si todas las quests del NPC están completadas y oculta el icono persistente si está configurado
        /// </summary>
        private void CheckAndHidePersistentIconIfAllCompleted()
        {
            if (npcManager == null || npcManager.Configuration == null)
                return;
            
            var questConfig = npcManager.Configuration.questConfig;
            if (questConfig == null || !questConfig.hideIconWhenAllCompleted)
                return;
            
            var questManager = QuestManager.Instance;
            if (questManager == null)
                return;
            
            // Verificar si todas las quests están completadas
            bool allCompleted = true;
            foreach (var entry in questConfig.questChain)
            {
                if (entry == null || entry.questData == null)
                    continue;
                
                var state = questManager.GetState(entry.questData.questId);
                if (state != QuestState.Completed)
                {
                    allCompleted = false;
                    break;
                }
            }
            
            // Si todas están completadas, ocultar el icono
            if (allCompleted)
            {
                if (debugMode)
                    Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Todas las quests completadas");
                
                // TODO: Implementar HidePersistentIcon en NPCBehaviourManagerV2
                // npcManager.HidePersistentIcon();
            }
        }

        /// <summary>
        /// Ejecuta la acción post-quest configurada en el QuestChainEntry.
        /// Llama esto desde onQuestCompleted del QuestChainEntry.
        /// </summary>
        public void ExecutePostQuestAction(int questIndex)
        {
            if (_isExecutingPostAction)
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] Ya hay una post-action ejecutándose, ignorando");
                return;
            }

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

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ▶️ ExecutePostQuestAction - questIndex={questIndex}, actionType={action.actionType}");

            // Ejecutar la acción con bloqueo
            StartCoroutine(ExecuteActionCoroutine(action, questIndex));
        }

        private IEnumerator ExecuteActionCoroutine(QuestPostAction action, int questIndex)
        {
            _isExecutingPostAction = true;

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 🎬 ExecuteActionCoroutine - actionType={action.actionType}");

            // Esperar un frame para asegurar que todos los callbacks de CompleteQuest han terminado
            yield return null;

            // 1. Diálogo pre-acción
            if (action.dialogueBeforeAction != null)
            {
                if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 💬 Reproduciendo diálogo pre-acción");

                var dialogueManager = DialogueManager.Instance;
                if (dialogueManager != null)
                {
                    dialogueManager.StartDialogue(action.dialogueBeforeAction);

                    while (dialogueManager.IsOpen)
                    {
                        yield return null;
                    }
                }

                if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Diálogo pre-acción completado");
            }

            // 2. Espera opcional
            if (action.delayBeforeAction > 0f)
            {
                if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ⏳ Esperando {action.delayBeforeAction}s");
                yield return new WaitForSeconds(action.delayBeforeAction);
            }

            // 3. Ejecutar acción según tipo
            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ⚙️ Ejecutando acción: {action.actionType}");

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

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Acción {action.actionType} completada");

            // Asegurar que el NPC vuelva a Idle después de la acción
            if (npcManager != null)
            {
                npcManager.ForceIdle();
                if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 🔄 NPC forzado a estado Idle");
            }

            // Disparar evento onPostActionCompleted si está configurado
            var questConfig = npcManager?.Configuration?.questConfig;
            if (questConfig != null && questConfig.questChain != null)
            {
                if (questIndex >= 0 && questIndex < questConfig.questChain.Length)
                {
                    var entry = questConfig.questChain[questIndex];
                    // Usar reflexión para acceder al evento (workaround para Unity)
                    var fieldInfo = entry.GetType().GetField("onPostActionCompleted");
                    if (fieldInfo != null)
                    {
                        var unityEvent = fieldInfo.GetValue(entry) as UnityEngine.Events.UnityEvent;
                        unityEvent?.Invoke();
                        if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 📣 Evento onPostActionCompleted disparado");
                    }
                }
            }

            _isExecutingPostAction = false;
            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ✨ Post-action {questIndex} COMPLETADA");
        }

        private IEnumerator ExecuteMoveAction(QuestPostAction action)
        {
            Debug.Log($"[NPCQuestActionExecutor:{name}] 🚶 ExecuteMoveAction iniciado - Buscando anchor '{action.targetAnchorName}'");

            // Verificar que tenemos un anchor válido
            if (string.IsNullOrEmpty(action.targetAnchorName))
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌ targetAnchorName está vacío - se requiere anchor para Move");
                yield break;
            }

            // Buscar el anchor
            var targetAnchor = SpawnAnchor.FindById(action.targetAnchorName);
            if (targetAnchor == null)
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌ SpawnAnchor '{action.targetAnchorName}' NO ENCONTRADO");
                
                // Listar todos los anchors disponibles para debugging
                var allAnchors = Object.FindObjectsByType<SpawnAnchor>(FindObjectsSortMode.None);
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] 📋 Anchors disponibles: {allAnchors.Length}");
                foreach (var anchor in allAnchors)
                {
                    Debug.LogWarning($"  - ID: '{anchor.anchorId}' en posición {anchor.transform.position}");
                }
                
                yield break;
            }

            Vector3 targetPosition = targetAnchor.transform.position;
            Quaternion targetRotation = targetAnchor.transform.rotation;
            float distance = Vector3.Distance(transform.position, targetPosition);

            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Anchor '{action.targetAnchorName}' ENCONTRADO");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Posición actual NPC: {transform.position}");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Posición destino: {targetPosition}");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Distancia: {distance:F2}m");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Rotación destino: {targetRotation.eulerAngles}");

            // Forzar salida de cinemática si el NPC está en ella
            if (npcManager.Context != null && npcManager.Context.IsInCinematic)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] 🎬 NPC está en cinemática, forzando a Idle");
                npcManager.ForceIdle();
                yield return new WaitForSeconds(0.2f);
            }

            // FASE 1: NPC camina visiblemente durante walkDisplayDuration o hasta llegar
            float walkTime = action.walkDisplayDuration;
            bool useTransition = action.useTransition && action.transitionSettings != null && walkTime < 999f;

            Debug.Log($"[NPCQuestActionExecutor:{name}] 🚶 Fase 1: Caminata visible durante {walkTime}s, useTransition={useTransition}");

            // Crear secuencia de movimiento para la caminata visible
            // ✅ Pasar el SpawnAnchor para que aplique la orientación correcta al llegar
            var moveSequence = new MoveToPositionSequence(
                npcManager,
                targetPosition,
                walkTime, // Duración de caminata visible
                action.turnAroundOnArrival, // Girar solo si no hay anchor
                walkTime, // Camina durante este tiempo
                targetAnchor // ✅ Pasar el anchor de destino
            );

            npcManager.StartCinematicSequence(moveSequence);

            // Esperar hasta que termine la caminata o timeout
            float elapsed = 0f;
            float timeout = walkTime + 1f;

            while (!moveSequence.IsCompleted && elapsed < timeout)
            {
                // Log cada 2 segundos
                if (Mathf.FloorToInt(elapsed / 2f) != Mathf.FloorToInt((elapsed + Time.deltaTime) / 2f))
                {
                    float remainingDistance = Vector3.Distance(transform.position, targetPosition);
                    Debug.Log($"[NPCQuestActionExecutor:{name}] ⏱️ Caminando: {elapsed:F1}s / {walkTime:F1}s, Distancia: {remainingDistance:F2}m");
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            float finalDistance = Vector3.Distance(transform.position, targetPosition);
            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Fase 1 completada en {elapsed:F2}s, Distancia restante: {finalDistance:F2}m");

            // FASE 2: Si no llegó Y hay transición configurada, hacer fade + teleport
            if (finalDistance > 1f && useTransition)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] 🎭 Fase 2: Iniciando transición para completar el movimiento");

                var transitionManager = EasyTransition.TransitionManager.Instance();
                if (transitionManager != null)
                {
                    bool transitionCompleted = false;

                    // Callback para teletransportar al NPC en el punto de corte
                    void OnCutPoint()
                    {
                        // ✅ FIX: Detener NavMeshAgent ANTES de teleportar para evitar movimiento residual
                        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null && agent.isOnNavMesh)
                        {
                            agent.isStopped = true;
                            agent.ResetPath();
                            agent.velocity = Vector3.zero;
                        }
                        
                        // ✅ FIX: Forzar salida del estado cinemático para evitar que siga procesando
                        if (npcManager != null)
                        {
                            npcManager.ForceIdle();
                        }
                        
                        transform.position = targetPosition;
                        transform.rotation = targetRotation;
                        
                        if (action.turnAroundOnArrival)
                        {
                            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
                        }
                        
                        // ✅ FIX: Re-colocar NavMeshAgent en la nueva posición
                        if (agent != null)
                        {
                            agent.Warp(targetPosition);
                        }
                        
                        Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 NPC teletransportado a {targetPosition} en punto de corte del fade");
                        transitionManager.onTransitionCutPointReached -= OnCutPoint;
                    }

                    void OnEnd()
                    {
                        transitionCompleted = true;
                        transitionManager.onTransitionEnd -= OnEnd;
                    }

                    transitionManager.onTransitionCutPointReached += OnCutPoint;
                    transitionManager.onTransitionEnd += OnEnd;

                    transitionManager.Transition(action.transitionSettings, action.transitionDelay);

                    // Esperar a que termine la transición
                    float transitionTimeout = action.transitionDelay + action.transitionSettings.transitionTime + action.transitionSettings.destroyTime + 2f;
                    elapsed = 0f;

                    while (!transitionCompleted && elapsed < transitionTimeout)
                    {
                        yield return null;
                        elapsed += Time.deltaTime;
                    }

                    Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Transición completada en {elapsed:F2}s");
                }
                else
                {
                    Debug.LogWarning($"[NPCQuestActionExecutor:{name}] ⚠️ TransitionManager no disponible, teletransporte directo");
                    transform.position = targetPosition;
                    transform.rotation = targetRotation;
                    
                    if (action.turnAroundOnArrival)
                    {
                        transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
                    }
                }
            }
            else if (finalDistance > 1f)
            {
                // Sin transición pero tampoco llegó, teletransporte directo
                Debug.Log($"[NPCQuestActionExecutor:{name}] ⚡ Sin transición, teletransporte directo al destino");
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                
                if (action.turnAroundOnArrival)
                {
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
                }
                
                Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 NPC colocado en: {transform.position}, Rotación: {transform.rotation.eulerAngles}");
            }
            else
            {
                // Llegó caminando
                Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ NPC llegó caminando al destino");
                
                // Aplicar la rotación del anchor
                transform.rotation = targetRotation;
                
                if (action.turnAroundOnArrival)
                {
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
                }
                
                Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 NPC en: {transform.position}, Rotación: {transform.rotation.eulerAngles}");
            }

            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Move COMPLETADO");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Posición final NPC: {transform.position}");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Rotación final NPC: {transform.rotation.eulerAngles}");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Anchor destino era: '{action.targetAnchorName}' en {targetPosition}");

            // Persistir la nueva posición del NPC
            if (npcManager != null && npcManager.persistLastPosition)
            {
                npcManager.lastPosition = transform.position; // Actualizar lastPosition
                Debug.Log($"[NPCQuestActionExecutor:{name}] 💾 Posición guardada: {npcManager.lastPosition}");
            }
        }

        private IEnumerator ExecuteTeleportAction(QuestPostAction action)
        {
            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 🌀 ExecuteTeleportAction iniciado");

            // Verificar que tenemos un anchor válido
            if (string.IsNullOrEmpty(action.targetAnchorName))
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌ targetAnchorName está vacío - no se puede teletransportar");
                yield break;
            }

            // Buscar el anchor
            var targetAnchor = SpawnAnchor.FindById(action.targetAnchorName);
            if (targetAnchor == null)
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌ SpawnAnchor '{action.targetAnchorName}' no encontrado");
                yield break;
            }

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Anchor encontrado: '{action.targetAnchorName}' en {targetAnchor.transform.position}");

            // 1. Teletransportar al PLAYER usando TeleportService
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 🎮 Teletransportando PLAYER a anchor '{action.targetAnchorName}'");
                
                // Usar el sistema de teletransporte existente con TransitionSettings
                var teleportService = TeleportService.Inst;
                if (teleportService != null)
                {
                    // Si hay transitionSettings configurado, usarlo
                    bool useTransitionForPlayer = action.useTransition && action.transitionSettings != null;
                    
                    teleportService.DoTeleportToAnchor(player, targetAnchor.transform, useTransitionForPlayer);
                    
                    // Esperar a que termine la transición
                    if (useTransitionForPlayer && action.transitionSettings != null)
                    {
                        float totalTransitionTime = action.transitionDelay + action.transitionSettings.transitionTime + action.transitionSettings.destroyTime;
                        if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ⏳ Esperando transición del player ({totalTransitionTime:F2}s)");
                        yield return new WaitForSeconds(totalTransitionTime);
                    }
                }
                else
                {
                    Debug.LogWarning($"[NPCQuestActionExecutor:{name}] ⚠️ TeleportService no disponible, usando teletransporte directo");
                    player.transform.position = targetAnchor.transform.position;
                    player.transform.rotation = targetAnchor.transform.rotation;
                }
            }
            else
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] ⚠️ Player no encontrado, solo se teletransportará el NPC");
            }

            // 2. Teletransportar al NPC al mismo anchor
            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 👤 Teletransportando NPC a anchor '{action.targetAnchorName}'");
            
            transform.position = targetAnchor.transform.position;
            transform.rotation = targetAnchor.transform.rotation;

            if (action.turnAroundOnArrival)
            {
                transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
            }

            if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Teletransporte completado - Player y NPC en anchor '{action.targetAnchorName}'");
            
            // Persistir la nueva posición del NPC
            if (npcManager != null && npcManager.persistLastPosition)
            {
                npcManager.lastPosition = transform.position; // Actualizar lastPosition
                // npcManager.SaveCurrentPosition(); // Método no existe en V2
                if (debugMode) Debug.Log($"[NPCQuestActionExecutor:{name}] 💾 Posición de teletransporte guardada en runtimePreset");
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
    }
}