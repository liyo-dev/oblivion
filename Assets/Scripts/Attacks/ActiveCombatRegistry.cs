using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Registro estático de NPCs actualmente en combate.
/// Permite búsqueda rápida sin necesidad de buscar componentes.
/// </summary>
public static class ActiveCombatRegistry
{
    private static readonly HashSet<GameObject> _npcsInCombat = new HashSet<GameObject>();

    // Petición de Raúl (1 sep 2026): enemigos "menores" (arañas, de momento) que sí deben contar
    // como combate activo (Battle Mode, ciclo manual L1/R1, etc.) pero NO deben hacer que la
    // cámara/objetivo se enganchen solos al caminar cerca de ellos — ver
    // CombatCameraTargeting.TryAutoLock/OnEnterCombat/OnNPCEnteredCombat, que usan
    // GetClosestCameraLockableCombatNPC en vez de GetClosestCombatNPC para las decisiones de
    // auto-lock. El marcador de apuntado para hechizos sigue funcionando igual vía el auto-scan
    // normal de PlayerTargeting (Targetable.isInActiveCombat), que no pasa por aquí.
    private static readonly HashSet<GameObject> _cameraLockExempt = new HashSet<GameObject>();
    
    /// <summary>
    /// Se dispara cuando un NPC entra en combate (útil para que los compañeros reaccionen)
    /// </summary>
    public static event Action<GameObject> OnNPCEnteredCombat;
    
    /// <summary>
    /// Se dispara cuando un NPC sale del combate
    /// </summary>
    public static event Action<GameObject> OnNPCExitedCombat;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _npcsInCombat.Clear();
        _cameraLockExempt.Clear();
        OnNPCEnteredCombat = null;
        OnNPCExitedCombat = null;
    }
    #endif
    
    /// <summary>
    /// Registra un NPC como "en combate"
    /// </summary>
    public static void RegisterNPC(GameObject npc, bool allowsCameraLock = true)
    {
        if (npc == null) return;

        if (!allowsCameraLock)
            _cameraLockExempt.Add(npc);
        else
            _cameraLockExempt.Remove(npc);

        if (_npcsInCombat.Add(npc))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActiveCombatRegistry] ⚔️ NPC '{npc.name}' registrado en combate{(allowsCameraLock ? "" : " (sin auto-lock de cámara)")}");
#endif
            OnNPCEnteredCombat?.Invoke(npc);
        }
    }

    /// <summary>
    /// Quita un NPC del registro de combate
    /// </summary>
    public static void UnregisterNPC(GameObject npc)
    {
        if (npc == null) return;

        _cameraLockExempt.Remove(npc);

        if (_npcsInCombat.Remove(npc))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActiveCombatRegistry] 🏳️ NPC '{npc.name}' removido del combate");
