﻿﻿﻿using UnityEngine;
using UnityEngine.AI;
namespace Game.NPC.Common
{
    public class NPCStateContext
    {
        public Transform Transform { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public NPCSimpleAnimator Animator { get; private set; }
        public Animator UnityAnimator { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public Transform Player { get; set; }
        public Transform PlayerCamera { get; set; }
        public NPCBrain Brain { get; set; }
        public NPCConfiguration Config { get; set; }
        public Vector3 LastKnownPosition { get; set; }
        public Quaternion LastKnownRotation { get; set; }
        public Vector3 TargetDestination { get; set; }
        public bool HasReachedDestination { get; set; }
        public bool IsInteracting { get; set; }
        public bool IsInCombat { get; set; }
        public bool IsInCinematic { get; set; }
        public bool WasDefeatedInCombat { get; set; } // NPC ha sido derrotado
        public bool IsPinnedByParty { get; set; }    // NPC anclado al salir del party (no debe vagar)
        public bool DebugMode { get; set; }

        // Social
        public Transform PendingSocialPartner { get; set; }
        public NPCRelationType PendingSocialRelation { get; set; }
        public float LastSocialEncounterTime { get; set; }

        /// <summary>
        /// Identidad estable de este NPC para el registro de relaciones dinámicas
        /// (NPCRelationshipRegistry). Normalmente es socialConfig.npcId, pero cuando ese
        /// campo está vacío (NPCs de relleno que comparten un NPCSocialConfig de arquetipo,
        /// ej. NPC_Social_Archetype_Friendly.asset) se genera un id único por instancia en
        /// NPCBehaviourManagerV2.Awake() para que no se fusionen entre sí. Nunca se escribe
        /// de vuelta al ScriptableObject compartido.
        /// </summary>
        public string RelationshipId { get; set; }

        // Refugio de lluvia (ver SeekShelterState / ReturnFromShelterState / NPCWeatherAwareness)
        public bool ShouldSeekShelter { get; set; }
        public NPCShelterPoint CurrentShelter { get; set; }

        /// <summary>
        /// Posición donde estaba el NPC justo antes de empezar a caminar hacia un refugio,
        /// capturada por SeekShelterState.OnEnter. Cuando deja de llover, ReturnFromShelterState
        /// usa este punto para que el NPC regrese exactamente a donde estaba en vez de quedarse
        /// vagando desde la puerta/árbol donde se refugió.
        /// </summary>
        public Vector3 ShelterOriginPosition { get; set; }

        /// <summary>
        /// True desde que SeekShelterState captura ShelterOriginPosition hasta que
        /// ReturnFromShelterState completa el regreso (o se descarta por no tener camino válido).
        /// Evita que reintentos dentro de SeekShelterState (p.ej. el punto de refugio se ocupó justo
        /// antes de llegar) sobreescriban el origen real con una posición intermedia.
        /// </summary>
        public bool HasShelterOrigin { get; set; }

        /// <summary>
        /// Última posición conocida del NPC sobre el área caminable del NavMesh, justo antes de
        /// que SeekShelterState empezara un tramo de aproximación manual (fuera de NavMesh) hacia
        /// el punto exacto de un NPCShelterPoint. Muchos árboles tienen el hueco no-transitable
        /// de su bakeo mucho más grande que el tronco (cubre toda la copa), así que el punto de
        /// refugio cae fuera del NavMesh aunque no haya ningún obstáculo físico real ahí.
        /// ReturnFromShelterState usa este punto para sacar al NPC por el mismo camino "a mano"
        /// antes de reengancharlo al NavMeshAgent. Ver NPCStateBase.BeginManualApproach.
        /// </summary>
        public Vector3 ShelterEdgePosition { get; set; }

        /// <summary>True mientras ShelterEdgePosition contenga un valor válido (ver arriba).</summary>
        public bool HasShelterEdgePosition { get; set; }

        public NPCStateContext(NPCBrain brain, Transform transform, NavMeshAgent agent,
            NPCSimpleAnimator animator, Animator unityAnimator, Rigidbody rigidbody)
        {
            Brain = brain;
            Transform = transform;
            Agent = agent;
            Animator = animator;
            UnityAnimator = unityAnimator;
            Rigidbody = rigidbody;
            LastKnownPosition = transform.position;
            LastKnownRotation = transform.rotation;
            LastSocialEncounterTime = float.NegativeInfinity;
        }
        public void Log(string message)
        {
            if (DebugMode)
                Debug.Log($"[NPC:{Transform.name}] {message}");
        }
        public void LogWarning(string message)
        {
            if (DebugMode)
                Debug.LogWarning($"[NPC:{Transform.name}] {message}");
        }
        public void LogError(string message)
        {
            Debug.LogError($"[NPC:{Transform.name}] {message}");
        }
    }
}
