﻿using System;
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
        Ambient = 1 << 0,              // Camina libremente, idle/wander
        Combat = 1 << 1,               // Puede combatir
        Quest = 1 << 2,                // Ofrece misiones
        Companion = 1 << 6,            // Compañero del jugador
        InteractiveNarrative = 1 << 7  // Cadenas narrativas interactivas (secuencias)
    }
}

