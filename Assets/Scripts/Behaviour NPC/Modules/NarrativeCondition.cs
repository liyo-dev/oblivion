﻿using System;
using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Tipos de condiciones para narrativas
    /// </summary>
    public enum NarrativeConditionType
    {
        None            = 0, // Sin condición (siempre se ejecuta)
        QuestNotStarted = 1, // La quest no ha sido iniciada
        QuestStarted    = 2, // La quest ha sido iniciada
        QuestCompleted  = 3, // La quest ha sido completada
        QuestActive        = 4, // La quest está activa (iniciada pero no completada)
        Custom             = 5, // Condición custom (delegate/función)
        QuestStepCompleted = 6  // Un step concreto de la quest está completado
    }
    
    /// <summary>
    /// Define una condición que debe cumplirse para ejecutar una narrativa
    /// </summary>
    [Serializable]
    public class NarrativeCondition
    {
        [Header("Condición")]
        [Tooltip("Tipo de condición a evaluar")]
        public NarrativeConditionType conditionType = NarrativeConditionType.None;
        
        [Header("Quest Condition")]
        [Tooltip("Quest a verificar (si conditionType requiere quest)")]
        public QuestData targetQuest;
        
        [Header("Quest Step Condition")]
        [Tooltip("conditionId del step a verificar (ej: 'hablar_con_eldran'). Usado cuando conditionType = QuestStepCompleted")]
        public string targetStepConditionId = "";

        [Header("Custom Event Condition")]
        [Tooltip("Clave del evento custom a escuchar (ej: EVT_MOUNTAIN). Usado cuando conditionType = Custom")]
        public string customEventKey = "";
        
        // Estado runtime: true si el evento custom fue recibido
        [NonSerialized]
        private bool _customEventReceived = false;
        
        [Header("Debug")]
        [Tooltip("Mostrar logs cuando se evalúa esta condición")]
        public bool debugMode = false;
        
        // Cache para QuestManager
        private static QuestManager _cachedQuestManager;
        private static int _questManagerCacheFrame = -1;
        
        /// <summary>
        /// Obtiene QuestManager con caché por frame
        /// </summary>
        private static QuestManager GetQuestManager()
        {
            int currentFrame = Time.frameCount;
            if (_questManagerCacheFrame != currentFrame)
            {
                _cachedQuestManager = QuestManager.Instance;
                _questManagerCacheFrame = currentFrame;
            }
            return _cachedQuestManager;
        }
        
        /// <summary>
        /// Evalúa si la condición se cumple
        /// </summary>
        public bool Evaluate()
        {
            // Early exit para el caso más común (sin condición)
            if (conditionType == NarrativeConditionType.None)
            {
                return true;
            }
            
            bool result;
            
            switch (conditionType)
            {
                case NarrativeConditionType.QuestNotStarted:
                    result = EvaluateQuestNotStarted();
                    break;
                
                case NarrativeConditionType.QuestStarted:
                    result = EvaluateQuestStarted();
                    break;
                
                case NarrativeConditionType.QuestCompleted:
                    result = EvaluateQuestCompleted();
                    break;
                
                case NarrativeConditionType.QuestActive:
                    result = EvaluateQuestActive();
                    break;
                
                case NarrativeConditionType.QuestStepCompleted:
                    result = EvaluateQuestStepCompleted();
                    break;

                case NarrativeConditionType.Custom:
                    // Para custom, verificamos si el evento fue recibido
                    result = _customEventReceived;
                    if (debugMode)
                        Debug.Log($"[NarrativeCondition] Custom('{customEventKey}') = {result} (eventReceived={_customEventReceived})");
                    break;
                
                default:
                    result = true;
                    break;
            }
            
            if (debugMode)
            {
                string questName = targetQuest != null ? targetQuest.questId : "N/A";
                Debug.Log($"[NarrativeCondition] Evaluate: Type={conditionType}, Quest={questName}, Result={result}");
            }
            
            return result;
        }
        
        private bool EvaluateQuestNotStarted()
        {
            if (targetQuest == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestNotStarted: targetQuest es null");
                return false;
            }
            
            var questManager = GetQuestManager();
            if (questManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestManager no disponible");
                return false;
            }
            
            var state = questManager.GetState(targetQuest.questId);
            bool result = state == QuestState.Inactive;
            
            if (debugMode)
                Debug.Log($"[NarrativeCondition] QuestNotStarted('{targetQuest.questId}') = {result} (state={state})");
            
            return result;
        }
        
        private bool EvaluateQuestStarted()
        {
            if (targetQuest == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestStarted: targetQuest es null");
                return false;
            }
            
            var questManager = GetQuestManager();
            if (questManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestManager no disponible");
                return false;
            }
            
            var state = questManager.GetState(targetQuest.questId);
            bool result = state == QuestState.Active || state == QuestState.Completed;
            
            if (debugMode)
                Debug.Log($"[NarrativeCondition] QuestStarted('{targetQuest.questId}') = {result} (state={state})");
            
            return result;
        }
        
        private bool EvaluateQuestCompleted()
        {
            if (targetQuest == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestCompleted: targetQuest es null");
                return false;
            }
            
            var questManager = GetQuestManager();
            if (questManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestManager no disponible");
                return false;
            }
            
            var state = questManager.GetState(targetQuest.questId);
            bool result = state == QuestState.Completed;
            
            if (debugMode)
                Debug.Log($"[NarrativeCondition] QuestCompleted('{targetQuest.questId}') = {result} (state={state})");
            
            return result;
        }
        
        private bool EvaluateQuestActive()
        {
            if (targetQuest == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestActive: targetQuest es null");
                return false;
            }
            
            var questManager = GetQuestManager();
            if (questManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestManager no disponible");
                return false;
            }
            
            var state = questManager.GetState(targetQuest.questId);
            bool result = state == QuestState.Active;
            
            if (debugMode)
                Debug.Log($"[NarrativeCondition] QuestActive('{targetQuest.questId}') = {result} (state={state})");
            
            return result;
        }
        
        private bool EvaluateQuestStepCompleted()
        {
            if (targetQuest == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestStepCompleted: targetQuest es null");
                return false;
            }

            if (string.IsNullOrEmpty(targetStepConditionId))
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestStepCompleted: targetStepConditionId está vacío");
                return false;
            }

            var questManager = GetQuestManager();
            if (questManager == null)
            {
                if (debugMode)
                    Debug.LogWarning("[NarrativeCondition] QuestManager no disponible");
                return false;
            }

            int idx = questManager.FindStepIndexByConditionId(targetQuest.questId, targetStepConditionId);
            if (idx < 0)
            {
                if (debugMode)
                    Debug.LogWarning($"[NarrativeCondition] QuestStepCompleted: step '{targetStepConditionId}' no encontrado en '{targetQuest.questId}'");
                return false;
            }

            bool result = questManager.IsStepCompleted(targetQuest.questId, idx);

            if (debugMode)
                Debug.Log($"[NarrativeCondition] QuestStepCompleted('{targetQuest.questId}', step='{targetStepConditionId}', idx={idx}) = {result}");

            return result;
        }

        /// <summary>
        /// Obtiene una descripción legible de la condición
        /// </summary>
        public string GetDescription()
        {
            switch (conditionType)
            {
                case NarrativeConditionType.None:
                    return "Siempre";
                
                case NarrativeConditionType.QuestNotStarted:
                    return targetQuest != null 
                        ? $"Quest '{targetQuest.questId}' no iniciada" 
                        : "Quest no iniciada (sin quest)";
                
                case NarrativeConditionType.QuestStarted:
                    return targetQuest != null 
                        ? $"Quest '{targetQuest.questId}' iniciada" 
                        : "Quest iniciada (sin quest)";
                
                case NarrativeConditionType.QuestCompleted:
                    return targetQuest != null 
                        ? $"Quest '{targetQuest.questId}' completada" 
                        : "Quest completada (sin quest)";
                
                case NarrativeConditionType.QuestActive:
                    return targetQuest != null 
                        ? $"Quest '{targetQuest.questId}' activa" 
                        : "Quest activa (sin quest)";
                
                case NarrativeConditionType.QuestStepCompleted:
                    if (targetQuest != null && !string.IsNullOrEmpty(targetStepConditionId))
                        return $"Step '{targetStepConditionId}' de '{targetQuest.questId}' completado";
                    return "Step completado (sin configurar)";

                case NarrativeConditionType.Custom:
                    return !string.IsNullOrEmpty(customEventKey)
                        ? $"Evento '{customEventKey}'"
                        : "Evento custom (sin clave)";
                
                default:
                    return "Desconocida";
            }
        }
        
        /// <summary>
        /// Marca que el evento custom fue recibido
        /// </summary>
        public void MarkCustomEventReceived()
        {
            _customEventReceived = true;
            if (debugMode)
                Debug.Log($"[NarrativeCondition] ✅ Evento custom '{customEventKey}' recibido");
        }
        
        /// <summary>
        /// Resetea el estado del evento custom
        /// </summary>
        public void ResetCustomEventState()
        {
            _customEventReceived = false;
        }
        
        /// <summary>
        /// Indica si esta condición requiere suscripción a eventos custom
        /// </summary>
        public bool RequiresCustomEventSubscription => 
            conditionType == NarrativeConditionType.Custom && !string.IsNullOrEmpty(customEventKey);
        
        /// <summary>
        /// Indica si el evento custom ya fue recibido
        /// </summary>
        public bool CustomEventReceived => _customEventReceived;
    }
}

