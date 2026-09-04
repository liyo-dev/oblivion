using System;
using UnityEngine;
using UnityEngine.AI;
using Game.NPC.Common;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// "Vida artificial" del grupo — primera pieza de la PARTE D.2 del TDD (idle del grupo
    /// jugable). De momento solo Estela: mientras va siguiendo al jugador con normalidad
    /// (FollowPlayerState, no controlada por el jugador, no en combate/cinemática/diálogo), de vez
    /// en cuando suelta un comentario suelto sin dejar de andar, se sienta a decir que tiene hambre,
    /// o se planta un rato delante del jugador quejándose de aburrimiento. Es puramente cosmético:
    /// no bloquea nada del gameplay real y se corta solo si el jugador entra en combate, la cambia
    /// a ella como personaje activo, el grupo pasa a modo Libre, o entra cualquier cinemática.
    ///
    /// Implementación: reutiliza CinematicState/CinematicSequence (Game.NPC.States.CinematicState),
    /// el mismo mecanismo que ya usan las secuencias narrativas (ReinoExitBanterSequencer,
    /// NPCBehaviourManagerV2.MoveToPosition/StartCinematicSequence). Es importante NO dejar a
    /// Estela en un estado literalmente llamado "Idle" mientras dura el numerito: NPCPartyMember.
    /// Update() sondea cada 0.5s y, si ve currentState.StateName == "Idle" con el equipo en modo
    /// Siguiendo, la vuelve a poner a seguir inmediatamente (mismo mecanismo que el FIX INC-059
    /// documentado ahí para el sentado de la taberna) — deshaciendo el numerito a media frase.
    /// CinematicState sí es inmune a ese sondeo (pone Context.IsInCinematic = true mientras dura),
    /// y al terminar la secuencia vuelve a IdleState de forma normal — momento en el que ese mismo
    /// sondeo de NPCPartyMember la pone a seguir otra vez sola, así que no hace falta gestionar el
    /// "reanudar seguimiento" a mano aquí.
    ///
    /// Cómo activarlo: añadir este componente al mismo GameObject de Estela que ya tiene
    /// NPCPartyMember/NPCBehaviourManagerV2 (el prefab o la instancia en escena). No hace falta
    /// tocar nada más — se auto-desactiva solo mientras no toque (combate, diálogo, personaje
    /// controlado, modo Libre, etc.) y no hace nada si esos campos se dejan vacíos.
    /// </summary>
    // OJO: a propósito SIN [RequireComponent(typeof(NPCPartyMember))]. NPCPartyMember lo añade
    // NPCBehaviourManagerV2.EnsureRequiredComponents() a mano, con partyMember.SetConfig(...) —
    // si RequireComponent lo auto-instancia antes (Unity lo hace al construir el GameObject,
    // antes de que corra ningún Awake()), EnsureRequiredComponents() se lo encuentra ya
    // presente y se SALTA el SetConfig(), dejando el PartyConfig sin asignar — así es como se
    // rompió el party de Estela y Liam (INC-149/INC-151). GetComponent<NPCPartyMember>() en
    // Start() ya maneja con seguridad el caso null (ver IsEligibleNow) sin necesitar el atributo.
    [RequireComponent(typeof(NPCBehaviourManagerV2))]
    public class EstelaIdleCommentary : MonoBehaviour
    {
        [Header("Frecuencia")]
        [Tooltip("Segundos mínimos entre numeritos mientras va siguiendo al jugador con normalidad.")]
        [SerializeField] private float minInterval = 25f;
        [Tooltip("Segundos máximos entre numeritos mientras va siguiendo al jugador con normalidad.")]
        [SerializeField] private float maxInterval = 55f;

        [Header("Comentario suelto (sin dejar de andar)")]
        [SerializeField] private string[] ambientComments =
        {
            "¿Cuánto queda ya?",
            "Qué callados vais todos...",
            "Se está bien caminando así, ¿eh?",
            "Oye, ¿y si paramos a descansar un rato?",
        };

        [Header("Sentarse a quejarse de hambre")]
        [SerializeField] private string[] hungryLines =
        {
            "Tengo hambre...",
            "¿Nadie más tiene hambre? Yo sí.",
            "Con lo bien que se comía en el último pueblo...",
        };
        [Tooltip("Cuánto tiempo se queda sentada antes de levantarse y seguir andando.")]
        [SerializeField] private float sitDuration = 4f;

        [Header("Plantarse delante a molestar")]
        [SerializeField] private string[] boredLines =
        {
            "¡Me aburro! ¿Podemos hacer algo?",
            "Esto es un rollo, ¿no os parece?",
            "¿Ya hemos llegado? ¿Y ahora? ¿Y ahora?",
        };
        [Tooltip("Distancia delante del jugador a la que intenta plantarse.")]
        [SerializeField] private float blockDistanceInFront = 2.2f;
        [Tooltip("Cuánto tiempo se queda plantada delante antes de dejar seguir andando.")]
        [SerializeField] private float blockStandDuration = 3.5f;
        [Tooltip("Tiempo máximo caminando hacia el punto delante del jugador antes de rendirse y solo comentar.")]
        [SerializeField] private float blockMaxWalkDuration = 6f;

        [Header("Bocadillo")]
        [SerializeField] private float bubbleDuration = 3f;

        [Header("Peso relativo de cada numerito (no hace falta que sumen 100)")]
        [SerializeField] private float weightComment = 3f;
        [SerializeField] private float weightSit = 2f;
        [SerializeField] private float weightBlock = 1.5f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private NPCPartyMember _partyMember;
        private NPCBehaviourManagerV2 _npcManager;
        private float _nextTriggerTime;
        private bool _timerArmed;

        // ✅ MEJORA (1 sep 2026, petición de Raúl: dar la misma vida a Will/Liam/Eldran): referencia
        // a la acción de "sentarse con hambre" actualmente en curso (si hay alguna), para que otro
        // compañero (ver LiamIdleCommentary) pueda pedirle a Estela que se levante antes de tiempo
        // como pago de su propio numerito, sin tener que tocar el NPCStateContext de Estela desde
        // fuera.
        private EstelaSitAction _activeSitAction;

        // Evento estático: se dispara justo cuando Estela se sienta a quejarse de hambre, para que
        // otro compañero (Liam) pueda reaccionar. Solo transporta el Transform de Estela — nada de
        // estado pesado. Patrón de reset obligatorio para estado estático (CLAUDE.md §3), evita
        // contaminación entre sesiones de PlayMode en el Editor.
        public static event Action<Transform> EstelaSatDownHungry;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { EstelaSatDownHungry = null; }
#endif

        /// <summary>
        /// Llamado desde fuera (LiamIdleCommentary) para pedirle a Estela que corte su numerito de
        /// "sentarse con hambre" antes de tiempo. No-op si no está sentada en ese momento — seguro
        /// de llamar siempre.
        /// </summary>
        public void RequestStandUp() => _activeSitAction?.ForceComplete();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float _nextDiagnosticLogTime;
#endif

        // NPCPartyMember se añade en tiempo de ejecución dentro del propio Awake() de
        // NPCBehaviourManagerV2 (EnsureRequiredComponents) — Unity no garantiza qué Awake()
        // de los dos corre primero en el mismo GameObject. Por eso el cacheo va en Start()
        // (que Unity sí garantiza que corre después de TODOS los Awake() de la escena) y no en
        // Awake() pese a la convención habitual del proyecto — si esto corriera en Awake(),
        // _partyMember podría quedarse en null para siempre según el orden de ejecución.
        void Start()
        {
            _partyMember = GetComponent<NPCPartyMember>();
            _npcManager = GetComponent<NPCBehaviourManagerV2>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Diagnóstico (15 ago 2026): si esta línea no aparece en consola al arrancar la
            // escena, el componente no está añadido al GameObject (o el GameObject está
            // desactivado) — la causa más probable de "no ha pasado nada" al probarlo. Si aparece
            // pero nunca se dispara ningún numerito, ver los logs periódicos de IsEligibleNow más
            // abajo (uno cada 5s explicando qué condición no se cumple todavía).
            Debug.Log($"[EstelaIdleCommentary:{name}] Componente activo. partyMember={(_partyMember != null)}, npcManager={(_npcManager != null)}.");
#endif
        }

        void Update()
        {
            if (!IsEligibleNow())
            {
                // Se re-arma solo en cuanto vuelva a cumplir condiciones — no hace falta acordarse
                // de "cuánto le faltaba" de antes de dejar de cumplirlas (p.ej. tras un combate).
                _timerArmed = false;
                return;
            }

            if (!_timerArmed)
            {
                ArmNextTimer();
                return;
            }

            if (Time.time >= _nextTriggerTime)
            {
                TriggerRandomBit();
                ArmNextTimer();
            }
        }

        private void ArmNextTimer()
        {
            _timerArmed = true;
            float lo = Mathf.Min(minInterval, maxInterval);
            float hi = Mathf.Max(minInterval, maxInterval);
            _nextTriggerTime = Time.time + UnityEngine.Random.Range(lo, hi);
        }

        /// <summary>
        /// Solo dispara mientras Estela va siguiendo al jugador con total normalidad. Cualquier
        /// otra circunstancia (combate, diálogo, cinemática, personaje controlado por el jugador,
        /// modo Libre del equipo, fuera de NavMesh por ir volando/nadando/escalando junto al
        /// jugador) la deja pasar de largo sin disparar nada.
        /// </summary>
        private bool IsEligibleNow()
        {
            if (_partyMember == null || _npcManager == null)
            {
                LogIneligible("falta NPCPartyMember o NPCBehaviourManagerV2 en este GameObject");
                return false;
            }
            if (!_partyMember.IsActiveInParty)
            {
                LogIneligible("IsActiveInParty=false (no está en el party ahora mismo, o está en combate/cinemática/otra secuencia activa)");
                return false;
            }
            if (ActiveCharacterSwapper.Instance != null && ActiveCharacterSwapper.Instance.HiddenNpc == _partyMember)
            {
                LogIneligible("es el personaje que controla el jugador ahora mismo");
                return false;
            }
            if (!(PartyControlManager.Instance?.IsPartyFollowing ?? true))
            {
                LogIneligible("el equipo está en modo Libre (no Siguiendo)");
                return false;
            }
            if (!(_npcManager.Brain?.CurrentState is FollowPlayerState))
            {
                LogIneligible($"Brain.CurrentState es '{_npcManager.Brain?.CurrentState?.StateName ?? "null"}', no FollowPlayerState");
                return false;
            }
            if (_npcManager.Agent == null || !_npcManager.Agent.isOnNavMesh)
            {
                LogIneligible("el NavMeshAgent es null o no está sobre el NavMesh (volando/nadando/escalando junto al jugador)");
                return false;
            }
            if (MenuManager.AnyOpen())
            {
                LogIneligible("hay un menú abierto");
                return false;
            }
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Diagnóstico (15 ago 2026): tras el aviso de Raúl de que "nada de esto ha funcionado" al
        // probarlo, sin más pista que esa. Antes IsEligibleNow fallaba en silencio — indistinguible
        // entre "el componente no está añadido", "está en combate", "el equipo está en modo Libre",
        // etc. Un log por frame sería spam ilegible, así que se limita a uno cada 5s (igual que el
        // patrón ya usado en SeekShelterState.LogNoShelterFound).
        private void LogIneligible(string reason)
        {
            if (Time.time < _nextDiagnosticLogTime) return;
            _nextDiagnosticLogTime = Time.time + 5f;
            Debug.Log($"[EstelaIdleCommentary:{name}] Sin disparar todavía — {reason}.");
        }
#else
        private void LogIneligible(string reason) { }
#endif

        private void TriggerRandomBit()
        {
            float total = Mathf.Max(0.01f, weightComment) + Mathf.Max(0.01f, weightSit) + Mathf.Max(0.01f, weightBlock);
            float roll = UnityEngine.Random.Range(0f, total);

            if (roll < weightComment)
                TriggerAmbientComment();
            else if (roll < weightComment + weightSit)
                TriggerSit();
            else
                TriggerBlockPlayer();
        }

        private void TriggerAmbientComment()
        {
            string line = PickLine(ambientComments);
            if (string.IsNullOrEmpty(line)) return;

            if (debugMode) Debug.Log($"[EstelaIdleCommentary] Comentario suelto: \"{line}\"");
            SpeechBubbleUI.Instance?.Show(transform, line, bubbleDuration);
        }

        private void TriggerSit()
        {
            string line = PickLine(hungryLines);
            if (debugMode) Debug.Log($"[EstelaIdleCommentary] Se sienta a quejarse de hambre: \"{line}\"");

            var action = new EstelaSitAction(sitDuration, line, bubbleDuration);
            _activeSitAction = action;

            var sequence = new CompositeSequence();
            sequence.AddAction(action);
            _npcManager.StartCinematicSequence(sequence);

            EstelaSatDownHungry?.Invoke(transform);
        }

        private void TriggerBlockPlayer()
        {
            var player = _npcManager.Player;
            if (player == null) { TriggerAmbientComment(); return; }

            Vector3 desired = player.position + player.forward * blockDistanceInFront;
            if (!NavMesh.SamplePosition(desired, out var hit, 3f, NavMesh.AllAreas))
            {
                // Sin punto de NavMesh válido delante del jugador (borde de un muelle, escalón,
                // etc.): mejor no mandarla a caminar a ciegas, se conforma con un comentario suelto.
                TriggerAmbientComment();
                return;
            }

            string line = PickLine(boredLines);
            if (debugMode) Debug.Log($"[EstelaIdleCommentary] Se planta delante a molestar: \"{line}\"");

            var sequence = new CompositeSequence();
            sequence.AddAction(new EstelaBlockPlayerAction(hit.position, player, blockStandDuration, line, bubbleDuration, blockMaxWalkDuration));
            _npcManager.StartCinematicSequence(sequence);
        }

        private string PickLine(string[] lines)
        {
            if (lines == null || lines.Length == 0) return null;
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }
    }

    /// <summary>
    /// CinematicAction: se sienta en el sitio, suelta un bocadillo y espera un rato antes de que la
    /// secuencia termine (momento en el que NPCPartyMember la pone a seguir otra vez sola, ver
    /// cabecera de EstelaIdleCommentary).
    /// </summary>
    internal class EstelaSitAction : CinematicAction
    {
        private readonly float _duration;
        private readonly string _line;
        private readonly float _bubbleDuration;

        private bool _started;
        private float _timer;

        public EstelaSitAction(float duration, string line, float bubbleDuration)
        {
            _duration = Mathf.Max(0.1f, duration);
            _line = line;
            _bubbleDuration = bubbleDuration;
        }

        /// <summary>
        /// Corta el numerito ya mismo (ver EstelaIdleCommentary.RequestStandUp). Marca completado
        /// tal cual — Cleanup() ya se llama sea cual sea el motivo de la finalización (normal o
        /// interrumpida), así que esto por sí solo ya deja a Estela de pie de nuevo.
        /// </summary>
        internal void ForceComplete() => IsCompleted = true;

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;

            if (!_started)
            {
                _started = true;

                if (context.Agent != null)
                    NavMeshAgentUtility.HardStop(context.Agent);
                context.Animator?.ResetMovement();
                context.Animator?.PlayAmbientActivity(NPCAmbientActivity.SitGround);

                if (!string.IsNullOrEmpty(_line) && SpeechBubbleUI.Instance != null)
                    SpeechBubbleUI.Instance.Show(context.Transform, _line, _bubbleDuration, speakerName: "Estela");
            }

            _timer += Time.deltaTime;
            if (_timer >= _duration)
                IsCompleted = true;
        }

        public override void Cleanup(NPCStateContext context)
        {
            // Se llama tanto al terminar con normalidad como si algo interrumpe la secuencia
            // antes de tiempo (p.ej. entra en combate) — en ambos casos hay que levantarla.
            context.Animator?.StopAmbientActivity(NPCAmbientActivity.SitGround);
        }
    }

    /// <summary>
    /// CinematicAction: camina hasta un punto delante del jugador, se para mirando hacia él,
    /// suelta un bocadillo y se queda plantada un rato (bloqueando físicamente el paso, que es
    /// justo el efecto de "molestar" buscado) antes de que la secuencia termine.
    /// </summary>
    internal class EstelaBlockPlayerAction : CinematicAction
    {
        private const float ARRIVAL_TOLERANCE = 0.15f;

        private readonly Vector3 _targetPosition;
        private readonly Transform _player;
        private readonly float _standDuration;
        private readonly string _line;
        private readonly float _bubbleDuration;
        private readonly float _maxWalkDuration;

        private bool _hasSetDestination;
        private bool _hasArrivedAndSpoken;
        private float _walkTimer;
        private float _standTimer;

        public EstelaBlockPlayerAction(Vector3 targetPosition, Transform player, float standDuration,
            string line, float bubbleDuration, float maxWalkDuration = 6f)
        {
            _targetPosition = targetPosition;
            _player = player;
            _standDuration = Mathf.Max(0.1f, standDuration);
            _line = line;
            _bubbleDuration = bubbleDuration;
            _maxWalkDuration = Mathf.Max(0.5f, maxWalkDuration);
        }

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;

            if (!_hasArrivedAndSpoken)
            {
                var agent = context.Agent;

                if (!_hasSetDestination)
                {
                    if (agent != null && agent.isOnNavMesh)
                    {
                        context.Animator?.TransitionToLocomotion();
                        NavMeshAgentUtility.SetDestination(agent, _targetPosition);
                        _hasSetDestination = true;
                    }
                    else
                    {
                        // Sin agente válido para caminar: se queda donde está y pasa directamente
                        // a la fase de hablar, en vez de quedarse la secuencia colgada.
                        ArriveAndSpeak(context);
                        return;
                    }
                }

                _walkTimer += Time.deltaTime;

                bool arrived = agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending
                    && agent.remainingDistance <= agent.stoppingDistance + ARRIVAL_TOLERANCE;

                if (arrived || _walkTimer >= _maxWalkDuration)
                {
                    ArriveAndSpeak(context);
                    return;
                }

                if (agent != null && context.Animator != null)
                {
                    float speedFactor = NavMeshAgentUtility.ComputeSpeedFactor(agent);
                    context.Animator.SetMovementSpeed(speedFactor);
                    if (agent.velocity.sqrMagnitude > 0.01f)
                        context.Animator.FaceDirection(agent.velocity.normalized);
                }
                return;
            }

            _standTimer += Time.deltaTime;
            if (_standTimer >= _standDuration)
                IsCompleted = true;
        }

        private void ArriveAndSpeak(NPCStateContext context)
        {
            _hasArrivedAndSpoken = true;

            if (context.Agent != null)
                NavMeshAgentUtility.HardStop(context.Agent);
            context.Animator?.ResetMovement();

            if (_player != null)
            {
                Vector3 dir = _player.position - context.Transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    context.Animator?.FaceDirection(dir.normalized);
            }

            if (!string.IsNullOrEmpty(_line) && SpeechBubbleUI.Instance != null)
                SpeechBubbleUI.Instance.Show(context.Transform, _line, _bubbleDuration, speakerName: "Estela");
        }

        public override void Cleanup(NPCStateContext context)
        {
            if (context.Agent != null)
                NavMeshAgentUtility.HardStop(context.Agent);
            context.Animator?.ResetMovement();
        }
    }
}
