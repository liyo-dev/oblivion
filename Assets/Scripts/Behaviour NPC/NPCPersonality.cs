using System;
using UnityEngine;

[Serializable]
public struct NPCPersonality
{
    [Range(0f, 1f), Tooltip("Qué tan dispuesto está a iniciar interacciones con otros NPCs (0=solitario, 1=muy social)")]
    public float sociability;

    [Range(0f, 1f), Tooltip("Qué tan amistoso es por defecto con extraños (0=hostil, 1=muy amigable)")]
    public float friendliness;

    [Range(0f, 1f), Tooltip("Nivel de energía: afecta si prefiere descansar o moverse (0=perezoso, 1=activo)")]
    public float energy;

    public static NPCPersonality CreateRandom() => new NPCPersonality
    {
        sociability  = UnityEngine.Random.value,
        friendliness = UnityEngine.Random.value,
        energy       = UnityEngine.Random.value,
    };

    public static readonly NPCPersonality Default = new NPCPersonality
    {
        sociability  = 0.5f,
        friendliness = 0.5f,
        energy       = 0.5f,
    };
}
