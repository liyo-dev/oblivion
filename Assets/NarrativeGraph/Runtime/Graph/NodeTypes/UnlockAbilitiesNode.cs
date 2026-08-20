﻿// UnlockAbilitiesNode.cs
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

    [Tooltip("Los hechizos dinámicos (aprendidos en runtime, vía UnlockService) solo existen en el " +
             "preset de Will: UnlockService siempre escribe en runtimePreset, que ES el preset de Will. " +
             "Liam y Estela no tienen progresión propia — usan un loadout fijo desde su NPCPartyConfig " +
             "(ver ActiveCharacterSwapper.ApplySpells). Con esta opción activa, si hay hechizos que " +
             "desbloquear el nodo cambia el personaje activo a Will ANTES de aplicarlos, para que quien " +
             "se ve en pantalla coincida siempre con quien realmente aprende el hechizo. Cambiar esto de " +
             "raíz (que lo aprenda quien esté seleccionado) requiere dar progresión propia a Liam/Estela " +
             "— eso sí es un cambio de arquitectura, no lo toques aquí.")]
    public bool forceWillBeforeSpellUnlock = true;

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

        // SIEMPRE diferir la ejecución para asegurar que los callbacks de diálogo terminen
        var runner = ctx.Runner;
        if (runner != null)
        {
            Debug.Log($"[UnlockAbilitiesNode] Difiriendo ejecución para permitir que callbacks de diálogo terminen");
            runner.StartCoroutine(ExecuteAfterDelay(ctx, onReadyToAdvance));
        }
        else
        {
            // Fallback si no hay runner
            ExecuteUnlocks(ctx, onReadyToAdvance);
        }
    }

    private System.Collections.IEnumerator ExecuteAfterDelay(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Esperar a que termine el frame actual (donde puede estar ejecutándose el callback del diálogo)
        yield return null;
        
        // Esperar un frame adicional para que el sistema de input se estabilice
        yield return null;
        
        Debug.Log($"[UnlockAbilitiesNode] Frames esperados, verificando estado del DialogueManager");
        
        // Verificar que realmente no hay diálogo activo
        var dialogueManager = UnityEngine.Object.FindAnyObjectByType<DialogueManager>();
        if (dialogueManager != null && dialogueManager.IsOpen)
        {
            Debug.LogWarning($"[UnlockAbilitiesNode] DialogueManager aún abierto después de esperar, esperando más...");
            // Esperar hasta que el diálogo se cierre
            while (dialogueManager != null && dialogueManager.IsOpen)
            {
                yield return null;
            }
            // Esperar un frame más después de que se cierre
            yield return null;
        }
        
        Debug.Log($"[UnlockAbilitiesNode] Ejecutando unlock ahora");
        ExecuteUnlocks(ctx, onReadyToAdvance);
    }

    private void ExecuteUnlocks(NarrativeContext ctx, Action onReadyToAdvance)
    {
        bool changed = false;

            try
            {
                // Compatibilidad: si venimos de assets antiguos, mapear AbilityId -> AbilityKey (solo MagicAttack -> Magic).
                // abilitiesToUnlock está marcado [Obsolete] a propósito: solo se lee aquí como shim de
                // compatibilidad con assets viejos, se suprime el warning en vez de eliminar el campo.
#pragma warning disable 618
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
#pragma warning restore 618

                if (spellsToUnlock != null && spellsToUnlock.Count > 0)
                {
                    // Ver comentario de forceWillBeforeSpellUnlock: los hechizos aprendidos en runtime
                    // solo se guardan en el preset de Will (arquitectura actual). Forzamos el swap ANTES
                    // de desbloquear para que no se dé el caso de "aprendes X con Liam/Estela seleccionado
                    // pero el hechizo en realidad se lo queda Will sin que se note en pantalla".
                    if (forceWillBeforeSpellUnlock)
                        PartyControlManager.Instance?.ForceSwitch(PartyControlManager.CharacterSlot.Will);

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
