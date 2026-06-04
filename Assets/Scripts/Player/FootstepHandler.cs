using System;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Detecta pisadas monitorizando la velocidad vertical de los huesos de los pies (Humanoid rig).
    /// No requiere Animation Events ni modificar los clips. Añadir al GameObject raíz del personaje.
    /// </summary>
    public class FootstepHandler : MonoBehaviour
    {
        public static event Action<Vector3> OnFootstep;

        [SerializeField] private Animator _animator;
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _raycastDistance = 2f;

        [Tooltip("Velocidad mínima de descenso (m/s) para considerar que el pie está bajando.")]
        [SerializeField] private float _descentSpeedThreshold = 0.08f;

        [Tooltip("Distancia máxima del pie al suelo para confirmar la pisada (evita falsas en saltos/patadas).")]
        [SerializeField] private float _groundProximityThreshold = 0.2f;

        [Tooltip("Distancia mínima que debe recorrer el personaje desde la última pisada para generar una nueva huella. Evita el parpadeo en idle.")]
        [SerializeField] private float _minStepDistance = 0.35f;

        private Transform _leftFoot;
        private Transform _rightFoot;
        private float _leftPrevY;
        private float _rightPrevY;
        private bool _leftDescending;
        private bool _rightDescending;
        private Vector3 _lastStepRootPos;
        private bool _hasStep;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[1];

        private void Awake()
        {
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();

            if (_animator != null)
            {
                _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_leftFoot == null || _rightFoot == null)
                Debug.LogWarning($"[FootstepHandler] No se encontraron huesos de pies en {name}. ¿Es un rig Humanoid?");
#endif

            if (_leftFoot != null) _leftPrevY = _leftFoot.position.y;
            if (_rightFoot != null) _rightPrevY = _rightFoot.position.y;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            CheckFoot(_leftFoot, ref _leftPrevY, ref _leftDescending, dt);
            CheckFoot(_rightFoot, ref _rightPrevY, ref _rightDescending, dt);
        }

        private void CheckFoot(Transform foot, ref float prevY, ref bool wasDescending, float dt)
        {
            if (foot == null) return;

            float currentY = foot.position.y;
            float speedY = (currentY - prevY) / dt;
            bool isDescending = speedY < -_descentSpeedThreshold;

            // Pie pasó de bajar a detenerse/subir → acaba de plantar
            if (wasDescending && !isDescending)
                TryFireFootstep(foot.position);

            prevY = currentY;
            wasDescending = isDescending;
        }

        private void TryFireFootstep(Vector3 footPos)
        {
            // No generar huella si el personaje no se ha desplazado suficiente (idle, animación quieta)
            Vector3 rootPos = transform.position;
            if (_hasStep && Vector3.Distance(rootPos, _lastStepRootPos) < _minStepDistance) return;

            Vector3 origin = footPos + Vector3.up * 0.1f;
            int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _hitBuffer, _raycastDistance, _groundLayer);

            if (hits == 0) return;

            Vector3 groundPos = _hitBuffer[0].point;

            // Descarta si el pie está demasiado lejos del suelo (salto, patada, etc.)
            if (footPos.y - groundPos.y > _groundProximityThreshold) return;

            _lastStepRootPos = rootPos;
            _hasStep = true;
            OnFootstep?.Invoke(groundPos);
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => OnFootstep = null;
#endif
    }
}
