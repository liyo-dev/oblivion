using System;
using UnityEngine;
using EasyTransition;

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
        ShowSpeechBubble,  // Mostrar bocadillo de texto/pensamiento
        JoinParty,         // Unirse al equipo del jugador
        LeaveParty,        // Abandonar el equipo del jugador
        CheckPartyMembers  // Verificar miembros del equipo para quests activas
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

        [Header("Speech Bubble")]
        [Tooltip("Texto a mostrar en el bocadillo (si actionType = ShowSpeechBubble)")]
        [TextArea(2, 5)]
        public string speechBubbleText;

        [Tooltip("¿Es un bocadillo de pensamiento? (cambia el estilo visual si está configurado)")]
        public bool isThoughtBubble;

        [Tooltip("Duración del bocadillo en segundos")]
        [Min(0.5f)]
        public float speechBubbleDuration = 3f;

        [Tooltip("Prefab específico para este bocadillo (opcional, si null usa el default del config)")]
        public GameObject speechBubblePrefabOverride;

        [Tooltip("¿Esperar a que termine la duración del bocadillo antes de continuar con la siguiente acción?")]
        public bool waitForBubble = true;

        [Header("Movement")]
        [Tooltip("Nombre del anchor de destino (si actionType = Move)")]
        public string targetAnchorName;
        
        [Tooltip("O usa un Transform directo")]
        public Transform targetTransform;
        
        [Tooltip("Mover a un punto aleatorio en lugar de un destino fijo")]
        public bool moveToRandomPoint = false;
        
        [Tooltip("Radio mínimo para el punto aleatorio")]
        [Min(1f)]
        public float randomMoveMinRadius = 5f;
        
        [Tooltip("Radio máximo para el punto aleatorio")]
        [Min(2f)]
        public float randomMoveMaxRadius = 15f;
        
        [Tooltip("Desaparecer (desactivar GameObject) al llegar al destino")]
        public bool disappearOnArrival = false;
        
        [Tooltip("Configuración de transición al desaparecer (VFX, fade, etc.)")]
        public TransitionSettings disappearTransition;
        
        [Tooltip("Si es líder de equipo, mover también a los compañeros a puntos aleatorios")]
        public bool moveTeamMembers = true;
        
        [Tooltip("Tiempo máximo permitido para que el NPC complete el movimiento (en segundos)")]
        [Min(1f)]
        public float maxMovementDuration = 15f;
        
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
        [Tooltip("Configuración del combate a iniciar (si actionType = StartCombat)")]
        public NPCCombatConfig combatConfig;
        
        [Tooltip("Target opcional del combate (si no se especifica, usa al jugador)")]
        public Transform combatTarget;
        
        [Header("Combat - Evento al Derrotar")]
        [Tooltip("¿Enviar evento al grafo narrativo cuando el NPC sea derrotado en combate?")]
        public bool sendEventOnDefeat = false;
        
        [Tooltip("Clave del evento a enviar cuando el NPC es derrotado (ej: 'Boss_Defeated', 'Enemy_Killed')")]
        public string defeatEventKey = "";
        
        [Tooltip("¿Enviar el evento ANTES de la animación de muerte? (por defecto se envía después de la secuencia de muerte)")]
        public bool sendDefeatEventBeforeDeath = false;

        [Header("Wait")]
        [Tooltip("Tiempo de espera en segundos (si actionType = Wait)")]
        [Min(0.1f)]
        public float waitDuration = 1f;

        [Header("Alert Icon (Alerta Visual)")]
        [Tooltip("¿Mostrar icono de alerta antes de esta acción?")]
        public bool showAlertIcon = false;
        
        [Tooltip("Prefab del icono de alerta (ej: Exclamation_Prefab)")]
        public GameObject alertIconPrefab;
        
        [Tooltip("Duración del icono de alerta en segundos")]
        [Min(0.5f)]
        public float alertIconDuration = 2f;
        
        [Tooltip("Offset del icono respecto al NPC")]
        public Vector3 alertIconOffset = new Vector3(0, 2.5f, 0);

        [Header("Narrative Event (Evento al Grafo)")]
        [Tooltip("¿Enviar evento al grafo narrativo al completar esta acción?")]
        public bool sendNarrativeEvent = false;
        
        [Tooltip("Clave del evento a enviar (ej: 'NPC_ReachedTower', 'Item_Delivered')")]
        public string narrativeEventKey = "";
        
        [Tooltip("¿Enviar el evento al INICIAR la acción? (por defecto se envía al completar)")]
        public bool sendEventOnStart = false;
    }
}
