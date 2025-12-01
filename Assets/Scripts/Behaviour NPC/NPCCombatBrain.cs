using System;
using System.Collections;
using Game.NPC.Common;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC
{
    /// <summary>
    /// Controla la IA de combate del NPC durante una batalla, separada del gestor principal.
    /// Maneja el movimiento para mantener la distancia y lanza las animaciones de ataque usando PlayOneShot.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(NPCSimpleAnimator))]
    public sealed class NPCCombatBrain : MonoBehaviour
    {
        [Serializable]
        public struct Settings
        {
            public float sightRadius;
            public float minDistance;
            public float maxDistance;
            public float attackCooldown;
            public float specialAttackChance;
            public float repathInterval;
            public float retreatDistance;
            public float turnSpeed;
            public string lightAttackStateLeft;
            public string lightAttackStateRight;
            public string specialAttackState;
        }

        NPCBehaviourManager _ctx;
        NavMeshAgent _agent;
        NPCSimpleAnimator _animator;
        Transform _player;

        Coroutine _combatRoutine;
        Settings _settings;

        public void Initialize(NPCBehaviourManager ctx)
        {
            _ctx = ctx;
            _agent = ctx ? ctx.Agent : null;
            _animator = ctx ? ctx.Animator : null;
        }

        public void BeginCombat(Settings settings)
        {
            _settings = settings;
            _ctx?.EnsurePlayerReference();
            _player = _ctx ? _ctx.Player : null;

            StopCombat();
            if (!isActiveAndEnabled || _player == null || _ctx == null)
                return;

            _combatRoutine = StartCoroutine(CombatLoop());
        }

        public void StopCombat()
        {
            if (_combatRoutine != null)
            {
                StopCoroutine(_combatRoutine);
                _combatRoutine = null;
            }

            if (_agent)
                NavMeshAgentUtility.SafeSetStopped(_agent, true);

            _animator?.ResetMovement();
        }

        IEnumerator CombatLoop()
        {
            float attackTimer = 0f;
            float repathTimer = 0f;

            while (_ctx != null && _ctx.isActiveAndEnabled && _player != null)
            {
                _ctx.EnsurePlayerReference();
                _player = _ctx.Player;
                if (_player == null)
                    break;

                FacePlayer();

                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
                bool shouldAttack = distanceToPlayer >= _settings.minDistance && distanceToPlayer <= _settings.maxDistance;

                repathTimer -= Time.deltaTime;
                if (!shouldAttack)
                {
                    Vector3 target = ComputeTarget(distanceToPlayer);
                    if (repathTimer <= 0f && _ctx.EnsureAgentOnNavMesh(_settings.sightRadius))
                    {
                        NavMeshAgentUtility.SetDestination(_agent, target, _settings.minDistance);
                        repathTimer = _settings.repathInterval;
                    }

                    float speed = NavMeshAgentUtility.ComputeSpeedFactor(_agent);
                    _animator?.SetMovementSpeed(speed, 0.08f);
                    attackTimer = Mathf.Min(attackTimer, _settings.attackCooldown * 0.35f);
                }
                else
                {
                    NavMeshAgentUtility.SafeSetStopped(_agent, true);
                    _animator?.ResetMovement();

                    attackTimer -= Time.deltaTime;
                    if (attackTimer <= 0f)
                    {
                        PlayAttackAnimation();
                        attackTimer = _settings.attackCooldown;
                    }
                }

                yield return null;
            }

            StopCombat();
        }

        Vector3 ComputeTarget(float currentDistance)
        {
            if (_player == null)
                return transform.position;

            if (currentDistance > _settings.maxDistance)
                return _player.position;

            Vector3 away = (transform.position - _player.position).normalized;
            float retreat = Mathf.Max(_settings.retreatDistance, 0.5f);
            return transform.position + away * retreat;
        }

        void FacePlayer()
        {
            if (_player == null)
                return;

            Vector3 direction = _player.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * Mathf.Max(0.1f, _settings.turnSpeed));
        }

        void PlayAttackAnimation()
        {
            if (_animator == null)
                return;

            bool useSpecial = UnityEngine.Random.value < _settings.specialAttackChance && !string.IsNullOrEmpty(_settings.specialAttackState);
            string state = useSpecial
                ? _settings.specialAttackState
                : (UnityEngine.Random.value > 0.5f ? _settings.lightAttackStateLeft : _settings.lightAttackStateRight);

            if (!string.IsNullOrEmpty(state))
                _animator.PlayOneShot(state);
        }
    }
}
