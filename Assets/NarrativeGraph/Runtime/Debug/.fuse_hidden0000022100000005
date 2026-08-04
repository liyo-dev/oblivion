using UnityEngine;

/// <summary>
/// Herramienta de debug para emitir eventos narrativos manualmente.
/// Útil para desbloquear el grafo cuando está esperando eventos que no se disparan.
/// </summary>
public class NarrativeEventDebugger : MonoBehaviour
{
    [Header("Emitir Evento Custom")]
    [Tooltip("Nombre del evento custom a emitir (ej: LETTER_START, EXIT_FROM_WOODS_ESTELA)")]
    public string eventKey = "";
    
    [Header("Eventos Comunes")]
    [SerializeField] private bool emitLETTER_START = false;
    [SerializeField] private bool emitADD_CLOAK = false;
    [SerializeField] private bool emitERIKA_FIGHT = false;
    [SerializeField] private bool emitERIKA_BATTLE_WON = false;
    [SerializeField] private bool emitESTELA_FOUND = false;
    [SerializeField] private bool emitEXIT_FROM_WOODS_ESTELA = false;
    
    [Header("Estado")]
    [SerializeField] private bool showPendingEvents = false;
    
    private DefaultNarrativeSignals _signals;

    void Update()
    {
        if (_signals == null)
        {
            _signals = FindFirstObjectByType<DefaultNarrativeSignals>();
        }
        
        // Procesar eventos comunes
        if (emitLETTER_START)
        {
            emitLETTER_START = false;
            EmitEvent("LETTER_START");
        }
        
        if (emitADD_CLOAK)
        {
            emitADD_CLOAK = false;
            EmitEvent("ADD_CLOAK");
        }
        
        if (emitERIKA_FIGHT)
        {
            emitERIKA_FIGHT = false;
            EmitEvent("ERIKA_FIGHT");
        }
        
        if (emitERIKA_BATTLE_WON)
        {
            emitERIKA_BATTLE_WON = false;
            EmitEvent("ERIKA_BATTLE_WON");
        }
        
        if (emitESTELA_FOUND)
        {
            emitESTELA_FOUND = false;
            EmitEvent("ESTELA_FOUND");
        }
        
        if (emitEXIT_FROM_WOODS_ESTELA)
        {
            emitEXIT_FROM_WOODS_ESTELA = false;
            EmitEvent("EXIT_FROM_WOODS_ESTELA");
        }
    }

    /// <summary>
    /// Emite un evento custom al sistema narrativo.
    /// </summary>
    [ContextMenu("Emitir Evento Custom")]
    public void EmitCustomEvent()
    {
        if (string.IsNullOrWhiteSpace(eventKey))
        {
            Debug.LogWarning("[NarrativeEventDebugger] ⚠️ eventKey está vacío. Escribe el nombre del evento a emitir.");
            return;
        }
        
        EmitEvent(eventKey);
    }
    
    private void EmitEvent(string key)
    {
        if (_signals == null)
        {
            _signals = FindFirstObjectByType<DefaultNarrativeSignals>();
        }
        
        if (_signals == null)
        {
            Debug.LogError("[NarrativeEventDebugger] ❌ No se encontró DefaultNarrativeSignals en la escena.");
            return;
        }
        
        Debug.Log($"[NarrativeEventDebugger] 📤 Emitiendo evento: '{key}'");
        _signals.RaiseCustom(key, name);
    }

    /// <summary>
    /// Muestra los eventos pendientes (para debug).
    /// </summary>
    [ContextMenu("Mostrar Eventos Pendientes")]
    public void ShowPendingEvents()
    {
        if (_signals == null)
        {
            _signals = FindFirstObjectByType<DefaultNarrativeSignals>();
        }
        
        if (_signals == null)
        {
            Debug.LogWarning("[NarrativeEventDebugger] ⚠️ DefaultNarrativeSignals no encontrado.");
            return;
        }
        
        // Usar reflexión para acceder a _pending (es privado)
        var field = typeof(DefaultNarrativeSignals).GetField("_pending", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            var pending = field.GetValue(_signals) as System.Collections.Generic.HashSet<string>;
            if (pending != null && pending.Count > 0)
            {
                Debug.Log($"[NarrativeEventDebugger] 📋 Eventos pendientes ({pending.Count}): {string.Join(", ", pending)}");
            }
            else
            {
                Debug.Log("[NarrativeEventDebugger] ✅ No hay eventos pendientes.");
            }
        }
        else
        {
            Debug.LogWarning("[NarrativeEventDebugger] ⚠️ No se pudo acceder a _pending.");
        }
    }
}
