using System.Collections.Generic;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de combate para NPCs aliados/compañeros.
    /// VERSIÓN MEJORADA: Usa ActiveCombatRegistry para encontrar enemigos activos en combate.
    /// </summary>
    public class AllyCombatState : NPCStateBase
    {
        public override string StateName => "AllyCombat";

        private Transform _currentTarget;
        private Transform _forcedTarget; // Target forzado desde OnPlayerEnteredCombat
        private string _forcedTargetName; // Nombre del target forzado para re-búsqueda
        private NPCPartyMember _partyMember;
        private Transform _player;
        
        // Cooldowns de hechizos
        private float _lastAttackTime;
        private float _attackCooldown; // Cooldown dinámico basado en el hechizo
        
        // Configuración
        private const float ATTACK_RANGE = 15f;
        private const float DETECTION_RANGE = 30f; // ✅ OPTIMIZACIÓN: Reducido de 100m a 30m (más razonable)
        private const float DEFAULT_ATTACK_COOLDOWN = 2f;
        private const float NO_ENEMY_TIMEOUT = 30f; // 30 segundos - suficiente tiempo para batallas de boss
        
        private float _noEnemyTimer;
        private float _searchCooldown; // Para no buscar cada frame
        
        // Movimiento táctico - valores ajustados para comportamiento más dinámico
        private Vector3 _tacticalPosition;
        private float _repositionTimer;
        private const float REPOSITION_INTERVAL = 1.5f; // Reposicionarse cada 1.5 segundos (más frecuente)
        private const float MIN_COMBAT_DISTANCE = 4f; // Distancia mínima al enemigo (más cerca)
        private const float MAX_COMBAT_DISTANCE = 10f; // Distancia máxima al enemigo (más cerca)
        private const float STRAFE_RADIUS = 5f; // Radio de movimiento lateral (más amplio)
        
        // Seguimiento del player durante combate
        private const float MAX_PLAYER_DISTANCE = 30f; // Si el player está más lejos, acercarse a él
        
        // Estados de comportamiento
        private enum CombatBehavior { Idle, Approaching, Strafing, Retreating, FollowingPlayer }
        private CombatBehavior _currentBehavior = CombatBehavior.Idle;
        private float _behaviorTimer;
        private const float BEHAVIOR_CHANGE_CHANCE = 0.3f; // Probabilidad de cambiar comportamiento
        
        // Sistema de casting con delay
        private bool _isCasting;
        private float _castTimer;
        private MagicSpellSO _pendingSpell;
        
        // Rotación de hechizos entre los 3 disponibles
        private int _currentSpellIndex = 0;
        
        // ✅ OPTIMIZACIÓN FASE 1: Buffer reutilizable para Physics queries (evita allocations)
        private Collider[] _physicsBuffer = new Collider[32];

        // ✅ FIX #17 (auditoría combate, 15 ago 2026): buffer reutilizable para
        // ActiveCombatRegistry.GetAllInCombatNonAlloc (antes GetAllInCombat() alocaba una List
        // nueva en cada llamada, incluida desde OnUpdate/FindNearestEnemy con cooldown de 0.3s).
        private readonly List<GameObject> _combatNpcBuffer = new List<GameObject>(16);

        // ✅ FIX #20 (auditoría combate, 15 ago 2026): LayerMask.GetMask cacheado como static readonly
        // (antes se recalculaba en FindNearestEnemy y en cada lanzamiento de hechizo).
        private static readonly int EnemyBossMask = LayerMask.GetMask("Enemy", "Boss");
        private static readonly int EnemyBossDefaultMask = LayerMask.GetMask("Enemy", "Boss", "Default");

        // Cache de Damageable para el target actual (evita GetComponent cada frame)
        private Damageable _currentTargetDamageable;
        private Transform _damageableCachedFor;

        // Escudo
        private NPCShieldController _shieldController;
        private float _nextShieldTime;

        // Indica si la salida del combate fue una victoria (no cinemática ni timeout)
        private bool _exitingAsVictory;

        /// <summary>
        /// Constructor que permite pasar un target inicial (desde OnPlayerEnteredCombat)
        /// </summary>
        public AllyCombatState(Transform forcedTarget = null)
        {
            _forcedTarget = forcedTarget;
            // Guardar el nombre para poder re-buscar si el Transform se vuelve inválido
            _forcedTargetName = forcedTarget != null ? forcedTarget.name : null;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            if (context.DebugMode)
            {
                Debug.Log($"[AllyCombatState:{context.Transform.name}] ⚔️ ENTRANDO EN COMBATE - ForcedTarget: {_forcedTarget?.name ?? "NULL"}, Registry.Count: {ActiveCombatRegistry.Count}");
                ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatNpcBuffer);
                foreach (var npc in _combatNpcBuffer)
                    Debug.Log($"[AllyCombatState:{context.Transform.name}]   Registry NPC: {npc?.name ?? "NULL"} (active: {npc?.activeInHierarchy})");
            }
            
            context.IsInCombat = true;
            
            _partyMember = context.Transform.GetComponent<NPCPartyMember>();

            var partyConfig = _partyMember?.PartyConfig;
            if (partyConfig != null && partyConfig.useShield)
            {
                _shieldController = context.Transform.GetComponent<NPCShieldController>();
                if (_shieldController == null)
                    _shieldController = context.Transform.gameObject.AddComponent<NPCShieldController>();
                _shieldController.ConfigureShield(partyConfig.shieldPrefab, partyConfig.shieldMinDuration, partyConfig.shieldMaxDuration);
                _shieldController.ConfigureBlockLayers("ProjectileEnemy", "EnemyProjectile");
                _nextShieldTime = Time.time + Random.Range(4f, 8f);
            }
            else
            {
                _shieldController = null;
            }
            _lastAttackTime = -DEFAULT_ATTACK_COOLDOWN; // Puede atacar inmediatamente
            _attackCooldown = DEFAULT_ATTACK_COOLDOWN;
            _noEnemyTimer = 0f;
            _searchCooldown = 0f;
            
            // Movimiento táctico
            _repositionTimer = 0f;
            _tacticalPosition = context.Transform.position;
            _currentBehavior = CombatBehavior.Idle;
            _behaviorTimer = 0f;
            
            // Sistema de casting
            _isCasting = false;
            _castTimer = 0f;
            _pendingSpell = null;
            
            // Obtener jugador
            if (PlayerService.Player != null)
            {
                _player = PlayerService.Player.transform;
            }
            
            // Si tenemos un target forzado, usarlo DIRECTAMENTE sin validaciones
            if (_forcedTarget != null && _forcedTarget.gameObject != null)
            {
                _currentTarget = _forcedTarget;
                if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] 🎯 Target forzado: {_forcedTarget.name}");
            }
            else
            {
                ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatNpcBuffer);
                if (_combatNpcBuffer.Count > 0)
                {
                    foreach (var npc in _combatNpcBuffer)
                    {
                        if (npc != null && npc != context.Transform.gameObject && npc.activeInHierarchy && IsTargetAlive(npc.transform))
                        {
                            _currentTarget = npc.transform;
                            _forcedTarget = npc.transform;
                            _forcedTargetName = npc.name;
                            break;
                        }
                    }
                }

                if (_currentTarget == null)
                    FindNearestEnemy(context);
            }
            
            // Activar modo batalla
            context.Animator?.SetBattleMode(true);
            
            // Parar movimiento inicial
            StopMovement(context);

            if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] OnEnter completado - Target: {_currentTarget?.name ?? "NINGUNO"}");
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            // PRIORIDAD 1: Si tenemos un target forzado que aún existe y está VIVO, mantenerlo
            if (_forcedTarget != null && _forcedTarget.gameObject != null && _forcedTarget.gameObject.activeInHierarchy)
            {
                // ✅ FIX #9 (auditoría combate, 15 ago 2026): reutiliza el cache de Damageable en vez
                // de GetComponent<Damageable>() cada frame (IsTargetAlive() lo hacía sin cachear, y
                // este chequeo se ejecuta todos los frames mientras hay target forzado).
                if (_damageableCachedFor != _forcedTarget)
                {
                    _currentTargetDamageable = _forcedTarget.GetComponent<Damageable>();
                    _damageableCachedFor = _forcedTarget;
                }
                bool forcedTargetAlive = _currentTargetDamageable == null || _currentTargetDamageable.Current > 0;

                if (forcedTargetAlive)
                {
                    _currentTarget = _forcedTarget;
                    _noEnemyTimer = 0f;
                }
                else
                {
                    if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] ☠️ Target forzado {_forcedTarget.name} está muerto, liberando target...");
                    _forcedTarget = null;
                    _forcedTargetName = null;
                    _currentTarget = null;
                    _currentTargetDamageable = null;
                    _damageableCachedFor = null;
                }
            }
            // PRIORIDAD 1.5: Si perdimos el target forzado pero tenemos su nombre, re-buscarlo en registry
            else if (_forcedTarget == null && !string.IsNullOrEmpty(_forcedTargetName))
            {
                // ✅ FIX #8 (auditoría combate, 15 ago 2026): TryFindTargetByName cae a
                // GameObject.Find(...) (recorre toda la escena) cuando no está en el registry. Sin
                // cooldown esto se ejecutaba cada frame mientras el target forzado no reaparecía.
                // Reutiliza _searchCooldown (se decrementa en la Prioridad 3 de más abajo, que se
                // ejecuta en el mismo frame si seguimos sin target).
                if (_searchCooldown <= 0f)
                {
                    var foundByName = TryFindTargetByName(_forcedTargetName);
                    if (foundByName != null)
                    {
                        _forcedTarget = foundByName;
                        _currentTarget = foundByName;
                        _noEnemyTimer = 0f;
                    }
                }
            }
            // PRIORIDAD 2: Verificar si current target sigue siendo válido
            else if (_currentTarget != null && _currentTarget.gameObject != null && _currentTarget.gameObject.activeInHierarchy)
            {
                // El target actual sigue existiendo, verificar si sigue vivo (con cache de Damageable)
                if (_damageableCachedFor != _currentTarget)
                {
                    _currentTargetDamageable = _currentTarget.GetComponent<Damageable>();
                    _damageableCachedFor = _currentTarget;
                }
                if (_currentTargetDamageable != null && _currentTargetDamageable.Current <= 0)
                {
                    if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] ☠️ Target {_currentTarget.name} murió, buscando otro...");
                    _currentTarget = null;
                    _currentTargetDamageable = null;
                    _damageableCachedFor = null;
                }
                else
                {
                    _noEnemyTimer = 0f;
                }
            }
            
            // PRIORIDAD 3: Si no hay target, buscar uno nuevo (con cooldown)
            if (_currentTarget == null)
            {
                _searchCooldown -= Time.deltaTime;
                
                if (_searchCooldown <= 0f)
                {
                    _searchCooldown = 0.3f; // Buscar más frecuentemente (cada 0.3 segundos)
                    
                    // Primero intentar del Registry
                    ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatNpcBuffer);
                    if (_combatNpcBuffer.Count > 0)
                    {
                        foreach (var npc in _combatNpcBuffer)
                        {
                            if (npc == null || npc == context.Transform.gameObject || !npc.activeInHierarchy) continue;
                            if (!IsTargetAlive(npc.transform)) continue;

                            _currentTarget = npc.transform;
                            _forcedTarget = npc.transform;
                            _forcedTargetName = npc.name;
                            if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] 🎯 Target encontrado en Registry: {npc.name}");
                            break;
                        }
                    }
                    
                    // Si no encontramos en registry, buscar por tag/layer
                    if (_currentTarget == null)
                    {
                        FindNearestEnemy(context);
                    }
                }
                
                // Incrementar timer de no-enemigo
                if (_currentTarget == null)
                {
                    _noEnemyTimer += Time.deltaTime;

                    if (context.DebugMode && (int)_noEnemyTimer != (int)(_noEnemyTimer - Time.deltaTime) && _noEnemyTimer > 1f)
                        Debug.Log($"[AllyCombatState:{context.Transform.name}] ⏳ Sin enemigos por {_noEnemyTimer:F1}s (timeout: {NO_ENEMY_TIMEOUT}s)");
                    
                    UpdateMovementAnimation(context);
                    return;
                }
            }
            
            // ¡Tenemos target! Resetear timer y combatir
            _noEnemyTimer = 0f;

            // ===== ESCUDO PERIÓDICO =====
            if (_shieldController != null && !_shieldController.IsDefending && Time.time >= _nextShieldTime && !_isCasting)
            {
                _shieldController.StartDefending();
                _nextShieldTime = Time.time + Random.Range(6f, 12f);
            }

            // ===== SISTEMA DE CASTING CON DELAY =====
            if (_isCasting)
            {
                _castTimer -= Time.deltaTime;
                
                // Seguir mirando al enemigo mientras castea
                RotateTowardsTarget(context);
                StopMovement(context);
                
                if (_castTimer <= 0f)
                {
                    // ¡Lanzar el hechizo!
                    ExecuteSpellLaunch(context, _pendingSpell);
                    _isCasting = false;
                    _pendingSpell = null;
                }
                
                UpdateMovementAnimation(context);
                return;
            }
            
            // Calcular distancias
            float distanceToEnemy = Vector3.Distance(context.Transform.position, _currentTarget.position);
            float distanceToPlayer = _player != null ? Vector3.Distance(context.Transform.position, _player.position) : 0f;
            
            // ===== MOVIMIENTO TÁCTICO MEJORADO =====
            _repositionTimer += Time.deltaTime;
            _behaviorTimer += Time.deltaTime;
            
            // Actualizar comportamiento
            UpdateCombatBehavior(context, distanceToEnemy, distanceToPlayer);
            
            // Ejecutar comportamiento actual
            switch (_currentBehavior)
            {
                case CombatBehavior.Approaching:
                    MoveTowardsTarget(context);
                    RotateTowardsTarget(context);
                    // Intentar atacar mientras se acerca si está en rango
                    if (distanceToEnemy <= MAX_COMBAT_DISTANCE)
                        TryAttack(context);
                    break;
                    
                case CombatBehavior.Strafing:
                    if (_repositionTimer >= REPOSITION_INTERVAL)
                    {
                        _repositionTimer = 0f;
                        CalculateTacticalPosition(context);
                    }
                    MoveToTacticalPosition(context);
                    RotateTowardsTarget(context);
                    TryAttack(context);
                    break;
                    
                case CombatBehavior.Retreating:
                    MoveAwayFromTarget(context);
                    RotateTowardsTarget(context);
                    TryAttack(context);
                    break;
                    
                case CombatBehavior.FollowingPlayer:
                    MoveTowardsPlayer(context);
                    RotateTowardsTarget(context);
                    break;
                    
                case CombatBehavior.Idle:
                default:
                    StopMovement(context);
                    RotateTowardsTarget(context);
                    TryAttack(context);
                    break;
            }
            
            UpdateMovementAnimation(context);
        }
        
        /// <summary>
        /// Actualiza el comportamiento de combate basado en distancias y tiempo
        /// </summary>
        private void UpdateCombatBehavior(NPCStateContext context, float distanceToEnemy, float distanceToPlayer)
        {
            // PRIORIDAD 1: Si el player está muy lejos, seguirlo
            if (_player != null && distanceToPlayer > MAX_PLAYER_DISTANCE)
            {
                if (_currentBehavior != CombatBehavior.FollowingPlayer)
                {
                    _currentBehavior = CombatBehavior.FollowingPlayer;
                    _behaviorTimer = 0f;
                }
                return;
            }
            
            // PRIORIDAD 2: Si está muy lejos del enemigo, acercarse
            if (distanceToEnemy > MAX_COMBAT_DISTANCE * 1.5f)
            {
                if (_currentBehavior != CombatBehavior.Approaching)
                {
                    _currentBehavior = CombatBehavior.Approaching;
                    _behaviorTimer = 0f;
                }
                return;
            }
            
            // PRIORIDAD 3: Si está muy cerca del enemigo, retroceder
            if (distanceToEnemy < MIN_COMBAT_DISTANCE)
            {
                if (_currentBehavior != CombatBehavior.Retreating)
                {
                    _currentBehavior = CombatBehavior.Retreating;
                    _behaviorTimer = 0f;
                }
                return;
            }
            
            // En rango óptimo - alternar entre strafing e idle de forma natural
            float behaviorDuration = _currentBehavior switch
            {
                CombatBehavior.Strafing => Random.Range(2f, 4f),
                CombatBehavior.Idle => Random.Range(1f, 2f),
                _ => 2f
            };
            
            // Cambiar comportamiento cada cierto tiempo para ser más dinámico
            if (_behaviorTimer > behaviorDuration || _currentBehavior == CombatBehavior.FollowingPlayer)
            {
                _behaviorTimer = 0f;
                
                // Probabilidad de cada comportamiento
                float rand = Random.value;
                if (rand < 0.5f)
                {
                    // 50% - Hacer strafe (más movimiento)
                    _currentBehavior = CombatBehavior.Strafing;
                    CalculateTacticalPosition(context);
                    _repositionTimer = 0f;
                }
                else if (rand < 0.8f)
                {
                    // 30% - Acercarse un poco
                    _currentBehavior = CombatBehavior.Approaching;
                }
                else
                {
                    // 20% - Quedarse quieto atacando
                    _currentBehavior = CombatBehavior.Idle;
                }
            }
        }
        
        /// <summary>
        /// Moverse hacia el jugador (cuando está muy lejos)
        /// </summary>
        private void MoveTowardsPlayer(NPCStateContext context)
        {
            if (_player == null) return;
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;
            
            context.Agent.isStopped = false;
            context.Agent.speed = context.Config?.runSpeed ?? 6f;
            context.Agent.SetDestination(_player.position);
        }

        public override void OnExit(NPCStateContext context)
        {
            context.IsInCombat = false;
            context.Animator?.SetBattleMode(false);

            if (_exitingAsVictory && !context.IsInCinematic)
                context.Animator?.PlayVictoryCelebration(UnityEngine.Random.Range(0f, 0.8f));

            _currentTarget = null;
            _forcedTarget = null;
            _currentTargetDamageable = null;
            _damageableCachedFor = null;

            _isCasting = false;
            _pendingSpell = null;
            _castTimer = 0f;

            _shieldController?.StopDefending();
            _shieldController = null;

            StopMovement(context);
            if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] 🏳️ Saliendo de combate");
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Cinemática
            if (context.IsInCinematic) return new CinematicState();
            
            // 2. Si no hay enemigos por mucho tiempo, volver a seguir (victoria)
            if (_noEnemyTimer > NO_ENEMY_TIMEOUT)
            {
                if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] ⏰ Timeout sin enemigos, volviendo a seguir al jugador.");
                _exitingAsVictory = true;
                return new FollowPlayerState(_partyMember);
            }

            // 3. Si ya no está en combate (forzado externamente, también es victoria)
            if (!context.IsInCombat)
            {
                _exitingAsVictory = true;
                return new FollowPlayerState(_partyMember);
            }
            
            return null;
        }

        #region Private Methods
        
        /// <summary>
        /// Busca el enemigo más cercano usando MÚLTIPLES métodos:
        /// 1. ActiveCombatRegistry (NPCs actualmente en combate)
        /// 2. Tag "Enemy"
        /// 3. Layer "Enemy" o "Boss"
        /// </summary>
        private void FindNearestEnemy(NPCStateContext context)
        {
            _currentTarget = null;
            float closestDistance = float.MaxValue;

            // MÉTODO 1: ActiveCombatRegistry
            ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatNpcBuffer);
            if (_combatNpcBuffer.Count > 0)
            {
                foreach (var npc in _combatNpcBuffer)
                {
                    if (npc == null || npc == context.Transform.gameObject) continue;
                    if (!IsTargetAlive(npc.transform)) continue;

                    float dist = Vector3.Distance(context.Transform.position, npc.transform.position);
                    if (dist < closestDistance && dist <= DETECTION_RANGE)
                    {
                        closestDistance = dist;
                        _currentTarget = npc.transform;
                    }
                }
            }

            if (_currentTarget != null)
            {
                if (context.DebugMode) Debug.Log($"[AllyCombatState:{context.Transform.name}] ✅ Enemigo via Registry: {_currentTarget.name}");
                return;
            }

            // MÉTODO 2 (Fallback): Layer "Enemy" / "Boss"
            int hitCount = Physics.OverlapSphereNonAlloc(context.Transform.position, DETECTION_RANGE, _physicsBuffer, EnemyBossMask);

            for (int i = 0; i < hitCount; i++)
            {
                var col = _physicsBuffer[i];
                if (col == null || !IsTargetAlive(col.transform)) continue;

                float dist = Vector3.Distance(context.Transform.position, col.transform.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    _currentTarget = col.transform;
                }
            }

            if (context.DebugMode)
            {
                if (_currentTarget != null) Debug.Log($"[AllyCombatState:{context.Transform.name}] ✅ Enemigo via Layer: {_currentTarget.name}");
                else Debug.Log($"[AllyCombatState:{context.Transform.name}] ❌ Sin enemigos (Registry:{_combatNpcBuffer.Count}, Layers:{hitCount})");
            }
        }
        
        /// <summary>
        /// Intenta re-encontrar un target por su nombre (útil si se perdió la referencia)
        /// </summary>
        private Transform TryFindTargetByName(string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return null;
            
            // Primero buscar en el registry
            ActiveCombatRegistry.GetAllInCombatNonAlloc(_combatNpcBuffer);
            foreach (var npc in _combatNpcBuffer)
            {
                if (npc != null && npc.name == targetName && npc.activeInHierarchy)
                {
                    return npc.transform;
                }
            }

            // Buscar en toda la escena
            var found = GameObject.Find(targetName);
            if (found != null && found.activeInHierarchy && IsTargetAlive(found.transform))
            {
                return found.transform;
            }
            
            return null;
        }
        
        private bool IsTargetAlive(Transform target)
        {
            if (target == null) return false;
            if (target.gameObject == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;
            
            var damageable = target.GetComponent<Damageable>();
            if (damageable != null && damageable.Current <= 0)
            {
                return false;
            }
            
            return true;
        }
        
        private bool IsTargetValid(Transform target)
        {
            if (target == null) return false;
            if (target.gameObject == null) return false;
            if (!target.gameObject.activeInHierarchy) return false;
            
            // Verificar que siga vivo
            if (!IsTargetAlive(target)) return false;
            
            // ✅ REMOVIDO: Ya no verificamos distancia al jugador porque en batallas de boss
            // el jugador puede estar lejos y queremos que los compañeros sigan atacando
            
            return true;
        }
        
        private void MoveTowardsTarget(NPCStateContext context)
        {
            if (_currentTarget == null) return;
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;
            
            context.Agent.isStopped = false;
            context.Agent.speed = context.Config?.runSpeed ?? 5f;
            context.Agent.SetDestination(_currentTarget.position);
        }
        
        private void RotateTowardsTarget(NPCStateContext context)
        {
            if (_currentTarget == null) return;
            
            Vector3 direction = (_currentTarget.position - context.Transform.position).normalized;
            direction.y = 0;
            
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                context.Transform.rotation = Quaternion.RotateTowards(
                    context.Transform.rotation,
                    targetRotation,
                    360f * Time.deltaTime
                );
            }
        }
        
        private void TryAttack(NPCStateContext context)
        {
            // Si ya estamos casteando, no iniciar otro ataque
            if (_isCasting) return;
            
            // Verificar cooldown
            if (Time.time < _lastAttackTime + _attackCooldown) return;
            
            // Verificar que está mirando al enemigo
            if (!IsFacingTarget(context)) return;
            
            // Verificar que el target sigue válido
            if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy) return;
            
            if (_partyMember == null)
            {
                Debug.LogWarning($"[AllyCombatState:{context.Transform.name}] ⚠️ No hay NPCPartyMember!");
                return;
            }

            // Rotar entre los 3 hechizos disponibles (runtime overrides o PartyConfig)
            MagicSpellSO spell = null;
            int attempts = 0;
            int selectedIndex = _currentSpellIndex;
            while (spell == null && attempts < 3)
            {
                spell = _partyMember.GetEffectiveSpell(_currentSpellIndex);
                if (spell == null)
                {
                    _currentSpellIndex = (_currentSpellIndex + 1) % 3;
                    attempts++;
                }
                else
                {
                    selectedIndex = _currentSpellIndex;
                }
            }
            
            if (spell == null)
            {
                Debug.LogWarning($"[AllyCombatState:{context.Transform.name}] ⚠️ No hay ningún hechizo configurado en PartyConfig!");
                return;
            }
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AllyCombatState:{context.Transform.name}] 📝 Hechizo seleccionado: [{selectedIndex}] {spell.name}");
#endif
            
            // Rotar al siguiente hechizo para el próximo ataque
            _currentSpellIndex = (_currentSpellIndex + 1) % 3;
            
            // Iniciar casting con delay
            StartCasting(context, spell);
        }
        
        private bool IsFacingTarget(NPCStateContext context)
        {
            if (_currentTarget == null) return false;
            
            Vector3 dirToTarget = (_currentTarget.position - context.Transform.position).normalized;
            float angle = Vector3.Angle(context.Transform.forward, dirToTarget);
            return angle < 45f; // 45 grados para ser más permisivo
        }
        
        /// <summary>
        /// Inicia el casting de un hechizo con delay (para sincronizar con animación)
        /// </summary>
        private void StartCasting(NPCStateContext context, MagicSpellSO spell)
        {
            _pendingSpell = spell;
            _isCasting = true;
            
            // Usar el castDelay del hechizo (como hace el player)
            _castTimer = spell.castDelaySeconds > 0 ? spell.castDelaySeconds : 0.15f;
            
            // Actualizar cooldown basado en el hechizo
            _attackCooldown = spell.cooldown > 0 ? spell.cooldown + 0.5f : DEFAULT_ATTACK_COOLDOWN;
            
            // Reproducir animación de casting ANTES de lanzar
            context.Animator?.PlaySpellCast();
            
            // Reproducir SFX de cast si existe
            if (!string.IsNullOrEmpty(spell.castSFXKey))
            {
                AudioService.Instance?.PlaySFX(spell.castSFXKey, worldPosition: context.Transform.position);
            }
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AllyCombatState:{context.Transform.name}] 🔮 Iniciando cast de {spell.name} " +
                      $"(delay:{_castTimer:F2}s, cd:{_attackCooldown:F1}s, speed:{spell.initialSpeed}, dmg:{spell.damage})");
