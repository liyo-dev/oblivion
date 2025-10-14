using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [SerializeField] private string defaultLocale = "es";
    [SerializeField] private string[] catalogs = { "prologue", "ui", "cinematicintro", "cinematicdemon", "dialogues", "quests" };

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

        var locale = PlayerPrefs.GetString("locale", defaultLocale);
        LoadLocale(locale);
    }

    public void LoadLocale(string locale)
    {
        _table.Clear();
        _subs.Clear();
        CurrentLocale = locale;

        Debug.Log($"[LocalizationManager] Cargando idioma: {locale}");
        Debug.Log($"[LocalizationManager] Catálogos a cargar: {string.Join(", ", catalogs)}");

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
                    Debug.LogError($"[LocalizationManager] ✗ No se encontró ni el catálogo {path} ni el fallback");
                }
            }
            else
            {
                MergeJsonIntoTables(textAsset.text);
                Debug.Log($"[LocalizationManager] ✓ Cargado catálogo: {cat}_{locale} ({_table.Count} entradas totales)");
            }
        }

        PlayerPrefs.SetString("locale", locale);
        OnLocaleChanged?.Invoke();
        
        Debug.Log($"[LocalizationManager] Localización completa. Total de {_table.Count} traducciones cargadas.");
        
        // Debug: Mostrar algunas claves cargadas
        int count = 0;
        foreach (var key in _table.Keys)
        {
            if (key.StartsWith("DLG_") || key.StartsWith("CHAR_"))
            {
                Debug.Log($"[LocalizationManager]   → {key} = {_table[key]}");
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
