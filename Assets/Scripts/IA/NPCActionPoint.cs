using UnityEngine;

public enum NPCActionType
{
    Generic,      // vale para casi todo
    SleepSpot,    // camas / alfombras
    DrinkSpot,    // barra, mesa
    DanceFloor,   // pista
    LookSpot,     // balcón, mirador
    SocialSpot    // zona para interactuar con gente
}

[DisallowMultipleComponent]
public class NPCActionPoint : MonoBehaviour
{
    public NPCActionType type = NPCActionType.Generic;
    [Tooltip("Radio aceptable para considerarse 'llegado'. Si 0, usa stoppingDistance del Agent.")]
    public float arriveRadius = 0f;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.5f);
        Gizmos.DrawSphere(transform.position, 0.2f);
    }
}