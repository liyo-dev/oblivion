using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC.Common
{
    /// <summary>
    /// Gestiona el estado del NavMeshAgent de forma robusta.
    /// PRINCIPIO: El estado debe ser atómico y consistente en todo momento.
    /// </summary>
    public class NavMeshAgentState : MonoBehaviour
    {
        #region State
        private NavMeshAgent _agent;
        private Vector3 _committedPosition;
        private bool _positionDirty;
        #endregion

        #region Properties
        public Vector3 GuaranteedPosition
        {
            get
            {
                if (_agent == null) return transform.position;
                return _positionDirty ? _committedPosition : transform.position;
            }
        }
        
        public bool IsReady => !_positionDirty;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_agent == null)
            {
                enabled = false;
                return;
            }
            _committedPosition = transform.position;
            _positionDirty = false;
        }

        private void LateUpdate()
        {
            if (_positionDirty)
            {
                float distance = Vector3.Distance(transform.position, _committedPosition);
                if (distance < 0.1f)
                {
                    _positionDirty = false;
                }
            }
            
            if (!_positionDirty)
            {
                _committedPosition = transform.position;
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Detiene al agente de forma agresiva, limpiando cualquier estado residual.
        /// </summary>
        public void ForceStop()
        {
            if (_agent == null || !_agent.enabled) return;

            if (_agent.isOnNavMesh)
            {
                _agent.isStopped = true;
                _agent.ResetPath();
                _agent.velocity = Vector3.zero;
            }
            
            // Forzar sincronización de posición para evitar saltos
            _committedPosition = transform.position;
            _positionDirty = false;
        }

        public bool Warp(Vector3 targetPosition)
        {
            if (_agent == null) return false;

            // Si el agente está deshabilitado, lo habilitamos para el warp
            if (!_agent.enabled) _agent.enabled = true;

            if (!_agent.isOnNavMesh)
            {
                transform.position = targetPosition;
                return true;
            }
            
            bool warpSuccess = _agent.Warp(targetPosition);
            
            if (warpSuccess)
            {
                _agent.nextPosition = targetPosition;
                transform.position = targetPosition;
                _committedPosition = targetPosition;
                _positionDirty = false;
                Physics.SyncTransforms();
            }
            
            return warpSuccess;
        }
        
        public bool ReEnable(Vector3 newPosition)
        {
            if (_agent == null) return false;
            
            _agent.enabled = true;
            
            if (!NavMesh.SamplePosition(newPosition, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                return false;
            }
            
            return Warp(hit.position);
        }
        #endregion
    }
}
