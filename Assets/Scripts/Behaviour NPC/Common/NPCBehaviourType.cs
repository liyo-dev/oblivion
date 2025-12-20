using System;
using UnityEngine;

namespace Game.NPC.Common
{
    /// <summary>
    /// Tipos de comportamiento que puede tener un NPC
    /// </summary>
    [Flags]
    public enum NPCBehaviourType
    {
        None = 0,
        Ambient = 1 << 0,      // Camina libremente, idle/wander
        Combat = 1 << 1,       // Puede combatir
        Quest = 1 << 2,        // Ofrece misiones
        Narrative = 1 << 3,    // Controlado por grafo narrativo
        Dialogue = 1 << 4,     // Solo diálogo simple (no misiones)
        Merchant = 1 << 5,     // Tienda/comercio
        Companion = 1 << 6     // Compañero del jugador
    }
}

