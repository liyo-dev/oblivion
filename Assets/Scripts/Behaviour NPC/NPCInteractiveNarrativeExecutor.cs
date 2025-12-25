using System.Collections;
using UnityEngine;
using Game.NPC.Common;

// Updated: 2025-12-23 - Simplified to conditional-only mode
namespace Game.NPC.Modules
{
    /// <summary>
    /// Ejecutor de cadenas narrativas interactivas.
    /// Procesa secuencialmente las acciones configuradas en NPCInteractiveNarrativeConfig.
    /// Version: 2025-12-23-v3
    /// </summary>
    public class NPCInteractiveNarrativeExecutor : MonoBehaviour
    {
        // Version marker para detectar componentes viejos
        public const int COMPONENT_VERSION = 3;
        
        [SerializeField]
        [HideInInspector]
        private int _componentVersion = COMPONENT_VERSION;
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            Debug.Log("[NPCInteractiveNarrativeExecutor] 🔧 Clase cargada por Unity - Version 3");
        }
        
        public int ComponentVersion => _componentVersion;
        
        private NPCBehaviourManagerV2 _npcManager;
        private NPCInteractiveNarrativeConfig _config;
        private bool _isExecuting;
        private bool _hasBeenUsed;
        private int _currentActionIndex = -1;

        // Sistema de alerta automática
        private bool _hasDetectedPlayer;
        private NPCAlertIconController _alertIconController;
        private Transform _player;

