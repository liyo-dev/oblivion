using UnityEngine;
using Game.NPC.Common;

namespace Game.NPC.States
{
    /// <summary>
    /// Dos NPCs se detienen, se miran y realizan gestos sociales durante un tiempo
    /// determinado por su relación. Ambos entran a este estado de forma coordinada.
    /// </summary>
    public class NPCSocialEncounterState : NPCStateBase
    {
        public override string StateName => "SocialEncounter";

        private Transform _partner;
        private NPCRelationType _relation;
        private float _duration;
        private float _timer;
        private float _gestureTimer;
        private float _gestureInterval;

        private static readonly string[] FriendlyGestures = { "Talk01", "Talk02", "Laugh01", "Cheer01", "Talk03" };
        private static readonly string[] NeutralGestures   = { "Talk01", "Talk02", "Talk03", "Question01" };
        private static readonly string[] RivalGestures     = { "Angry01", "Talk01", "Question01" };

        public override void OnEnter(NPCStateContext context)
        {
            base.OnEnter(context);

            _partner  = context.PendingSocialPartner;
            _relation = context.PendingSocialRelation;

            // Limpiar el flag inmediatamente para no re-entrar si se interrumpe y vuelve
            context.PendingSocialPartner = null;

            _duration = _relation switch
            {
                NPCRelationType.BestFriend   => Random.Range(12f, 22f),
                NPCRelationType.Friend       => Random.Range(7f, 14f),
                NPCRelationType.Acquaintance => Random.Range(4f, 8f),
                NPCRelationType.Rival        => Random.Range(3f, 6f),
                NPCRelationType.Enemy        => Random.Range(2f, 4f),
                _                            => Random.Range(4f, 8f),
            };

            _timer         = 0f;
            _gestureTimer  = 0f;
            _gestureInterval = Random.Range(2.5f, 4.5f);

            StopMovement(context);

            context.LastSocialEncounterTime = Time.time;

            // Gesto de apertura
            string[] gestures = GetGestureSet(_relation);
            string openingGesture = gestures[Random.Range(0, gestures.Length)];

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Diagnóstico siempre visible: confirma si el encuentro realmente se dispara y con
            // qué gesto, para distinguir "el encuentro no se activó" de "se activó pero la
            // animación no se ve" (ver también el warning en NPCSimpleAnimator.PlaySocialGesture).
            Debug.Log($"[Social] 🗣️ {context.Transform.name} ↔ {_partner?.name ?? "?"} — " +
                $"encuentro iniciado (relación: {_relation}, duración: {_duration:F1}s, gesto: {openingGesture})");
#endif

            context.Animator?.PlaySocialGesture(openingGesture, null);
        }

        public override void OnUpdate(NPCStateContext context)
        {
            base.OnUpdate(context);

            _timer        += Time.deltaTime;
            _gestureTimer += Time.deltaTime;

            // Mirar al compañero
            if (_partner != null)
            {
                Vector3 dir = _partner.position - context.Transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    Quaternion target = Quaternion.LookRotation(dir);
                    context.Transform.rotation = Quaternion.Slerp(
                        context.Transform.rotation, target, Time.deltaTime * 5f);
                }
            }

            // Gestos periódicos
            if (_gestureTimer >= _gestureInterval)
            {
                _gestureTimer    = 0f;
                _gestureInterval = Random.Range(2.5f, 4.5f);

                string[] gestures = GetGestureSet(_relation);
                context.Animator?.PlaySocialGesture(gestures[Random.Range(0, gestures.Length)], null);
            }
        }

        public override void OnExit(NPCStateContext context)
        {
            // Si el encuentro llegó a completar su duración (no se cortó por combate/cinemática/
            // interacción a medias), registrarlo en NPCRelationshipRegistry para que la relación
            // pueda evolucionar (Stranger → Acquaintance → Friend → BestFriend).
            // Simplificación v1: no distingue el caso extremo de que combate/cinemática coincidan
            // en el mismo frame exacto en que _timer ya había alcanzado _duration.
            if (_partner != null && _timer >= _duration)
            {
                var partnerManager = _partner.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
                string myId = context.RelationshipId;
                string partnerId = partnerManager?.Context?.RelationshipId;

                // Ambos NPCs del encuentro ejecutan su propia instancia de este estado; solo uno
                // de los dos debe registrar el encuentro para no duplicar el progreso del vínculo.
                // Mismo criterio de orden que NPCRelationshipRegistry.MakeKey (comparación ordinal).
                if (!string.IsNullOrEmpty(myId) && !string.IsNullOrEmpty(partnerId) &&
                    string.CompareOrdinal(myId, partnerId) <= 0)
                {
                    float myFriendliness = context.Config?.socialConfig?.personality.friendliness ?? 0.5f;
                    float partnerFriendliness = partnerManager?.Configuration?.socialConfig?.personality.friendliness ?? 0.5f;
                    float avgFriendliness = (myFriendliness + partnerFriendliness) * 0.5f;

                    NPCRelationshipRegistry.RegisterEncounterCompleted(myId, partnerId, _relation, avgFriendliness);
                }
            }

            base.OnExit(context);
        }

        public override INPCState CheckTransitions(NPCStateContext context)
        {
            if (context.IsInCinematic)        return new CinematicState();
            if (context.IsInCombat)           return new CombatState();
            if (context.WasDefeatedInCombat)  return new DeadState();
            if (context.IsInteracting)        return new IdleState();

            if (_partner == null)             return new WanderState();
            if (_timer >= _duration)          return new WanderState();

            return null;
        }

        private static string[] GetGestureSet(NPCRelationType relation) => relation switch
        {
            NPCRelationType.BestFriend or NPCRelationType.Friend => FriendlyGestures,
            NPCRelationType.Rival or NPCRelationType.Enemy       => RivalGestures,
            _                                                     => NeutralGestures,
        };
    }
}