#endif
            OnNPCExitedCombat?.Invoke(npc);
        }
    }
    
    /// <summary>
    /// Verifica si un NPC está actualmente en combate
    /// </summary>
    public static bool IsInCombat(GameObject npc)
    {
        return npc != null && _npcsInCombat.Contains(npc);
    }

    /// <summary>
    /// True si el NPC está registrado como "exento de auto-lock de cámara" (ver
    /// RegisterNPC(npc, allowsCameraLock: false)) — p.ej. arañas y otros enemigos menores.
    /// </summary>
    public static bool IsCameraLockExempt(GameObject npc)
    {
        return npc != null && _cameraLockExempt.Contains(npc);
    }

    /// <summary>
    /// Verifica si existe al menos un combate activo distinto al NPC indicado.
    /// Útil para bloquear que un NPC externo inicie un segundo combate.
    /// </summary>
    public static bool HasActiveCombatExcluding(GameObject npc)
    {
        CleanupDestroyedNPCs();

        foreach (var combatNpc in _npcsInCombat)
        {
            if (combatNpc == null)
                continue;
            if (combatNpc == npc)
                continue;
            return true;
        }

        return false;
    }
    
    /// <summary>
    /// Obtiene el NPC en combate más cercano a una posición
    /// </summary>
    public static GameObject GetClosestCombatNPC(Vector3 position, float maxDistance = 50f)
    {
        // ✅ FIX #16 (auditoría combate, 15 ago 2026): antes había ~5 Debug.Log por NPC evaluado,
        // sin gatear, en un método pensado para poder llamarse con cierta frecuencia.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ActiveCombatRegistry] GetClosestCombatNPC: Buscando entre {_npcsInCombat.Count} NPCs (pos={position}, maxDist={maxDistance}m)");
#endif

        GameObject closest = null;
        float closestDist = maxDistance;
        int nullCount = 0;

        foreach (var npc in _npcsInCombat)
        {
            if (npc == null)
            {
                nullCount++;
                continue; // NPC destruido
            }

            float dist = Vector3.Distance(npc.transform.position, position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = npc;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (nullCount > 0)
            Debug.LogWarning($"[ActiveCombatRegistry] ⚠️ {nullCount} NPCs null en el registro (destruidos)");
        Debug.Log($"[ActiveCombatRegistry] Resultado: {(closest != null ? $"'{closest.name}' a {closestDist:F1}m" : "NINGUNO")}");
#endif

        return closest;
    }

    /// <summary>
    /// Igual que <see cref="GetClosestCombatNPC"/> pero ignorando los NPCs marcados como
    /// "exentos de auto-lock de cámara" (ver RegisterNPC(npc, allowsCameraLock: false)) — usado
    /// por CombatCameraTargeting para decidir cuándo enganchar la cámara automáticamente sin
    /// perder de vista a esos NPCs para otros propósitos (Battle Mode, ciclo manual L1/R1...).
    /// </summary>
    public static GameObject GetClosestCameraLockableCombatNPC(Vector3 position, float maxDistance = 50f)
    {
        GameObject closest = null;
        float closestDist = maxDistance;

        foreach (var npc in _npcsInCombat)
        {
            if (npc == null) continue;
            if (_cameraLockExempt.Contains(npc)) continue;

            float dist = Vector3.Distance(npc.transform.position, position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = npc;
            }
        }

        return closest;
    }

    /// <summary>
    /// Obtiene una lista de TODOS los NPCs actualmente en combate.
    /// Aloca una List nueva cada llamada — evitar desde Update/bucles de búsqueda periódica.
    /// Para esos casos usar <see cref="GetAllInCombatNonAlloc"/> con un buffer reutilizado.
    /// </summary>
    public static List<GameObject> GetAllInCombat()
    {
        // Limpiar NPCs destruidos primero
        _npcsInCombat.RemoveWhere(npc => npc == null);
        return new List<GameObject>(_npcsInCombat);
    }

    /// <summary>
    /// ✅ FIX #17 (auditoría combate, 15 ago 2026): variante sin allocation de GetAllInCombat,
    /// para llamadores guiados por Update/cooldown periódico (p.ej. AllyCombatState.FindNearestEnemy)
    /// que antes copiaban el HashSet completo a una List nueva cada vez. El buffer lo mantiene y
    /// reutiliza el llamador.
    /// </summary>
    public static void GetAllInCombatNonAlloc(List<GameObject> buffer)
    {
        if (buffer == null) return;
        _npcsInCombat.RemoveWhere(npc => npc == null);
        buffer.Clear();
        buffer.AddRange(_npcsInCombat);
    }
    
    /// <summary>
    /// Limpia NPCs destruidos del registro
    /// </summary>
    public static void CleanupDestroyedNPCs()
    {
        _npcsInCombat.RemoveWhere(npc => npc == null);
    }
    
    /// <summary>
    /// Limpia todo el registro (útil al cambiar de escena)
    /// </summary>
    public static void ClearAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ActiveCombatRegistry] 🧹 Limpiando {_npcsInCombat.Count} NPCs del registro");
#endif
        _npcsInCombat.Clear();
    }
    
    /// <summary>
    /// Debug: Cantidad de NPCs en combate.
    /// FIX A1 (auditoría 2026-08-07): antes devolvía el tamaño crudo del HashSet, que puede
    /// contener referencias "fake-null" a NPCs destruidos sin pasar por UnregisterNPC (Destroy
    /// directo, descarga de escena aditiva — ClearAll solo se llama en GameOver). Consumidores
    /// como PlayerBattleModeController.DetectEnemiesNearby() y PlayerActionManager leían
    /// Count > 0 directamente: un enemigo destruido así dejaba Count > 0 para siempre → Battle
    /// Mode y ActionMode.Combat permanentes (que además bloqueaba Interact). Limpiar aquí, en el
    /// único punto de lectura, corrige los tres consumidores de una vez sin tener que auditar
    /// cada OnDestroy de NPC.
    /// </summary>
    public static int Count
    {
        get
        {
            CleanupDestroyedNPCs();
            return _npcsInCombat.Count;
        }
    }
}
