using System;

/// <summary>
/// Utilidades compartidas para comparar identificadores de party member entre los distintos
/// formatos usados en el proyecto (PersistenceId largo, nombre del GameObject, DisplayName).
///
/// Antes vivía duplicada de forma literal en QuestManager.cs y en
/// Behaviour NPC/Modules/NPCQuestConfig.cs (misma lógica, copiada a mano). Ambos archivos delegan
/// ahora aquí para que un cambio en el formato de IDs no pueda divergir silenciosamente entre las
/// dos copias.
/// </summary>
public static class QuestMatchingUtils
{
    private const string NarrativeIdPrefix = "NPC_InteractiveNarrative_Config_";

    /// <summary>
    /// Extrae el nombre base de un ID con formato "NPC_InteractiveNarrative_Config_&lt;Nombre&gt;_&lt;hash&gt;".
    /// Si el ID no sigue ese patrón devuelve null.
    /// </summary>
    public static string ExtractNarrativeBaseName(string id)
    {
        if (string.IsNullOrEmpty(id) || !id.StartsWith(NarrativeIdPrefix, StringComparison.Ordinal))
            return null;
        string withoutPrefix = id.Substring(NarrativeIdPrefix.Length);
        int lastUnderscore   = withoutPrefix.LastIndexOf('_');
        return lastUnderscore > 0 ? withoutPrefix.Substring(0, lastUnderscore) : withoutPrefix;
    }

    /// <summary>
    /// Compara un miembro del party con un memberId tolerando los distintos formatos de ID.
    /// Los IDs pueden ser:
    ///   - El PersistenceId completo del NPCBehaviourManagerV2 en el GO ("NPC_InteractiveNarrative_Config_Estela_b17a2d68")
    ///   - El nombre del GameObject ("Estela")
    ///   - El DisplayName del partyMember ("Estela")
    ///
    /// Cuando el memberId sigue el formato "NPC_InteractiveNarrative_Config_&lt;Nombre&gt;_&lt;hash&gt;",
    /// también se extrae el nombre base y se compara con el nombre del GO y el DisplayName.
    /// </summary>
    public static bool IsPartyMemberMatch(Game.NPC.NPCPartyMember member, string memberId)
    {
        if (member == null || string.IsNullOrEmpty(memberId)) return false;

        string persistenceId = member.NPCManager?.PersistenceId ?? "";
        string goName        = member.gameObject.name;
        string displayName   = member.DisplayName ?? "";

        // 1. Coincidencia exacta con persistenceId o nombre del GO
        if (persistenceId == memberId || goName == memberId) return true;

        // 2. Coincidencia con DisplayName (ej: "Estela")
        if (!string.IsNullOrEmpty(displayName) && displayName == memberId) return true;

        // 3. El memberId sigue el patrón largo: extraer nombre base y comparar con goName/displayName
        string memberIdBase = ExtractNarrativeBaseName(memberId);
        if (memberIdBase != null)
        {
            if (string.Equals(goName, memberIdBase, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(displayName, memberIdBase, StringComparison.OrdinalIgnoreCase)) return true;
        }

        // 4. El persistenceId sigue el patrón largo y el memberId es el nombre corto
        string persistenceBase = ExtractNarrativeBaseName(persistenceId);
        if (persistenceBase != null &&
            string.Equals(memberId, persistenceBase, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
