using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Valida la configuración de los grafos narrativos para detectar errores comunes.
/// </summary>
public static class NarrativeGraphValidator
{
    public class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        
        public void LogResults(string graphName)
        {
            if (IsValid && Warnings.Count == 0)
            {
                Debug.Log($"[NarrativeGraphValidator] ✅ Grafo '{graphName}' es válido");
                return;
            }
            
            if (Errors.Count > 0)
            {
                Debug.LogError($"[NarrativeGraphValidator] ❌ Grafo '{graphName}' tiene {Errors.Count} error(es):");
                foreach (var error in Errors)
                {
                    Debug.LogError($"  • {error}");
                }
            }
            
            // Reducir ruido: solo mostrar warnings si hay errores o si se habilita explícitamente
            // Para evitar spam en la consola durante el desarrollo normal
            if (Warnings.Count > 0)
            {
                // Opcional: comentar o usar Log en lugar de LogWarning para reducir severidad visual
                Debug.Log($"[NarrativeGraphValidator] ℹ️ Grafo '{graphName}' tiene {Warnings.Count} advertencia(s):");
                foreach (var warning in Warnings)
                {
                    Debug.Log($"  • {warning}");
                }
            }
        }
    }
    
    /// <summary>
    /// Valida un grafo narrativo completo.
    /// </summary>
    public static ValidationResult ValidateGraph(NarrativeGraph graph)
    {
        var result = new ValidationResult();
        
        if (graph == null)
        {
            result.Errors.Add("El grafo es null");
            return result;
        }
        
        // Validar que tiene un StartNode
        if (string.IsNullOrEmpty(graph.startNodeGuid))
        {
            result.Errors.Add("No hay StartNode definido. Marca un nodo como 'Set as Start'");
        }
        else
        {
            var startNode = graph.FindNode(graph.startNodeGuid);
            if (startNode == null)
            {
                result.Errors.Add($"StartNode con GUID '{graph.startNodeGuid}' no existe");
            }
        }
        
        // Validar que no hay nodos huérfanos (sin conexiones)
        var orphanNodes = FindOrphanNodes(graph);
        if (orphanNodes.Count > 0)
        {
            result.Warnings.Add($"Hay {orphanNodes.Count} nodo(s) huérfano(s) sin conexiones: {string.Join(", ", orphanNodes.Select(n => n.GetType().Name))}");
        }
        
        // Validar que todos los nodos tienen GUIDs únicos
        var duplicateGuids = FindDuplicateGuids(graph);
        if (duplicateGuids.Count > 0)
        {
            result.Errors.Add($"Hay GUIDs duplicados: {string.Join(", ", duplicateGuids)}");
        }
        
        // Validar nodos específicos
        ValidateWaitQuestNodes(graph, result);
        ValidateWaitCustomEventNodes(graph, result);
        ValidateStartQuestNodes(graph, result);
        ValidateCompleteQuestStepsNodes(graph, result);
        ValidateSavePoints(graph, result);
        
        return result;
    }
    
    static List<NarrativeNode> FindOrphanNodes(NarrativeGraph graph)
    {
        var orphans = new List<NarrativeNode>();
        var connectedNodes = new HashSet<string>();
        
        // Marcar el StartNode como conectado
        if (!string.IsNullOrEmpty(graph.startNodeGuid))
        {
            connectedNodes.Add(graph.startNodeGuid);
            TraverseFromNode(graph, graph.startNodeGuid, connectedNodes);
        }
        
        // Los nodos no alcanzados son huérfanos
        foreach (var node in graph.nodes)
        {
            if (node == null)
            {
                UnityEngine.Debug.LogWarning($"[NarrativeGraphValidator] Nodo null encontrado en el grafo '{graph.name}', saltando validación de huérfanos para este nodo.");
                continue;
            }
            
            if (!connectedNodes.Contains(node.guid))
            {
                orphans.Add(node);
            }
        }
        
        return orphans;
    }
    
    static void TraverseFromNode(NarrativeGraph graph, string nodeGuid, HashSet<string> visited)
    {
        var node = graph.FindNode(nodeGuid);
        if (node == null || node.outputs == null) return;
        
        foreach (var outputGuid in node.outputs)
        {
            if (string.IsNullOrEmpty(outputGuid)) continue;
            
            if (!visited.Contains(outputGuid))
            {
                visited.Add(outputGuid);
                TraverseFromNode(graph, outputGuid, visited);
            }
        }
    }
    
    static List<string> FindDuplicateGuids(NarrativeGraph graph)
    {
        var guids = new Dictionary<string, int>();
        
        foreach (var node in graph.nodes)
        {
            if (node == null || string.IsNullOrEmpty(node.guid)) continue;
            
            if (!guids.ContainsKey(node.guid))
                guids[node.guid] = 0;
            
            guids[node.guid]++;
        }
        
        return guids.Where(kv => kv.Value > 1).Select(kv => kv.Key).ToList();
    }
    
    static void ValidateWaitQuestNodes(NarrativeGraph graph, ValidationResult result)
    {
        foreach (var node in graph.nodes.OfType<WaitQuestCompleteNode>())
        {
            if (string.IsNullOrEmpty(node.questId))
            {
                result.Warnings.Add($"WaitQuestCompleteNode tiene questId vacío");
            }
        }
    }
    
    static void ValidateWaitCustomEventNodes(NarrativeGraph graph, ValidationResult result)
    {
        var eventKeys = new HashSet<string>();
        
        foreach (var node in graph.nodes.OfType<WaitCustomEventNode>())
        {
            if (string.IsNullOrEmpty(node.eventKey))
            {
                result.Warnings.Add($"WaitCustomEventNode tiene eventKey vacío");
            }
            else
            {
                eventKeys.Add(node.eventKey);
            }
        }
        
        // Advertir si hay eventos que nunca se emiten
        // (esto es solo una advertencia porque los eventos pueden venir de otras partes del juego)
        if (eventKeys.Count > 0)
        {
            result.Warnings.Add($"Grafo espera {eventKeys.Count} evento(s) custom: {string.Join(", ", eventKeys)}. Verifica que estos eventos se emiten en algún lugar.");
        }
    }
    
    static void ValidateStartQuestNodes(NarrativeGraph graph, ValidationResult result)
    {
        var questIds = new HashSet<string>();
        
        foreach (var node in graph.nodes.OfType<StartQuestNode>())
        {
            if (string.IsNullOrEmpty(node.questId))
            {
                result.Warnings.Add($"StartQuestNode tiene questId vacío");
            }
            else
            {
                if (questIds.Contains(node.questId))
                {
                    result.Warnings.Add($"La quest '{node.questId}' se inicia múltiples veces en el grafo");
                }
                questIds.Add(node.questId);
            }
        }
    }
    
    static void ValidateCompleteQuestStepsNodes(NarrativeGraph graph, ValidationResult result)
    {
        foreach (var node in graph.nodes.OfType<CompleteQuestStepsNode>())
        {
            if (string.IsNullOrEmpty(node.questId))
            {
                result.Warnings.Add("CompleteQuestStepsNode tiene questId vacío");
                continue;
            }

            bool hasSteps = node.steps != null && node.steps.Count > 0;
            if (!hasSteps && !node.completeQuest)
            {
                result.Warnings.Add($"CompleteQuestStepsNode para '{node.questId}' no tiene pasos configurados ni completará la quest.");
            }

            if (hasSteps && node.steps.Any(step => step < 0))
            {
                result.Warnings.Add($"CompleteQuestStepsNode para '{node.questId}' contiene índices de paso negativos.");
            }
        }
    }

    static void ValidateSavePoints(NarrativeGraph graph, ValidationResult result)
    {
        int safeNodes = 0;
        int unsafeNodes = 0;
        
        foreach (var node in graph.nodes)
        {
            if (node == null) continue;
            
            var nodeType = node.GetType();
            var hasSavePoint = nodeType.GetCustomAttributes(typeof(SavePointAttribute), false).Length > 0;
            var hasUnsafe = nodeType.GetCustomAttributes(typeof(UnsafeForSaveAttribute), false).Length > 0;
            
            if (hasSavePoint) safeNodes++;
            if (hasUnsafe) unsafeNodes++;
        }
        
        if (safeNodes == 0 && graph.nodes.Count > 0)
        {
            result.Warnings.Add($"No hay nodos marcados como [SavePoint]. Considera marcar los nodos de espera como seguros para guardar.");
        }
        
        if (unsafeNodes > 0)
        {
            result.Warnings.Add($"Hay {unsafeNodes} nodo(s) marcados como [UnsafeForSave]. Ten cuidado al guardar durante su ejecución.");
        }
    }
}
