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

        // FSM State
        public enum CombatState { EVALUATE, REPOSITION, ATTACK, DEFENSE }
        [SerializeField, ReadOnly] private CombatState _currentState; // Visible debug

        // Cooldowns
        float _leftCd, _rightCd, _specialCd, _shieldCd, _globalCd;
        
        Coroutine _fsmRoutine;
        bool _isActive;
        #endregion

        // Inicialización
        public void Initialize(NPCBehaviourManagerV2 manager)
        {
            _manager = manager;
            _ctx = manager.Context;
            _agent = _ctx.Agent;
            _animator = _ctx.Animator;
            _rawAnimator = _ctx.UnityAnimator;
            _shieldController = GetComponent<NPCShieldController>();

            // Configurar NavMesh para movimiento fluido
            _agent.updateRotation = false; // Controlamos la rotación manualmente para encarar al player
            _agent.updatePosition = true;
            _agent.acceleration = 12f;
        }

        public void BeginCombat(Settings newSettings)
        {
            settings = newSettings;
            if (_fsmRoutine != null) StopCoroutine(_fsmRoutine);
            
            // Buscar player si no existe
            if (_ctx.Player == null) 
                 _ctx.Player = GameObject.FindWithTag("Player").transform;
            _player = _ctx.Player;

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

            // Rotación suave hacia el jugador siempre (Strafing)
            if (_player != null && _currentState != CombatState.REPOSITION) // Al huir no miramos al player
            {
                FaceTarget(_player.position);
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
                }
                yield return null;
            }
        }

        // 1. EVALUAR: El cerebro que decide qué hacer
        IEnumerator State_Evaluate()
        {
            float dist = Vector3.Distance(transform.position, _player.position);

            // A. Si está demasiado cerca -> HUIR (Reposicionarse)
            if (dist < settings.minSafeDistance)
            {
                _currentState = CombatState.REPOSITION;
                yield break;
            }

            // B. Si tengo ataques disponibles y estoy en rango -> ATACAR
            if (HasAnyAttackReady() && dist <= settings.maxDistance && _globalCd <= 0)
            {
                _currentState = CombatState.ATTACK;
                yield break;
            }

            // C. Si no tengo ataques (estoy en CD) -> DEFENDERSE
            if (!HasAnyAttackReady())
            {
                _currentState = CombatState.DEFENSE;
                yield break;
            }

            // D. Si estoy muy lejos -> Acercarse (Usamos Reposition para acercarnos también)
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
                // Calcular vector opuesto al jugador
                Vector3 dirAway = (transform.position - _player.position).normalized;
                targetPos = transform.position + dirAway * 5f; // Alejarse 5 metros
                
                // Asegurar que miramos hacia donde corremos si es una huida desesperada
                FaceTarget(targetPos); 
                MoveTo(targetPos, settings.runSpeed);
                
                // Esperar hasta llegar o 2 segundos máx
                float timer = 0;
                while (_agent.remainingDistance > 1f && timer < 2f)
                {
                    timer += Time.deltaTime;
                    yield return null;
                }
            }
            
            // Volver a evaluar al terminar movimiento
            StopMove();
            _currentState = CombatState.EVALUATE;
        }

        // 3. ATACAR: Seleccionar hechizo y disparar
        IEnumerator State_Attack()
        {
            StopMove(); // Quieto para disparar
            FaceTarget(_player.position);

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
                
                // Ejecutar Animación
                _rawAnimator.Play(chosenAttack.animationState, settings.upperBodyLayer);
                
                // Disparar Proyectil (Si no es por evento de animación)
                if (!settings.spawnProjectileViaAnimEvent)
                {
                    yield return new WaitForSeconds(settings.fireDelaySeconds);
                    SpawnProjectile(chosenAttack.slotIndex);
                }

                // Pausa post-ataque (Global Cooldown)
                _globalCd = settings.globalCooldown;
                yield return new WaitForSeconds(0.5f); // Tiempo de recuperación de animación

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
                    _shieldController.StartDefending(settings.shieldDuration);
                    _shieldCd = settings.shieldCooldown + settings.shieldDuration;
                    yield return new WaitForSeconds(settings.shieldDuration); // Esperar protegido
                }
                // B. Si no hay escudo, buscar COBERTURA
                else
                {
                    Vector3 coverPos;
                    if (TryGetCoverPosition(out coverPos))
                    {
                        MoveTo(coverPos, settings.runSpeed);
                        // Esperar a llegar
                        while (_agent.remainingDistance > 0.5f) yield return null;
                        
                        // Esperar tras la cobertura recuperando cooldowns
                        yield return new WaitForSeconds(1.5f);
                    }
                    else
                    {
                        // Si no hay cobertura, esquiva simple
                        yield return DoDodge();
                    }
                }
            }
            else
            {
                // === LÓGICA TORPE/BAJA DIFICULTAD ===
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

        private void FaceTarget(Vector3 target)
        {
            Vector3 dir = (target - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 10f);
            }
        }
        
        // Debug Visual
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, settings.minSafeDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, settings.maxDistance);
        }
    }
}