        private void Awake()
        {
            Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] ⚡⚡⚡ AWAKE EJECUTÁNDOSE ⚡⚡⚡ - Frame: {Time.frameCount}, GameObject: {gameObject.name}, Active: {gameObject.activeInHierarchy}");
            _npcManager = GetComponent<NPCBehaviourManagerV2>();
            
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ NPCBehaviourManagerV2 es NULL en Awake()");
            }
            else
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ NPCBehaviourManagerV2 encontrado en Awake()");
            }
        }

        private void OnEnable()
        {
            Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] 🟢🟢🟢 ON ENABLE ⚡⚡⚡ - Frame: {Time.frameCount}, GameObject activo: {gameObject.activeInHierarchy}");
            
            // Registrar en el registro global
            NPCInteractiveNarrativeRegistry.Register(this);
        }

        private void OnDisable()
        {
            // Des-registrar del registro global
            NPCInteractiveNarrativeRegistry.Unregister(this);
        }

        private void Start()
        {
            if (_npcManager == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ NPCBehaviourManagerV2 es NULL");
                return;
            }

            if (_npcManager.Configuration == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ Configuration es NULL");
                return;
            }

            _config = _npcManager.Configuration.interactiveNarrativeConfig;
            
            if (_config == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ interactiveNarrativeConfig es NULL");
                return;
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Config cargado - AutoStart: {_config.autoStartOnPlayerDetection}");
            }
            
            // Re-registrar ahora que tenemos la config cargada (para registrar por ID si tiene persistenceId)
            NPCInteractiveNarrativeRegistry.Register(this);

            // Cargar estado persistente (del último guardado manual del jugador)
            if (_config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                RestoreState();
            }

            // Iniciar detección automática si está configurada y no ha sido usada
            if (_config.autoStartOnPlayerDetection && !_hasBeenUsed)
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔍 Iniciando detección automática (rango={_config.detectionRange}m)");
                StartCoroutine(DetectPlayerRoutine());
            }
        }

        private void Update()
        {
            // ============================================
            // 1. DEBUG VISUAL EN TIEMPO REAL
            // ============================================
            // Debug visual en tiempo real (solo si config está cargado y auto-start activado)
            if (_config != null && _config.autoStartOnPlayerDetection && !_hasBeenUsed && !_hasDetectedPlayer)
            {
                if (_player != null)
                {
                    float dist = Vector3.Distance(transform.position, _player.position);
                    
                    // Log cada 2 segundos
                    if (Time.frameCount % 120 == 0)
                    {
                        Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 📍 Distancia actual al jugador: {dist:F2}m / {_config.detectionRange}m");
                    }
                }
                else if (Time.frameCount % 120 == 0)
                {
                    Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] ⚠️ Player es NULL en Update()");
                }
            }

            // ============================================
            // 2. GESTIÓN DE ICONO PERSISTENTE
            // ============================================
            if (_config == null)
                return;
            
            if (_isExecuting)
            {
                // Ocultar icono mientras se ejecuta la narrativa
                _npcManager.HidePersistentIcon();
                return;
            }
            
            // Obtener la narrativa activa actual
            var activeNarrative = _config.GetActiveNarrative();
            
            if (activeNarrative != null && activeNarrative.showPersistentIcon)
            {
                // Verificar si tiene prefab o sprite configurado
                if (activeNarrative.persistentIconPrefab != null || activeNarrative.persistentIconSprite != null)
                {
                    _npcManager.ShowPersistentIcon();
                }
            }
            else
            {
                // Ocultar icono si no hay narrativa activa o no requiere icono
                _npcManager.HidePersistentIcon();
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

            // Obtener la narrativa activa
            var activeConditionalNarrative = _config.GetActiveNarrative();
            
            if (activeConditionalNarrative == null)
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] No hay narrativas disponibles (condiciones no cumplidas)");
                return false;
            }
            
            var chainToExecute = activeConditionalNarrative.narrativeChain;
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Ejecutando narrativa: '{activeConditionalNarrative.description}'");


            if (chainToExecute == null || chainToExecute.Length == 0)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] Narrative chain vacía");
                return false;
            }

            StartCoroutine(ExecuteNarrativeChain(chainToExecute, activeConditionalNarrative));
            return true;
        }

        private IEnumerator ExecuteNarrativeChain(NarrativeChainEntry[] chain, ConditionalNarrative conditionalNarrative = null)
        {
            _isExecuting = true;
            _currentActionIndex = 0;

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Iniciando cadena narrativa con {chain.Length} acciones");

            // Rotar hacia el jugador si está configurado
            if (_config.rotateToPlayerOnInteract)
            {
                yield return RotateToPlayer();
            }
            
            // Reproducir animación de interacción (saludo/hablar)
            if (_npcManager?.Context?.Animator != null)
            {
                _npcManager.Context.Animator.PlayOneShot("InteractWithPeople_NoWeapon", 0, onComplete: null);
            }

            // Ejecutar cada acción secuencialmente
            for (int i = 0; i < chain.Length; i++)
            {
                _currentActionIndex = i;
                var entry = chain[i];

                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ▶️ INICIO Acción {i}/{chain.Length}: {entry.actionType}");

                entry.onActionStarted?.Invoke();

                yield return ExecuteAction(entry);

                entry.onActionCompleted?.Invoke();

                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ COMPLETADA Acción {i}: {entry.actionType}");
            }
            
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🎉 TODAS LAS ACCIONES COMPLETADAS ({chain.Length} total)");

            // Marcar como usada
            _hasBeenUsed = true;
            
            // Si es narrativa condicional, marcarla y enviar evento al grafo narrativo
            if (conditionalNarrative != null)
            {
                conditionalNarrative.MarkAsExecuted();
                
                // Enviar evento al grafo narrativo si está configurado
                if (conditionalNarrative.sendNarrativeEvent && !string.IsNullOrEmpty(conditionalNarrative.narrativeEventKey))
                {
                    SendNarrativeEvent(conditionalNarrative.narrativeEventKey);
                }
                
                // Ocultar icono persistente si está configurado
                if (conditionalNarrative.showPersistentIcon)
                {
                    _npcManager.HidePersistentIcon();
                }
            }

            // 🎯 Guardar estado en runtimePreset (NO guarda a JSON automáticamente)
            // El guardado a JSON se hará cuando el jugador guarde manualmente en un punto de guardado
            if (_config.persistState)
            {
                SaveState();
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Estado guardado en runtimePreset (persistencia={_config.persistenceId})");
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ℹ️ Se guardará a JSON cuando el jugador guarde manualmente");
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

            // Flag para saber cuándo el diálogo ha terminado
            bool callbackInvoked = false;

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 📖 Iniciando diálogo: {entry.dialogue.name}");
            
            // Usar el callback onFinished para saber cuándo termina el diálogo
            dm.StartDialogue(entry.dialogue, transform, () =>
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🎬 Callback onFinished invocado - Diálogo completado");
                callbackInvoked = true;
            });

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ⏳ Esperando a que el diálogo termine...");
            
            // Esperar a que el diálogo se abra
            float waitForOpenTimeout = 2f;
            float waitForOpenElapsed = 0f;
            while (!dm.IsOpen && waitForOpenElapsed < waitForOpenTimeout)
            {
                waitForOpenElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            
            if (!dm.IsOpen)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] ⚠️ Diálogo no se abrió después de {waitForOpenTimeout}s, continuando...");
                yield break;
            }
            
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Diálogo abierto, esperando a que se cierre...");
            
            // Esperar hasta que el diálogo se cierre (con timeout de seguridad)
            int frameCount = 0;
            int logInterval = 0;
            float timeout = 120f; // 2 minutos máximo
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                frameCount++;
                logInterval++;
                elapsed += Time.unscaledDeltaTime;
                
                // Verificar si el callback se invocó
                if (callbackInvoked)
                {
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Callback invocado - Diálogo completado");
                    break;
                }
                
                // FALLBACK: Si el diálogo se cerró pero el callback no se invocó
                if (!dm.IsOpen && frameCount > 10) // Dar algunos frames para que el callback se ejecute
                {
                    Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] ⚠️ Diálogo cerrado pero callback NO invocado (fallback activado)");
                    break;
                }
                
                // Log cada 60 frames (~1 segundo) con información de estado
                if (logInterval >= 60)
                {
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ⏳ Aún esperando diálogo... " +
                              $"({frameCount} frames, {elapsed:F1}s, IsOpen={dm.IsOpen}, Callback={callbackInvoked})");
                    logInterval = 0;
                }
                
                yield return null;
            }
            
            if (elapsed >= timeout)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ TIMEOUT: Diálogo no terminó después de {timeout}s, forzando continuación");
            }
            
            // Pequeña espera adicional para asegurar que la UI se cierre completamente
            yield return new WaitForSeconds(0.1f);
            
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Diálogo completado después de {frameCount} frames ({elapsed:F1}s) - Continuando con siguiente acción");
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
            if (entry.combatConfig == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ StartCombat requiere combatConfig");
                yield break;
            }

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ⚔️ Iniciando combate con config: {entry.combatConfig.name}");

            // Asignar el combatConfig al NPC
            if (_npcManager.Configuration != null)
            {
                _npcManager.Configuration.combatConfig = entry.combatConfig;
                _npcManager.Configuration.behaviourType |= NPCBehaviourType.Combat; // Asegurar que tiene comportamiento de combate
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ CombatConfig asignado al NPC");
            }

            // Activar comportamiento de combate
            if (_npcManager.Context != null)
            {
                _npcManager.Context.IsInCombat = true;
                
                // Si hay un target específico, asignarlo
                if (entry.combatTarget != null)
                {
                    _npcManager.Context.Player = entry.combatTarget;
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🎯 Target de combate: {entry.combatTarget.name}");
                }
                else
                {
                    // Si no hay target, usar al jugador
                    if (PlayerService.TryGetPlayer(out var player, allowSceneLookup: true))
                    {
                        _npcManager.Context.Player = player.transform;
                        Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🎯 Target de combate: Jugador");
                    }
                }
                
                // El FSM debería transicionar a CombatState automáticamente
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔄 FSM transicionará a CombatState");
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

        // ============================================
        // PERSISTENCIA Y RESETEO
        // ============================================
        // Los métodos SaveState() y RestoreState() están más abajo usando GameBootService

        /// <summary>
        /// Resetea el estado de la narrativa (llamar al iniciar nueva partida)
        /// </summary>
        public void ResetState()
        {
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔄 Iniciando ResetState...");
            
            _hasBeenUsed = false;
            _hasDetectedPlayer = false;
            _isExecuting = false;

            // Resetear todas las narrativas condicionales
            if (_config?.conditionalNarratives != null)
            {
                foreach (var narrative in _config.conditionalNarratives)
                {
                    narrative?.ResetExecutionState();
                }
            }

            // Limpiar estado guardado en PlayerPrefs (sistema antiguo)
            if (_config != null && _config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                string key = $"NarrativeState_{_config.persistenceId}";
                PlayerPrefs.DeleteKey(key);

                // Limpiar estado de narrativas condicionales
                if (_config.conditionalNarratives != null)
                {
                    for (int i = 0; i < _config.conditionalNarratives.Length; i++)
                    {
                        string narrativeKey = $"NarrativeState_{_config.persistenceId}_Conditional_{i}";
                        PlayerPrefs.DeleteKey(narrativeKey);
                    }
                }

                PlayerPrefs.Save();
                
                // CRÍTICO: Limpiar también el GameBootService.Profile (donde realmente se lee el estado)
                var preset = GameBootService.Profile?.GetActivePresetResolved();
                if (preset != null && preset.completedInteractiveNarratives != null)
                {
                    bool wasCompleted = preset.completedInteractiveNarratives.Remove(_config.persistenceId);
                    if (wasCompleted)
                    {
                        Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Removido '{_config.persistenceId}' de completedInteractiveNarratives");
                    }
                }
            }

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔄 Estado reseteado completamente");

            // Reiniciar detección automática si está configurada
            if (_config != null && _config.autoStartOnPlayerDetection && !_hasBeenUsed)
            {
                StopAllCoroutines();
                StartCoroutine(DetectPlayerRoutine());
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔍 Detección automática reiniciada");
            }
        }

        /// <summary>
        /// Obtiene la configuración actual de narrativa interactiva
        /// </summary>
        public NPCInteractiveNarrativeConfig GetConfiguration()
        {
            // Lazy initialization si aún no se ha cargado
            if (_config == null && _npcManager != null && _npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
            
            return _config;
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

        /// <summary>
        /// Escribe el estado completado en runtimePreset (memoria).
        /// NO guarda a JSON automáticamente - eso solo ocurre cuando el jugador guarda manualmente.
        /// </summary>
        private void SaveState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId))
                return;

            // Obtener el runtimePreset activo
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null)
                return;

            preset.completedInteractiveNarratives ??= new System.Collections.Generic.List<string>();
            
            // Añadir al preset en memoria (runtimePreset)
            if (!preset.completedInteractiveNarratives.Contains(_config.persistenceId))
            {
                preset.completedInteractiveNarratives.Add(_config.persistenceId);
                
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Narrativa '{_config.persistenceId}' añadida a runtimePreset");
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ℹ️ Total completadas en memoria: {preset.completedInteractiveNarratives.Count}");
            }
            
            // ⚠️ IMPORTANTE: NO llamamos a SaveCurrentGameState() aquí
            // El guardado a JSON solo debe ocurrir en puntos de guardado manuales
        }

        /// <summary>
        /// Restaura el estado desde runtimePreset (cargado desde JSON del último guardado manual del jugador).
        /// Se ejecuta en Start() al cargar la escena.
        /// </summary>
        private void RestoreState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId))
                return;

            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null || preset.completedInteractiveNarratives == null)
                return;

            _hasBeenUsed = preset.completedInteractiveNarratives.Contains(_config.persistenceId);
            
            if (_hasBeenUsed)
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔄 Narrativa '{_config.persistenceId}' ya completada (del último guardado manual)");
            }
            else
            {
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Narrativa '{_config.persistenceId}' disponible para ejecutar");
            }
        }

        /// <summary>
        /// Corrutina que detecta automáticamente al jugador y comienza la narrativa
        /// </summary>
        private IEnumerator DetectPlayerRoutine()
        {
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔍 DetectPlayerRoutine iniciado");
            
            // Esperar un frame para asegurar que todo esté inicializado
            yield return null;

            // Buscar al jugador con múltiples métodos
            int attempts = 0;
            const int maxAttempts = 20; // Intentar durante 10 segundos
            
            while (_player == null && attempts < maxAttempts)
            {
                attempts++;
                
                // Método 1: PlayerService
                if (PlayerService.Player != null)
                {
                    _player = PlayerService.Player.transform;
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Player encontrado vía PlayerService: {_player.name}");
                    break;
                }
                
                // Método 2: Buscar por nombre (fallback extremo)
                GameObject playerGO = GameObject.Find("vBasicController_MaleCharacterPBR");
                if (playerGO != null)
                {
                    _player = playerGO.transform;
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Player encontrado vía nombre: {_player.name}");
                    break;
                }
                
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] ⚠️ Intento {attempts}/{maxAttempts}: Esperando al jugador...");
                yield return new WaitForSeconds(0.5f);
            }
            
            if (_player == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ No se pudo encontrar al jugador después de {maxAttempts} intentos. Abortando detección.");
                yield break;
            }

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ Player encontrado: {_player.name}. Iniciando bucle de detección...");

            // Bucle de detección
            int checkCount = 0;
            while (!_hasDetectedPlayer && !_hasBeenUsed)
            {
                // Verificar que el player sigue existiendo
                if (_player == null)
                {
                    Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] ❌ Player se volvió NULL. Abortando.");
                    yield break;
                }
                
                checkCount++;
                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

                if (checkCount % 10 == 0) // Log cada 10 checks (cada 2 segundos)
                {
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 🔍 Check #{checkCount}: Distancia al jugador = {distanceToPlayer:F2}m (rango={_config.detectionRange}m)");
                }

                if (distanceToPlayer <= _config.detectionRange)
                {
                    // Verificar si el jugador acaba de soltar un objeto para evitar interacciones inmediatas
                    var carrySystem = _player.GetComponent<PlayerCarrySystem>();
                    if (carrySystem != null && carrySystem.JustDroppedObject)
                    {
                        if (checkCount % 10 == 0) // Log periódico
                        {
                            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ⏳ Jugador acaba de soltar objeto, esperando cooldown...");
                        }
                        yield return new WaitForSeconds(0.2f);
                        continue; // Volver a checkear en el siguiente ciclo
                    }
                    
                    _hasDetectedPlayer = true;
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✅ ¡Jugador detectado a {distanceToPlayer:F2}m!");
                    
                    // Iniciar secuencia de alerta
                    yield return StartAlertSequence();
                    
                    // Iniciar narrativa
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] 📖 Intentando ejecutar narrativa...");
                    bool success = TryExecuteNarrative();
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Resultado de TryExecuteNarrative: {success}");
                    yield break;
                }

                yield return new WaitForSeconds(0.2f); // Checkear cada 0.2 segundos
            }
            
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] DetectPlayerRoutine finalizado - detected={_hasDetectedPlayer}, used={_hasBeenUsed}");
        }

        /// <summary>
        /// Muestra el icono de alerta y opcionalmente camina hacia el jugador
        /// </summary>
        private IEnumerator StartAlertSequence()
        {
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Iniciando secuencia de alerta");

            // Obtener o crear AlertIconController
            if (_alertIconController == null)
            {
                _alertIconController = GetComponent<Game.NPC.Common.NPCAlertIconController>();
                if (_alertIconController == null)
                {
                    _alertIconController = gameObject.AddComponent<Game.NPC.Common.NPCAlertIconController>();
                }
            }

            // Mostrar icono de alerta desde el config de narrativa interactiva
            if (_config.alertIconPrefab != null)
            {
                _alertIconController.ShowAlertIcon(
                    _config.alertIconPrefab, 
                    _config.alertIconDuration
                );
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Icono de alerta mostrado (prefab de narrativa)");
            }
            // Fallback: Si no hay icono en narrativa pero sí en combatConfig, usarlo
            else if (_npcManager.Configuration != null && 
                _npcManager.Configuration.combatConfig != null && 
                _npcManager.Configuration.combatConfig.alertIconPrefab != null)
            {
                _alertIconController.ShowAlertIcon(
                    _npcManager.Configuration.combatConfig.alertIconPrefab, 
                    _config.alertIconDuration
                );
                Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Icono de alerta mostrado (prefab de combate)");
            }
            else
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] No hay alertIconPrefab configurado");
            }

            // Caminar hacia el jugador si está configurado
            if (_config.walkTowardsPlayerOnAlert && _player != null)
            {
                yield return WalkTowardsPlayer();
            }
            else
            {
                // Solo esperar la duración del icono
                yield return new WaitForSeconds(_config.alertIconDuration);
            }

            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Secuencia de alerta completada");
        }


        /// <summary>
        /// Hace que el NPC camine hacia el jugador hasta la distancia de parada
        /// </summary>
        private IEnumerator WalkTowardsPlayer()
        {
            var agent = _npcManager.Context?.Agent;
            if (agent == null || !agent.isOnNavMesh || _player == null)
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] No se puede caminar hacia el jugador");
                yield return new WaitForSeconds(_config.alertIconDuration);
                yield break;
            }

            float startTime = Time.time;
            float maxDuration = _config.alertIconDuration;
            
            agent.isStopped = false;
            agent.stoppingDistance = _config.stopDistanceFromPlayer;

            while (Time.time - startTime < maxDuration)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

                // Si llegamos a la distancia de parada, terminar
                if (distanceToPlayer <= _config.stopDistanceFromPlayer)
                {
                    Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] Llegó a la distancia de parada del jugador");
                    break;
                }

                // Actualizar destino hacia el jugador
                agent.SetDestination(_player.position);

                // Actualizar animación de movimiento
                if (_npcManager.Context?.Animator != null)
                {
                    float speedFactor = agent.velocity.magnitude / agent.speed;
                    _npcManager.Context.Animator.SetMovementSpeed(speedFactor);
                }

                yield return null;
            }

            // Detener agente
            agent.isStopped = true;
            agent.ResetPath();

            // Resetear animación
            if (_npcManager.Context?.Animator != null)
            {
                _npcManager.Context.Animator.ResetMovement();
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Dibujar rango de detección si auto-inicio está activado
            if (_config != null && _config.autoStartOnPlayerDetection)
            {
                // Rango de detección (amarillo)
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, _config.detectionRange);
                
                // Distancia de parada (verde)
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, _config.stopDistanceFromPlayer);
                
                // Línea hacia el jugador si existe
                if (_player != null)
                {
                    float distance = Vector3.Distance(transform.position, _player.position);
                    
                    // Color según si está en rango o no
                    Gizmos.color = distance <= _config.detectionRange ? Color.green : Color.red;
                    Gizmos.DrawLine(transform.position, _player.position);
                    
#if UNITY_EDITOR
                    // Mostrar distancia en la escena
                    UnityEditor.Handles.Label(
                        transform.position + Vector3.up * 2.5f,
                        $"Player: {distance:F1}m / {_config.detectionRange}m\n" +
                        $"AutoStart: {_config.autoStartOnPlayerDetection}\n" +
                        $"Detected: {_hasDetectedPlayer}\n" +
                        $"Used: {_hasBeenUsed}",
                        new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = distance <= _config.detectionRange ? Color.green : Color.yellow },
                            fontSize = 11,
                            alignment = UnityEngine.TextAnchor.MiddleCenter
                        }
                    );
