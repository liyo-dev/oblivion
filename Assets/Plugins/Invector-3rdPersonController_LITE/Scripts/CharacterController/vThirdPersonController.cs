using System.Collections;
using UnityEngine;

namespace Invector.vCharacterController
{
    public enum MagicCastType { Basic, Combo }
    public delegate void MagicCastHandler(GameObject caster, Transform origin, Vector3 direction, MagicCastType type);

    public class vThirdPersonController : vThirdPersonAnimator
    {
        [Header("Physical Attacks (Base Layer)")]
        [SerializeField] private string[] physicalAttackStates = { "Attack1", "Attack2", "Attack3", "Attack4" };
        [SerializeField] private float attackFade = 0.10f;
        [SerializeField] private float physicalCooldown = 0.20f;

        [Header("UpperBody Magic (use FULL PATHS)")]
        [SerializeField] private int upperLayerIndex = 1;
        [SerializeField] private string magicState1Path = "UpperBody.Magic.Magic1";
        [SerializeField] private string magicStateComboPath = "UpperBody.Magic.MagicComboDone";
        [SerializeField] private string upperIdlePath  = "UpperBody.UpperIdle";
        [SerializeField] private float  magicFade      = 0.10f;

        [Header("Attack Impulse")]
        [SerializeField] private float impulseIdle   = 2.4f;
        [SerializeField] private float impulseMoving = 1.2f;
        [SerializeField] private float impulseDamp   = 10f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        [Header("Magic Events")]
        [SerializeField] private Transform magicSpawnPoint;
        public event MagicCastHandler OnMagicCast;

        private int nextPhysicalIndex = 0;
        private float nextPhysicalTime = 0f;
        private Vector3 extraImpulse = Vector3.zero;
        private Coroutine upperWeightCo;

        [Header("Magic Anti-Retrigger")]
        [SerializeField, Range(0.2f, 0.9f)] private float magicMinRepeatNormalized = 0.60f;
        [SerializeField] private float magicBufferGrace = 0.30f;
        private bool magicReplayBuffered = false;
        private int magicLastTargetHash = 0;
        private float magicBufferExpireAt = 0f;
        private MagicCastType bufferedCastType = MagicCastType.Basic;

        [SerializeField] private bool autoAimMelee = true;
        private ITargetProvider _targeting;

        // === NUEVO: puente de habilidades/maná ===
        [SerializeField] private IAbilityGate _gate;

        public virtual void ControlAnimatorRootMotion()
        {
            if (!this.enabled) return;

            if (inputSmooth == Vector3.zero)
            {
                transform.position = animator.rootPosition;
                transform.rotation = animator.rootRotation;
            }

            if (useRootMotion) ApplyMove(moveDirection);
        }

        public virtual void ControlLocomotionType()
        {
            if (lockMovement) return;

            if (locomotionType.Equals(LocomotionType.FreeWithStrafe) && !isStrafing || locomotionType.Equals(LocomotionType.OnlyFree))
            {
                SetControllerMoveSpeed(freeSpeed);
                SetAnimatorMoveSpeed(freeSpeed);
            }
            else if (locomotionType.Equals(LocomotionType.OnlyStrafe) || locomotionType.Equals(LocomotionType.FreeWithStrafe) && isStrafing)
            {
                isStrafing = true;
                SetControllerMoveSpeed(strafeSpeed);
                SetAnimatorMoveSpeed(strafeSpeed);
            }

            if (!useRootMotion) ApplyMove(moveDirection);
        }

        public virtual void ControlRotationType()
        {
            if (lockRotation) return;

            bool validInput = input != Vector3.zero || (isStrafing ? strafeSpeed.rotateWithCamera : freeSpeed.rotateWithCamera);

            if (validInput)
            {
                inputSmooth = Vector3.Lerp(inputSmooth, input, (isStrafing ? strafeSpeed.movementSmooth : freeSpeed.movementSmooth) * Time.deltaTime);
                Vector3 dir = (isStrafing && (!isSprinting || sprintOnlyFree == false) || (freeSpeed.rotateWithCamera && input == Vector3.zero)) && rotateTarget ? rotateTarget.forward : moveDirection;
                RotateToDirection(dir);
            }
        }

