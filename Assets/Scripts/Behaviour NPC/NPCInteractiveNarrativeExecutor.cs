using System.Collections;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.States;
using EasyTransition;

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
        
        // Cache para optimización
        private ConditionalNarrative _cachedActiveNarrative;
        private int _lastNarrativeCheckFrame = -1;
        private const int NARRATIVE_CHECK_INTERVAL = 10; // Revisar narrativa cada N frames
        
        // Cache de WaitForSeconds para evitar GC
        private static readonly WaitForSeconds _waitHalfSecond = new WaitForSeconds(0.5f);
        private static readonly WaitForSeconds _waitPointTwo = new WaitForSeconds(0.2f);
        private static readonly WaitForSeconds _waitPointOne = new WaitForSeconds(0.1f);
        private static readonly WaitForSeconds _waitOneSecond = new WaitForSeconds(1f);
        #endregion
        
        #region 📢 Public API
        /// <summary>
        /// Indica si el NPC está ejecutando una narrativa actualmente.
        /// Mientras ejecuta, no debe permitirse interacción.
        /// </summary>
        public bool IsExecuting => _isExecuting;
        #endregion
        
        /// <summary>
        /// Obtiene la narrativa activa con cache para evitar evaluaciones frecuentes
        /// </summary>
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
        
        /// <summary>
        /// Invalida el cache de narrativa activa (llamar después de ejecutar una narrativa)
        /// </summary>
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
            
            // ✅ CRÍTICO: Inicializar _config en Awake para que esté disponible en OnEnable
            // Esto es necesario porque OnEnable se ejecuta antes que Start, y el Registry
            // necesita acceder a la configuración para registrar correctamente el persistenceId
            if (_npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
        }

        private void OnEnable() => NPCInteractiveNarrativeRegistry.Register(this);
        private void OnDisable() => NPCInteractiveNarrativeRegistry.Unregister(this);
        
        /// <summary>
        /// Obtiene la configuración narrativa asociada a este ejecutor
        /// </summary>
        public NPCInteractiveNarrativeConfig GetConfiguration()
        {
            // Intentar obtener la config si aún no está inicializada
            if (_config == null && _npcManager != null && _npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
            return _config;
        }

        private void Start()
        {
            // _config ya se inicializó en Awake, pero verificamos por si acaso
            if (_config == null && _npcManager != null && _npcManager.Configuration != null)
            {
                _config = _npcManager.Configuration.interactiveNarrativeConfig;
            }
            
            if (_npcManager == null || _npcManager.Configuration == null) return;
            if (_config == null) return;

            // Inicializar el controlador de iconos (si existe o lo creamos)
            InitializeAlertIconController();

            // Restaurar estado guardado
            if (_config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                RestoreState();
            }

            // Aplicar capa inicial (Interactable/Enemy)
            ApplyInitialLayer();

            // Iniciar detección automática SOLO si la narrativa activa lo requiere
            // Ahora se verifica por narrativa, no globalmente
            StartCoroutine(DetectPlayerRoutine());
            
            // Nota: El icono persistente se gestiona en Update() automáticamente
            // cuando hay una narrativa disponible con showPersistentIcon = true
        }
        
        /// <summary>
        /// Inicializa el controlador de iconos de alerta
        /// </summary>
        private void InitializeAlertIconController()
        {
            _alertIconController = GetComponent<NPCAlertIconController>();
            if (_alertIconController == null)
            {
                // Solo crear si hay narrativas que podrían usar iconos
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
                
                if (needsIconController || _config.alertIconPrefab != null)
                {
                    _alertIconController = gameObject.AddComponent<NPCAlertIconController>();
                }
            }
        }

        private void Update()
        {
            if (_config == null) return;

            // 1. Gestión de Interactable
            if (_interactable != null)
            {
                // Deshabilitar interacción mientras se ejecuta una narrativa
                if (_isExecuting)
                {
                    if (_interactable.enabled)
                    {
                        _interactable.enabled = false;
                    }
                }
                else
                {
                    // Solo habilitar si hay narrativa disponible (usando cache)
                    bool hasNarrative = GetCachedActiveNarrative() != null;
                    if (_interactable.enabled != hasNarrative)
                    {
                        _interactable.enabled = hasNarrative;
                    }
                }
            }

            // 2. Gestión de Icono Persistente (Exclamación sobre la cabeza)
            if (!_isExecuting)
            {
                var activeNarrative = GetCachedActiveNarrative();
                if (activeNarrative != null)
                {
                    if (activeNarrative.showPersistentIcon)
                    {
                        // Mostrar icono persistente si hay un prefab configurado
                        ShowPersistentIconIfNeeded(activeNarrative);
                    }
                    else
                    {
                        // Hay narrativa activa pero no tiene icono persistente configurado
                        HidePersistentIconIfActive();
                    }
                }
                else
                {
                    // No hay narrativa activa
                    HidePersistentIconIfActive();
                }
            }
            else
            {
                // Mientras se ejecuta una narrativa, ocultar el icono
                HidePersistentIconIfActive();
            }
        }
        
        /// <summary>
        /// Muestra el icono persistente si está configurado
        /// </summary>
        private void ShowPersistentIconIfNeeded(ConditionalNarrative narrative)
        {
            if (_alertIconController == null)
            {
                _alertIconController = GetComponent<NPCAlertIconController>();
                if (_alertIconController == null)
                {
                    _alertIconController = gameObject.AddComponent<NPCAlertIconController>();
                }
            }
            
            // Usar el prefab configurado en la narrativa, o el del config general
            GameObject iconPrefab = narrative.persistentIconPrefab;
            if (iconPrefab == null && _config != null)
            {
                iconPrefab = _config.alertIconPrefab;
            }
            
            if (iconPrefab != null && !_alertIconController.HasPersistentIcon)
            {
                _alertIconController.ShowPersistentIcon(iconPrefab);
            }
        }
        
        /// <summary>
        /// Oculta el icono persistente si está activo
        /// </summary>
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
            Debug.Log($"[NarrativeExecutor] 🔍 {name} - TryExecuteNarrative llamado");
            
            if (_isExecuting)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {name} - Ya está ejecutando una narrativa (_isExecuting=true)");
                return false;
            }
            
            if (_config == null)
            {
                Debug.LogError($"[NarrativeExecutor] ❌ {name} - _config es NULL");
                return false;
            }

            var activeNarrative = _config.GetActiveNarrative();
            if (activeNarrative == null)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {name} - No hay narrativa activa");
                return false;
            }

            var chain = activeNarrative.narrativeChain;
            if (chain == null || chain.Length == 0)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {gameObject.name} - La narrativa '{activeNarrative.description}' no tiene cadena o está vacía");
                return false;
            }

            Debug.Log($"[NarrativeExecutor] ✅ {gameObject.name} - Iniciando ejecución de narrativa '{activeNarrative.description}' con {chain.Length} acciones");
            StartCoroutine(ExecuteNarrativeChain(chain, activeNarrative));
            return true;
        }

        private IEnumerator ExecuteNarrativeChain(NarrativeChainEntry[] chain, ConditionalNarrative narrativeData)
        {
            _isExecuting = true;
            _currentActionIndex = 0;
            
            // Ocultar icono persistente al iniciar la narrativa
            HidePersistentIconIfActive();

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
            
            // ✅ CRÍTICO: Invalidar caché de narrativa ANTES de ejecutar el PostNarrativeState
            // Esto asegura que GetActiveNarrative() retornará null o la siguiente narrativa
            // disponible en lugar de la narrativa recién completada
            InvalidateNarrativeCache();

            // 4. Estado Post-Narrativa (usa el de la narrativa específica)
            yield return HandlePostNarrativeState(narrativeData);

            _isExecuting = false;
            
            // ✅ Resetear flag de detección para permitir futuras narrativas con autoStartOnDetection
            _hasDetectedPlayer = false;
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
            while (!completed)
            {
                yield return null;
            }
            
            // Breve pausa para limpieza de UI
            yield return _waitPointOne;
        }

        private IEnumerator ExecuteMove(NarrativeChainEntry entry)
        {
            Debug.Log($"[NarrativeExecutor] 🚶 ExecuteMove iniciado para {name}");
            
            Vector3 targetPos;
            
            // ✅ Movimiento a punto aleatorio
            if (entry.moveToRandomPoint)
            {
                targetPos = GetRandomMovePosition(entry.randomMoveMinRadius, entry.randomMoveMaxRadius);
                Debug.Log($"[NarrativeExecutor] 🎲 Moviendo {name} a punto aleatorio: {targetPos}");
                
                // Si es líder de equipo y moveTeamMembers está activo, mover también a los compañeros
                var combatTeam = GetComponent<NPCCombatTeam>();
                if (combatTeam != null && entry.moveTeamMembers)
                {
                    Debug.Log($"[NarrativeExecutor] 👥 Moviendo también a {combatTeam.TeamSize - 1} miembros del equipo");
                    MoveTeamMembersToRandomPoints(combatTeam, entry);
                }
                
                // Para movimiento aleatorio, usar NavMeshAgent directamente
                yield return MoveToPositionDirect(targetPos, entry.maxMovementDuration);
            }
            else
            {
                targetPos = GetTargetPosition(entry);
                if (targetPos == Vector3.zero)
                {
                    Debug.LogWarning($"[NarrativeExecutor] ⚠️ targetPos es Vector3.zero - abortando movimiento");
                    yield break;
                }

                // ...existing code...
            }
            
            // ✅ Desaparecer al llegar con transición
            if (entry.disappearOnArrival)
            {
                Debug.Log($"[NarrativeExecutor] 👻 Iniciando desaparición para {name}");
                yield return DisappearWithTransition(entry.disappearTransition);
            }
            else
            {
                Debug.Log($"[NarrativeExecutor] ✅ ExecuteMove completado para {name} (sin desaparecer)");
            }
        }
        
        /// <summary>
        /// Mueve al NPC directamente usando NavMeshAgent (sin secuencia cinemática)
        /// </summary>
        private IEnumerator MoveToPositionDirect(Vector3 targetPos, float maxDuration)
        {
            var agent = _npcManager.Agent;
            
            Debug.Log($"[NarrativeExecutor] 🔍 MoveToPositionDirect - Agent: {(agent != null ? "OK" : "NULL")}, " +
                     $"Enabled: {(agent != null ? agent.enabled : false)}, " +
                     $"OnNavMesh: {(agent != null && agent.enabled ? agent.isOnNavMesh : false)}");
            
            if (agent == null)
            {
                Debug.LogError($"[NarrativeExecutor] ❌ NavMeshAgent es NULL para {name}");
                yield break;
            }
            
            // ✅ Verificar si hay NPCCombatBrain activo que pueda interferir
            var combatBrain = GetComponent<NPCCombatBrain>();
            bool hadActiveBrain = false;
            if (combatBrain != null && combatBrain.enabled)
            {
                combatBrain.enabled = false;
                hadActiveBrain = true;
                Debug.Log($"[NarrativeExecutor] 🔧 Desactivando NPCCombatBrain temporalmente para {name}");
            }
            
            // ✅ Reactivar el agent si está deshabilitado (puede pasar después de combate)
            if (!agent.enabled)
            {
                Debug.Log($"[NarrativeExecutor] 🔧 Reactivando NavMeshAgent para {name}");
                agent.enabled = true;
                yield return new WaitForSeconds(0.1f); // Pequeña espera para que se inicialice
            }
            
            if (!agent.isOnNavMesh)
            {
                Debug.LogError($"[NarrativeExecutor] ❌ {name} no está en NavMesh - posición: {transform.position}");
                
                // Reactivar brain si fue desactivado antes de abortar
                if (hadActiveBrain && combatBrain != null)
                {
                    combatBrain.enabled = true;
                }
                yield break;
            }
            
            // ✅ CRÍTICO: Asegurar que el agent mueva y rote el transform (después del combate)
            agent.updatePosition = true;
            agent.updateRotation = true;
            
            // ✅ Verificar que hay un camino válido antes de moverse
            UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
            if (!agent.CalculatePath(targetPos, path))
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {name} no puede calcular camino a {targetPos}");
                
                // Reactivar brain si fue desactivado antes de abortar
                if (hadActiveBrain && combatBrain != null)
                {
                    combatBrain.enabled = true;
                }
                yield break;
            }
            
            if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {name} camino incompleto a {targetPos} - status: {path.status}");
                
                // Si el camino es parcial, intentar ir al punto más cercano alcanzable
                if (path.status == UnityEngine.AI.NavMeshPathStatus.PathPartial && path.corners.Length > 1)
                {
                    Vector3 lastReachablePoint = path.corners[path.corners.Length - 1];
                    Debug.Log($"[NarrativeExecutor] 📍 {name} usando punto parcial más cercano: {lastReachablePoint}");
                    targetPos = lastReachablePoint;
                }
                else
                {
                    Debug.LogError($"[NarrativeExecutor] ❌ {name} no hay camino válido - abortando movimiento");
                    
                    // Reactivar brain si fue desactivado antes de abortar
                    if (hadActiveBrain && combatBrain != null)
                    {
                        combatBrain.enabled = true;
                    }
                    yield break;
                }
            }
            
            // Configurar y comenzar movimiento
            agent.isStopped = false;
            agent.SetDestination(targetPos);
            
            Debug.Log($"[NarrativeExecutor] 🚶 {name} configurado para moverse a {targetPos}, distancia: {Vector3.Distance(transform.position, targetPos):F1}m");
            
            // ✅ CRÍTICO: Cancelar secuencia dizzy ANTES de resetear el animator
            var lifecycle = _npcManager.GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycle != null)
            {
                lifecycle.CancelDizzySequence();
                Debug.Log($"[NarrativeExecutor] 🛑 Secuencia dizzy cancelada para {name}");
            }
            
            // ✅ CRÍTICO: Resetear el animator para salir del estado dizzy/muerte antes de mover
            if (_npcManager.SimpleAnimator != null)
            {
                // Primero desactivar battle mode para volver al layer base
                _npcManager.SimpleAnimator.SetBattleMode(false);
                
                // Forzar transición a Idle para salir de dizzy
                _npcManager.SimpleAnimator.TransitionToIdle();
                
                // Resetear velocidad de movimiento
                _npcManager.SimpleAnimator.ResetMovement();
                
                Debug.Log($"[NarrativeExecutor] 🔄 Animator reseteado para {name} - BattleMode OFF, Idle forzado");
                
                // ✅ IMPORTANTE: Esperar un frame para que el Animator procese las transiciones
                yield return null;
            }
            
            // Activar animación de caminar
            if (_npcManager.SimpleAnimator != null)
            {
                float movementSpeed = agent.velocity.magnitude;
                _npcManager.SimpleAnimator.SetMovementSpeed(movementSpeed, 0.1f);
                Debug.Log($"[NarrativeExecutor] 🏃 Activando animación de caminar para {name} - Speed: {movementSpeed:F2}, AgentSpeed: {agent.speed:F2}, Velocity: {agent.velocity.magnitude:F2}");
            }
            
            float timer = 0f;
            bool hasReachedDestination = false;
            
            while (timer < maxDuration)
            {
                // Verificar si llegó
                if (!agent.pathPending && agent.remainingDistance < 1f)
                {
                    Debug.Log($"[NarrativeExecutor] ✅ {name} llegó al destino (remainingDistance: {agent.remainingDistance:F2}m)");
                    hasReachedDestination = true;
                    break;
                }
                
                // Actualizar animación según velocidad real
                if (_npcManager.SimpleAnimator != null && agent.velocity.magnitude > 0.1f)
                {
                    _npcManager.SimpleAnimator.SetMovementSpeed(agent.velocity.magnitude / Mathf.Max(agent.speed, 1f), 0.1f);
                }
                
                // Debug cada segundo
                if (Mathf.FloorToInt(timer) != Mathf.FloorToInt(timer + Time.deltaTime))
                {
                    Debug.Log($"[NarrativeExecutor] 🚶 {name} moviéndose... Distancia restante: {agent.remainingDistance:F1}m, Velocidad: {agent.velocity.magnitude:F1}");
                }
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            if (!hasReachedDestination)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⏱️ {name} timeout de movimiento (no llegó en {maxDuration}s)");
            }
            
            // Detener
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            _npcManager.SimpleAnimator?.SetMovementSpeed(0, 0.1f);
            
            // ✅ Reactivar combat brain si fue desactivado
            if (hadActiveBrain && combatBrain != null)
            {
                combatBrain.enabled = true;
                Debug.Log($"[NarrativeExecutor] 🔧 Reactivando NPCCombatBrain para {name}");
            }
            
            //Debug.Log($"[NarrativeExecutor] ✅ MoveToPositionDirect completado para {name}");
        }
        
        /// <summary>
        /// Desaparece el NPC con una transición visual opcional
        /// </summary>
        private IEnumerator DisappearWithTransition(TransitionSettings transition)
        {
            Debug.Log($"[NarrativeExecutor] 👻 {name} desapareciendo...");
            
            if (transition != null)
            {
                // Usar el sistema de transiciones
                var transitionManager = TransitionManager.Instance();
                if (transitionManager != null)
                {
                    // Iniciar transición visual
                    transitionManager.Transition(transition, 0f);
                    
                    // Esperar la mitad del tiempo de transición antes de desactivar
                    float totalTime = transition.transitionTime + transition.destroyTime;
                    yield return new WaitForSeconds(totalTime / 2f);
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor] ⚠️ TransitionManager no encontrado");
                    yield return new WaitForSeconds(0.5f);
                }
            }
            else
            {
                // Sin transición, pequeña pausa
                yield return new WaitForSeconds(0.3f);
            }
            
            // Desactivar el GameObject
            gameObject.SetActive(false);
            Debug.Log($"[NarrativeExecutor] ✅ {name} desactivado");
        }
        
        /// <summary>
        /// Obtiene una posición aleatoria válida en el NavMesh, ALEJÁNDOSE del player
        /// </summary>
        private Vector3 GetRandomMovePosition(float minRadius, float maxRadius)
        {
            // Obtener dirección OPUESTA al player para huir
            Vector3 fleeDirection = transform.forward; // Default: hacia adelante
            
            if (PlayerService.TryGetPlayer(out var player))
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0;
                if (toPlayer.sqrMagnitude > 0.1f)
                {
                    // Dirección OPUESTA al player
                    fleeDirection = -toPlayer.normalized;
                }
            }
            
            // Intentar encontrar un punto válido varias veces
            for (int i = 0; i < 10; i++)
            {
                // Añadir variación aleatoria a la dirección de huida (±45 grados)
                float randomAngle = Random.Range(-45f, 45f);
                Vector3 randomizedDir = Quaternion.Euler(0, randomAngle, 0) * fleeDirection;
                
                float randomDist = Random.Range(minRadius, maxRadius);
                Vector3 randomPoint = transform.position + randomizedDir * randomDist;
                
                // Verificar que está en NavMesh
                if (UnityEngine.AI.NavMesh.SamplePosition(randomPoint, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    // Verificar que hay un camino válido
                    var agent = _npcManager.Agent;
                    if (agent != null && agent.enabled && agent.isOnNavMesh)
                    {
                        UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
                        if (agent.CalculatePath(hit.position, path) && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                        {
                            return hit.position;
                        }
                    }
                    else
                    {
                        return hit.position;
                    }
                }
            }
            
            // Fallback: huir en la dirección opuesta al player
            Debug.LogWarning($"[NarrativeExecutor] ⚠️ No se encontró punto aleatorio válido para {name}, usando dirección de huida");
            return transform.position + fleeDirection * minRadius;
        }
        
        /// <summary>
        /// Mueve a todos los miembros del equipo a puntos aleatorios
        /// </summary>
        private void MoveTeamMembersToRandomPoints(NPCCombatTeam team, NarrativeChainEntry entry)
        {
            foreach (var member in team.AllMembers)
            {
                // Saltar al líder (ya se está moviendo con la lógica principal)
                if (member == _npcManager) continue;
                if (member == null || !member.gameObject.activeInHierarchy) continue;
                
                // Obtener posición aleatoria para este miembro
                Vector3 memberTargetPos = GetRandomMovePositionForMember(member.transform, entry.randomMoveMinRadius, entry.randomMoveMaxRadius);
                
                Debug.Log($"[NarrativeExecutor] 🎲 Moviendo miembro de equipo {member.name} a punto aleatorio: {memberTargetPos}");
                
                // Iniciar movimiento del miembro
                StartCoroutine(MoveTeamMemberAndDisappear(member, memberTargetPos, entry));
            }
        }
        
        /// <summary>
        /// Obtiene posición aleatoria para un miembro específico del equipo, ALEJÁNDOSE del player
        /// </summary>
        private Vector3 GetRandomMovePositionForMember(Transform memberTransform, float minRadius, float maxRadius)
        {
            // Obtener dirección OPUESTA al player para huir
            Vector3 fleeDirection = memberTransform.forward; // Default: hacia adelante
            
            if (PlayerService.TryGetPlayer(out var player))
            {
                Vector3 toPlayer = player.transform.position - memberTransform.position;
                toPlayer.y = 0;
                if (toPlayer.sqrMagnitude > 0.1f)
                {
                    // Dirección OPUESTA al player
                    fleeDirection = -toPlayer.normalized;
                }
            }
            
            for (int i = 0; i < 10; i++)
            {
                // Añadir variación aleatoria a la dirección de huida (±45 grados)
                float randomAngle = Random.Range(-45f, 45f);
                Vector3 randomizedDir = Quaternion.Euler(0, randomAngle, 0) * fleeDirection;
                
                float randomDist = Random.Range(minRadius, maxRadius);
                Vector3 randomPoint = memberTransform.position + randomizedDir * randomDist;
                
                if (UnityEngine.AI.NavMesh.SamplePosition(randomPoint, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }
            
            // Fallback: huir en la dirección opuesta al player
            return memberTransform.position + fleeDirection * minRadius;
        }
        
        /// <summary>
        /// Corrutina para mover un miembro del equipo y hacerlo desaparecer
        /// </summary>
        private IEnumerator MoveTeamMemberAndDisappear(NPCBehaviourManagerV2 member, Vector3 targetPos, NarrativeChainEntry entry)
        {
            var agent = member.Agent;
            if (agent == null) 
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ Agent NULL para {member.name}");
                yield break;
            }
            
            // ✅ Verificar si hay NPCCombatBrain activo que pueda interferir
            var combatBrain = member.GetComponent<NPCCombatBrain>();
            bool hadActiveBrain = false;
            if (combatBrain != null && combatBrain.enabled)
            {
                combatBrain.enabled = false;
                hadActiveBrain = true;
                Debug.Log($"[NarrativeExecutor] 🔧 Desactivando NPCCombatBrain temporalmente para {member.name}");
            }
            
            // ✅ Reactivar agent si está deshabilitado
            if (!agent.enabled)
            {
                Debug.Log($"[NarrativeExecutor] 🔧 Reactivando agent para {member.name}");
                agent.enabled = true;
                yield return new WaitForSeconds(0.1f);
            }
            
            if (!agent.isOnNavMesh)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {member.name} no está en NavMesh");
                yield break;
            }
            
            Debug.Log($"[NarrativeExecutor] 🚶 Miembro {member.name} moviéndose de {member.transform.position} a {targetPos}");
            
            // ✅ Verificar que hay un camino válido antes de moverse
            UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
            if (!agent.CalculatePath(targetPos, path))
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {member.name} no puede calcular camino a {targetPos}");
                
                // Reactivar brain si fue desactivado antes de abortar
                if (hadActiveBrain && combatBrain != null)
                {
                    combatBrain.enabled = true;
                }
                yield break;
            }
            
            if (path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete)
            {
                Debug.LogWarning($"[NarrativeExecutor] ⚠️ {member.name} camino incompleto - status: {path.status}");
                
                // Si el camino es parcial, intentar ir al punto más cercano alcanzable
                if (path.status == UnityEngine.AI.NavMeshPathStatus.PathPartial && path.corners.Length > 1)
                {
                    Vector3 lastReachablePoint = path.corners[path.corners.Length - 1];
                    Debug.Log($"[NarrativeExecutor] 📍 {member.name} usando punto parcial más cercano: {lastReachablePoint}");
                    targetPos = lastReachablePoint;
                }
                else
                {
                    Debug.LogError($"[NarrativeExecutor] ❌ {member.name} no hay camino válido - abortando");
                    
                    // Reactivar brain si fue desactivado antes de abortar
                    if (hadActiveBrain && combatBrain != null)
                    {
                        combatBrain.enabled = true;
                    }
                    yield break;
                }
            }
            
            // ✅ Asegurar que el agent mueva y rote el transform
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.SetDestination(targetPos);
            
            // Obtener el animador
            var animator = member.SimpleAnimator;
            
            Debug.Log($"[NarrativeExecutor] 🎬 Animator para {member.name}: {(animator != null ? "OK" : "NULL")}");
            
            // ✅ CRÍTICO: Cancelar secuencia dizzy ANTES de resetear el animator
            var lifecycle = member.GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycle != null)
            {
                lifecycle.CancelDizzySequence();
                Debug.Log($"[NarrativeExecutor] 🛑 Secuencia dizzy cancelada para {member.name}");
            }
            
            // ✅ CRÍTICO: Resetear el animator para salir del estado dizzy/muerte
            if (animator != null)
            {
                // Primero desactivar battle mode para volver al layer base
                animator.SetBattleMode(false);
                
                // Forzar transición a Idle para salir de dizzy
                animator.TransitionToIdle();
                
                // Resetear velocidad de movimiento
                animator.ResetMovement();
                
                Debug.Log($"[NarrativeExecutor] 🔄 Animator reseteado para {member.name} - BattleMode OFF, Idle forzado");
            }
            
            // Esperar a que llegue o timeout
            float timer = 0;
            float lastLogTime = 0;
            while (timer < entry.maxMovementDuration)
            {
                if (!agent.pathPending && agent.remainingDistance < 1f)
                {
                    Debug.Log($"[NarrativeExecutor] ✅ Miembro {member.name} llegó al destino");
                    break;
                }
                
                // ✅ Actualizar animación según velocidad real cada frame
                if (animator != null)
                {
                    float currentSpeed = agent.velocity.magnitude;
                    if (currentSpeed > 0.1f)
                    {
                        float normalizedSpeed = currentSpeed / Mathf.Max(agent.speed, 1f);
                        animator.SetMovementSpeed(normalizedSpeed, 0.1f);
                        
                        // Log cada segundo
                        if (timer - lastLogTime >= 1f)
                        {
                            Debug.Log($"[NarrativeExecutor] 🚶 {member.name} - Speed: {currentSpeed:F2}, Normalized: {normalizedSpeed:F2}, Dist: {agent.remainingDistance:F1}m, BattleMode: {animator.IsInBattleMode}");
                            lastLogTime = timer;
                        }
                    }
                    else
                    {
                        animator.SetMovementSpeed(0f, 0.1f);
                    }
                }
                else if (timer - lastLogTime >= 1f)
                {
                    Debug.LogWarning($"[NarrativeExecutor] ⚠️ {member.name} sin animator - velocidad: {agent.velocity.magnitude:F2}");
                    lastLogTime = timer;
                }
                
                timer += Time.deltaTime;
                yield return null;
            }
            
            // Detener movimiento
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            animator?.SetMovementSpeed(0, 0.1f);
            
            // ✅ Reactivar combat brain si fue desactivado
            if (hadActiveBrain && combatBrain != null)
            {
                combatBrain.enabled = true;
                Debug.Log($"[NarrativeExecutor] 🔧 Reactivando NPCCombatBrain para {member.name}");
            }
            
            // Desaparecer si está configurado
            if (entry.disappearOnArrival)
            {
                Debug.Log($"[NarrativeExecutor] 👻 Miembro de equipo {member.name} desapareciendo...");
                
                if (entry.disappearTransition != null)
                {
                    // Esperar la mitad del tiempo de transición (el líder maneja la transición global)
                    float totalTime = entry.disappearTransition.transitionTime + entry.disappearTransition.destroyTime;
                    yield return new WaitForSeconds(totalTime / 2f);
                }
                else
                {
                    yield return new WaitForSeconds(0.3f);
                }
                
                member.gameObject.SetActive(false);
                Debug.Log($"[NarrativeExecutor] ✅ Miembro {member.name} desactivado");
            }
        }

        private IEnumerator ExecuteMoveWithPlayerFollow(NarrativeChainEntry entry, Vector3 targetPos, SpawnAnchor targetAnchor)
        {
            // Nota: Esta lógica se mantiene similar a la original porque es específica de tu juego
            // pero limpiamos las referencias.
            var agent = _npcManager.Agent;
            var player = PlayerService.Player;
            if (!agent || !player) yield break;

            // ✅ Asegurar que el agent mueva y rote el transform
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.SetDestination(targetPos);
            
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
            
            // ✅ MEJORA: Siempre aplicar orientación del SpawnAnchor si existe (independiente de turnAroundOnArrival)
            // El flag turnAroundOnArrival solo controla el fallback de giro 180° cuando NO hay anchor
            SpawnAnchor anchor = targetAnchor ?? FindNearbySpawnAnchor(targetPos);
            
            if (anchor != null)
            {
                // CONVENCIÓN: El SpawnAnchor se coloca con el eje Z (forward) apuntando
                // hacia donde quieres que mire el personaje POR DEFECTO
                Quaternion targetRotation;
                if (anchor.faceDoor)
                {
                    // faceDoor = true → Invertir la dirección (mirar al lado contrario)
                    targetRotation = Quaternion.LookRotation(-anchor.transform.forward, Vector3.up);
                }
                else
                {
                    // faceDoor = false (por defecto) → Usar la dirección del anchor tal cual
                    targetRotation = Quaternion.LookRotation(anchor.transform.forward, Vector3.up);
                }
                transform.rotation = targetRotation;
                Debug.Log($"[NPCInteractiveNarrativeExecutor] NPC '{gameObject.name}' orientado según SpawnAnchor '{anchor.anchorId}' (faceDoor={anchor.faceDoor})");
            }
            else if (entry.turnAroundOnArrival)
            {
                // Fallback: girar 180° si no hay SpawnAnchor Y está activada la opción
                transform.rotation *= Quaternion.Euler(0, 180, 0);
                Debug.Log($"[NPCInteractiveNarrativeExecutor] NPC '{gameObject.name}' girado 180° (sin SpawnAnchor cercano)");
            }
        }
        
        /// <summary>
        /// Busca un SpawnAnchor cerca de una posición usando el AnchorRegistry (O(n) sobre anchors registrados)
        /// </summary>
        private SpawnAnchor FindNearbySpawnAnchor(Vector3 position)
        {
            const float searchRadius = 2f;
            float searchRadiusSqr = searchRadius * searchRadius;
            
            SpawnAnchor closest = null;
            float closestDistanceSqr = searchRadiusSqr;
            
            // Usar el registro en lugar de FindObjectsByType (mucho más eficiente)
            foreach (var kvp in AnchorRegistry.All)
            {
                var anchor = kvp.Value;
                if (anchor == null) continue;
                
                // Usar sqrMagnitude para evitar sqrt costoso
                float distanceSqr = (anchor.transform.position - position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
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

            Debug.Log($"[NarrativeExecutor] ⚙️ Preparando combate para {gameObject.name}");
            
            // 1. Preparar Capas y Config
            SwitchToEnemyLayer();
            _npcManager.Configuration.combatConfig = entry.combatConfig;
            
            // ✅ Transferir configuración de eventos de derrota
            if (entry.sendEventOnDefeat && !string.IsNullOrEmpty(entry.defeatEventKey))
            {
                entry.combatConfig.sendEventOnDefeat = entry.sendEventOnDefeat;
                entry.combatConfig.defeatEventKey = entry.defeatEventKey;
                entry.combatConfig.sendDefeatEventBeforeDeath = entry.sendDefeatEventBeforeDeath;
                Debug.Log($"[NarrativeExecutor] 📤 Configurado evento de derrota: '{entry.defeatEventKey}'");
            }
            
            // 2. Asegurar que los componentes de combate existan
            if (!GetComponent<Damageable>())
            {
                var dmg = gameObject.AddComponent<Damageable>();
                dmg.SetMaxAndCurrent(entry.combatConfig.health, entry.combatConfig.health);
                dmg.SetDestroyOnDeath(false);
                Debug.Log($"[NarrativeExecutor] 🛡️ Damageable añadido");
            }
            
            NPCCombatLifecycleHandler lifecycleHandler = GetComponent<NPCCombatLifecycleHandler>();
            if (lifecycleHandler == null)
            {
                lifecycleHandler = gameObject.AddComponent<NPCCombatLifecycleHandler>();
                Debug.Log($"[NarrativeExecutor] ☠️ NPCCombatLifecycleHandler añadido para {gameObject.name}");
            }
            
            // 3. Verificar si hay un equipo de combate
            var combatTeam = GetComponent<NPCCombatTeam>();
            bool hasTeam = combatTeam != null && combatTeam.TeamSize > 1;
            
            // 4. Esperar a que termine cualquier diálogo activo antes de iniciar combate
            while (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            {
                yield return null;
            }
            
            // 5. AHORA sí iniciar el combate (después del diálogo)
            Debug.Log($"[NarrativeExecutor] ⚔️ Iniciando combate para {gameObject.name}");
            _npcManager.EnterCombat();
            
            // Pequeña espera para que el combate se inicialice
            yield return new WaitForSeconds(0.5f);
            
            // 6. Esperar a que el combate termine
            if (hasTeam)
            {
                // ✅ EQUIPO: Esperar a que TODO el equipo sea derrotado
                Debug.Log($"[NarrativeExecutor] 👥 Esperando a que todo el equipo sea derrotado ({combatTeam.TeamSize} miembros)...");
                
                while (!combatTeam.IsTeamDefeated)
                {
                    // También verificar si el combate terminó por otra razón
                    if (_npcManager.Context != null && !_npcManager.Context.IsInCombat && !combatTeam.IsTeamInCombat)
                    {
                        Debug.Log($"[NarrativeExecutor] ⚠️ Combate de equipo terminó sin derrota total");
                        break;
                    }
                    yield return null;
                }
                
                Debug.Log($"[NarrativeExecutor] 💀 ¡Todo el equipo ha sido derrotado!");
                
                // ✅ Esperar a que el diálogo de dizzy se abra (máximo 5 segundos)
                Debug.Log($"[NarrativeExecutor] 💬 Esperando a que se abra el diálogo de dizzy...");
                
                float waitForDialogueTimeout = 5f;
                float waitTimer = 0f;
                
                // Esperar hasta que el diálogo se abra o timeout
                while (waitTimer < waitForDialogueTimeout)
                {
                    if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
                    {
                        Debug.Log($"[NarrativeExecutor] 💬 Diálogo de dizzy abierto");
                        break;
                    }
                    waitTimer += Time.deltaTime;
                    yield return null;
                }
                
                // Si el diálogo está abierto, esperar a que el usuario lo cierre
                if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
                {
                    Debug.Log($"[NarrativeExecutor] 💬 Esperando a que el usuario cierre el diálogo...");
                    
                    while (DialogueManager.Instance.IsOpen)
                    {
                        yield return null;
                    }
                    
                    Debug.Log($"[NarrativeExecutor] ✅ Diálogo de dizzy cerrado por el usuario");
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor] ⚠️ El diálogo de dizzy no se abrió (timeout o no configurado)");
                }
            }
            else
            {
                // NPC INDIVIDUAL: Esperar a que este NPC sea derrotado
                while (true)
                {
                    if (lifecycleHandler != null && lifecycleHandler.IsDefeatedAndInactive)
                    {
                        Debug.Log($"[NarrativeExecutor] 💀 NPC derrotado - combate terminado");
                        break;
                    }
                    
                    if (_npcManager.Context != null && !_npcManager.Context.IsInCombat && !_npcManager.Context.WasDefeatedInCombat)
                    {
                        Debug.Log($"[NarrativeExecutor] ⚠️ Combate terminó sin derrota");
                        break;
                    }
                    
                    if (_npcManager.Context != null && _npcManager.Context.WasDefeatedInCombat)
                    {
                        Debug.Log($"[NarrativeExecutor] ✅ Secuencia de derrota completada");
                        break;
                    }
                    
                    yield return null;
                }
                
                // ✅ Esperar a que el diálogo de dizzy se abra (máximo 5 segundos)
                Debug.Log($"[NarrativeExecutor] 💬 Esperando a que se abra el diálogo de dizzy...");
                
                float waitForDialogueTimeout = 5f;
                float waitTimer = 0f;
                
                while (waitTimer < waitForDialogueTimeout)
                {
                    if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
                    {
                        Debug.Log($"[NarrativeExecutor] 💬 Diálogo de dizzy abierto");
                        break;
                    }
                    waitTimer += Time.deltaTime;
                    yield return null;
                }
                
                // Si el diálogo está abierto, esperar a que el usuario lo cierre
                if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
                {
                    Debug.Log($"[NarrativeExecutor] 💬 Esperando a que el usuario cierre el diálogo...");
                    
                    while (DialogueManager.Instance.IsOpen)
                    {
                        yield return null;
                    }
                    
                    Debug.Log($"[NarrativeExecutor] ✅ Diálogo de dizzy cerrado por el usuario");
                }
                else
                {
                    Debug.LogWarning($"[NarrativeExecutor] ⚠️ El diálogo de dizzy no se abrió (timeout o no configurado)");
                }
            }
            
            Debug.Log($"[NarrativeExecutor] ✅ ExecuteStartCombat completado - continuando con siguiente acción");
        }

        private IEnumerator HandlePostNarrativeState(ConditionalNarrative narrativeData)
        {
            // Si la narrativa no tiene postNarrativeState o es None, no hacer nada
            if (narrativeData == null || narrativeData.postNarrativeState == PostNarrativeState.None)
            {
                yield break;
            }
            
            // Solo ejecutar postNarrativeState si la narrativa es singleUse
            // Las narrativas repetibles no deberían cambiar el estado del NPC permanentemente
            if (!narrativeData.singleUse)
            {
                Debug.Log($"[NarrativeExecutor:{name}] ⏸️ PostNarrativeState ignorado - narrativa '{narrativeData.description}' no es singleUse");
                yield break;
            }
            
            Debug.Log($"[NarrativeExecutor:{name}] ✅ Ejecutando PostNarrativeState: {narrativeData.postNarrativeState} para narrativa '{narrativeData.description}'");
            
            // Decidir qué hacer al terminar la narrativa
            switch (narrativeData.postNarrativeState)
            {
                case PostNarrativeState.Idle:
                    _npcManager.ForceIdle();
                    break;

                case PostNarrativeState.Wander:
                case PostNarrativeState.SwitchToAmbient:
                    if (narrativeData.postNarrativeAmbientConfig != null)
                        _npcManager.Configuration.ambientConfig = narrativeData.postNarrativeAmbientConfig;
                    // TODO: Activar comportamiento wander
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
            yield return _waitOneSecond; // Startup delay

            while (true)
            {
                // ✅ NUEVO: Si pertenece a un equipo y NO es líder, no detectar
                // El líder del equipo manejará la detección y notificará a los compañeros
                var teamMember = GetComponent<NPCTeamMember>();
                if (teamMember != null && teamMember.HasTeam && !teamMember.IsLeader)
                {
                    // Este NPC es parte de un equipo pero no es líder - NO detectar
                    yield return _waitHalfSecond;
                    continue;
                }
                
                // Obtener la narrativa activa actual (forzar refresh para detección)
                var activeNarrative = _config?.GetActiveNarrative();
                
                // Si no hay narrativa activa o no tiene autoStartOnDetection, esperar
                if (activeNarrative == null || !activeNarrative.autoStartOnDetection)
                {
                    yield return _waitHalfSecond;
                    continue;
                }
                
                // Si ya se detectó o se usó, salir
                if (_hasDetectedPlayer || _isExecuting)
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
                        // ✅ NUEVO: Si es líder de equipo, reagrupar primero
                        var combatTeam = GetComponent<NPCCombatTeam>();
                        if (combatTeam != null)
                        {
                            // Notificar al equipo - esto reagrupará a los compañeros
                            combatTeam.OnPlayerDetected(_player);
                            
                            // Esperar a que el equipo se reagrupe antes de mostrar alerta
                            while (combatTeam.IsRegrouping)
                            {
                                yield return null;
                            }
                        }
                        
                        _hasDetectedPlayer = true;
                        
                        Debug.Log($"[NarrativeExecutor] 🎯 {name} - Equipo reagrupado, iniciando secuencia de alerta...");
                        yield return StartAlertSequence(); // Exclamación !
                        
                        Debug.Log($"[NarrativeExecutor] 🎭 {name} - Alerta completada, intentando ejecutar narrativa...");
                        Debug.Log($"[NarrativeExecutor] 📋 {name} - Estado: _isExecuting={_isExecuting}, _config={((_config != null) ? "OK" : "NULL")}");
                        
                        // ✅ Esperar a que TryExecuteNarrative inicie la ejecución
                        bool narrativeStarted = TryExecuteNarrative();
                        
                        Debug.Log($"[NarrativeExecutor] {(narrativeStarted ? "✅" : "❌")} {name} - Narrativa {(narrativeStarted ? "INICIADA" : "NO INICIADA")}");
                        
                        // Solo resetear si la narrativa NO se ejecutó (por ejemplo, si ya estaba ejecutándose)
                        // Si se ejecutó, _isExecuting será true y el loop esperará automáticamente
                        if (!narrativeStarted)
                        {
                            _hasDetectedPlayer = false;
                            Debug.LogWarning($"[NarrativeExecutor] ⚠️ {name} - Reseteando _hasDetectedPlayer porque la narrativa no se inició");
                        }
                        // Si se ejecutó, _hasDetectedPlayer permanece en true hasta que termine la ejecución completa
                    }
                }
                yield return _waitPointTwo;
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
                
                // Configurar la altura del icono
                float iconHeight = _config.alertIconHeight;
                if (iconHeight <= 0 && _npcManager.Configuration.combatConfig != null)
                {
                    iconHeight = _npcManager.Configuration.combatConfig.alertIconHeight;
                }
                
                if (iconHeight > 0)
                {
                    _alertIconController.SetIconOffset(new Vector3(0f, iconHeight, 0f));
                }
                
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
            if (!PlayerService.TryGetPlayer(out var p, true))
                yield break;
            
            Vector3 dir = p.transform.position - transform.position;
            dir.y = 0; // Ignorar altura
            
            // Si el jugador está muy cerca o justo encima, no rotar
            if (dir.sqrMagnitude < 0.01f)
                yield break;
            
            dir.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(dir);
            
            // Calcular el ángulo entre la rotación actual y la objetivo
            float angle = Quaternion.Angle(transform.rotation, targetRotation);
            
            // Si ya está mirando al jugador (ángulo menor a 5 grados), no rotar
            if (angle < 5f)
                yield break;
            
            // Rotar suavemente
            float duration = _config.rotationDuration;
            float elapsed = 0f;
            Quaternion startRotation = transform.rotation;
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); // Suavizado
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
            
            // Asegurar rotación final exacta
            transform.rotation = targetRotation;
        }

        private void SendNarrativeEvent(string key)
        {
            if (!string.IsNullOrEmpty(key)) 
                DefaultNarrativeSignals.Instance?.RaiseCustom(key);
        }

        // --- PERSISTENCIA MEJORADA ---
        // Ahora guarda el estado de cada narrativa condicional individualmente
        
        private void SaveState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId)) 
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ SaveState: persistenceId está vacío, no se puede guardar");
                return;
            }
            
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null) 
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ SaveState: preset es null, no se puede guardar");
                return;
            }
            
            // Asegurar que la lista existe
            if (preset.completedInteractiveNarratives == null)
            {
                preset.completedInteractiveNarratives = new System.Collections.Generic.List<string>();
                Debug.Log($"[NarrativeExecutor:{name}] 📋 Creada nueva lista completedInteractiveNarratives");
            }
            
            // Guardar estado del config general
            if (!preset.completedInteractiveNarratives.Contains(_config.persistenceId))
            {
                preset.completedInteractiveNarratives.Add(_config.persistenceId);
                Debug.Log($"[NarrativeExecutor:{name}] 💾 Guardado config general: {_config.persistenceId}");
            }
            
            // Guardar estado de cada narrativa condicional ejecutada
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
                            Debug.Log($"[NarrativeExecutor:{name}] 💾 Guardada narrativa condicional #{i}: {narrativeId}");
                        }
                    }
                }
            }
            
            // Verificar el estado final
            Debug.Log($"[NarrativeExecutor:{name}] 📊 SaveState finalizado - Total en preset: {preset.completedInteractiveNarratives.Count} narrativas");
        }

        private void RestoreState()
        {
            if (string.IsNullOrEmpty(_config.persistenceId)) return;
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset == null)
            {
                Debug.LogWarning($"[NarrativeExecutor:{name}] ⚠️ RestoreState: preset es null");
                return;
            }
            
            // Debug.Log($"[NarrativeExecutor:{name}] 🔄 RestoreState - completedInteractiveNarratives tiene {preset.completedInteractiveNarratives?.Count ?? 0} entradas");
            
            // Restaurar estado del config general
            _hasBeenUsed = preset.completedInteractiveNarratives.Contains(_config.persistenceId);
            
            // Restaurar estado de cada narrativa condicional
            if (_config.conditionalNarratives != null)
            {
                for (int i = 0; i < _config.conditionalNarratives.Length; i++)
                {
                    var narrative = _config.conditionalNarratives[i];
                    if (narrative != null)
                    {
                        string narrativeId = GetConditionalNarrativeId(i);
                        
                        // IMPORTANTE: Primero resetear el estado, luego restaurar si está en la lista
                        // Esto asegura que en nueva partida (lista vacía), las narrativas empiezan sin ejecutar
                        narrative.ResetExecutionState();
                        
                        bool wasCompleted = preset.completedInteractiveNarratives.Contains(narrativeId);
                        // Debug.Log($"[NarrativeExecutor:{name}] Narrativa #{i} '{narrative.description}' - ID: {narrativeId}, EnLista: {wasCompleted}, SingleUse: {narrative.singleUse}");
                        
                        if (wasCompleted)
                        {
                            narrative.MarkAsExecuted();
                            // Debug.Log($"[NarrativeExecutor:{name}] 🔄 Restaurada narrativa condicional #{i} como ejecutada: {narrativeId}");
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Genera un ID único para una narrativa condicional específica
        /// </summary>
        private string GetConditionalNarrativeId(int index)
        {
            return $"{_config.persistenceId}_CN{index}";
        }

        /// <summary>
        /// Resetea el estado de ejecución y opcionalmente restaura desde el preset.
        /// Se llama cuando se carga una partida o se inicia nueva partida para
        /// sincronizar el estado con el preset actual.
        /// </summary>
        /// <param name="restoreFromPreset">Si es true, restaura el estado desde el preset después del reset</param>
        public void ResetState(bool restoreFromPreset = true)
        {
            _hasBeenUsed = false;
            _hasDetectedPlayer = false;
            _isExecuting = false;
            
            // Resetear estado de todas las narrativas condicionales
            if (_config?.conditionalNarratives != null)
            {
                foreach (var narrative in _config.conditionalNarratives)
                {
                    narrative?.ResetExecutionState();
                }
            }
            
            // Restaurar estado desde el preset si está configurado
            if (restoreFromPreset && _config != null && _config.persistState && !string.IsNullOrEmpty(_config.persistenceId))
            {
                RestoreState();
                Debug.Log($"[NarrativeExecutor:{name}] 🔄 Estado reseteado y restaurado desde preset");
            }
        }
    }
}