#endif
        }
        
        /// <summary>
        /// Ejecuta el lanzamiento del hechizo después del delay de casting
        /// </summary>
        private void ExecuteSpellLaunch(NPCStateContext context, MagicSpellSO spell)
        {
            if (spell == null || spell.prefab == null)
            {
                Debug.LogWarning($"[AllyCombatState:{context.Transform.name}] ⚠️ Spell o prefab es null!");
                return;
            }
            
            // Verificar que aún tenemos target
            if (_currentTarget == null || !_currentTarget.gameObject.activeInHierarchy)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AllyCombatState:{context.Transform.name}] ⚠️ Target perdido durante casting, cancelando...");
#endif
                return;
            }
            
            // Obtener punto de spawn (mano o centro)
            Vector3 spawnPos = context.Transform.position + Vector3.up * 1.2f + context.Transform.forward * spell.forwardOffset;
            
            // Aplicar offset del spell si existe
            spawnPos += context.Transform.TransformDirection(spell.positionOffset);
            
            // Dirección hacia el enemigo (apuntar al centro del cuerpo)
            Vector3 targetPos = _currentTarget.position + Vector3.up * 1f;
            Vector3 direction = (targetPos - spawnPos).normalized;
            
            // Aplanar dirección si el spell lo requiere
            if (spell.flattenDirection)
            {
                direction.y = 0;
                direction.Normalize();
                if (direction.sqrMagnitude < 0.01f)
                {
                    direction = context.Transform.forward;
                }
            }
            
            // Spawn VFX si existe
            if (spell.spawnVFX != null)
            {
                var vfx = Object.Instantiate(spell.spawnVFX, spawnPos, Quaternion.LookRotation(direction));
                if (spell.vfxLifetime > 0)
                    Object.Destroy(vfx, spell.vfxLifetime);
            }
            
            // Instanciar proyectil
            GameObject projectile = Object.Instantiate(spell.prefab, spawnPos, Quaternion.LookRotation(direction));
            
            // Aplicar escala si está configurado
            if (spell.useScaleOverride)
            {
                projectile.transform.localScale = spell.scaleOverride;
            }
            
            // Asignar layer Projectile para colisionar con enemigos
            int projectileLayer = LayerMask.NameToLayer("Projectile");
            if (projectileLayer != -1)
            {
                projectile.layer = projectileLayer;
                foreach (Transform child in projectile.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = projectileLayer;
                }
            }
            
            // Configurar proyectil
            var magicProj = projectile.GetComponent<MagicProjectile>();
            if (magicProj != null)
            {
                // Crear config completa para el proyectil (incluyendo VFX!)
                var config = new MagicProjectile.ProjectileConfig
                {
                    damage = spell.damage,
                    aoeRadius = spell.aoeRadius,
                    knockbackForce = spell.knockbackForce,
                    hitLayers = EnemyBossMask,
                    collisionLayers = EnemyBossDefaultMask,
                    destroyOnHit = spell.destroyOnHit,
                    lifeTime = spell.lifeTime,
                    maxRange = spell.maxRange,
                    initialSpeed = spell.initialSpeed,
                    useGravity = spell.useGravity,
                    element = spell.element,
                    // ✅ IMPORTANTE: Pasar los VFX del spell!
                    impactVFX = spell.impactVFX,
                    despawnVFX = spell.despawnVFX,
                    vfxLifetime = spell.vfxLifetime,
                    impactSFXKey = spell.impactSFXKey
                };
                
                magicProj.Configure(config, context.Transform.gameObject);
                magicProj.Launch(direction, spell.initialSpeed, spell.useGravity);
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AllyCombatState:{context.Transform.name}] 🔥 Lanzando {spell.name} hacia {_currentTarget.name} " +
                          $"(dmg:{spell.damage}, speed:{spell.initialSpeed}, lifeTime:{spell.lifeTime}, maxRange:{spell.maxRange})");
