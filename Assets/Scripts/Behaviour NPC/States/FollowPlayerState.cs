using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.Modules;

namespace Game.NPC.States
{
    /// <summary>
    /// Estado FSM para NPCs que siguen al jugador como compañeros de equipo.
    /// Replica las animaciones del jugador en modos vuelo, nado, escalada y plataformas sin NavMesh.
    /// </summary>
    public class FollowPlayerState : NPCStateBase
    {
        public override string StateName => "FollowPlayer";

        private readonly NPCPartyMember _partyMember;
        private NPCPartyConfig _config;

        // Timers
        private float _pathUpdateTimer;
        private float _stateTimer;
        private float _idleTimer;

        // Estados internos NavMesh
        private bool _isInitialized;
        private Vector3 _lastPlayerPosition;
        private bool _isWanderingNearPlayer;
        private Vector3 _wanderTarget;

        // Constantes NavMesh
        private const float PLAYER_STATIC_BEFORE_WANDER = 3f;
        private const float PATH_UPDATE_INTERVAL = 0.1f;
        private const float DEFAULT_STOP_DISTANCE = 1.2f;
        private const float DEFAULT_RUN_DISTANCE = 3f;
        private const float DEFAULT_WALK_SPEED = 3.5f;
        private const float DEFAULT_RUN_SPEED = 7.5f;
        private const float PLAYER_MOVE_THRESHOLD = 0.1f;
        private const float INITIAL_DELAY = 0.3f;
        private const float ROTATION_ANGLE_DEADZONE = 2.5f;

        // --- Seguimiento especial (vuelo, nado, escalada, plataformas sin NavMesh) ---
        private PlayerActionManager _playerActionManager;
        private bool _inSpecialFollow;
        private ActionMode _lastSpecialMode = ActionMode.Default;
        private bool _storedGravity;
        private bool _gravityStored;
        private float _storedAnimSpeed = 1f;
        private bool _animSpeedStored;

        private const float SPECIAL_FOLLOW_SPEED = 8f;
        private const float SPECIAL_FOLLOW_STOP_DIST = 1.5f;
        private const float OFF_NAVMESH_THRESHOLD = 1.5f;
        private const float WARP_SEARCH_RADIUS = 10f;

        // Hashes de estados y parámetros (compartidos con el animator del jugador)
        private static readonly int HashFlyIdle      = Animator.StringToHash("fly_idle");
        private static readonly int HashFlyDive      = Animator.StringToHash("fly_dive");
        private static readonly int HashFlyLanding   = Animator.StringToHash("Landing");
        private static readonly int HashLocomotion   = Animator.StringToHash("Free Locomotion");
        private static readonly int HashSwimFloat    = Animator.StringToHash("Swimming_Floating_NoWeapon");
        private static readonly int HashClimbUp      = Animator.StringToHash("ClimbUp_RM_NoWeapon");
        private static readonly int HashClimbDown    = Animator.StringToHash("ClimbDown_RM_NoWeapon");
        private static readonly int HashClimbIdle    = Animator.StringToHash("ClimbIdle_RM_NoWeapon");
        private static readonly int HashIsFlying     = Animator.StringToHash("isFlying");
        private static readonly int HashIsGrounded   = Animator.StringToHash("IsGrounded");
        private static readonly int HashGroundDist   = Animator.StringToHash("GroundDistance");
        private static readonly int HashInputMag     = Animator.StringToHash("InputMagnitude");

        private readonly bool _skipPartyCheck;

        public FollowPlayerState(NPCPartyMember partyMember, bool skipPartyCheck = false)
        {
            _partyMember = partyMember;
            _skipPartyCheck = skipPartyCheck;
            _config = partyMember?.PartyConfig;
        }

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FollowPlayerState] {context.Transform.name} entró en FollowPlayerState");
#endif

            _pathUpdateTimer = 0f;
            _stateTimer = 0f;
            _idleTimer = 0f;
            _isInitialized = false;
            _isWanderingNearPlayer = false;
            _inSpecialFollow = false;
            _lastSpecialMode = ActionMode.Default;
            _gravityStored = false;
            _animSpeedStored = false;

            if (context.Player != null)
            {
                _lastPlayerPosition = context.Player.position;
                _playerActionManager = context.Player.GetComponent<PlayerActionManager>();
            }

            if (context.Agent != null)
            {
                context.Agent.isStopped = true;
                context.Agent.updatePosition = false;
                context.Agent.updateRotation = false;
                context.Agent.speed = GetWalkSpeed();

                if (context.Agent.isOnNavMesh)
                    context.Agent.ResetPath();
            }

