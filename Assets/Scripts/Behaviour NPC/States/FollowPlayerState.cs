using System.Collections.Generic;
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

        // FIX (25 ago 2026) — "el party va a trompicones/tembloroso cuando el jugador va lento"
        // (nadando, o siguiendo a un guardia con la velocidad reducida por un lore pop-up).
        // _isFollowingSticky reemplaza la decisión "moverse si playerIsMoving || distance >
        // stopDist*1.2" por una histéresis persistente (igual que ya usa _isMovingSpecial más
        // abajo para el seguimiento especial): playerIsMoving solo es true el frame exacto en que
        // el jugador acumula 0.1m de movimiento, así que a velocidades bajas se queda "false" la
        // mayoría de los frames y parpadea a "true" de forma intermitente — con la condición OR
        // original eso hacía que el Agent se detuviera (isStopped=true + ResetPath()) y se
        // reactivara en cada parpadeo mientras la distancia se quedaba en la zona gris entre
        // stopDist y stopDist*1.2, visible como el compañero caminando a trompicones. Con
        // histéresis: una vez en marcha, sigue hasta volver a stopDist (ya cubierto por la rama de
        // parada de más abajo); una vez parado, no arranca hasta superar stopDist*1.2 de verdad.
        private bool _isFollowingSticky;

        // FIX (25 ago 2026) — "el party se queda atrás al esprintar y se teletransporta cerca,
        // viéndose feo" + el mismo trompiqueo de arriba pero visto desde el otro lado (el
        // compañero corriendo a una velocidad fija muy por encima del jugador cuando este va
        // despacio, así que adelanta y frena en seco una y otra vez). Causa raíz común: la
        // velocidad de seguimiento (walkSpeed/runSpeed del NPCPartyConfig, o SPECIAL_FOLLOW_SPEED
        // en el seguimiento especial) era una CONSTANTE fija sin relación con la velocidad real del
        // jugador — que va de ~5 caminando a 15 esprintando en _WILL.prefab, y baja mucho durante
        // secciones controladas (nado, seguir a un NPC despacio, lore pop-ups). Se estima la
        // velocidad real del jugador por posición (delta/deltaTime, suavizado) y se usa como suelo
        // dinámico para la velocidad de seguimiento en ambos caminos (NavMesh normal y seguimiento
        // especial), en vez de una constante pensada solo para el paso normal.
        private Vector3 _prevPlayerPosForSpeed;
        private float _smoothedPlayerSpeed;
        private bool _speedSampleInitialized;
        private const float PLAYER_SPEED_SMOOTH_RATE = 6f;   // /s, más alto = se adapta más rápido
        // Deltas de posición del jugador por encima de esta velocidad implícita se consideran un
        // teletransporte/warp (no locomoción real) y se ignoran para no disparar la media hacia un
        // pico de un solo frame — el sprint real más alto del proyecto es 15 (_WILL.prefab), así
        // que 30 deja margen de sobra sin colar teletransportes.
        private const float PLAYER_SPEED_TELEPORT_CUTOFF = 30f;

        // FIX (1 sep 2026) — continuación de INC-096: aunque Agent.speed sube dinámicamente hasta
        // runSpeed*2.5 para alcanzar al jugador en sprint, Agent.acceleration se quedaba en el
        // valor de fábrica del prefab (8 en Estela/Liam) sin relación con esa velocidad objetivo.
        // Con el destino recalculándose cada PATH_UPDATE_INTERVAL mientras el jugador sigue en
        // movimiento, el agente nunca llegaba a alcanzar de verdad la velocidad alta que se le
        // pedía — seguía quedándose atrás hasta cruzar distanciaParaTeletransporte y disparar el
        // teletransporte de emergencia ("de pronto aparecen" / pop-in en cámara). Escalamos la
        // aceleración junto con la velocidad en vez de dejarla fija.
        private const float AGENT_ACCELERATION_MULTIPLIER = 2.5f;
        private const float AGENT_MIN_ACCELERATION = 8f;

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
        private Animator _playerAnimator;
        private bool _inSpecialFollow;

        // _lastSpecialMode: modo actual (puede ser Default durante transiciones)
        // _exitMode: último modo SIGNIFICATIVO (Flying/Swimming/Climbing), usado para cleanup al salir
        // Esto evita que una transición transitoria por Default borre el contexto de vuelo/nado/escalada
        private ActionMode _lastSpecialMode = ActionMode.Default;
        private ActionMode _exitMode        = ActionMode.Default;

        private bool _storedGravity;
        private bool _gravityStored;
        private float _storedAnimSpeed = 1f;
        private bool _animSpeedStored;
        private int _npcFlightLayerIndex = 0;
        private GameObject _footVfxInstance;
        private ParticleSystem _footVfxPs;

        // Parámetros del Animator del NPC cacheados una vez por entrada al estado (no en Update).
        // Animator.SetBool/SetFloat loguean un error nativo si el hash no existe en el controller,
        // incluso dentro de try/catch (no lanzan excepción C#), así que hay que filtrar antes de llamar.
        private HashSet<int> _animatorParams;

        // Posición "lógica" durante el seguimiento especial (sin el offset visual de bobbing de vuelo).
        // Se usa para los cálculos de formación/distancia; el bobbing se aplica solo al Transform final.
        private Vector3 _specialFollowPosition;
        private float _bobPhase;

        // Estado persistente (no recalculado desde cero cada frame) para la histéresis de
        // isMoving en seguimiento especial, y valor suavizado del InputMagnitude de nado.
        private bool _isMovingSpecial;
        private float _swimInputMag;

        private const float SPECIAL_FOLLOW_SPEED    = 8f;
        private const float SPECIAL_FOLLOW_STOP_DIST = 1.5f;
        // Histéresis para el estado "moviéndose" en seguimiento especial (vuelo/nado/plataforma).
        // Con un único umbral, dist oscila frame a frame alrededor de él mientras el jugador nada
        // a velocidad constante, y el compañero arranca/frena en seco (y el blend de animación
        // salta entre 0 y el valor "moviéndose") en vez de decelerar suavemente. Con dos umbrales
        // separados, una vez que empieza a moverse no se para hasta estar bastante más cerca, y
        // viceversa.
        private const float SPECIAL_FOLLOW_MOVE_ENTER = SPECIAL_FOLLOW_STOP_DIST * 0.65f;
        private const float SPECIAL_FOLLOW_MOVE_EXIT   = SPECIAL_FOLLOW_STOP_DIST * 0.35f;
        // Velocidad de interpolación del parámetro InputMagnitude del nado (unidades/seg).
        // Evita que el blend tree de nado salte en seco entre "quieto" y "nadando".
        private const float SWIM_INPUT_MAG_LERP_SPEED = 3f;
        private const float OFF_NAVMESH_THRESHOLD   = 1.5f;
        private const float WARP_SEARCH_RADIUS      = 12f;
        // Valores reales usados por Will (_WILL.prefab), no los defaults de PlayerFlyingController:
        // el campo del script vale 0.8/1.8, pero el prefab lo tiene afinado a 0.18/1.2.
        private const float DEFAULT_FLIGHT_BOB_AMPLITUDE = 0.18f;
        private const float DEFAULT_FLIGHT_BOB_FREQUENCY = 1.2f;

        // Hashes de estados y parámetros (compartidos con el animator del jugador)
        private static readonly int HashFlyIdle    = Animator.StringToHash("fly_idle");
        private static readonly int HashFlyDive    = Animator.StringToHash("fly_dive");
        private static readonly int HashFlyLanding = Animator.StringToHash("Landing");
        private static readonly int HashLocomotion = Animator.StringToHash("Free Locomotion");
        private static readonly int HashSwimFloat  = Animator.StringToHash("Swimming_Floating_NoWeapon");
        private static readonly int HashClimbUp    = Animator.StringToHash("ClimbUp_RM_NoWeapon");
        private static readonly int HashClimbDown  = Animator.StringToHash("ClimbDown_RM_NoWeapon");
        private static readonly int HashClimbIdle  = Animator.StringToHash("ClimbIdle_RM_NoWeapon");
        private static readonly int HashIsFlying   = Animator.StringToHash("isFlying");
        private static readonly int HashIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int HashGroundDist = Animator.StringToHash("GroundDistance");
        private static readonly int HashInputMag   = Animator.StringToHash("InputMagnitude");

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

            _pathUpdateTimer   = 0f;
            _stateTimer        = 0f;
            _idleTimer         = 0f;
            _isInitialized     = false;
            _isWanderingNearPlayer = false;
            _inSpecialFollow   = false;
            _lastSpecialMode   = ActionMode.Default;
            _exitMode          = ActionMode.Default;
            _gravityStored       = false;
            _animSpeedStored     = false;
            _npcFlightLayerIndex = DetectFlightLayer(context.UnityAnimator);
            _animatorParams      = BuildAnimatorParamSet(context.UnityAnimator);
            _bobPhase            = (_partyMember?.PartyIndex ?? 0) * 0.7f;
            _footVfxInstance     = null;
            _footVfxPs           = null;
            _isMovingSpecial     = false;
            _swimInputMag        = 0f;
            _isFollowingSticky   = false;
            _speedSampleInitialized = false;
            _smoothedPlayerSpeed = 0f;

            // Ver NPCStateContext.IsActivelyFollowingPlayer: cubre también a compañeros
            // temporales (p.ej. el NPC de Will) que nunca pasan por PlayerParty.AddMember, para
            // que NPCBehaviourManagerV2 no los "duerma" por LOD de distancia mientras siguen.
            context.IsActivelyFollowingPlayer = true;

            if (context.Player != null)
            {
                _lastPlayerPosition = context.Player.position;
                _playerActionManager = context.Player.GetComponent<PlayerActionManager>();
                _playerAnimator = context.Player.GetComponent<Animator>();
            }

            if (context.Agent != null)
            {
                // FIX (16 ago 2026): isStopped/ResetPath sólo son válidos con el agente sobre el
                // NavMesh. PlayerParty.TeleportMemberToPlayer() puede mover el transform "a mano"
                // (member.transform.position = targetPos) cuando el agente no estaba sobre malla,
                // y llama a OnTeleportedToPlayer() -> StartFollowing() en el mismo frame: este
                // OnEnter se ejecuta con el agente todavía fuera de malla, y agent.isStopped = true
                // lanzaba "Stop can only be called on an active agent that has been placed on a
                // NavMesh." Igual que ya hace NavMeshAgentUtility.SafeSetStopped/HardStop.
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.isStopped = true;
                    context.Agent.ResetPath();
                }
                context.Agent.updatePosition = false;
                context.Agent.updateRotation = false;
                SetAgentSpeed(context.Agent, GetWalkSpeed());
            }

            context.Animator?.SetMovementSpeed(0f);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            if (context.Player == null) return;

            UpdatePlayerSpeedEstimate(context);

            _stateTimer += Time.deltaTime;

            // --- MODO ESPECIAL: vuelo, nado, escalada, o jugador fuera del NavMesh ---
            ActionMode playerMode     = _playerActionManager?.Top ?? ActionMode.Default;
            // Nota: NO incluir Climbing aquí — los companions no entrarán en special follow
            // durante la escalada del jugador; permanecerán en su lugar y serán teletransportados
            // al terminar la escalada. Además, si el jugador está fuera del NavMesh pero está en
            // modo Climbing, NO debemos entrar en special follow: el caso Climbing lo ignoramos.
            bool needsSpecialFollow = playerMode == ActionMode.Flying
                                   || playerMode == ActionMode.Swimming
                                   || (IsPlayerOffNavMesh(context) && playerMode != ActionMode.Climbing);

            if (needsSpecialFollow)
            {
                HandleSpecialFollow(context, playerMode);
                return;
            }

            // Salir del modo especial si el jugador volvió al suelo
            if (_inSpecialFollow)
            {
                ExitSpecialFollow(context);
                return;
            }

            // --- SEGUIMIENTO NORMAL POR NAVMESH ---
            if (context.Agent == null || !context.Agent.isOnNavMesh) return;

            if (_stateTimer < INITIAL_DELAY)
            {
                context.Agent.isStopped = true;
                context.Animator?.SetMovementSpeed(0f);
                return;
            }

            // FIX INC-028: margen de gracia tras restaurar posición/rotación persistida al
            // cargar partida (ver GameBootProfile.ApplyNpcPositionsToScene y
            // NPCStateContext.FollowGraceUntil). El jugador casi siempre se restaura solo a
            // un ancla aproximada (no a su Vector3/Quaternion exacto), así que nada más cargar
            // este NPC suele estar a más de stopDist de él — sin esta comprobación arrancaría
            // a caminar de inmediato y NPCSimpleAnimator.SyncWithNavMeshAgent() sustituiría la
            // rotación recién restaurada por la de marcha antes de que se llegue a ver quieto.
            if (Time.time < context.FollowGraceUntil)
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

            float distance  = Vector3.Distance(context.Transform.position, context.Player.position);
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
                _isFollowingSticky = false; // alcanzado al jugador: rearma la histéresis de arranque
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

            // FIX (25 ago 2026): decisión de moverse con histéresis persistente en vez de
            // "playerIsMoving || distance > stopDist*1.2" evaluado fresco cada frame — ver
            // comentario de _isFollowingSticky en la cabecera de la clase.
            _isFollowingSticky = _isFollowingSticky
                ? true // seguimos moviéndonos: la rama "distance <= stopDist" de más arriba ya
                       // corta el movimiento en cuanto realmente se alcanza al jugador
                : (playerIsMoving || distance > stopDist * 1.2f);

            if (_isFollowingSticky)
            {
                if (!context.Agent.updatePosition)
                {
                    if (context.Agent.isOnNavMesh)
                        context.Agent.nextPosition = context.Transform.position;
                    context.Agent.updatePosition = true;
                    context.Agent.updateRotation = false;
                }

                context.Agent.isStopped = false;
                // Suelo dinámico: nunca más lento que el ritmo real del jugador. El margen sobre
                // esa velocidad ya NO es un 15% fijo (FIX 25 ago 2026 anterior, KO en juego): con
                // un margen fijo, el NPC como mucho EMPATABA con el jugador en la práctica (el
                // suavizado de _smoothedPlayerSpeed tarda en converger tras cada arranque de
                // sprint, y el camino por NavMesh casi siempre rodea más que la línea recta que
                // usa PlayerParty para medir distancia), así que el hueco no se cerraba de verdad:
                // oscilaba justo alrededor del umbral de teletransporte de emergencia y disparaba
                // el warp una y otra vez en vez de evitarlo (visto en juego: "Liam/Estela demasiado
                // lejos" repetido cada pocos segundos durante un sprint sostenido). Ahora el margen
                // crece con la propia distancia — igual que ya hacía HandleSpecialFollow más abajo
                // (baseSpecialSpeed*1.5 cuando dist > STOP_DIST*3) para nado/vuelo, pero aquí nunca
                // se había traído al seguimiento normal por NavMesh: cerca de distanciaParaCorrer
                // el margen sigue siendo un 15% discreto, y crece hasta un 60% al acercarse al
                // umbral de teletransporte de este NPC — así el NPC gana terreno de verdad cuanto
                // más atrás se queda, en vez de solo no perder más.
                float catchUpDistance = _config?.distanciaParaTeletransporte ?? 25f;
                float catchUpT = Mathf.Clamp01((distance - runDist) / Mathf.Max(1f, catchUpDistance - runDist));
                float speedMargin = Mathf.Lerp(1.15f, 1.6f, catchUpT);
                float dynamicRunSpeed = Mathf.Clamp(_smoothedPlayerSpeed * speedMargin, runSpeed, runSpeed * 2.5f);
                SetAgentSpeed(context.Agent, distance > runDist ? dynamicRunSpeed : walkSpeed);

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

            // Primera entrada o cambio de modo
            if (!_inSpecialFollow || modeChanged)
            {
                if (!_inSpecialFollow)
                {
                    // Primer frame: desactivar NavMesh y gravedad
                    _inSpecialFollow = true;
                    _pathUpdateTimer = 0f;
                    _isWanderingNearPlayer = false;
                    _specialFollowPosition = context.Transform.position;

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
                        if (!context.Rigidbody.isKinematic)
                            context.Rigidbody.linearVelocity = Vector3.zero;
                        _gravityStored = true;
                    }
                }

                OnSpecialModeEnter(context, playerMode);
                _lastSpecialMode = playerMode;
            }

            // Calcular posición objetivo y mover
            // Si el party member está en preparación para trepar, usamos la Y de la base
            // de escalada y fijamos la posición objetivo en la base hasta que esté listo.
            Vector3 targetPos;
            if (_partyMember != null && _partyMember.IsWaitingForClimb)
            {
                // Recalcular la posición de formación pero manteniendo la Y de la base
                Vector3 formation = GetFormationTarget3D(context);
                var baseY = _partyMember.ClimbBasePosition.y;
                targetPos = new Vector3(formation.x, baseY, formation.z);

                // Si estamos lo bastante cerca del slot horizontalmente, confirmar inicio de trepa
                float horizDist = Vector3.Distance(new Vector3(context.Transform.position.x, 0f, context.Transform.position.z),
                                                   new Vector3(targetPos.x, 0f, targetPos.z));
                const float READY_TO_CLIMB_DISTANCE = 0.8f;
                if (horizDist <= READY_TO_CLIMB_DISTANCE)
                {
                    // Liberar la preparación para que el NPC comience a seguir la Y del player
                    try { _partyMember.CancelClimbPreparation(); } catch { }
                    // Re-evaluar targetPos: ahora usar la posición normal (incluyendo Y del jugador)
                    targetPos = GetFormationTarget3D(context);
                }
            }
            else
            {
                targetPos  = GetFormationTarget3D(context);
            }
            // _specialFollowPosition es la posición "lógica" (sin bobbing) usada para toda la
            // navegación/formación. El offset visual de vuelo se aplica solo al final, sobre
            // el Transform real, para no contaminar los cálculos de distancia/destino.
            Vector3 currentPos = _specialFollowPosition;
            Vector3 delta      = targetPos - currentPos;
            float   dist       = delta.magnitude;

            // Histéresis: si ya se estaba moviendo, sigue hasta bajar de MOVE_EXIT;
            // si estaba parado, no arranca hasta superar MOVE_ENTER. Evita el parpadeo
            // frame a frame de isMoving (y el consiguiente movimiento/animación a trompicones)
            // cuando dist oscila justo alrededor de un único umbral.
            _isMovingSpecial = _isMovingSpecial
                ? dist > SPECIAL_FOLLOW_MOVE_EXIT
                : dist > SPECIAL_FOLLOW_MOVE_ENTER;
            bool isMoving = _isMovingSpecial;

            if (isMoving)
            {
                // FIX (25 ago 2026): igual que en el seguimiento normal, la velocidad base sigue el
                // ritmo real del jugador (nado lento, vuelo, plataformas) en vez de una constante
                // fija — evita que el compañero adelante y frene en seco quien va despacio.
                float baseSpecialSpeed = Mathf.Max(_smoothedPlayerSpeed * 1.15f, SPECIAL_FOLLOW_SPEED * 0.5f);
                float speed = dist > SPECIAL_FOLLOW_STOP_DIST * 3f
                    ? baseSpecialSpeed * 1.5f : baseSpecialSpeed;

                _specialFollowPosition = currentPos + delta.normalized * Mathf.Min(speed * Time.deltaTime, dist);

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

            // Bobbing visual de vuelo: replica el movimiento arriba/abajo que PlayerFlyingController
            // aplica al jugador (Mathf.Sin sobre Time.time), para que los compañeros que vuelan
            // también parezcan flotar en el aire. Solo afecta al Transform final, no a la lógica.
            float bobOffset = 0f;
            if (playerMode == ActionMode.Flying)
            {
                float amp  = _config?.flightBobAmplitude ?? DEFAULT_FLIGHT_BOB_AMPLITUDE;
                float freq = _config?.flightBobFrequency ?? DEFAULT_FLIGHT_BOB_FREQUENCY;
                if (Mathf.Abs(amp) > 0.0001f && Mathf.Abs(freq) > 0.0001f)
                    bobOffset = Mathf.Sin(Time.time * (2f * Mathf.PI * freq) + _bobPhase) * amp;
            }
            context.Transform.position = _specialFollowPosition + Vector3.up * bobOffset;

            UpdateSpecialModeAnimation(context, playerMode, delta, isMoving);
        }

        /// <summary>
        /// Animación de entrada al activar un modo especial.
        /// Solo Flying/Swimming/Climbing actualizan _exitMode (modo "significativo").
        /// Las transiciones transitorias por Default no sobreescriben _exitMode,
        /// así ExitSpecialFollow sabe siempre de qué modo real salimos.
        /// </summary>
        private void OnSpecialModeEnter(NPCStateContext context, ActionMode mode)
        {
            Animator anim = context.UnityAnimator;

            // Seguridad adicional: ignorar explícitamente Climbing para evitar que
            // NPCs repliquen la animación de trepa aunque llegue aquí por algún camino raro.
            if (mode == ActionMode.Climbing)
                return;

            // Si salimos del modo vuelo hacia cualquier otro, limpiar isFlying inmediatamente
            // para que no quede el personaje en fly_idle durante la transición transitoria
            if (_lastSpecialMode == ActionMode.Flying && mode != ActionMode.Flying)
            {
                TrySetBool(anim, HashIsFlying, false);
                StopFootVfx();
            }

            switch (mode)
            {
                case ActionMode.Flying:
                    _exitMode = mode;
                    TrySetBool(anim, HashIsFlying, true);
                    TryPlay(anim, HashFlyIdle, _npcFlightLayerIndex);
                    SpawnFootVfx(context);
                    break;

                case ActionMode.Swimming:
                    _exitMode = mode;
                    TryPlay(anim, HashSwimFloat, 0);
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    break;

                case ActionMode.Climbing:
                    _exitMode = mode;
                    if (!_animSpeedStored && anim != null)
                    {
                        _storedAnimSpeed = anim.speed;
                        _animSpeedStored = true;
                    }
                    TryPlay(anim, HashClimbIdle, 0);
                    if (anim != null) anim.speed = 0f;
                    break;

                // Default (plataforma sin NavMesh / salto): espejamos el estado aéreo inmediatamente
                default:
                    if (_playerAnimator != null && anim != null)
                    {
                        TrySetBool(anim, HashIsGrounded, _playerAnimator.GetBool(HashIsGrounded));
                        TrySetFloat(anim, HashGroundDist, _playerAnimator.GetFloat(HashGroundDist));
                    }
                    break;
            }
        }

        /// <summary>Actualización de animación cada frame según modo y movimiento.</summary>
        private void UpdateSpecialModeAnimation(NPCStateContext context, ActionMode mode,
                                                Vector3 delta, bool isMoving)
        {
            Animator anim = context.UnityAnimator;
            if (anim == null) return;

            // Seguridad adicional: no ejecutar la rama de Climbing — el party no debe trepar.
            if (mode == ActionMode.Climbing) return;

            switch (mode)
            {
                case ActionMode.Flying:
                    TryPlay(anim, isMoving ? HashFlyDive : HashFlyIdle, _npcFlightLayerIndex);
                    break;

                case ActionMode.Swimming:
                    TrySetBool(anim, HashIsGrounded, true);
                    TrySetFloat(anim, HashGroundDist, 0f);
                    // Interpolado, no asignado en seco: InputMagnitude es a la vez el blend
                    // parameter del blend tree de nado y el speed parameter de al menos un
                    // estado, así que un salto discreto entre 0 y 0.5 se ve como un tirón tanto
                    // en la pose como en el ritmo de reproducción del clip.
                    float targetInputMag = isMoving ? 0.5f : 0f;
                    _swimInputMag = Mathf.MoveTowards(_swimInputMag, targetInputMag, SWIM_INPUT_MAG_LERP_SPEED * Time.deltaTime);
                    TrySetFloat(anim, HashInputMag, _swimInputMag);
                    break;

                case ActionMode.Climbing:
                    if (Mathf.Abs(delta.y) > 0.05f)
                    {
                        anim.speed = 1f;
                        TryCrossFade(anim, delta.y > 0f ? HashClimbUp : HashClimbDown, 0.05f, 0);
                    }
                    else
                    {
                        anim.speed = 0f;
                    }
                    TrySetBool(anim, HashIsGrounded, true);
                    break;

                default:
                    // Plataforma/salto sin NavMesh: locomoción normal + replicar estado aéreo del jugador
                    context.Animator?.SetMovementSpeed(Mathf.Clamp01(delta.magnitude / 3f));
                    if (_playerAnimator != null && anim != null)
                    {
                        bool playerGrounded = _playerAnimator.GetBool(HashIsGrounded);
                        float playerGroundDist = _playerAnimator.GetFloat(HashGroundDist);
                        TrySetBool(anim, HashIsGrounded, playerGrounded);
                        TrySetFloat(anim, HashGroundDist, playerGroundDist);
                    }
                    break;
            }
        }

        /// <summary>
        /// Sale del modo especial: restaura física y animator, snappea al NavMesh.
        /// Usa _exitMode (último modo significativo) para saber qué animación de salida reproducir.
        /// </summary>
        private void ExitSpecialFollow(NPCStateContext context)
        {
            Animator anim         = context.UnityAnimator;
            ActionMode exitingMode = _exitMode;   // último modo significativo (Flying/Swimming/Climbing/Default)

            // Quitar cualquier offset visual de bobbing residual antes de restaurar física/navmesh
            context.Transform.position = _specialFollowPosition;

            _inSpecialFollow = false;
            _lastSpecialMode = ActionMode.Default;
            _exitMode        = ActionMode.Default;

            // Restaurar velocidad del animator (escalada la pausa)
            if (_animSpeedStored && anim != null)
            {
                anim.speed = _storedAnimSpeed;
                _animSpeedStored = false;
            }

            // Siempre limpiar isFlying al salir para que no quede el personaje en fly_idle
            TrySetBool(anim, HashIsFlying, false);

            // Animación de salida según el modo real del que venimos
            if (anim != null)
            {
                switch (exitingMode)
                {
                    case ActionMode.Flying:
                        TrySetBool(anim, HashIsGrounded, true);
                        TrySetFloat(anim, HashGroundDist, 0f);
                        StopFootVfx();
                        if (!TryCrossFade(anim, HashFlyLanding, 0.08f, _npcFlightLayerIndex))
                            TryCrossFade(anim, HashLocomotion, 0.1f, _npcFlightLayerIndex);
                        break;

                    case ActionMode.Swimming:
                    case ActionMode.Climbing:
                        TryCrossFade(anim, HashLocomotion, 0.1f, 0);
                        break;

                    default:
                        // Plataforma/salto: restaurar estado grounded para no quedar en mid-air
                        TrySetBool(anim, HashIsGrounded, true);
                        TrySetFloat(anim, HashGroundDist, 0f);
                        break;
                }
            }

            // Restaurar gravedad
            if (context.Rigidbody != null && _gravityStored)
            {
                context.Rigidbody.useGravity = _storedGravity;
                if (!context.Rigidbody.isKinematic)
                    context.Rigidbody.linearVelocity = Vector3.zero;
                _gravityStored = false;
            }

            // Snap al NavMesh y devolver control al agent
            if (context.Agent != null && context.Agent.isActiveAndEnabled)
            {
                // Activar updatePosition ANTES del Warp para que éste actualice el transform
                context.Agent.updatePosition = true;

                if (!context.Agent.isOnNavMesh)
                {
                    if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit hit, WARP_SEARCH_RADIUS, NavMesh.AllAreas))
                    {
                        // Mover el transform directamente además del Warp como garantía extra
                        context.Transform.position = hit.position;
                        context.Agent.Warp(hit.position);
                    }
                }
                else
                {
                    context.Agent.nextPosition = context.Transform.position;
                }

                context.Agent.isStopped = true;
                context.Agent.updateRotation = false;
            }
        }

        // =========================================================================
        // FORMACIÓN 3D
        // =========================================================================

        /// <summary>
        /// Posición de formación en 3D: detrás del jugador en XZ, misma Y.
        /// idx 0: centro  idx 1: +0.8m  idx 2: -0.8m  idx 3: +1.6m …
        /// </summary>
        private Vector3 GetFormationTarget3D(NPCStateContext context)
        {
            float followDist = _config?.distanciaParaPararse ?? DEFAULT_STOP_DISTANCE;
            Vector3 behind   = -context.Player.forward * (followDist * 0.8f);

            int   partyIdx = _partyMember?.PartyIndex ?? 0;
            float spread   = _config?.flightFormationSpread ?? 1.5f;
            float lateral  = partyIdx == 0 ? 0f
                : (partyIdx % 2 == 1 ? 1f : -1f) * spread * Mathf.Ceil(partyIdx * 0.5f);
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

        /// <summary>
        /// Detecta el layer del animator del NPC que contiene los estados de vuelo.
        /// Equivalente a PlayerFlyingController.DetectFlightLayer para evitar hardcodear layer 0.
        /// </summary>
        private static int DetectFlightLayer(Animator anim)
        {
            if (anim == null) return 0;
            int hash = Animator.StringToHash("fly_idle");
            for (int i = 0; i < anim.layerCount; i++)
            {
                try { if (anim.HasState(i, hash)) return i; }
                catch { }
            }
            return 0;
        }

        private void SpawnFootVfx(NPCStateContext context)
        {
            var prefab = _config?.footVfxPrefab;
            if (prefab == null || _footVfxInstance != null) return;
            _footVfxInstance = Object.Instantiate(prefab, context.Transform);
            _footVfxPs = _footVfxInstance.GetComponent<ParticleSystem>();
            _footVfxInstance.SetActive(true);
            if (_footVfxPs != null && !_footVfxPs.isPlaying) _footVfxPs.Play();
        }

        private void StopFootVfx()
        {
            if (_footVfxInstance == null) return;
            if (_footVfxPs != null) _footVfxPs.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(_footVfxInstance);
            _footVfxInstance = null;
            _footVfxPs = null;
        }

        private static void TryPlay(Animator anim, int stateHash, int layer)
        {
            if (anim == null) return;
            try
            {
                if (anim.HasState(layer, stateHash) &&
                    anim.GetCurrentAnimatorStateInfo(layer).shortNameHash != stateHash)
                    anim.Play(stateHash, layer);
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
                if (anim.GetCurrentAnimatorStateInfo(layer).shortNameHash != stateHash)
                    anim.CrossFade(stateHash, duration, layer);
                return true;
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Snapshot de los hashes de parámetros del Animator, tomado una sola vez al entrar al estado.
        /// No apto para llamar en Update: recorre anim.parameters, que es costoso.
        /// </summary>
        private static HashSet<int> BuildAnimatorParamSet(Animator anim)
        {
            var set = new HashSet<int>();
            if (anim == null) return set;
            try
            {
                var parameters = anim.parameters;
                for (int i = 0; i < parameters.Length; i++)
                    set.Add(parameters[i].nameHash);
            }
            catch { }
            return set;
        }

        private void TrySetBool(Animator anim, int paramHash, bool value)
        {
            if (anim == null) return;
            if (_animatorParams != null && !_animatorParams.Contains(paramHash)) return;
            try { anim.SetBool(paramHash, value); } catch { }
        }

        private void TrySetFloat(Animator anim, int paramHash, float value)
        {
            if (anim == null) return;
            if (_animatorParams != null && !_animatorParams.Contains(paramHash)) return;
            try { anim.SetFloat(paramHash, value); } catch { }
        }

        // =========================================================================
        // HELPERS ORIGINALES
        // =========================================================================

        /// <summary>
        /// Estima la velocidad real actual del jugador por delta de posición (suavizada), para usar
        /// como suelo dinámico de la velocidad de seguimiento en vez de constantes fijas — ver
        /// comentario de _smoothedPlayerSpeed en la cabecera. Ignora saltos de posición enormes en un
        /// solo frame (teletransportes) para que no disparen la media hacia un pico irreal.
        /// </summary>
        private void UpdatePlayerSpeedEstimate(NPCStateContext context)
        {
            if (!_speedSampleInitialized)
            {
                _prevPlayerPosForSpeed = context.Player.position;
                _speedSampleInitialized = true;
                return;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = context.Player.position - _prevPlayerPosForSpeed;
            _prevPlayerPosForSpeed = context.Player.position;

            float instantaneousSpeed = delta.magnitude / dt;
            if (instantaneousSpeed > PLAYER_SPEED_TELEPORT_CUTOFF)
                return; // teletransporte/warp: no es locomoción real, no contaminar la media

            _smoothedPlayerSpeed = Mathf.Lerp(_smoothedPlayerSpeed, instantaneousSpeed,
                Mathf.Clamp01(PLAYER_SPEED_SMOOTH_RATE * dt));
        }

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

            // FIX: este cálculo daba el MISMO punto exacto detrás del jugador a todos los
            // compañeros (sin spread por índice), a diferencia de PlayerParty.GetFormationPosition
            // (ya usado para teletransportar al equipo en formación — TeleportMemberToPlayer), que sí
            // reparte a cada miembro en abanico (±60° detrás del jugador, vía CalculateFormationAngle).
            // NPCPartyConfig.offsetLateral existía para esto mismo ("para que no esté exactamente
            // detrás") pero nunca se leía en ningún sitio. Con 2+ compañeros siguiendo a pie
            // convergían en el mismo punto y se empujaban/superponían entre sí — visualmente uno
            // parecía ir "a cuestas" del otro. Se reutiliza la formación ya validada en vez de
            // reimplementar el cálculo aquí.
            Vector3 targetPos = preferBehind && _partyMember != null
                ? _partyMember.GetFormationPosition()
                : context.Player.position + (context.Transform.position - context.Player.position).normalized * followDist;

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

        /// <summary>
        /// Fija Agent.speed y escala Agent.acceleration junto con ella (ver comentario de
        /// AGENT_ACCELERATION_MULTIPLIER) para que el NavMeshAgent pueda alcanzar de verdad
        /// velocidades altas de catch-up en vez de quedarse limitado por la aceleración fija
        /// serializada en el prefab.
        /// </summary>
        private static void SetAgentSpeed(NavMeshAgent agent, float speed)
        {
            agent.speed = speed;
            agent.acceleration = Mathf.Max(AGENT_MIN_ACCELERATION, speed * AGENT_ACCELERATION_MULTIPLIER);
        }

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
            SetAgentSpeed(context.Agent, _config.velocidadCaminando);
            context.Agent.SetDestination(_wanderTarget);
        }

        public override void OnExit(NPCStateContext context)
        {
            context.IsActivelyFollowingPlayer = false;

            if (_inSpecialFollow)
            {
                _inSpecialFollow = false;

                // Quitar cualquier offset visual de bobbing residual
                context.Transform.position = _specialFollowPosition;

                if (_animSpeedStored && context.UnityAnimator != null)
                {
                    context.UnityAnimator.speed = _storedAnimSpeed;
                    _animSpeedStored = false;
                }

                // Siempre limpiar isFlying al salir del estado
                TrySetBool(context.UnityAnimator, HashIsFlying, false);
                StopFootVfx();

                if (context.Rigidbody != null && _gravityStored)
                {
                    context.Rigidbody.useGravity = _storedGravity;
                    context.Rigidbody.linearVelocity = Vector3.zero;
                    _gravityStored = false;
                }
            }

            if (context.Agent != null && context.Agent.isActiveAndEnabled)
            {
                context.Agent.updatePosition = true;
                if (context.Agent.isOnNavMesh)
                {
                    context.Agent.isStopped = true;
                    context.Agent.nextPosition = context.Transform.position;
                }
                else if (NavMesh.SamplePosition(context.Transform.position, out NavMeshHit hit, WARP_SEARCH_RADIUS, NavMesh.AllAreas))
                {
                    context.Transform.position = hit.position;
                    context.Agent.Warp(hit.position);
                }
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
