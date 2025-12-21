using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Tipos de acciones que se pueden encadenar en una narrativa interactiva
    /// </summary>
    public enum NarrativeActionType
    {
        Dialogue,           // Mostrar diálogo
        Move,              // Mover a punto
        PlayAnimation,     // Reproducir animación
        StartQuest,        // Iniciar quest
        StartCombat,       // Iniciar combate
        Wait,              // Esperar X segundos
        Custom             // Evento personalizado (UnityEvent)
    }

    /// <summary>
    /// Representa una acción individual en una cadena narrativa
    /// </summary>
    [Serializable]
    public class NarrativeChainEntry
    {
        [Header("Tipo de Acción")]
        [Tooltip("Tipo de acción a ejecutar")]
        public NarrativeActionType actionType = NarrativeActionType.Dialogue;

        [Header("Dialogue")]
        [Tooltip("Diálogo a reproducir (si actionType = Dialogue)")]
        public DialogueAsset dialogue;

        [Header("Movement")]
        [Tooltip("Nombre del anchor de destino (si actionType = Move)")]
        public string targetAnchorName;
        
        [Tooltip("O usa un Transform directo")]
        public Transform targetTransform;
        
        [Tooltip("Duración máxima del movimiento")]
        [Min(1f)]
        public float maxMovementDuration = 15f;
        
        [Tooltip("Tiempo que camina visible antes del fade+teleport (999 = camina todo el trayecto)")]
        [Min(0.5f)]
        public float walkDisplayDuration = 999f;
        
        [Tooltip("Girar 180° al llegar")]
        public bool turnAroundOnArrival = false;

        [Header("Follow Player (Movement)")]
        [Tooltip("¿El NPC debe esperar al jugador si se aleja? (útil para 'sígueme')")]
        public bool waitForPlayer = false;
        
        [Tooltip("Distancia máxima permitida antes de esperar al jugador")]
        [Min(1f)]
        public float maxPlayerDistance = 10f;
        
        [Tooltip("Distancia mínima para reanudar el movimiento")]
        [Min(0.5f)]
        public float resumePlayerDistance = 5f;

        [Header("Animation")]
        [Tooltip("Nombre del trigger en el Animator (si actionType = PlayAnimation)")]
        public string animationTrigger;
        
        [Tooltip("O usa un AnimationClip directamente (.anim)")]
        public AnimationClip animationClip;
        
        [Tooltip("Duración de la animación (0 = espera hasta que termine)")]
        [Min(0f)]
        public float animationDuration = 0f;

        [Header("Quest")]
        [Tooltip("Quest a iniciar (si actionType = StartQuest)")]
        public QuestData questToStart;

        [Header("Combat")]
        [Tooltip("Target del combate (si actionType = StartCombat)")]
        public Transform combatTarget;

        [Header("Wait")]
        [Tooltip("Tiempo de espera en segundos (si actionType = Wait)")]
        [Min(0.1f)]
        public float waitDuration = 1f;

        [Header("Custom")]
        [Tooltip("Evento personalizado (si actionType = Custom)")]
        public UnityEvent customAction;

        [Header("Eventos")]
        [Tooltip("Se dispara cuando esta acción comienza")]
        public UnityEvent onActionStarted;
        
        [Tooltip("Se dispara cuando esta acción termina")]
        public UnityEvent onActionCompleted;
    }
}
