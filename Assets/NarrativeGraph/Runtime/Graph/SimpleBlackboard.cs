// SimpleBlackboard.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SimpleBlackboard
{
    readonly Dictionary<string, object> _data = new();
    public T Get<T>(string key, T fallback = default) => _data.TryGetValue(key, out var v) && v is T t ? t : fallback;
    public void Set<T>(string key, T value) => _data[key] = value;
    public bool Has(string key) => _data.ContainsKey(key);
    public void Clear() => _data.Clear();

    // === Serializable snapshot helpers ===
    [Serializable]
    public class Entry
    {
        public string key;
        public string type; // "int", "float", "bool", "string"
        public string value;
    }

    public List<Entry> ExportToSerializable()
    {
        var list = new List<Entry>();
        foreach (var kv in _data)
        {
            if (kv.Value == null) continue;
            var e = new Entry { key = kv.Key };
            switch (kv.Value)
            {
                case int i:
                    e.type = "int"; e.value = i.ToString(); break;
                case float f:
                    e.type = "float"; e.value = f.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                case bool b:
                    e.type = "bool"; e.value = b ? "1" : "0"; break;
                case string s:
                    e.type = "string"; e.value = s; break;
                default:
                    // omit unsupported types
                    continue;
            }
            list.Add(e);
        }
        return list;
    }

    public void ImportFromSerializable(List<Entry> entries)
    {
        if (entries == null) return;
        _data.Clear();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.key) || string.IsNullOrEmpty(e.type)) continue;
            try
            {
                switch (e.type)
                {
                    case "int":
                        if (int.TryParse(e.value, out var vi)) _data[e.key] = vi;
                        break;
                    case "float":
                        if (float.TryParse(e.value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vf)) _data[e.key] = vf;
                        break;
                    case "bool":
                        _data[e.key] = (e.value == "1" || e.value?.ToLowerInvariant() == "true");
                        break;
                    case "string":
                        _data[e.key] = e.value ?? string.Empty;
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimpleBlackboard] Error al importar entry '{e?.key}': {ex.Message}");
            }
        }
    }
}