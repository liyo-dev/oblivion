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
    private static readonly Dictionary<AbilityKey, AbilityPresentationForKey> Defaults = new()
    {
        { AbilityKey.Swim,  new AbilityPresentationForKey { abilityKey = AbilityKey.Swim,  title = "Nadar",  description = "Permite moverse por el agua." } },
        { AbilityKey.Jump,  new AbilityPresentationForKey { abilityKey = AbilityKey.Jump,  title = "Saltar", description = "Habilidad básica para superar obstáculos." } },
        { AbilityKey.Climb, new AbilityPresentationForKey { abilityKey = AbilityKey.Climb, title = "Trepar", description = "Accede a superficies verticales." } },
        { AbilityKey.Magic, new AbilityPresentationForKey { abilityKey = AbilityKey.Magic, title = "Magia", description = "Activa el uso de hechizos." } },
        { AbilityKey.Fly,   new AbilityPresentationForKey { abilityKey = AbilityKey.Fly,   title = "Volar",  description = "Permite desplazarse por el aire." } },
        // NUEVO (1 sep 2026): Sprint y Shield ya disparaban OnAbilityUnlockedKey desde
        // TagMinigameController.cs (recompensa del minijuego de la taberna) y
        // PlayerShieldController.cs respectivamente, pero no tenían entrada aquí — el popup
        // salía con el nombre del enum en crudo ("Sprint"/"Shield") y sin descripción.
        { AbilityKey.Sprint, new AbilityPresentationForKey { abilityKey = AbilityKey.Sprint, title = "Esprintar", description = "Corre más rápido durante un tiempo limitado." } },
        { AbilityKey.Shield, new AbilityPresentationForKey { abilityKey = AbilityKey.Shield, title = "Escudo", description = "Levanta un escudo para bloquear ataques enemigos." } },
    };

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

        if (Defaults.TryGetValue(key, out var preset))
            return preset;

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