            context.Animator?.SetMovementSpeed(0f);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (context.Player == null) return;

            _stateTimer += Time.deltaTime;

            // --- MODO ESPECIAL: vuelo, nado, escalada, o jugador fuera del NavMesh ---
            ActionMode playerMode = _playerActionManager?.Top ?? ActionMode.Default;
            bool needsSpecialFollow = playerMode == ActionMode.Flying
                                   || playerMode == ActionMode.Swimming
                                   || playerMode == ActionMode.Climbing
                                   || IsPlayerOffNavMesh(context);

            if (needsSpecialFollow)
            {
                HandleSpecialFollow(context, playerMode);
                return;
            }

            // Salir del modo especial si el jugador volvió al suelo
            if (_inSpecialFollow)
                ExitSpecialFollow(context);

            // --- SEGUIMIENTO NORMAL POR NAVMESH ---
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;

            if (_stateTimer < INITIAL_DELAY)
            {
                context.Agent.isStopped = true;
                context.Animator?.SetMovementSpeed(0f);
                return;
            }

            if (!_isInitialized)
            {
                _isInitialized = true;
                _lastPlayerPosition = context.Player.position;
            }

            float distance = Vector3.Distance(context.Transform.position, context.Player.position);

            float stopDist  = _config?.distanciaParaPararse ?? DEFAULT_STOP_DISTANCE;
            float runDist   = _config?.distanciaParaCorrer  ?? DEFAULT_RUN_DISTANCE;
            float walkSpeed = _config?.velocidadCaminando   ?? DEFAULT_WALK_SPEED;
            float runSpeed  = _config?.velocidadCorriendo   ?? DEFAULT_RUN_SPEED;

            bool playerIsMoving = Vector3.Distance(_lastPlayerPosition, context.Player.position) > PLAYER_MOVE_THRESHOLD;
            if (playerIsMoving)
            {
                _lastPlayerPosition = context.Player.position;
                _idleTimer = 0f;
                _isWanderingNearPlayer = false;
            }

            if (_isWanderingNearPlayer)
            {
                bool reached = !context.Agent.pathPending && context.Agent.remainingDistance < stopDist * 0.5f;
                if (reached)
                {
                    _isWanderingNearPlayer = false;
                    _idleTimer = 0f;
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Animator?.SetMovementSpeed(0f);
                }
                else
                {
                    UpdateMovementAnimation(context);
                    return;
                }
            }

            if (distance <= stopDist)
            {
                if (!context.Agent.isStopped || context.Agent.updatePosition)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Agent.ResetPath();
                }
                context.Animator?.SetMovementSpeed(0f);

                _idleTimer += Time.deltaTime;

                if (_idleTimer > 0.5f)
                    RotateTowardsPlayer(context);

                if (_config != null && _config.puedeVagarCerca &&
                    _idleTimer >= PLAYER_STATIC_BEFORE_WANDER)
                {
                    TryStartPartyWander(context, stopDist);
                }

