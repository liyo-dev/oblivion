using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro runtime de NPCs "ambientales" actualmente activos en la escena, indexados por su
/// NPCStateContext.RelationshipId. Permite consultas espaciales baratas (sobre los NPCs activos,
/// no sobre toda la escena, sin FindObjectOfType) — usado por el "radar de amistad" en
/// WanderState.OnEnter para que un NPC pueda buscar activamente a un Friend/BestFriend conocido
/// en vez de solo toparse con quien pase cerca por azar.
///
/// Se registra/desregistra en NPCBehaviourManagerV2.OnEnable/OnDisable, así que un NPC oculto
/// (ej. refugiado dentro de una casa durante la lluvia, ver SeekShelterState) desaparece
/// automáticamente del radar mientras está desactivado.
///
/// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § B.6.1.
/// </summary>
public static class NPCAmbientRegistry
{
    private static readonly Dictionary<string, Game.NPC.NPCBehaviourManagerV2> _byId = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _byId.Clear();
#endif

    public static void Register(string relationshipId, Game.NPC.NPCBehaviourManagerV2 npc)
    {
        if (string.IsNullOrEmpty(relationshipId) || npc == null) return;
        _byId[relationshipId] = npc;
    }

    public static void Unregister(string relationshipId, Game.NPC.NPCBehaviourManagerV2 npc)
    {
        if (string.IsNullOrEmpty(relationshipId)) return;
        if (_byId.TryGetValue(relationshipId, out var existing) && existing == npc)
            _byId.Remove(relationshipId);
    }

    /// <summary>
    /// Busca, entre los NPCs activos, un Friend/BestFriend forjado más cercano a una posición
    /// dentro de un radio dado, que además esté libre para socializar ahora mismo. Devuelve null
    /// si no hay ninguno. NPCRelationshipRegistry solo forja Acquaintance/Friend/BestFriend
    /// (nunca Rival/Enemy, ver RegisterEncounterCompleted), así que no hace falta filtrarlos aquí.
    /// </summary>
    public static Game.NPC.NPCBehaviourManagerV2 FindNearbyFriend(
        string selfId, Vector3 position, float maxDist, NPCRelationType minRelation = NPCRelationType.Friend)
    {
        if (string.IsNullOrEmpty(selfId)) return null;

        Game.NPC.NPCBehaviourManagerV2 best = null;
        float bestSqr = maxDist * maxDist;

        foreach (var kvp in _byId)
        {
            if (kvp.Key == selfId) continue;

            var candidate = kvp.Value;
            if (candidate == null) continue;

            var candidateContext = candidate.Context;
            if (candidateContext == null) continue;
            if (candidateContext.IsInCombat || candidateContext.IsInCinematic ||
                candidateContext.IsInteracting || candidateContext.WasDefeatedInCombat) continue;

            if (!NPCRelationshipRegistry.TryGetForgedRelation(selfId, kvp.Key, out var relation)) continue;
            if (relation < minRelation) continue;

            float sqr = (candidate.transform.position - position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Devuelve, en <paramref name="results"/> (limpiada primero), todos los NPCs ambientales
    /// activos dentro de <paramref name="radius"/> de <paramref name="position"/>, sin filtrar por
    /// relación ni estado. Pensado para sistemas que necesitan "quién anda cerca ahora mismo" sin
    /// FindObjectsByType — por ejemplo <c>DialogueCinematicController</c>, que congela temporalmente
    /// a los NPCs ambientales cercanos a una conversación cinematográfica para que no crucen el
    /// encuadre caminando. Reutiliza la lista del caller, sin allocation.
    /// </summary>
    public static void GetActiveNPCsInRadius(Vector3 position, float radius, List<Game.NPC.NPCBehaviourManagerV2> results)
    {
        results.Clear();
        float sqrRadius = radius * radius;

        foreach (var kvp in _byId)
        {
            var candidate = kvp.Value;
            if (candidate == null) continue;

            float sqr = (candidate.transform.position - position).sqrMagnitude;
            if (sqr <= sqrRadius)
                results.Add(candidate);
        }
    }
}
