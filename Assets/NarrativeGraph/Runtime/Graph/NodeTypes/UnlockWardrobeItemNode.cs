using System;
using UnityEngine;

[Serializable]
public sealed class UnlockWardrobeItemNode : NarrativeNode
{
    [Header("Vestuario")]
    public WardrobeItemSO wardrobeItem;
    public bool logWarnings = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Si el item ya está desbloqueado, WardrobeService devuelve false.
        // Esto es normal en rejugabilidad o recarga de saves, así que evitamos el warning.
        if (!WardrobeService.UnlockWardrobeItem(wardrobeItem, logWarnings))
        {
            if (logWarnings)
            {
                // Cambiado a Log normal para evitar spam de warnings
                Debug.Log($"[UnlockWardrobeItemNode] El item '{wardrobeItem?.name}' ya estaba desbloqueado o no se pudo desbloquear.");
            }
        }

        onReadyToAdvance?.Invoke();
    }
}