        public virtual void UpdateMoveDirection(Transform referenceTransform = null)
        {
            if (input.magnitude <= 0.01)
            {
                moveDirection = Vector3.Lerp(moveDirection, Vector3.zero, (isStrafing ? strafeSpeed.movementSmooth : freeSpeed.movementSmooth) * Time.deltaTime);
                return;
            }

            if (referenceTransform && !rotateByWorld)
            {
                var right = referenceTransform.right; right.y = 0;
                var forward = Quaternion.AngleAxis(-90, Vector3.up) * right;
                moveDirection = (inputSmooth.x * right) + (inputSmooth.z * forward);
            }
            else
            {
                moveDirection = new Vector3(inputSmooth.x, 0, inputSmooth.z);
            }
        }

        public virtual void Sprint(bool value)
        {
            var sprintConditions = (input.sqrMagnitude > 0.1f && isGrounded &&
                !(isStrafing && !strafeSpeed.walkByDefault && (horizontalSpeed >= 0.5 || horizontalSpeed <= -0.5 || verticalSpeed <= 0.1f)));

            if (value && sprintConditions)
            {
                if (input.sqrMagnitude > 0.1f)
                {
                    if (isGrounded && useContinuousSprint) isSprinting = !isSprinting;
                    else if (!isSprinting) isSprinting = true;
                }
                else if (!useContinuousSprint && isSprinting) isSprinting = false;
            }
            else if (isSprinting) isSprinting = false;
        }

        public virtual void Strafe() => isStrafing = !isStrafing;

        public virtual void Jump()
        {
            jumpCounter = jumpTimer;
            isJumping = true;

            if (input.sqrMagnitude < 0.1f) animator.CrossFadeInFixedTime("Jump", 0.1f, 0);
            else                            animator.CrossFadeInFixedTime("JumpMove", 0.2f, 0);
        }

        private void ApplyMove(Vector3 dir)
        {
            if (extraImpulse.sqrMagnitude > 0.0001f)
            {
                dir += extraImpulse;
                extraImpulse = Vector3.Lerp(extraImpulse, Vector3.zero, Time.deltaTime * impulseDamp);
            }
            MoveCharacter(dir);
        }

        // ----------------- Físico -----------------
        public virtual void AttackPhysical()
        {
            if (!CanAttack() || Time.time < nextPhysicalTime) return;

            // === GATEO ===
            if (!_gate.CanUsePhysical())
            {
                if (debugLogs) Debug.Log("Bloqueado: PhysicalAttack no desbloqueado");
                return;
            }

            string state = (physicalAttackStates != null && physicalAttackStates.Length > 0)
                ? physicalAttackStates[Mathf.Clamp(nextPhysicalIndex, 0, physicalAttackStates.Length - 1)]
                : "Attack1";

            if (autoAimMelee && _targeting != null && _targeting.TryGetTarget(out var t))
            {
                Vector3 to = t.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            }

            animator.CrossFadeInFixedTime(state, attackFade, 0);

            var hitbox = GetComponentInChildren<IAttackHitbox>(true);
            if (hitbox != null) hitbox.ArmForSeconds(0.25f);

            nextPhysicalIndex = (nextPhysicalIndex + 1) % physicalAttackStates.Length;
            nextPhysicalTime  = Time.time + physicalCooldown;

            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float strength = (input.sqrMagnitude > 0.1f) ? impulseMoving : impulseIdle;
            extraImpulse += fwd * strength;
        }

        // ----------------- Magia -----------------
        public virtual void CastMagic1()      { PlayUpperAndAutoOff(magicState1Path, magicFade, MagicCastType.Basic); }
        public virtual void CastMagicFinish() { PlayUpperAndAutoOff(magicStateComboPath, magicFade, MagicCastType.Combo); }

