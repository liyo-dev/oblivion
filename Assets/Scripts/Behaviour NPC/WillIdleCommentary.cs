using System;
using UnityEngine;
using Game.NPC.Common;
using Game.NPC.States;

namespace Game.NPC
{
    /// <summary>
    /// "Vida artificial" del grupo — PARTE D.2 del TDD (idle del grupo jugable), pieza de Will.
    /// Hermana de EstelaIdleCommentary, mismo mecanismo exacto (ver esa clase para el porqué del
    /// CinematicState/CinematicSequence en vez de tocar IdleState directamente — NPCPartyMember.
    /// Update() reengancharía el seguimiento a media frase si no fuera por eso). Mientras Will va
    /// siguiendo al jugador con normalidad (no controlado, no en combate/cinemática/diálogo, equipo
    /// en modo Siguiendo, sobre NavMesh), de vez en cuando suelta un comentario suelto sin dejar de
    /// andar, o se para un momento a rebuscar algo entre sus cosas ("¿dónde estará...?").
    ///
    /// Cómo activarlo: añadir este componente al mismo GameObject de Will que ya tiene
    /// NPCPartyMember/NPCBehaviourManagerV2. No hace falta tocar nada más — trae ya sus propias
    /// frases por defecto y se auto-desactiva solo mientras no toque.
    /// </summary>
    // OJO: a propósito SIN [RequireComponent(typeof(NPCPartyMember))]. NPCPartyMember lo añade
    // NPCBehaviourManagerV2.EnsureRequiredComponents() a mano, con partyMember.SetConfig(...) —
    // si RequireComponent lo auto-instancia antes (Unity lo hace al construir el GameObject,
    // antes de que corra ningún Awake()), EnsureRequiredComponents() se lo encuentra ya
    // presente y se SALTA el SetConfig(), dejando el PartyConfig sin asignar — así es como se
    // rompió el party de Estela y Liam (INC-149/INC-151). GetComponent<NPCPartyMember>() en
    // Start() ya maneja con seguridad el caso null (ver IsEligibleNow) sin necesitar el atributo.
    [RequireComponent(typeof(NPCBehaviourManagerV2))]
    public class WillIdleCommentary : MonoBehaviour
    {
        [Header("Frecuencia")]
        [Tooltip("Segundos mínimos entre numeritos mientras va siguiendo al jugador con normalidad.")]
        [SerializeField] private float minInterval = 28f;
        [Tooltip("Segundos máximos entre numeritos mientras va siguiendo al jugador con normalidad.")]
        [SerializeField] private float maxInterval = 60f;

        [Header("Comentario suelto (sin dejar de andar)")]
        [SerializeField] private string[] ambientComments =
        {
            "¿Cuánto queda para llegar?",
            "Todo esto sigue pareciéndome raro, la verdad.",
            "Menos mal que no vengo solo en esto.",
            "A veces creo que ya he estado en un sitio así antes... no sé por qué.",
        };

        [Header("Rebuscar algo (gesto interrogativo, de pie, sin dejar de andar el grupo)")]
        [SerializeField] private string[] searchLines =
        {
            "¿Dónde habré puesto esto...?",
            "Juraría que lo tenía aquí mismo.",
            "Qué manía tengo de perderlo todo.",
            "Un momento... no, nada, falsa alarma.",
        };
        [Tooltip("Nombre del estado de Animator para el gesto de \"buscando algo\" (UpperBody layer).")]
        [SerializeField] private string searchGestureState = "Question01";
        [Tooltip("Cuánto tiempo se queda parado rebuscando antes de seguir andando.")]
        [SerializeField] private float searchDuration = 3f;

        [Header("Bocadillo")]
        [SerializeField] private float bubbleDuration = 3f;

        [Header("Peso relativo de cada numerito (no hace falta que sumen 100)")]
        [SerializeField] private float weightComment = 3f;
        [SerializeField] private float weightSearch = 2f;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;

