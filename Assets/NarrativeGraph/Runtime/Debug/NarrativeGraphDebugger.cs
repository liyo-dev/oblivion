using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Debugger para visualizar el estado de los grafos narrativos en runtime.
/// Añade este componente al mismo GameObject que el NarrativeGraphHub.
/// </summary>
[RequireComponent(typeof(NarrativeGraphHub))]
public class NarrativeGraphDebugger : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Mostrar el panel de debug en pantalla")]
    public bool showDebugPanel = true;
    
    [Tooltip("Tecla para mostrar/ocultar el panel")]
    public KeyCode toggleKey = KeyCode.F3;
    
    [Tooltip("Registrar historial de nodos visitados")]
    public bool trackHistory = true;
    
    [Tooltip("Máximo de entradas en el historial")]
    public int maxHistoryEntries = 50;
    
    [Header("Visual")]
    public Color panelColor = new Color(0, 0, 0, 0.8f);
    public Color textColor = Color.white;
    public Color activeColor = Color.green;
    public Color waitingColor = Color.yellow;
    
    private NarrativeGraphHub _hub;
    private Dictionary<string, List<string>> _history = new Dictionary<string, List<string>>();
    private Vector2 _scrollPosition;
    private bool _isVisible = true;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    
    void Awake()
    {
        _hub = GetComponent<NarrativeGraphHub>();
    }
    
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            _isVisible = !_isVisible;
        }
    }
    
    void OnGUI()
    {
        if (!showDebugPanel || !_isVisible) return;
        
        InitializeStyles();
        
        float panelWidth = 400;
        float panelHeight = Screen.height * 0.6f;
        Rect panelRect = new Rect(10, 10, panelWidth, panelHeight);
        
        GUI.Box(panelRect, "", _boxStyle);
        
        GUILayout.BeginArea(new Rect(panelRect.x + 10, panelRect.y + 10, panelRect.width - 20, panelRect.height - 20));
        
        GUILayout.Label("NARRATIVE GRAPH DEBUGGER", _headerStyle);
        GUILayout.Label($"[{toggleKey}] para mostrar/ocultar", _labelStyle);
        GUILayout.Space(10);
        
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        
        DrawGraphStates();
        
        if (trackHistory)
        {
            GUILayout.Space(10);
            DrawHistory();
        }
        
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
    
    void DrawGraphStates()
    {
        if (_hub == null) return;
        
        var runners = _hub.GetAllRunners();
        if (runners.Count == 0)
        {
            GUILayout.Label("No hay grafos registrados", _labelStyle);
            return;
        }
        
        foreach (var runner in runners)
        {
            if (runner == null) continue;
            
            var graphName = runner.gameObject.name;
            var currentNodeGuid = runner.Blackboard.Get<string>("__currentNodeGuid", null);
            var currentNode = string.IsNullOrEmpty(currentNodeGuid) 
                ? null 
                : runner.graph?.FindNode(currentNodeGuid);
            
            GUILayout.BeginVertical(GUI.skin.box);
            
            // Header del grafo
            var headerColor = currentNode != null ? activeColor : Color.gray;
            var oldColor = GUI.color;
            GUI.color = headerColor;
            GUILayout.Label($"▶ {graphName}", _headerStyle);
            GUI.color = oldColor;
            
            // Estado actual
            if (currentNode != null)
            {
                var nodeType = currentNode.GetType().Name;
                GUILayout.Label($"  Nodo: {nodeType}", _labelStyle);
                GUILayout.Label($"  GUID: {currentNodeGuid?.Substring(0, 8)}...", _labelStyle);
                
                // Información específica del nodo
                DrawNodeSpecificInfo(currentNode, runner);
            }
            else
            {
                GUILayout.Label("  Estado: No iniciado", _labelStyle);
            }
            
            // Blackboard info
            var blackboardEntries = runner.Blackboard.ExportToSerializable();
            if (blackboardEntries.Count > 0)
            {
                GUILayout.Label($"  Blackboard: {blackboardEntries.Count} entradas", _labelStyle);
                
                // Mostrar algunas entradas importantes
                foreach (var entry in blackboardEntries.Take(5))
                {
                    if (entry.key.StartsWith("__")) continue; // Saltar claves internas
                    GUILayout.Label($"    • {entry.key} = {entry.value}", _labelStyle);
                }
            }
            
            GUILayout.EndVertical();
            GUILayout.Space(5);
        }
    }
    
    void DrawNodeSpecificInfo(NarrativeNode node, NarrativeRunner runner)
    {
        if (node is WaitQuestCompleteNode waitQuest)
        {
            GUI.color = waitingColor;
            GUILayout.Label($"  ⏳ Esperando quest: {waitQuest.questId}", _labelStyle);
            GUI.color = Color.white;
        }
        else if (node is WaitCustomEventNode waitEvent)
        {
            var eventReceived = runner.Blackboard.Get<bool>($"__event_{waitEvent.eventKey}_received", false);
            GUI.color = eventReceived ? activeColor : waitingColor;
            GUILayout.Label($"  ⏳ Esperando evento: {waitEvent.eventKey}", _labelStyle);
            GUI.color = Color.white;
        }
        else if (node is StartQuestNode startQuest)
        {
            var questStarted = runner.Blackboard.Get<bool>($"__quest_{startQuest.questId}_started", false);
            GUI.color = questStarted ? activeColor : waitingColor;
            GUILayout.Label($"  🎯 Quest: {startQuest.questId}", _labelStyle);
            GUI.color = Color.white;
        }
    }
    
    void DrawHistory()
    {
        GUILayout.Label("HISTORIAL (últimos nodos visitados)", _headerStyle);
        
        foreach (var kvp in _history)
        {
            if (kvp.Value.Count == 0) continue;
            
            GUILayout.Label($"• {kvp.Key}:", _labelStyle);
            foreach (var entry in kvp.Value.Take(10))
            {
                GUILayout.Label($"  - {entry}", _labelStyle);
            }
        }
    }
    
    void InitializeStyles()
    {
        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTex(2, 2, panelColor);
        }
        
        if (_labelStyle == null)
        {
            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.normal.textColor = textColor;
            _labelStyle.fontSize = 12;
        }
        
        if (_headerStyle == null)
        {
            _headerStyle = new GUIStyle(GUI.skin.label);
            _headerStyle.normal.textColor = activeColor;
            _headerStyle.fontSize = 14;
            _headerStyle.fontStyle = FontStyle.Bold;
        }
    }
    
    Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    
    /// <summary>
    /// Registra un nodo visitado en el historial (llamar desde el NarrativeRunner).
    /// </summary>
    public void LogNodeVisit(string graphLabel, string nodeType, string nodeGuid)
    {
        if (!trackHistory) return;
        
        if (!_history.ContainsKey(graphLabel))
        {
            _history[graphLabel] = new List<string>();
        }
        
        var entry = $"{System.DateTime.Now:HH:mm:ss} - {nodeType}";
        _history[graphLabel].Insert(0, entry);
        
        // Limitar el tamaño del historial
        if (_history[graphLabel].Count > maxHistoryEntries)
        {
            _history[graphLabel].RemoveAt(_history[graphLabel].Count - 1);
        }
    }
}
