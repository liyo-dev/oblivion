using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC
{
    /// <summary>
    /// Componente para gestionar la huida táctica del NPC hacia cobertura cuando está en desventaja.
    /// Busca objetos cercanos (árboles, rocas, edificios) y se posiciona detrás de ellos.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NPCTacticalRetreat : MonoBehaviour
    {
        [Header("Configuración de Cobertura")]
        [SerializeField, Min(5f)] 
        [Tooltip("Radio de búsqueda de objetos de cobertura")]
        private float coverSearchRadius = 15f;

        [SerializeField] 
        [Tooltip("Capas que se consideran cobertura (Default, Environment, Props, etc.)")]
        private LayerMask coverLayerMask = -1; // Todo por defecto

        [SerializeField, Min(2f)] 
        [Tooltip("Distancia mínima del objeto de cobertura al NPC")]
        private float minCoverDistance = 3f;

        [SerializeField, Min(5f)] 
        [Tooltip("Distancia máxima del objeto de cobertura al NPC")]
        private float maxCoverDistance = 15f;

        [SerializeField, Min(0.5f)] 
        [Tooltip("Tiempo que permanece en cobertura antes de volver a combatir")]
        private float coverStayDuration = 4f;

        [SerializeField, Min(0.5f)] 
        [Tooltip("Distancia mínima detrás del objeto para considerar que está a cubierto")]
        private float coverDistanceBehind = 1.5f;

        [Header("Debug")]
        [SerializeField] 
        private bool showDebugGizmos = true;

        [SerializeField, Min(1)] 
        [Tooltip("Número máximo de objetos a evaluar como cobertura")]
        private int maxCoverObjectsToCheck = 10;

        // Referencias
        private NavMeshAgent _agent;
        private Transform _player;

        // Estado
        private Vector3? _currentCoverPosition;
        private Transform _currentCoverObject;
        private float _coverStayTimer;
        private bool _isBehindCover;
        private List<Vector3> _evaluatedPositions = new List<Vector3>();
        
        // ✅ OPTIMIZACIÓN FASE 2: Buffer reutilizable para Physics queries
        private Collider[] _coverSearchBuffer = new Collider[32];

        public bool IsRetreating { get; private set; }
        public bool IsBehindCover => _isBehindCover;
        public Vector3? CoverPosition => _currentCoverPosition;
        public Transform CoverObject => _currentCoverObject;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
        }

        /// <summary>
        /// Inicia el proceso de huida táctica buscando cobertura
        /// </summary>
        /// <param name="player">Transform del jugador para calcular posiciones óptimas</param>
        /// <returns>True si se encontró cobertura, False si no</returns>
        public bool StartRetreat(Transform player)
        {
            _player = player;
            _evaluatedPositions.Clear();

            if (!FindNearestCover(out Vector3 coverPosition, out Transform coverObject))
            {
                Debug.Log("[NPCTacticalRetreat] ❌ No se encontró cobertura disponible");
                return false;
            }

            _currentCoverPosition = coverPosition;
            _currentCoverObject = coverObject;
            _coverStayTimer = coverStayDuration;
            IsRetreating = true;
            _isBehindCover = false;

            // Navegar hacia la cobertura
            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.isStopped = false;
                _agent.SetDestination(coverPosition);
                Debug.Log($"[NPCTacticalRetreat] 🏃 Huyendo hacia cobertura: {coverObject?.name} en {coverPosition}");
            }

            return true;
        }

        /// <summary>
        /// Detiene la huida y vuelve al combate normal
        /// </summary>
        public void StopRetreat()
        {
            IsRetreating = false;
            _isBehindCover = false;
            _currentCoverPosition = null;
            _currentCoverObject = null;
            _coverStayTimer = 0f;
            _evaluatedPositions.Clear();

            Debug.Log("[NPCTacticalRetreat] 🛡️ Saliendo de cobertura, volviendo a combate");
        }

        void Update()
        {
            if (!IsRetreating) return;

            // Actualizar timer de permanencia en cobertura
            if (_isBehindCover)
            {
                _coverStayTimer -= Time.deltaTime;
                if (_coverStayTimer <= 0f)
                {
                    StopRetreat();
                    return;
                }
            }

            // Verificar si llegó a la cobertura
            if (_currentCoverPosition.HasValue && !_isBehindCover)
            {
                float distanceToCover = Vector3.Distance(transform.position, _currentCoverPosition.Value);
                if (distanceToCover < 1.5f) // Tolerancia de llegada
                {
                    _isBehindCover = true;
                    _agent.isStopped = true;
                    Debug.Log("[NPCTacticalRetreat] ✅ Llegó a cobertura, permanecerá por " + coverStayDuration + "s");
                }
            }

            // Verificar línea de visión con el jugador
            if (_isBehindCover && _player != null && _currentCoverObject != null)
            {
                // Si el jugador puede ver al NPC, la cobertura no es efectiva
                if (HasLineOfSight(_player.position))
                {
                    Debug.LogWarning("[NPCTacticalRetreat] ⚠️ Cobertura comprometida, jugador tiene LOS");
                    // Podría buscar nueva cobertura aquí si se desea
                }
            }
        }

        /// <summary>
        /// Busca el objeto de cobertura más cercano y calcula la posición óptima detrás de él
        /// </summary>
        private bool FindNearestCover(out Vector3 coverPosition, out Transform coverObject)
        {
            coverPosition = Vector3.zero;
            coverObject = null;

            if (_player == null)
            {
                Debug.LogWarning("[NPCTacticalRetreat] No hay referencia al jugador");
                return false;
            }

            // Buscar objetos cercanos que puedan servir de cobertura
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, coverSearchRadius, _coverSearchBuffer, coverLayerMask); // ✅ OPTIMIZACIÓN FASE 2: NonAlloc
            
            if (hitCount == 0)
            {
                Debug.Log("[NPCTacticalRetreat] No hay objetos cercanos que sirvan de cobertura");
                return false;
            }

            float bestScore = float.MinValue;
            Vector3 bestPosition = Vector3.zero;
            Transform bestObject = null;

            // Limitar el número de objetos a evaluar para no penalizar rendimiento
            int objectsToCheck = Mathf.Min(hitCount, maxCoverObjectsToCheck);

            for (int i = 0; i < objectsToCheck; i++)
            {
                Collider col = _coverSearchBuffer[i];
                
                // Ignorar el propio NPC
                if (col.transform == transform || col.transform.IsChildOf(transform))
                    continue;

                // Calcular posición detrás del objeto (opuesta al jugador)
                Vector3 objectPosition = col.bounds.center;
                Vector3 directionFromPlayer = (objectPosition - _player.position).normalized;
                Vector3 potentialCoverPosition = objectPosition + directionFromPlayer * (col.bounds.extents.magnitude + coverDistanceBehind);

                // Verificar distancia al NPC
                float distanceToNPC = Vector3.Distance(transform.position, potentialCoverPosition);
                if (distanceToNPC < minCoverDistance || distanceToNPC > maxCoverDistance)
                    continue;

                // Verificar que la posición esté en NavMesh
                if (!NavMesh.SamplePosition(potentialCoverPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    continue;

                potentialCoverPosition = hit.position;

                // Verificar que realmente bloquea la línea de visión con el jugador
                if (!DoesObjectBlockLineOfSight(potentialCoverPosition, _player.position, col))
                    continue;

                // Calcular score (más cerca es mejor, pero no demasiado cerca del jugador)
                float distanceToPlayer = Vector3.Distance(potentialCoverPosition, _player.position);
                float score = CalculateCoverScore(potentialCoverPosition, col, distanceToNPC, distanceToPlayer);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPosition = potentialCoverPosition;
                    bestObject = col.transform;
                }

                _evaluatedPositions.Add(potentialCoverPosition);
            }

            if (bestObject != null)
            {
                coverPosition = bestPosition;
                coverObject = bestObject;
                Debug.Log($"[NPCTacticalRetreat] ✅ Cobertura encontrada: {bestObject.name} (Score: {bestScore:F2})");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calcula un score para una posición de cobertura (mayor es mejor)
        /// </summary>
        private float CalculateCoverScore(Vector3 position, Collider coverObject, float distanceToNPC, float distanceToPlayer)
        {
            float score = 0f;

            // Preferir cobertura cercana al NPC (pero no demasiado cerca ni lejos)
            float distanceScore = Mathf.InverseLerp(maxCoverDistance, minCoverDistance, distanceToNPC);
            score += distanceScore * 30f;

            // Preferir cobertura que esté a distancia razonable del jugador (ni muy cerca ni muy lejos)
            float optimalPlayerDistance = 10f;
            float playerDistanceScore = 1f - Mathf.Abs(distanceToPlayer - optimalPlayerDistance) / optimalPlayerDistance;
            score += Mathf.Max(0, playerDistanceScore) * 20f;

            // Bonus por tamaño del objeto (objetos grandes = mejor cobertura)
            float objectSize = coverObject.bounds.size.magnitude;
            score += Mathf.Min(objectSize, 10f) * 2f;

            // Bonus si está en dirección opuesta al jugador (huida efectiva)
            Vector3 directionToPlayer = (_player.position - transform.position).normalized;
            Vector3 directionToCover = (position - transform.position).normalized;
            float retreatAngle = Vector3.Dot(directionToPlayer, directionToCover);
            if (retreatAngle < 0) // Retrocediendo respecto al jugador
            {
                score += Mathf.Abs(retreatAngle) * 15f;
            }

            return score;
        }

        /// <summary>
        /// Verifica si un objeto bloquea la línea de visión entre dos puntos
        /// </summary>
        private bool DoesObjectBlockLineOfSight(Vector3 coverPosition, Vector3 targetPosition, Collider coverObject)
        {
            Vector3 direction = targetPosition - coverPosition;
            float distance = direction.magnitude;
            
            // Raycast desde la posición de cobertura hacia el jugador
            if (Physics.Raycast(coverPosition + Vector3.up, direction.normalized, out RaycastHit hit, distance, coverLayerMask))
            {
                // Verificar si el objeto que bloquea es el objeto de cobertura
                return hit.collider == coverObject || hit.transform.IsChildOf(coverObject.transform);
            }

            return false;
        }

        /// <summary>
        /// Verifica si hay línea de visión directa con un punto
        /// </summary>
        private bool HasLineOfSight(Vector3 targetPosition)
        {
            Vector3 origin = transform.position + Vector3.up * 1.5f; // Altura de ojos
            Vector3 direction = targetPosition - origin;
            float distance = direction.magnitude;

            return !Physics.Raycast(origin, direction.normalized, distance, coverLayerMask);
        }

        #if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!showDebugGizmos) return;

            // Radio de búsqueda
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, coverSearchRadius);

            // Posición de cobertura actual
            if (_currentCoverPosition.HasValue)
            {
                Gizmos.color = _isBehindCover ? Color.green : Color.yellow;
                Gizmos.DrawSphere(_currentCoverPosition.Value, 0.5f);
                Gizmos.DrawLine(transform.position, _currentCoverPosition.Value);

                // Objeto de cobertura
                if (_currentCoverObject != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawWireCube(_currentCoverObject.position, _currentCoverObject.GetComponent<Collider>()?.bounds.size ?? Vector3.one);
                }
            }

            // Posiciones evaluadas (debug)
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            foreach (var pos in _evaluatedPositions)
            {
                Gizmos.DrawSphere(pos, 0.3f);
            }
        }
        #endif
    }
}

