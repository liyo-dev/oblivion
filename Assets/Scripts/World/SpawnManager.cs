using System;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    // Estado global del anchor actual
    public static string CurrentAnchorId { get; private set; }

    // Evento opcional para quien quiera reaccionar a cambios de anchor (HUD, etc.)
    public static event Action<string> OnAnchorChanged;

    /// <summary>Establece el anchor actual (no mueve al jugador).</summary>
    public static void SetCurrentAnchor(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (id == CurrentAnchorId) return;

        CurrentAnchorId = id;
        OnAnchorChanged?.Invoke(id);
        // Debug.Log($"[SpawnManager] CurrentAnchorId = {id}");
    }

    /// <summary>Devuelve el SpawnAnchor por id usando tu clase del mundo.</summary>
    public static SpawnAnchor GetAnchor(string anchorId)
    {
        return SpawnAnchor.FindById(anchorId);
    }

    /// <summary>Coloca al jugador en el anchor indicado usando TeleportService.</summary>
    public static void PlaceAtAnchor(GameObject player, string anchorId, bool immediate = true)
    {
        TeleportService.PlaceAtAnchor(player, anchorId, immediate);
        // TeleportService ya llama a SetCurrentAnchor(anchorId)
    }

    /// <summary>Coloca al jugador en el anchor actual (si existe).</summary>
    public static void PlaceAtCurrent(GameObject player, bool immediate = true)
    {
        if (string.IsNullOrEmpty(CurrentAnchorId)) return;
        TeleportService.PlaceAtAnchor(player, CurrentAnchorId, immediate);
    }
}