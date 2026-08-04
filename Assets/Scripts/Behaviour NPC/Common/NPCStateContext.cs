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

        // Refugio de lluvia (ver SeekShelterState / NPCWeatherAwareness)
        public bool ShouldSeekShelter { get; set; }
        public NPCShelterPoint CurrentShelter { get; set; }

        /// <summary>
        /// True mientras el GameObject está desactivado por haber "entrado" en una casa
        /// (NPCShelterType.HouseDoor). Con el GameObject inactivo, Unity no llama a Update(),
        /// así que NPCBehaviourManagerV2.HandleRainStopped() reactiva el GameObject directamente
        /// consultando este flag (la suscripción al evento de lluvia sí se ejecuta con el
        /// GameObject desactivado, al ser una invocación de delegado C# normal).
        /// </summary>
        public bool IsHiddenForShelter { get; set; }

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
