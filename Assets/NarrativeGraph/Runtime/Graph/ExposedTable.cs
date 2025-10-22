// ExposedTable.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public sealed class ExposedPropertyTable : IExposedPropertyTable
{
    [Serializable] struct Entry { public PropertyName name; public UnityEngine.Object value; }
    [SerializeField] List<Entry> entries = new();

    public void SetReferenceValue(PropertyName id, UnityEngine.Object value)
    {
        int i = entries.FindIndex(e => e.name == id);
        if (i >= 0) entries[i] = new Entry{ name = id, value = value };
        else entries.Add(new Entry{ name = id, value = value });
    }

    public UnityEngine.Object GetReferenceValue(PropertyName id, out bool idValid)
    {
        int i = entries.FindIndex(e => e.name == id);
        if (i >= 0) { idValid = true; return entries[i].value; }
        idValid = false; return null;
    }

    public void ClearReferenceValue(PropertyName id)
    {
        int i = entries.FindIndex(e => e.name == id);
        if (i >= 0) entries.RemoveAt(i);
    }
}