using System;
using UnityEngine;

public enum NPCRelationType
{
    Stranger     = 0,
    Acquaintance = 1,
    Friend       = 2,
    BestFriend   = 3,
    Rival        = 4,
    Enemy        = 5,
}

[Serializable]
public struct NPCRelationshipEntry
{
    [Tooltip("ID del otro NPC (debe coincidir con el npcId de su NPCSocialConfig)")]
    public string npcId;

    [Tooltip("Tipo de relación con ese NPC")]
    public NPCRelationType type;
}
