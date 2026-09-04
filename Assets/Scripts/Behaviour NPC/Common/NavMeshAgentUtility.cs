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

            // FIX INC-NPCS-EN-ARBOLES (14 ago 2026): este reseteo de nextPosition solo tiene
            // sentido con el agente sobre el NavMesh (limpiar residuo de un path real). Si se
            // llama con el agente fuera de malla (p.ej. mientras SeekShelterState lo mueve a mano
            // bajo la copa de un árbol, ver NPCStateBase.BeginManualApproach), asignar aquí una
            // posición fuera de malla hace que el NavMeshAgent la reproyecte sobre el NavMesh en
            // cuanto pueda — y si algo reactiva agent.updatePosition después, arrastra el
            // transform de vuelta a ese punto proyectado (el NPC se "escupe" del árbol). No hay
            // nada que limpiar aquí si ya está fuera de malla: se deja tal cual.
            if (agent.isOnNavMesh)
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

        // FIX 4 sep 2026 (petición de Raúl: "cuando eldran camina... antes hacia la animación
        // correcta ahora los npcs... cuando les tengo que seguir del punto A al punto B están
        // haciendo una animación de andar que no es la que toca, deben hacer la que hagan los
        // personajes principales"): el Animator Controller genérico de los NPCs (NPC_NoWeapon,
        // usado por Eldran y compañía) resultó ser LITERALMENTE el mismo blend tree "Free
        // Locomotion" y los mismos clips que usa el propio Invector@BasicLocomotion.controller de
        // los personajes jugables (mismos guids de WalkFWD_RM en el umbral 0.5 y MoveFWD_Normal_RM
        // en el umbral 1) — no son sistemas de animación distintos como se sospechaba al principio.
        // El problema es de CALIBRACIÓN: ComputeSpeedFactor (arriba) devuelve
        // velocidad_actual/agent.speed, así que en cuanto un NavMeshAgent alcanza su velocidad
        // configurada -algo casi inmediato al hacer SetDestination en una secuencia de "sígueme",
        // ver CinematicState.MoveToPositionSequence/MoveToAction/LeadPlayerToAnchorSequence- el
        // valor llega a ~1.0, que en el blend tree cae en el tramo de MoveFWD_Normal (el mismo
        // clip que el jugador solo enseña esprintando), no en WalkFWD (umbral 0.5, lo que el
        // jugador enseña al caminar con normalidad). De ahí que estos NPCs parecieran "trotar" en
        // vez de caminar. Este helper satura el resultado al tramo de caminar del blend tree, para
        // usar en las secuencias donde el NPC debe caminar con paso normal (nunca correr) sin
        // tocar ComputeSpeedFactor en sí -otros llamadores (p.ej. combate/persecución) sí pueden
        // querer el rango completo 0-1 para mostrar una marcha más rápida-.
        public const float WalkGaitThreshold = 0.5f;

        public static float ComputeWalkGaitSpeedFactor(NavMeshAgent agent)
        {
            float raw = ComputeSpeedFactor(agent);
            return raw > 0f ? Mathf.Min(raw, WalkGaitThreshold) : 0f;
        }
    }
}
