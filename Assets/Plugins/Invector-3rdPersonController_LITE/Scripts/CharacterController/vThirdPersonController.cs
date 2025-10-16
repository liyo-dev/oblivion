using System.Collections;
using UnityEngine;

namespace Invector.vCharacterController
{
    /// <summary>
    /// Controller de tercera persona con sistema de combate físico y mágico.
    /// 
    /// IMPORTANTE: Este controller está en el assembly de Plugins y NO debe referenciar
    /// enums o tipos específicos del assembly principal (como MagicSlot, PlayerState, etc.).
    /// 
    /// Para comunicación entre assemblies, usa:
    /// - Interfaces en el namespace global (IActionValidator, ITargetProvider, IAttackHitbox)
    /// - Tipos primitivos (int, float, bool, string)
    /// - El evento OnMagicSlotCast usa int: 0=Left, 1=Right, 2=Special
    /// </summary>
    public class vThirdPersonController : vThirdPersonAnimator
    {
        [Header("Physical Attacks (Base Layer)")]
        [SerializeField] private string[] physicalAttackStates = { "Attack1", "Attack2", "Attack3", "Attack4" };
        [SerializeField] private float attackFade = 0.10f;
        [SerializeField] private float physicalCooldown = 0.20f;

        [Header("UpperBody Magic (use FULL PATHS)")]
        [SerializeField] private int    upperLayerIndex        = 1;
        [SerializeField] private string magicLeftStatePath     = "UpperBody.Magic.MagicLeft";
        [SerializeField] private string magicRightStatePath    = "UpperBody.Magic.MagicRight";
        [SerializeField] private string magicSpecialStatePath  = "UpperBody.Magic.MagicSpecial";
        [SerializeField] private string upperIdlePath          = "UpperBody.UpperIdle";
        [SerializeField] private float  magicFade              = 0.10f;
        [SerializeField, Min(0f)] private float upperLayerFadeOut = 0.22f;


        [Header("Attack Impulse")]
        [SerializeField] private float impulseIdle   = 2.4f;
        [SerializeField] private float impulseMoving = 1.2f;
        [SerializeField] private float impulseDamp   = 10f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = false;

        // ---- Eventos ----
        /// <summary>
        /// Evento disparado al lanzar magia. Usa int en lugar de enum para evitar dependencias:
        /// 0 = Left, 1 = Right, 2 = Special
        /// </summary>
        public System.Action<int> OnMagicSlotCast;

        // ---- Runtime ----
        private int nextPhysicalIndex = 0;
        private float nextPhysicalTime = 0f;
        private Vector3 extraImpulse = Vector3.zero;
        private Coroutine upperWeightCo;

        [SerializeField] private bool autoAimMelee = true;
        private ITargetProvider _targeting;
        
        /// <summary>
        /// Referencia a IActionValidator (interfaz del namespace global).
        /// Permite validar acciones sin depender de enums del assembly principal.
        /// Es opcional, el sistema funciona sin él.
        /// </summary>
        private IActionValidator _actionValidator;

        // ========================= Motor base =========================
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
            // Verificar permiso del ActionValidator
            if (_actionValidator != null && !_actionValidator.CanSprint()) return;

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
            // Verificar permiso del ActionValidator
            if (_actionValidator != null && !_actionValidator.CanJump()) return;

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

