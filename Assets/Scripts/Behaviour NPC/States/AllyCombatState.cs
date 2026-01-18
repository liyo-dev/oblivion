using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado de combate para NPCs aliados/compañeros.
    /// A diferencia de CombatState, este busca y ataca ENEMIGOS (no al jugador).
    /// </summary>
    public class AllyCombatState : NPCStateBase
    {
        public override string StateName => "AllyCombat";

        private Transform _currentTarget;
        private NPCCombatBrain _combatBrain;
        private NPCPartyMember _partyMember;
        private float _targetCheckTimer;
        private float _attackTimer;
        private Vector3 _lastKnownEnemyPosition;
        
        private const float TARGET_CHECK_INTERVAL = 0.5f;
        private const float MAX_COMBAT_DISTANCE = 25f;
        private const float LOSE_TARGET_TIME = 3f;
        
        private float _timeSinceLastSawTarget;

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            context.Log("[AllyCombatState] ⚔️ Compañero entrando en combate aliado");
            context.IsInCombat = true;
            
            _partyMember = context.Transform.GetComponent<NPCPartyMember>();
            _targetCheckTimer = 0f;
            _attackTimer = 0f;
            _timeSinceLastSawTarget = 0f;
            
            // Buscar objetivo inicial (el que se pasó como context.Player, que en este caso es el enemigo)
            _currentTarget = context.Player;
            if (_currentTarget != null)
            {
                _lastKnownEnemyPosition = _currentTarget.position;
            }
            
            // Activar modo batalla en el animator
            context.Animator?.SetBattleMode(true);
            
            // Inicializar cerebro de combate si existe configuración
            InitializeCombatBrain(context);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            // Verificar objetivo periódicamente
            _targetCheckTimer += Time.deltaTime;
            if (_targetCheckTimer >= TARGET_CHECK_INTERVAL)
            {
                _targetCheckTimer = 0f;
                UpdateTarget(context);
            }
            
            if (_currentTarget == null)
            {
                _timeSinceLastSawTarget += Time.deltaTime;
                
                // Si no hay objetivo, moverse a la última posición conocida
                if (_lastKnownEnemyPosition != Vector3.zero)
                {
                    MoveToPosition(context, _lastKnownEnemyPosition);
                }
                return;
            }
            
            _timeSinceLastSawTarget = 0f;
            _lastKnownEnemyPosition = _currentTarget.position;
            
            // Lógica de combate
            float distanceToTarget = Vector3.Distance(context.Transform.position, _currentTarget.position);
            var combatConfig = context.Config?.combatConfig;
            
            float minDist = combatConfig?.minAttackDistance ?? 2f;
            float maxDist = combatConfig?.maxAttackDistance ?? 8f;
            
            // Usar el CombatBrain si existe, sino lógica simple
            if (_combatBrain != null && _combatBrain.enabled)
            {
                // El brain maneja todo
                return;
            }
            
            // Lógica simple de combate para aliados sin brain completo
            if (distanceToTarget > maxDist)
            {
                // Acercarse al enemigo
                MoveToPosition(context, _currentTarget.position);
                UpdateMovementAnimation(context);
            }
            else if (distanceToTarget < minDist)
            {
                // Retroceder un poco
                Vector3 retreatDir = (context.Transform.position - _currentTarget.position).normalized;
                Vector3 retreatPos = context.Transform.position + retreatDir * 2f;
                MoveToPosition(context, retreatPos);
                UpdateMovementAnimation(context);
            }
            else
            {
                // En rango óptimo - atacar
                StopMovement(context);
                RotateTowardsTarget(context);
                TryAttack(context);
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            context.IsInCombat = false;
            context.Animator?.SetBattleMode(false);
            StopMovement(context);
            
            // Desactivar brain si existe
            if (_combatBrain != null)
            {
                _combatBrain.StopCombat();
            }
            
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Cinemática tiene prioridad
            if (context.IsInCinematic) return new CinematicState();
            
            // 2. Si fue derrotado
            if (context.WasDefeatedInCombat) return new DeadState();
            
            // 3. Si ya no está en combate (forzado externamente)
            if (!context.IsInCombat) return new FollowPlayerState(_partyMember);
            
            // 4. Si no hay objetivo por mucho tiempo, volver a seguir
            if (_currentTarget == null && _timeSinceLastSawTarget > LOSE_TARGET_TIME)
            {
                context.Log("[AllyCombatState] 🏳️ Sin objetivo, volviendo a seguir al jugador");
                return new FollowPlayerState(_partyMember);
            }
            
            // 5. Si no hay más enemigos en combate
            if (ActiveCombatRegistry.Count == 0)
            {
                context.Log("[AllyCombatState] 🏳️ No hay más enemigos, fin del combate");
                return new FollowPlayerState(_partyMember);
            }
            
            return null;
        }

        #region Private Methods
        
        private void InitializeCombatBrain(NPCStateContext context)
        {
            var combatConfig = context.Config?.combatConfig;
            if (combatConfig == null) return;
            
            _combatBrain = context.Transform.GetComponent<NPCCombatBrain>();
            if (_combatBrain == null)
            {
                // Para aliados, usamos lógica simple por ahora
                // El CombatBrain está diseñado para atacar al jugador
                context.Log("[AllyCombatState] ℹ️ Usando lógica de combate simple para aliado");
                return;
            }
            
            // Si hay brain, configurarlo pero con el enemigo como objetivo
            // NOTA: Esto requeriría modificar NPCCombatBrain para soportar objetivos que no sean el jugador
        }

        private void UpdateTarget(NPCStateContext context)
        {
            // Verificar si el objetivo actual sigue siendo válido
            if (_currentTarget != null)
            {
                // Verificar que siga vivo y en rango
                var damageable = _currentTarget.GetComponent<Damageable>();
                if (damageable != null && damageable.Current <= 0)
                {
                    context.Log($"[AllyCombatState] ☠️ Objetivo {_currentTarget.name} derrotado");
                    _currentTarget = null;
                }
                
                // Verificar distancia
                float dist = Vector3.Distance(context.Transform.position, _currentTarget.position);
                if (dist > MAX_COMBAT_DISTANCE)
                {
                    context.Log($"[AllyCombatState] 📍 Objetivo {_currentTarget.name} demasiado lejos");
                    _currentTarget = null;
                }
            }
            
            // Si no hay objetivo, buscar el enemigo más cercano
            if (_currentTarget == null)
            {
                _currentTarget = FindClosestEnemy(context);
                if (_currentTarget != null)
                {
                    context.Log($"[AllyCombatState] 🎯 Nuevo objetivo: {_currentTarget.name}");
                    context.Player = _currentTarget; // Actualizar referencia para el brain
                }
            }
        }

        private Transform FindClosestEnemy(NPCStateContext context)
        {
            // Buscar en el ActiveCombatRegistry (enemigos en combate)
            var closestEnemy = ActiveCombatRegistry.GetClosestCombatNPC(
                context.Transform.position, 
                MAX_COMBAT_DISTANCE
            );
            
            if (closestEnemy != null)
            {
                // Verificar que no sea un aliado
                var partyMember = closestEnemy.GetComponent<NPCPartyMember>();
                if (partyMember != null && partyMember.IsInParty)
                {
                    return null; // Es un aliado, no atacar
                }
                
                return closestEnemy.transform;
            }
            
            return null;
        }

        private void MoveToPosition(NPCStateContext context, Vector3 position)
        {
            if (!IsAgentValid(context)) return;
            
            // Configurar velocidad de carrera
            context.Agent.speed = context.Config?.runSpeed ?? 4f;
            SetDestination(context, position);
        }

        private void RotateTowardsTarget(NPCStateContext context)
        {
            if (_currentTarget == null) return;
            
            Vector3 direction = (_currentTarget.position - context.Transform.position).normalized;
            direction.y = 0;
            
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                float rotSpeed = context.Config?.rotationSpeed ?? 180f;
                context.Transform.rotation = Quaternion.RotateTowards(
                    context.Transform.rotation,
                    targetRotation,
                    rotSpeed * Time.deltaTime
                );
            }
        }

        private void TryAttack(NPCStateContext context)
        {
            _attackTimer += Time.deltaTime;
            
            var combatConfig = context.Config?.combatConfig;
            float attackCooldown = combatConfig?.spell1Cooldown ?? 2f;
            
            if (_attackTimer >= attackCooldown)
            {
                _attackTimer = 0f;
                
                // Ejecutar ataque (animación)
                context.Animator?.PlayOneShot("Attack");
                
                // Si hay proyectil configurado, dispararlo
                if (combatConfig?.spell1Prefab != null && _currentTarget != null)
                {
                    SpawnProjectile(context, combatConfig.spell1Prefab, combatConfig);
                }
                
                context.Log($"[AllyCombatState] 💥 Atacando a {_currentTarget?.name}");
            }
        }

        private void SpawnProjectile(NPCStateContext context, GameObject projectilePrefab, NPCCombatConfig combatConfig)
        {
            if (projectilePrefab == null || _currentTarget == null) return;
            
            // Buscar punto de spawn (primero "SpellSpawnPoint", luego centro del NPC)
            Transform spawnPoint = context.Transform.Find("SpellSpawnPoint");
            if (spawnPoint == null) spawnPoint = context.Transform;
            
            Vector3 spawnPos = spawnPoint.position + Vector3.up * 1.2f;
            Vector3 direction = (_currentTarget.position + Vector3.up - spawnPos).normalized;
            
            var projectileGO = Object.Instantiate(
                projectilePrefab, 
                spawnPos, 
                Quaternion.LookRotation(direction)
            );
            
            // Configurar el MagicProjectile como proyectil de aliado
            var magicProjectile = projectileGO.GetComponent<MagicProjectile>();
            if (magicProjectile != null)
            {
                // Usar el daño del combatConfig si está definido, sino usar default
                float damage = 15f; // Daño base
                float speed = 12f;  // Velocidad base
                
                // Configurar como proyectil aliado
                magicProjectile.ConfigureAlly(damage, speed, context.Transform.gameObject);
                
                // Lanzar hacia el objetivo
                magicProjectile.Launch(direction, speed, false);
                
                context.Log($"[AllyCombatState] 🎯 Proyectil lanzado hacia {_currentTarget.name}");
            }
            else
            {
                // Si no tiene MagicProjectile, al menos darle velocidad
                var rb = projectileGO.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = direction * 12f;
                }
                
                // Auto-destruir después de 5 segundos
                Object.Destroy(projectileGO, 5f);
            }
        }
        
        #endregion
    }
}

