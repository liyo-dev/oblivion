using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Registro estático de NPCs actualmente en combate.
/// Permite búsqueda rápida sin necesidad de buscar componentes.
/// </summary>
public static class ActiveCombatRegistry
{
    private static readonly HashSet<GameObject> _npcsInCombat = new HashSet<GameObject>();
    
    /// <summary>
    /// Se dispara cuando un NPC entra en combate (útil para que los compañeros reaccionen)
    /// </summary>
    public static event Action<GameObject> OnNPCEnteredCombat;
    
    /// <summary>
    /// Se dispara cuando un NPC sale del combate
    /// </summary>
    public static event Action<GameObject> OnNPCExitedCombat;
    
    /// <summary>
    /// Registra un NPC como "en combate"
    /// </summary>
    public static void RegisterNPC(GameObject npc)
    {
        if (npc == null) return;
        
        if (_npcsInCombat.Add(npc))
        {
            Debug.Log($"[ActiveCombatRegistry] ⚔️ NPC '{npc.name}' registrado en combate");
            OnNPCEnteredCombat?.Invoke(npc);
        }
    }
    
    /// <summary>
    /// Quita un NPC del registro de combate
    /// </summary>
    public static void UnregisterNPC(GameObject npc)
    {
        if (npc == null) return;
        
        if (_npcsInCombat.Remove(npc))
        {
            Debug.Log($"[ActiveCombatRegistry] 🏳️ NPC '{npc.name}' removido del combate");
            OnNPCExitedCombat?.Invoke(npc);
        }
    }
    
    /// <summary>
    /// Verifica si un NPC está actualmente en combate
    /// </summary>
    public static bool IsInCombat(GameObject npc)
    {
        return npc != null && _npcsInCombat.Contains(npc);
    }
    
    /// <summary>
    /// Obtiene el NPC en combate más cercano a una posición
    /// </summary>
    public static GameObject GetClosestCombatNPC(Vector3 position, float maxDistance = 50f)
    {
        Debug.Log($"[ActiveCombatRegistry] GetClosestCombatNPC: Buscando entre {_npcsInCombat.Count} NPCs");
        Debug.Log($"[ActiveCombatRegistry]   - Posición búsqueda: {position}");
        Debug.Log($"[ActiveCombatRegistry]   - Distancia máxima: {maxDistance}m");
        
        GameObject closest = null;
        float closestDist = maxDistance;
        int checkedCount = 0;
        int nullCount = 0;
        
        foreach (var npc in _npcsInCombat)
        {
            if (npc == null)
            {
                nullCount++;
                continue; // NPC destruido
            }
            
            checkedCount++;
            float dist = Vector3.Distance(npc.transform.position, position);
            
            Debug.Log($"[ActiveCombatRegistry]   - NPC '{npc.name}': distancia = {dist:F1}m");
            
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = npc;
                Debug.Log($"[ActiveCombatRegistry]     ✅ Este es el más cercano por ahora");
            }
        }
        
        if (nullCount > 0)
        {
            Debug.LogWarning($"[ActiveCombatRegistry] ⚠️ {nullCount} NPCs null en el registro (destruidos)");
        }
        
        Debug.Log($"[ActiveCombatRegistry] Resultado: {(closest != null ? $"'{closest.name}' a {closestDist:F1}m" : "NINGUNO")}");
        
        return closest;
    }
    
    /// <summary>
    /// Limpia NPCs destruidos del registro
    /// </summary>
    public static void CleanupDestroyedNPCs()
    {
        _npcsInCombat.RemoveWhere(npc => npc == null);
    }
    
    /// <summary>
    /// Limpia todo el registro (útil al cambiar de escena)
    /// </summary>
    public static void ClearAll()
    {
        Debug.Log($"[ActiveCombatRegistry] 🧹 Limpiando {_npcsInCombat.Count} NPCs del registro");
        _npcsInCombat.Clear();
    }
    
    /// <summary>
    /// Debug: Cantidad de NPCs en combate
    /// </summary>
    public static int Count => _npcsInCombat.Count;
}
