using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.States;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Ejecutor de cadenas narrativas interactivas.
    /// Procesa secuencialmente las acciones configuradas en NPCInteractiveNarrativeConfig.
    /// </summary>
    public class NPCInteractiveNarrativeExecutor : MonoBehaviour
    {
        private NPCBehaviourManagerV2 _npcManager;
        private NPCInteractiveNarrativeConfig _config;
        private bool _isExecuting;
        private bool _hasBeenUsed;
        private int _currentActionIndex = -1;

        private void Awake()
        {
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
        }

        private void Start()
        {
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] No se encontró NPCBehaviourManagerV2");
                return;
            }

            if (_npcManager.Configuration == null)
                return;

            _config = _npcManager.Configuration.interactiveNarrativeConfig;
            
            if (_config == null)
                return;

            // Cargar estado persistente
            if (_config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                RestoreState();
            }
        }

        /// <summary>
        /// Inicia la ejecución de la cadena narrativa
        /// </summary>
        public bool TryExecuteNarrative()
        {
            if (_isExecuting)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] Ya hay una narrativa ejecutándose");
                return false;
            }

            if (_config == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] Config es null");
                return false;
            }

            if (_config.singleUse && _hasBeenUsed)
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Narrativa ya fue usada (singleUse=true)");
                return false;
            }

            if (_config.narrativeChain == null || _config.narrativeChain.Length == 0)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] Narrative chain vacía");
                return false;
            }

            StartCoroutine(ExecuteNarrativeChain());
            return true;
        }

        private IEnumerator ExecuteNarrativeChain()
        {
            _isExecuting = true;
            _currentActionIndex = 0;

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Iniciando cadena narrativa con {_config.narrativeChain.Length} acciones");

            // Rotar hacia el jugador si está configurado
            if (_config.rotateToPlayerOnInteract)
            {
                yield return RotateToPlayer();
            }

            // Ejecutar cada acción secuencialmente
            for (int i = 0; i < _config.narrativeChain.Length; i++)
            {
                _currentActionIndex = i;
                var entry = _config.narrativeChain[i];

                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ▶️ INICIO Acción {i}/{_config.narrativeChain.Length}: {entry.actionType}");

                entry.onActionStarted?.Invoke();

                yield return ExecuteAction(entry);

                entry.onActionCompleted?.Invoke();

                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ COMPLETADA Acción {i}: {entry.actionType}");
            }
            
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🎉 TODAS LAS ACCIONES COMPLETADAS ({_config.narrativeChain.Length} total)");

            // Marcar como usada
            _hasBeenUsed = true;

            // Guardar estado
            if (_config.persistState)
            {
                SaveState();
            }

            // Ejecutar estado post-narrativa
            yield return HandlePostNarrativeState();

            _isExecuting = false;
            _currentActionIndex = -1;

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Cadena narrativa completada");
        }

        private IEnumerator ExecuteAction(NarrativeChainEntry entry)
        {
            switch (entry.actionType)
            {
                case NarrativeActionType.Dialogue:
                    yield return ExecuteDialogue(entry);
                    break;

                case NarrativeActionType.Move:
                    yield return ExecuteMove(entry);
                    break;

                case NarrativeActionType.PlayAnimation:
                    yield return ExecuteAnimation(entry);
                    break;

                case NarrativeActionType.StartQuest:
                    yield return ExecuteStartQuest(entry);
                    break;

                case NarrativeActionType.StartCombat:
                    yield return ExecuteStartCombat(entry);
                    break;

                case NarrativeActionType.Wait:
                    yield return new WaitForSeconds(entry.waitDuration);
                    break;

                case NarrativeActionType.Custom:
                    entry.customAction?.Invoke();
                    yield return null;
                    break;
            }
        }

        private IEnumerator ExecuteDialogue(NarrativeChainEntry entry)
        {
            if (entry.dialogue == null)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] ⚠️ Dialogue es null, saltando");
                yield break;
            }

            var dm = DialogueManager.Instance;
            if (dm == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ DialogueManager.Instance es null");
                yield break;
            }

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 📖 Iniciando diálogo: {entry.dialogue.name}");
            dm.StartDialogue(entry.dialogue, transform, null);

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ⏳ Esperando a que el diálogo termine...");
            int frameCount = 0;
            while (dm.IsOpen)
            {
                frameCount++;
                if (frameCount % 60 == 0) // Log cada 60 frames (~1 segundo)
                {
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ⏳ Aún esperando diálogo... ({frameCount} frames)");
                }
                yield return null;
            }
            
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Diálogo completado después de {frameCount} frames");
        }

        private IEnumerator ExecuteMove(NarrativeChainEntry entry)
        {
            Vector3 targetPosition = GetTargetPosition(entry);

            if (targetPosition == Vector3.zero)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] No se pudo obtener target position");
                yield break;
            }

            // Si waitForPlayer está activado, usar el modo de movimiento con seguimiento
            if (entry.waitForPlayer)
            {
                yield return ExecuteMoveWithPlayerFollow(entry, targetPosition);
            }
            else
            {
                // Movimiento normal sin esperar al jugador
                yield return ExecuteStandardMove(entry, targetPosition);
            }
        }

        private IEnumerator ExecuteStandardMove(NarrativeChainEntry entry, Vector3 targetPosition)
        {
            // Crear secuencia de movimiento
            var moveSequence = new States.MoveToPoscionSequence(
                _npcManager,
                targetPosition,
                entry.maxMovementDuration,
                entry.turnAroundOnArrival,
                entry.walkDisplayDuration
            );

            _npcManager.StartCinematicSequence(moveSequence);

            // Esperar a que complete
            float timeout = entry.maxMovementDuration + 2f;
            float elapsed = 0f;

            while (!moveSequence.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        private IEnumerator ExecuteMoveWithPlayerFollow(NarrativeChainEntry entry, Vector3 targetPosition)
        {
            var player = PlayerService.Player;
            if (player == null)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] Player no encontrado, movimiento sin seguimiento");
                yield return ExecuteStandardMove(entry, targetPosition);
                yield break;
            }

            var agent = _npcManager.Context?.Agent;
            if (agent == null || !agent.isOnNavMesh)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] Agent no válido");
                yield break;
            }

            // Configurar agent
            agent.SetDestination(targetPosition);
            agent.isStopped = false;

            float elapsed = 0f;
            float timeout = entry.maxMovementDuration + 2f;
            bool isWaitingForPlayer = false;
            float stoppingDist = _npcManager.Configuration?.stoppingDistance ?? 0.5f;

            while (elapsed < timeout)
            {
                // Verificar si llegó al destino
                if (!agent.pathPending && agent.remainingDistance <= stoppingDist + 0.1f)
                {
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Destino alcanzado");
                    break;
                }

                // Calcular distancia al jugador
                float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

                // Si player se alejó demasiado, pausar
                if (!isWaitingForPlayer && distanceToPlayer > entry.maxPlayerDistance)
                {
                    isWaitingForPlayer = true;
                    agent.isStopped = true;
                    
                    // Actualizar animación a idle
                    if (_npcManager.Context?.Animator != null)
                    {
                        _npcManager.Context.Animator.SetMovementSpeed(0f);
                    }

                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Esperando al jugador (distancia: {distanceToPlayer:F2}m > {entry.maxPlayerDistance}m)");
                }
                // Si player se acercó suficiente, reanudar
                else if (isWaitingForPlayer && distanceToPlayer <= entry.resumePlayerDistance)
                {
                    isWaitingForPlayer = false;
                    agent.isStopped = false;
                    agent.SetDestination(targetPosition);

                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Reanudando movimiento (distancia: {distanceToPlayer:F2}m <= {entry.resumePlayerDistance}m)");
                }

                // Actualizar animación mientras se mueve
                if (!isWaitingForPlayer && _npcManager.Context?.Animator != null)
                {
                    float speedFactor = agent.velocity.magnitude / agent.speed;
                    _npcManager.Context.Animator.SetMovementSpeed(speedFactor);
                }

                yield return null;
                elapsed += Time.deltaTime;
            }

            // Detener agent y resetear animación
            if (agent != null)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            if (_npcManager.Context?.Animator != null)
            {
                _npcManager.Context.Animator.ResetMovement();
            }

            // Girar si está configurado
            if (entry.turnAroundOnArrival)
            {
                var newRotation = transform.rotation * Quaternion.Euler(0, 180, 0);
                transform.rotation = newRotation;
            }

            if (elapsed >= timeout)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] Movimiento alcanzó timeout");
            }
        }

        private IEnumerator ExecuteAnimation(NarrativeChainEntry entry)
        {
            var animator = _npcManager.Context?.UnityAnimator;
            if (animator == null)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] No hay Animator");
                yield break;
            }

            // Modo 1: AnimationClip directo
            if (entry.animationClip != null)
            {
                animator.Play(entry.animationClip.name);
                
                // Esperar duración del clip o la especificada
                float duration = entry.animationDuration > 0f 
                    ? entry.animationDuration 
                    : entry.animationClip.length;
                    
                yield return new WaitForSeconds(duration);
            }
            // Modo 2: Trigger string
            else if (!string.IsNullOrEmpty(entry.animationTrigger))
            {
                animator.SetTrigger(entry.animationTrigger);

                // Esperar duración específica o un frame
                if (entry.animationDuration > 0f)
                {
                    yield return new WaitForSeconds(entry.animationDuration);
                }
                else
                {
                    yield return null;
                }
            }
            else
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] No hay animationClip ni animationTrigger configurado");
            }
        }

        private IEnumerator ExecuteStartQuest(NarrativeChainEntry entry)
        {
            if (entry.questToStart == null)
                yield break;

            var qm = QuestManager.Instance;
            if (qm == null)
            {
                Debug.LogError("[NPCInteractiveNarrativeExecutor] QuestManager.Instance es null");
                yield break;
            }

            qm.AddQuest(entry.questToStart);
            qm.StartQuest(entry.questToStart.questId);

            yield return null;
        }

        private IEnumerator ExecuteStartCombat(NarrativeChainEntry entry)
        {
            if (entry.combatTarget == null)
                yield break;

            // Activar comportamiento de combate
            if (_npcManager.Context != null)
            {
                _npcManager.Context.IsInCombat = true;
                // El FSM debería transicionar a CombatState automáticamente
            }

            yield return null;
        }

        private IEnumerator HandlePostNarrativeState()
        {
            switch (_config.postNarrativeState)
            {
                case PostNarrativeState.Idle:
                    _npcManager.ExitCinematic();
                    break;

                case PostNarrativeState.Wander:
                    // Activar ambient/wander si está en la configuración
                    if (_npcManager.Configuration != null)
                    {
                        _npcManager.Configuration.behaviourType |= NPCBehaviourType.Ambient;
                    }
                    _npcManager.ExitCinematic();
                    break;

                case PostNarrativeState.SwitchToAmbient:
                    // Cambiar a ambient config
                    if (_config.postNarrativeAmbientConfig != null && _npcManager.Configuration != null)
                    {
                        _npcManager.Configuration.ambientConfig = _config.postNarrativeAmbientConfig;
                        _npcManager.Configuration.behaviourType |= NPCBehaviourType.Ambient;
                    }
                    _npcManager.ExitCinematic();
                    break;

                case PostNarrativeState.Disable:
                    yield return new WaitForSeconds(0.5f);
                    gameObject.SetActive(false);
                    break;
            }
        }

        private IEnumerator RotateToPlayer()
        {
            var player = PlayerService.Player;
            if (player == null)
                yield break;

            var targetDir = (player.transform.position - transform.position).normalized;
            targetDir.y = 0f;

            if (targetDir.sqrMagnitude < 0.01f)
                yield break;

            var targetRotation = Quaternion.LookRotation(targetDir);
            float elapsed = 0f;

            while (elapsed < _config.rotationDuration)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, elapsed / _config.rotationDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.rotation = targetRotation;
        }

        private Vector3 GetTargetPosition(NarrativeChainEntry entry)
        {
            // Prioridad 1: SpawnAnchor por ID
            if (!string.IsNullOrEmpty(entry.targetAnchorName))
            {
                var spawnAnchor = SpawnAnchor.FindById(entry.targetAnchorName);
                if (spawnAnchor != null)
                {
                    return spawnAnchor.transform.position;
                }
                else
                {
                    Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] SpawnAnchor '{entry.targetAnchorName}' no encontrado");
                }
            }

            // Prioridad 2: Transform directo
            if (entry.targetTransform != null)
            {
                return entry.targetTransform.position;
            }

            return Vector3.zero;
        }

        private void SaveState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId))
                return;

            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null)
                return;

            preset.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();
            
            if (!preset.completedInteractiveNarratives.Contains(_config.persistenceId))
            {
                preset.completedInteractiveNarratives.Add(_config.persistenceId);
            }
        }

        private void RestoreState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId))
                return;

            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null || preset.completedInteractiveNarratives == null)
                return;

            _hasBeenUsed = preset.completedInteractiveNarratives.Contains(_config.persistenceId);
        }
    }
}
