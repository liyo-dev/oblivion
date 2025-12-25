﻿using UnityEngine;
using Game.NPC.Modules;

namespace Game.NPC
{
    /// <summary>
    /// Manager que resetea el estado de todos los NPCs con narrativas interactivas.
    /// Llamar NPCNarrativeStateManager.ResetAllNPCs() al iniciar una nueva partida.
    /// </summary>
    public static class NPCNarrativeStateManager
    {
        /// <summary>
        /// Resetea el estado de TODOS los NPCs con narrativas interactivas en la escena.
        /// Llamar este método al iniciar una nueva partida.
        /// </summary>
        public static void ResetAllNPCs()
        {
            Debug.Log("[NPCNarrativeStateManager] 🔄 Iniciando reset de todos los NPCs...");
            
            var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
            
            Debug.Log($"[NPCNarrativeStateManager] 📋 Encontrados {allExecutors.Count} executors en el registro");
            
            int resetCount = 0;
            int failedCount = 0;
            
            foreach (var executor in allExecutors)
            {
                if (executor != null)
                {
                    try
                    {
                        Debug.Log($"[NPCNarrativeStateManager] 🔄 Reseteando: {executor.name}");
                        executor.ResetState();
                        resetCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[NPCNarrativeStateManager] ❌ Error al resetear {executor.name}: {ex.Message}");
                        failedCount++;
                    }
                }
                else
                {
                    Debug.LogWarning("[NPCNarrativeStateManager] ⚠️ Executor null en la lista");
                    failedCount++;
                }
            }
            
            Debug.Log($"[NPCNarrativeStateManager] ✅ Reset completado: {resetCount} NPCs reseteados, {failedCount} fallos");
        }
        
        /// <summary>
        /// Resetea el estado de un NPC específico por nombre
        /// </summary>
        public static bool ResetNPC(string npcName)
        {
            var executor = NPCInteractiveNarrativeRegistry.GetByName(npcName);
            
            if (executor != null)
            {
                executor.ResetState();
                Debug.Log($"[NPCNarrativeStateManager] 🔄 NPC '{npcName}' reseteado");
                return true;
            }
            
            Debug.LogWarning($"[NPCNarrativeStateManager] ⚠️ No se encontró NPC con nombre '{npcName}'");
            return false;
        }
        
        /// <summary>
        /// Limpia todos los datos de persistencia de narrativas guardados
        /// </summary>
        public static void ClearAllSavedStates()
        {
            Debug.Log("[NPCNarrativeStateManager] 🗑️ Iniciando limpieza de estados guardados...");
            
            // Buscar todas las claves que empiezan con "NarrativeState_"
            int clearedCount = 0;
            
            // Obtener todos los executors desde el registro
            var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
            
            // Obtener el preset activo del GameBootService
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            
            foreach (var executor in allExecutors)
            {
                if (executor != null)
                {
                    var config = executor.GetConfiguration();
                    if (config != null && config.persistState && !string.IsNullOrEmpty(config.persistenceId))
                    {
                        // Limpiar PlayerPrefs (sistema antiguo)
                        string key = $"NarrativeState_{config.persistenceId}";
                        if (PlayerPrefs.HasKey(key))
                        {
                            PlayerPrefs.DeleteKey(key);
                            clearedCount++;
                        }
                        
                        // Limpiar narrativas condicionales en PlayerPrefs
                        if (config.conditionalNarratives != null)
                        {
                            for (int i = 0; i < config.conditionalNarratives.Length; i++)
                            {
                                string narrativeKey = $"NarrativeState_{config.persistenceId}_Conditional_{i}";
                                if (PlayerPrefs.HasKey(narrativeKey))
                                {
                                    PlayerPrefs.DeleteKey(narrativeKey);
                                }
                            }
                        }
                        
                        // CRÍTICO: Limpiar del GameBootService.Profile (donde realmente se lee)
                        if (preset != null && preset.completedInteractiveNarratives != null)
                        {
                            bool removed = preset.completedInteractiveNarratives.Remove(config.persistenceId);
                            if (removed)
                            {
                                Debug.Log($"[NPCNarrativeStateManager] ✅ Removido '{config.persistenceId}' de GameBootService.Profile");
                            }
                        }
                    }
                }
            }
            
            PlayerPrefs.Save();
            Debug.Log($"[NPCNarrativeStateManager] ✅ Limpieza completada: {clearedCount} estados de PlayerPrefs limpiados");
        }
        
        /// <summary>
        /// Obtiene información de debug sobre todos los NPCs con narrativas
        /// </summary>
        public static string GetDebugInfo()
        {
            var allExecutors = NPCInteractiveNarrativeRegistry.GetAll();
            
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== NPCs con Narrativas Interactivas ({allExecutors.Count}) ===");
            
            foreach (var executor in allExecutors)
            {
                if (executor != null)
                {
                    var config = executor.GetConfiguration();
                    if (config != null)
                    {
                        sb.AppendLine($"\n🎭 {executor.name}");
                        sb.AppendLine($"   SingleUse: {config.singleUse}");
                        sb.AppendLine($"   Persist: {config.persistState}");
                        sb.AppendLine($"   ID: {config.persistenceId}");
                        sb.AppendLine($"   Narrativas: {config.conditionalNarratives?.Length ?? 0}");
                        
                        if (config.conditionalNarratives != null)
                        {
                            for (int i = 0; i < config.conditionalNarratives.Length; i++)
                            {
                                var narrative = config.conditionalNarratives[i];
                                if (narrative != null)
                                {
                                    sb.AppendLine($"      {i}. {narrative.description} - Ejecutada: {narrative.HasBeenExecuted}");
                                }
                            }
                        }
                    }
                }
            }
            
            return sb.ToString();
        }
    }
}

