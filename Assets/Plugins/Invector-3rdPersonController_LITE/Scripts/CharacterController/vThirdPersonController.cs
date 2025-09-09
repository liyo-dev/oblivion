using System.Collections;
using UnityEngine;

namespace Invector.vCharacterController
{
    public class vThirdPersonController : vThirdPersonAnimator
    {
        // ======== FÍSICO (Base Layer) ========
        [Header("Physical Attacks (Base Layer)")]
        [SerializeField] private string[] physicalAttackStates = { "Attack1", "Attack2", "Attack3", "Attack4" }; // en Base Layer
        [SerializeField] private float attackFade = 0.10f;
        [SerializeField] private float physicalCooldown = 0.20f;

        // ======== MAGIA (Layer superior) ========
        [Header("UpperBody Magic (use FULL PATHS)")]
        [SerializeField] private int upperLayerIndex = 1;                       // layer UpperBody
        [SerializeField] private string magicState1Path = "UpperBody.Magic.Magic1"; 
        [SerializeField] private string magicStateComboPath = "UpperBody.Magic.MagicComboDone"; 
        [SerializeField] private string upperIdlePath  = "UpperBody.UpperIdle";     
        [SerializeField] private float  magicFade      = 0.10f;

        // ======== IMPULSO EN ATAQUE ========
        [Header("Attack Impulse")]
        [SerializeField] private float impulseIdle   = 2.4f;
        [SerializeField] private float impulseMoving = 1.2f;
        [SerializeField] private float impulseDamp   = 10f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        // ======== Runtime ========
        private int nextPhysicalIndex = 0;
        private float nextPhysicalTime = 0f;
        private Vector3 extraImpulse = Vector3.zero;
        private Coroutine upperWeightCo;

        // ======== NUEVO: Anti-retrigger para magia ========
        [Header("Magic Anti-Retrigger")]
        [SerializeField, Range(0.2f, 0.9f)] private float magicMinRepeatNormalized = 0.60f; // % del clip antes de permitir relanzar
        [SerializeField] private float magicBufferGrace = 0.30f;  // margen para permitir un relanzamiento buffered
        private bool magicReplayBuffered = false;
        private int magicLastTargetHash = 0;
        private float magicBufferExpireAt = 0f;

        // ----------------- Motor original -----------------
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

            string state = (physicalAttackStates != null && physicalAttackStates.Length > 0)
                ? physicalAttackStates[Mathf.Clamp(nextPhysicalIndex, 0, physicalAttackStates.Length - 1)]
                : "Attack1";

            // Base Layer (0), nombres simples
            animator.CrossFadeInFixedTime(state, attackFade, 0);

            nextPhysicalIndex = (nextPhysicalIndex + 1) % physicalAttackStates.Length;
            nextPhysicalTime  = Time.time + physicalCooldown;

            // Impulso hacia donde mira el player
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float strength = (input.sqrMagnitude > 0.1f) ? impulseMoving : impulseIdle;
            extraImpulse += fwd * strength;
        }

        // ----------------- Magia -----------------
        public virtual void CastMagic1()      { PlayUpperAndAutoOff(magicState1Path, magicFade); }
        public virtual void CastMagicFinish() { PlayUpperAndAutoOff(magicStateComboPath, magicFade); } 

        private void PlayUpperAndAutoOff(string fullPath, float fade)
        {
            if (!CanAttack()) return;

            int layer      = upperLayerIndex;
            int targetHash = Animator.StringToHash(fullPath);
            magicLastTargetHash = targetHash;

            // Sube peso siempre
            animator.SetLayerWeight(layer, 1f);

            var cur = animator.GetCurrentAnimatorStateInfo(layer);
            var nxt = animator.GetNextAnimatorStateInfo(layer);

            // ANTIRETRIGGER para Magic1: si el mismo estado está muy al inicio, no reiniciar; bufferiza un relanzamiento
            bool isSameState = (cur.fullPathHash == targetHash) || (nxt.fullPathHash == targetHash);
            bool isMagic1 = (targetHash == Animator.StringToHash(magicState1Path));
            if (isMagic1 && isSameState)
            {
                float norm = (cur.fullPathHash == targetHash) ? cur.normalizedTime : nxt.normalizedTime;
                float frac = Mathf.Repeat(norm, 1f);
                if (frac < magicMinRepeatNormalized)
                {
                    magicReplayBuffered = true;
                    magicBufferExpireAt = Time.time + magicBufferGrace;
                    if (debugLogs) Debug.Log("[Magic] Buffered re-cast (anti-retrigger)");
                    return;
                }
            }

            // Transición normal
            animator.CrossFadeInFixedTime(fullPath, fade, layer);

            if (upperWeightCo != null) StopCoroutine(upperWeightCo);
            upperWeightCo = StartCoroutine(Co_UpperBlendOut(targetHash));
        }

        private IEnumerator Co_UpperBlendOut(int targetHash)
        {
            int layer = upperLayerIndex;

            // Espera a entrar
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

                // Si teníamos un relanzamiento buffered para Magic1 y ya pasamos el umbral, relanza ahora
                if (magicReplayBuffered && targetHash == Animator.StringToHash(magicState1Path))
                {
                    float frac = Mathf.Repeat(st.normalizedTime, 1f);
                    bool canReplay = (frac >= magicMinRepeatNormalized) || (Time.time <= magicBufferExpireAt && frac >= 0.95f);
                    if (canReplay)
                    {
                        magicReplayBuffered = false;
                        animator.Play(targetHash, layer, 0f);
                        if (debugLogs) Debug.Log("[Magic] Buffered re-cast fired");
                        yield return null; // deja que entre y vuelve a esperar al final del nuevo pase
                        continue;
                    }
                }

                if (st.fullPathHash != targetHash) break;
                if (st.normalizedTime >= 0.98f) break;
                yield return null;
            }

            // Pre-blend a idle si lo tienes (si no existe, no pasa nada)
            if (!string.IsNullOrEmpty(upperIdlePath))
            {
                animator.CrossFadeInFixedTime(upperIdlePath, 0.12f, layer);
            }

            // Bajar peso suave
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

        public virtual bool CanAttack() => isGrounded && !isJumping && !stopMove;
    }
}
