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
        public const int COMPONENT_VERSION = 4; // Versión actualizada

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
        #endregion

        private void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            _interactable = GetComponent<Interactable>();
            
            if (_npcManager == null) Debug.LogError($"[NarrativeExecutor:{name}] ❌ Falta NPCBehaviourManagerV2");
        }

        private void OnEnable() => NPCInteractiveNarrativeRegistry.Register(this);
        private void OnDisable() => NPCInteractiveNarrativeRegistry.Unregister(this);
        
        /// <summary>
        /// Obtiene la configuración narrativa asociada a este ejecutor
        /// </summary>
        public NPCInteractiveNarrativeConfig GetConfiguration()
        {
            return _config;
        }

        private void Start()
        {
            if (_npcManager == null || _npcManager.Configuration == null) return;

            _config = _npcManager.Configuration.interactiveNarrativeConfig;
            if (_config == null) return;

            // Restaurar estado guardado
            if (_config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                RestoreState();
            }

            // Aplicar capa inicial (Interactable/Enemy)
            ApplyInitialLayer();

            // Iniciar detección automática
            if (_config.autoStartOnPlayerDetection && !_hasBeenUsed)
            {
                StartCoroutine(DetectPlayerRoutine());
            }
        }

        private void Update()
        {
            if (_config == null) return;

            // 1. Gestión de Interactable (Solo activo si hay narrativa disponible)
            if (_interactable != null && !_isExecuting)
            {
                bool hasNarrative = _config.GetActiveNarrative() != null;
                if (_interactable.enabled != hasNarrative)
                {
                    _interactable.enabled = hasNarrative;
                }
            }

            // 2. Gestión de Icono Persistente (Exclamación sobre la cabeza)
            if (!_isExecuting)
            {
                var activeNarrative = _config.GetActiveNarrative();
                if (activeNarrative != null && activeNarrative.showPersistentIcon)
                {
                    // TODO: Implementar ShowPersistentIcon en NPCBehaviourManagerV2
                    // if (activeNarrative.persistentIconPrefab || activeNarrative.persistentIconSprite)
                    //     _npcManager.ShowPersistentIcon();
                }
                else
                {
                    // TODO: Implementar HidePersistentIcon en NPCBehaviourManagerV2
                    // _npcManager.HidePersistentIcon();
                }
            }
            else
            {
                // TODO: Implementar HidePersistentIcon en NPCBehaviourManagerV2
                // _npcManager.HidePersistentIcon();
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
            // TODO: Implementar HidePersistentIcon en NPCBehaviourManagerV2
            // _npcManager.HidePersistentIcon();

            // 1. Preparación (Rotar y Saludar)
            if (_config.rotateToPlayerOnInteract) yield return RotateToPlayer();
            if (_npcManager.Animator) _npcManager.Animator.SetTrigger("Interact"); // Trigger genérico

            // 2. Ejecución Secuencial
            for (int i = 0; i < chain.Length; i++)
            {
                var entry = chain[i];
                _currentActionIndex = i;

                // Evento Pre-Acción
                if (entry.sendNarrativeEvent && entry.sendEventOnStart)
                    SendNarrativeEvent(entry.narrativeEventKey);

                // Ejecutar Acción
                yield return ExecuteAction(entry);

                // Evento Post-Acción
                if (entry.sendNarrativeEvent && !entry.sendEventOnStart)
                    SendNarrativeEvent(entry.narrativeEventKey);
            }

            // 3. Finalización
            _hasBeenUsed = true;
            
            if (narrativeData != null)
            {
                narrativeData.MarkAsExecuted();
                if (narrativeData.sendNarrativeEvent) SendNarrativeEvent(narrativeData.narrativeEventKey);
            }

            if (_config.persistState) SaveState();

            // 4. Estado Post-Narrativa
            yield return HandlePostNarrativeState();

            _isExecuting = false;
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
            }
        }

        // =================================================================================
        // 🎭 ACTIONS IMPLEMENTATION
        // =================================================================================

        private IEnumerator ExecuteDialogue(NarrativeChainEntry entry)
        {
            if (entry.dialogue == null || DialogueManager.Instance == null) yield break;

            bool completed = false;
            
            // Iniciamos el diálogo y confiamos en el callback
            DialogueManager.Instance.StartDialogue(entry.dialogue, transform, () => completed = true);

            // Esperamos a que el sistema de diálogo reporte finalización
            // (Eliminamos logs excesivos y lógica compleja de polling)
            while (!completed)
            {
                yield return null;
            }
            
            // Breve pausa para limpieza de UI
            yield return new WaitForSeconds(0.1f);
        }

        private IEnumerator ExecuteMove(NarrativeChainEntry entry)
        {
            Vector3 targetPos = GetTargetPosition(entry);
            if (targetPos == Vector3.zero) yield break;

            if (entry.waitForPlayer)
            {
                // Lógica compleja de seguimiento (Player Follow)
                yield return ExecuteMoveWithPlayerFollow(entry, targetPos);
            }
            else
            {
                // Movimiento Estándar usando el sistema cinemático del Manager
                var moveSeq = new MoveToPositionSequence(
                    _npcManager, 
                    targetPos, 
                    entry.maxMovementDuration, 
                    entry.turnAroundOnArrival, 
                    999f // Walk duration infinita (sin teleport)
                );
                
                _npcManager.StartCinematicSequence(moveSeq);
                
                while (!moveSeq.IsCompleted) yield return null;
            }
        }

        private IEnumerator ExecuteMoveWithPlayerFollow(NarrativeChainEntry entry, Vector3 targetPos)
        {
            // Nota: Esta lógica se mantiene similar a la original porque es específica de tu juego
            // pero limpiamos las referencias.
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

                // Pausar si el jugador se aleja
                if (!waiting && distToPlayer > entry.maxPlayerDistance)
                {
                    waiting = true;
                    agent.isStopped = true;
                    _npcManager.SimpleAnimator?.SetMovementSpeed(0);
                }
                // Reanudar si se acerca
                else if (waiting && distToPlayer <= entry.resumePlayerDistance)
                {
                    waiting = false;
                    agent.isStopped = false;
                    agent.SetDestination(targetPos);
                }

                // Animar
                if (!waiting)
                {
                    _npcManager.SimpleAnimator?.SetMovementSpeed(agent.velocity.magnitude / agent.speed);
                }

                timer += Time.deltaTime;
                yield return null;
            }

            agent.isStopped = true;
            _npcManager.SimpleAnimator?.ResetMovement();
            
            // ✅ FIX: Respetar SpawnAnchor en lugar de siempre girar 180°
            if (entry.turnAroundOnArrival)
            {
                // Buscar SpawnAnchor cercano
                SpawnAnchor nearbyAnchor = FindNearbySpawnAnchor(targetPos);
                
                if (nearbyAnchor != null)
                {
                    // CONVENCIÓN: El SpawnAnchor se coloca con el eje Z (forward) apuntando
                    // hacia donde quieres que mire el personaje POR DEFECTO
                    Quaternion targetRotation;
                    if (nearbyAnchor.faceDoor)
                    {
                        // faceDoor = true → Invertir la dirección (mirar al lado contrario)
                        // Usamos -forward para dar la vuelta 180°
                        targetRotation = Quaternion.LookRotation(-nearbyAnchor.transform.forward, Vector3.up);
                    }
                    else
                    {
                        // faceDoor = false (por defecto) → Usar la dirección del anchor tal cual
                        // El NPC mira en la dirección del eje Z del anchor
                        targetRotation = Quaternion.LookRotation(nearbyAnchor.transform.forward, Vector3.up);
                    }
                    transform.rotation = targetRotation;
                    Debug.Log($"[NPCInteractiveNarrativeExecutor] NPC '{gameObject.name}' orientado según SpawnAnchor '{nearbyAnchor.anchorId}' (faceDoor={nearbyAnchor.faceDoor})");
                }
                else
                {
                    // Fallback: girar 180° si no hay SpawnAnchor
                    transform.rotation *= Quaternion.Euler(0, 180, 0);
                    Debug.Log($"[NPCInteractiveNarrativeExecutor] NPC '{gameObject.name}' girado 180° (sin SpawnAnchor cercano)");
                }
            }
        }
        
        /// <summary>
        /// Busca un SpawnAnchor cerca de una posición
        /// </summary>
        private SpawnAnchor FindNearbySpawnAnchor(Vector3 position)
        {
            const float searchRadius = 2f;
            var allAnchors = FindObjectsByType<SpawnAnchor>(FindObjectsSortMode.None);
            
            SpawnAnchor closest = null;
            float closestDistance = searchRadius;
            
            foreach (var anchor in allAnchors)
            {
                float distance = Vector3.Distance(anchor.transform.position, position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = anchor;
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

            // ✅ FIX: Solo preparar la configuración de combate
            // NO iniciar el combate directamente para evitar:
            // 1. Música duplicada (aquí + AlertState)
            // 2. Salto extraño (Idle → Combat → Idle → Alert → Combat)
            // 
            // El flujo natural será: Idle → Alert (con diálogo y música) → Combat
            
            Debug.Log($"[NarrativeExecutor] ⚙️ Preparando configuración de combate para {gameObject.name}");
            
            // 1. Preparar Capas y Config
            SwitchToEnemyLayer();
            _npcManager.Configuration.combatConfig = entry.combatConfig;
            
            // 2. Asegurar que los componentes de combate existan (sin iniciar combate)
            // Esto prepara al NPC para combate pero NO lo inicia
            if (!GetComponent<Damageable>())
            {
                var dmg = gameObject.AddComponent<Damageable>();
                dmg.SetMaxAndCurrent(entry.combatConfig.health, entry.combatConfig.health);
                dmg.SetDestroyOnDeath(false);
                Debug.Log($"[NarrativeExecutor] 🛡️ Damageable añadido preventivamente");
            }
            
            if (!GetComponent<NPCCombatLifecycleHandler>())
            {
                gameObject.AddComponent<NPCCombatLifecycleHandler>();
                Debug.Log($"[NarrativeExecutor] ☠️ NPCCombatLifecycleHandler añadido preventivamente para {gameObject.name}");
            }
            else
            {
                Debug.Log($"[NarrativeExecutor] ℹ️ NPCCombatLifecycleHandler ya existe en {gameObject.name}");
            }
            
            // 3. El NPC detectará al jugador naturalmente y entrará en AlertState
            // que manejará el diálogo, la música, y la transición a combate
            Debug.Log($"[NarrativeExecutor] ✅ NPC preparado para combate - esperando detección natural del jugador");
        }

        private IEnumerator HandlePostNarrativeState()
        {
            // Decidir qué hacer al terminar la charla
            switch (_config.postNarrativeState)
            {
                case PostNarrativeState.Idle:
                    // TODO: Implementar ExitCinematic en NPCBehaviourManagerV2
                    // _npcManager.ExitCinematic();
                    _npcManager.ForceIdle();
                    break;

                case PostNarrativeState.Wander:
                case PostNarrativeState.SwitchToAmbient:
                    if (_config.postNarrativeAmbientConfig != null)
                        _npcManager.Configuration.ambientConfig = _config.postNarrativeAmbientConfig;
                    
                    // Activar comportamiento wander
                    // TODO: Implementar ExitCinematic en NPCBehaviourManagerV2
                    // _npcManager.ExitCinematic();
                    // Aquí podrías forzar _npcManager.Brain.ChangeState(new WanderState());
                    break;

                case PostNarrativeState.Disable:
                    yield return new WaitForSeconds(0.5f);
                    gameObject.SetActive(false);
                    break;
            }
        }

        // =================================================================================
        // 🔍 UTILS & PERSISTENCE
        // =================================================================================

        private IEnumerator DetectPlayerRoutine()
        {
            yield return new WaitForSeconds(1f); // Startup delay

            while (!_hasDetectedPlayer && !_hasBeenUsed)
            {
                if (PlayerService.TryGetPlayer(out var p, false)) _player = p.transform;

                if (_player != null)
                {
                    float dist = Vector3.Distance(transform.position, _player.position);
                    if (dist <= _config.detectionRange)
                    {
                        _hasDetectedPlayer = true;
                        yield return StartAlertSequence(); // Exclamación !
                        TryExecuteNarrative();
                    }
                }
                yield return new WaitForSeconds(0.2f);
            }
        }

        private IEnumerator StartAlertSequence()
        {
            // Mostrar Icono
            GameObject iconPrefab = _config.alertIconPrefab;
            if (!iconPrefab && _npcManager.Configuration.combatConfig) 
                iconPrefab = _npcManager.Configuration.combatConfig.alertIconPrefab;

            if (iconPrefab)
            {
                if (!_alertIconController) 
                    _alertIconController = gameObject.AddComponent<NPCAlertIconController>();
                
                _alertIconController.ShowAlertIcon(iconPrefab, _config.alertIconDuration);
            }

            // Caminar hacia jugador
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
            if (entry.targetTransform) return entry.targetTransform.position;
            return Vector3.zero;
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
            if (_config == null) return;
            string layerName = _config.initialLayer.ToString();
            if (_config.initialLayer == LayerMode.Custom) return;
            
            int layer = LayerMask.NameToLayer(layerName);
            if (layer != -1) gameObject.layer = layer;
        }

        private IEnumerator RotateToPlayer()
        {
            if (PlayerService.TryGetPlayer(out var p, true))
            {
                Vector3 dir = (p.transform.position - transform.position).normalized;
                dir.y = 0;
                Quaternion target = Quaternion.LookRotation(dir);
                float t = 0;
                while (t < 0.5f) // Rápido
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, target, t / 0.5f);
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }

        private void SendNarrativeEvent(string key)
        {
            if (!string.IsNullOrEmpty(key)) 
                DefaultNarrativeSignals.Instance?.RaiseCustom(key);
        }

        // --- PERSISTENCIA BÁSICA ---
        private void SaveState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset != null && !preset.completedInteractiveNarratives.Contains(_config.persistenceId))
            {
                preset.completedInteractiveNarratives.Add(_config.persistenceId);
            }
        }

        private void RestoreState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset != null) _hasBeenUsed = preset.completedInteractiveNarratives.Contains(_config.persistenceId);
        }

        public void ResetState()
        {
            _hasBeenUsed = false;
            _hasDetectedPlayer = false;
            _isExecuting = false;
            // Limpieza de datos en PlayerPrefs/Profile...
        }
    }
}