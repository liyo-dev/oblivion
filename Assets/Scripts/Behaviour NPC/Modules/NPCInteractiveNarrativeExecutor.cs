using System.Collections;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.States;
using Sendero.Core.Feedback; // Para eventos narrativos

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

        #region 📊 State
        private bool _isExecuting;
        private bool _hasBeenUsed;
        private bool _hasDetectedPlayer;
        private int _currentActionIndex = -1;
        
        private float _lastExecutionEndTime = -999f;
        private const float POST_EXECUTION_COOLDOWN = 0.5f;
        
        private ConditionalNarrative _cachedActiveNarrative;
        private int _lastNarrativeCheckFrame = -1;
        private const int NARRATIVE_CHECK_INTERVAL = 10;
        
        private static readonly WaitForSeconds _waitHalfSecond = new WaitForSeconds(0.5f);
        private static readonly WaitForSeconds _waitPointTwo = new WaitForSeconds(0.2f);
        private static readonly WaitForSeconds _waitPointOne = new WaitForSeconds(0.1f);
        private static readonly WaitForSeconds _waitOneSecond = new WaitForSeconds(1f);
        #endregion
        
        #region 📢 Public API
        public bool IsExecuting => _isExecuting || (Time.time - _lastExecutionEndTime < POST_EXECUTION_COOLDOWN);
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

        private void OnEnable() => NPCInteractiveNarrativeRegistry.Register(this);
        private void OnDisable() => NPCInteractiveNarrativeRegistry.Unregister(this);
        
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

            if (_config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                RestoreState();
            }

            ApplyInitialLayer();
            StartCoroutine(DetectPlayerRoutine());
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
                        if (narrative != null && (narrative.showPersistentIcon || DoesChainUseBubbles(narrative.narrativeChain)))
                        {
                            needsIconController = true;
                            break;
                        }
                    }
                }
                
                if (needsIconController || _config.alertIconPrefab != null)
                {
                    _alertIconController = gameObject.AddComponent<NPCAlertIconController>();
                }
            }
        }

        private bool DoesChainUseBubbles(NarrativeChainEntry[] chain)
        {
            if (chain == null) return false;
            foreach(var entry in chain)
            {
                if (entry.actionType == NarrativeActionType.ShowSpeechBubble) return true;
            }
            return false;
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
                    if (_interactable.enabled != hasNarrative) _interactable.enabled = hasNarrative;
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
            
            GameObject iconPrefab = narrative.persistentIconPrefab ?? _config?.alertIconPrefab;
            
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
            if (_isExecuting || _config == null) return false;

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
            
            if (_config.rotateToPlayerOnInteract) yield return RotateToPlayer();

            for (int i = 0; i < chain.Length; i++)
            {
                var entry = chain[i];
                _currentActionIndex = i;
                
                Debug.Log($"[NarrativeExecutor:{name}] -> Executing Action #{i}: {entry.actionType}");

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

            _isExecuting = false;
            _lastExecutionEndTime = Time.time;
            Debug.Log($"[NarrativeExecutor:{name}] ⏱️ Narrativa finalizada - Cooldown activo hasta {_lastExecutionEndTime + POST_EXECUTION_COOLDOWN:F2}s");
        }

        private IEnumerator ExecuteAction(NarrativeChainEntry entry)
        {
            switch (entry.actionType)
            {
                case NarrativeActionType.Dialogue:      yield return ExecuteDialogue(entry); break;
                case NarrativeActionType.Move:          yield return ExecuteMove(entry); break;
                case NarrativeActionType.PlayAnimation: yield return ExecuteAnimation(entry); break;
                case NarrativeActionType.StartQuest:    yield return ExecuteStartQuest(entry); break;
                case NarrativeActionType.StartCombat:   yield return ExecuteStartCombat(entry); break;
                case NarrativeActionType.Wait:          yield return new WaitForSeconds(entry.waitDuration); break;
                case NarrativeActionType.ShowSpeechBubble: yield return ExecuteShowSpeechBubble(entry); break;
                case NarrativeActionType.JoinParty:     yield return ExecuteJoinParty(); break;
                case NarrativeActionType.LeaveParty:    yield return ExecuteLeaveParty(); break;
            }
        }

        // =================================================================================
        // 🎭 ACTIONS IMPLEMENTATION
        // =================================================================================

        private IEnumerator ExecuteDialogue(NarrativeChainEntry entry)
        {
            if (entry.dialogue == null || DialogueManager.Instance == null) yield break;
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
                }

                timer += Time.deltaTime;
                yield return null;
            }

            agent.isStopped = true;
            _npcManager.SimpleAnimator?.ResetMovement();
            
            SpawnAnchor anchor = targetAnchor ?? FindNearbySpawnAnchor(targetPos);
            
            if (anchor != null)
            {
                Quaternion targetRotation = anchor.faceDoor ? Quaternion.LookRotation(-anchor.transform.forward, Vector3.up) : Quaternion.LookRotation(anchor.transform.forward, Vector3.up);
                transform.rotation = targetRotation;
            }
            else if (entry.turnAroundOnArrival)
            {
                transform.rotation *= Quaternion.Euler(0, 180, 0);
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
            
            Debug.Log($"[NarrativeExecutor] ✅ NPC preparado para combate - esperando detección natural del jugador");
            yield break;
        }

        private IEnumerator ExecuteShowSpeechBubble(NarrativeChainEntry entry)
        {
            Debug.Log($"[NarrativeExecutor:{name}] Attempting to show bubble. Text: '{entry.speechBubbleText}'");

            if (string.IsNullOrEmpty(entry.speechBubbleText)) 
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] SpeechBubble text is empty. Aborting action.");
                yield break;
            }

            if (_alertIconController == null) InitializeAlertIconController();
            if (_alertIconController == null)
            {
                Debug.LogError($"[NarrativeExecutor:{name}] NPCAlertIconController is missing and could not be created.");
                yield break;
            }

            GameObject prefabToUse = entry.speechBubblePrefabOverride ?? (entry.isThoughtBubble ? _config?.defaultThoughtBubblePrefab : _config?.defaultSpeechBubblePrefab);

            if (prefabToUse == null)
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No prefab for SpeechBubble (IsThought={entry.isThoughtBubble}). Assign one in NPCInteractiveNarrativeConfig or the NarrativeChainEntry.");
                yield break;
            }
            
            Debug.Log($"[NarrativeExecutor:{name}] Using prefab '{prefabToUse.name}' for speech bubble.");

            if (_config != null && _config.speechBubbleHeight > 0)
            {
                _alertIconController.SetIconHeight(_config.speechBubbleHeight);
            }

            _alertIconController.ShowSpeechBubble(prefabToUse, entry.speechBubbleText, entry.speechBubbleDuration);

            if (entry.waitForBubble)
            {
                yield return new WaitForSeconds(entry.speechBubbleDuration);
            }
        }

        /// <summary>
        /// Ejecuta la acción de unirse al equipo del jugador.
        /// </summary>
        private IEnumerator ExecuteJoinParty()
        {
            Debug.Log($"[NarrativeExecutor:{name}] 🤝 Intentando unir al party...");
            
            var partyMember = GetComponent<Game.NPC.NPCPartyMember>();
            if (partyMember == null)
            {
                // Si no tiene el componente, intentar crearlo si tiene config
                if (_npcManager?.Configuration?.partyConfig != null)
                {
                    partyMember = gameObject.AddComponent<Game.NPC.NPCPartyMember>();
                    partyMember.SetConfig(_npcManager.Configuration.partyConfig);
                    Debug.Log($"[NarrativeExecutor:{name}] 🤝 NPCPartyMember creado dinámicamente");
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No hay NPCPartyMember ni partyConfig configurado. No puede unirse al equipo.");
                    yield break;
                }
            }
            
            bool success = partyMember.JoinParty();
            if (success)
            {
                Debug.Log($"[NarrativeExecutor:{name}] ✨ {name} se unió al equipo del jugador");
            }
            else
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No se pudo unir al equipo (¿está lleno?)");
            }
            
            yield return null;
        }

        /// <summary>
        /// Ejecuta la acción de abandonar el equipo del jugador.
        /// </summary>
        private IEnumerator ExecuteLeaveParty()
        {
            Debug.Log($"[NarrativeExecutor:{name}] 👋 Intentando abandonar el party...");
            
            var partyMember = GetComponent<Game.NPC.NPCPartyMember>();
            if (partyMember == null)
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ No hay NPCPartyMember. No puede abandonar el equipo.");
                yield break;
            }
            
            if (!partyMember.IsInParty)
            {
                Debug.Log($"[NarrativeExecutor:{name}] ℹ️ {name} no está en el equipo");
                yield break;
            }
            
            bool success = partyMember.LeaveParty();
            if (success)
            {
                Debug.Log($"[NarrativeExecutor:{name}] 👋 {name} abandonó el equipo del jugador");
            }
            
            yield return null;
        }

        private IEnumerator HandlePostNarrativeState(ConditionalNarrative narrativeData)
        {
            if (narrativeData == null || narrativeData.postNarrativeState == PostNarrativeState.None || !narrativeData.singleUse)
            {
                yield break;
            }
            
            Debug.Log($"[NarrativeExecutor:{name}] ✅ Executing PostNarrativeState: {narrativeData.postNarrativeState} for narrative '{narrativeData.description}'");
            
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
                if (activeNarrative == null || !activeNarrative.autoStartOnDetection || _hasDetectedPlayer || _isExecuting)
                {
                    yield return _waitHalfSecond;
                    continue;
                }
                
                if (PlayerService.TryGetPlayer(out var p, false)) _player = p.transform;

                if (_player != null)
                {
                    float dist = Vector3.Distance(transform.position, _player.position);
                    if (dist <= _config.detectionRange)
                    {
                        _hasDetectedPlayer = true;
                        yield return StartAlertSequence();
                        TryExecuteNarrative();
                        _hasDetectedPlayer = false;
                    }
                }
                yield return _waitPointTwo;
            }
        }

        private IEnumerator StartAlertSequence()
        {
            GameObject iconPrefab = _config.alertIconPrefab ?? _npcManager.Configuration.combatConfig?.alertIconPrefab;

            if (iconPrefab)
            {
                if (!_alertIconController) InitializeAlertIconController();
                if (_alertIconController && !_alertIconController.HasActiveIcon)
                {
                    float iconHeight = _config.alertIconHeight > 0 ? _config.alertIconHeight : _npcManager.Configuration.combatConfig?.alertIconHeight ?? 0;
                    if (iconHeight > 0) _alertIconController.SetIconHeight(iconHeight);
                    _alertIconController.ShowAlertIcon(iconPrefab, _config.alertIconDuration);
                }
            }

            if (_config.walkTowardsPlayerOnAlert && _player != null)
            {
                var agent = _npcManager.Agent;
                if (agent && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.stoppingDistance = _config.stopDistanceFromPlayer;
                    float t = 0;
                    
                    while (t < _config.alertIconDuration)
                    {
                        agent.SetDestination(_player.position);
                        _npcManager.SimpleAnimator?.SetMovementSpeed(agent.velocity.magnitude / agent.speed);
                        if (Vector3.Distance(transform.position, _player.position) <= _config.stopDistanceFromPlayer) break;
                        t += Time.deltaTime;
                        yield return null;
                    }
                    agent.isStopped = true;
                    _npcManager.SimpleAnimator?.ResetMovement();
                }
            }
            else
            {
                yield return new WaitForSeconds(_config.alertIconDuration);
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
            if (_config.switchToEnemyLayerOnCombat)
            {
                int layer = LayerMask.NameToLayer("Enemy");
                if (layer != -1) gameObject.layer = layer;
            }
        }
        
        private void ApplyInitialLayer()
        {
            if (_config == null || _config.initialLayer == LayerMode.Custom) return;
            int layer = LayerMask.NameToLayer(_config.initialLayer.ToString());
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
            
            float duration = _config.rotationDuration;
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
                DefaultNarrativeSignals.Instance?.RaiseCustom(key);
        }
        
        private void SaveState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null) return;
            
            preset.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();
            
            if (!preset.completedInteractiveNarratives.Contains(_config.persistenceId))
            {
                preset.completedInteractiveNarratives.Add(_config.persistenceId);
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
            if (string.IsNullOrEmpty(_config.persistenceId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset?.completedInteractiveNarratives == null) return;
            
            _hasBeenUsed = preset.completedInteractiveNarratives.Contains(_config.persistenceId);
            
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
            }
        }
        
        private string GetConditionalNarrativeId(int index)
        {
            return $"{_config.persistenceId}_CN{index}";
        }

        public void ResetState(bool restoreFromPreset = true)
        {
            _hasBeenUsed = false;
            _hasDetectedPlayer = false;
            _isExecuting = false;
            
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