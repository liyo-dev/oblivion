using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana de debug para monitorizar en tiempo real todos los eventos de DefaultNarrativeSignals.
/// Abre desde Window > Narrativa > Signal Monitor.
/// </summary>
public class NarrativeSignalsDebugWindow : EditorWindow
{
    [MenuItem("Window/Narrativa/Signal Monitor")]
    static void Open() => GetWindow<NarrativeSignalsDebugWindow>("Signal Monitor");

    // ── Colores por estado ────────────────────────────────────────
    static readonly Color ColFired    = new Color(0.30f, 0.85f, 0.35f);
    static readonly Color ColQueued   = new Color(0.95f, 0.75f, 0.10f);
    static readonly Color ColConsumed = new Color(0.35f, 0.65f, 1.00f);
    static readonly Color ColReset    = new Color(0.90f, 0.40f, 0.40f);
    static readonly Color ColRowOdd   = new Color(0.22f, 0.22f, 0.22f, 0.5f);
    static readonly Color ColRowEven  = new Color(0.18f, 0.18f, 0.18f, 0.5f);

    Vector2 _scrollHistory;
    Vector2 _scrollState;
    string  _filter      = "";
    bool    _autoScroll  = true;
    bool    _showState   = true;
    bool    _paused      = false;
    int     _selectedIdx = -1;

    // ── Lifecycle ─────────────────────────────────────────────────

    void OnEnable()
    {
        DefaultNarrativeSignals.HistoryChanged += OnHistoryChanged;
        EditorApplication.update += OnEditorUpdate;
        titleContent = new GUIContent("Signal Monitor");
    }

    void OnDisable()
    {
        DefaultNarrativeSignals.HistoryChanged -= OnHistoryChanged;
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnHistoryChanged()
    {
        if (!_paused) Repaint();
    }

    void OnEditorUpdate()
    {
        if (EditorApplication.isPlaying && !_paused)
            Repaint();
    }

    // ── GUI ───────────────────────────────────────────────────────

    void OnGUI()
    {
        DrawToolbar();

        float remainingH  = position.height - EditorGUIUtility.singleLineHeight - 6f;
        float callerH     = 38f;
        float stateH      = _showState ? Mathf.Min(160f, remainingH * 0.30f) : 0f;
        float historyH    = remainingH - stateH - callerH - 6f;

        DrawHistory(historyH);
        DrawCallerBar();

        if (_showState)
        {
            GUILayout.Space(2f);
            DrawCurrentState(stateH);
        }
    }

    // ── Toolbar ───────────────────────────────────────────────────

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Filtro:", GUILayout.Width(38));
        _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(150));

        GUILayout.Space(4f);
        _autoScroll = GUILayout.Toggle(_autoScroll, "Auto-scroll", EditorStyles.toolbarButton, GUILayout.Width(80));
        _showState  = GUILayout.Toggle(_showState,  "Estado",      EditorStyles.toolbarButton, GUILayout.Width(56));

        bool waspaused = _paused;
        _paused = GUILayout.Toggle(_paused, _paused ? "Reanudar" : "Pausar", EditorStyles.toolbarButton, GUILayout.Width(70));
        if (waspaused && !_paused) Repaint();

        GUILayout.FlexibleSpace();

        bool playing = EditorApplication.isPlaying;
        var prevColor = GUI.color;
        GUI.color = playing ? ColFired : Color.gray;
        GUILayout.Label(playing ? "  Grabando" : "  Parado", EditorStyles.toolbarButton, GUILayout.Width(74));
        GUI.color = prevColor;

        if (GUILayout.Button("Limpiar", EditorStyles.toolbarButton, GUILayout.Width(56)))
            DefaultNarrativeSignals.ClearHistory();

