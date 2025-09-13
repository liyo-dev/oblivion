using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [SerializeField] private string defaultLocale = "es";
    [SerializeField] private string[] catalogs = { "prologue", "ui" };

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

        foreach (var cat in catalogs)
        {
            var path = $"Localization/{cat}_{locale}";
            var textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogWarning($"[Localization] Missing catalog: {path}. Falling back to default.");
                var fallback = Resources.Load<TextAsset>($"Localization/{cat}_{defaultLocale}");
                if (fallback != null) MergeJsonIntoTables(fallback.text);
                continue;
            }
            MergeJsonIntoTables(textAsset.text);
        }

        PlayerPrefs.SetString("locale", locale);
        PlayerPrefs.Save();
        OnLocaleChanged?.Invoke();
    }

    public string Get(string key, string fallback = "")
    {
        if (string.IsNullOrEmpty(key)) return fallback;
        return _table.TryGetValue(key, out var v) ? v : (string.IsNullOrEmpty(fallback) ? key : fallback);
    }

    public bool TryGetSubtitleInfo(string id, out SubtitleInfo info)
    {
        return _subs.TryGetValue(id, out info);
    }

    private void MergeJsonIntoTables(string json)
    {
        var root = JsonUtility.FromJson<LocalizationRoot>(json);
        if (root == null) return;

        if (root.strings != null)
        {
            foreach (var kv in root.strings)
                _table[kv.key] = kv.value;
        }
        if (root.subtitles != null)
        {
            foreach (var s in root.subtitles)
            {
                _table[s.id] = s.text;
                _subs[s.id] = new SubtitleInfo { id = s.id, start = s.start, duration = s.duration };
            }
        }
    }

    [Serializable] private class LocalizationRoot
    {
        public SubtitleEntry[] subtitles;
        public StringKV[] strings;
    }
    [Serializable] private class SubtitleEntry
    {
        public string id;
        public string text;
        public float start;
        public float duration;
    }
    [Serializable] private class StringKV { public string key; public string value; }
}
