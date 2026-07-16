
using UnityEngine;

namespace Invector.vCharacterController
{
    public class vThirdPersonAnimator : vThirdPersonMotor
    {
        #region Variables                

        public const float walkSpeed = 0.5f;
        public const float runningSpeed = 1f;
        public const float sprintSpeed = 1.5f;

        #endregion  

        public virtual void UpdateAnimator()
        {
            if (animator == null || !animator.enabled) return;

            animator.SetBool(vAnimatorParameters.IsStrafing, isStrafing); ;
            animator.SetBool(vAnimatorParameters.IsSprinting, isSprinting);
            animator.SetBool(vAnimatorParameters.IsGrounded, isGrounded);
            animator.SetFloat(vAnimatorParameters.GroundDistance, groundDistance);

            // Cuando lockMovement está activo el motor no llama a SetAnimatorMoveSpeed(),
            // por lo que verticalSpeed/horizontalSpeed/inputMagnitude mantienen el valor
            // del último frame en movimiento. Forzamos 0 para que la animación de caminar
            // se detenga correctamente al interactuar con NPCs, puntos de guardado, etc.
            // También cubrimos el caso en que el jugador choca contra un obstáculo físico
            // (ej: el modelo sólido de la hoguera) que no está en groundLayer: en ese caso
            // stopMove no se activa, pero la velocidad horizontal del rigidbody sí cae a 0.
            bool forceZeroMotion = lockMovement || stopMove;
            if (!forceZeroMotion && isGrounded && inputMagnitude > 0.3f &&
                _rigidbody != null && !_rigidbody.isKinematic)
            {
                float hVelSqr = _rigidbody.linearVelocity.x * _rigidbody.linearVelocity.x +
                                _rigidbody.linearVelocity.z * _rigidbody.linearVelocity.z;
                if (hVelSqr < 0.01f) // < ~0.1 m/s horizontal → bloqueado contra pared
                    forceZeroMotion = true;
            }

            if (isStrafing)
            {
                animator.SetFloat(vAnimatorParameters.InputHorizontal, forceZeroMotion ? 0 : horizontalSpeed, strafeSpeed.animationSmooth, Time.deltaTime);
                animator.SetFloat(vAnimatorParameters.InputVertical, forceZeroMotion ? 0 : verticalSpeed, strafeSpeed.animationSmooth, Time.deltaTime);
            }
            else
            {
                animator.SetFloat(vAnimatorParameters.InputVertical, forceZeroMotion ? 0 : verticalSpeed, freeSpeed.animationSmooth, Time.deltaTime);
            }

            animator.SetFloat(vAnimatorParameters.InputMagnitude, forceZeroMotion ? 0f : inputMagnitude, isStrafing ? strafeSpeed.animationSmooth : freeSpeed.animationSmooth, Time.deltaTime);
        }

        public virtual void SetAnimatorMoveSpeed(vMovementSpeed speed)
        {
            Vector3 relativeInput = transform.InverseTransformDirection(moveDirection);
            verticalSpeed = relativeInput.z;
            horizontalSpeed = relativeInput.x;

            var newInput = new Vector2(verticalSpeed, horizontalSpeed);

            if (speed.walkByDefault)
                inputMagnitude = Mathf.Clamp(newInput.magnitude, 0, isSprinting ? runningSpeed : walkSpeed);
            else
                inputMagnitude = Mathf.Clamp(isSprinting ? newInput.magnitude + 0.5f : newInput.magnitude, 0, isSprinting ? sprintSpeed : runningSpeed);
        }
    }

    public static partial class vAnimatorParameters
    {
        public static int InputHorizontal = Animator.StringToHash("InputHorizontal");
        public static int InputVertical = Animator.StringToHash("InputVertical");
        public static int InputMagnitude = Animator.StringToHash("InputMagnitude");
        public static int IsGrounded = Animator.StringToHash("IsGrounded");
        public static int IsStrafing = Animator.StringToHash("IsStrafing");
        public static int IsSprinting = Animator.StringToHash("IsSprinting");
        public static int GroundDistance = Animator.StringToHash("GroundDistance");
    }
}