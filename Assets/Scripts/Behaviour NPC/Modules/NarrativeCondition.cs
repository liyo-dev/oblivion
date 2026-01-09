using System;
using UnityEngine;

namespace Game.NPC.Modules
{
    /// <summary>
    /// Tipos de condiciones para narrativas
    /// </summary>
    public enum NarrativeConditionType
    {
        None,              // Sin condición (siempre se ejecuta)
        QuestNotStarted,   // La quest no ha sido iniciada
        QuestStarted,      // La quest ha sido iniciada
        QuestCompleted,    // La quest ha sido completada
        QuestActive,       // La quest está activa (iniciada pero no completada)
        Custom             // Condición custom (delegate/función)
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
                
                case NarrativeConditionType.Custom:
                    // Para custom, siempre retorna false por defecto
                    // El código que usa esto puede override el comportamiento
                    if (debugMode)
                        Debug.LogWarning("[NarrativeCondition] Custom condition not implemented, returning false");
                    result = false;
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
                
                case NarrativeConditionType.Custom:
                    return "Condición custom";
                
                default:
                    return "Desconocida";
            }
        }
    }
}

