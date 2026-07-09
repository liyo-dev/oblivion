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
            context.Animator?.PlaySocialGesture(gestures[Random.Range(0, gestures.Length)], null);
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
