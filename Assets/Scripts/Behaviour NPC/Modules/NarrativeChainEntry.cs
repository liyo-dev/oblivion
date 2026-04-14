using System;
using UnityEngine;
using EasyTransition;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Tipos de acciones que se pueden encadenar en una narrativa interactiva
    /// </summary>
    /// <summary>
    /// IMPORTANTE: Los valores enteros son explícitos y NO deben cambiarse nunca.
    /// Unity serializa enums por su valor entero en los .asset. Si se elimina o reordena
    /// un valor sin mantener su entero, todos los ScriptableObjects que lo usaran quedarán
    /// apuntando a la acción equivocada. Para "eliminar" un tipo, márcalo como obsoleto
    /// con [System.Obsolete] pero mantén su valor numérico en la lista.
    /// Para añadir un tipo nuevo, asigna el siguiente número libre (nunca reutilices).
    /// </summary>
    public enum NarrativeActionType
    {
        Dialogue          = 0,  // Mostrar diálogo
        Move              = 1,  // Mover a punto
        PlayAnimation     = 2,  // Reproducir animación
        StartQuest        = 3,  // Iniciar quest
        StartCombat       = 4,  // Iniciar combate
        Wait              = 5,  // Esperar X segundos
        JoinParty         = 6,  // Unirse al equipo del jugador
        LeaveParty        = 7,  // Abandonar el equipo del jugador
        CheckPartyMembers = 8,  // Verificar miembros del equipo para quests activas
        MoveNearPlayer    = 9,  // Moverse a una posición cercana al jugador
        LeadPlayerToAnchor = 10, // Guiar al jugador hacia un anchor (modo escolta)
        TeleportNearPlayer = 11, // Aparece al lado del jugador instantáneamente
        // 12 era ShowSpeechBubble — eliminado. NO reutilizar el 12.
        TeleportPlayer     = 13, // Teletransporta al jugador a un anchor/Transform con transición
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
        [Tooltip("Nombre del anchor de destino. Usado por: Move, LeadPlayerToAnchor (escolta al guardia). " +
                 "Para escolta, configura también 'escortMaxDuration' en la sección 'Lead Player'.")]
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

        [Header("Move Near Player")]
        [Tooltip("Distancia al jugador a la que el NPC se posicionará (si actionType = MoveNearPlayer)")]
        [Min(0.5f)]
        public float nearPlayerRadius = 1.8f;

        [Tooltip("¿Mirar hacia el jugador al llegar?")]
        public bool lookAtPlayerOnArrival = true;

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

        [Header("Lead Player (Escort)")]
        [Tooltip("Duración máxima de la escolta en segundos. Aumenta para trayectos largos. " +
                 "IMPORTANTE: Si el guardia no se mueve o para antes de llegar, sube este valor. " +
                 "El destino se configura en 'targetAnchorName' (sección Movement).")]
        [Min(5f)]
        public float escortMaxDuration = 120f;

        [Tooltip("Distancia máxima entre el NPC guía y el jugador antes de que el NPC pare y vuelva a buscarle.")]
        [Min(2f)]
        public float escortMaxPlayerDistance = 8f;

        [Tooltip("Distancia a la que el jugador debe estar del NPC para que éste reanude su camino al anchor.")]
        [Min(1f)]
        public float escortResumeDistance = 3f;

        [Tooltip("Diálogo que muestra el NPC cuando el jugador se aleja demasiado y el NPC vuelve a buscarlo. Vacío = sin diálogo.")]
        public DialogueAsset outOfRangeDialogue;

        [Tooltip("Velocidad del NPC durante la escolta (unidades/s). " +
                 "Súbela si el jugador le adelanta. 0 = usar la velocidad por defecto del NPC.")]
        [Min(0f)]
        public float escortNpcSpeed = 0f;

        [Tooltip("Multiplicador de velocidad del jugador durante la escolta (0.5 = mitad de velocidad). " +
                 "Evita que el jugador adelante al NPC. 1 = sin cambio.")]
        [Range(0.1f, 1f)]
        public float escortPlayerSpeedMultiplier = 0.6f;

        [Header("Teleport Player")]
        [Tooltip("Transición de pantalla para cubrir y descubrir el teletransporte del jugador")]
        public TransitionSettings teleportTransition;

        [Header("Narrative Event (Evento al Grafo)")]
        [Tooltip("¿Enviar evento al grafo narrativo al completar esta acción?")]
        public bool sendNarrativeEvent = false;
        
        [Tooltip("Clave del evento a enviar (ej: 'NPC_ReachedTower', 'Item_Delivered')")]
        public string narrativeEventKey = "";
        
        [Tooltip("¿Enviar el evento al INICIAR la acción? (por defecto se envía al completar)")]
        public bool sendEventOnStart = false;
    }
}
