using UnityEngine;
using UnityEngine.AI;

namespace Game.NPC.Common
{
    public static class NavMeshAgentUtility
    {
        const int DefaultSampleAttempts = 8;

        public static bool EnsureAgentOnNavMesh(NavMeshAgent agent, Vector3 origin, float searchRadius)
        {
            if (agent == null) return false;
            if (agent.isOnNavMesh) return true;

            if (NavMesh.SamplePosition(origin, out var hit, Mathf.Max(1f, searchRadius), NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return true;
            }
            return false;
        }

        public static bool TryGetRandomPoint(Vector3 origin, float radius, out Vector3 result, int attempts = DefaultSampleAttempts)
        {
            for (int i = 0; i < attempts; i++)
            {
                var randomPoint = origin + Random.insideUnitSphere * radius;
                if (NavMesh.SamplePosition(randomPoint, out var hit, radius, NavMesh.AllAreas))
                {
                    result = hit.position;
                    return true;
                }
            }

            result = origin;
            return false;
        }

        public static void SafeSetStopped(NavMeshAgent agent, bool stopped)
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = stopped;
        }

        public static void HardStop(NavMeshAgent agent)
        {
            if (agent == null)
                return;

            if (agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }

            // ✅ LIMPIEZA AGRESIVA: Eliminar toda velocidad residual
            agent.velocity = Vector3.zero;
            agent.nextPosition = agent.transform.position;
            
            // ✅ También limpiar la velocidad deseada para evitar acumulación
            // Esto previene que el agente "recuerde" hacia dónde iba
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(agent.transform.position);
            }
        }

        public static void SetDestination(NavMeshAgent agent, Vector3 destination, float stoppingDistance = -1f)
        {
            if (agent == null) return;
            if (!agent.isOnNavMesh) return;

            agent.isStopped = false;
            if (stoppingDistance >= 0f)
                agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(destination);
        }

        public static float ComputeSpeedFactor(NavMeshAgent agent)
        {
            if (agent == null || !agent.isOnNavMesh)
                return 0f;

            if (agent.speed <= 0.01f)
                return 0f;

            // Usar la mayor entre velocidad real y deseada para reflejar la intención de locomoción
            float vel = agent.velocity.magnitude;
            float desired = agent.desiredVelocity.magnitude;
            float refSpeed = Mathf.Max(vel, desired);

            // Si hay camino y aún lejos, evitar que el factor caiga a cero por amortiguación
            if (refSpeed < 0.05f && agent.hasPath && !agent.isStopped && agent.remainingDistance > agent.stoppingDistance + 0.05f)
                refSpeed = 0.05f;

            return Mathf.Clamp01(refSpeed / agent.speed);
        }
    }
}
