using System;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    // Estado global del anchor actual
    public static string CurrentAnchorId { get; private set; }

    // Evento opcional para quien quiera reaccionar a cambios de anchor (HUD, etc.)
    public static event Action<string> OnAnchorChanged;

    private bool _initialized;

    void OnEnable()
    {
        GameBootService.OnProfileReady += HandleProfileReady;
        if (GameBootService.IsAvailable)
        {
            HandleProfileReady();
        }
    }

    void OnDisable()
    {
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    private void HandleProfileReady()
    {
        if (_initialized) return;

        // Inicializar con el anchor del preset activo
        var bootProfile = GameBootService.Profile;
        if (bootProfile != null)
        {
            var startAnchor = bootProfile.GetStartAnchorOrDefault();
            if (!string.IsNullOrEmpty(startAnchor))
            {
                SetCurrentAnchor(startAnchor);
            }
        }

        _initialized = true;
        GameBootService.OnProfileReady -= HandleProfileReady;
    }

    /// <summary>Establece el anchor actual (no mueve al jugador).</summary>
    public static void SetCurrentAnchor(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (id == CurrentAnchorId) return;

        CurrentAnchorId = id;

        // Persistir también en el runtimePreset del GameBootProfile
        var profile = GameBootService.Profile;
        if (profile != null)
        {
            var preset = profile.GetActivePresetResolved();
            if (preset != null)
            {
                preset.spawnAnchorId = id;
            }
        }

        OnAnchorChanged?.Invoke(id);
        // Debug.Log($"[SpawnManager] CurrentAnchorId = {id}");
    }

    /// <summary>Devuelve el SpawnAnchor por id usando tu clase del mundo.</summary>
    public static SpawnAnchor GetAnchor(string anchorId)
    {
        return SpawnAnchor.FindById(anchorId);
    }


    /// <summary>Teletransporta al jugador al anchor indicado por id.</summary>
    public static void TeleportTo(string anchorId, bool? useTransition = null)
    {
        if (string.IsNullOrEmpty(anchorId)) return;
        var player = PlayerService.Player;
        if (!player) { Debug.LogWarning("[SpawnManager] No se encontró player para teletransporte"); return; }
        TeleportService.TeleportToAnchor(player, anchorId, useTransition);
    }

    /// <summary>Teletransporta al jugador al anchor actual.</summary>
    public static void TeleportToCurrent(bool? useTransition = null)
    {
        if (string.IsNullOrEmpty(CurrentAnchorId)) return;
        var player = PlayerService.Player;
        if (!player) { Debug.LogWarning("[SpawnManager] No se encontró player para teletransporte"); return; }
        TeleportService.TeleportToAnchor(player, CurrentAnchorId, useTransition);
    }
}