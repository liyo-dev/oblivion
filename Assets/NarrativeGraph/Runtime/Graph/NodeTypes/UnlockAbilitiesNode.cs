// UnlockAbilitiesNode.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UnlockAbilitiesNode : NarrativeNode
{
    [Header("Internal Unlock - Abilities & Spells")]
    [Tooltip("Habilidades principales (swim/jump/climb/magic/fly) a desbloquear desde el grafo")]
    public List<AbilityKey> abilityKeysToUnlock = new();

    [Obsolete("Usa abilityKeysToUnlock (AbilityKey). Este campo solo se mantiene para compatibilidad.")]
    [SerializeField, HideInInspector] private List<AbilityId> abilitiesToUnlock = new();

    [Tooltip("Lista de hechizos a desbloquear desde el grafo")]
    public List<SpellId> spellsToUnlock = new();

    [Tooltip("Si se asignan hechizos, intentar ponerlos en ranuras vacías")]
    public bool assignSpellsToEmptySlot = true;

    [Header("One-shot / Flags")]
    [Tooltip("Si se establece, añadirá este flag al preset (one-shot).")]
    public string oneShotFlag = "";

    [Header("Save/Apply")]
    public bool applyPresetAfterUnlock = true;
    public bool saveAfterUnlock = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Si tiene oneShotFlag configurado, verificar si ya se ejecutó
        if (!string.IsNullOrEmpty(oneShotFlag))
        {
            if (UnlockService.HasFlag(oneShotFlag))
            {
                Debug.Log($"[UnlockAbilitiesNode] Ya se ejecutó previamente (flag: {oneShotFlag}), saltando.");
                onReadyToAdvance?.Invoke();
                return;
            }
        }

        bool changed = false;
        
            try
            {
                // Compatibilidad: si venimos de assets antiguos, mapear AbilityId -> AbilityKey (solo MagicAttack -> Magic).
                if ((abilityKeysToUnlock == null || abilityKeysToUnlock.Count == 0) && abilitiesToUnlock != null && abilitiesToUnlock.Count > 0)
                {
                    foreach (var legacy in abilitiesToUnlock)
                    {
                        if (legacy == AbilityId.MagicAttack && !abilityKeysToUnlock.Contains(AbilityKey.Magic))
                            abilityKeysToUnlock.Add(AbilityKey.Magic);
                    }
                }

                if (abilityKeysToUnlock != null)
                {
                    for (int i = 0; i < abilityKeysToUnlock.Count; i++)
                        changed |= UnlockService.UnlockAbility(abilityKeysToUnlock[i]);
                }

                // Legacy support: mantener UnlockAbility(AbilityId) por si queda algo en assets antiguos
                if (abilitiesToUnlock != null)
                {
                    for (int i = 0; i < abilitiesToUnlock.Count; i++)
                        changed |= UnlockService.UnlockAbility(abilitiesToUnlock[i]);
                }

                if (spellsToUnlock != null)
                {
                    for (int i = 0; i < spellsToUnlock.Count; i++)
                        changed |= UnlockService.UnlockSpell(spellsToUnlock[i], assignSpellsToEmptySlot);
                }

                if (!string.IsNullOrEmpty(oneShotFlag))
                {
                    changed |= UnlockService.AddFlag(oneShotFlag);
                }

                if (changed && applyPresetAfterUnlock)
                {
                    // Buscar PlayerPresetService incluyendo objetos inactivos.
                    var ps = ServiceLocator.Get<PlayerPresetService>(false);

                    // Fallback: buscar por el Player y componentes en hijos (incluyendo inactivos).
                    if (ps == null)
                    {
                        var player = GameObject.FindWithTag("Player");
                        if (player != null)
                            ps = player.GetComponentInChildren<PlayerPresetService>(true);
                    }

                    if (ps != null)
                    {
                        // No restaurar inventario al desbloquear abilities (solo actualizar abilities)
                        ps.ApplyCurrentPreset(includeInventory: false);
                    }
                    else
                    {
                        Debug.LogWarning("[UnlockAbilitiesNode] No se encontró PlayerPresetService para aplicar el preset. El preset quedó actualizado.");
                    }

                    // Nota: mantenemos el guardado deshabilitado aquí (como antes).
                    if (saveAfterUnlock)
                    {
                        Debug.Log("[UnlockAbilitiesNode] Auto-guardado deshabilitado. Usa un punto de guardado para conservar el progreso.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnlockAbilitiesNode] Error al aplicar unlock interno: {ex.Message}");
            }

        // Avanzar inmediatamente sin esperar popup
        onReadyToAdvance?.Invoke();
    }
}
