using System.Collections;
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

        // Control de ejecución
        private bool _isExecutingPostAction = false;

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
                    Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Todas las quests completadas, ocultando icono persistente");
                
                npcManager.HidePersistentIcon();
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

            Debug.Log($"[NPCQuestActionExecutor:{name}] ▶️ ExecutePostQuestAction - questIndex={questIndex}, actionType={action.actionType}");

            // Ejecutar la acción con bloqueo
            StartCoroutine(ExecuteActionCoroutine(action, questIndex));
        }

        private IEnumerator ExecuteActionCoroutine(QuestPostAction action, int questIndex)
        {
            _isExecutingPostAction = true;

            Debug.Log($"[NPCQuestActionExecutor:{name}] 🎬 ExecuteActionCoroutine - actionType={action.actionType}");

            // Esperar un frame para asegurar que todos los callbacks de CompleteQuest han terminado
            yield return null;

            // 1. Diálogo pre-acción
            if (action.dialogueBeforeAction != null)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] 💬 Reproduciendo diálogo pre-acción");

                var dialogueManager = DialogueManager.Instance;
                if (dialogueManager != null)
                {
                    dialogueManager.StartDialogue(action.dialogueBeforeAction);

                    while (dialogueManager.IsOpen)
                    {
                        yield return null;
                    }
                }

                Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Diálogo pre-acción completado");
            }

            // 2. Espera opcional
            if (action.delayBeforeAction > 0f)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] ⏳ Esperando {action.delayBeforeAction}s");
                yield return new WaitForSeconds(action.delayBeforeAction);
            }

            // 3. Ejecutar acción según tipo
            Debug.Log($"[NPCQuestActionExecutor:{name}] ⚙️ Ejecutando acción: {action.actionType}");

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

            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Acción {action.actionType} completada");

            // Asegurar que el NPC salga del estado cinemático y vuelva a Idle
            if (npcManager != null)
            {
                npcManager.ExitCinematic();
                Debug.Log($"[NPCQuestActionExecutor:{name}] 🔄 NPC forzado a salir de cinemática → Idle");
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
                        Debug.Log($"[NPCQuestActionExecutor:{name}] 📣 Evento onPostActionCompleted disparado");
                    }
                }
            }

            _isExecutingPostAction = false;
            Debug.Log($"[NPCQuestActionExecutor:{name}] ✨ Post-action {questIndex} COMPLETADA");
        }

        private IEnumerator ExecuteMoveAction(QuestPostAction action)
        {
            Debug.Log($"[NPCQuestActionExecutor:{name}] 🚶 ExecuteMoveAction iniciado - SOLO MUEVE AL NPC");

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
                Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌ SpawnAnchor '{action.targetAnchorName}' no encontrado");
                yield break;
            }

            Vector3 targetPosition = targetAnchor.transform.position;
            float distance = Vector3.Distance(transform.position, targetPosition);

            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Destino: '{action.targetAnchorName}' en {targetPosition}");
            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 NPC actual: {transform.position}, Distancia: {distance:F2}m");

            // Forzar salida de cinemática si el NPC está en ella
            if (npcManager.Context != null && npcManager.Context.IsInCinematic)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] 🎬 NPC está en cinemática, forzando salida");
                npcManager.ExitCinematic();
                yield return new WaitForSeconds(0.2f);
            }

            // FASE 1: NPC camina visiblemente durante walkDisplayDuration o hasta llegar
            float walkTime = action.walkDisplayDuration;
            bool useTransition = action.useTransition && action.transitionSettings != null && walkTime < 999f;

            Debug.Log($"[NPCQuestActionExecutor:{name}] 🚶 Fase 1: Caminata visible durante {walkTime}s, useTransition={useTransition}");

            // Crear secuencia de movimiento para la caminata visible
            var moveSequence = new States.MoveToPoscionSequence(
                npcManager,
                targetPosition,
                walkTime, // Duración de caminata visible
                false, // No girar todavía
                walkTime // Camina durante este tiempo
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
                        transform.position = targetPosition;
                        transform.rotation = targetAnchor.transform.rotation;
                        
                        if (action.turnAroundOnArrival)
                        {
                            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
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
                    transform.rotation = targetAnchor.transform.rotation;
                    
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
                transform.rotation = targetAnchor.transform.rotation;
                
                if (action.turnAroundOnArrival)
                {
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
                }
            }
            else
            {
                // Llegó caminando
                Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ NPC llegó caminando al destino");
                
                if (action.turnAroundOnArrival)
                {
                    transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
                }
            }

            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Move completado - NPC en '{action.targetAnchorName}' (pos: {transform.position})");

            // Persistir la nueva posición del NPC
            if (npcManager != null && npcManager.persistLastPosition)
            {
                npcManager.SaveCurrentPosition();
                Debug.Log($"[NPCQuestActionExecutor:{name}] 💾 Posición guardada en runtimePreset");
            }
        }

        private IEnumerator ExecuteTeleportAction(QuestPostAction action)
        {
            Debug.Log($"[NPCQuestActionExecutor:{name}] 🌀 ExecuteTeleportAction iniciado");

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

            Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 Anchor encontrado: '{action.targetAnchorName}' en {targetAnchor.transform.position}");

            // 1. Teletransportar al PLAYER usando TeleportService
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] 🎮 Teletransportando PLAYER a anchor '{action.targetAnchorName}'");
                
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
                        Debug.Log($"[NPCQuestActionExecutor:{name}] ⏳ Esperando transición del player ({totalTransitionTime:F2}s)");
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
            Debug.Log($"[NPCQuestActionExecutor:{name}] 👤 Teletransportando NPC a anchor '{action.targetAnchorName}'");
            
            transform.position = targetAnchor.transform.position;
            transform.rotation = targetAnchor.transform.rotation;

            if (action.turnAroundOnArrival)
            {
                transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y + 180f, 0f);
            }

            Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Teletransporte completado - Player y NPC en anchor '{action.targetAnchorName}'");
            
            // Persistir la nueva posición del NPC
            if (npcManager != null && npcManager.persistLastPosition)
            {
                npcManager.SaveCurrentPosition();
                Debug.Log($"[NPCQuestActionExecutor:{name}] 💾 Posición de teletransporte guardada en runtimePreset");
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
            string transformName = "NULL";
            if (action.targetTransform != null)
            {
                transformName = action.targetTransform.name;
            }
            
            Debug.Log($"[NPCQuestActionExecutor:{name}] 🎯 GetTargetPosition llamado - AnchorName: '{action.targetAnchorName}', Transform: {transformName}, useAnchorSystem: {useAnchorSystem}");

            // Prioridad 1: SpawnAnchor por ID (sistema existente usando AnchorRegistry)
            if (useAnchorSystem && !string.IsNullOrEmpty(action.targetAnchorName))
            {
                Debug.Log($"[NPCQuestActionExecutor:{name}] 🔍 Buscando SpawnAnchor: '{action.targetAnchorName}' en AnchorRegistry");

                var spawnAnchor = SpawnAnchor.FindById(action.targetAnchorName);
                if (spawnAnchor != null)
                {
                    Vector3 pos = spawnAnchor.transform.position;
                    Debug.Log($"[NPCQuestActionExecutor:{name}] ✅✅✅ SpawnAnchor encontrado: '{action.targetAnchorName}' en posición {pos} (GameObject: {spawnAnchor.name})");
                    Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 NPC actual en: {transform.position}, distancia al anchor: {Vector3.Distance(transform.position, pos):F2}m");
                    return pos;
                }
                else
                {
                    Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌❌❌ SpawnAnchor con ID '{action.targetAnchorName}' NO ENCONTRADO en AnchorRegistry");

                    // Listar todos los anchors registrados
                    var allAnchors = AnchorRegistry.All;
                    Debug.LogError($"[NPCQuestActionExecutor:{name}] Anchors registrados en AnchorRegistry: {allAnchors.Count}");
                    foreach (var kvp in allAnchors)
                    {
                        if (kvp.Value != null)
                        {
                            Debug.LogError($"  - '{kvp.Key}' en {kvp.Value.transform.position} (GO: {kvp.Value.name})");
                        }
                    }
                    
                    Debug.LogError($"[NPCQuestActionExecutor:{name}] ⚠️ No se puede continuar sin un anchor válido registrado.");
                }
            }
            else if (!useAnchorSystem)
            {
                Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌❌❌ useAnchorSystem está DESACTIVADO - Activa el checkbox 'Use Anchor System' en el inspector");
            }
            else if (string.IsNullOrEmpty(action.targetAnchorName))
            {
                Debug.LogWarning($"[NPCQuestActionExecutor:{name}] ⚠️ targetAnchorName está vacío, intentando usar Transform directo");
            }

            // Prioridad 2: Transform directo
            if (action.targetTransform != null)
            {
                Vector3 pos = action.targetTransform.position;
                Debug.Log($"[NPCQuestActionExecutor:{name}] ✅ Usando Transform directo: {action.targetTransform.name} en posición {pos}");
                Debug.Log($"[NPCQuestActionExecutor:{name}] 📍 NPC actual en: {transform.position}, distancia al transform: {Vector3.Distance(transform.position, pos):F2}m");
                return pos;
            }

            Debug.LogError($"[NPCQuestActionExecutor:{name}] ❌ No se pudo obtener target position - AnchorName: '{action.targetAnchorName}', Transform: NULL");
            return Vector3.zero;
        }
    }
}
