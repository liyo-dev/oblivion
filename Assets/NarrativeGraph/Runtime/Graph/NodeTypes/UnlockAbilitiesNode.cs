// UnlockAbilitiesNode.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UnlockAbilitiesNode : NarrativeNode
{
    [Header("Internal Unlock - Abilities & Spells")]
    [Tooltip("Lista de habilidades a desbloquear desde el grafo")]
    public List<AbilityId> abilitiesToUnlock = new();

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
                    var ps = UnityEngine.Object.FindFirstObjectByType<PlayerPresetService>(FindObjectsInactive.Include);

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

        // Nodo de acción inmediata: avanza
        onReadyToAdvance?.Invoke();
    }
}
