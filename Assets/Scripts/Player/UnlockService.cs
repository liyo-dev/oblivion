using System.Collections.Generic;
using UnityEngine;

/// Utilidades para modificar el preset activo del jugador (runtimePreset) de forma segura/idempotente.
public static class UnlockService
{
    /// Intenta obtener el preset activo (runtime) del GameBootProfile.
    public static PlayerPresetSO GetActivePreset()
    {
        if (!GameBootService.IsAvailable || GameBootService.Profile == null)
        {
            Debug.LogWarning("[UnlockService] GameBootService no está disponible todavía");
            return null;
        }
        return GameBootService.Profile.GetActivePresetResolved();
    }

    /// Desbloquea una habilidad en el preset activo. Devuelve true si añadió algo.
    public static bool UnlockAbility(AbilityId ability)
    {
        var preset = GetActivePreset();
        if (!preset) return false;
        if (preset.unlockedAbilities == null) preset.unlockedAbilities = new List<AbilityId>();
        if (preset.unlockedAbilities.Contains(ability)) return false;
        preset.unlockedAbilities.Add(ability);
        return true;
    }

    /// Desbloquea un hechizo y opcionalmente lo asigna a un slot vacío.
    public static bool UnlockSpell(SpellId spell, bool assignToEmptySlot = true)
    {
        var preset = GetActivePreset();
        if (!preset) return false;
        if (preset.unlockedSpells == null) preset.unlockedSpells = new List<SpellId>();

        bool changed = false;
        if (!preset.unlockedSpells.Contains(spell))
        {
            preset.unlockedSpells.Add(spell);
            changed = true;
        }

        if (assignToEmptySlot)
        {
            // Si es Special y el slot está vacío, asignarlo ahí; si no, usar Left/Right vacíos
            if (spell == SpellId.None)
            {
                // nada
            }
            else
            {
                // Regla simple: si Special está vacío y parece ser de tipo Special (heurística por nombre)
                bool looksSpecial = spell.ToString().ToLowerInvariant().Contains("special");
                if (looksSpecial)
                {
                    if (preset.specialSpellId == SpellId.None)
                    {
                        preset.specialSpellId = spell; changed = true;
                    }
                }
                else
                {
                    if (preset.leftSpellId == SpellId.None)
                    {
                        preset.leftSpellId = spell; changed = true;
                    }
                    else if (preset.rightSpellId == SpellId.None)
                    {
                        preset.rightSpellId = spell; changed = true;
                    }
                }
            }
        }

        return changed;
    }

    /// Asegura unos valores mínimos de maná en el preset activo.
    public static bool EnsureMana(float minMax, float minCurrent)
    {
        var preset = GetActivePreset();
        if (!preset) return false;
        float oldMax = preset.maxMP;
        float oldCur = preset.currentMP;
        preset.maxMP = Mathf.Max(preset.maxMP, minMax);
        preset.currentMP = Mathf.Clamp(Mathf.Max(preset.currentMP, minCurrent), 0f, preset.maxMP);
        return !Mathf.Approximately(oldMax, preset.maxMP) || !Mathf.Approximately(oldCur, preset.currentMP);
    }

    /// Añade un flag simple al preset (misiones/progresos).
    public static bool AddFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        var preset = GetActivePreset();
        if (!preset) return false;
        if (preset.flags == null) preset.flags = new List<string>();
        if (preset.flags.Contains(flag)) return false;
        preset.flags.Add(flag);
        return true;
    }
}

