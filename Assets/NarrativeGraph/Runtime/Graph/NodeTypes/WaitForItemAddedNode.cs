using System;
using UnityEngine;

/// <summary>
/// Espera a que se añada un item específico al inventario del jugador.
/// Útil para completar misiones cuando el jugador recoge/compra un item.
/// </summary>
[Serializable]
[SavePoint("Seguro guardar mientras espera item")]
public sealed class WaitForItemAddedNode : NarrativeNode
{
    [Header("Item Configuration")]
    [Tooltip("El item que debe ser añadido al inventario")]
    public ItemData itemToWaitFor;
    
    [Tooltip("Cantidad mínima que debe añadirse de una sola vez (0 = cualquier cantidad)")]
    [Min(0)] public int minimumAmount = 0;
    
    [Header("Options")]
    [Tooltip("Si está activo, se completa inmediatamente si el item ya está en el inventario")]
    public bool completeIfAlreadyOwned = false;
    
    [Tooltip("Activar logs de debug")]
    public bool debugLogs = false;

    private Action<ItemData, int, int> _itemAddedHandler;
    private Inventory _inventory;

    void Log(string msg)
    {
        if (debugLogs)
            Debug.Log($"[WaitForItemAdded:{guid}] {msg}");
    }

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (itemToWaitFor == null)
        {
            Debug.LogWarning($"[WaitForItemAdded:{guid}] No se configuró itemToWaitFor, avanzando inmediatamente");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Buscar el inventario del jugador
        if (!PlayerService.TryGetComponent<Inventory>(out _inventory, includeInactive: true, allowSceneLookup: true) || _inventory == null)
        {
            Debug.LogWarning($"[WaitForItemAdded:{guid}] No se encontró Inventory en el Player, avanzando inmediatamente");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Verificar si ya tiene el item (si está configurado)
        if (completeIfAlreadyOwned)
        {
            int currentCount = _inventory.Count(itemToWaitFor.itemId);
            if (currentCount > 0)
            {
                Log($"El jugador ya tiene {currentCount}x {itemToWaitFor.displayName}, completando inmediatamente");
                onReadyToAdvance?.Invoke();
                return;
            }
        }

        // Crear handler que escucha el evento OnItemAdded
        _itemAddedHandler = (addedItem, addedAmount, newTotal) =>
        {
            // Verificar si es el item correcto
            if (addedItem == null || addedItem.itemId != itemToWaitFor.itemId)
                return;

            // Verificar cantidad mínima si está configurada
            if (minimumAmount > 0 && addedAmount < minimumAmount)
            {
                Log($"Se añadió {addedAmount}x {addedItem.displayName} pero se requieren al menos {minimumAmount}, esperando más...");
                return;
            }

            Log($"¡Item añadido! {addedAmount}x {addedItem.displayName} (total: {newTotal}) → completando nodo");
            
            // Desuscribirse y avanzar
            Cleanup();
            onReadyToAdvance?.Invoke();
        };

        // Suscribirse al evento
        _inventory.OnItemAdded += _itemAddedHandler;
        Log($"Esperando a que se añada {itemToWaitFor.displayName}" + (minimumAmount > 0 ? $" (mínimo {minimumAmount})" : ""));
    }

    public override void Exit(NarrativeContext ctx)
    {
        Cleanup();
    }

    void Cleanup()
    {
        if (_itemAddedHandler != null && _inventory != null)
        {
            _inventory.OnItemAdded -= _itemAddedHandler;
            Log("Desuscrito del evento OnItemAdded");
        }
        _itemAddedHandler = null;
        _inventory = null;
    }
}
