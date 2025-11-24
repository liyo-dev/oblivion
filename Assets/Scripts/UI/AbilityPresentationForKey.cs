using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AbilityPresentationForKey
{
    public AbilityKey abilityKey;
    public string title;
    [TextArea]
    public string description;
    public Sprite icon;
}

public static class AbilityPresentationKeyLookup
{
    /// <summary>
    /// Resolve presentation for an AbilityKey using a custom list first, then fallback to a simple default.
    /// </summary>
    public static AbilityPresentationForKey Resolve(AbilityKey key, IList<AbilityPresentationForKey> custom)
    {
        if (custom != null)
        {
            for (int i = 0; i < custom.Count; i++)
            {
                var entry = custom[i];
                if (entry != null && entry.abilityKey == key) return entry;
            }
        }

        // Fallback: basic titles based on enum name
        return new AbilityPresentationForKey
        {
            abilityKey = key,
            title = key.ToString(),
            description = string.Empty,
            icon = null
        };
    }
}
