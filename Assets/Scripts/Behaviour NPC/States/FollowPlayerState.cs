using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado FSM para NPCs que siguen al jugador como compañeros de equipo.
    /// VERSIÓN MEJORADA: Sigue al jugador pegadito como en Pokémon/Zelda.
    /// </summary>
    public class FollowPlayerState : NPCStateBase
    {
        public override string StateName => "FollowPlayer";

        private readonly NPCPartyMember _partyMember;
        private NPCPartyConfig _config;
        
        // Timers
        private float _pathUpdateTimer;
        private float _stateTimer; // Tiempo total en este estado
        private float _idleTimer; // Tiempo que lleva parado
        
        // Estados internos
        private bool _isInitialized;
        private Vector3 _lastPlayerPosition;
        
        // Valores por defecto (se usan si no hay config)
        private const float PATH_UPDATE_INTERVAL = 0.1f; // Actualizar path MÁS frecuente (era 0.15)
        private const float DEFAULT_STOP_DISTANCE = 1.2f; // MÁS CERCA (era 2f)
        private const float DEFAULT_RUN_DISTANCE = 3f; // Correr antes (era 4f)
        private const float DEFAULT_WALK_SPEED = 3.5f;
        private const float DEFAULT_RUN_SPEED = 7.5f; // Un poco más rápido
        private const float PLAYER_MOVE_THRESHOLD = 0.1f; // Umbral de movimiento del jugador
        private const float INITIAL_DELAY = 0.3f; // Delay inicial antes de empezar a seguir

        public FollowPlayerState(NPCPartyMember partyMember)
        {
            _partyMember = partyMember;
            _config = partyMember?.PartyConfig;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);
            
            Debug.Log($"[FollowPlayerState] 🚶 {context.Transform.name} entró en FollowPlayerState");
            
            _pathUpdateTimer = 0f;
            _stateTimer = 0f;
            _idleTimer = 0f;
            _isInitialized = false;
            
            // Guardar posición inicial del jugador
            if (context.Player != null)
            {
                _lastPlayerPosition = context.Player.position;
            }
            
            if (context.Agent != null)
            {
                // ✅ IMPORTANTE: Empezar PARADO para evitar movimientos bruscos al inicio
                context.Agent.isStopped = true;
                context.Agent.updatePosition = false; // ✅ FIX: Evitar oscilación en Y cuando está quieto
                context.Agent.updateRotation = false; // Controlaremos la rotación manualmente
                context.Agent.speed = GetWalkSpeed();
                
                // Resetear path para evitar que vaya a destinos antiguos
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.ResetPath();
                }
            }
            
            // Asegurar animación idle al inicio
            context.Animator?.SetMovementSpeed(0f);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);
            
            if (context.Player == null) return;
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;
            
            _stateTimer += Time.deltaTime;
            
            // ✅ DELAY INICIAL: Esperar un poco antes de empezar a seguir (evita caos al inicio)
            if (_stateTimer < INITIAL_DELAY)
            {
                context.Agent.isStopped = true;
                context.Animator?.SetMovementSpeed(0f);
                return;
            }
            
            // Marcar como inicializado después del delay
            if (!_isInitialized)
            {
                _isInitialized = true;
                _lastPlayerPosition = context.Player.position;
            }
            
            float distance = Vector3.Distance(context.Transform.position, context.Player.position);
            
            // Obtener valores del config o usar defaults
            float stopDist = _config?.distanciaParaPararse ?? DEFAULT_STOP_DISTANCE;
            float runDist = _config?.distanciaParaCorrer ?? DEFAULT_RUN_DISTANCE;
            float walkSpeed = _config?.velocidadCaminando ?? DEFAULT_WALK_SPEED;
            float runSpeed = _config?.velocidadCorriendo ?? DEFAULT_RUN_SPEED;
            
            // ✅ DETECTAR SI EL JUGADOR SE ESTÁ MOVIENDO
            bool playerIsMoving = Vector3.Distance(_lastPlayerPosition, context.Player.position) > PLAYER_MOVE_THRESHOLD;
            if (playerIsMoving)
            {
                _lastPlayerPosition = context.Player.position;
                _idleTimer = 0f; // Resetear idle timer
            }
            
            // ===== LÓGICA MEJORADA DE SEGUIMIENTO =====
            
            // 1. Si está MUY CERCA -> PARAR y MIRAR al jugador
            if (distance <= stopDist)
            {
                // ✅ Solo actualizar si no estaba detenido antes (evita llamadas repetidas)
                if (!context.Agent.isStopped || context.Agent.updatePosition)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false; // ✅ FIX: Desactivar actualización de posición para evitar oscilación en Y
                    context.Agent.ResetPath(); // ✅ Limpiar el path para asegurar que no hay movimiento residual
                }
                context.Animator?.SetMovementSpeed(0f);
                
                _idleTimer += Time.deltaTime;
                
                // Girar suavemente hacia el jugador cuando está cerca
                if (_idleTimer > 0.5f) // Esperar medio segundo antes de girar
                {
                    RotateTowardsPlayer(context);
                }
                
                return;
            }
            
            // 2. Si el jugador SE ESTÁ MOVIENDO o está lejos -> SEGUIR
            if (playerIsMoving || distance > stopDist * 1.2f)
            {
                // ✅ Reactivar actualización de posición cuando empieza a moverse
                if (!context.Agent.updatePosition)
                {
                    // ✅ FIX: Sincronizar la posición del agente con el transform ANTES de reactivar
                    // Esto evita el "salto" o teletransporte cuando se reactiva updatePosition
                    if (context.Agent.isOnNavMesh)
                    {
                        context.Agent.nextPosition = context.Transform.position;
                    }
                    
                    context.Agent.updatePosition = true;
                    context.Agent.updateRotation = true;
                }
                
                // Si está LEJOS -> CORRER
                if (distance > runDist)
                {
                    context.Agent.isStopped = false;
                    context.Agent.speed = runSpeed;
                }
                // Si está CERCA pero no en rango de parada -> CAMINAR
                else
                {
                    context.Agent.isStopped = false;
                    context.Agent.speed = walkSpeed;
                }
                
                // Actualizar destino periódicamente
                _pathUpdateTimer += Time.deltaTime;
                if (_pathUpdateTimer >= PATH_UPDATE_INTERVAL)
                {
                    _pathUpdateTimer = 0f;
                    UpdateDestination(context, stopDist);
                }
            }
            // 3. Si el jugador ESTÁ QUIETO y el NPC está cerca -> PARAR
            else
            {
                // ✅ Solo actualizar si no estaba detenido antes (evita llamadas repetidas)
                if (!context.Agent.isStopped || context.Agent.updatePosition)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false; // ✅ FIX: Desactivar actualización de posición
                    context.Agent.ResetPath(); // ✅ Limpiar el path para asegurar que no hay movimiento residual
                }
                context.Animator?.SetMovementSpeed(0f);
            }
            
            // Actualizar animación de movimiento
            UpdateMovementAnimation(context);
        }
        
        /// <summary>
        /// Gira suavemente hacia el jugador cuando está cerca
        /// </summary>
        private void RotateTowardsPlayer(NPCStateContext context)
        {
            if (context.Player == null) return;
            
            Vector3 directionToPlayer = context.Player.position - context.Transform.position;
            directionToPlayer.y = 0; // Mantener en plano horizontal
            
            if (directionToPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
                context.Transform.rotation = Quaternion.Slerp(
                    context.Transform.rotation, 
                    targetRotation, 
                    Time.deltaTime * 3f // Velocidad de rotación suave
                );
            }
        }
        
        private void UpdateDestination(NPCStateContext context, float followDist)
        {
            bool preferBehind = _config?.quedarseDetras ?? true;
            
            // Calcular posición objetivo
            Vector3 targetPos;
            if (preferBehind)
            {
                // Posición detrás del jugador (más cercana que antes)
                Vector3 behindOffset = -context.Player.forward * (followDist * 0.7f); // 70% de la distancia
                targetPos = context.Player.position + behindOffset;
            }
            else
            {
                // Directamente hacia el jugador
                Vector3 direction = (context.Transform.position - context.Player.position).normalized;
                targetPos = context.Player.position + direction * followDist;
            }
            
            // Validar en NavMesh
            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                // Solo actualizar si el destino cambió significativamente
                if (Vector3.Distance(context.Agent.destination, hit.position) > 0.5f)
                {
                    context.Agent.SetDestination(hit.position);
                }
            }
            else
            {
                // Fallback: ir directamente al jugador
                context.Agent.SetDestination(context.Player.position);
            }
        }
        
        private float GetWalkSpeed()
        {
            return _config?.velocidadCaminando ?? DEFAULT_WALK_SPEED;
        }

        public override void OnExit(NPCStateContext context)
        {
            if (context.Agent != null && context.Agent.isOnNavMesh)
            {
                context.Agent.isStopped = true;
                // ✅ Restaurar valores por defecto al salir
                context.Agent.updatePosition = true;
                context.Agent.updateRotation = true;
            }
            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            // 1. Cinemática
            if (context.IsInCinematic) return new CinematicState();
            
            // 2. Combate
            if (context.IsInCombat) return new AllyCombatState();
            
            // 3. Si ya no está en el equipo
            if (_partyMember == null || !_partyMember.IsInParty)
            {
                return new IdleState();
            }
            
            return null;
        }
    }
}