        private void PlayUpperAndAutoOff(string fullPath, float fade, MagicCastType castType)
        {
            if (!CanAttack()) return;

            // === GATEO ===
            if (!_gate.CanUseMagic(castType))
            {
                if (debugLogs) Debug.Log("Bloqueado: MagicAttack no desbloqueado o sin MP");
                return;
            }

            int layer       = upperLayerIndex;
            int targetHash  = Animator.StringToHash(fullPath);
            magicLastTargetHash = targetHash;

            animator.SetLayerWeight(layer, 1f);

            var cur = animator.GetCurrentAnimatorStateInfo(layer);
            var nxt = animator.GetNextAnimatorStateInfo(layer);

            bool isSameState = (cur.fullPathHash == targetHash) || (nxt.fullPathHash == targetHash);
            bool isMagic1    = (targetHash == Animator.StringToHash(magicState1Path));

            if (isMagic1 && isSameState)
            {
                float norm = (cur.fullPathHash == targetHash) ? cur.normalizedTime : nxt.normalizedTime;
                float frac = Mathf.Repeat(norm, 1f);
                if (frac < magicMinRepeatNormalized)
                {
                    magicReplayBuffered = true;
                    magicBufferExpireAt = Time.time + magicBufferGrace;
                    bufferedCastType = castType;
                    if (debugLogs) Debug.Log("[Magic] Buffered re-cast (anti-retrigger)");
                    StartBlendOutRoutineIfNeeded(targetHash);
                    return;
                }
            }

            animator.CrossFadeInFixedTime(fullPath, fade, layer);
            RaiseMagicEvent(castType);
            StartBlendOutRoutine(targetHash);
        }

        private void StartBlendOutRoutineIfNeeded(int targetHash)
        {
            if (upperWeightCo == null)
                upperWeightCo = StartCoroutine(Co_UpperBlendOut(targetHash));
        }

        private void StartBlendOutRoutine(int targetHash)
        {
            if (upperWeightCo != null) StopCoroutine(upperWeightCo);
            upperWeightCo = StartCoroutine(Co_UpperBlendOut(targetHash));
        }

        private IEnumerator Co_UpperBlendOut(int targetHash)
        {
            int layer = upperLayerIndex;

            float safety = 1f;
            while (safety > 0f)
            {
                var st = animator.GetCurrentAnimatorStateInfo(layer);
                if (st.fullPathHash == targetHash) break;
                safety -= Time.deltaTime;
                yield return null;
            }

            while (true)
            {
                var st = animator.GetCurrentAnimatorStateInfo(layer);

                if (magicReplayBuffered && targetHash == Animator.StringToHash(magicState1Path))
                {
                    float frac = Mathf.Repeat(st.normalizedTime, 1f);
                    bool canReplay = (frac >= magicMinRepeatNormalized) || (Time.time <= magicBufferExpireAt && frac >= 0.95f);
                    if (canReplay)
                    {
                        magicReplayBuffered = false;
                        animator.Play(targetHash, layer, 0f);
                        RaiseMagicEvent(bufferedCastType);
                        if (debugLogs) Debug.Log("[Magic] Buffered re-cast fired");
                        yield return null;
                        continue;
                    }
                }

                if (st.fullPathHash != targetHash) break;
                if (st.normalizedTime >= 0.98f) break;
                yield return null;
            }

            if (!string.IsNullOrEmpty(upperIdlePath))
                animator.CrossFadeInFixedTime(upperIdlePath, 0.12f, layer);

            float t = 0f, start = animator.GetLayerWeight(layer);
            const float fadeOutTime = 0.22f;
            while (t < fadeOutTime)
            {
                t += Time.deltaTime;
                animator.SetLayerWeight(layer, Mathf.Lerp(start, 0f, t / fadeOutTime));
                yield return null;
            }
            animator.SetLayerWeight(layer, 0f);
            upperWeightCo = null;
        }

        private void RaiseMagicEvent(MagicCastType type)
        {
            if (OnMagicCast == null || magicSpawnPoint == null) return;
            OnMagicCast.Invoke(this.gameObject, magicSpawnPoint, magicSpawnPoint.forward, type);
        }

        private void Start()
        {
            if (magicSpawnPoint == null)
            {
                foreach (var tr in GetComponentsInChildren<Transform>(true))
                {
                    if (tr.name == "MagicAttackSpawner") { magicSpawnPoint = tr; break; }
                }
            }
            _targeting = GetComponent<ITargetProvider>();
            if (_gate == null) _gate = GetComponent<IAbilityGate>();
        }

        public virtual bool CanAttack() => isGrounded && !isJumping && !stopMove;
    }
}