                return;
            }

            if (playerIsMoving || distance > stopDist * 1.2f)
            {
                if (!context.Agent.updatePosition)
                {
                    if (context.Agent.isOnNavMesh)
                        context.Agent.nextPosition = context.Transform.position;

                    context.Agent.updatePosition = true;
                    context.Agent.updateRotation = false;
                }

                context.Agent.isStopped = false;
                context.Agent.speed = distance > runDist ? runSpeed : walkSpeed;

                _pathUpdateTimer += Time.deltaTime;
                if (_pathUpdateTimer >= PATH_UPDATE_INTERVAL)
                {
                    _pathUpdateTimer = 0f;
                    UpdateDestination(context, stopDist);
                }
            }
            else
            {
                if (!context.Agent.isStopped || context.Agent.updatePosition)
                {
                    context.Agent.isStopped = true;
                    context.Agent.updatePosition = false;
                    context.Agent.ResetPath();
                }
                context.Animator?.SetMovementSpeed(0f);
            }

            UpdateMovementAnimation(context);
        }

        // =========================================================================
        // SEGUIMIENTO ESPECIAL
        // =========================================================================

        private bool IsPlayerOffNavMesh(NPCStateContext context)
        {
            if (context.Player == null) return false;
            return !NavMesh.SamplePosition(context.Player.position, out _, OFF_NAVMESH_THRESHOLD, NavMesh.AllAreas);
        }

        private void HandleSpecialFollow(NPCStateContext context, ActionMode playerMode)
        {
            bool modeChanged = playerMode != _lastSpecialMode;

            // Primera entrada o cambio de modo: configurar física y animación
            if (!_inSpecialFollow || modeChanged)
            {
                if (!_inSpecialFollow)
                {
                    // Primer frame: desactivar NavMesh y gravedad
                    _inSpecialFollow = true;
                    _pathUpdateTimer = 0f;
                    _isWanderingNearPlayer = false;

                    if (context.Agent != null && context.Agent.isActiveAndEnabled)
                    {
                        if (context.Agent.isOnNavMesh) context.Agent.ResetPath();
                        context.Agent.isStopped = true;
                        context.Agent.updatePosition = false;
                        context.Agent.updateRotation = false;
                    }

                    if (context.Rigidbody != null && !_gravityStored)
                    {
                        _storedGravity = context.Rigidbody.useGravity;
                        context.Rigidbody.useGravity = false;
                        context.Rigidbody.linearVelocity = Vector3.zero;
                        _gravityStored = true;
                    }
                }

                // Reproducir animación de entrada al modo
                OnSpecialModeEnter(context, playerMode);
                _lastSpecialMode = playerMode;
            }

            // Calcular posición objetivo y mover
            Vector3 targetPos  = GetFormationTarget3D(context);
            Vector3 currentPos = context.Transform.position;
            Vector3 delta      = targetPos - currentPos;
            float   dist       = delta.magnitude;

            bool isMoving = dist > SPECIAL_FOLLOW_STOP_DIST * 0.5f;

            if (isMoving)
            {
                float speed = dist > SPECIAL_FOLLOW_STOP_DIST * 3f
                    ? SPECIAL_FOLLOW_SPEED * 1.5f
                    : SPECIAL_FOLLOW_SPEED;

                context.Transform.position = currentPos + delta.normalized * Mathf.Min(speed * Time.deltaTime, dist);

                if (context.Rigidbody != null && !context.Rigidbody.isKinematic)
                    context.Rigidbody.linearVelocity = Vector3.zero;

                Vector3 faceDir = delta; faceDir.y = 0f;
                if (faceDir.sqrMagnitude > 0.01f)
                    context.Animator?.FaceDirection(faceDir);
            }
            else
            {
                if (context.Rigidbody != null && !context.Rigidbody.isKinematic)
                    context.Rigidbody.linearVelocity = Vector3.zero;
                RotateTowardsPlayer(context);
            }

            // Actualizar animación según modo y movimiento
            UpdateSpecialModeAnimation(context, playerMode, delta, isMoving);
        }

        /// <summary>Animación de entrada cuando se activa un modo especial.</summary>
        private void OnSpecialModeEnter(NPCStateContext context, ActionMode mode)
        {
            Animator anim = context.UnityAnimator;
            if (anim == null) return;

            switch (mode)
            {
                case ActionMode.Flying:
                    TrySetBool(anim, HashIsFlying, true);
                    TryPlay(anim, HashFlyIdle, 0);
                    break;

                case ActionMode.Swimming:
                    TryPlay(anim, HashSwimFloat, 0);
                    // Forzar grounded para evitar que el animator entre en estados de caída
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    break;

                case ActionMode.Climbing:
                    // Guardar velocidad original para pausar/restaurar
                    if (!_animSpeedStored)
                    {
                        _storedAnimSpeed = anim.speed;
                        _animSpeedStored = true;
                    }
                    TryPlay(anim, HashClimbIdle, 0);
                    anim.speed = 0f;
                    break;

                // Plataforma sin NavMesh: sin cambio de animación
            }
        }

        /// <summary>Actualización de animación cada frame según modo y movimiento.</summary>
        private void UpdateSpecialModeAnimation(NPCStateContext context, ActionMode mode,
                                                Vector3 delta, bool isMoving)
        {
            Animator anim = context.UnityAnimator;
            if (anim == null) return;

            switch (mode)
            {
                case ActionMode.Flying:
                    // fly_dive si se está moviendo, fly_idle si está quieto
                    if (isMoving)
                        TryPlay(anim, HashFlyDive, 0);
                    else
                        TryPlay(anim, HashFlyIdle, 0);
                    break;

                case ActionMode.Swimming:
                    // Mantener siempre la animación de nado y parámetros grounded
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    TrySetFloat(anim, HashInputMag, isMoving ? 0.5f : 0f);
                    break;

                case ActionMode.Climbing:
                    // Subir, bajar o quieto según delta Y
                    if (Mathf.Abs(delta.y) > 0.05f)
                    {
                        anim.speed = 1f;
                        if (delta.y > 0f)
                            TryCrossFade(anim, HashClimbUp, 0.05f, 0);
                        else
                            TryCrossFade(anim, HashClimbDown, 0.05f, 0);
                    }
                    else
                    {
                        anim.speed = 0f;
                    }
                    TrySetBool(anim, HashIsGrounded, true);
                    break;

                default:
                    // Plataforma sin NavMesh: locomoción normal
                    float animSpeed = Mathf.Clamp01(delta.magnitude / 3f);
                    context.Animator?.SetMovementSpeed(animSpeed);
                    break;
            }
        }

        /// <summary>Sale del modo especial: restaura física, animator y vuelve al NavMesh.</summary>
        private void ExitSpecialFollow(NPCStateContext context)
        {
            Animator anim = context.UnityAnimator;
            ActionMode exitingMode = _lastSpecialMode;

            _inSpecialFollow  = false;
            _lastSpecialMode  = ActionMode.Default;

            // Restaurar velocidad de animator (escalada la pausa)
            if (_animSpeedStored && anim != null)
            {
                anim.speed = _storedAnimSpeed;
                _animSpeedStored = false;
            }

            // Animación de salida
            if (anim != null)
            {
                switch (exitingMode)
                {
                    case ActionMode.Flying:
                        TrySetBool(anim, HashIsFlying, false);
                        // Landing si tiene el estado, si no Free Locomotion
                        if (!TryCrossFade(anim, HashFlyLanding, 0.08f, 0))
                            TryCrossFade(anim, HashLocomotion, 0.1f, 0);
                        break;

                    case ActionMode.Swimming:
                        TryCrossFade(anim, HashLocomotion, 0.1f, 0);
                        break;

                    case ActionMode.Climbing:
                        TryCrossFade(anim, HashLocomotion, 0.1f, 0);
                        break;
                }
            }

            // Restaurar gravedad
            if (context.Rigidbody != null && _gravityStored)
            {
                context.Rigidbody.useGravity = _storedGravity;
                var vel = context.Rigidbody.linearVelocity;
                vel.y = 0f;
                context.Rigidbody.linearVelocity = vel;
                _gravityStored = false;
            }

            // Devolver control al NavMeshAgent
            if (context.Agent != null && context.Agent.isActiveAndEnabled)
            {
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.nextPosition = context.Transform.position;
                }
                else if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit hit, WARP_SEARCH_RADIUS, NavMesh.AllAreas))
                {
                    context.Agent.Warp(hit.position);
                }

                context.Agent.isStopped = true;
                context.Agent.updatePosition = true;
                context.Agent.updateRotation = false;
            }
        }

        // =========================================================================
        // FORMACIÓN 3D
        // =========================================================================

        /// <summary>
        /// Posición de formación en 3D: detrás del jugador en XZ, misma Y.
        /// Offset lateral por índice de party para que no se apilen.
        /// idx 0: centro  idx 1: +0.8m  idx 2: -0.8m  idx 3: +1.6m …
        /// </summary>
        private Vector3 GetFormationTarget3D(NPCStateContext context)
        {
            float followDist = _config?.distanciaParaPararse ?? DEFAULT_STOP_DISTANCE;
            Vector3 behind   = -context.Player.forward * (followDist * 0.8f);

            int   partyIdx = _partyMember?.PartyIndex ?? 0;
            float lateral  = partyIdx == 0 ? 0f
                : (partyIdx % 2 == 1 ? 1f : -1f) * 0.8f * Mathf.Ceil(partyIdx * 0.5f);
            Vector3 side = context.Player.right * lateral;

            return new Vector3(
                context.Player.position.x + behind.x + side.x,
                context.Player.position.y,
                context.Player.position.z + behind.z + side.z
            );
        }

        // =========================================================================
        // HELPERS ANIMATOR (acceso seguro al Animator raw)
        // =========================================================================

        private static void TryPlay(Animator anim, int stateHash, int layer)
        {
            if (anim == null) return;
            try
            {
                if (anim.HasState(layer, stateHash))
                {
                    var cur = anim.GetCurrentAnimatorStateInfo(layer);
                    if (cur.shortNameHash != stateHash)
                        anim.Play(stateHash, layer);
                }
            }
            catch { }
        }

        /// <returns>true si el estado existe y se hizo crossfade.</returns>
        private static bool TryCrossFade(Animator anim, int stateHash, float duration, int layer)
        {
            if (anim == null) return false;
            try
            {
                if (!anim.HasState(layer, stateHash)) return false;
                // Solo hacer crossfade si no estamos ya en ese estado
                if (anim.GetCurrentAnimatorStateInfo(layer).shortNameHash != stateHash)
                    anim.CrossFade(stateHash, duration, layer);
                return true;
            }
            catch { }
            return false;
        }

        private static void TrySetBool(Animator anim, int paramHash, bool value)
        {
            if (anim == null) return;
            try { anim.SetBool(paramHash, value); } catch { }
        }

        private static void TrySetFloat(Animator anim, int paramHash, float value)
        {
            if (anim == null) return;
            try { anim.SetFloat(paramHash, value); } catch { }
        }

        // =========================================================================
        // HELPERS ORIGINALES
        // =========================================================================

        private void RotateTowardsPlayer(NPCStateContext context)
        {
            if (context.Player == null) return;

            Vector3 dir = context.Player.position - context.Transform.position;
            dir.y = 0;

            if (dir.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.Angle(context.Transform.forward, dir.normalized);
                if (angle <= ROTATION_ANGLE_DEADZONE) return;
                context.Animator?.FaceDirection(dir);
            }
        }

        private void UpdateDestination(NPCStateContext context, float followDist)
        {
            bool preferBehind = _config?.quedarseDetras ?? true;

            Vector3 targetPos;
            if (preferBehind)
            {
                targetPos = context.Player.position + (-context.Player.forward * (followDist * 0.7f));
            }
            else
            {
                Vector3 dir = (context.Transform.position - context.Player.position).normalized;
                targetPos = context.Player.position + dir * followDist;
            }

            if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                if (Vector3.Distance(context.Agent.destination, hit.position) > 0.5f)
                    context.Agent.SetDestination(hit.position);
            }
            else
            {
                context.Agent.SetDestination(context.Player.position);
            }
        }

        private float GetWalkSpeed() => _config?.velocidadCaminando ?? DEFAULT_WALK_SPEED;

        private void TryStartPartyWander(NPCStateContext context, float stopDist)
        {
            if (context.Player == null || context.Agent == null) return;

            float radius = _config.radioVagabundeo;
            Vector3 randomDir = Random.insideUnitSphere; randomDir.y = 0;
            if (randomDir.sqrMagnitude < 0.01f) randomDir = Vector3.right;
            Vector3 candidate = context.Player.position + randomDir.normalized * Random.Range(radius * 0.4f, radius);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas)) return;

            _isWanderingNearPlayer = true;
            _wanderTarget = hit.position;
            _idleTimer = 0f;

            if (!context.Agent.updatePosition)
            {
                context.Agent.nextPosition = context.Transform.position;
                context.Agent.updatePosition = true;
            }
            context.Agent.isStopped = false;
            context.Agent.speed = _config.velocidadCaminando;
            context.Agent.SetDestination(_wanderTarget);
        }

        public override void OnExit(NPCStateContext context)
        {
            if (_inSpecialFollow)
            {
                _inSpecialFollow = false;

                // Restaurar velocidad del animator (escalada)
                if (_animSpeedStored && context.UnityAnimator != null)
                {
                    context.UnityAnimator.speed = _storedAnimSpeed;
                    _animSpeedStored = false;
                }

                // Limpiar flag de vuelo
                TrySetBool(context.UnityAnimator, HashIsFlying, false);

                // Restaurar gravedad
                if (context.Rigidbody != null && _gravityStored)
                {
                    context.Rigidbody.useGravity = _storedGravity;
                    var vel = context.Rigidbody.linearVelocity;
                    vel.y = 0f;
                    context.Rigidbody.linearVelocity = vel;
                    _gravityStored = false;
                }
            }

            if (context.Agent != null && context.Agent.isActiveAndEnabled)
            {
                context.Agent.isStopped = true;
                if (!context.Agent.updatePosition)
                {
                    if (context.Agent.isOnNavMesh)
                        context.Agent.nextPosition = context.Transform.position;
                    else if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit hit, WARP_SEARCH_RADIUS, NavMesh.AllAreas))
                        context.Agent.Warp(hit.position);
                }
                context.Agent.updatePosition = true;
                context.Agent.updateRotation = false;
            }

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic) return new CinematicState();
            if (context.IsInCombat) return new AllyCombatState();
            if (!_skipPartyCheck && _partyMember != null && !_partyMember.IsInParty)
                return new IdleState();

            return null;
        }
    }
}