        private NPCPartyMember _partyMember;
        private NPCBehaviourManagerV2 _npcManager;
        private float _nextTriggerTime;
        private bool _timerArmed;

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
            Debug.Log($"[WillIdleCommentary:{name}] Componente activo. partyMember={(_partyMember != null)}, npcManager={(_npcManager != null)}.");
#endif
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
        /// Misma lista de condiciones que EstelaIdleCommentary.IsEligibleNow — ver esa clase para
        /// el detalle de cada guard. Duplicado a propósito (cada personaje es un componente
        /// independiente) en vez de compartir una base común, para no arriesgar el comportamiento
        /// ya probado de Estela tocando una clase base compartida sin poder probarlo en el Editor.
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
            Debug.Log($"[WillIdleCommentary:{name}] Sin disparar todavía — {reason}.");
        }
#else
        private void LogIneligible(string reason) { }
#endif

        private void TriggerRandomBit()
        {
            float total = Mathf.Max(0.01f, weightComment) + Mathf.Max(0.01f, weightSearch);
            float roll = UnityEngine.Random.Range(0f, total);

            if (roll < weightComment)
                TriggerAmbientComment();
            else
                TriggerSearch();
        }

        private void TriggerAmbientComment()
        {
            string line = PickLine(ambientComments);
            if (string.IsNullOrEmpty(line)) return;

            if (debugMode) Debug.Log($"[WillIdleCommentary] Comentario suelto: \"{line}\"");
            SpeechBubbleUI.Instance?.Show(transform, line, bubbleDuration);
        }

        private void TriggerSearch()
        {
            string line = PickLine(searchLines);
            if (debugMode) Debug.Log($"[WillIdleCommentary] Se para a rebuscar: \"{line}\"");

            var sequence = new CompositeSequence();
            sequence.AddAction(new WillSearchAction(searchDuration, searchGestureState, line, bubbleDuration));
            _npcManager.StartCinematicSequence(sequence);
        }

        private string PickLine(string[] lines)
        {
            if (lines == null || lines.Length == 0) return null;
            return lines[UnityEngine.Random.Range(0, lines.Length)];
        }
    }

    /// <summary>
    /// CinematicAction: se para en el sitio (sin sentarse — Will se queda de pie, palpándose la
    /// ropa/mochila), reproduce un gesto interrogativo de la UpperBody layer, suelta un bocadillo y
    /// espera un rato antes de que la secuencia termine (momento en el que NPCPartyMember lo pone a
    /// seguir otra vez sola, ver cabecera de WillIdleCommentary/EstelaIdleCommentary).
    /// </summary>
    internal class WillSearchAction : CinematicAction
    {
        private readonly float _duration;
        private readonly string _gestureState;
        private readonly string _line;
        private readonly float _bubbleDuration;

        private bool _started;
        private float _timer;

        public WillSearchAction(float duration, string gestureState, string line, float bubbleDuration)
        {
            _duration = Mathf.Max(0.1f, duration);
            _gestureState = gestureState;
            _line = line;
            _bubbleDuration = bubbleDuration;
        }

        public override void Update(NPCStateContext context)
        {
            if (IsCompleted) return;

            if (!_started)
            {
                _started = true;

                if (context.Agent != null)
                    NavMeshAgentUtility.HardStop(context.Agent);
                context.Animator?.ResetMovement();
                if (!string.IsNullOrEmpty(_gestureState))
                    context.Animator?.PlaySocialGesture(_gestureState, null);

                if (!string.IsNullOrEmpty(_line) && SpeechBubbleUI.Instance != null)
                    SpeechBubbleUI.Instance.Show(context.Transform, _line, _bubbleDuration, speakerName: "Will");
            }

            _timer += Time.deltaTime;
            if (_timer >= _duration)
                IsCompleted = true;
        }

        public override void Cleanup(NPCStateContext context)
        {
            // No hay ninguna actividad ambiental que revertir (a diferencia de EstelaSitAction) —
            // Will nunca se sienta en este numerito, solo queda un gesto de UpperBody que ya se
            // resuelve solo al volver a Idle/locomoción normal.
        }
    }
}
