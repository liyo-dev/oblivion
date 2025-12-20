using System;
using UnityEngine;
using Game.NPC.Modules;
namespace Game.NPC.Common
{
    /// <summary>
    /// Configuración modular de NPC basada en ScriptableObjects.
    /// Solo configuras los módulos que el NPC realmente necesita.
    /// </summary>
    [Serializable]
    public class NPCConfiguration
    {
        [Header("Tipo de Comportamiento")]
        [Tooltip("Selecciona qué comportamientos tendrá este NPC")]
        public NPCBehaviourType behaviourType = NPCBehaviourType.Ambient;
        [Header("Módulos de Configuración")]
        [Tooltip("Config de comportamiento ambiental (Idle/Wander)")]
        public NPCAmbientConfig ambientConfig;
        [Tooltip("Config de combate (stats, ataques, rangos)")]
        public NPCCombatConfig combatConfig;
        [Tooltip("Config de misiones (quest chain)")]
        public NPCQuestConfig questConfig;
        [Tooltip("Config de narrativa (ID para el grafo)")]
        public NPCNarrativeConfig narrativeConfig;
        [Header("Configuración Base (Común a todos)")]
        [Min(0f)] public float walkSpeed = 1.5f;
        [Min(0f)] public float runSpeed = 4f;
        [Min(0f)] public float rotationSpeed = 180f;
        [Min(0f)] public float stoppingDistance = 0.5f;
        [Min(0f)] public float acceleration = 8f;
        [Header("Animación")]
        [Range(0f, 1f)] public float minAnimSpeed = 0.25f;
        public bool resetAnimationOnStateExit = true;
        [Header("NavMesh")]
        [Min(0.1f)] public float navMeshSampleRadius = 2f;
        [Min(0.1f)] public float stuckCheckInterval = 1.5f;
        [Min(0.01f)] public float stuckThreshold = 0.02f;
        [Header("Física")]
        public bool useKinematicRigidbody = true;
        public RigidbodyConstraints rigidbodyConstraints = RigidbodyConstraints.FreezeRotation;
        public bool Validate(out string errors)
        {
            errors = "";
            bool isValid = true;
            if (HasBehaviour(NPCBehaviourType.Ambient) && ambientConfig == null)
            {
                errors += "Behaviour Ambient activado pero no hay ambientConfig asignado.\n";
                isValid = false;
            }
            if (HasBehaviour(NPCBehaviourType.Combat) && combatConfig == null)
            {
                errors += "Behaviour Combat activado pero no hay combatConfig asignado.\n";
                isValid = false;
            }
            if (HasBehaviour(NPCBehaviourType.Quest) && questConfig == null)
            {
                errors += "Behaviour Quest activado pero no hay questConfig asignado.\n";
                isValid = false;
            }
            if (HasBehaviour(NPCBehaviourType.Narrative) && narrativeConfig == null)
            {
                errors += "Behaviour Narrative activado pero no hay narrativeConfig asignado.\n";
                isValid = false;
            }
            if (ambientConfig != null && !ambientConfig.ValidateConfig(out string ambientError))
            {
                errors += $"Ambient Config: {ambientError}\n";
                isValid = false;
            }
            if (combatConfig != null && !combatConfig.ValidateConfig(out string combatError))
            {
                errors += $"Combat Config: {combatError}\n";
                isValid = false;
            }
            if (questConfig != null && !questConfig.ValidateConfig(out string questError))
            {
                errors += $"Quest Config: {questError}\n";
                isValid = false;
            }
            if (narrativeConfig != null && !narrativeConfig.ValidateConfig(out string narrativeError))
            {
                errors += $"Narrative Config: {narrativeError}\n";
                isValid = false;
            }
            return isValid;
        }
        public bool HasBehaviour(NPCBehaviourType type)
        {
            return (behaviourType & type) == type;
        }
        public float wanderRadius => ambientConfig != null ? ambientConfig.wanderRadius : 6f;
        public float minIdleTime => ambientConfig != null ? ambientConfig.minIdleTime : 1.2f;
        public float maxIdleTime => ambientConfig != null ? ambientConfig.maxIdleTime : 3.0f;
        public bool enableWander => ambientConfig != null && ambientConfig.enableWander;
        public float detectionRadius => combatConfig != null ? combatConfig.detectionRange : 10f;
        public float combatRange => combatConfig != null ? combatConfig.combatRange : 8f;
        public float meleeRange => combatConfig != null ? combatConfig.meleeRange : 2f;
        public float attackCooldown => combatConfig != null ? combatConfig.attackCooldown : 1.5f;
    }
}
