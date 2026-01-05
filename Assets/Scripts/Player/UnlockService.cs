﻿using System.Collections.Generic;
using UnityEngine;

/// Utilidades para modificar el preset activo del jugador (runtimePreset) de forma segura/idempotente.
public static class UnlockService
{
    public static event System.Action<AbilityId> OnAbilityUnlocked;
    // New event for the boolean-based abilities (PlayerAbilities -> AbilityKey)
    public static event System.Action<AbilityKey> OnAbilityUnlockedKey;

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

    /// Desbloquea una habilidad en el preset activo. Devuelve true si se modificó el preset (lista u otros datos asociados).
    public static bool UnlockAbility(AbilityId ability)
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.unlockedAbilities == null)
            preset.unlockedAbilities = new List<AbilityId>();

        bool changed = false;

        if (!preset.unlockedAbilities.Contains(ability))
        {
            preset.unlockedAbilities.Add(ability);
            changed = true;
            OnAbilityUnlocked?.Invoke(ability);
        }

        switch (ability)
        {
            case AbilityId.MagicAttack:
                // Garantizar que el preset permite usar magia (habilita PlayerActionManager.AllowMagic)
                if (preset.abilities == null)
                    preset.abilities = new PlayerAbilities();

                if (!preset.abilities.magic)
                {
                    preset.abilities.magic = true;
                    changed = true;
                }

                // Asegurar que exista una reserva mínima de maná y RELLENAR AL 100%
                const float minMagicMaxMp = 50f;
                float desiredMax = Mathf.Max(preset.maxMP, minMagicMaxMp);
                // Al desbloquear magia, SIEMPRE rellenar al máximo
                float desiredCurrent = desiredMax;
                
                if (!Mathf.Approximately(preset.maxMP, desiredMax) || !Mathf.Approximately(preset.currentMP, desiredCurrent))
                {
                    preset.maxMP = desiredMax;
                    preset.currentMP = desiredCurrent;
                    changed = true;
                    Debug.Log($"[UnlockService] MagicAttack desbloqueado: Maná seteado a {desiredCurrent}/{desiredMax} (100%)");
                }

                // Propagar por el sistema un unlock de la habilidad "Magic" (AbilityKey)
                try { OnAbilityUnlockedKey?.Invoke(AbilityKey.Magic); } catch { }
                break;
        }

        // ✅ CRÍTICO: Aplicar cambios al jugador inmediatamente
        if (changed)
        {
            ApplyPresetToPlayer();
        }

        return changed;
    }

    /// Desbloquea una de las habilidades booleanas principales (Swim, Jump, Climb, Magic, Fly).
    public static bool UnlockAbility(AbilityKey key)
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.abilities == null)
            preset.abilities = new PlayerAbilities();

        bool changed = false;
        switch (key)
        {
            case AbilityKey.Swim:
                if (!preset.abilities.swim)
                {
                    preset.abilities.swim = true;
                    changed = true;
                }
                break;
            case AbilityKey.Jump:
                if (!preset.abilities.jump)
                {
                    preset.abilities.jump = true;
                    changed = true;
                }
                break;
            case AbilityKey.Climb:
                if (!preset.abilities.climb)
                {
                    preset.abilities.climb = true;
                    changed = true;
                }
                break;
            case AbilityKey.Magic:
                if (!preset.abilities.magic)
                {
                    preset.abilities.magic = true;
                    changed = true;
                }
                // Asegurar valores mínimos de maná y RELLENAR AL 100% al desbloquear magia
                {
                    const float minMagicMaxMp = 50f;
                    float newMax = Mathf.Max(preset.maxMP, minMagicMaxMp);
                    // Al desbloquear magia, SIEMPRE rellenar al máximo
                    float newCurrent = newMax;
                    
                    if (!Mathf.Approximately(preset.maxMP, newMax) || !Mathf.Approximately(preset.currentMP, newCurrent))
                    {
                        preset.maxMP = newMax;
                        preset.currentMP = newCurrent;
                        changed = true;
                        Debug.Log($"[UnlockService] Magic desbloqueada: Maná seteado a {newCurrent}/{newMax} (100%)");
                    }
                }
                break;
            case AbilityKey.Fly:
                if (!preset.abilities.fly)
                {
                    preset.abilities.fly = true;
                    changed = true;
                }
                break;
            default:
                break;
        }

        if (changed)
        {
            try { OnAbilityUnlockedKey?.Invoke(key); } catch { }
            
            // ✅ CRÍTICO: Aplicar cambios al jugador inmediatamente
            ApplyPresetToPlayer();
        }

        return changed;
    }

    /// Desbloquea volar en el preset activo.
    public static bool UnlockFly()
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.abilities == null)
            preset.abilities = new PlayerAbilities();

        if (!preset.abilities.fly)
        {
            preset.abilities.fly = true;
            try { OnAbilityUnlockedKey?.Invoke(AbilityKey.Fly); } catch { }
            return true;
        }
        return false;
    }

    /// Desbloquea nadar en el preset activo.
    public static bool UnlockSwim()
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.abilities == null)
            preset.abilities = new PlayerAbilities();

        if (!preset.abilities.swim)
        {
            preset.abilities.swim = true;
            try { OnAbilityUnlockedKey?.Invoke(AbilityKey.Swim); } catch { }
            return true;
        }
        return false;
    }

    /// Desbloquea trepar en el preset activo.
    public static bool UnlockClimb()
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.abilities == null)
            preset.abilities = new PlayerAbilities();

        if (!preset.abilities.climb)
        {
            preset.abilities.climb = true;
            try { OnAbilityUnlockedKey?.Invoke(AbilityKey.Climb); } catch { }
            return true;
        }
        return false;
    }

    /// Desbloquea saltar en el preset activo.
    public static bool UnlockJump()
    {
        var preset = GetActivePreset();
        if (!preset) return false;

        if (preset.abilities == null)
            preset.abilities = new PlayerAbilities();

        if (!preset.abilities.jump)
        {
            preset.abilities.jump = true;
            try { OnAbilityUnlockedKey?.Invoke(AbilityKey.Jump); } catch { }
            return true;
        }
        return false;
    }

    /// Desbloquea un hechizo y opcionalmente lo asigna a un slot vacío.
    public static bool UnlockSpell(SpellId spell, bool assignToEmptySlot = true)
    {
        var preset = GetActivePreset();
        if (!preset) return false;
        if (preset.unlockedSpells == null) preset.unlockedSpells = new List<SpellId>();

        bool changed = false;
        bool isNewUnlock = false;
        
        if (!preset.unlockedSpells.Contains(spell))
        {
            preset.unlockedSpells.Add(spell);
            changed = true;
            isNewUnlock = true;
        }

        // Solo asignar a un slot vacío si es la PRIMERA VEZ que se desbloquea
        // Si ya estaba desbloqueado (carga de partida), respetar la configuración del usuario
        if (assignToEmptySlot && isNewUnlock)
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
        Debug.Log($"[UnlockService] EnsureMana(minMax={minMax}, minCurrent={minCurrent}) - Antes: {oldMax}/{oldCur} -> Después: {preset.maxMP}/{preset.currentMP}");
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

    /// Verifica si un flag existe en el preset activo.
    public static bool HasFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag)) return false;
        var preset = GetActivePreset();
        if (!preset || preset.flags == null) return false;
        return preset.flags.Contains(flag);
    }

    /// Aplica el preset activo al jugador en escena (si existe).
    /// Esto actualiza ManaPool, hechizos, abilities, etc. inmediatamente.
    private static void ApplyPresetToPlayer()
    {
        // Buscar PlayerPresetService en el jugador activo
        if (PlayerService.TryGetComponent<PlayerPresetService>(out var presetService, includeInactive: false, allowSceneLookup: true))
        {
            // ✅ CORREGIDO: NO incluir inventario al aplicar preset tras desbloquear abilities
            // El inventario debe conservarse con los items que el jugador ha recogido
            presetService.ApplyCurrentPreset(includeInventory: false, includeAbilities: true);
        }
    }
}

