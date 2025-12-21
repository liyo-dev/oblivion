using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de cadena narrativa interactiva para NPCs.
    /// Permite encadenar diálogos, movimientos, animaciones, quests, combat, etc.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_InteractiveNarrative_Config", menuName = "NPC/Módulos/Interactive Narrative Config", order = 5)]
    public class NPCInteractiveNarrativeConfig : NPCModuleConfigBase
    {
        [Header("Narrative Chain")]
        [Tooltip("Cadena de acciones narrativas que se ejecutan al interactuar")]
        public NarrativeChainEntry[] narrativeChain = System.Array.Empty<NarrativeChainEntry>();

        [Header("Configuración")]
        [Tooltip("¿Esta narrativa solo se puede ejecutar una vez?")]
        public bool singleUse = true;

        [Tooltip("¿Persistir el estado de uso entre sesiones?")]
        public bool persistState = true;

        [Tooltip("ID único para persistencia (generado automáticamente si está vacío)")]
        public string persistenceId;

        [Header("Behavior")]
        [Tooltip("¿El NPC gira hacia el jugador al interactuar?")]
        public bool rotateToPlayerOnInteract = true;

        [Min(0f)]
        [Tooltip("Duración de la rotación")]
        public float rotationDuration = 0.3f;

        [Header("Estado Post-Narrativa")]
        [Tooltip("¿Qué hace el NPC después de completar toda la cadena?")]
        public PostNarrativeState postNarrativeState = PostNarrativeState.Idle;

        [Tooltip("Config ambient si postNarrativeState = SwitchToAmbient")]
        public NPCAmbientConfig postNarrativeAmbientConfig;

        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";

            if (narrativeChain == null || narrativeChain.Length == 0)
            {
                errorMessage = "Narrative chain no puede estar vacío. Añade al menos una acción.";
                return false;
            }

            for (int i = 0; i < narrativeChain.Length; i++)
            {
                var entry = narrativeChain[i];
                if (entry == null)
                {
                    errorMessage = $"Narrative chain entry {i} es null";
                    return false;
                }

                // Validar según tipo de acción
                switch (entry.actionType)
                {
                    case NarrativeActionType.Dialogue:
                        if (entry.dialogue == null)
                        {
                            errorMessage = $"Entry {i} tipo Dialogue requiere un DialogueAsset";
                            return false;
                        }
                        break;

                    case NarrativeActionType.Move:
                        if (string.IsNullOrEmpty(entry.targetAnchorName) && entry.targetTransform == null)
                        {
                            errorMessage = $"Entry {i} tipo Move requiere targetAnchorName o targetTransform";
                            return false;
                        }
                        break;

                    case NarrativeActionType.PlayAnimation:
                        if (string.IsNullOrEmpty(entry.animationTrigger))
                        {
                            errorMessage = $"Entry {i} tipo PlayAnimation requiere animationTrigger";
                            return false;
                        }
                        break;

                    case NarrativeActionType.StartQuest:
                        if (entry.questToStart == null)
                        {
                            errorMessage = $"Entry {i} tipo StartQuest requiere questToStart";
                            return false;
                        }
                        break;

                    case NarrativeActionType.StartCombat:
                        if (entry.combatTarget == null)
                        {
                            errorMessage = $"Entry {i} tipo StartCombat requiere combatTarget";
                            return false;
                        }
                        break;
                }
            }

            if (postNarrativeState == PostNarrativeState.SwitchToAmbient && postNarrativeAmbientConfig == null)
            {
                errorMessage = "PostNarrativeState = SwitchToAmbient requiere postNarrativeAmbientConfig";
                return false;
            }

            return true;
        }

        private void OnValidate()
        {
            // Auto-generar ID de persistencia si está vacío y se requiere persistencia
            if (persistState && string.IsNullOrEmpty(persistenceId))
            {
                persistenceId = System.Guid.NewGuid().ToString();
            }
        }
    }

    /// <summary>
    /// Estado del NPC después de completar la narrativa
    /// </summary>
    public enum PostNarrativeState
    {
        Idle,               // Se queda en Idle
        Wander,            // Activa comportamiento Wander
        SwitchToAmbient,   // Cambia a un NPCAmbientConfig específico
        Disable            // Se desactiva el GameObject
    }
}
