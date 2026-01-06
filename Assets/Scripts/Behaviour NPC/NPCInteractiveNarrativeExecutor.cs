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

            // 4. Estado Post-Narrativa (usa el de la narrativa específica)
            yield return HandlePostNarrativeState(narrativeData);

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
            while (!completed)
            {
                yield return null;
            }
            
            // Breve pausa para limpieza de UI
            yield return _waitPointOne;
        }

        private IEnumerator ExecuteMove(NarrativeChainEntry entry)
        {
            Vector3 targetPos = GetTargetPosition(entry);
            if (targetPos == Vector3.zero) yield break;

            // ✅ Obtener el SpawnAnchor de destino si existe
            SpawnAnchor targetAnchor = null;
            if (!string.IsNullOrEmpty(entry.targetAnchorName))
            {
                targetAnchor = SpawnAnchor.FindById(entry.targetAnchorName);
            }

            if (entry.waitForPlayer)
            {
                // Lógica compleja de seguimiento (Player Follow)
                yield return ExecuteMoveWithPlayerFollow(entry, targetPos, targetAnchor);
            }
            else
            {
                // Movimiento Estándar usando el sistema cinemático del Manager
                var moveSeq = new MoveToPositionSequence(
                    _npcManager, 
                    targetPos, 
                    entry.maxMovementDuration, 
                    entry.turnAroundOnArrival, 
                    999f, // Walk duration infinita (sin teleport)
                    targetAnchor // ✅ Pasar el anchor de destino
                );
                
                _npcManager.StartCinematicSequence(moveSeq);
                
                while (!moveSeq.IsCompleted) yield return null;
            }
        }

        private IEnumerator ExecuteMoveWithPlayerFollow(NarrativeChainEntry entry, Vector3 targetPos, SpawnAnchor targetAnchor)
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
            
            // ✅ FIX CRÍTICO: Transferir configuración de eventos de derrota desde NarrativeChainEntry
            // al NPCCombatConfig para que NPCCombatLifecycleHandler pueda enviar el evento
            if (entry.sendEventOnDefeat && !string.IsNullOrEmpty(entry.defeatEventKey))
            {
                entry.combatConfig.sendEventOnDefeat = entry.sendEventOnDefeat;
                entry.combatConfig.defeatEventKey = entry.defeatEventKey;
                entry.combatConfig.sendDefeatEventBeforeDeath = entry.sendDefeatEventBeforeDeath;
                Debug.Log($"[NarrativeExecutor] 📤 Configurado evento de derrota: '{entry.defeatEventKey}' (antes de muerte: {entry.sendDefeatEventBeforeDeath})");
            }
            
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
                        _hasDetectedPlayer = true;
                        yield return StartAlertSequence(); // Exclamación !
                        TryExecuteNarrative();
                        
                        // Después de ejecutar, resetear _hasDetectedPlayer para permitir
                        // que futuras narrativas con autoStartOnDetection también funcionen
                        _hasDetectedPlayer = false;
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
            
            Debug.Log($"[NarrativeExecutor:{name}] 🔄 RestoreState - completedInteractiveNarratives tiene {preset.completedInteractiveNarratives?.Count ?? 0} entradas");
            
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
                        Debug.Log($"[NarrativeExecutor:{name}] Narrativa #{i} '{narrative.description}' - ID: {narrativeId}, EnLista: {wasCompleted}, SingleUse: {narrative.singleUse}");
                        
                        if (wasCompleted)
                        {
                            narrative.MarkAsExecuted();
                            Debug.Log($"[NarrativeExecutor:{name}] 🔄 Restaurada narrativa condicional #{i} como ejecutada: {narrativeId}");
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