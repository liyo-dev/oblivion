using System;
using System.Collections.Generic;
using UnityEngine;

public enum MenuKind
{
    None = 0,
    Equipment,
    Shop,
    Inventory,
    Pause,
    Dialog
}

/// <summary>
/// Simple central manager for menu open/close priorities.
/// Start conservative: disallow opening a menu if any other menu is open.
/// This avoids scattering "is open" checks across menus. It is intentionally
/// minimal and non-invasive. Rules can be extended later.
/// </summary>
public static class MenuManager
{
    public static event Action<MenuKind> MenuOpened;
    public static event Action<MenuKind> MenuClosed;

    static readonly Dictionary<MenuKind, bool> s_open = new();

    /// <summary>Return true if this menu kind is currently registered as open.</summary>
    public static bool IsOpen(MenuKind kind)
    {
        return s_open.TryGetValue(kind, out var v) && v;
    }

    /// <summary>Return true if any menu is open.</summary>
    public static bool AnyOpen()
    {
        foreach (var kv in s_open)
            if (kv.Value) return true;
        return false;
    }

    /// <summary>
    /// Try to open the requested menu. Returns true and registers it as open
    /// if no other menu is currently open. Does not automatically close others.
    /// </summary>
    public static bool TryOpen(MenuKind kind)
    {
        if (IsOpen(kind))
            return true;

        // Clean up stale registrations by validating known menu instances.
        // If a menu was marked open in s_open but the actual UI reports closed,
        // reset the registration so it doesn't block new opens.
        var keys = new List<MenuKind>(s_open.Keys);
        foreach (var k in keys)
        {
            if (!s_open.TryGetValue(k, out var val) || !val) continue;
            bool stillOpen = true;
            switch (k)
            {
                case MenuKind.Equipment:
                    stillOpen = PlayerEquipmentMenuController.IsOpen;
                    break;
                case MenuKind.Pause:
                    stillOpen = GameState.Is(GamePhase.PauseMenu);
                    break;
                case MenuKind.Inventory:
                    stillOpen = GameState.Is(GamePhase.Inventory) || GameState.Is(GamePhase.Equipment);
                    break;
                case MenuKind.Dialog:
                    stillOpen = GameState.Is(GamePhase.Dialogue);
                    break;
                case MenuKind.Shop:
                    // Locate any ShopUI instances that are part of a scene (skip assets/prefabs)
#if UNITY_2022_3_OR_NEWER
                    var shops = Object.FindObjectsByType<ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                    var shops = Resources.FindObjectsOfTypeAll(typeof(ShopUI)) as ShopUI[];
#endif
                    stillOpen = false;
                    if (shops != null)
                    {
                        foreach (var shop in shops)
                        {
                            if (shop == null || !shop.gameObject.scene.IsValid()) continue;
                            if (shop.IsOpen)
                            {
                                stillOpen = true;
                                break;
                            }
                        }
                    }
                    break;
                default:
                    stillOpen = val;
                    break;
            }

            if (!stillOpen)
            {
                s_open[k] = false;
                Debug.Log($"[MenuManager] Cleared stale registration for {k}");
            }
        }

        // Conservative rule: if any other menu is open, deny open.
        foreach (var kv in s_open)
        {
            if (kv.Value)
            {
                Debug.Log($"[MenuManager] Deny open {kind} because {kv.Key} is open");
                return false;
            }
        }

        s_open[kind] = true;
        Debug.Log($"[MenuManager] Opened {kind}");
        MenuOpened?.Invoke(kind);
        return true;
    }

    /// <summary>Mark a menu as closed.</summary>
    public static void Close(MenuKind kind)
    {
        bool wasOpen = IsOpen(kind);
        if (s_open.ContainsKey(kind))
            s_open[kind] = false;
        else
            s_open[kind] = false;
        Debug.Log($"[MenuManager] Closed {kind}");
        if (wasOpen)
            MenuClosed?.Invoke(kind);
    }

    /// <summary>Utility that registers a menu as open (used if a menu is opened externally).</summary>
    public static void RegisterOpen(MenuKind kind)
    {
        bool alreadyOpen = IsOpen(kind);
        s_open[kind] = true;
        if (!alreadyOpen)
            MenuOpened?.Invoke(kind);
    }

    /// <summary>Reset internal state (for debug/tests).</summary>
    public static void Reset()
    {
        s_open.Clear();
    }
}
