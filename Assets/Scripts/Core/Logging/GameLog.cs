using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Sistema de filtrado de logs por tag. Intercepta todos los Debug.Log que usan
/// el formato "[Tag] mensaje" y permite activarlos/desactivarlos individualmente
/// desde la ventana El Sendero > GameLog > Ventana de Logs.
///
/// Uso para código nuevo:
///   GameLog.Log("Audio", $"Reproduciendo {clip.name}");
///   GameLog.Warn("NPC", "Ruta no encontrada");
///   GameLog.Error("Quest", "QuestId inválido");
/// </summary>
public static class GameLog
{
    private static GameLogHandler _handler;

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    static void EditorInstall() => Install();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void RuntimeInstall() => Install();

    static void Install()
    {
        var current = Debug.unityLogger.logHandler;
        // Protegemos contra escenarios extraños donde el logHandler pueda ser null
        // o donde otro GameLogHandler ya esté instalado.
        if (current is GameLogHandler existing)
        {
            _handler = existing;
            return;
        }

        try
        {
            _handler = new GameLogHandler(current);
            Debug.unityLogger.logHandler = _handler;
            _handler.LoadFromPrefs();
        }
        catch (Exception)
        {
            // Si por alguna razón no podemos instalar el handler, no bloqueamos el editor/play.
            // Dejamos el handler en null y continuamos.
            _handler = null;
        }
    }

    // ── API para código nuevo ─────────────────────────────────────────────────

    public static void Log(string tag, string message, UnityEngine.Object ctx = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[{tag}] {message}", ctx);
#endif
    }

    public static void Warn(string tag, string message, UnityEngine.Object ctx = null)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[{tag}] {message}", ctx);
#endif
    }

    public static void Error(string tag, string message, UnityEngine.Object ctx = null)
    {
        Debug.LogError($"[{tag}] {message}", ctx);
    }

    // ── Control ───────────────────────────────────────────────────────────────

    public static bool GlobalEnabled
    {
        get => _handler?.GlobalEnabled ?? true;
        set
        {
            if (_handler == null) return;
            _handler.GlobalEnabled = value;
            _handler.SaveToPrefs();
        }
    }

    public static IReadOnlyDictionary<string, bool> Tags => _handler?.Tags
        ?? (IReadOnlyDictionary<string, bool>)new Dictionary<string, bool>();

    public static bool DefaultNewTagEnabled => _handler?.DefaultNewTagEnabled ?? true;

    public static void SetTag(string tag, bool enabled)
    {
        _handler?.SetTag(tag, enabled);
        _handler?.SaveToPrefs();
    }

    public static void EnableAll()
    {
        _handler?.SetAll(true);
        _handler?.SaveToPrefs();
    }

    public static void DisableAll()
    {
        _handler?.SetAll(false);
        _handler?.SaveToPrefs();
    }

    public static void ResetAll()
    {
        _handler?.Reset();
        _handler?.SaveToPrefs();
    }

    /// <summary>Pre-registra tags desde la ventana de editor (sin esperar a que aparezcan en logs).</summary>
    public static void RegisterTag(string tag)
    {
        _handler?.RegisterTag(tag);
    }
}

// ── Handler interno ───────────────────────────────────────────────────────────

public sealed class GameLogHandler : ILogHandler
{
    private const string PREFS_GLOBAL     = "GameLog.GlobalEnabled";
    private const string PREFS_TAG_PREFIX = "GameLog.Tag.";
    private const string PREFS_KNOWN_TAGS = "GameLog.KnownTags";

    private readonly ILogHandler _upstream;
    private readonly Dictionary<string, bool> _tags = new();
    private bool _defaultNewTagEnabled = true;

    public bool GlobalEnabled { get; set; } = true;
    public bool DefaultNewTagEnabled => _defaultNewTagEnabled;
    public IReadOnlyDictionary<string, bool> Tags => _tags;

    public GameLogHandler(ILogHandler upstream) => _upstream = upstream;

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        if (logType == LogType.Error || logType == LogType.Exception || logType == LogType.Assert)
        {
            _upstream.LogFormat(logType, context, format, args);
            return;
        }

        if (!GlobalEnabled) return;

        string message = ExtractMessage(format, args);
        string tag     = ExtractTag(message);