#endif
            }
            else
            {
                Debug.LogWarning($"[AllyCombatState:{context.Transform.name}] ⚠️ Proyectil no tiene MagicProjectile component!");
            }
            
            _lastAttackTime = Time.time;
        }
        
        #region Movimiento Táctico
        
        /// <summary>
        /// Calcula una posición táctica para evadir y atacar desde diferentes ángulos
        /// </summary>
        private void CalculateTacticalPosition(NPCStateContext context)
        {
            if (_currentTarget == null) return;
            
            Vector3 dirToTarget = (_currentTarget.position - context.Transform.position).normalized;
            
            // Elegir lado aleatorio para strafe
            float strafeAngle = Random.value > 0.5f ? 90f : -90f;
            strafeAngle += Random.Range(-30f, 30f); // Añadir variación
            
            Vector3 strafeDir = Quaternion.Euler(0, strafeAngle, 0) * dirToTarget;
            
            // Distancia ideal al enemigo
            float idealDistance = (MIN_COMBAT_DISTANCE + MAX_COMBAT_DISTANCE) / 2f;
            
            // Calcular posición base a distancia ideal
            Vector3 basePos = _currentTarget.position - dirToTarget * idealDistance;
            
            // Añadir strafe
            _tacticalPosition = basePos + strafeDir * Random.Range(1f, STRAFE_RADIUS);
            _tacticalPosition.y = context.Transform.position.y; // Mantener altura
        }
        
        /// <summary>
        /// Moverse hacia la posición táctica calculada
        /// </summary>
        private void MoveToTacticalPosition(NPCStateContext context)
        {
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;
            
            context.Agent.isStopped = false;
            context.Agent.speed = context.Config?.walkSpeed ?? 3.5f; // Caminar al moverse tácticamente
            context.Agent.SetDestination(_tacticalPosition);
        }
        
        /// <summary>
        /// Alejarse del enemigo cuando está demasiado cerca
        /// </summary>
        private void MoveAwayFromTarget(NPCStateContext context)
        {
            if (_currentTarget == null) return;
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;
            
            Vector3 awayDir = (context.Transform.position - _currentTarget.position).normalized;
            Vector3 retreatPos = context.Transform.position + awayDir * 5f;
            
            context.Agent.isStopped = false;
            context.Agent.speed = context.Config?.runSpeed ?? 5f;
            context.Agent.SetDestination(retreatPos);
        }
        
        #endregion
        
        // Mantener LaunchSpell para compatibilidad pero redirigir a ExecuteSpellLaunch
        private void LaunchSpell(NPCStateContext context, MagicSpellSO spell)
        {
            ExecuteSpellLaunch(context, spell);
        }
        
        #endregion
    }
}

