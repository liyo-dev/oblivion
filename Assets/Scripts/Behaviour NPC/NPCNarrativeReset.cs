using UnityEngine;
using Game.NPC;

/// <summary>
/// Componente helper para resetear narrativas de NPCs.
/// Añadir a la escena START o a un GameManager.
/// </summary>
public class NPCNarrativeReset : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Resetear narrativas automáticamente al cargar esta escena (útil para testing)")]
    [SerializeField] private bool resetOnSceneLoad = false;
    
    [Header("Debug")]
    [Tooltip("Mostrar información de debug en consola")]
    [SerializeField] private bool showDebugInfo = true;
    
    private void Start()
    {
        if (resetOnSceneLoad)
        {
            ResetAllNarratives();
        }
        
        if (showDebugInfo)
        {
            Debug.Log(NPCNarrativeStateManager.GetDebugInfo());
        }
    }
    
    /// <summary>
    /// Resetea todas las narrativas de NPCs (llamar desde UI o código)
    /// </summary>
    [ContextMenu("Reset All Narratives")]
    public void ResetAllNarratives()
    {
        NPCNarrativeStateManager.ResetAllNPCs();
        Debug.Log("[NPCNarrativeReset] ✅ Todas las narrativas reseteadas");
    }
    
    /// <summary>
    /// Limpia todos los estados guardados (llamar al iniciar nueva partida)
    /// </summary>
    [ContextMenu("Clear All Saved States")]
    public void ClearAllSavedStates()
    {
        NPCNarrativeStateManager.ClearAllSavedStates();
        Debug.Log("[NPCNarrativeReset] ✅ Todos los estados guardados limpiados");
    }
    
    /// <summary>
    /// Resetea y limpia todo (equivalente a nueva partida fresca)
    /// </summary>
    [ContextMenu("Full Reset (New Game)")]
    public void FullReset()
    {
        ClearAllSavedStates();
        ResetAllNarratives();
        Debug.Log("[NPCNarrativeReset] ✅ RESET COMPLETO - Como nueva partida");
    }
    
    /// <summary>
    /// Muestra información de debug en consola
    /// </summary>
    [ContextMenu("Show Debug Info")]
    public void ShowDebugInfo()
    {
        Debug.Log(NPCNarrativeStateManager.GetDebugInfo());
    }
}

