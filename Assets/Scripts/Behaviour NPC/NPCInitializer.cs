using System;
using UnityEngine;

namespace Game.NPC
{
    /// <summary>
    /// Sistema de inicialización robusto para NPCs.
    /// CERO delays, CERO "yield return null". Usa eventos y verificaciones directas.
    /// </summary>
    public static class NPCInitializer
    {
        /// <summary>
        /// Verifica si un NPC está completamente inicializado y listo para usar.
        /// NO usa delays ni coroutines, verificación INMEDIATA.
        /// </summary>
        public static bool IsNPCReady(NPCBehaviourManagerV2 npc, out string reason)
        {
            reason = "";
            
            if (npc == null) 
            { 
                reason = "NPC is null"; 
                return false; 
            }
            
            if (npc.Agent == null) 
            { 
                reason = "NavMeshAgent is null"; 
                return false; 
            }
            
            if (!npc.Agent.enabled) 
            { 
                reason = "NavMeshAgent disabled"; 
                return false; 
            }
            
            if (!npc.Agent.isOnNavMesh) 
            { 
                reason = "Not on NavMesh"; 
                return false; 
            }
            
            if (npc.Brain == null) 
            { 
                reason = "Brain is null"; 
                return false; 
            }
            
            return true;
        }
    }
}
