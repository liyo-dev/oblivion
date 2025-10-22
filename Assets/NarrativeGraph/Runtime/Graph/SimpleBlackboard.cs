// SimpleBlackboard.cs
using System.Collections.Generic;

public sealed class SimpleBlackboard
{
    readonly Dictionary<string, object> _data = new();
    public T Get<T>(string key, T fallback = default) => _data.TryGetValue(key, out var v) && v is T t ? t : fallback;
    public void Set<T>(string key, T value) => _data[key] = value;
    public bool Has(string key) => _data.ContainsKey(key);
    public void Clear() => _data.Clear();
}