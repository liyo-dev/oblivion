using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Catálogo central de "hechos" narrativos. Un hecho es cualquier variable
/// de estado que afecta al flujo de la historia: flags, quest states,
/// eventos recibidos, variables de blackboard, etc.
///
/// Este ScriptableObject NO reemplaza los sistemas existentes — es una capa
/// de documentación y consulta centralizada. Los sistemas runtime siguen
/// siendo la fuente de verdad; el catálogo simplemente los documenta y
/// proporciona metadatos para el editor.
///
/// Crear: Assets → Create → Narrative → Fact Catalog
/// </summary>
[CreateAssetMenu(menuName = "El Sendero/Narrativa/Fact Catalog", fileName = "NarrativeFactCatalog")]
public class NarrativeFactCatalog : ScriptableObject
{
    public enum FactCategory
    {
        Narrative,
        Quest,
        Event,
        Ability,
        Inventory,
        Flag,
        World,
        NPC,
        Audio,
        Custom
    }

    public enum FactType
    {
        Bool,
        Int,
        Float,
        String
    }

    public enum FactSource
    {
        Blackboard,
        QuestManager,
        Signals,
        PlayerState,
        Custom
    }

    [Serializable]
    public class FactDefinition
    {
        [Tooltip("ID único del hecho (ej: QUEST_MISSION1_COMPLETED, EVT_MOUNTAIN, flag_canSwim)")]
        public string factId;

        [Tooltip("Nombre legible para mostrar en el editor")]
        public string displayName;

        public FactCategory category;
        public FactType factType;
        public FactSource source;

        [TextArea(1, 3)]
        [Tooltip("Descripción del hecho: qué representa, cuándo cambia, quién lo usa")]
        public string description;

        [Tooltip("Tags opcionales para agrupar/filtrar (ej: 'cap1', 'erika', 'taberna')")]
        public List<string> tags = new List<string>();
    }

    [Header("Definiciones de hechos")]
    [Tooltip("Lista de hechos documentados. Usa el Fact Browser para auto-generar.")]
    public List<FactDefinition> definitions = new List<FactDefinition>();

    /// <summary>Busca una definición por su factId.</summary>
    public FactDefinition FindDefinition(string factId)
    {
        if (string.IsNullOrEmpty(factId)) return null;
        return definitions.Find(d => d != null && d.factId == factId);
    }

    /// <summary>Devuelve todas las definiciones de una categoría.</summary>
    public List<FactDefinition> GetByCategory(FactCategory category)
    {
        return definitions.FindAll(d => d != null && d.category == category);
    }

    /// <summary>Devuelve todas las definiciones con un tag específico.</summary>
    public List<FactDefinition> GetByTag(string tag)
    {
        if (string.IsNullOrEmpty(tag)) return new List<FactDefinition>();
        return definitions.FindAll(d => d != null && d.tags != null && d.tags.Contains(tag));
    }
}
