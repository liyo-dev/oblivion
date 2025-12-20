using UnityEngine;
namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración de misiones para NPCs.
    /// Aquí se configura la cadena de misiones (Quest Chain) que ofrece el NPC.
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Quest_Config", menuName = "NPC/Módulos/Quest Config", order = 4)]
    public class NPCQuestConfig : NPCModuleConfigBase
    {
        [Header("Quest Chain")]
        [Tooltip("Cadena de misiones que ofrece este NPC. Cada elemento representa una quest en orden.")]
        public QuestChainEntry[] questChain = System.Array.Empty<QuestChainEntry>();
        [Header("Item Detection (Global)")]
        [Tooltip("¿Detectar automáticamente cuando el jugador entrega ítems? (Se aplica globalmente)")]
        public bool enableItemDetection = true;
        [Min(0f)]
        [Tooltip("Radio de detección de ítems por defecto")]
        public float detectionRadius = 3f;
        [Range(0f, 180f)]
        [Tooltip("Ángulo de detección de ítems por defecto")]
        public float detectionAngle = 90f;
        [Tooltip("Layer de detección de ítems")]
        public LayerMask detectionLayer = ~0;
        [Min(0.05f)]
        [Tooltip("Intervalo de escaneo de ítems")]
        public float detectionInterval = 0.33f;
        [Header("Behavior")]
        [Tooltip("¿El NPC gira hacia el jugador al interactuar?")]
        public bool rotateToPlayerOnInteract = true;
        [Min(0f)]
        [Tooltip("Duración de la rotación")]
        public float rotationDuration = 0.3f;
        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            if (questChain == null || questChain.Length == 0)
            {
                errorMessage = "Quest chain no puede estar vacío. Añade al menos una quest.";
                return false;
            }
            for (int i = 0; i < questChain.Length; i++)
            {
                var entry = questChain[i];
                if (entry == null)
                {
                    errorMessage = $"Quest chain entry {i} es null";
                    return false;
                }
                if (entry.questData == null)
                {
                    errorMessage = $"Quest chain entry {i} no tiene questData asignado";
                    return false;
                }
                if (entry.autoDetectItemDelivery && string.IsNullOrEmpty(entry.itemTag))
                {
                    errorMessage = $"Quest {i} tiene autoDetectItemDelivery activado pero itemTag vacío";
                    return false;
                }
                if (entry.requireItemInInventory && entry.requiredItem == null)
                {
                    errorMessage = $"Quest {i} requiere ítem pero requiredItem es null";
                    return false;
                }
            }
            if (detectionRadius <= 0f)
            {
                errorMessage = "Detection radius debe ser mayor a 0";
                return false;
            }
            return true;
        }
    }
}
