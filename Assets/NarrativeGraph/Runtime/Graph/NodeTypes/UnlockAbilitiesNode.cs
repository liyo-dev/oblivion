// UnlockAbilitiesNode.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class UnlockAbilitiesNode : NarrativeNode
{
    [Tooltip("Usar desbloqueo interno mediante selección en la tarjeta (no requiere GameObject)")]
    public bool useInternalUnlock = true;

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
        bool changed = false;

        if (useInternalUnlock)
        {
            try
            {
                if (abilitiesToUnlock != null)
                {
                    foreach (var ab in abilitiesToUnlock)
                        changed |= UnlockService.UnlockAbility(ab);
                }

                if (spellsToUnlock != null)
                {
                    foreach (var sp in spellsToUnlock)
                        changed |= UnlockService.UnlockSpell(sp, assignSpellsToEmptySlot);
                }

                if (!string.IsNullOrEmpty(oneShotFlag))
                {
                    changed |= UnlockService.AddFlag(oneShotFlag);
                }

                if (changed && applyPresetAfterUnlock)
                {
                    var ps = UnityEngine.Object.FindFirstObjectByType<PlayerPresetService>();
                    if (ps != null) ps.ApplyCurrentPreset();

                    if (saveAfterUnlock)
                    {
                        var profile = GameBootService.Profile;
                        var saveSystem = UnityEngine.Object.FindFirstObjectByType<SaveSystem>();
                        if (profile != null && saveSystem != null)
                        {
                            try { profile.SaveCurrentGameState(saveSystem, SaveRequestContext.Auto); }
                            catch (Exception ex) { Debug.LogWarning($"[UnlockAbilitiesNode] Error al guardar tras unlock: {ex.Message}"); }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnlockAbilitiesNode] Error al aplicar unlock interno: {ex.Message}");
            }
        }

        // Este nodo es puramente de acción: avanza inmediatamente
        onReadyToAdvance?.Invoke();
    }
}

