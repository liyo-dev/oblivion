using System;
using UnityEngine;
using UnityEngine.Events;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Representa una entrada en la cadena de misiones de un NPC.
    /// Contiene toda la información de una quest: datos, modo de completado, detección, diálogos, etc.
    /// </summary>
    [Serializable]
    public class QuestChainEntry
    {
        [Tooltip("Quest correspondiente a esta etapa.")]
        public QuestData questData;

        [Tooltip("Modo de completado de la quest.")]
        public QuestCompletionMode completionMode = QuestCompletionMode.Manual;

        [Header("Detección de objetos")]
        [Tooltip("Si está activado, detecta automáticamente cuando el jugador entrega el ítem.")]
        public bool autoDetectItemDelivery = false;
        
        [Tooltip("Índice del step de la quest que requiere entrega de ítem.")]
        public int itemDeliveryStepIndex = 1;
        
        [Tooltip("Tag del ítem a detectar.")]
        public string itemTag = "Untagged";
        
        [Tooltip("Ignorar FOV; usa solo radio alrededor del NPC para detección")]
        public bool ignoreFovForItem = false;
        
        [Tooltip("Si >0, usa este radio en lugar del del módulo para detectar el item")]
        public float overrideDetectionRadius = 0f;

        [Header("Verificación de Inventario")]
        [Tooltip("Lista de items requeridos en el inventario. Cada item se asocia a un step de la quest.")]
        public ItemRequirement[] requiredItems = System.Array.Empty<ItemRequirement>();

        [Header("Diálogos")]
        [Tooltip("Diálogo antes de aceptar la quest.")]
        public DialogueAsset dlgBefore;
        
        [Tooltip("Diálogo mientras la quest está en progreso.")]
        public DialogueAsset dlgInProgress;
        
        [Tooltip("Diálogo al entregar/completar la quest.")]
        public DialogueAsset dlgTurnIn;
        
        [Tooltip("Diálogo después de completar la quest.")]
        public DialogueAsset dlgCompleted;

        [Header("Eventos")]
        [Tooltip("Se dispara cuando se completa esta quest.")]
        public UnityEvent onQuestCompleted;
        
        [Tooltip("Se dispara cuando inicia el diálogo de oferta (dlgBefore) para esta etapa de la cadena.")]
        public UnityEvent onOfferDialogueStarted;
        
        [Tooltip("Se dispara cuando termina el diálogo de oferta (dlgBefore) para esta etapa de la cadena.")]
        public UnityEvent onOfferDialogueFinished;
        
        [Tooltip("Se dispara cuando termina la post-action (movimiento, teleport, etc).")]
        public UnityEvent onPostActionCompleted;
        
        [Header("Acción Post-Quest")]
        [Tooltip("Qué hace el NPC automáticamente cuando se completa esta quest")]
        public QuestPostAction postAction = new QuestPostAction();
    }
    
    /// <summary>
    /// Tipos de acciones que puede ejecutar el NPC al completar una quest
    /// </summary>
    public enum QuestActionType
    {
        None = 0,           // No hace nada
        Move = 1,           // Moverse a un punto/anchor
        Teleport = 2,       // Teletransporte instantáneo a anchor
        StartCombat = 3,    // Entrar en modo combate
        Dialogue = 4,       // Solo reproducir diálogo
        Custom = 99         // Evento personalizado (UnityEvent)
    }
    
    /// <summary>
    /// Configuración de acción post-quest
    /// </summary>
    [Serializable]
    public class QuestPostAction
    {
        [Tooltip("Tipo de acción a ejecutar")]
        public QuestActionType actionType = QuestActionType.None;
        
        [Header("Diálogo Pre-Acción")]
        [Tooltip("Diálogo antes de ejecutar la acción (opcional)")]
        public DialogueAsset dialogueBeforeAction;
        
        [Header("Move/Teleport Settings")]
        [Tooltip("Nombre del anchor de destino (sistema de teletransporte)")]
        public string targetAnchorName;
        
        [Tooltip("O usa un Transform directo (si no usas anchor)")]
        public Transform targetTransform;
        
        [Tooltip("Duración máxima del movimiento (solo para Move)")]
        [Min(1f)]
        public float maxMovementDuration = 15f;
        
        [Tooltip("Tiempo que camina visible antes del fade+teleport (999 = camina todo el trayecto sin fade)")]
        [Min(0.5f)]
        public float walkDisplayDuration = 3f;
        
        [Tooltip("Girar 180° al llegar")]
        public bool turnAroundOnArrival = false;
        
        [Header("Combat Settings")]
        [Tooltip("Target del combate (si actionType = StartCombat)")]
        public Transform combatTarget;
        
        [Header("Dialogue Settings")]
        [Tooltip("Diálogo a reproducir (si actionType = Dialogue)")]
        public DialogueAsset dialogueToPlay;
        
        [Header("Timing")]
        [Tooltip("Espera antes de ejecutar la acción")]
        [Min(0f)]
        public float delayBeforeAction = 0f;
        
        [Header("Transition Settings (Move/Teleport)")]
        [Tooltip("TransitionSettings para el fade/transición (ej: Fade.asset del sistema EasyTransition)")]
        public EasyTransition.TransitionSettings transitionSettings;
        
        [Tooltip("¿Usar transición para el movimiento/teleporte? (si false, será instantáneo)")]
        public bool useTransition = true;
        
        [Tooltip("Delay antes de iniciar la transición (segundos)")]
        [Min(0f)]
        public float transitionDelay = 0f;
        
        [Header("Custom Action")]
        [Tooltip("Evento personalizado (si actionType = Custom)")]
        public UnityEvent customAction;
    }
    
    /// <summary>
    /// Requisito de item para una quest
    /// </summary>
    [Serializable]
    public class ItemRequirement
    {
        [Tooltip("El item requerido")]
        public ItemData item;
        
        [Tooltip("Cantidad requerida")]
        [Min(1)] public int amount = 1;
        
        [Tooltip("ID de la condición del step de la quest (ej: ITEM_Potion)")]
        public string stepConditionId = "";
        
        [Tooltip("Índice del step de la quest que corresponde a este item (-1 = auto-detectar por conditionId)")]
        public int stepIndex = -1;
        
        [Tooltip("Si está activado, consume el ítem del inventario al completar la quest")]
        public bool consumeOnComplete = true;
    }
}

