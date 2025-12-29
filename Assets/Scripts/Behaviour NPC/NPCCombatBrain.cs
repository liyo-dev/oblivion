using System;
using System.Collections;
using System.Collections.Generic;
using Game.NPC.Common;
using Unity.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC
{
    /// <summary>
    /// Cerebro de Combate Táctico con FSM (Evaluate -> Reposition -> Attack -> Defense).
    /// Soporta Cobertura, Escudos y Dificultad Dinámica.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    public sealed class NPCCombatBrain : MonoBehaviour
    {
        #region ⚙️ Configuration
        [Serializable]
        public struct AttackSlot
        {
            public string animationState;
            public float cooldown;
            public int slotIndex; // 0=left, 1=right, 2=special
        }

        [Serializable]
        public struct Settings
        {
            [Header("Distances & Movement")]
            public float minSafeDistance;       // Si player está más cerca, huye
            public float optimalDistance;       // Distancia ideal de combate
            public float maxDistance;           // Si player está más lejos, se acerca
            public float runSpeed;              // Velocidad al recolocarse
            public float walkSpeed;             // Velocidad al acercarse
            
            [Header("Attacks")]
            public int upperBodyLayer;
            public AttackSlot leftAttack;
            public AttackSlot rightAttack;
            public AttackSlot specialAttack;
            public float attackFrequencyMultiplier; // 1 = normal, 2 = doble de rápido
            public float globalCooldown;            // Pausa entre ataques
            public bool spawnProjectileViaAnimEvent;
            public float fireDelaySeconds;          // Si no usa anim events

            [Header("Defense & Tactics")]
            [Range(0f, 1f)] public float difficultyLevel; // 0=Torpe, 1=Experto
            public bool useShield;
            public float shieldDuration;
            public float shieldCooldown;
            public LayerMask coverLayerMask;    // Capa de objetos que sirven de cobertura (Arboles, Cajas)
            public float coverSearchRadius;     // Qué tan lejos busca cobertura
            public float dodgeDistance;         // Distancia de salto lateral
            
            [Header("Line of Sight & Searching")]
            public LayerMask obstacleLayerMask; // Capas que bloquean la visión (Default, etc.)
            public float searchDuration;        // Tiempo que busca al jugador antes de rendirse
            public float searchMovementRadius;  // Radio de movimiento durante búsqueda
            public bool returnToOriginAfterSearch; // Si vuelve al origen después de buscar
            
            [Header("Search Behavior")]
            [Tooltip("Si está activado, el NPC se mueve activamente buscando al jugador. Si no, se queda quieto mostrando interrogación")]
            public bool activelySearchForPlayer; // ¿Busca activamente o se rinde?
            [Tooltip("Si está desactivado 'activelySearchForPlayer', cuánto tiempo espera antes de abandonar")]
            public float passiveSearchDuration; // Tiempo esperando antes de abandonar (si no busca activamente)
        }
        #endregion

        #region 🔌 Dependencies & State
        public Settings settings; // Visible en inspector

        NPCBehaviourManagerV2 _manager;
        NPCStateContext _ctx;
        NavMeshAgent _agent;
        NPCSimpleAnimator _animator;
        Animator _rawAnimator;
        Transform _player;
        NPCShieldController _shieldController;
        NPCAlertIconController _alertIconController; // Sistema de iconos visuales (usa prefabs)
        // FSM State
        public enum CombatState { EVALUATE, REPOSITION, ATTACK, DEFENSE, SEARCHING }
        [SerializeField, ReadOnly] private CombatState _currentState; // Visible debug

        // Cooldowns
        float _leftCd, _rightCd, _specialCd, _shieldCd, _globalCd;
        
        // Line of Sight & Searching
        bool _hasLineOfSight;
        float _lastSeenTime;
        Vector3 _lastKnownPlayerPosition;
        Vector3 _combatStartPosition; // Posición original para volver
        
        // 🔥 Memoria de combate reciente para evitar interrogación innecesaria
        private const float RECENT_COMBAT_THRESHOLD = 5f; // Si estuvo en combate hace menos de 5s, NO mostrar interrogación
        private float _lastCombatTime; // Timestamp del último momento en combate activo
        private bool _wasInRecentCombat => (Time.time - _lastCombatTime) < RECENT_COMBAT_THRESHOLD;
        
        Coroutine _fsmRoutine;
        bool _isActive;
        #endregion

        // Referencia al config para acceder a prefabs de iconos
        private Modules.NPCCombatConfig _config;

        // Inicialización
        public void Initialize(NPCBehaviourManagerV2 manager)
        {
            _manager = manager;
            _ctx = manager.Context;
            _agent = _ctx.Agent;
            _animator = _ctx.Animator;
            _rawAnimator = _ctx.UnityAnimator;
            _shieldController = GetComponent<NPCShieldController>();
            
            // Buscar componente de iconos visuales si existe
            _alertIconController = _manager.GetComponent<NPCAlertIconController>();
            if (_alertIconController == null)
            {
                Debug.LogWarning($"[CombatBrain:{_manager.name}] ⚠️ NPCAlertIconController no encontrado - Los iconos visuales no se mostrarán");
            }

            // Configurar NavMesh para movimiento fluido
            _agent.updateRotation = false; // Controlamos la rotación manualmente para encarar al player
            _agent.updatePosition = true;
            _agent.acceleration = 12f;
        }

        public void BeginCombat(Settings newSettings, Modules.NPCCombatConfig config = null)
        {
            settings = newSettings;
            _config = config; // Guardar referencia al config
            
            if (_fsmRoutine != null) StopCoroutine(_fsmRoutine);
            
            // Buscar player si no existe
            if (_ctx.Player == null) 
                 _ctx.Player = GameObject.FindWithTag("Player").transform;
            _player = _ctx.Player;

            // Guardar posición inicial para poder volver
            _combatStartPosition = transform.position;
            _lastKnownPlayerPosition = _player.position;
            _lastSeenTime = Time.time;
            _hasLineOfSight = true;

            // ✅ Mostrar icono de admiración - ¡Te vi!
            if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
            {
                _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
            }

            _animator.SetBattleMode(true);
            _isActive = true;
            
            _fsmRoutine = StartCoroutine(FSM_Loop());
        }

        public void StopCombat()
        {
            _isActive = false;
            if (_fsmRoutine != null) StopCoroutine(_fsmRoutine);
            _agent.isStopped = true;
            _animator.SetBattleMode(false);
        }
        
        /// <summary>
        /// Llamado cuando el NPC recibe daño. Si está buscando, activa alerta inmediata.
        /// </summary>
        public void OnTakeDamage(Vector3 damageSourcePosition)
        {
            if (!_isActive) return;
            
            // Si está en modo SEARCHING (buscando al jugador) y recibe daño
            if (_currentState == CombatState.SEARCHING)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ ¡ATACADO POR LA ESPALDA! - Alertando inmediatamente");
                
                // Actualizar última posición conocida
                _lastKnownPlayerPosition = damageSourcePosition;
                _lastSeenTime = Time.time;
                
                // Girar hacia la fuente del daño
                Vector3 directionToDamage = (damageSourcePosition - transform.position).normalized;
                directionToDamage.y = 0;
                if (directionToDamage.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(directionToDamage);
                }
                
                // Mostrar icono de alerta inmediatamente
                if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                {
                    _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                }
                
                // Salir del estado de búsqueda y evaluar situación
                StopAllCoroutines();
                _currentState = CombatState.EVALUATE;
                _fsmRoutine = StartCoroutine(FSM_Loop());
            }
        }

        private void Update()
        {
            if (!_isActive) return;

            // Reducir Cooldowns
            float dt = Time.deltaTime * settings.attackFrequencyMultiplier;
            if (_leftCd > 0) _leftCd -= dt;
            if (_rightCd > 0) _rightCd -= dt;
            if (_specialCd > 0) _specialCd -= dt;
            if (_shieldCd > 0) _shieldCd -= Time.deltaTime; // Escudo no se ve afectado por multiplicador
            if (_globalCd > 0) _globalCd -= dt;

            // ✅ Verificar Line of Sight cada frame
            if (_player != null)
            {
                _hasLineOfSight = CheckLineOfSight();
                
                if (_hasLineOfSight)
                {
                    _lastSeenTime = Time.time;
                    _lastCombatTime = Time.time; // 🔥 Actualizar tiempo de combate activo
                    _lastKnownPlayerPosition = _player.position;
                }
            }

            // ✅ Rotación gestionada por NPCSimpleAnimator
            // En ATTACK/DEFENSE: mirar al player (o última posición conocida)
            // En REPOSITION: mirar hacia donde se mueve (gestionado automáticamente por SyncWithNavMeshAgent)
            // En EVALUATE: mirar al player
            // En SEARCHING: animación maneja la rotación
            if (_player != null && _currentState != CombatState.REPOSITION && _currentState != CombatState.SEARCHING && _agent.isStopped)
            {
                // Solo rotar hacia el player cuando está parado (no en movimiento)
                Vector3 targetPos = _hasLineOfSight ? _player.position : _lastKnownPlayerPosition;
                _animator.FaceTarget(targetPos);
            }
        }

        // =================================================================================
        // 🧠 MÁQUINA DE ESTADOS FINITOS (FSM)
        // =================================================================================
        
        IEnumerator FSM_Loop()
        {
            while (_isActive && _player != null)
            {
                switch (_currentState)
                {
                    case CombatState.EVALUATE:
                        yield return State_Evaluate();
                        break;
                    
                    case CombatState.REPOSITION:
                        yield return State_Reposition();
                        break;
                    
                    case CombatState.ATTACK:
                        yield return State_Attack();
                        break;
                    
                    case CombatState.DEFENSE:
                        yield return State_Defense();
                        break;
                    
                    case CombatState.SEARCHING:
                        yield return State_Searching();
                        break;
                }
                yield return null;
            }
        }

        // 1. EVALUAR: El cerebro que decide qué hacer
        IEnumerator State_Evaluate()
        {
            // ✅ A. PRIORIDAD MÁXIMA: Si no veo al jugador -> BUSCAR
            if (!_hasLineOfSight)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Sin línea de visión al jugador - Iniciando búsqueda");
                _currentState = CombatState.SEARCHING;
                yield break;
            }
            
            float dist = Vector3.Distance(transform.position, _player.position);

            // B. Si está demasiado cerca -> HUIR (Reposicionarse)
            if (dist < settings.minSafeDistance)
            {
                _currentState = CombatState.REPOSITION;
                yield break;
            }

            // C. Si debería considerar defenderse (estrategia táctica) -> DEFENDERSE
            if (ShouldConsiderDefense())
            {
                _currentState = CombatState.DEFENSE;
                yield break;
            }

            // D. Si tengo ataques disponibles y estoy en rango -> ATACAR
            if (HasAnyAttackReady() && dist <= settings.maxDistance && _globalCd <= 0)
            {
                _currentState = CombatState.ATTACK;
                yield break;
            }

            // E. Si estoy muy lejos -> Acercarse (Usamos Reposition para acercarnos también)
            if (dist > settings.maxDistance)
            {
                 MoveTo(_player.position, settings.walkSpeed);
                 yield return new WaitForSeconds(0.5f); // Caminar un poco
            }

            yield return null; 
        }

        // 2. REPOSICIONARSE: Moverse a un lugar seguro o acercarse
        IEnumerator State_Reposition()
        {
            float dist = Vector3.Distance(transform.position, _player.position);
            Vector3 targetPos = transform.position;

            if (dist < settings.minSafeDistance)
            {
                // ✅ NUEVO: Buscar cobertura detrás de objetos Default
                Vector3 coverPosition;
                bool foundCover = FindCoverBehindObstacle(out coverPosition);
                
                if (foundCover)
                {
                    // Encontró cobertura detrás de un obstáculo
                    targetPos = coverPosition;
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🏃 Huyendo hacia cobertura detrás de obstáculo: {targetPos}");
                }
                else
                {
                    // No encontró cobertura, huir en dirección opuesta como antes
                    Vector3 dirAway = (transform.position - _player.position).normalized;
                    targetPos = transform.position + dirAway * 5f;
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🏃 Huyendo sin cobertura - dirección opuesta al jugador");
                }
                
                // ✅ MoveTo iniciará el movimiento y NPCSimpleAnimator.SyncWithNavMeshAgent()
                // rotará automáticamente hacia la dirección de movimiento (navAgent.velocity)
                MoveTo(targetPos, settings.runSpeed);
                
                // Esperar hasta llegar o 3 segundos máx
                float timer = 0;
                while (_agent.remainingDistance > 1.5f && timer < 3f)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
                
                // ✅ Al detenerse después de huir, SIEMPRE reproducir animación de búsqueda
                StopMove();
                Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 Llegó a posición de cobertura - Reproduciendo animación de búsqueda");
                
                // ✅ Mostrar icono de interrogación
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
                
                // Esperar tiempo de la animación de búsqueda
                yield return new WaitForSeconds(1.5f);
                
                // ✅ VERIFICAR SI PERDIÓ DE VISTA AL JUGADOR
                if (!_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Perdió visión del jugador tras llegar a cobertura - ENTRANDO EN BÚSQUEDA");
                    _currentState = CombatState.SEARCHING;
                    yield break;
                }
            }
            
            // Volver a evaluar al terminar movimiento (si aún lo ve)
            _currentState = CombatState.EVALUATE;
        }

        // 3. ATACAR: Seleccionar hechizo y disparar
        IEnumerator State_Attack()
        {
            // ✅ Verificar que tengamos línea de visión antes de atacar
            if (!_hasLineOfSight)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Ataque cancelado - Sin línea de visión");
                _currentState = CombatState.SEARCHING;
                yield break;
            }
            
            StopMove(); // Quieto para disparar
            _animator.FaceTarget(_player.position);

            // Seleccionar ataque disponible (Prioridad: Special > Right > Left)
            AttackSlot chosenAttack = new AttackSlot();
            bool found = false;

            if (_specialCd <= 0 && UnityEngine.Random.value > 0.4f) // 60% chance de special
            {
                chosenAttack = settings.specialAttack; _specialCd = chosenAttack.cooldown; found = true;
            }
            else if (_rightCd <= 0)
            {
                chosenAttack = settings.rightAttack; _rightCd = chosenAttack.cooldown; found = true;
            }
            else if (_leftCd <= 0)
            {
                chosenAttack = settings.leftAttack; _leftCd = chosenAttack.cooldown; found = true;
            }

            if (found)
            {
                // Windup (preparación)
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.2f, 0.5f));
                
                // ✅ Verificar visión de nuevo antes de ejecutar el ataque
                if (!_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Ataque cancelado durante windup - Perdida línea de visión");
                    _currentState = CombatState.SEARCHING;
                    yield break;
                }
                
                // Ejecutar Animación
                _rawAnimator.Play(chosenAttack.animationState, settings.upperBodyLayer);
                
                // Disparar Proyectil (Si no es por evento de animación)
                if (!settings.spawnProjectileViaAnimEvent)
                {
                    yield return new WaitForSeconds(settings.fireDelaySeconds);
                    
                    // ✅ Verificar visión ANTES de disparar
                    if (!_hasLineOfSight)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Disparo cancelado - Jugador se escondió durante animación");
                        _currentState = CombatState.SEARCHING;
                        yield break;
                    }
                    
                    SpawnProjectile(chosenAttack.slotIndex);
                }

                // Pausa post-ataque (Global Cooldown)
                _globalCd = settings.globalCooldown;
                yield return new WaitForSeconds(0.5f);
                
                // ✅ Verificar visión DESPUÉS del ataque
                if (!_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ❌ Jugador se escondió después del ataque - Iniciando búsqueda");
                    _currentState = CombatState.SEARCHING;
                    yield break;
                }

                // DECISIÓN TÁCTICA POST-ATAQUE
                // Si tengo maná (otros ataques listos) -> Seguir atacando
                if (HasAnyAttackReady())
                {
                    // 30% chance de moverse para flanquear, 70% de seguir disparando
                    if (UnityEngine.Random.value < 0.3f)
                    {
                        Vector3 flankPos = GetFlankPosition();
                        MoveTo(flankPos, settings.runSpeed);
                        yield return new WaitForSeconds(1f);
                    }
                    else
                    {
                        _currentState = CombatState.ATTACK; // Repetir ataque
                        yield break;
                    }
                }
            }

            _currentState = CombatState.EVALUATE;
        }

        // 4. DEFENSA: La lógica inteligente basada en dificultad
        IEnumerator State_Defense()
        {
            StopMove();

            // Factor de decisión basado en dificultad
            // Si Dificultad es 0.8, hay 80% de probabilidad de tomar la decisión "Experta"
            bool makeSmartDecision = UnityEngine.Random.value < settings.difficultyLevel;

            if (makeSmartDecision)
            {
                // === LÓGICA EXPERTA (Escudo o Cobertura) ===
                
                // A. Intentar usar ESCUDO si está disponible
                if (settings.useShield && _shieldController != null && _shieldCd <= 0)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Activando ESCUDO defensivo por {settings.shieldDuration:F1}s");
                    
                    _shieldController.StartDefending(settings.shieldDuration);
                    _shieldCd = settings.shieldCooldown + settings.shieldDuration;
                    
                    // 🔥 NUEVO: Mientras se defiende, puede moverse lentamente
                    // Esto permite estrategias como "defender y flanquear" o "retroceder con escudo"
                    float defendTime = settings.shieldDuration;
                    float elapsed = 0f;
                    
                    while (elapsed < defendTime)
                    {
                        // Si el jugador está muy cerca, retroceder lentamente con escudo
                        if (_player != null)
                        {
                            float dist = Vector3.Distance(transform.position, _player.position);
                            if (dist < settings.minSafeDistance * 0.7f)
                            {
                                Vector3 retreatDir = (transform.position - _player.position).normalized;
                                Vector3 retreatPos = transform.position + retreatDir * 2f;
                                MoveTo(retreatPos, settings.walkSpeed * 0.5f); // Retroceso lento
                                
                                Debug.Log($"[CombatBrain:{gameObject.name}] 🚶 Retrocediendo con escudo activo");
                            }
                        }
                        
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                    
                    Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Escudo completado - volviendo a evaluar");
                }
                // B. Si no hay escudo disponible, buscar COBERTURA
                else
                {
                    if (_shieldCd > 0)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⏳ Escudo en cooldown ({_shieldCd:F1}s) - buscando cobertura");
                    }
                    
                    Vector3 coverPos;
                    if (TryGetCoverPosition(out coverPos))
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🌳 Corriendo hacia cobertura para recargar");
                        MoveTo(coverPos, settings.runSpeed);
                        
                        // Esperar a llegar o timeout
                        float timeout = 3f;
                        while (_agent.remainingDistance > 0.5f && timeout > 0 && _agent.pathStatus == NavMeshPathStatus.PathComplete)
                        {
                            timeout -= Time.deltaTime;
                            yield return null;
                        }
                        
                        // Esperar tras la cobertura recuperando cooldowns
                        Debug.Log($"[CombatBrain:{gameObject.name}] ⏳ Esperando tras cobertura, recargando cooldowns...");
                        yield return new WaitForSeconds(2.0f);
                    }
                    else
                    {
                        // Si no hay cobertura, esquiva simple
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🤸 No hay cobertura - esquiva táctica");
                        yield return DoDodge();
                    }
                }
            }
            else
            {
                // === LÓGICA TORPE/BAJA DIFICULTAD ===
                Debug.Log($"[CombatBrain:{gameObject.name}] 😵 Defensa torpe (baja dificultad)");
                
                // Solo espera un poco o hace una esquiva tonta
                if (UnityEngine.Random.value > 0.5f)
                    yield return DoDodge();
                else
                    yield return new WaitForSeconds(1.0f); // Quedarse pasmado
            }

            _currentState = CombatState.EVALUATE;
        }

        // =================================================================================
        // 🔧 HELPER FUNCTIONS
        // =================================================================================

        private bool TryGetCoverPosition(out Vector3 position)
        {
            position = Vector3.zero;
            // Buscar objetos en el radio
            Collider[] hits = Physics.OverlapSphere(transform.position, settings.coverSearchRadius, settings.coverLayerMask);
            
            float bestDist = float.MaxValue;
            bool found = false;

            foreach (var hit in hits)
            {
                // Calcular punto opuesto al player detrás del objeto
                Vector3 dirFromPlayer = (hit.transform.position - _player.position).normalized;
                Vector3 coverSpot = hit.transform.position + (dirFromPlayer * 2.5f); // 2.5m detrás del objeto

                // Verificar si es accesible en NavMesh
                NavMeshHit navHit;
                if (NavMesh.SamplePosition(coverSpot, out navHit, 2.0f, NavMesh.AllAreas))
                {
                    float d = Vector3.Distance(transform.position, navHit.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        position = navHit.position;
                        found = true;
                    }
                }
            }
            return found;
        }

        private IEnumerator DoDodge()
        {
            // Esquiva lateral simple
            Vector3 side = UnityEngine.Random.value > 0.5f ? transform.right : -transform.right;
            Vector3 dest = transform.position + side * settings.dodgeDistance;
            
            MoveTo(dest, settings.runSpeed * 1.5f);
            yield return new WaitForSeconds(0.5f);
            StopMove();
        }

        private Vector3 GetFlankPosition()
        {
            // Moverse 45 grados a un lado
            Vector3 dir = (_player.position - transform.position).normalized;
            Vector3 flankDir = Quaternion.Euler(0, 45, 0) * dir;
            return transform.position + flankDir * 4f;
        }

        private void SpawnProjectile(int slotIndex)
        {
            // Aquí iría tu lógica de instanciar prefab
            // Usa _ctx.Config.combatConfig.GetSpellPrefab(slotIndex) como tenías antes
            Debug.Log($"[NPC] Disparando hechizo slot {slotIndex}");
            
            if (_ctx.Config?.combatConfig != null)
            {
                var prefab = _ctx.Config.combatConfig.GetSpellPrefab(slotIndex);
                if (prefab)
                {
                     Vector3 spawnPos = transform.position + Vector3.up * 1.5f + transform.forward;
                     var spell = Instantiate(prefab, spawnPos, Quaternion.LookRotation(_player.position - spawnPos));
                     if(spell.TryGetComponent<EnemyProjectile>(out var proj)) 
                         proj.Initialize((_player.position - spawnPos).normalized);
                }
            }
        }

        /// <summary>
        /// Evalúa si el NPC debería defenderse basado en su estado táctico
        /// </summary>
        private bool ShouldConsiderDefense()
        {
            // A. Contar cuántos ataques están disponibles
            int attacksReady = 0;
            if (_leftCd <= 0) attacksReady++;
            if (_rightCd <= 0) attacksReady++;
            if (_specialCd <= 0) attacksReady++;
            
            bool fewAttacksReady = attacksReady == 0; // 🔥 CAMBIO: Solo si NO tiene ataques disponibles
            
            // B. Si está en cooldown global (acaba de atacar) - MUY VULNERABLE
            bool inGlobalCooldown = _globalCd > 0;
            
            // C. Si todos los ataques importantes están en cooldown (>70% del cooldown total)
            bool mostAttacksOnCooldown = false;
            if (settings.leftAttack.cooldown > 0 && settings.rightAttack.cooldown > 0)
            {
                float leftProgress = _leftCd / settings.leftAttack.cooldown;
                float rightProgress = _rightCd / settings.rightAttack.cooldown;
                mostAttacksOnCooldown = (leftProgress > 0.7f && rightProgress > 0.7f);
            }
            
            // D. 🔥 NUEVO: Probabilidad basada en dificultad (más agresivo)
            // Dificultad alta = usa escudo más frecuentemente como ESTRATEGIA
            // Máximo 60% de chance si dificultad = 1.0 (antes era 40%)
            float defensiveChance = settings.difficultyLevel * 0.6f;
            bool randomDefensive = UnityEngine.Random.value < defensiveChance;
            
            // E. 🔥 NUEVO: Si el escudo está disponible, aumentar probabilidad
            bool shieldReady = settings.useShield && _shieldController != null && _shieldCd <= 0;
            if (shieldReady)
            {
                // Bonus +30% si el escudo está listo
                randomDefensive = UnityEngine.Random.value < (defensiveChance + 0.3f);
            }
            
            // Decidir si debe defenderse
            bool shouldDefend = fewAttacksReady || inGlobalCooldown || mostAttacksOnCooldown || randomDefensive;
            
            if (shouldDefend)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Considerando DEFENSA - Ataques:{attacksReady}, GlobalCD:{inGlobalCooldown}, MostOnCD:{mostAttacksOnCooldown}, Random:{randomDefensive}, ShieldReady:{shieldReady}");
            }
            
            return shouldDefend;
        }

        private bool HasAnyAttackReady()
        {
            return _leftCd <= 0 || _rightCd <= 0 || _specialCd <= 0;
        }

        private void MoveTo(Vector3 pos, float speed)
        {
            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.speed = speed;
                _agent.SetDestination(pos);
                _animator.SetMovementSpeed(speed, 0.1f);
            }
        }

        private void StopMove()
        {
            if (_agent.isOnNavMesh) _agent.isStopped = true;
            _animator.SetMovementSpeed(0, 0.1f);
        }
        
        /// <summary>
        /// Verifica si hay línea de visión directa al jugador (sin obstáculos).
        /// Usa raycast para detectar objetos en la capa de obstáculos.
        /// </summary>
        private bool CheckLineOfSight()
        {
            if (_player == null) return false;
            
            Vector3 origin = transform.position + Vector3.up * 1.5f; // Altura de los ojos
            Vector3 targetPos = _player.position + Vector3.up * 1.0f; // Centro del jugador
            Vector3 direction = targetPos - origin;
            float distance = direction.magnitude;
            
            // ✅ Raycast para detectar obstáculos (usa QueryTriggerInteraction.Ignore para no detectar triggers)
            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                // Verificar si el objeto golpeado ES el jugador
                if (hit.collider.CompareTag("Player"))
                {
                    // Línea de visión clara - golpeó directamente al jugador
                    Debug.DrawRay(origin, direction, Color.green);
                    return true;
                }
                
                // ✅ Golpeó otra cosa ANTES que al jugador - visión bloqueada
                Debug.DrawRay(origin, direction.normalized * hit.distance, Color.red);
                Debug.Log($"[CombatBrain:{gameObject.name}] 🚫 Visión bloqueada por: {hit.collider.gameObject.name} (Tag: {hit.collider.tag}, Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)})");
                return false;
            }
            
            // No golpeó nada - línea de visión clara (caso raro)
            Debug.DrawRay(origin, direction, Color.yellow);
            return true;
        }
        
        /// <summary>
        /// Verifica si el jugador está dentro del campo de visión del NPC.
        /// Usa el fieldOfView configurado en NPCCombatConfig.
        /// </summary>
        private bool IsPlayerInFieldOfView()
        {
            if (_player == null) return false;
            
            // Obtener fieldOfView del config (usar valor por defecto si no está disponible)
            float fov = 160f; // Valor por defecto
            if (_manager != null && _manager.Configuration != null && _manager.Configuration.combatConfig != null)
            {
                fov = _manager.Configuration.combatConfig.fieldOfView;
            }
            
            // Calcular dirección al jugador
            Vector3 dirToPlayer = (_player.position - transform.position).normalized;
            dirToPlayer.y = 0; // Ignorar diferencia de altura
            
            Vector3 forward = transform.forward;
            forward.y = 0;
            forward.Normalize();
            
            // Calcular ángulo entre la dirección del NPC y la dirección al jugador
            float angle = Vector3.Angle(forward, dirToPlayer);
            
            // El jugador está en el campo de visión si el ángulo es menor que la mitad del FOV
            bool inFOV = angle <= (fov / 2f);
            
            return inFOV;
        }
        
        // =================================================================================
        // 🔍 ESTADO DE BÚSQUEDA
        // =================================================================================
        
        /// <summary>
        /// Estado SEARCHING: El NPC ha perdido de vista al jugador y lo busca activamente.
        /// Muestra icono de interrogación y animación en CADA parada.
        /// </summary>
        IEnumerator State_Searching()
        {
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 INICIANDO BÚSQUEDA - Última posición conocida: {_lastKnownPlayerPosition}");
            
            StopMove();
            
            // 🔥 CORRECCIÓN: Solo mostrar interrogación si NO estábamos en combate reciente
            // Si el NPC simplemente se giró y perdió visión momentáneamente, NO mostrar interrogación
            bool showQuestionMark = !_wasInRecentCombat;
            
            if (showQuestionMark)
            {
                // ✅ Mostrar icono de interrogación INICIAL
                if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                {
                    _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                }
                
                // ✅ Reproducir animación de búsqueda INICIAL
                if (_animator != null)
                {
                    _animator.PlaySearching();
                }
                
                // ⏳ ESPERAR A QUE LA ANIMACIÓN DE BÚSQUEDA SE VEA (duración aprox de la animación)
                yield return new WaitForSeconds(1.5f);
            }
            else
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] 🎯 Combate reciente detectado ({Time.time - _lastSeenTime:F1}s) - Búsqueda sin interrogación");
            };
            
            float searchStartTime = Time.time;
            float searchTimeout = settings.activelySearchForPlayer ? settings.searchDuration : settings.passiveSearchDuration;
            
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 Modo: {(settings.activelySearchForPlayer ? "BÚSQUEDA ACTIVA" : "BÚSQUEDA PASIVA")} - Duración: {searchTimeout}s");
            
            int searchAttempts = 0;
            const int maxSearchAttempts = 5; // Número de veces que buscará en diferentes lugares
            
            // ✅ BUCLE DE BÚSQUEDA
            while (Time.time - searchStartTime < searchTimeout && searchAttempts < maxSearchAttempts)
            {
                // Verificar si encontramos al jugador
                if (_hasLineOfSight)
                {
                    Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡JUGADOR ENCONTRADO! - Mostrando alerta");
                    
                    // ✅ Ocultar interrogación y mostrar admiración
                    if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                    {
                        _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                    }
                    
                    // Esperar para que se vea el feedback
                    yield return new WaitForSeconds(1.0f);
                    
                    StopMove();
                    _currentState = CombatState.EVALUATE;
                    yield break;
                }
                
                // ✅ BÚSQUEDA ACTIVA: Moverse a diferentes puntos
                if (settings.activelySearchForPlayer)
                {
                    searchAttempts++;
                    
                    // Calcular punto de búsqueda (más cerca en las primeras búsquedas)
                    float searchRadius = settings.searchMovementRadius * (0.5f + searchAttempts * 0.3f);
                    Vector3 searchPoint = _lastKnownPlayerPosition + 
                                         new Vector3(
                                             UnityEngine.Random.Range(-searchRadius, searchRadius),
                                             0,
                                             UnityEngine.Random.Range(-searchRadius, searchRadius)
                                         );
                    
                    // Verificar que el punto esté en NavMesh
                    if (NavMesh.SamplePosition(searchPoint, out NavMeshHit navHit, searchRadius, NavMesh.AllAreas))
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] 👣 Movimiento de búsqueda #{searchAttempts} hacia: {navHit.position}");
                        MoveTo(navHit.position, settings.walkSpeed);
                        
                        // ✅ DURANTE EL MOVIMIENTO: Verificar constantemente
                        float moveTimeout = 5f;
                        float moveTimer = 0f;
                        while (_agent.pathPending || (_agent.remainingDistance > 1f && moveTimer < moveTimeout))
                        {
                            // Si encontramos al jugador durante el movimiento
                            if (_hasLineOfSight)
                            {
                                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡Jugador encontrado durante movimiento!");
                                
                                // Mostrar admiración
                                if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                                {
                                    _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                                }
                                
                                yield return new WaitForSeconds(1.0f);
                                
                                StopMove();
                                _currentState = CombatState.EVALUATE;
                                yield break;
                            }
                            
                            moveTimer += Time.deltaTime;
                            yield return null;
                        }
                        
                        StopMove();
                        
                        // ✅ AL DETENERSE: Mostrar interrogación de nuevo y animación
                        // 🔥 CORRECCIÓN: Solo si NO es combate reciente
                        Debug.Log($"[CombatBrain:{gameObject.name}] ❓ Parada de búsqueda #{searchAttempts} - No encontrado");
                        
                        if (showQuestionMark)
                        {
                            if (_alertIconController != null && _config != null && _config.questionIconPrefab != null)
                            {
                                _alertIconController.ShowQuestion(_config.questionIconPrefab, _config.alertIconDuration);
                            }
                            
                            // Reproducir animación de búsqueda
                            if (_animator != null)
                            {
                                _animator.PlaySearching();
                            }
                            
                            // Pausa para la animación y el icono (tiempo realista de "mirar alrededor")
                            yield return new WaitForSeconds(2.0f);
                        }
                        else
                        {
                            // En combate reciente: búsqueda más rápida sin animaciones largas
                            yield return new WaitForSeconds(0.5f);
                        }
                        
                        // Verificar de nuevo si lo encontró mientras miraba alrededor
                        if (_hasLineOfSight)
                        {
                            Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡Jugador encontrado mientras miraba alrededor!");
                            
                            if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                            {
                                _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                            }
                            
                            yield return new WaitForSeconds(1.0f);
                            
                            StopMove();
                            _currentState = CombatState.EVALUATE;
                            yield break;
                        }
                    }
                }
                else
                {
                    // BÚSQUEDA PASIVA: Solo espera y observa
                    yield return new WaitForSeconds(1.0f);
                }
            }
            
            // ✅ BÚSQUEDA AGOTADA - No encontró al jugador después de todos los intentos
            Debug.Log($"[CombatBrain:{gameObject.name}] 😞 Búsqueda agotada - {searchAttempts} intentos completados sin éxito");
            
            // Ocultar icono de interrogación
            if (_alertIconController != null)
            {
                _alertIconController.HideAlertIcon();
            }
            
            // ✅ DECISIÓN POST-BÚSQUEDA: ¿Volver al origen o abandonar?
            if (settings.returnToOriginAfterSearch)
            {
                // OPCIÓN A: Volver a la posición inicial
                Debug.Log($"[CombatBrain:{gameObject.name}] 🏠 Volviendo al origen tras búsqueda fallida: {_combatStartPosition}");
                MoveTo(_combatStartPosition, settings.walkSpeed);
                
                // Esperar a llegar al origen
                while (_agent.pathPending || _agent.remainingDistance > 1.5f)
                {
                    // Si encuentra al jugador durante el regreso, retomar combate inmediatamente
                    if (_hasLineOfSight)
                    {
                        Debug.Log($"[CombatBrain:{gameObject.name}] ✅ ¡Jugador encontrado en el camino de regreso!");
                        
                        if (_alertIconController != null && _config != null && _config.exclamationIconPrefab != null)
                        {
                            _alertIconController.ShowExclamation(_config.exclamationIconPrefab, _config.alertIconDuration);
                        }
                        
                        yield return new WaitForSeconds(1.0f);
                        
                        StopMove();
                        _currentState = CombatState.EVALUATE;
                        yield break;
                    }
                    
                    yield return null;
                }
                
                StopMove();
                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Regresó al origen - Saliendo del modo combate");
            }
            else
            {
                // OPCIÓN B: Abandonar directamente sin volver
                Debug.Log($"[CombatBrain:{gameObject.name}] 🚫 No vuelve al origen (returnToOriginAfterSearch = false) - Abandonando combate");
            }
            
            // ✅ ABANDONAR MODO COMBATE
            Debug.Log($"[CombatBrain:{gameObject.name}] 🏳️ Abandonando modo combate - Jugador no encontrado tras búsqueda exhaustiva");
            StopCombat();
            
            // Notificar al manager que salimos de combate
            if (_manager != null && _manager.Context != null)
            {
                _manager.Context.IsInCombat = false;
            }
        }
        
        // =================================================================================
        // 🛡️ BÚSQUEDA DE COBERTURA
        // =================================================================================
        
        /// <summary>
        /// Busca objetos en el layer Default (obstáculos) para esconderse detrás de ellos.
        /// Retorna true si encontró una posición de cobertura válida.
        /// </summary>
        private bool FindCoverBehindObstacle(out Vector3 coverPosition)
        {
            coverPosition = transform.position;
            
            // Buscar todos los colliders en un radio (layer Default)
            int defaultLayer = LayerMask.NameToLayer("Default");
            LayerMask defaultMask = 1 << defaultLayer;
            
            Collider[] nearbyObstacles = Physics.OverlapSphere(
                transform.position, 
                15f, // Radio de búsqueda de obstáculos
                defaultMask
            );
            
            if (nearbyObstacles.Length == 0)
            {
                Debug.Log($"[CombatBrain:{gameObject.name}] ⚠️ No se encontraron obstáculos Default cercanos");
                return false;
            }
            
            Debug.Log($"[CombatBrain:{gameObject.name}] 🔍 Encontrados {nearbyObstacles.Length} obstáculos Default para cobertura");
            
            // Buscar el mejor obstáculo para esconderse
            float bestScore = float.MinValue;
            Vector3 bestPosition = transform.position;
            bool foundValidCover = false;
            
            foreach (var obstacle in nearbyObstacles)
            {
                // Ignorar triggers
                if (obstacle.isTrigger) continue;
                
                // Obtener el punto más cercano del obstáculo al NPC
                Vector3 obstaclePoint = obstacle.ClosestPoint(transform.position);
                
                // Calcular dirección del jugador al obstáculo
                Vector3 dirPlayerToObstacle = (obstaclePoint - _player.position).normalized;
                
                // Posición de cobertura: detrás del obstáculo, alejándose del jugador
                Vector3 potentialCoverPos = obstaclePoint + dirPlayerToObstacle * 2f;
                
                // Verificar que esté en NavMesh
                if (!NavMesh.SamplePosition(potentialCoverPos, out NavMeshHit navHit, 3f, NavMesh.AllAreas))
                {
                    continue;
                }
                
                // Verificar que el obstáculo esté entre el jugador y la posición de cobertura
                Vector3 dirToPlayer = (_player.position - navHit.position).normalized;
                if (Physics.Raycast(navHit.position + Vector3.up, dirToPlayer, out RaycastHit hit, 
                    Vector3.Distance(navHit.position, _player.position), defaultMask))
                {
                    // Hay un obstáculo entre la posición de cobertura y el jugador - PERFECTO
                    
                    // Calcular puntuación: preferir obstáculos más cercanos al NPC
                    float distanceToNPC = Vector3.Distance(transform.position, navHit.position);
                    float score = 20f - distanceToNPC; // Mayor puntuación = más cerca
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPosition = navHit.position;
                        foundValidCover = true;
                        
                        Debug.Log($"[CombatBrain:{gameObject.name}] 🛡️ Cobertura válida encontrada: {obstacle.gameObject.name} (score: {score:F1})");
                    }
                }
            }
            
            if (foundValidCover)
            {
                coverPosition = bestPosition;
                Debug.Log($"[CombatBrain:{gameObject.name}] ✅ Mejor cobertura seleccionada en: {coverPosition}");
                return true;
            }
            
            Debug.Log($"[CombatBrain:{gameObject.name}] ❌ No se encontró cobertura válida detrás de obstáculos");
            return false;
        }
        
        // Debug Visual
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, settings.minSafeDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, settings.maxDistance);
            
            // Visualizar radio de búsqueda
            if (_currentState == CombatState.SEARCHING)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_lastKnownPlayerPosition, settings.searchMovementRadius);
            }
        }
    }
}