#endif
                }
                else
                {
#if UNITY_EDITOR
                    // Advertencia si no hay player
                    UnityEditor.Handles.Label(
                        transform.position + Vector3.up * 2.5f,
                        "⚠️ Player NULL",
                        new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = Color.red },
                            fontSize = 12,
                            fontStyle = FontStyle.Bold,
                            alignment = UnityEngine.TextAnchor.MiddleCenter
                        }
                    );
#endif
                }
            }
        }
        
        /// <summary>
        /// Envía un evento al grafo narrativo usando DefaultNarrativeSignals
        /// </summary>
        private void SendNarrativeEvent(string eventKey)
        {
            if (string.IsNullOrEmpty(eventKey))
            {
                Debug.LogWarning($"[NPCInteractiveNarrativeExecutor:{name}] eventKey vacío, no se puede enviar evento");
                return;
            }
            
            var signals = DefaultNarrativeSignals.Instance;
            if (signals == null)
            {
                Debug.LogError($"[NPCInteractiveNarrativeExecutor:{name}] DefaultNarrativeSignals.Instance no disponible");
                return;
            }
            
            signals.RaiseCustom(eventKey);
            Debug.Log($"[NPCInteractiveNarrativeExecutor:{name}] ✉️ Evento enviado al grafo narrativo: '{eventKey}'");
        }
    }

    /// <summary>
    /// Componente simple para hacer que un sprite siempre mire a la cámara
    /// OBSOLETO: Usar NPCAlertIconController en su lugar
    /// </summary>
    [System.Obsolete("Usar NPCAlertIconController en su lugar")]
    public class BillboardSprite : MonoBehaviour
    {
        private Camera _mainCamera;

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (_mainCamera != null)
            {
                transform.rotation = _mainCamera.transform.rotation;
            }
        }
    }
}
