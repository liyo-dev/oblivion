using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Información de presentación para cada habilidad (nombre, descripción e icono).
/// Se usa tanto en el inventario como en los popups de desbloqueo.
/// </summary>
[System.Serializable]
public class AbilityPresentation
{
    public AbilityId abilityId;
    public string title;
    [TextArea]
    public string description;
    public Sprite icon;
}

public static class AbilityPresentationLookup
{
    private static readonly Dictionary<AbilityId, AbilityPresentation> Defaults = new()
    {
        { AbilityId.PhysicalAttack, new AbilityPresentation { abilityId = AbilityId.PhysicalAttack, title = "Ataque físico", description = "Golpe básico cuerpo a cuerpo." } },
        { AbilityId.MagicAttack,    new AbilityPresentation { abilityId = AbilityId.MagicAttack,    title = "Magia",          description = "Permite canalizar y usar hechizos." } },
        { AbilityId.Dash,           new AbilityPresentation { abilityId = AbilityId.Dash,           title = "Impulso",        description = "Esquiva rápida para evitar daño." } },
        { AbilityId.Block,          new AbilityPresentation { abilityId = AbilityId.Block,          title = "Bloqueo",        description = "Levanta la guardia para reducir daño." } },
    };

    /// <summary>
    /// Devuelve la presentación para una habilidad usando primero la lista personalizada proporcionada.
    /// Si no hay coincidencia se usan valores por defecto o el id como título.
    /// </summary>
    public static AbilityPresentation Resolve(AbilityId abilityId, IList<AbilityPresentation> custom)
    {
        if (custom != null)
        {
            for (int i = 0; i < custom.Count; i++)
            {
                var entry = custom[i];
                if (entry != null && entry.abilityId == abilityId)
                {
                    return entry;
                }
            }
        }

        if (Defaults.TryGetValue(abilityId, out var preset))
        {
            return preset;
        }

        return new AbilityPresentation
        {
            abilityId = abilityId,
            title = abilityId.ToString(),
            description = string.Empty,
            icon = null
        };
    }
}