        if (tag != null)
        {
            if (!_tags.ContainsKey(tag)) _tags[tag] = _defaultNewTagEnabled;
            if (!_tags[tag]) return;
        }

        _upstream.LogFormat(logType, context, format, args);
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        try
        {
#if UNITY_EDITOR
            if (IsHarmlessEditorException(exception)) return;
#endif
            _upstream?.LogException(exception, context);
        }
        catch
        {
            // Intencionalmente silencioso: evita recursión con el propio logHandler.
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Excepciones internas del editor que no indican ningún error del juego.
    /// Se filtran para no ensuciar la consola. Criterios acotados a tipos/stacks
    /// de UnityEditor para no ocultar MissingReference reales del gameplay.
    /// </summary>
    static bool IsHarmlessEditorException(Exception ex)
    {
        if (ex == null) return false;

        // UnityEditor.Graphs lanza NullReferenceException propias al refrescar el
        // Animator window en Play. Bug interno del editor.
        if (ex.StackTrace?.Contains("UnityEditor.Graphs") == true) return true;

        // El Inspector reconstruye sus editores sobre un objeto ya destruido
        // (entrar/salir de Play, recarga de escena, objetos generados en runtime).
        // Solo la lanza UnityEditor.Editor.CreateSerializedObject.
        if (ex.GetType().Name == "SerializedObjectNotCreatableException") return true;

        // Mismo caso, vía GameObjectInspector.OnEnable ("m_Targets doesn't exist anymore")
        if (ex is MissingReferenceException &&
            (ex.Message?.Contains("GameObjectInspector") == true ||
             ex.StackTrace?.Contains("UnityEditor.GameObjectInspector") == true))
            return true;

        return false;
    }
#endif

    public void SetTag(string tag, bool enabled)   => _tags[tag] = enabled;
    public void RegisterTag(string tag)            { if (!_tags.ContainsKey(tag)) _tags[tag] = true; }

    public void SetAll(bool enabled)
    {
        _defaultNewTagEnabled = enabled;
        var keys = new List<string>(_tags.Keys);
        foreach (var k in keys) _tags[k] = enabled;
    }

    public void Reset()
    {
        var keys = new List<string>(_tags.Keys);
        foreach (var k in keys) _tags[k] = true;
        GlobalEnabled = true;
    }

    // ── Persistencia (EditorPrefs en editor, PlayerPrefs en builds) ───────────

    public void LoadFromPrefs()
    {
#if UNITY_EDITOR
        GlobalEnabled        = EditorPrefs.GetBool(PREFS_GLOBAL, true);
        _defaultNewTagEnabled = EditorPrefs.GetBool("GameLog.DefaultNewTagEnabled", true);
        string known = EditorPrefs.GetString(PREFS_KNOWN_TAGS, "");
        if (string.IsNullOrEmpty(known)) return;
        foreach (var tag in known.Split(','))
        {
            if (!string.IsNullOrEmpty(tag))
                _tags[tag] = EditorPrefs.GetBool(PREFS_TAG_PREFIX + tag, _defaultNewTagEnabled);
        }
#else
        GlobalEnabled = PlayerPrefs.GetInt(PREFS_GLOBAL, 1) == 1;
#endif
    }

    public void SaveToPrefs()
    {
#if UNITY_EDITOR
        EditorPrefs.SetBool(PREFS_GLOBAL, GlobalEnabled);
        EditorPrefs.SetBool("GameLog.DefaultNewTagEnabled", _defaultNewTagEnabled);
        EditorPrefs.SetString(PREFS_KNOWN_TAGS, string.Join(",", _tags.Keys));
        foreach (var kv in _tags)
            EditorPrefs.SetBool(PREFS_TAG_PREFIX + kv.Key, kv.Value);
#else
        PlayerPrefs.SetInt(PREFS_GLOBAL, GlobalEnabled ? 1 : 0);
        PlayerPrefs.Save();
#endif
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static string ExtractMessage(string format, object[] args)
        => format == "{0}" && args?.Length > 0 ? args[0]?.ToString() ?? "" : format;

    static string ExtractTag(string msg)
    {
        if (string.IsNullOrEmpty(msg) || msg[0] != '[') return null;
        int end = msg.IndexOf(']', 1);
        return end > 1 ? msg.Substring(1, end - 1) : null;
    }
}
