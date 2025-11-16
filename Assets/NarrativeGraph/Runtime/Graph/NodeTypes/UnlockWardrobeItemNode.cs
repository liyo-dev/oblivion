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
        if (!WardrobeService.UnlockWardrobeItem(wardrobeItem, logWarnings) && logWarnings)
            Debug.LogWarning("[UnlockWardrobeItemNode] No se pudo desbloquear la prenda.");

        onReadyToAdvance?.Invoke();
    }
}
