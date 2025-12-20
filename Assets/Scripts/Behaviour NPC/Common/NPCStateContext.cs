﻿using UnityEngine;
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
        public bool DebugMode { get; set; }
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
