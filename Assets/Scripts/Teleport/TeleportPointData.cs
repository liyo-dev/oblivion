using System;
using UnityEngine;

/// <summary>
/// Datos serializables de un punto de teletransporte desbloqueado.
/// </summary>
[Serializable]
public class TeleportPointData
{
    public string anchorId;
    public string displayName;
    public string sceneName;
    public Sprite icon; // Opcional: icono para mostrar en la UI
    
    public TeleportPointData() { }
    
    public TeleportPointData(string anchorId, string displayName, string sceneName = null)
    {
        this.anchorId = anchorId;
        this.displayName = displayName;
        this.sceneName = sceneName;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is TeleportPointData other)
            return anchorId == other.anchorId;
        return false;
    }
    
    public override int GetHashCode() => anchorId?.GetHashCode() ?? 0;
}

