using System;
using System.Collections.Generic;
using UnityEngine;

// FIX INC-023: antes compartía -1000 con GameBootService (Core/GameBootService.cs). Con el mismo
// orden, Unity no garantiza cuál Awake() corre primero — si GameBootService ganaba la carrera,
// el primer teletransporte/spawn de la partida podía leer nombres/textos ANTES de que
// LocalizationManager hubiera cargado sus catálogos, cayendo al fallback (texto en inglés).
// Debe ejecutarse estrictamente antes que GameBootService.
[DefaultExecutionOrder(-2000)]
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif

    [SerializeField] private string defaultLocale = "es";
    private static readonly string[] RequiredCatalogs = { "prologue", "ui", "cinematics", "dialogues", "quests", "other" };
    [SerializeField] private string[] catalogs = { "prologue", "ui", "cinematics", "dialogues", "quests", "other" };

    private readonly Dictionary<string, string> _table = new Dictionary<string, string>(1024);
    private readonly Dictionary<string, SubtitleInfo> _subs = new Dictionary<string, SubtitleInfo>(64);

    public string CurrentLocale { get; private set; }
    public event Action OnLocaleChanged;

    [Serializable]
    public class SubtitleInfo
    {
        public string id;
        public float start;
        public float duration;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        catalogs = EnsureRequiredCatalogs(catalogs);

        PlayerSettings.EnsureLoaded();
        var locale = string.IsNullOrWhiteSpace(PlayerSettings.Language) ? defaultLocale : PlayerSettings.Language;
        LoadLocale(locale);
    }

    public void LoadLocale(string locale)
    {
        _table.Clear();
        _subs.Clear();
        CurrentLocale = locale;

        foreach (var cat in catalogs)
        {
            var path = $"Localization/{cat}_{locale}";
            var textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogWarning($"[Localization] Missing catalog: {path}. Falling back to default.");
                var fallback = Resources.Load<TextAsset>($"Localization/{cat}_{defaultLocale}");
                if (fallback != null) 
                {
                    MergeJsonIntoTables(fallback.text);
                    Debug.Log($"[LocalizationManager] ✓ Cargado catálogo fallback: {cat}_{defaultLocale}");
                }
                else
                {
                    Debug.LogError($"[Localization] Missing fallback catalog as well: {cat}_{defaultLocale}");
                }
            }
            else
            {
                MergeJsonIntoTables(textAsset.text);
            }
        }

        OnLocaleChanged?.Invoke();
        
        // Debug: Mostrar algunas claves cargadas
        int count = 0;
        foreach (var key in _table.Keys)
        {
            if (key.StartsWith("DLG_") || key.StartsWith("CHAR_"))
            {
                count++;
                if (count >= 5) break; // Mostrar solo las primeras 5
            }
        }
    }

    private void MergeJsonIntoTables(string json)
    {
        try
        {
            var data = JsonUtility.FromJson<LocalizationData>(json);
            
            // Manejar formato "texts" (UI general)
            if (data.texts != null)
                foreach (var entry in data.texts)
                    _table[entry.key] = entry.value;

            // Manejar formato "subtitles" (cinemáticas)
            if (data.subtitles != null)
                foreach (var entry in data.subtitles)
                    _table[entry.id] = entry.text;

            // Manejar subtítulos con timing (si existen)
            if (data.timedSubtitles != null)
                foreach (var entry in data.timedSubtitles)
                    _subs[entry.id] = entry;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Localization] Error parsing JSON: {e.Message}");
        }
    }

    public string Get(string key, string fallback = "")
    {
        // FIX: Dictionary<string,>.TryGetValue lanza ArgumentNullException si key es null (no admite
        // claves null). Antes esto solo lo evitaba quien llamara a Get() con cuidado; ahora es
        // defensivo aquí también, para que ninguna llamada externa con clave null/vacía pueda tirar
        // abajo un panel de UI entero (ver fix en LocalizedText.cs para el caso concreto que lo
        // disparaba: AddComponent<LocalizedText>().key = "..." asignado después de Awake()).
        if (string.IsNullOrEmpty(key)) return fallback;
        return _table.TryGetValue(key, out var value) ? value : fallback;
    }

    public SubtitleInfo GetSubtitle(string id)
    {
        return _subs.TryGetValue(id, out var info) ? info : null;
    }

    public void ChangeLanguage(string newLocale)
    {
        if (CurrentLocale != newLocale)
            LoadLocale(newLocale);
    }

    private string[] EnsureRequiredCatalogs(string[] current)
    {
        if (current == null || current.Length == 0)
            return (string[])RequiredCatalogs.Clone();

        var ordered = new List<string>();
        foreach (var entry in current)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            if (!ordered.Contains(entry))
                ordered.Add(entry);
        }

        foreach (var required in RequiredCatalogs)
        {
            if (!ordered.Contains(required))
                ordered.Add(required);
        }

        return ordered.ToArray();
    }

    [Serializable]
    private class LocalizationData
    {
        public TextEntry[] texts;           // Para UI general
        public SubtitleEntry[] subtitles;   // Para cinemáticas (tu formato)
        public SubtitleInfo[] timedSubtitles; // Para subtítulos con timing
    }

    [Serializable]
    private class TextEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    private class SubtitleEntry
    {
        public string id;
        public string text;
    }
}
