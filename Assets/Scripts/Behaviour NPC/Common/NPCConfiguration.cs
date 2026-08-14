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
        
        [Tooltip("Config de narrativa interactiva (cadena de acciones al interactuar)")]
        public NPCInteractiveNarrativeConfig interactiveNarrativeConfig;
        
        [Tooltip("Config de compañero (seguir al jugador, formar equipo)")]
        public NPCPartyConfig partyConfig;

        [Tooltip("Config social (personalidad, relaciones, encuentros entre NPCs)")]
        public NPCSocialConfig socialConfig;
        [Header("Configuración Base (Común a todos)")]
        [Min(0f)]
        [Tooltip("Velocidad de caminata del NPC (m/s). Usado cuando el NPC se mueve normalmente.")]
        public float walkSpeed = 1.5f;
        
        [Min(0f)]
        [Tooltip("Velocidad de carrera del NPC (m/s). Usado en persecuciones o situaciones urgentes.")]
        public float runSpeed = 4f;
        
        [Min(0f)]
        [Tooltip("Velocidad de rotación del NPC (grados/segundo). Controla qué tan rápido gira.")]
        public float rotationSpeed = 180f;
        
        [Min(0f)]
        [Tooltip("Distancia de parada del NavMeshAgent (metros). El NPC para cuando está a esta distancia del destino.")]
        public float stoppingDistance = 0.5f;
        
        [Min(0f)]
        [Tooltip("Aceleración del NavMeshAgent (m/s²). Controla qué tan rápido acelera/desacelera.")]
        public float acceleration = 8f;
        
        // Configuración avanzada (oculta en Inspector, usa valores por defecto razonables)
        [HideInInspector] public float minAnimSpeed = 0.25f;
        [HideInInspector] public bool resetAnimationOnStateExit = true;
        [HideInInspector] public float navMeshSampleRadius = 2f;
        [HideInInspector] public float stuckCheckInterval = 1.5f;
        [HideInInspector] public float stuckThreshold = 0.02f;
        [HideInInspector] public bool useKinematicRigidbody = true;
        [HideInInspector] public RigidbodyConstraints rigidbodyConstraints = RigidbodyConstraints.FreezeRotation;
        public bool Validate(out string errors)
        {
            errors = "";
            bool isValid = true;
            
            // Ambient sin config asignada: usa defaults integrados (enableWander=true, radio=6m, etc.)
            // No es error — es comportamiento intencional para NPCs sin configuración explícita.
            
            // Validar Combat
            if (HasBehaviour(NPCBehaviourType.Combat) && combatConfig == null)
            {
                errors += "Behaviour Combat activado pero no hay combatConfig asignado.\n";
                isValid = false;
            }
            
            // Validar Quest
            if (HasBehaviour(NPCBehaviourType.Quest) && questConfig == null)
            {
                errors += "Behaviour Quest activado pero no hay questConfig asignado.\n";
                isValid = false;
            }
            
            // Validar Narrative (sistema de grafo - DEPRECADO, ahora usa InteractiveNarrative)
            // Ya no validamos Narrative porque está obsoleto
            
            // Validar Interactive Narrative
            if (HasBehaviour(NPCBehaviourType.InteractiveNarrative) && interactiveNarrativeConfig == null)
            {
                errors += "Behaviour InteractiveNarrative activado pero no hay interactiveNarrativeConfig asignado.\n";
                isValid = false;
            }
            
            // Validar Companion (Party)
            if (HasBehaviour(NPCBehaviourType.Companion) && partyConfig == null)
            {
                errors += "Behaviour Companion activado pero no hay partyConfig asignado.\n";
                isValid = false;
            }
            
            // Validar configs si están asignados
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
            
            if (interactiveNarrativeConfig != null && !interactiveNarrativeConfig.ValidateConfig(out string interactiveError))
            {
                errors += $"Interactive Narrative Config: {interactiveError}\n";
                isValid = false;
            }
            
            if (partyConfig != null && !partyConfig.ValidateConfig(out string partyError))
            {
                errors += $"Party Config: {partyError}\n";
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
        private bool _wanderDisabledOverride;
        public void DisableWander() => _wanderDisabledOverride = true;
        public bool enableWander => !_wanderDisabledOverride && (ambientConfig != null ? ambientConfig.enableWander : true);

        // Refugio de lluvia (ver SeekShelterState). NPCBehaviourManagerV2 desactiva esto
        // automáticamente para cualquier NPC con persistenceId (relevante para el grafo narrativo),
        // para que no abandone su puesto a mitad de una interacción de quest.
        private bool _shelterSeekingDisabledOverride;
        public void DisableShelterSeeking() => _shelterSeekingDisabledOverride = true;
        public bool canSeekShelter => !_shelterSeekingDisabledOverride && (ambientConfig != null ? ambientConfig.canSeekShelter : true);
        public float detectionRadius => combatConfig != null ? combatConfig.detectionRange : 10f;
        public float combatRange => combatConfig != null ? combatConfig.maxAttackDistance : 8f;
        public float meleeRange => combatConfig != null ? combatConfig.minAttackDistance : 2f;
    }
}
