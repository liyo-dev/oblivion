using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpellPresentation
{
    public SpellId spellId;
    public string title;
    [TextArea]
    public string description;
    public Sprite icon;
}

public static class SpellPresentationLookup
{
    private static readonly Dictionary<SpellId, SpellPresentation> Defaults = new()
    {
        { SpellId.Fireball,     new SpellPresentation { spellId = SpellId.Fireball,     title = "Bola de fuego",  description = "Proyectil ardiente que abrasa a los enemigos." } },
        { SpellId.Plasmaball,   new SpellPresentation { spellId = SpellId.Plasmaball,   title = "Plasmaball",     description = "Orbe de energía pura de alto impacto." } },
        { SpellId.LightSpecial, new SpellPresentation { spellId = SpellId.LightSpecial, title = "Luz especial",   description = "Destello de luz cegador de gran potencia." } },
        { SpellId.Levitation,   new SpellPresentation { spellId = SpellId.Levitation,   title = "Levitación",     description = "Permite flotar sobre el terreno." } },
        { SpellId.AuraEstelar,  new SpellPresentation { spellId = SpellId.AuraEstelar,  title = "Aura Estelar",   description = "Rodéate de un aura de energía estelar." } },
        { SpellId.Cycloneburst, new SpellPresentation { spellId = SpellId.Cycloneburst, title = "Ciclón",         description = "Explosión de viento devastadora en área." } },
    };

    public static SpellPresentation Resolve(SpellId spellId, IList<SpellPresentation> custom)
    {
        if (custom != null)
        {
            for (int i = 0; i < custom.Count; i++)
            {
                var entry = custom[i];
                if (entry != null && entry.spellId == spellId)
                    return entry;
            }
        }

        if (Defaults.TryGetValue(spellId, out var preset))
            return preset;

        return new SpellPresentation
        {
            spellId = spellId,
            title = spellId.ToString(),
            description = string.Empty,
            icon = null
        };
    }
}
