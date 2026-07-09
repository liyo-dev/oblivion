using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.States;
using Sendero.Core.Feedback; // Para eventos narrativos
using EasyTransition;
using Invector.vCharacterController;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Ejecutor de cadenas narrativas interactivas.
    /// Procesa secuencialmente acciones (Hablar, Moverse, Combatir) configuradas en NPCInteractiveNarrativeConfig.
    /// </summary>
    public class NPCInteractiveNarrativeExecutor : MonoBehaviour
    {
        public const int COMPONENT_VERSION = 7; // Added diagnostic logs

        #region 🔌 Dependencies
        private NPCBehaviourManagerV2 _npcManager;
        private NPCInteractiveNarrativeConfig _config;
        private NPCAlertIconController _alertIconController;
        private Interactable _interactable; // Caché del componente
        private Transform _player;
        #endregion

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;
        
        #region 📊 State
        private bool _isExecuting;
        private bool _hasBeenUsed;
        private bool _hasDetectedPlayer;
        private int _currentActionIndex = -1;
        private float _lastExecutionEndTime = -999f;
        private const float POST_EXECUTION_COOLDOWN = 0.5f;
        
        // ✅ NUEVO: Cooldown especial cuando JoinParty falla por party lleno
        private float _joinPartyFailedUntil = -999f;
        private const float JOIN_PARTY_FAILED_COOLDOWN = 30f; // 30 segundos antes de reintentar
        
        private ConditionalNarrative _cachedActiveNarrative;
        private int _lastNarrativeCheckFrame = -1;
        private const int NARRATIVE_CHECK_INTERVAL = 10;
        
        private bool _profileReady = false;
        private bool _detectPlayerRoutineStarted = false;
        private bool _playerFrozenByDetection = false;
        private bool _wasTriggeredByDetection = false;
        private bool _chainWasTriggeredByDetection = false;
        private bool _playerLockedByPostChain = false;
        private Coroutine _postChainUnlockTimeout = null;

        private static readonly WaitForSeconds _waitHalfSecond = new WaitForSeconds(0.5f);
        private static readonly WaitForSeconds _waitPointTwo = new WaitForSeconds(0.2f);
        private static readonly WaitForSeconds _waitPointOne = new WaitForSeconds(0.1f);
        private static readonly WaitForSeconds _waitOneSecond = new WaitForSeconds(1f);
        #endregion
        
        #region 📢 Public API
        public bool IsExecuting => _isExecuting || (Time.time - _lastExecutionEndTime < POST_EXECUTION_COOLDOWN) || (Time.time < _joinPartyFailedUntil);
        public NPCBehaviourManagerV2 Manager => _npcManager;
        #endregion
        
        private ConditionalNarrative GetCachedActiveNarrative(bool forceRefresh = false)
        {
            int currentFrame = Time.frameCount;
            if (!forceRefresh && currentFrame - _lastNarrativeCheckFrame < NARRATIVE_CHECK_INTERVAL)
            {
                return _cachedActiveNarrative;
            }
            
            _lastNarrativeCheckFrame = currentFrame;
            _cachedActiveNarrative = _config?.GetActiveNarrative();
            return _cachedActiveNarrative;
        }
        
        private void InvalidateNarrativeCache()
        {
            _lastNarrativeCheckFrame = -1;
            _cachedActiveNarrative = null;
        }

        // Usa el persistenceId del manager si está definido; si no, el del config SO (backward compat con presets antiguos)
        private string GetEffectivePersistenceId()
        {
            if (_npcManager != null && !string.IsNullOrEmpty(_npcManager.PersistenceId))
                return _npcManager.PersistenceId;
            return _config?.persistenceId;
        }

        private void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _interactable = GetComponent<Interactable>();
            
            if (_npcManager == null) 
            {
                Debug.LogError($"[NarrativeExecutor:{name}] ❌ Falta NPCBehaviourManagerV2");
                return;
            }
            
            if (_npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
        }

        private void OnEnable()
        {
            NPCInteractiveNarrativeRegistry.Register(this);
            GameBootService.OnProfileReady += HandleProfileReady;
            ProfileReadyDiagnostics.RegisterSubscriber(nameof(NPCInteractiveNarrativeExecutor));
            
            // Suscribirse a eventos de quest para auto-ejecución
            SubscribeToQuestEvents();
            
            // ⚠️ NO suscribirse a eventos custom aquí - se hará en Start() después de RestoreState()
            // para evitar que eventos pendientes de sesiones anteriores activen narrativas ya completadas
        }
        
        private void OnDisable()
        {
            NPCInteractiveNarrativeRegistry.Unregister(this);
            GameBootService.OnProfileReady -= HandleProfileReady;
            
            // Desuscribirse de eventos de quest
            UnsubscribeFromQuestEvents();
            
            // Desuscribirse de eventos custom
            UnsubscribeFromCustomEvents();
        }
        
        #region Quest Auto-Execute
        
        private bool _subscribedToQuests = false;
        
        private void SubscribeToQuestEvents()
        {
            if (_subscribedToQuests) return;
            
            var questManager = QuestManager.Instance;
            if (questManager != null)
            {
                questManager.OnQuestCompleted += OnQuestCompletedHandler;
                questManager.OnQuestStarted += OnQuestStartedHandler;
                questManager.OnStepCompleted += OnStepCompletedHandler;
                _subscribedToQuests = true;
            }
            else
            {
                // Reintentar después si QuestManager aún no está listo
                StartCoroutine(RetrySubscribeToQuests());
            }
        }
        
        private void UnsubscribeFromQuestEvents()
        {
            if (!_subscribedToQuests) return;
            
            var questManager = QuestManager.Instance;
            if (questManager != null)
            {
                questManager.OnQuestCompleted -= OnQuestCompletedHandler;
                questManager.OnQuestStarted -= OnQuestStartedHandler;
                questManager.OnStepCompleted -= OnStepCompletedHandler;
            }
            _subscribedToQuests = false;
        }
        
        private IEnumerator RetrySubscribeToQuests()
        {
            yield return _waitHalfSecond;
            SubscribeToQuestEvents();
        }
        
        /// <summary>
        /// Llamado cuando se completa una quest - verifica si hay narrativas que deban ejecutarse automáticamente
        /// </summary>
        private void OnQuestCompletedHandler(string questId)
        {
            CheckAndAutoExecuteQuestNarrative(questId, NarrativeConditionType.QuestCompleted);
        }
        
        /// <summary>
        /// Llamado cuando se inicia una quest - verifica si hay narrativas que deban ejecutarse automáticamente
        /// </summary>
        private void OnQuestStartedHandler(string questId)
        {
            CheckAndAutoExecuteQuestNarrative(questId, NarrativeConditionType.QuestStarted);
        }

        /// <summary>
        /// Llamado cuando se completa un step - verifica narrativas QuestStepCompleted con autoExecute
        /// </summary>
        private void OnStepCompletedHandler(string questId, int stepIndex)
        {
            if (_config == null || _config.conditionalNarratives == null) return;
            if (_isExecuting) return;

            foreach (var narrative in _config.conditionalNarratives)
            {
                if (narrative == null) continue;
                if (!narrative.autoExecuteOnQuestConditionMet) continue;
                if (narrative.condition.conditionType != NarrativeConditionType.QuestStepCompleted) continue;
                if (narrative.condition.targetQuest == null) continue;
                if (narrative.condition.targetQuest.questId != questId) continue;

                if (narrative.CanExecute())
                {
                    if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 🎯 Auto-ejecutando narrativa '{narrative.description}' porque step {stepIndex} de quest '{questId}' fue completado");
                    StartCoroutine(AutoExecuteNarrative(narrative));
                    return;
                }
            }
        }

        /// <summary>
        /// Verifica si hay narrativas con autoExecuteOnQuestConditionMet que deben ejecutarse
        /// </summary>
        private void CheckAndAutoExecuteQuestNarrative(string questId, NarrativeConditionType expectedConditionType)
        {
            if (_config == null || _config.conditionalNarratives == null) return;
            if (_isExecuting) return; // Ya ejecutando otra narrativa
            
            foreach (var narrative in _config.conditionalNarratives)
            {
                if (narrative == null) continue;
                if (!narrative.autoExecuteOnQuestConditionMet) continue;
                
                // Verificar si la condición es del tipo correcto y para esta quest
                if (narrative.condition.conditionType != expectedConditionType) continue;
                if (narrative.condition.targetQuest == null) continue;
                if (narrative.condition.targetQuest.questId != questId) continue;
                
                // Verificar si puede ejecutarse (condición cumplida y no usada si es singleUse)
                if (narrative.CanExecute())
                {
                    if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 🎯 Auto-ejecutando narrativa '{narrative.description}' porque quest '{questId}' cumplió condición {expectedConditionType}");
                    
                    // Ejecutar la narrativa
                    StartCoroutine(AutoExecuteNarrative(narrative));
                    return; // Solo ejecutar una
                }
            }
        }
        
        /// <summary>
        /// Ejecuta una narrativa automáticamente (sin interacción del jugador)
        /// </summary>
        private IEnumerator AutoExecuteNarrative(ConditionalNarrative narrative)
        {
            // Pequeña espera para que el sistema se estabilice
            yield return _waitPointTwo;
            
            if (_isExecuting) yield break;
            
            // Invalidar caché y forzar esta narrativa
            InvalidateNarrativeCache();
            _cachedActiveNarrative = narrative;
            
            // Ejecutar
            TryExecuteNarrative();
        }
        
        #endregion
        
        #region Custom Event Auto-Execute
        
        private readonly Dictionary<string, System.Action> _customEventHandlers = new();

        private void SubscribeToCustomEvents()
        {
            if (_config == null || _config.conditionalNarratives == null) return;

            var signals = DefaultNarrativeSignals.Instance;
            if (signals == null)
            {
                // Reintentar después si DefaultNarrativeSignals aún no está listo
                StartCoroutine(RetrySubscribeToCustomEvents());
                return;
            }

            foreach (var narrative in _config.conditionalNarratives)
            {
                if (narrative == null) continue;
                if (!narrative.condition.RequiresCustomEventSubscription) continue;

                string eventKey = narrative.condition.customEventKey;
                if (_customEventHandlers.ContainsKey(eventKey)) continue; // Ya suscrito

                // Crear handler para este evento
                var narrativeRef = narrative; // Captura local para el closure
                System.Action handler = () => OnCustomEventReceived(eventKey, narrativeRef);
                _customEventHandlers[eventKey] = handler;

                signals.OnCustom(eventKey, handler);

                if (verboseLogging || narrative.condition.debugMode)
                    Debug.Log($"[NarrativeExecutor:{name}] 📡 Suscrito a evento custom '{eventKey}'");
            }
        }

        /// <summary>
        /// Llamado cuando DefaultNarrativeSignals hace ResetState() (el grafo narrativo se reinicia).
        /// _custom queda borrado, así que re-registramos nuestros handlers.
        /// </summary>
        private void OnSignalsReset()
        {
            // Borrar caché local para que SubscribeToCustomEvents() los registre de nuevo
            _customEventHandlers.Clear();
            SubscribeToCustomEvents();
            if (verboseLogging)
                Debug.Log($"[NarrativeExecutor:{name}] 🔄 Re-suscrito a eventos custom tras reset de señales");
        }

        private void UnsubscribeFromCustomEvents()
        {
            DefaultNarrativeSignals.OnAfterReset -= OnSignalsReset;

            var signals = DefaultNarrativeSignals.Instance;
            if (signals == null) return;

            foreach (var kvp in _customEventHandlers)
            {
                signals.OffCustom(kvp.Key, kvp.Value);
            }
            _customEventHandlers.Clear();
        }
        
        private IEnumerator RetrySubscribeToCustomEvents()
        {
            yield return _waitHalfSecond;
            SubscribeToCustomEvents();
        }
        
        /// <summary>
        /// Llamado cuando se recibe un evento custom
        /// </summary>
        private void OnCustomEventReceived(string eventKey, ConditionalNarrative narrative)
        {
            if (narrative == null) return;
            
            // ✅ IMPORTANTE: Verificar si la narrativa ya fue ejecutada (singleUse)
            // Esto previene que eventos pendientes de sesiones anteriores reactiven narrativas ya completadas
            if (narrative.singleUse && narrative.HasBeenExecuted)
            {
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ⏭️ Ignorando evento '{eventKey}' - narrativa '{narrative.description}' ya fue ejecutada (singleUse)");
                return;
            }
            
            // ✅ También verificar si está en la lista de completadas del preset (persistencia)
            if (_config != null && _config.persistState && !string.IsNullOrEmpty(GetEffectivePersistenceId()))
            {
                var preset = GameBootService.Profile?.runtimePreset;
                if (preset != null && preset.completedInteractiveNarratives != null)
                {
                    string narrativeId = GetConditionalNarrativeId(System.Array.IndexOf(_config.conditionalNarratives, narrative));
                    if (preset.completedInteractiveNarratives.Contains(narrativeId))
                    {
                        if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ⏭️ Ignorando evento '{eventKey}' - narrativa '{narrative.description}' ya completada en preset");
                        return;
                    }
                }
            }
            
            // Marcar que el evento fue recibido
            narrative.condition.MarkCustomEventReceived();
            
            if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 📨 Evento custom '{eventKey}' recibido para narrativa '{narrative.description}'");
            
            // Si tiene autoExecuteOnQuestConditionMet, auto-ejecutar
            if (narrative.autoExecuteOnQuestConditionMet && !_isExecuting)
            {
                if (narrative.CanExecute())
                {
                    if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 🎯 Auto-ejecutando narrativa '{narrative.description}' por evento custom '{eventKey}'");
                    StartCoroutine(AutoExecuteNarrative(narrative));
                }
            }
            
            // Invalidar caché para que se re-evalúe
            InvalidateNarrativeCache();
        }
        
        #endregion
        
        private void HandleProfileReady()
        {
            _profileReady = true;
            GameBootService.OnProfileReady -= HandleProfileReady;
            
            // Solo iniciar DetectPlayerRoutine si aún no se ha iniciado
            if (!_detectPlayerRoutineStarted && _config != null)
            {
                _detectPlayerRoutineStarted = true;
                StartCoroutine(DetectPlayerRoutine());
            }
        }
        
        public NPCInteractiveNarrativeConfig GetConfiguration()
        {
            if (_config == null && _npcManager != null && _npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
            return _config;
        }

        private void Start()
        {
            if (_config == null && _npcManager != null && _npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
            
            if (_npcManager == null || _npcManager.Configuration == null || _config == null) return;

            InitializeAlertIconController();

            if (_config.persistState && !string.IsNullOrEmpty(GetEffectivePersistenceId()))
            {
                RestoreState();
            }

            // ✅ Suscribirse a eventos custom DESPUÉS de RestoreState()
            // Esto evita que eventos pendientes de sesiones anteriores activen narrativas ya completadas
            DefaultNarrativeSignals.OnAfterReset += OnSignalsReset;
            SubscribeToCustomEvents();

            ApplyInitialLayer();
            
            // ✅ FIX: Solo iniciar si no se ha iniciado ya (evitar múltiples corrutinas)
            if (!_detectPlayerRoutineStarted)
            {
                _detectPlayerRoutineStarted = true;
                StartCoroutine(DetectPlayerRoutine());
            }
        }
        
        private void InitializeAlertIconController()
        {
            _alertIconController = GetComponent<NPCAlertIconController>();
            if (_alertIconController == null)
            {
                bool needsIconController = false;
                if (_config.conditionalNarratives != null)
                {
                    foreach (var narrative in _config.conditionalNarratives)
                    {
                        if (narrative != null && narrative.showPersistentIcon)
                        {
                            needsIconController = true;
                            break;
                        }
                    }
                }
                
                // También necesita controlador si hay narrativas con showAlertIcon en sus chain entries
                if (!needsIconController && _config.conditionalNarratives != null)
                {
                    foreach (var narrative in _config.conditionalNarratives)
                    {
                        if (narrative?.narrativeChain == null) continue;
                        foreach (var entry in narrative.narrativeChain)
                        {
                            if (entry != null && entry.showAlertIcon)
                            {
                                needsIconController = true;
                                break;
                            }
                        }
                        if (needsIconController) break;
                    }
                }

                if (needsIconController
                    || _npcManager.Configuration.combatConfig?.alertIconPrefab != null
                    || _npcManager.NarrativeAlertIconPrefab != null
                    || _config.alertIconPrefab != null)
                {
                    _alertIconController = gameObject.AddComponent<NPCAlertIconController>();
                }
            }
        }

        private void Update()
        {
            if (_config == null) return;

            if (_interactable != null)
            {
                if (_isExecuting)
                {
                    if (_interactable.enabled) _interactable.enabled = false;
                }
                else
                {
                    bool hasNarrative = GetCachedActiveNarrative() != null;
                    bool hasQuestModule = _npcManager?.Configuration != null &&
                                         _npcManager.Configuration.HasBehaviour(NPCBehaviourType.Quest) &&
                                         _npcManager.Configuration.questConfig != null;
                    bool shouldBeEnabled = hasNarrative || hasQuestModule;
                    if (_interactable.enabled != shouldBeEnabled) _interactable.enabled = shouldBeEnabled;
                }
            }

            if (!_isExecuting)
            {
                var activeNarrative = GetCachedActiveNarrative();
                if (activeNarrative != null)
                {
                    if (activeNarrative.showPersistentIcon) ShowPersistentIconIfNeeded(activeNarrative);
                    else HidePersistentIconIfActive();
                }
                else
                {
                    HidePersistentIconIfActive();
                }
            }
            else
            {
                HidePersistentIconIfActive();
            }
        }
        
        private void ShowPersistentIconIfNeeded(ConditionalNarrative narrative)
        {
            if (_alertIconController == null) InitializeAlertIconController();
            if (_alertIconController == null) return;

            GameObject iconPrefab = narrative.persistentIconPrefab ?? _npcManager.Configuration.combatConfig?.alertIconPrefab;

            if (iconPrefab != null && !_alertIconController.HasPersistentIcon)
            {
                _alertIconController.ShowPersistentIcon(iconPrefab);
            }
        }

        private void HidePersistentIconIfActive()
        {
            if (_alertIconController != null && _alertIconController.HasPersistentIcon)
            {
                _alertIconController.HideAlertIcon();
            }

        }

        // =================================================================================
        // 🎬 EXECUTION CORE
        // =================================================================================

        public bool TryExecuteNarrative()
        {
            // ✅ FIX: Usar IsExecuting (propiedad) para respetar todos los cooldowns
            if (IsExecuting || _config == null) return false;

            var activeNarrative = _config.GetActiveNarrative();
            if (activeNarrative == null) return false;

            var chain = activeNarrative.narrativeChain;
            if (chain == null || chain.Length == 0) return false;

            StartCoroutine(ExecuteNarrativeChain(chain, activeNarrative));
            return true;
        }

        private IEnumerator ExecuteNarrativeChain(NarrativeChainEntry[] chain, ConditionalNarrative narrativeData)
        {
            _isExecuting = true;
            _currentActionIndex = 0;
            _chainWasTriggeredByDetection = _wasTriggeredByDetection;
            _wasTriggeredByDetection = false;

            // Bloquear wander/idle durante toda la ejecución narrativa
            if (_npcManager?.Context != null)
                _npcManager.Context.IsInteracting = true;

            HidePersistentIconIfActive();

            if (_npcManager?.SimpleAnimator != null)
            {
                _npcManager.SimpleAnimator.AllowManualRotation = true;
            }

            // --- ANIMACIÓN DE INTERACCIÓN ---
            if (_npcManager?.SimpleAnimator != null && PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, toPlayer);

                if (angle <= 90f)
                {
                    _npcManager.SimpleAnimator.PlayOneShot("Greeting01_NoWeapon");
                }
                else
                {
                    _npcManager.SimpleAnimator.PlayOneShot("SenseSomethingStart_NoWeapon");
                }
            }
            
            if (_npcManager.RotateToPlayerOnInteract) yield return RotateToPlayer();

            for (int i = 0; i < chain.Length; i++)
            {
                var entry = chain[i];
                _currentActionIndex = i;
                
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] -> Executing Action #{i}: {entry.actionType}");

                if (entry.sendNarrativeEvent && entry.sendEventOnStart)
                    SendNarrativeEvent(entry.narrativeEventKey);

                yield return ExecuteAction(entry);

                if (entry.sendNarrativeEvent && !entry.sendEventOnStart)
                    SendNarrativeEvent(entry.narrativeEventKey);
            }

            _hasBeenUsed = true;
            
            if (narrativeData != null)
            {
                narrativeData.MarkAsExecuted();
                if (narrativeData.sendNarrativeEvent) SendNarrativeEvent(narrativeData.narrativeEventKey);
            }

            if (_config.persistState) SaveState();
            
            InvalidateNarrativeCache();
            yield return HandlePostNarrativeState(narrativeData);

            if (_npcManager?.SimpleAnimator != null)
            {
                _npcManager.SimpleAnimator.AllowManualRotation = false;
            }

            if (_playerFrozenByDetection && global::Core.PlayerInputManager.Instance != null)
            {
                global::Core.PlayerInputManager.Instance.PopUIMode();
                _playerFrozenByDetection = false;
            }

            if (narrativeData != null && narrativeData.lockPlayerAfterChain && !_playerLockedByPostChain
                && global::Core.PlayerInputManager.Instance != null)
            {
                global::Core.PlayerInputManager.Instance.PushUIMode();
                _playerLockedByPostChain = true;
                _postChainUnlockTimeout = StartCoroutine(PostChainUnlockTimeout(narrativeData.lockPlayerMaxDuration));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[NarrativeExecutor:{name}] 🔒 Player bloqueado post-cadena (máx {narrativeData.lockPlayerMaxDuration}s)");
#endif
            }

            if (_npcManager?.Context != null)
                _npcManager.Context.IsInteracting = false;

            _isExecuting = false;
            _lastExecutionEndTime = Time.time;
            if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ⏱️ Narrativa finalizada - Cooldown activo hasta {_lastExecutionEndTime + POST_EXECUTION_COOLDOWN:F2}s");
        }

        private IEnumerator ExecuteAction(NarrativeChainEntry entry)
        {
            // Mostrar icono de alerta antes de la acción si está configurado.
            // Si la detección ya mostró este icono (primer entry de un chain autoStart),
            // no lo duplicamos.
            if (entry.showAlertIcon)
            {
                bool alreadyShownAtDetection = _chainWasTriggeredByDetection && _currentActionIndex == 0;
                if (!alreadyShownAtDetection)
                {
                    var iconPrefab = entry.alertIconPrefab != null ? entry.alertIconPrefab
                                   : _npcManager.NarrativeAlertIconPrefab != null ? _npcManager.NarrativeAlertIconPrefab
                                   : _config.alertIconPrefab;
                    if (iconPrefab != null)
                    {
                        if (!_alertIconController) InitializeAlertIconController();
                        if (_alertIconController != null && !_alertIconController.HasActiveIcon)
                        {
                            _alertIconController.SetIconOffset(entry.alertIconOffset);
                            _alertIconController.ShowAlertIcon(iconPrefab, entry.alertIconDuration);
                        }
                        yield return new WaitForSeconds(entry.alertIconDuration);
                    }
                }
            }

            switch (entry.actionType)
            {
                case NarrativeActionType.Dialogue:       yield return ExecuteDialogue(entry); break;
                case NarrativeActionType.Move:           yield return ExecuteMove(entry); break;
                case NarrativeActionType.MoveNearPlayer:    yield return ExecuteMoveNearPlayer(entry); break;
                case NarrativeActionType.LeadPlayerToAnchor:  yield return ExecuteLeadPlayerToAnchor(entry); break;
                case NarrativeActionType.TeleportNearPlayer:  yield return ExecuteTeleportNearPlayer(entry); break;
                case NarrativeActionType.PlayAnimation:        yield return ExecuteAnimation(entry); break;
                case NarrativeActionType.StartQuest:    yield return ExecuteStartQuest(entry); break;
                case NarrativeActionType.StartCombat:   yield return ExecuteStartCombat(entry); break;
                case NarrativeActionType.Wait:          yield return new WaitForSeconds(entry.waitDuration); break;
                case NarrativeActionType.JoinParty:     yield return ExecuteJoinParty(); break;
                case NarrativeActionType.LeaveParty:    yield return ExecuteLeaveParty(); break;
                case NarrativeActionType.CheckPartyMembers: yield return ExecuteCheckPartyMembers(); break;
                case NarrativeActionType.TeleportPlayer:    yield return ExecuteTeleportPlayer(entry); break;
                case NarrativeActionType.ScreenFade:        yield return ExecuteScreenFade(entry); break;
            }
        }

        // =================================================================================
        // 🎭 ACTIONS IMPLEMENTATION
        // =================================================================================

        private IEnumerator ExecuteDialogue(NarrativeChainEntry entry)
        {
            if (entry.dialogue == null || DialogueManager.Instance == null) yield break;

            // Reproducir animación de interacción igual que Interactable.StartDialogue()
            if (_npcManager?.SimpleAnimator != null &&
                (_npcManager.Context == null || !_npcManager.Context.IsInCombat))
            {
                _npcManager.SimpleAnimator.PlayOneShot("InteractWithPeople_NoWeapon");
                yield return null;
            }

            bool completed = false;
            DialogueManager.Instance.StartDialogue(entry.dialogue, transform, () => completed = true);
            while (!completed) yield return null;
            yield return _waitPointOne;
        }

        private IEnumerator ExecuteMove(NarrativeChainEntry entry)
        {
            Vector3 targetPos = GetTargetPosition(entry);
            if (targetPos == Vector3.zero) yield break;

            SpawnAnchor targetAnchor = null;
            if (!string.IsNullOrEmpty(entry.targetAnchorName))
            {
                targetAnchor = SpawnAnchor.FindById(entry.targetAnchorName);
            }
            
            // ✅ FIX: Desactivar AllowManualRotation durante el movimiento
            // para que la rotación automática funcione y el NPC mire hacia donde camina
            if (_npcManager?.SimpleAnimator != null)
            {
                _npcManager.SimpleAnimator.AllowManualRotation = false;
                _npcManager.SimpleAnimator.EnableAutoRotation();
                
                // Rotar hacia el destino antes de empezar a caminar
                Vector3 directionToTarget = targetPos - transform.position;
                directionToTarget.y = 0f;
                if (directionToTarget.sqrMagnitude > 0.01f)
                {
                    _npcManager.SimpleAnimator.FaceDirection(directionToTarget.normalized);
                }
            }

            if (entry.waitForPlayer)
            {
                yield return ExecuteMoveWithPlayerFollow(entry, targetPos, targetAnchor);
            }
            else
            {
                var moveSeq = new MoveToPositionSequence(_npcManager, targetPos, entry.maxMovementDuration, entry.turnAroundOnArrival, 999f, targetAnchor);
                _npcManager.StartCinematicSequence(moveSeq);
                while (!moveSeq.IsCompleted) yield return null;
            }

            // ✅ FIX: Restaurar AllowManualRotation después del movimiento
            // por si hay más acciones narrativas que necesiten control manual
            if (_npcManager?.SimpleAnimator != null)
            {
                _npcManager.SimpleAnimator.AllowManualRotation = true;
            }

            if (entry.disappearOnArrival)
            {
                var tm = TransitionManager.Instance();
                if (entry.disappearTransition != null && tm != null)
                {
                    tm.Transition(entry.disappearTransition, 0f);
                    float coverTime = entry.disappearTransition.autoAdjustTransitionTime
                        ? entry.disappearTransition.transitionTime / entry.disappearTransition.transitionSpeed
                        : entry.disappearTransition.transitionTime;
                    yield return new WaitForSecondsRealtime(coverTime);
                }
                var agent = _npcManager.Agent;
                if (agent != null && agent.isOnNavMesh)
                    agent.Warp(targetPos);
                else
                    transform.position = targetPos;
            }
        }

        /// <summary>
        /// Mueve al NPC a una posición cercana al jugador. Útil para que un NPC
        /// se acerque al jugador y le inicie un diálogo tras recibir un evento.
        /// </summary>
        private IEnumerator ExecuteMoveNearPlayer(NarrativeChainEntry entry)
        {
            Debug.Log($"[NarrativeExecutor:{name}] 🚶 MoveNearPlayer iniciado.");
            if (!PlayerService.TryGetPlayer(out var player, true))
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ MoveNearPlayer: no se encontró al jugador (PlayerService).");
                yield break;
            }

            // Calcular posición al lado del jugador: dirección desde el NPC hacia el jugador,
            // retrocediendo el radio para quedar frente a él.
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;

            Vector3 offsetDir = toPlayer.sqrMagnitude > 0.01f
                ? toPlayer.normalized
                : -transform.forward;

            Vector3 desiredPos = player.transform.position - offsetDir * entry.nearPlayerRadius;

            // Buscar posición válida en el NavMesh
            Vector3 targetPos = desiredPos;
            if (UnityEngine.AI.NavMesh.SamplePosition(desiredPos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                targetPos = hit.position;
            else if (UnityEngine.AI.NavMesh.SamplePosition(player.transform.position, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                targetPos = hit.position;
            else
            {
                // Fallback: posición del jugador directamente (sin NavMesh válido)
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ MoveNearPlayer: NavMesh.SamplePosition falló en {desiredPos} - usando posición directa del jugador.");
                targetPos = player.transform.position;
            }

            // Activar auto-rotación durante el movimiento
            if (_npcManager?.SimpleAnimator != null)
            {
                _npcManager.SimpleAnimator.AllowManualRotation = false;
                _npcManager.SimpleAnimator.EnableAutoRotation();

                Vector3 dir = targetPos - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    _npcManager.SimpleAnimator.FaceDirection(dir.normalized);
            }

            // Moverse
            var moveSeq = new MoveToPositionSequence(_npcManager, targetPos, entry.maxMovementDuration,
                                                      turnAroundOnArrival: false, walkDisplayDuration: 999f, targetAnchor: null);
            _npcManager.StartCinematicSequence(moveSeq);
            while (!moveSeq.IsCompleted) yield return null;

            // Restaurar rotación manual
            if (_npcManager?.SimpleAnimator != null)
                _npcManager.SimpleAnimator.AllowManualRotation = true;

            // Girar hacia el jugador al llegar
            if (entry.lookAtPlayerOnArrival)
                yield return RotateToPlayer();
        }

        /// <summary>
        /// Aparece instantáneamente al lado del jugador usando una transición de pantalla.
        /// Inverso del Move+disappearOnArrival: primero la pantalla se cubre, luego el NPC hace Warp junto al jugador.
        /// Usa los mismos campos que MoveNearPlayer (nearPlayerRadius, lookAtPlayerOnArrival)
        /// y disappearTransition para el efecto visual.
        /// </summary>
        private IEnumerator ExecuteTeleportNearPlayer(NarrativeChainEntry entry)
        {
            Debug.Log($"[NarrativeExecutor:{name}] ⚡ TeleportNearPlayer iniciado.");
            if (!PlayerService.TryGetPlayer(out var player, true))
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ TeleportNearPlayer: no se encontró al jugador (PlayerService).");
                yield break;
            }

            // ── Calcular posición válida en NavMesh al lado del jugador ──
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            Vector3 offsetDir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : -transform.forward;
            Vector3 desiredPos = entry.teleportNearPlayerRadius > 0f
                ? player.transform.position - offsetDir * entry.teleportNearPlayerRadius
                : player.transform.position;

            Vector3 targetPos = desiredPos;
            if (UnityEngine.AI.NavMesh.SamplePosition(desiredPos, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
                targetPos = hit.position;
            else if (UnityEngine.AI.NavMesh.SamplePosition(player.transform.position, out hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                targetPos = hit.position;
            else
            {
                // Fallback: posición del jugador directamente (sin NavMesh válido en el área)
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ TeleportNearPlayer: NavMesh.SamplePosition falló en {desiredPos} - usando posición directa del jugador.");
                targetPos = player.transform.position;
            }

            // ── Transición de pantalla (in): cubre la pantalla ──
            var tm = TransitionManager.Instance();
            if (entry.disappearTransition != null && tm != null)
            {
                tm.Transition(entry.disappearTransition, 0f);

                float coverTime = entry.disappearTransition.autoAdjustTransitionTime
                    ? entry.disappearTransition.transitionTime / entry.disappearTransition.transitionSpeed
                    : entry.disappearTransition.transitionTime;

                // Esperar hasta que la pantalla esté completamente cubierta
                yield return new WaitForSecondsRealtime(coverTime);
            }

            // ── Warp instantáneo ──
            var agent = _npcManager.Agent;
            if (agent != null && agent.isOnNavMesh)
                agent.Warp(targetPos);
            else
                transform.position = targetPos;

            // ── La transición continúa y hace el fade-out automáticamente ──
            // Girar hacia el jugador al aparecer
            if (entry.lookAtPlayerOnArrival)
                yield return RotateToPlayer();

            if (verboseLogging)
                Debug.Log($"[NarrativeExecutor:{name}] ⚡ TeleportNearPlayer: apareció junto al jugador en {targetPos}");
        }

        /// <summary>
        /// Teletransporta al JUGADOR a un anchor o Transform con transición de pantalla.
        /// Usa 'targetAnchorName' o 'targetTransform' (sección Movement) para el destino
        /// y 'teleportTransition' (sección Teleport Player) para el efecto visual.
        /// La pantalla se cubre → el jugador se mueve → la transición hace fade-out automáticamente.
        /// </summary>
        private IEnumerator ExecuteTeleportPlayer(NarrativeChainEntry entry)
        {
            Debug.Log($"[NarrativeExecutor:{name}] 🚀 TeleportPlayer iniciado.");

            // ── Resolver destino ──
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            bool hasTarget = false;

            if (entry.targetTransform != null)
            {
                targetPos = entry.targetTransform.position;
                targetRot = entry.targetTransform.rotation;
                hasTarget = true;
            }
            else if (!string.IsNullOrEmpty(entry.targetAnchorName))
            {
                var anchor = SpawnAnchor.FindById(entry.targetAnchorName);
                if (anchor != null)
                {
                    targetPos = anchor.transform.position;
                    targetRot = anchor.GetCharacterRotation();
                    hasTarget = true;
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ TeleportPlayer: anchor '{entry.targetAnchorName}' no encontrado.");
                    yield break;
                }
            }
            else
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ TeleportPlayer: no se configuró 'targetTransform' ni 'targetAnchorName'.");
                yield break;
            }

            if (!hasTarget) yield break;

            if (!PlayerService.TryGetPlayer(out var player, true))
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ TeleportPlayer: no se encontró al jugador (PlayerService).");
                yield break;
            }

            // ── Transición de entrada: cubre la pantalla ──
            var tm = TransitionManager.Instance();
            if (entry.teleportTransition != null && tm != null)
            {
                tm.Transition(entry.teleportTransition, 0f);

                float coverTime = entry.teleportTransition.autoAdjustTransitionTime
                    ? entry.teleportTransition.transitionTime / entry.teleportTransition.transitionSpeed
                    : entry.teleportTransition.transitionTime;

                yield return new WaitForSecondsRealtime(coverTime);
            }

            // ── Teletransporte del jugador ──
            // Desactivar CharacterController para que Unity permita mover el Transform
            var charCtrl = player.GetComponent<UnityEngine.CharacterController>();
            if (charCtrl != null) charCtrl.enabled = false;
            player.transform.position = targetPos;
            player.transform.rotation = targetRot;
            if (charCtrl != null) charCtrl.enabled = true;

            // ── La transición hace el fade-out automáticamente ──
            if (verboseLogging)
                Debug.Log($"[NarrativeExecutor:{name}] 🚀 TeleportPlayer: jugador teletransportado a {targetPos}");
        }

        /// <summary>
        /// El NPC camina hacia un anchor mientras el jugador debe seguirle.
        /// Si el jugador se aleja más de 'escortMaxPlayerDistance', el NPC para y vuelve a buscarlo.
        /// Cuando el jugador está de nuevo cerca, el NPC reanuda su camino al anchor.
        /// </summary>
        private IEnumerator ExecuteLeadPlayerToAnchor(NarrativeChainEntry entry)
        {
            // Verificar que el anchor existe antes de hacer nada
            if (!string.IsNullOrEmpty(entry.targetAnchorName))
            {
                var foundAnchor = SpawnAnchor.FindById(entry.targetAnchorName);
                if (foundAnchor == null)
                {
                    Debug.LogError($"[NarrativeExecutor:{name}] ❌ LeadPlayerToAnchor: No se encontró anchor con ID '{entry.targetAnchorName}'. " +
                                   $"Anchors registrados: [{string.Join(", ", AnchorRegistry.All.Keys)}]");
                    yield break;
                }
            }
            else if (entry.targetTransform == null)
            {
                Debug.LogError($"[NarrativeExecutor:{name}] ❌ LeadPlayerToAnchor: No hay targetAnchorName ni targetTransform asignado.");
                yield break;
            }

            Vector3 anchorPos = GetTargetPosition(entry);

            if (!PlayerService.TryGetPlayer(out var player, true))
            {
                Debug.LogError($"[NarrativeExecutor:{name}] ❌ LeadPlayerToAnchor: No se encontró al jugador.");
                yield break;
            }

            var agent = _npcManager.Agent;
            if (agent == null)
            {
                Debug.LogError($"[NarrativeExecutor:{name}] ❌ LeadPlayerToAnchor: No hay NavMeshAgent.");
                yield break;
            }

            // Reactivar el agente si fue desactivado (p.ej. por NavMeshAgentForceStop en IdleState)
            if (!agent.enabled)
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ LeadPlayerToAnchor: Agente desactivado, reactivando...");
                agent.enabled = true;
                yield return null; // esperar un frame para que vuelva al NavMesh
            }

            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"[NarrativeExecutor:{name}] ❌ LeadPlayerToAnchor: Agente fuera del NavMesh (enabled={agent.enabled}). ¿Está en un NavMesh Surface?");
                yield break;
            }

            Debug.Log($"[NarrativeExecutor:{name}] 🚶 LeadPlayerToAnchor iniciado → anchor '{entry.targetAnchorName}' pos={anchorPos}");

            // Activar auto-rotación para que el NPC mire hacia donde camina
            if (_npcManager?.SimpleAnimator != null)
            {
                _npcManager.SimpleAnimator.AllowManualRotation = false;
                _npcManager.SimpleAnimator.EnableAutoRotation();
                Vector3 dirToAnchor = anchorPos - transform.position;
                dirToAnchor.y = 0f;
                if (dirToAnchor.sqrMagnitude > 0.01f)
                    _npcManager.SimpleAnimator.FaceDirection(dirToAnchor.normalized);
            }

            var escortSeq = new LeadPlayerToAnchorSequence(
                anchorPos,
                player.transform,
                entry.escortMaxDuration,
                entry.escortMaxPlayerDistance,
                entry.escortResumeDistance);

            // Velocidad del NPC durante la escolta
            float originalAgentSpeed = agent.speed;
            if (entry.escortNpcSpeed > 0f)
                agent.speed = entry.escortNpcSpeed;

            // Reducir la velocidad del jugador activo y de los miembros del partido
            vThirdPersonController playerController = null;
            float origFreeWalk = 0f, origFreeRun = 0f, origFreeSprint = 0f;
            float origStrafeWalk = 0f, origStrafeRun = 0f, origStrafeSprint = 0f;
            float mult = Mathf.Clamp(entry.escortPlayerSpeedMultiplier, 0.1f, 1f);
            if (mult < 1f && PlayerService.TryGetComponent<vThirdPersonController>(out playerController))
            {
                origFreeWalk    = playerController.freeSpeed.walkSpeed;
                origFreeRun     = playerController.freeSpeed.runningSpeed;
                origFreeSprint  = playerController.freeSpeed.sprintSpeed;
                origStrafeWalk  = playerController.strafeSpeed.walkSpeed;
                origStrafeRun   = playerController.strafeSpeed.runningSpeed;
                origStrafeSprint = playerController.strafeSpeed.sprintSpeed;

                playerController.freeSpeed.walkSpeed    = origFreeWalk   * mult;
                playerController.freeSpeed.runningSpeed = origFreeRun    * mult;
                playerController.freeSpeed.sprintSpeed  = origFreeSprint * mult;
                playerController.strafeSpeed.walkSpeed    = origStrafeWalk   * mult;
                playerController.strafeSpeed.runningSpeed = origStrafeRun    * mult;
                playerController.strafeSpeed.sprintSpeed  = origStrafeSprint * mult;
            }

            // Reducir también el NavMeshAgent de cada miembro del partido para que
            // sigan al jugador a la misma velocidad reducida y no adelanten al guardia
            var partyOriginalSpeeds = new System.Collections.Generic.Dictionary<UnityEngine.AI.NavMeshAgent, float>();
            if (mult < 1f && Game.NPC.PlayerParty.HasInstance)
            {
                foreach (var member in Game.NPC.PlayerParty.Instance.Members)
                {
                    var memberAgent = member?.NPCManager?.Agent;
                    if (memberAgent == null || !memberAgent.isOnNavMesh) continue;
                    partyOriginalSpeeds[memberAgent] = memberAgent.speed;
                    memberAgent.speed *= mult;
                }
            }

            _npcManager.StartCinematicSequence(escortSeq);

            while (!escortSeq.IsCompleted)
            {
                if (escortSeq.WaitingForRetrievedDialogue)
                {
                    Debug.Log($"[NarrativeExecutor:{name}] 🔔 WaitingForRetrievedDialogue=true | outOfRangeDialogue={entry.outOfRangeDialogue?.name ?? "NULL"} | lines={entry.outOfRangeDialogue?.lines?.Length ?? -1} | DM={DialogueManager.Instance != null}");
                    if (entry.outOfRangeDialogue != null && DialogueManager.Instance != null &&
                        entry.outOfRangeDialogue.lines != null && entry.outOfRangeDialogue.lines.Length > 0)
                    {
                        if (_npcManager?.SimpleAnimator != null)
                            _npcManager.SimpleAnimator.PlayOneShot("InteractWithPeople_NoWeapon");

                        bool dialogueDone = false;
                        Debug.Log($"[NarrativeExecutor:{name}] 💬 Iniciando outOfRangeDialogue: {entry.outOfRangeDialogue.name}");
                        DialogueManager.Instance.StartDialogue(entry.outOfRangeDialogue, transform, () => dialogueDone = true);
                        while (!dialogueDone) yield return null;
                        Debug.Log($"[NarrativeExecutor:{name}] ✅ outOfRangeDialogue completado");
                    }

                    escortSeq.AcknowledgePlayerRetrieved(_npcManager.Context);
                }

                yield return null;
            }

            // Restaurar velocidad del NPC
            if (entry.escortNpcSpeed > 0f)
                agent.speed = originalAgentSpeed;

            // Restaurar velocidad del jugador activo
            if (playerController != null)
            {
                playerController.freeSpeed.walkSpeed    = origFreeWalk;
                playerController.freeSpeed.runningSpeed = origFreeRun;
                playerController.freeSpeed.sprintSpeed  = origFreeSprint;
                playerController.strafeSpeed.walkSpeed    = origStrafeWalk;
                playerController.strafeSpeed.runningSpeed = origStrafeRun;
                playerController.strafeSpeed.sprintSpeed  = origStrafeSprint;
            }

            // Restaurar velocidad de los miembros del partido
            foreach (var kvp in partyOriginalSpeeds)
                if (kvp.Key != null) kvp.Key.speed = kvp.Value;

            if (_npcManager?.SimpleAnimator != null)
                _npcManager.SimpleAnimator.AllowManualRotation = true;

            if (verboseLogging)
                Debug.Log($"[NarrativeExecutor:{name}] LeadPlayer: llegado al anchor o tiempo agotado");
        }

        private IEnumerator ExecuteMoveWithPlayerFollow(NarrativeChainEntry entry, Vector3 targetPos, SpawnAnchor targetAnchor)
        {
            var agent = _npcManager.Agent;
            var player = PlayerService.Player;
            if (!agent || !player) yield break;

            agent.SetDestination(targetPos);
            agent.isStopped = false;
            bool waiting = false;
            float timer = 0;

            while (timer < entry.maxMovementDuration)
            {
                if (!agent.pathPending && agent.remainingDistance < 0.5f) break;

                float distToPlayer = Vector3.Distance(transform.position, player.transform.position);

                if (!waiting && distToPlayer > entry.maxPlayerDistance)
                {
                    waiting = true;
                    agent.isStopped = true;
                    _npcManager.SimpleAnimator?.SetMovementSpeed(0);
                }
                else if (waiting && distToPlayer <= entry.resumePlayerDistance)
                {
                    waiting = false;
                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }

                if (!waiting)
                {
                    _npcManager.SimpleAnimator?.SetMovementSpeed(agent.velocity.magnitude / agent.speed);
                    
                    // ✅ FIX: Rotar hacia la dirección del movimiento
                    if (agent.velocity.sqrMagnitude > 0.01f && _npcManager?.SimpleAnimator != null)
                    {
                        _npcManager.SimpleAnimator.FaceDirection(agent.velocity.normalized);
                    }
                }

                timer += Time.deltaTime;
                yield return null;
            }

            agent.isStopped = true;
            _npcManager.SimpleAnimator?.ResetMovement();
            
            SpawnAnchor anchor = targetAnchor ?? FindNearbySpawnAnchor(targetPos);
            
            if (anchor != null && !entry.turnAroundOnArrival)
            {
                transform.rotation = Quaternion.LookRotation(anchor.transform.forward, Vector3.up);
            }
            else if (entry.turnAroundOnArrival)
            {
                // turnAroundOnArrival tiene prioridad: girar hacia el jugador
                _npcManager?.SimpleAnimator?.FacePlayerInstantly();
            }
        }
        
        private SpawnAnchor FindNearbySpawnAnchor(Vector3 position)
        {
            const float searchRadiusSqr = 4f; // 2m radius
            SpawnAnchor closest = null;
            float closestDistanceSqr = searchRadiusSqr;
            
            foreach (var kvp in AnchorRegistry.All)
            {
                if (kvp.Value == null) continue;
                float distanceSqr = (kvp.Value.transform.position - position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closest = kvp.Value;
                }
            }
            return closest;
        }

        private IEnumerator ExecuteAnimation(NarrativeChainEntry entry)
        {
            var anim = _npcManager.Animator;
            if (!anim) yield break;

            if (entry.animationClip != null)
            {
                anim.Play(entry.animationClip.name);
                yield return new WaitForSeconds(entry.animationDuration > 0 ? entry.animationDuration : entry.animationClip.length);
            }
            else if (!string.IsNullOrEmpty(entry.animationTrigger))
            {
                anim.SetTrigger(entry.animationTrigger);
                yield return new WaitForSeconds(entry.animationDuration > 0 ? entry.animationDuration : 1.0f);
            }
        }

        private IEnumerator ExecuteStartQuest(NarrativeChainEntry entry)
        {
            if (entry.questToStart != null && QuestManager.Instance != null)
            {
                QuestManager.Instance.AddQuest(entry.questToStart);
                QuestManager.Instance.StartQuest(entry.questToStart.questId);
            }
            yield return null;
        }

        private IEnumerator ExecuteStartCombat(NarrativeChainEntry entry)
        {
            if (entry.combatConfig == null) yield break;
            
            SwitchToEnemyLayer();
            _npcManager.Configuration.combatConfig = entry.combatConfig;
            
            if (entry.sendEventOnDefeat && !string.IsNullOrEmpty(entry.defeatEventKey))
            {
                entry.combatConfig.sendEventOnDefeat = entry.sendEventOnDefeat;
                entry.combatConfig.defeatEventKey = entry.defeatEventKey;
                entry.combatConfig.sendDefeatEventBeforeDeath = entry.sendDefeatEventBeforeDeath;
            }
            
            if (!GetComponent<Damageable>())
            {
                var dmg = gameObject.AddComponent<Damageable>();
                dmg.SetMaxAndCurrent(entry.combatConfig.health, entry.combatConfig.health);
                dmg.SetDestroyOnDeath(false);
            }
            
            if (!GetComponent<NPCCombatLifecycleHandler>())
            {
                gameObject.AddComponent<NPCCombatLifecycleHandler>();
            }
            
            if (verboseLogging) Debug.Log($"[NarrativeExecutor] ✅ NPC preparado para combate - esperando detección natural del jugador");
            yield break;
        }

        /// <summary>
        /// Ejecuta la acción de unirse al equipo del jugador.
        /// </summary>
        private IEnumerator ExecuteJoinParty()
        {
            if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 🤝 Intentando unir al party...");
            
            var partyMember = GetComponent<Game.NPC.NPCPartyMember>();
            if (partyMember == null)
            {
                // Si no tiene el componente, intentar crearlo si tiene config
                if (_npcManager?.Configuration?.partyConfig != null)
                {
                    partyMember = gameObject.AddComponent<Game.NPC.NPCPartyMember>();
                    partyMember.SetConfig(_npcManager.Configuration.partyConfig);
                    if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 🤝 NPCPartyMember creado dinámicamente");
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No hay NPCPartyMember ni partyConfig configurado. No puede unirse al equipo.");
                    yield break;
                }
            }
            
            // ✅ NUEVO: Verificar si YA está en el party antes de intentar unirse
            if (partyMember.IsInParty)
            {
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ℹ️ {name} YA está en el party, acción JoinParty ignorada");
                
                // ✅ FIX: Activar cooldown largo para evitar que la narrativa se ejecute en bucle
                // Si ya está en el party, no tiene sentido seguir intentándolo
                _joinPartyFailedUntil = Time.time + JOIN_PARTY_FAILED_COOLDOWN;
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ⏰ Cooldown de {JOIN_PARTY_FAILED_COOLDOWN}s activado (ya en party)");
                
                yield break;
            }
            
            bool success = partyMember.JoinParty();
            if (success)
            {
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ✨ {name} se unió al equipo del jugador");
                // ✅ Resetear cooldown de fallo si logra unirse
                _joinPartyFailedUntil = -999f;
            }
            else
            {
                // ✅ MEJORADO: Verificar por qué falló y activar cooldown largo
                if (Game.NPC.PlayerParty.HasInstance)
                {
                    var party = Game.NPC.PlayerParty.Instance;
                    Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No se pudo unir al equipo. " +
                        $"Miembros actuales: {party.MemberCount}/{party.MaxSize}. " +
                        $"¿Está lleno? {party.IsFull}. " +
                        $"⏰ Cooldown de {JOIN_PARTY_FAILED_COOLDOWN}s activado.");
                    
                    // ✅ NUEVO: Activar cooldown largo para evitar spam de reintentos
                    _joinPartyFailedUntil = Time.time + JOIN_PARTY_FAILED_COOLDOWN;
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No se pudo unir al equipo - PlayerParty no existe");
                    _joinPartyFailedUntil = Time.time + JOIN_PARTY_FAILED_COOLDOWN;
                }
            }
            
            yield return null;
        }

        /// <summary>
        /// Ejecuta la acción de abandonar el equipo del jugador.
        /// </summary>
        private IEnumerator ExecuteLeaveParty()
        {
            if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 👋 Intentando abandonar el party...");
            
            var partyMember = GetComponent<Game.NPC.NPCPartyMember>();
            if (partyMember == null)
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No hay NPCPartyMember. No puede abandonar el equipo.");
                yield break;
            }
            
            if (!partyMember.IsInParty)
            {
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ℹ️ {name} no está en el equipo");
                yield break;
            }
            
            bool success = partyMember.LeaveParty();
            if (success)
            {
                if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 👋 {name} abandonó el equipo del jugador");
            }
            
            yield return null;
        }

        /// <summary>
        /// Ejecuta la verificación de miembros del equipo para quests activas.
        /// Útil para asegurar que las quests se completan correctamente cuando se requiere un NPC en el party.
        /// </summary>
        private IEnumerator ExecuteCheckPartyMembers()
        {
            if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] 🔍 Verificando party members para quests activas...");
            
            var questManager = QuestManager.Instance;
            if (questManager == null)
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ QuestManager no está disponible");
                yield break;
            }
            
            questManager.ForceCheckPartyMembersForActiveQuests();
            
            yield return null;
        }

        private IEnumerator ExecuteScreenFade(NarrativeChainEntry entry)
        {
            if (entry.screenFadeTransition != null)
            {
                var tm = TransitionManager.Instance();
                if (tm != null)
                {
                    // Esperar a que termine cualquier transición en curso (timeout 3s)
                    float waited = 0f;
                    while (tm.IsRunning && waited < 3f)
                    {
                        waited += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    if (tm.IsRunning)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning($"[NarrativeExecutor:{name}] TransitionManager seguía ocupado tras 3s — forzando reset.");
#endif
                        tm.ForceResetTransition();
                    }

                    tm.Transition(entry.screenFadeTransition, 0f);
                    float duration = entry.screenFadeTransition.autoAdjustTransitionTime
                        ? entry.screenFadeTransition.transitionTime / entry.screenFadeTransition.transitionSpeed
                        : entry.screenFadeTransition.transitionTime;
                    yield return new WaitForSecondsRealtime(duration);
                }
                yield break;
            }

            if (entry.fadeDuration <= 0f)
            {
                FeedbackService.SetScreenFadeImmediate(entry.fadeIn ? entry.fadeColor : Color.clear);
                yield break;
            }
            yield return FeedbackService.ScreenFadeAsync(entry.fadeColor, entry.fadeDuration, entry.fadeIn);
        }

        private IEnumerator HandlePostNarrativeState(ConditionalNarrative narrativeData)
        {
            if (narrativeData == null || narrativeData.postNarrativeState == PostNarrativeState.None || !narrativeData.singleUse)
            {
                yield break;
            }
            
            if (verboseLogging) Debug.Log($"[NarrativeExecutor:{name}] ✅ Executing PostNarrativeState: {narrativeData.postNarrativeState} for narrative '{narrativeData.description}'");
            
            switch (narrativeData.postNarrativeState)
            {
                case PostNarrativeState.Idle:
                    _npcManager.ForceIdle();
                    break;
                case PostNarrativeState.Wander:
                case PostNarrativeState.SwitchToAmbient:
                    if (narrativeData.postNarrativeAmbientConfig != null)
                        _npcManager.Configuration.ambientConfig = narrativeData.postNarrativeAmbientConfig;
                    break;
                case PostNarrativeState.Disable:
                    yield return _waitHalfSecond;
                    gameObject.SetActive(false);
                    break;
            }
        }

        // =================================================================================
        // 🔍 UTILS & PERSISTENCE
        // =================================================================================

        private IEnumerator DetectPlayerRoutine()
        {
            yield return _waitOneSecond;

            while (true)
            {
                var activeNarrative = GetCachedActiveNarrative(false);
                bool hasNarrativeToDetect = activeNarrative != null && activeNarrative.autoStartOnDetection && !_hasDetectedPlayer && !IsExecuting;
                bool hasQuestToDetect = !_hasDetectedPlayer && !IsExecuting && ShouldAutoDetectForQuest();

                if (!hasNarrativeToDetect && !hasQuestToDetect)
                {
                    yield return _waitHalfSecond;
                    continue;
                }

                if (PlayerService.TryGetPlayer(out var p, false)) _player = p.transform;

                if (_player != null)
                {
                    float dist = Vector3.Distance(transform.position, _player.position);
                    if (dist <= _npcManager.NarrativeDetectionRange)
                    {
                        _hasDetectedPlayer = true;

                        if (hasNarrativeToDetect)
                        {
                            if (_playerLockedByPostChain) ReleasePostChainLock();
                            if (activeNarrative.freezePlayerOnDetection && global::Core.PlayerInputManager.Instance != null)
                            {
                                global::Core.PlayerInputManager.Instance.PushUIMode();
                                _playerFrozenByDetection = true;
                            }
                            yield return StartAlertSequence(activeNarrative);
                            _wasTriggeredByDetection = true;
                            TryExecuteNarrative();
                            _hasDetectedPlayer = false;
                        }
                        else if (hasQuestToDetect)
                        {
                            if (_playerLockedByPostChain) ReleasePostChainLock();
                            var questConfig = _npcManager.Configuration.questConfig;
                            questConfig.ProcessInteraction(_player.gameObject, _npcManager.Context);
                            // Esperar a que el diálogo abra y cierre antes de permitir re-detección.
                            // Sin esto el loop re-dispara mientras la quest sigue Inactive.
                            yield return _waitPointTwo;
                            var dm = DialogueManager.Instance;
                            if (dm != null && dm.IsOpen)
                            {
                                while (dm.IsOpen) yield return _waitHalfSecond;
                                yield return _waitPointTwo;
                            }
                            _hasDetectedPlayer = false;
                        }
                    }
                }
                yield return _waitPointTwo;
            }
        }

        private bool ShouldAutoDetectForQuest()
        {
            if (_npcManager?.Configuration == null) return false;
            if (!_npcManager.Configuration.HasBehaviour(NPCBehaviourType.Quest)) return false;
            var questConfig = _npcManager.Configuration.questConfig;
            if (questConfig == null || !questConfig.autoStartOnDetection) return false;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return false;

            var qm = QuestManager.Instance;
            if (qm == null) return false;

            foreach (var entry in questConfig.questChain)
            {
                if (entry?.questData == null) continue;
                if (qm.GetState(entry.questData.questId) == QuestState.Inactive && entry.dlgBefore != null)
                    return true;
            }
            return false;
        }

        private IEnumerator StartAlertSequence(ConditionalNarrative narrative = null)
        {
            var combatConfig = _npcManager.Configuration.combatConfig;
            GameObject iconPrefab = combatConfig?.alertIconPrefab;
            float iconDuration = combatConfig?.alertIconDuration ?? 1f;

            // Para NPCs sin combatConfig: buscar icono en las chain entries, en el manager o en el config SO
            if (iconPrefab == null && narrative?.narrativeChain != null)
            {
                foreach (var chainEntry in narrative.narrativeChain)
                {
                    if (chainEntry == null || !chainEntry.showAlertIcon) continue;
                    iconPrefab = chainEntry.alertIconPrefab;
                    if (iconPrefab == null) iconPrefab = _npcManager.NarrativeAlertIconPrefab;
                    if (iconPrefab == null) iconPrefab = _config.alertIconPrefab;
                    if (iconPrefab != null)
                    {
                        iconDuration = chainEntry.alertIconDuration;
                        break;
                    }
                }
            }


            if (iconPrefab)
            {
                if (!_alertIconController) InitializeAlertIconController();
                if (_alertIconController && !_alertIconController.HasActiveIcon)
                {
                    float iconHeight = combatConfig?.alertIconHeight ?? 2.5f;
                    if (iconHeight > 0) _alertIconController.SetIconHeight(iconHeight);
                    _alertIconController.ShowAlertIcon(iconPrefab, iconDuration);
                }
            }

            if (_npcManager.WalkTowardsPlayerOnAlert && _player != null)
            {
                var agent = _npcManager.Agent;
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.stoppingDistance = _npcManager.StopDistanceFromPlayer;
                    float t = 0;

                    while (t < iconDuration)
                    {
                        agent.SetDestination(_player.position);
                        _npcManager.SimpleAnimator?.SetMovementSpeed(agent.velocity.magnitude / agent.speed);
                        if (Vector3.Distance(transform.position, _player.position) <= _npcManager.StopDistanceFromPlayer) break;
                        t += Time.deltaTime;
                        yield return null;
                    }
                    agent.isStopped = true;
                    _npcManager.SimpleAnimator?.ResetMovement();
                }
            }
            else
            {
                yield return new WaitForSeconds(iconDuration);
            }
        }

        private Vector3 GetTargetPosition(NarrativeChainEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.targetAnchorName))
            {
                var anchor = SpawnAnchor.FindById(entry.targetAnchorName);
                if (anchor) return anchor.transform.position;
            }
            return entry.targetTransform ? entry.targetTransform.position : Vector3.zero;
        }

        private void SwitchToEnemyLayer()
        {
            if (_npcManager.SwitchToEnemyLayerOnCombat)
            {
                int layer = LayerMask.NameToLayer("Enemy");
                if (layer != -1) gameObject.layer = layer;
            }
        }
        
        private void ApplyInitialLayer()
        {
            if (_npcManager == null || _npcManager.InitialLayer == LayerMode.Custom) return;
            int layer = LayerMask.NameToLayer(_npcManager.InitialLayer.ToString());
            if (layer != -1) gameObject.layer = layer;
        }

        private IEnumerator RotateToPlayer()
        {
            if (!PlayerService.TryGetPlayer(out var p, true)) yield break;
            
            Vector3 dir = p.transform.position - transform.position;
            dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) yield break;
            
            Quaternion targetRotation = Quaternion.LookRotation(dir.normalized);
            if (Quaternion.Angle(transform.rotation, targetRotation) < 5f) yield break;
            
            float duration = _npcManager.RotationDuration;
            float elapsed = 0f;
            Quaternion startRotation = transform.rotation;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }
            transform.rotation = targetRotation;
        }

        private void SendNarrativeEvent(string key)
        {
            if (!string.IsNullOrEmpty(key))
                DefaultNarrativeSignals.Instance?.RaiseCustom(key, name);
        }
        
        private void SaveState()
        {
            string effectiveId = GetEffectivePersistenceId();
            if (string.IsNullOrEmpty(effectiveId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null) return;

            preset.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();

            if (!preset.completedInteractiveNarratives.Contains(effectiveId))
            {
                preset.completedInteractiveNarratives.Add(effectiveId);
            }
            
            if (_config.conditionalNarratives != null)
            {
                for (int i = 0; i < _config.conditionalNarratives.Length; i++)
                {
                    var narrative = _config.conditionalNarratives[i];
                    if (narrative != null && narrative.HasBeenExecuted && narrative.singleUse)
                    {
                        string narrativeId = GetConditionalNarrativeId(i);
                        if (!preset.completedInteractiveNarratives.Contains(narrativeId))
                        {
                            preset.completedInteractiveNarratives.Add(narrativeId);
                        }
                    }
                }
            }
        }

        private void RestoreState()
        {
            string effectiveId = GetEffectivePersistenceId();
            if (string.IsNullOrEmpty(effectiveId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset?.completedInteractiveNarratives == null) return;

            _hasBeenUsed = preset.completedInteractiveNarratives.Contains(effectiveId);

            if (_config.conditionalNarratives != null)
            {
                for (int i = 0; i < _config.conditionalNarratives.Length; i++)
                {
                    var narrative = _config.conditionalNarratives[i];
                    if (narrative != null)
                    {
                        narrative.ResetExecutionState();
                        string narrativeId = GetConditionalNarrativeId(i);
                        if (preset.completedInteractiveNarratives.Contains(narrativeId))
                        {
                            narrative.MarkAsExecuted();
                        }
                    }
                }

                // Catch-up para saves anteriores sin tracking per-narrativa (_CNn):
                // Solo aplica cuando el NPC ya fue usado antes (_hasBeenUsed=true).
                // Sin ese guarda, marcaríamos como ejecutadas narrativas aún pendientes en partidas nuevas.
                if (_hasBeenUsed)
                {
                    var qm = QuestManager.Instance;
                    if (qm != null)
                    {
                        for (int i = 0; i < _config.conditionalNarratives.Length; i++)
                        {
                            var narrative = _config.conditionalNarratives[i];
                            if (narrative == null || !narrative.singleUse || narrative.HasBeenExecuted) continue;
                            if (narrative.condition.conditionType != NarrativeConditionType.QuestStarted) continue;
                            if (narrative.condition.targetQuest == null) continue;

                            var questState = qm.GetState(narrative.condition.targetQuest.questId);
                            if (questState == QuestState.Completed || questState == QuestState.Active)
                            {
                                narrative.MarkAsExecuted();
                                if (verboseLogging)
                                    Debug.Log($"[NarrativeExecutor:{name}] 🔄 Catch-up: narrativa '{narrative.description}' marcada como ejecutada (quest {questState})");
                            }
                        }
                    }
                }
            }
        }
        
        private string GetConditionalNarrativeId(int index)
        {
            return $"{GetEffectivePersistenceId()}_CN{index}";
        }

        private void ReleasePostChainLock()
        {
            if (!_playerLockedByPostChain) return;
            if (_postChainUnlockTimeout != null)
            {
                StopCoroutine(_postChainUnlockTimeout);
                _postChainUnlockTimeout = null;
            }
            if (global::Core.PlayerInputManager.Instance != null)
                global::Core.PlayerInputManager.Instance.PopUIMode();
            _playerLockedByPostChain = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[NarrativeExecutor:{name}] 🔓 Player desbloqueado (lock post-cadena liberado)");
#endif
        }

        private IEnumerator PostChainUnlockTimeout(float duration)
        {
            yield return new WaitForSeconds(duration);
            if (_playerLockedByPostChain)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ Timeout post-cadena ({duration}s) alcanzado — desbloqueando player por failsafe");
#endif
                ReleasePostChainLock();
            }
        }

        public void ResetState(bool restoreFromPreset = true)
        {
            _hasBeenUsed = false;
            _hasDetectedPlayer = false;
            _isExecuting = false;
            _lastExecutionEndTime = -999f;

            if (_playerLockedByPostChain) ReleasePostChainLock();

            if (_npcManager?.Context != null)
                _npcManager.Context.IsInteracting = false;
            
            if (_config?.conditionalNarratives != null)
            {
                foreach (var narrative in _config.conditionalNarratives)
                {
                    narrative?.ResetExecutionState();
                }
            }
            
            if (restoreFromPreset && _config != null && _config.persistState)
            {
                RestoreState();
            }
        }
    }
}