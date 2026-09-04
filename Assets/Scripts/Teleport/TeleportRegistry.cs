using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro global de puntos de teletransporte desbloqueados.
/// Se persiste junto con los datos del jugador.
/// </summary>
public static class TeleportRegistry
{
    private static readonly Dictionary<string, TeleportPointData> _unlockedPoints = new();
    
    /// <summary>Evento disparado cuando se desbloquea un nuevo punto.</summary>
    public static event Action<TeleportPointData> OnPointUnlocked;
    
    /// <summary>Evento disparado cuando cambia la lista de puntos.</summary>
    public static event Action OnRegistryChanged;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _unlockedPoints.Clear();
        OnPointUnlocked = null;
        OnRegistryChanged = null;
    }
    #endif
    
    /// <summary>Número de puntos desbloqueados.</summary>
    public static int UnlockedCount => _unlockedPoints.Count;
    
    /// <summary>Retorna true si el sistema de teletransporte está disponible (más de 1 punto desbloqueado).</summary>
    public static bool IsSystemAvailable => _unlockedPoints.Count > 1;
    
    /// <summary>Registra un punto de teletransporte como desbloqueado.</summary>
    /// <returns>True si es un punto nuevo, false si ya estaba desbloqueado.</returns>
    public static bool UnlockPoint(TeleportPointData point)
    {
        if (point == null || string.IsNullOrEmpty(point.anchorId))
        {
            Debug.LogWarning("[TeleportRegistry] Intento de registrar un punto inválido.");
            return false;
        }
        
        if (_unlockedPoints.ContainsKey(point.anchorId))
        {
            // Ya existe, actualizar datos si es necesario
            _unlockedPoints[point.anchorId] = point;
            return false;
        }
        
        _unlockedPoints[point.anchorId] = point;
        Debug.Log($"[TeleportRegistry] ✨ Nuevo punto de teletransporte desbloqueado: {point.displayName} ({point.anchorId})");
        
        // Sincronizar con el preset para persistencia durante gameplay
        SyncToPreset();
        
        OnPointUnlocked?.Invoke(point);
        OnRegistryChanged?.Invoke();

        return true;
    }
    
    /// <summary>Registra un punto usando solo el anchorId (buscará el SpawnAnchor).</summary>
    public static bool UnlockPoint(string anchorId, string displayNameOverride = null)
    {
        if (string.IsNullOrEmpty(anchorId)) return false;
        
        string displayName = displayNameOverride;
        
        if (string.IsNullOrEmpty(displayName))
        {
            // Intentar obtener nombre localizado o usar el anchorId
            displayName = GetLocalizedPointName(anchorId);
        }
        
        var point = new TeleportPointData(anchorId, displayName);
        return UnlockPoint(point);
    }
    
    /// <summary>Verifica si un punto está desbloqueado.</summary>
    public static bool IsUnlocked(string anchorId)
    {
        return !string.IsNullOrEmpty(anchorId) && _unlockedPoints.ContainsKey(anchorId);
    }
    
    /// <summary>Obtiene los datos de un punto desbloqueado.</summary>
    public static TeleportPointData GetPoint(string anchorId)
    {
        _unlockedPoints.TryGetValue(anchorId, out var point);
        return point;
    }
    
    /// <summary>Obtiene todos los puntos desbloqueados.</summary>
    public static IReadOnlyCollection<TeleportPointData> GetAllUnlockedPoints()
    {
        return _unlockedPoints.Values;
    }
    
    /// <summary>Obtiene los puntos desbloqueados excluyendo el anchorId actual.</summary>
    public static List<TeleportPointData> GetAvailableDestinations(string currentAnchorId)
    {
        var result = new List<TeleportPointData>();
        foreach (var kvp in _unlockedPoints)
        {
            if (kvp.Key != currentAnchorId)
                result.Add(kvp.Value);
        }
        return result;
    }
    
    /// <summary>Limpia todos los puntos desbloqueados (usado al iniciar nueva partida).</summary>
    public static void Clear()
    {
        _unlockedPoints.Clear();
        OnRegistryChanged?.Invoke();
        Debug.Log("[TeleportRegistry] Registro limpiado.");
    }
    
    /// <summary>Carga los puntos desbloqueados desde una lista serializada.</summary>
    public static void LoadFromSaveData(List<string> unlockedAnchorIds)
    {
        _unlockedPoints.Clear();
        
        if (unlockedAnchorIds == null || unlockedAnchorIds.Count == 0)
        {
            Debug.Log("[TeleportRegistry] No hay puntos de teletransporte guardados.");
            return;
        }
        
        foreach (var anchorId in unlockedAnchorIds)
        {
            if (string.IsNullOrEmpty(anchorId)) continue;
            
            var displayName = GetLocalizedPointName(anchorId);
            var point = new TeleportPointData(anchorId, displayName);
            _unlockedPoints[anchorId] = point;
        }
        
        Debug.Log($"[TeleportRegistry] Cargados {_unlockedPoints.Count} puntos de teletransporte.");
        OnRegistryChanged?.Invoke();
    }
    
    /// <summary>
    /// Actualiza el nombre de visualización de un punto YA desbloqueado.
    /// No desbloquea el punto si no estaba previamente en el registro.
    /// </summary>
    public static void UpdateDisplayNameIfUnlocked(string anchorId, string displayName)
    {
        if (string.IsNullOrEmpty(anchorId) || string.IsNullOrEmpty(displayName)) return;
        if (!_unlockedPoints.TryGetValue(anchorId, out var existing)) return;

        existing.displayName = displayName;
    }

    /// <summary>
    /// FIX INC-023: recalcula el displayName de todos los puntos desbloqueados usando la
    /// localización actual. Necesario porque UnlockPoint() puede ejecutarse muy pronto
    /// (al usar el teletransporte por primera vez, justo cuando se registra el punto de
    /// origen) antes de que LocalizationManager.Instance esté listo; en ese caso el nombre
    /// se cachea con el fallback en inglés (anchorId con "_" por espacios) y se queda así
    /// el resto de la sesión aunque la localización termine de cargar. Al recargar la
    /// partida, LoadFromSaveData vuelve a resolver el nombre y por eso "se arregla solo".
    /// Llamar a esto justo antes de mostrar la UI (que requiere ya varios frames/acciones
    /// del jugador, tiempo de sobra para que LocalizationManager esté listo) corrige el
    /// nombre sin esperar a un recargado de partida.
    /// </summary>
    public static void RefreshAllDisplayNames()
    {
        if (LocalizationManager.Instance == null) return;

        foreach (var point in _unlockedPoints.Values)
        {
            if (point == null || string.IsNullOrEmpty(point.anchorId)) continue;
            point.displayName = GetLocalizedPointName(point.anchorId);
        }
    }

    /// <summary>Guarda los puntos desbloqueados en una lista serializable.</summary>
    public static List<string> ToSaveData()
    {
        return new List<string>(_unlockedPoints.Keys);
    }
    
    /// <summary>Obtiene el nombre localizado de un punto de teletransporte.</summary>
    private static string GetLocalizedPointName(string anchorId)
    {
        // Intentar obtener nombre localizado con clave TELEPORT_POINT_{anchorId}
        string locKey = $"TELEPORT_POINT_{anchorId.ToUpperInvariant()}";
        
        if (LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.Get(locKey, null);
            if (!string.IsNullOrEmpty(localized))
                return localized;
        }
        
        // Fallback: convertir anchorId a formato legible
        // "City_Gate" -> "City Gate"
        return anchorId.Replace("_", " ");
    }
    
    /// <summary>
    /// Sincroniza los puntos desbloqueados con el runtimePreset para persistencia durante gameplay.
    /// Este método debe llamarse cada vez que cambia el estado del registro.
    /// </summary>
    private static void SyncToPreset()
    {
        try
        {
            var preset = GameBootService.Profile?.GetActivePresetResolved();
            if (preset != null)
            {
                preset.unlockedTeleportPoints = ToSaveData();
                Debug.Log($"[TeleportRegistry] 🔄 Sincronizado con preset: {preset.unlockedTeleportPoints.Count} puntos");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[TeleportRegistry] Error sincronizando con preset: {ex.Message}");
        }
    }
}

