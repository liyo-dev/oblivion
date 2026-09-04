using System;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// "Vida artificial" del grupo — PARTE D.2 del TDD (idle del grupo jugable), pieza de Liam.
    /// Hermana de EstelaIdleCommentary/WillIdleCommentary (mismo mecanismo de CinematicState/
    /// CinematicSequence, ver EstelaIdleCommentary para el porqué). Dos comportamientos, uno propio
    /// y uno reactivo:
    ///
    /// 1) Comentario suelto propio, con su timer aleatorio de siempre (seco, algo sarcástico, tono
    ///    de estratega — su voz, ver biblia-del-universo.md).
    /// 2) REACTIVO: cuando Estela se sienta a quejarse de hambre (EstelaIdleCommentary dispara el
    ///    evento estático EstelaSatDownHungry), Liam tiene una probabilidad de reaccionar: se gira
    ///    hacia ella, le suelta una frase seca pidiéndole que se levante, ella replica, y Liam le
    ///    pide a su EstelaIdleCommentary que corte el numerito antes de tiempo (RequestStandUp) —
    ///    el "discuten entre los dos" que pedía el diseño original de TDD.md § 17 Parte D.2.
    ///
    /// Cómo activarlo: añadir este componente al mismo GameObject de Liam que ya tiene
    /// NPCPartyMember/NPCBehaviourManagerV2. No hace falta tocar nada más.
    /// </summary>
    // OJO: a propósito SIN [RequireComponent(typeof(NPCPartyMember))]. NPCPartyMember lo añade
    // NPCBehaviourManagerV2.EnsureRequiredComponents() a mano, con partyMember.SetConfig(...) —
    // si RequireComponent lo auto-instancia antes (Unity lo hace al construir el GameObject,
    // antes de que corra ningún Awake()), EnsureRequiredComponents() se lo encuentra ya
    // presente y se SALTA el SetConfig(), dejando el PartyConfig sin asignar — así es como se
    // rompió el party de Estela y Liam (INC-149/INC-151). GetComponent<NPCPartyMember>() en
    // Start() ya maneja con seguridad el caso null (ver IsEligibleNow) sin necesitar el atributo.
    [RequireComponent(typeof(NPCBehaviourManagerV2))]
    public class LiamIdleCommentary : MonoBehaviour
    {
        [Header("Frecuencia (comentario propio)")]
        [Tooltip("Segundos mínimos entre comentarios sueltos propios mientras sigue al jugador con normalidad.")]
        [SerializeField] private float minInterval = 30f;
        [Tooltip("Segundos máximos entre comentarios sueltos propios mientras sigue al jugador con normalidad.")]
        [SerializeField] private float maxInterval = 65f;

        [Header("Comentario suelto propio (sin dejar de andar)")]
        [SerializeField] private string[] ambientComments =
        {
            "Deberíamos ir más rápido.",
            "...",
            "No hace falta comentar cada cosa que vemos, ¿sabéis?",
            "Sigo pensando que hay una ruta mejor que esta.",
        };

        [Header("Reacción a Estela sentada con hambre (\"que se levante\")")]
        [Tooltip("Probabilidad (0-1) de que Liam reaccione cada vez que Estela se sienta a quejarse de hambre. No siempre — para que no resulte cargante.")]
        [SerializeField, Range(0f, 1f)] private float nudgeChance = 0.6f;
        [Tooltip("Tiempo mínimo entre dos reacciones de este tipo, aunque Estela se siente varias veces seguidas.")]
        [SerializeField] private float nudgeCooldown = 90f;
        [SerializeField] private string[] nudgeLines =
        {
            "Estela, levanta. No es el momento.",
            "¿En serio? Ahora no, vamos.",
            "Ya comerás luego. Arriba.",
        };
        [Tooltip("Réplica de Estela a la reacción de Liam (distinta de sus frases de hambre normales, para que la escena tenga sentido como intercambio).")]
        [SerializeField] private string[] estelaReplyLines =
        {
            "¡Tengo hambre, no es mi culpa!",
            "Un segundo más, pesado.",
            "Vale, vale, ya voy... qué genio.",
        };
        [Tooltip("Segundos entre la frase de Liam y la réplica de Estela.")]
        [SerializeField] private float lineDelay = 1.6f;
        [Tooltip("Segundos entre la réplica de Estela y el momento en que Liam la hace levantarse.")]
        [SerializeField] private float postReplyDelay = 1f;

        [Header("Bocadillo")]
        [SerializeField] private float bubbleDuration = 3f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private NPCPartyMember _partyMember;
        private NPCBehaviourManagerV2 _npcManager;
        private float _nextTriggerTime;
        private bool _timerArmed;
        private float _nextNudgeAllowedTime;

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
            Debug.Log($"[LiamIdleCommentary:{name}] Componente activo. partyMember={(_partyMember != null)}, npcManager={(_npcManager != null)}.");
#endif
        }

        void OnEnable()
        {
            EstelaIdleCommentary.EstelaSatDownHungry += HandleEstelaSatDownHungry;
        }

        void OnDisable()
        {
            EstelaIdleCommentary.EstelaSatDownHungry -= HandleEstelaSatDownHungry;
        }

        void Update()
        {
            if (!IsEligibleNow())
            {
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
                TriggerAmbientComment();
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
        /// Reacción a que Estela se haya sentado a quejarse de hambre en algún punto del mapa.
        /// Suscrita en OnEnable al evento estático de EstelaIdleCommentary — ver cabecera de esta
        /// clase. Todos los guards de abajo son sobre LIAM (¿está libre para reaccionar?); no hace
        /// falta comprobar nada de Estela aparte de que su componente exista, porque si ella está
        /// sentada quejándose de hambre es porque su propio IsEligibleNow ya dio luz verde.
        /// </summary>
        private void HandleEstelaSatDownHungry(Transform estela)
        {
            if (!IsEligibleNow())
            {
                if (debugMode) Debug.Log("[LiamIdleCommentary] Estela se sentó, pero Liam no está libre para reaccionar ahora mismo.");
                return;
            }
            if (Time.time < _nextNudgeAllowedTime)
            {
                if (debugMode) Debug.Log("[LiamIdleCommentary] Estela se sentó, pero Liam ya reaccionó hace poco (cooldown).");
                return;
            }
            if (UnityEngine.Random.value > nudgeChance)
            {
                if (debugMode) Debug.Log("[LiamIdleCommentary] Estela se sentó, pero esta vez Liam pasa de largo (tirada de dado).");
                return;
            }

            var estelaIdle = estela != null ? estela.GetComponent<EstelaIdleCommentary>() : null;
            if (estelaIdle == null) return;

            _nextNudgeAllowedTime = Time.time + nudgeCooldown;

            string liamLine = PickLine(nudgeLines);
            string estelaLine = PickLine(estelaReplyLines);
            if (debugMode) Debug.Log($"[LiamIdleCommentary] Reacciona a Estela sentada: Liam \"{liamLine}\" → Estela \"{estelaLine}\"");

            var sequence = new CompositeSequence();
            sequence.AddAction(new LiamNudgeEstelaAction(
                estela, estelaIdle, liamLine, estelaLine, bubbleDuration, lineDelay, postReplyDelay));
            _npcManager.StartCinematicSequence(sequence);
        }

        /// <summary>
        /// Misma lista de condiciones que EstelaIdleCommentary.IsEligibleNow — ver esa clase para
        /// el detalle de cada guard. Duplicado a propósito, ver WillIdleCommentary para el porqué.
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
        private void LogIneligible(string reason)
        {
            if (Time.time < _nextDiagnosticLogTime) return;
            _nextDiagnosticLogTime = Time.time + 5f;
            Debug.Log($"[LiamIdleCommentary:{name}] Sin disparar todavía — {reason}.");
        }
#else
        private void LogIneligible(string reason) { }
#endif

        private void TriggerAmbientComment()
        {
            string line = PickLine(ambientComments);
            if (string.IsNullOrEmpty(line)) return;

            if (debugMode) Debug.Log($"[LiamIdleCommentary] Comentario suelto: \"{line}\"");
            SpeechBubbleUI.Instance?.Show(transform, line, bubbleDuration);
        }

        private string PickLine(string[] lines)
        {
            if (lines == null || lines.Length == 0) return null;
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }
    }

    /// <summary>
    /// CinematicAction (corre en la SECUENCIA DE LIAM, no en la de Estela): se gira hacia Estela,
    /// suelta su línea, espera, muestra la réplica de Estela sobre SU PROPIO transform (sin tocar
    /// el NPCStateContext de Estela — ella sigue corriendo su propia CinematicAction de "sentarse",
    /// esto solo le pone un bocadillo encima), y termina pidiéndole que se levante antes de tiempo
    /// vía EstelaIdleCommentary.RequestStandUp(). Si Estela ya se ha levantado sola o su componente
    /// desaparece a media secuencia (por cualquier motivo), esto se corta con seguridad sin excepción.
    /// </summary>
    internal class LiamNudgeEstelaAction : CinematicAction
    {
        private enum Phase { LiamLine, WaitForReply, EstelaReply, WaitForStandUp, Done }

        private readonly Transform _estela;
        private readonly EstelaIdleCommentary _estelaIdle;
        private readonly string _liamLine;
        private readonly string _estelaLine;
        private readonly float _bubbleDuration;
        private readonly float _lineDelay;
        private readonly float _postReplyDelay;

        private Phase _phase = Phase.LiamLine;
        private float _phaseTimer;

        public LiamNudgeEstelaAction(Transform estela, EstelaIdleCommentary estelaIdle,
            string liamLine, string estelaLine, float bubbleDuration, float lineDelay, float postReplyDelay)
        {
            _estela = estela;
            _estelaIdle = estelaIdle;
            _liamLine = liamLine;
            _estelaLine = estelaLine;
            _bubbleDuration = bubbleDuration;
            _lineDelay = Mathf.Max(0.1f, lineDelay);
            _postReplyDelay = Mathf.Max(0.1f, postReplyDelay);
        }

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;

            // Mirar hacia Estela durante toda la secuencia, no solo al empezar.
            if (_estela != null)
            {
                Vector3 dir = _estela.position - context.Transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(dir);
                    context.Transform.rotation = Quaternion.Slerp(context.Transform.rotation, target, Time.deltaTime * 5f);
                }
            }

            switch (_phase)
            {
                case Phase.LiamLine:
                    if (context.Agent != null)
                        NavMeshAgentUtility.HardStop(context.Agent);
                    context.Animator?.ResetMovement();
                    if (!string.IsNullOrEmpty(_liamLine) && SpeechBubbleUI.Instance != null)
                        SpeechBubbleUI.Instance.Show(context.Transform, _liamLine, _bubbleDuration, speakerName: "Liam");
                    _phaseTimer = 0f;
                    _phase = Phase.WaitForReply;
                    break;

                case Phase.WaitForReply:
                    _phaseTimer += Time.deltaTime;
                    if (_phaseTimer >= _lineDelay)
                        _phase = Phase.EstelaReply;
                    break;

                case Phase.EstelaReply:
                    if (!string.IsNullOrEmpty(_estelaLine) && _estela != null && SpeechBubbleUI.Instance != null)
                        SpeechBubbleUI.Instance.Show(_estela, _estelaLine, _bubbleDuration, speakerName: "Estela");
                    _phaseTimer = 0f;
                    _phase = Phase.WaitForStandUp;
                    break;

                case Phase.WaitForStandUp:
                    _phaseTimer += Time.deltaTime;
                    if (_phaseTimer >= _postReplyDelay)
                    {
                        _estelaIdle?.RequestStandUp();
                        _phase = Phase.Done;
                    }
                    break;

                case Phase.Done:
                    IsCompleted = true;
                    break;
            }
        }

        public override void Cleanup(NPCStateContext context)
        {
            // Nada que revertir en Liam (nunca cambió de actividad ambiental, solo se paró y giró).
        }
    }
}
