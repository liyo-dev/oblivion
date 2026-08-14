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

        /// <summary>
        /// Habilita un NavMeshAgent evitando el error de consola "Failed to create agent because
        /// there is no valid NavMesh": Unity comprueba transform.position EN EL INSTANTE en que se
        /// pone agent.enabled = true (para colocar el agente sobre la malla), antes de que el código
        /// llamante pueda hacer Warp/SetDestination. Si en ese instante el transform está fuera del
        /// NavMesh (tras un salto, una animación de muerte, o mientras el agente estuvo desactivado),
        /// el error se loggea aunque el código lo corrija justo después con Warp — por eso hay que
        /// recolocar el transform en un punto válido ANTES de habilitar, no después.
        /// Si el agente ya está habilitado, no hace nada (no-op seguro).
        /// </summary>
        /// <param name="agent">Agente a habilitar.</param>
        /// <param name="t">Transform del mismo GameObject (se recoloca si hace falta).</param>
        /// <param name="desiredPosition">Posición cerca de la cual buscar un punto válido de NavMesh (normalmente transform.position o el punto de destino/aterrizaje).</param>
        /// <param name="searchRadius">Radio de búsqueda en NavMesh.SamplePosition.</param>
        /// <returns>True si el agente quedó habilitado y sobre el NavMesh.</returns>
        public static bool SafeEnable(NavMeshAgent agent, Transform t, Vector3 desiredPosition, float searchRadius = 5f)
        {
            if (agent == null) return false;
            if (agent.enabled) return true;

            if (NavMesh.SamplePosition(desiredPosition, out var hit, Mathf.Max(1f, searchRadius), NavMesh.AllAreas))
            {
                if (t != null) t.position = hit.position;
                agent.enabled = true;
                agent.Warp(hit.position);
                return true;
            }

            // No se encontró NavMesh cerca: habilitar igual para no romper el flujo existente.
            // El código llamante debe seguir comprobando agent.isOnNavMesh tras esta llamada.
            agent.enabled = true;
            return agent.isOnNavMesh;
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

            // Limpiar velocidad residual. ResetPath() ya cancela el path y desiredVelocity;
            // NO llamar SetDestination aquí: en Unity 6 puede resetear isStopped internamente
            // antes de que LateUpdate lo lea, produciendo falsos warnings del safety check.
            agent.velocity = Vector3.zero;
            agent.nextPosition = agent.transform.position;
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

            float vel = agent.velocity.magnitude;

            // Usar desiredVelocity solo cuando el agente ya está en movimiento real.
            // Evita mostrar animación de caminar cuando el agente está bloqueado
            // (desiredVelocity > 0 pero velocity ≈ 0).
            float refSpeed = vel >= 0.05f ? Mathf.Max(vel, agent.desiredVelocity.magnitude) : vel;

            return Mathf.Clamp01(refSpeed / agent.speed);
        }
    }
}
