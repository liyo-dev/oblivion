using UnityEngine;

/// <summary>
/// Utilidad estática para seleccionar el objetivo de combate.
/// El jugador es SIEMPRE el objetivo. Estela y Liam son soporte y nunca reciben daño.
/// </summary>
public static class CombatTargetProvider
{
    /// <summary>
    /// Devuelve siempre al jugador como objetivo primario.
    /// Los enemigos persiguen y atacan únicamente al jugador en todo momento.
    /// </summary>
    public static Transform GetNearestTarget(Vector3 fromPosition)
    {
        if (PlayerService.TryGetPlayer(out var playerGO) && playerGO != null)
            return playerGO.transform;
        return null;
    }
}