        EditorGUILayout.EndHorizontal();
    }

    // ── Historial ─────────────────────────────────────────────────

    void DrawHistory(float height)
    {
        var history = DefaultNarrativeSignals.History;
        bool hasFilter = !string.IsNullOrEmpty(_filter);

        _scrollHistory = EditorGUILayout.BeginScrollView(_scrollHistory,
            GUILayout.Height(height));

        bool isOdd = false;
        int visIdx = 0;
        for (int i = 0; i < history.Count; i++)
        {
            var rec = history[i];

            if (hasFilter && rec.key.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            bool selected = (_selectedIdx == i);
            DrawRow(i, rec, isOdd, selected);
            isOdd = !isOdd;
            visIdx++;
        }

        if (_autoScroll && !_paused && history.Count > 0)
            _scrollHistory = new Vector2(0f, float.MaxValue);

        EditorGUILayout.EndScrollView();
    }

    void DrawCallerBar()
    {
        var history = DefaultNarrativeSignals.History;
        string callerText = "Haz clic en una fila para ver quién disparó el evento.";

        if (_selectedIdx >= 0 && _selectedIdx < history.Count)
        {
            var rec = history[_selectedIdx];
            callerText = string.IsNullOrEmpty(rec.caller)
                ? $"[{rec.key}]  (caller no disponible)"
                : $"[{rec.key}]  {rec.caller}";
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(36f));
        EditorGUILayout.LabelField("Origen del evento:", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(callerText, EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    static GUIStyle _rowStyle;
    static readonly Color ColSelected = new Color(0.24f, 0.48f, 0.90f, 0.4f);

    void DrawRow(int index, in DefaultNarrativeSignals.SignalRecord rec, bool odd, bool selected)
    {
        if (_rowStyle == null)
        {
            _rowStyle = new GUIStyle(EditorStyles.label)
            {
                richText  = false,
                fontSize  = 11,
                padding   = new RectOffset(4, 4, 1, 1),
                fontStyle = FontStyle.Normal,
            };
            _rowStyle.normal.textColor = Color.white;
        }

        Rect row = EditorGUILayout.BeginHorizontal(GUILayout.Height(16f));

        if (Event.current.type == EventType.Repaint)
        {
            Color bg = selected ? ColSelected : (odd ? ColRowOdd : ColRowEven);
            EditorGUI.DrawRect(row, bg);
        }

        // Click para seleccionar fila
        if (Event.current.type == EventType.MouseDown && row.Contains(Event.current.mousePosition))
        {
            _selectedIdx = (selected && _selectedIdx == index) ? -1 : index;
            Event.current.Use();
            Repaint();
        }

        Color statusColor = rec.status switch
        {
            DefaultNarrativeSignals.SignalStatus.Fired    => ColFired,
            DefaultNarrativeSignals.SignalStatus.Queued   => ColQueued,
            DefaultNarrativeSignals.SignalStatus.Consumed => ColConsumed,
            DefaultNarrativeSignals.SignalStatus.Reset    => ColReset,
            _                                             => Color.white
        };

        string statusLabel = rec.status switch
        {
            DefaultNarrativeSignals.SignalStatus.Fired    => "FIRED   ",
            DefaultNarrativeSignals.SignalStatus.Queued   => "QUEUED  ",
            DefaultNarrativeSignals.SignalStatus.Consumed => "CONSUMED",
            DefaultNarrativeSignals.SignalStatus.Reset    => "RESET   ",
            _                                             => "?       "
        };

        var prevColor = GUI.color;

        GUI.color = new Color(0.7f, 0.7f, 0.7f);
        GUILayout.Label($"t={rec.time,6:F2}", _rowStyle, GUILayout.Width(60));

        GUI.color = statusColor;
        GUILayout.Label(statusLabel, _rowStyle, GUILayout.Width(76));

        GUI.color = rec.key.StartsWith("__") ? new Color(0.6f, 0.6f, 0.6f) : Color.white;
        GUILayout.Label(rec.key, _rowStyle, GUILayout.Width(200));

        // Caller resumido (solo ClassName.Method, sin ruta)
        GUI.color = new Color(0.85f, 0.85f, 0.45f);
        string callerShort = ShortCaller(rec.caller);
        GUILayout.Label(callerShort, _rowStyle, GUILayout.Width(220));

        // Detalle
        GUI.color = new Color(0.65f, 0.65f, 0.65f);
        GUILayout.Label(rec.detail, _rowStyle);

        GUI.color = prevColor;
        EditorGUILayout.EndHorizontal();
    }

    static string ShortCaller(string caller)
    {
        if (string.IsNullOrEmpty(caller)) return "";
        // "NPCInteractiveNarrativeExecutor.SendNarrativeEvent()  [File.cs:1455]"
        // → mostrar solo hasta el primer "[" para la columna corta
        int bracket = caller.IndexOf('[');
        return bracket > 0 ? caller.Substring(0, bracket).TrimEnd() : caller;
    }

    // ── Estado actual ─────────────────────────────────────────────

    void DrawCurrentState(float height)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(height));

        EditorGUILayout.LabelField("Estado actual de DefaultNarrativeSignals", EditorStyles.boldLabel);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Disponible solo en Play Mode.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        var inst = DefaultNarrativeSignals.Instance;
        if (inst == null)
        {
            EditorGUILayout.HelpBox("DefaultNarrativeSignals.Instance es null.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        _scrollState = EditorGUILayout.BeginScrollView(_scrollState);

        var pending = inst.CurrentPending;
        var raised  = inst.CurrentRaised;
        var subs    = inst.CurrentSubscribers;

        EditorGUILayout.BeginHorizontal();

        // Columna: pending
        EditorGUILayout.BeginVertical(GUILayout.Width(180));
        var prevColor = GUI.color;
        GUI.color = ColQueued;
        EditorGUILayout.LabelField($"_pending ({pending.Count})", EditorStyles.miniBoldLabel);
        GUI.color = prevColor;
        foreach (var k in pending) EditorGUILayout.LabelField("  " + k, EditorStyles.miniLabel);
        if (pending.Count == 0) EditorGUILayout.LabelField("  (vacio)", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        // Columna: raised
        EditorGUILayout.BeginVertical(GUILayout.Width(180));
        GUI.color = ColConsumed;
        EditorGUILayout.LabelField($"_raised ({raised.Count})", EditorStyles.miniBoldLabel);
        GUI.color = prevColor;
        foreach (var k in raised) EditorGUILayout.LabelField("  " + k, EditorStyles.miniLabel);
        if (raised.Count == 0) EditorGUILayout.LabelField("  (vacio)", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        // Columna: suscriptores activos
        EditorGUILayout.BeginVertical();
        GUI.color = ColFired;
        EditorGUILayout.LabelField($"_custom suscriptores ({subs.Count})", EditorStyles.miniBoldLabel);
        GUI.color = prevColor;
        foreach (var kvp in subs)
        {
            int n = kvp.Value?.GetInvocationList()?.Length ?? 0;
            EditorGUILayout.LabelField($"  {kvp.Key}  ({n})", EditorStyles.miniLabel);
        }
        if (subs.Count == 0) EditorGUILayout.LabelField("  (ninguno)", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }
}
