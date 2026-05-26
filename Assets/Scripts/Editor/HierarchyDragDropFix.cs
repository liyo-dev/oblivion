using UnityEditor;
using UnityEngine;

/// <summary>
/// Workaround para bug interno de Unity 6 (6000.x):
/// DragAndDrop.Drop llama DynamicInvoke pasando UInt64 (EntityId) pero el handler
/// interno espera Int32, produciendo ArgumentException en cada drag-and-drop
/// en la ventana de Jerarquía.
///
/// Este handler se registra antes que el handler por defecto. Al devolver un
/// DragAndDropVisualMode distinto de None cuando hay GameObjects arrastrados,
/// Unity omite el handler interno defectuoso.
/// </summary>
[InitializeOnLoad]
static class HierarchyDragDropFix
{
    static HierarchyDragDropFix()
    {
        DragAndDrop.AddDropHandler(OnHierarchyDrop);
    }

    static DragAndDropVisualMode OnHierarchyDrop(
        int dropTargetInstanceID,
        HierarchyDropFlags dropMode,
        Transform parentForDraggedObjects,
        bool perform)
    {
        var refs = DragAndDrop.objectReferences;
        if (refs == null || refs.Length == 0)
            return DragAndDropVisualMode.None;

        // Solo interceptar GameObjects — para otros tipos (assets, etc.) dejar el default
        bool anyGO = false;
        foreach (var r in refs)
        {
            if (r is GameObject)
            {
                anyGO = true;
                break;
            }
        }
        if (!anyGO)
            return DragAndDropVisualMode.None;

        // Fase de preview: solo indicar el cursor de movimiento
        if (!perform)
            return DragAndDropVisualMode.Move;

        // ─── Fase de ejecución: realizar el reparenting ─────────────────

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Mover en Jerarquía");

        // Calcular índice de hermano para inserción entre nodos
        int siblingIndex = -1;
        bool dropBetween = (dropMode & HierarchyDropFlags.DropBetween) != 0;

        if (dropBetween && dropTargetInstanceID != 0)
        {
            var targetObj = EditorUtility.InstanceIDToObject(dropTargetInstanceID) as GameObject;
            if (targetObj != null)
            {
                siblingIndex = targetObj.transform.GetSiblingIndex();

                // Si se suelta debajo del target, el índice es el siguiente
                bool below = (dropMode & HierarchyDropFlags.DropAfterParent) != 0
                          || ((int)dropMode & 8) != 0;
                if (below)
                    siblingIndex++;
            }
        }

        foreach (var r in refs)
        {
            if (r is not GameObject go) continue;

            Undo.SetTransformParent(go.transform, parentForDraggedObjects, "Mover " + go.name);

            if (siblingIndex >= 0)
            {
                go.transform.SetSiblingIndex(siblingIndex);
                siblingIndex++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        return DragAndDropVisualMode.Move;
    }
}
