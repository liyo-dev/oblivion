using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro runtime de vínculos sociales dinámicos entre NPCs ("forja" de relaciones).
///
/// Por qué existe aparte de NPCSocialConfig.relationships[]: varios NPCs de relleno
/// comparten el mismo NPCSocialConfig de arquetipo (ej. NPC_Social_Archetype_Friendly.asset,
/// usado a la vez por TownNpc#1, TownNpc#5, TownNpc#10...). Escribir una relación forjada
/// directamente en ese ScriptableObject compartido contaminaría a todos los NPCs que
/// comparten el arquetipo. El estado dinámico vive aquí en su lugar, indexado por el
/// NPCStateContext.RelationshipId de cada NPC (que SÍ es único por instancia, ver
/// NPCBehaviourManagerV2.ResolveRelationshipId()), nunca por el npcId crudo del SO.
///
/// El valor "autor" (relationships[] del SO) sigue siendo la fuente de verdad para
/// relaciones diseñadas a mano (Rival/Enemy, o vínculos ya definidos para NPCs con
/// historia propia) — este registro solo añade una capa de progresión encima:
/// Stranger → Acquaintance → Friend → BestFriend según cuánto hablen dos NPCs entre sí.
/// Nunca promueve relaciones marcadas como Rival/Enemy por diseño.
///
/// Ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § B.3 para el diseño completo.
/// </summary>
public static class NPCRelationshipRegistry
{
    [Serializable]
    public struct SaveEntry
    {
        public string npcIdA;
        public string npcIdB;
        public NPCRelationType type;
        public int encounterCount;
        public float bondScore;
    }

    private struct Bond
    {
        public int encounterCount;
        public float bondScore;
        public NPCRelationType? forgedType; // null = todavía no ha escalado, usar el valor autor
    }

    // Umbrales de bondScore para escalar la relación forjada. Punto de partida, tunable en producción.
    private const float ThresholdAcquaintance = 10f;
    private const float ThresholdFriend = 30f;
    private const float ThresholdBestFriend = 60f;

    private static readonly Dictionary<(string, string), Bond> _bonds = new();

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => _bonds.Clear();
#endif

    private static (string, string) MakeKey(string a, string b)
        => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

    /// <summary>
    /// Se llama SOLO cuando un encuentro social (NPCSocialEncounterState) termina de forma
    /// natural, nunca si se interrumpe por combate/cinemática/interacción a medias.
    /// <paramref name="authoredRelation"/> es la relación con la que arrancó el encuentro
    /// (valor autor del SO, o ya forjada de una vez anterior) — si es Rival o Enemy, no se
    /// acumula vínculo positivo (esas relaciones son fijas, autor-only en v1).
    /// </summary>
    public static NPCRelationType RegisterEncounterCompleted(
        string idA, string idB, NPCRelationType authoredRelation, float avgFriendliness)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB)
            return authoredRelation;

        var key = MakeKey(idA, idB);
        _bonds.TryGetValue(key, out var bond);

        if (authoredRelation != NPCRelationType.Rival && authoredRelation != NPCRelationType.Enemy)
        {
            bond.encounterCount++;
            bond.bondScore += Mathf.Lerp(2f, 8f, Mathf.Clamp01(avgFriendliness));

            if (bond.bondScore >= ThresholdBestFriend) bond.forgedType = NPCRelationType.BestFriend;
            else if (bond.bondScore >= ThresholdFriend) bond.forgedType = NPCRelationType.Friend;
            else if (bond.bondScore >= ThresholdAcquaintance) bond.forgedType = NPCRelationType.Acquaintance;
            // Por debajo del primer umbral: no degradar un forgedType ya alcanzado antes de tiempo.

            _bonds[key] = bond;
        }

        return Resolve(idA, idB, authoredRelation);
    }

    /// <summary>
    /// Relación "efectiva" entre dos NPCs: prioriza el vínculo forjado en runtime;
    /// si no hay ninguno todavía, cae al valor autor (relationships[] del NPCSocialConfig).
    /// </summary>
    public static NPCRelationType Resolve(string idA, string idB, NPCRelationType authoredRelation)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB) || idA == idB)
            return authoredRelation;

        if (_bonds.TryGetValue(MakeKey(idA, idB), out var bond) && bond.forgedType.HasValue)
            return bond.forgedType.Value;

        return authoredRelation;
    }

    /// <summary>Afinidad forjada (0-1) entre dos NPCs, usada por el radar de amistad (WanderState).</summary>
    public static float GetBondScore(string idA, string idB)
    {
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB)) return 0f;
        return _bonds.TryGetValue(MakeKey(idA, idB), out var bond) ? bond.bondScore : 0f;
    }

    public static bool TryGetForgedRelation(string idA, string idB, out NPCRelationType relation)
    {
        relation = NPCRelationType.Stranger;
        if (string.IsNullOrEmpty(idA) || string.IsNullOrEmpty(idB)) return false;
        if (!_bonds.TryGetValue(MakeKey(idA, idB), out var bond) || !bond.forgedType.HasValue) return false;
        relation = bond.forgedType.Value;
        return true;
    }

    // ── Persistencia (ver Diseno_Refugio_Lluvia_y_Relaciones_NPC.md § B.5) ──────────────────

    public static List<SaveEntry> ToSaveEntries()
    {
        var list = new List<SaveEntry>(_bonds.Count);
        foreach (var kvp in _bonds)
        {
            if (!kvp.Value.forgedType.HasValue && kvp.Value.encounterCount <= 0) continue;
            list.Add(new SaveEntry
            {
                npcIdA = kvp.Key.Item1,
                npcIdB = kvp.Key.Item2,
                type = kvp.Value.forgedType ?? NPCRelationType.Stranger,
                encounterCount = kvp.Value.encounterCount,
                bondScore = kvp.Value.bondScore,
            });
        }
        return list;
    }

    public static void LoadFromSaveEntries(List<SaveEntry> entries)
    {
        _bonds.Clear();
        if (entries == null) return;

        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.npcIdA) || string.IsNullOrEmpty(e.npcIdB)) continue;
            _bonds[MakeKey(e.npcIdA, e.npcIdB)] = new Bond
            {
                encounterCount = e.encounterCount,
                bondScore = e.bondScore,
                forgedType = e.type == NPCRelationType.Stranger ? (NPCRelationType?)null : e.type,
            };
        }
    }
}
