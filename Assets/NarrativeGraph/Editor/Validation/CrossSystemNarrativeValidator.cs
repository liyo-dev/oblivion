using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Game.NPC.Modules;
using GraphAsset = global::NarrativeGraph;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Validación cruzada entre los dos sistemas narrativos que conviven en el proyecto:
    /// el grafo (NarrativeGraph/NarrativeRunner) y el sistema legacy "Interactive"
    /// (NPCQuestConfig + NPCInteractiveNarrativeConfig/ConditionalNarrative/NarrativeCondition).
    ///
    /// No fusiona ni migra nada — solo avisa cuando la MISMA quest o el MISMO evento custom
    /// está siendo referenciado de forma independiente por ambos sistemas, que es exactamente
    /// el patrón que causó el bug real INC-020 (consumo duplicado de ítems de quest en dos
    /// sitios que no se conocían entre sí). Pensada para correr manualmente antes de una
    /// entrega, no en cada carga de escena (recorre todos los assets del proyecto vía
    /// AssetDatabase, es editor-only).
    /// </summary>
    public static class CrossSystemNarrativeValidator
    {
        [MenuItem("El Sendero/Narrativa/Validar Interactive vs Grafo (proyecto completo)")]
        public static void RunFromMenu()
        {
            var result = Validate();
            result.LogResults("Proyecto completo — Interactive vs Grafo");

            EditorUtility.DisplayDialog(
                "Validación cruzada Interactive ↔ Grafo",
                result.Errors.Count == 0 && result.Warnings.Count == 0
                    ? "No se encontraron quests ni eventos custom compartidos entre el sistema Interactive y el grafo narrativo."
                    : $"{result.Errors.Count} error(es), {result.Warnings.Count} advertencia(s).\nVer la Consola para el detalle.",
                "OK");
        }

        public static NarrativeGraphValidator.ValidationResult Validate()
        {
            var result = new NarrativeGraphValidator.ValidationResult();

            // ── 1. Qué toca el GRAFO ────────────────────────────────────────────
            var graphQuestRefs = new Dictionary<string, List<string>>();
            var graphEventsWaited = new Dictionary<string, List<string>>();
            var graphEventsRaised = new Dictionary<string, List<string>>();

            foreach (var guid in AssetDatabase.FindAssets("t:NarrativeGraph"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var graph = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
                if (graph == null || graph.nodes == null) continue;

                foreach (var node in graph.nodes)
                {
                    if (node == null) continue;
                    var type = node.GetType();

                    // Cualquier tipo de nodo con un campo público "questId" (StartQuestNode,
                    // CompleteQuestStepsNode, WaitQuestCompleteNode, OfferQuestNode,
                    // RequireInventoryItemNode, DeliverQuestCompleteNode, y cualquiera futuro).
                    var questIdField = type.GetField("questId", BindingFlags.Public | BindingFlags.Instance);
                    if (questIdField != null && questIdField.FieldType == typeof(string))
                    {
                        var questId = questIdField.GetValue(node) as string;
                        if (!string.IsNullOrEmpty(questId))
                            Add(graphQuestRefs, questId, $"{graph.name}:{type.Name}");
                    }

                    if (node is WaitCustomEventNode wait && !string.IsNullOrEmpty(wait.eventKey))
                        Add(graphEventsWaited, wait.eventKey, graph.name);

                    if (node is RaiseCustomEventNode raise && !string.IsNullOrEmpty(raise.eventKey))
                        Add(graphEventsRaised, raise.eventKey, graph.name);
                }
            }

            // ── 2. Qué toca el sistema INTERACTIVE / QuestConfig legacy ────────
            var legacyQuestRefs = new Dictionary<string, List<string>>();
            var legacyEventRefs = new Dictionary<string, List<string>>();

            foreach (var guid in AssetDatabase.FindAssets("t:NPCQuestConfig"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<NPCQuestConfig>(path);
                if (cfg == null || cfg.questChain == null) continue;

                foreach (var entry in cfg.questChain)
                {
                    if (entry?.questData == null || string.IsNullOrEmpty(entry.questData.questId)) continue;
                    Add(legacyQuestRefs, entry.questData.questId, $"{cfg.name}.questChain");
                }
            }

            foreach (var guid in AssetDatabase.FindAssets("t:NPCInteractiveNarrativeConfig"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var cfg = AssetDatabase.LoadAssetAtPath<NPCInteractiveNarrativeConfig>(path);
                if (cfg == null || cfg.conditionalNarratives == null) continue;

                foreach (var cn in cfg.conditionalNarratives)
                {
                    if (cn == null) continue;

                    if (cn.condition?.targetQuest != null && !string.IsNullOrEmpty(cn.condition.targetQuest.questId))
                        Add(legacyQuestRefs, cn.condition.targetQuest.questId, $"{cfg.name}.condition.targetQuest");

                    if (cn.condition != null
                        && cn.condition.conditionType == NarrativeConditionType.Custom
                        && !string.IsNullOrEmpty(cn.condition.customEventKey))
                        Add(legacyEventRefs, cn.condition.customEventKey, $"{cfg.name}.condition.customEventKey (escucha)");

                    if (cn.sendNarrativeEvent && !string.IsNullOrEmpty(cn.narrativeEventKey))
                        Add(legacyEventRefs, cn.narrativeEventKey, $"{cfg.name}.narrativeEventKey (emite)");

                    if (cn.narrativeChain == null) continue;
                    foreach (var chainEntry in cn.narrativeChain)
                    {
                        if (chainEntry == null) continue;
                        if (chainEntry.actionType == NarrativeActionType.StartQuest
                            && chainEntry.questToStart != null
                            && !string.IsNullOrEmpty(chainEntry.questToStart.questId))
                            Add(legacyQuestRefs, chainEntry.questToStart.questId, $"{cfg.name}.narrativeChain.questToStart");
                    }
                }
            }

            // ── 3. Cruce: misma quest referenciada por ambos mundos ────────────
            foreach (var kvp in graphQuestRefs)
            {
                if (!legacyQuestRefs.TryGetValue(kvp.Key, out var legacyRefs)) continue;

                result.Warnings.Add(
                    $"Quest '{kvp.Key}' referenciada tanto por el grafo narrativo ({string.Join(", ", kvp.Value)}) " +
                    $"como por el sistema Interactive/QuestConfig ({string.Join(", ", legacyRefs)}). " +
                    "No es necesariamente un error, pero confirma que ambos no compiten por iniciar/completar " +
                    "la misma quest sin saberlo el uno del otro (mismo patrón que INC-020).");
            }

            // ── 4. Cruce: mismo evento custom esperado por el grafo y tocado por legacy ─
            foreach (var kvp in graphEventsWaited)
            {
                if (!legacyEventRefs.TryGetValue(kvp.Key, out var legacyRefs)) continue;

                result.Warnings.Add(
                    $"Evento custom '{kvp.Key}' esperado por WaitCustomEventNode en [{string.Join(", ", kvp.Value)}] " +
                    $"y también usado por el sistema Interactive ({string.Join(", ", legacyRefs)}). " +
                    "Verifica que no son dos reacciones independientes al mismo disparo.");
            }

            // Aviso informativo (no error): eventos que el grafo emite pero que nadie en el
            // propio grafo espera Y que tampoco escucha el sistema Interactive — puede ser
            // intencional (p.ej. para un sistema externo) pero vale la pena revisarlo una vez.
            foreach (var kvp in graphEventsRaised)
            {
                bool waitedInGraph = graphEventsWaited.ContainsKey(kvp.Key);
                bool listenedInLegacy = legacyEventRefs.ContainsKey(kvp.Key);
                if (!waitedInGraph && !listenedInLegacy)
                {
                    result.Warnings.Add(
                        $"Evento custom '{kvp.Key}' se emite desde RaiseCustomEventNode en [{string.Join(", ", kvp.Value)}] " +
                        "pero ningún WaitCustomEventNode del grafo ni ninguna NarrativeCondition del sistema Interactive " +
                        "lo espera. Puede ser intencional; si no, es un evento huérfano.");
                }
            }

            return result;
        }

        private static void Add(Dictionary<string, List<string>> dict, string key, string value)
        {
            if (!dict.TryGetValue(key, out var list))
                dict[key] = list = new List<string>();
            list.Add(value);
        }
    }
}