        // ========================= Ataque físico =========================
        public virtual void AttackPhysical()
        {
            // Verificar permiso del ActionValidator
            if (_actionValidator != null && !_actionValidator.CanAttack()) return;
            if (!CanAttack() || Time.time < nextPhysicalTime) return;

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

        // ======== MAGIA (Left / Right / Special) ========

        private Coroutine magicCo;
        // TEMPORAL: Sistema directo mientras Unity recompila las interfaces
        private MonoBehaviour _magicCasterMB; // Referencia temporal como MonoBehaviour

        // Input → X/B/Y
        public void CastMagicLeft()    => TryCastMagic(magicLeftStatePath, 0);
        public void CastMagicRight()   => TryCastMagic(magicRightStatePath, 1);
        public void CastMagicSpecial() => TryCastMagic(magicSpecialStatePath, 2);

        private void TryCastMagic(string fullPath, int slotId)
        {
            // Verificar permiso del ActionValidator
            if (_actionValidator != null && !_actionValidator.CanCastMagic()) return;
            if (!CanAttack()) return;

            // TEMPORAL: Buscar MagicCaster por nombre de componente
            bool canCast = false;
            if (_magicCasterMB != null)
            {
                // Usar reflexión para llamar TryCastSpell
                var method = _magicCasterMB.GetType().GetMethod("TryCastSpell", new[] { typeof(int) });
                if (method != null)
                {
                    canCast = (bool)method.Invoke(_magicCasterMB, new object[] { slotId });
                }
            }
            else
            {
                // Fallback: usar el sistema anterior si no hay MagicCaster
                OnMagicSlotCast?.Invoke(slotId);
                canCast = true; // Asumimos que el casting es válido
            }

            // Solo reproducir animación si el casting fue exitoso
            if (canCast)
                PlayUpperAnimationAndExit(fullPath);
        }

        private void PlayUpperAnimationAndExit(string fullPath)
        {
            // Proteger contra Animator destruido
            if (animator == null)
            {
                if (debugLogs) Debug.LogWarning("PlayUpperAnimationAndExit called but animator is null/destroyed");
                return;
            }

            // Subimos el peso del layer y ENTRAMOS directo al estado (sin CrossFade)
            if (magicCo != null) { StopCoroutine(magicCo); magicCo = null; }
            animator.SetLayerWeight(upperLayerIndex, 1f);
            animator.Play(fullPath, upperLayerIndex, 0f);

            // Esperamos a que el Animator SALGA del estado y bajamos el layer suave
            magicCo = StartCoroutine(Co_WaitAnimatorExitThenLowerLayer(Animator.StringToHash(fullPath)));
        }

        private System.Collections.IEnumerator Co_WaitAnimatorExitThenLowerLayer(int targetHash)
        {
            int layer = upperLayerIndex;

            // Esperar a ENTRAR realmente en el estado
            while (animator != null && animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != targetHash)
                yield return null;

            if (animator == null)
            {
                // Animator fue destruido, limpiar y salir
                magicCo = null;
                yield break;
            }

            // Esperar a SALIR del estado por Exit Time (no cortamos el clip)
            while (animator != null && animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == targetHash)
                yield return null;

            if (animator == null)
            {
                magicCo = null;
                yield break;
            }

            // Desvanecer peso del layer superior
            float t = 0f, start = animator.GetLayerWeight(layer);
            while (t < upperLayerFadeOut)
            {
                t += Time.deltaTime;
                if (animator == null) break;
                animator.SetLayerWeight(layer, Mathf.Lerp(start, 0f, t / upperLayerFadeOut));
                yield return null;
            }

            if (animator != null) animator.SetLayerWeight(layer, 0f);
            magicCo = null;
        }

        // ========================= Lifecycle =========================
        private void Start()
        {
            _targeting = GetComponent<ITargetProvider>();
            // Buscar el ActionValidator (opcional, el sistema funciona sin él)
            _actionValidator = GetComponent<IActionValidator>();

            // TEMPORAL: Buscar MagicCaster por nombre de componente
            var allComponents = GetComponents<MonoBehaviour>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().Name == "MagicCaster")
                {
                    _magicCasterMB = comp;
                    break;
                }
            }

            // También buscar en el parent
            if (_magicCasterMB == null)
            {
                var parentComponents = GetComponentsInParent<MonoBehaviour>();
                foreach (var comp in parentComponents)
                {
                    if (comp.GetType().Name == "MagicCaster")
                    {
                        _magicCasterMB = comp;
                        break;
                    }
                }
            }
        }

        public virtual bool CanAttack() => isGrounded && !isJumping && !stopMove;

        // Limpiar coroutine y asegurar que el peso del layer no quede atascado si el objeto se desactiva o destruye
        private void OnDisable()
        {
            if (magicCo != null) { StopCoroutine(magicCo); magicCo = null; }
            if (animator != null) animator.SetLayerWeight(upperLayerIndex, 0f);
        }

        private void OnDestroy()
        {
            if (magicCo != null) { StopCoroutine(magicCo); magicCo = null; }
            // No podemos tocar animator si ya fue destruido, la comprobación es redundante pero segura
            if (animator != null) animator.SetLayerWeight(upperLayerIndex, 0f);
        }
    }
}

