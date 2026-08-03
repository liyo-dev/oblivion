using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sendero.Narrative.Editor
{
    /// <summary>
    /// Divide un NarrativeGraph en dos assets según el campo 'chapter' de los nodos.
    ///
    /// Extrae todos los nodos de un capítulo elegido a un NarrativeGraph nuevo, sustituyendo las
    /// aristas que cruzan la frontera por pares RaiseCustomEventNode (grafo origen) /
    /// WaitCustomEventNode (grafo nuevo) — el mismo patrón que ya separa MainNarrative.asset de
    /// Secundary.asset en este proyecto. No es un mecanismo nuevo, solo automatiza un procedimiento
    /// que ya se hizo a mano una vez.
    ///
    /// No toca el YAML del asset directamente: opera sobre la lista de nodos en memoria y deja que
    /// Unity serialice ([SerializeReference]) al guardar, igual que hace NarrativeGraphWindow.
    ///
    /// Lo que NO hace automáticamente (requiere un paso manual, señalado en el reporte):
    ///   - Registrar el grafo nuevo como GraphSlot en el NarrativeGraphHub de Start.unity.
    ///   - Decidir si el StartNode del capítulo nuevo es el correcto cuando hay más de una arista
    ///     de entrada (en ese caso, pide asignarlo a mano con "Set as Start").
    /// </summary>
    public class ChapterSplitWindow : EditorWindow
    {
        private NarrativeGraph _source;
        private string _selectedChapter;
        private string _lastReport = "";
        private Vector2 _scroll;

        [MenuItem("El Sendero/Narrativa/Dividir por Capítulo...")]
        public static void OpenWindow()
        {
            var w = GetWindow<ChapterSplitWindow>();
            w.titleContent = new GUIContent("Dividir por Capítulo");
            w.minSize = new Vector2(440, 360);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Extrae todos los nodos de un capítulo a un NarrativeGraph nuevo. Las aristas que " +
                "cruzan la frontera se sustituyen por un par RaiseCustomEventNode / WaitCustomEventNode " +
                "(el mismo patrón que ya separa MainNarrative de Secundary). Haz commit/backup del " +
                "grafo origen antes de ejecutar — esto modifica el asset original.",
                MessageType.Warning);

            EditorGUILayout.Space();
            var newSource = (NarrativeGraph)EditorGUILayout.ObjectField("Grafo origen", _source, typeof(NarrativeGraph), false);
            if (newSource != _source)
            {
                _source = newSource;
                _selectedChapter = null;
                _lastReport = "";
            }

            if (_source == null)
                return;

            var chapters = CollectChapters(_source);
            if (chapters.Count == 0)
            {
                EditorGUILayout.HelpBox("Este grafo no tiene nodos con 'chapter' asignado.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Capítulo a extraer", EditorStyles.boldLabel);
            foreach (var ch in chapters)
            {
                int count = _source.nodes.Count(n => n != null && n.chapter == ch);
                bool selected = _selectedChapter == ch;
                bool newSelected = EditorGUILayout.ToggleLeft($"{ch}  ({count} nodos)", selected);
                if (newSelected && !selected) _selectedChapter = ch;
            }

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_selectedChapter)))
            {
                if (GUILayout.Button("Extraer capítulo a nuevo grafo", GUILayout.Height(32)))
                {
                    if (EditorUtility.DisplayDialog(
                            "Dividir grafo por capítulo",
                            $"Esto va a modificar '{_source.name}' quitándole los nodos del capítulo " +
                            $"'{_selectedChapter}' y a crear un asset nuevo con esos nodos. ¿Confirmas " +
                            "que has hecho commit/backup antes de continuar?",
                            "Sí, continuar", "Cancelar"))
                    {
                        ExtractChapter(_source, _selectedChapter);
                    }
                }
            }

            if (!string.IsNullOrEmpty(_lastReport))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Resultado", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(240));
                EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
        }

        private static List<string> CollectChapters(NarrativeGraph graph)
        {
            var list = new List<string>();
            foreach (var n in graph.nodes)
            {
                if (n == null || string.IsNullOrWhiteSpace(n.chapter)) continue;
                if (!list.Contains(n.chapter)) list.Add(n.chapter);
            }
            list.Sort(StringComparer.OrdinalIgnoreCase);
            return list;
        }

        private static string SanitizeForIdentifier(string s)
        {
            var clean = new string(s.Where(char.IsLetterOrDigit).ToArray());
            return string.IsNullOrEmpty(clean) ? "Chapter" : clean;
        }

        private void ExtractChapter(NarrativeGraph source, string chapter)
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Extrayendo capítulo '{chapter}' de '{source.name}'...");
            report.AppendLine();

            var moving = source.nodes.Where(n => n != null && n.chapter == chapter).ToList();
            if (moving.Count == 0)
            {
                report.AppendLine("No hay nodos que mover. Cancelado.");
                _lastReport = report.ToString();
                return;
            }

            var movingGuids = new HashSet<string>(moving.Select(n => n.guid));
            string chapterKey = SanitizeForIdentifier(chapter);

            // Crear el nuevo asset junto al origen
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string dir = System.IO.Path.GetDirectoryName(sourcePath);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{dir}/{source.name}_{chapterKey}.asset");

            var dest = ScriptableObject.CreateInstance<NarrativeGraph>();
            AssetDatabase.CreateAsset(dest, newPath);

            Undo.RecordObject(source, "Dividir grafo por capítulo");

            int bridgeCount = 0;

            // 1. Mover los nodos del capítulo elegido al nuevo grafo
            foreach (var n in moving)
                source.nodes.Remove(n);
            dest.nodes.AddRange(moving);

            // 2a. Aristas que van de un nodo que se queda hacia uno que se movió:
            //     insertar RaiseCustomEventNode en origen + WaitCustomEventNode en destino.
            foreach (var stayNode in source.nodes.ToList())
            {
                if (stayNode?.outputs == null) continue;
                for (int i = 0; i < stayNode.outputs.Count; i++)
                {
                    var targetGuid = stayNode.outputs[i];
                    if (string.IsNullOrEmpty(targetGuid) || !movingGuids.Contains(targetGuid)) continue;

                    bridgeCount++;
                    string key = $"CH_{chapterKey}_{bridgeCount}";

                    var raiser = new RaiseCustomEventNode { eventKey = key, chapter = stayNode.chapter };
                    raiser.position = stayNode.position + new Vector2(260, 40 * i);
                    source.nodes.Add(raiser);

                    var waiter = new WaitCustomEventNode { eventKey = key, chapter = chapter };
                    waiter.outputs.Add(targetGuid);
                    waiter.position = new Vector2(-260, 40 * bridgeCount);
                    dest.nodes.Add(waiter);

                    stayNode.outputs[i] = raiser.guid;

                    report.AppendLine($"  Puente: '{stayNode.GetType().Name}' (queda) → evento '{key}' → '{FindNodeTypeName(moving, targetGuid)}' (nuevo grafo)");
                }
            }

            // 2b. Caso simétrico inverso: un nodo movido apunta a uno que se queda (menos común,
            //     p.ej. la historia "vuelve" a un nodo de un capítulo anterior).
            var stayingGuids = new HashSet<string>(source.nodes.Select(n => n.guid));
            foreach (var movedNode in moving.ToList())
            {
                if (movedNode?.outputs == null) continue;
                for (int i = 0; i < movedNode.outputs.Count; i++)
                {
                    var targetGuid = movedNode.outputs[i];
                    if (string.IsNullOrEmpty(targetGuid) || !stayingGuids.Contains(targetGuid)) continue;

                    bridgeCount++;
                    string key = $"CH_{chapterKey}_BACK_{bridgeCount}";

                    var raiser = new RaiseCustomEventNode { eventKey = key, chapter = chapter };
                    raiser.position = movedNode.position + new Vector2(260, 40 * i);
                    dest.nodes.Add(raiser);

                    var targetNode = source.nodes.FirstOrDefault(n => n != null && n.guid == targetGuid);
                    var waiter = new WaitCustomEventNode { eventKey = key, chapter = targetNode?.chapter };
                    waiter.outputs.Add(targetGuid);
                    waiter.position = new Vector2(-260, 40 * bridgeCount);
                    source.nodes.Add(waiter);

                    movedNode.outputs[i] = raiser.guid;

                    report.AppendLine($"  Puente inverso: nodo movido → evento '{key}' → '{targetNode?.GetType().Name}' (vuelve al grafo origen)");
                }
            }

            // 3. Nodo de inicio para el capítulo nuevo
            var hasStartNode = dest.nodes.OfType<StartNode>().Any();
            if (hasStartNode)
            {
                dest.startNodeGuid = dest.nodes.OfType<StartNode>().First().guid;
            }
            else
            {
                var entryPoints = dest.nodes.OfType<WaitCustomEventNode>()
                    .Where(w => w.eventKey != null
                                && w.eventKey.StartsWith($"CH_{chapterKey}_")
                                && !w.eventKey.Contains("_BACK_"))
                    .ToList();

                if (entryPoints.Count == 1)
                {
                    dest.startNodeGuid = entryPoints[0].guid;
                    report.AppendLine($"  StartNode del nuevo grafo: WaitCustomEventNode de entrada ('{entryPoints[0].eventKey}').");
                }
                else
                {
                    report.AppendLine($"  ⚠️ {entryPoints.Count} posibles nodos de entrada para el nuevo grafo — asígnalo a mano " +
                                       "en el editor de grafo con 'Set as Start' antes de registrarlo en NarrativeGraphHub.");
                }
            }

            EditorUtility.SetDirty(source);
            EditorUtility.SetDirty(dest);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.AppendLine();
            report.AppendLine($"Nuevo grafo: {newPath}");
            report.AppendLine($"Nodos movidos: {moving.Count}. Puentes creados: {bridgeCount}.");
            report.AppendLine();

            var sourceValidation = NarrativeGraphValidator.ValidateGraph(source);
            var destValidation = NarrativeGraphValidator.ValidateGraph(dest);

            AppendValidation(report, source.name, sourceValidation);
            AppendValidation(report, dest.name, destValidation);

            report.AppendLine();
            report.AppendLine("Pendiente manual: registrar el grafo nuevo como GraphSlot en el " +
                               "NarrativeGraphHub de Start.unity, y revisar visualmente el resultado " +
                               "(editor de grafo + Narrative Timeline) antes de dar por buena la separación.");

            _lastReport = report.ToString();
            _selectedChapter = null;

            Selection.activeObject = dest;
            EditorGUIUtility.PingObject(dest);
        }

        private static void AppendValidation(System.Text.StringBuilder report, string graphName, NarrativeGraphValidator.ValidationResult validation)
        {
            report.AppendLine($"Validación '{graphName}': {(validation.IsValid ? "OK" : "ERRORES")} — " +
                               $"{validation.Errors.Count} error(es), {validation.Warnings.Count} advertencia(s).");
            foreach (var e in validation.Errors) report.AppendLine($"    ERROR: {e}");
            foreach (var w in validation.Warnings) report.AppendLine($"    aviso: {w}");
        }

        private static string FindNodeTypeName(List<NarrativeNode> moving, string guid)
        {
            var n = moving.FirstOrDefault(x => x != null && x.guid == guid);
            return n != null ? n.GetType().Name : "?";
        }
    }
}
