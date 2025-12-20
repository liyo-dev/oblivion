using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Configuración para NPCs controlados por el grafo narrativo
    /// </summary>
    [CreateAssetMenu(fileName = "NPC_Narrative_Config", menuName = "NPC/Módulos/Narrative Config", order = 3)]
    public class NPCNarrativeConfig : NPCModuleConfigBase
    {
        [Header("Narrative Graph")]
        [Tooltip("ID único del NPC en el grafo narrativo. Debe coincidir con el usado en los nodos del grafo.")]
        public string narrativeID;
        
        [Tooltip("Tag alternativo para buscar este NPC en el grafo")]
        public string narrativeTag;
        
        [Header("Behavior")]
        [Tooltip("¿El NPC está completamente controlado por el grafo o puede tener comportamiento ambient cuando no esté en cinemática?")]
        public bool exclusiveNarrativeControl = true;
        
        [Tooltip("Config ambient para cuando no está en cinemática (si exclusiveNarrativeControl = false)")]
        public NPCAmbientConfig fallbackAmbientConfig;
        
        [Header("Persistence")]
        [Tooltip("¿Persistir la posición del NPC entre cargas de partida?")]
        public bool persistPosition = true;
        
        [Tooltip("¿Persistir el estado de activo/desactivado del NPC?")]
        public bool persistActiveState = true;
        
        public override bool ValidateConfig(out string errorMessage)
        {
            errorMessage = "";
            
            if (string.IsNullOrWhiteSpace(narrativeID) && string.IsNullOrWhiteSpace(narrativeTag))
            {
                errorMessage = "Debe especificar al menos un narrativeID o narrativeTag";
                return false;
            }
            
            if (!exclusiveNarrativeControl && fallbackAmbientConfig == null)
            {
                errorMessage = "Si exclusiveNarrativeControl = false, debe asignar fallbackAmbientConfig";
                return false;
            }
            
            return true;
        }
    }
}

