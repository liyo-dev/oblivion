using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    public class NodeView : Node
    {
        static readonly Dictionary<Type, Color> ExplicitPalette = new()
        {
            { typeof(StartNode),                new Color(0.11f, 0.58f, 0.95f) },
            { typeof(StartQuestNode),           new Color(0.23f, 0.74f, 0.47f) },
            { typeof(OfferQuestNode),           new Color(0.13f, 0.64f, 0.38f) },
            { typeof(WaitQuestCompleteNode),    new Color(0.09f, 0.49f, 0.28f) },
            { typeof(DeliverQuestCompleteNode), new Color(0.55f, 0.36f, 0.10f) },
            { typeof(CompleteQuestStepsNode),   new Color(0.32f, 0.68f, 0.45f) },
            { typeof(DeliverItemProximityNode), new Color(0.85f, 0.60f, 0.20f) },
            { typeof(UnlockAbilitiesNode),      new Color(0.90f, 0.48f, 0.18f) },
            { typeof(UnlockTriggerNode),        new Color(0.90f, 0.52f, 0.22f) },
            { typeof(GiveInventoryItemNode),    new Color(0.92f, 0.65f, 0.30f) },
            { typeof(RequireInventoryItemNode), new Color(0.78f, 0.47f, 0.20f) },
            { typeof(WaitCustomEventNode),      new Color(0.13f, 0.64f, 0.80f) },
            { typeof(WaitBattleWinNode),        new Color(0.88f, 0.33f, 0.33f) },
            { typeof(StartBattleNode),          new Color(0.96f, 0.38f, 0.38f) },
            { typeof(BranchBoolNode),           new Color(0.78f, 0.51f, 0.15f) },
            { typeof(ActivateGameObjectNode),   new Color(0.90f, 0.56f, 0.20f) },
            { typeof(PlayTimelineNode),         new Color(0.63f, 0.40f, 0.84f) },
            { typeof(PlayCinematicNode),        new Color(0.55f, 0.42f, 0.86f) },
            { typeof(PlayVoiceNode),            new Color(0.42f, 0.36f, 0.82f) },
            { typeof(PlayMusicNode),            new Color(0.32f, 0.64f, 0.82f) },
            { typeof(StopMusicNode),            new Color(0.28f, 0.36f, 0.65f) },
            { typeof(PlaySfxNode),              new Color(0.35f, 0.60f, 0.78f) },
            { typeof(AdditiveSceneCinematicNode), new Color(0.66f, 0.42f, 0.84f) },
            { typeof(GraphNoteNode),            new Color(1.00f, 0.91f, 0.56f) },
            { typeof(PlayDialogueNode),         new Color(0.20f, 0.70f, 0.82f) },
            { typeof(WaitNPCInteractionNode),   new Color(0.85f, 0.55f, 0.10f) },
            { typeof(NPCCommandNode),           new Color(0.75f, 0.42f, 0.15f) },
            { typeof(StartCombatNode),          new Color(0.85f, 0.20f, 0.20f) },
            { typeof(TeleportPlayerNode),       new Color(0.45f, 0.30f, 0.80f) }
        };

        // Paleta de colores para capítulos
        static readonly Color[] ChapterColors =
        {
            new Color(0.20f, 0.60f, 0.95f),  // azul
            new Color(0.18f, 0.78f, 0.45f),  // verde
            new Color(0.90f, 0.55f, 0.20f),  // naranja
            new Color(0.75f, 0.35f, 0.85f),  // púrpura
            new Color(0.95f, 0.40f, 0.40f),  // rojo
            new Color(0.25f, 0.75f, 0.75f),  // teal
            new Color(0.90f, 0.75f, 0.20f),  // amarillo
            new Color(0.55f, 0.45f, 0.80f),  // lavanda
        };

        static readonly Dictionary<string, Color> ChapterColorCache = new();

        static readonly Dictionary<Type, Color> ColorCache = new();

        public NarrativeNode Model;
        public Port Input;
        public Port Output;

        private readonly Label _subtitle;
        private readonly Label _chapterBadge;
        private readonly VisualElement _chapterStripe;

        public NodeView(NarrativeNode model)
        {
            if (model == null)
            {
                Debug.LogError("NodeView: Cannot create view for null node model");
                return;
            }

            Model = model;

            var isNote = model is GraphNoteNode;
            title = isNote ? "Nota" : model.GetType().Name;
            AddToClassList("narrative-node");
            mainContainer.style.position = Position.Relative;
            titleContainer.AddToClassList("narrative-node__title-bar");
            inputContainer.AddToClassList("narrative-port-dock");
            outputContainer.AddToClassList("narrative-port-dock");
            outputContainer.AddToClassList("narrative-port-dock--right");
            extensionContainer.AddToClassList("narrative-node__extension");

            var defaultSize = isNote ? new Vector2(260, 180) : new Vector2(320, 220);
            SetPosition(new Rect(model.position, defaultSize));

            // Barra lateral de color por capítulo
            _chapterStripe = new VisualElement();
            _chapterStripe.AddToClassList("narrative-node__chapter-stripe");
            mainContainer.Add(_chapterStripe);

            _subtitle = new Label();
            _subtitle.AddToClassList("narrative-node__subtitle");
            titleContainer.Add(_subtitle);
            UpdateSubtitle();

            // Badge de capítulo en el título
            _chapterBadge = new Label();
            _chapterBadge.AddToClassList("narrative-node__chapter-badge");
            titleContainer.Add(_chapterBadge);
            UpdateChapterBadge();

            if (!isNote)
            {
                Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                Input.portName = "";
                Input.AddToClassList("narrative-port");
                Input.AddToClassList("narrative-port--left");
                inputContainer.Add(Input);

                Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                Output.portName = "";
                Output.AddToClassList("narrative-port");
                Output.AddToClassList("narrative-port--right");
                outputContainer.Add(Output);
            }

            ApplyColorTheme(model);

            RefreshExpandedState();
            RefreshPorts();
        }

        void ApplyColorTheme(NarrativeNode node)
        {
            var color = ColorForType(node);
            var dark = Color.Lerp(color, Color.black, 0.25f);
            titleContainer.style.backgroundColor = new StyleColor(color);
            mainContainer.style.borderTopColor = new StyleColor(dark);
            mainContainer.style.borderLeftColor = new StyleColor(dark);
            mainContainer.style.borderRightColor = new StyleColor(new Color(dark.r, dark.g, dark.b, 0.65f));
            mainContainer.style.borderBottomColor = new StyleColor(new Color(dark.r, dark.g, dark.b, 0.35f));
            titleContainer.style.color = new StyleColor(Color.white);

            if (node is GraphNoteNode note)
            {
                var accent = note.accent;
                mainContainer.style.backgroundColor = new StyleColor(new Color(accent.r, accent.g, accent.b, 0.25f));
                titleContainer.style.color = new StyleColor(new Color(0.18f, 0.15f, 0.05f));
            }
        }

        public void UpdateSubtitle()
        {
            _subtitle.text = string.IsNullOrWhiteSpace(Model.displayTitle) ? "" : Model.displayTitle;
        }

        public void UpdateChapterBadge()
        {
            if (_chapterBadge == null || _chapterStripe == null) return;

            var ch = Model.chapter;
            if (string.IsNullOrWhiteSpace(ch))
            {
                _chapterBadge.style.display = DisplayStyle.None;
                _chapterStripe.style.display = DisplayStyle.None;
                return;
            }

            _chapterBadge.text = ch;
            _chapterBadge.style.display = DisplayStyle.Flex;
            _chapterStripe.style.display = DisplayStyle.Flex;

            var color = GetChapterColor(ch);
            _chapterBadge.style.backgroundColor = new StyleColor(new Color(color.r, color.g, color.b, 0.85f));
            _chapterBadge.style.color = new StyleColor(Color.white);
            _chapterStripe.style.backgroundColor = new StyleColor(color);
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Model.position = newPos.position;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            base.BuildContextualMenu(evt);

            // Find the parent graph for Quick Test
            evt.menu.AppendAction("Quick Test desde aquí", _ =>
            {
                // Walk up to find the NarrativeGraphWindow and get the graph
                var windows = Resources.FindObjectsOfTypeAll<NarrativeGraphWindow>();
                NarrativeGraph graph = null;
                foreach (var w in windows)
                {
                    var field = typeof(NarrativeGraphWindow).GetField("_graph",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        graph = field.GetValue(w) as NarrativeGraph;
                        if (graph != null) break;
                    }
                }

                if (graph != null)
                    NarrativeQuickTestWindow.OpenWithNode(graph, Model);
                else
                    Debug.LogWarning("[NodeView] No se pudo encontrar el grafo para Quick Test");
            });
        }

        static Color GetChapterColor(string chapter)
        {
            if (string.IsNullOrWhiteSpace(chapter))
                return new Color(0.5f, 0.5f, 0.5f);

            if (ChapterColorCache.TryGetValue(chapter, out var cached))
                return cached;

            int hash = 0;
            for (int i = 0; i < chapter.Length; i++)
                hash = (hash * 31) + chapter[i];

            var color = ChapterColors[Mathf.Abs(hash) % ChapterColors.Length];
            ChapterColorCache[chapter] = color;
            return color;
        }

        Color ColorForType(NarrativeNode n)
        {
            if (n == null) return new Color(0.35f, 0.35f, 0.35f);

            var type = n.GetType();
            if (ColorCache.TryGetValue(type, out var cached))
                return cached;

            if (ExplicitPalette.TryGetValue(type, out var paletteColor))
            {
                ColorCache[type] = paletteColor;
                return paletteColor;
            }

            ColorCache[type] = GenerateColorFromType(type);
            return ColorCache[type];
        }

        static Color GenerateColorFromType(Type t)
        {
            var name = t.FullName ?? t.Name;
            int hash = 0;
            for (int i = 0; i < name.Length; i++)
                hash = (hash * 31) + name[i];

            var hue = Mathf.Abs(hash % 360) / 360f;
            var color = Color.HSVToRGB(hue, 0.45f, 0.88f);
            color.a = 1f;
            return color;
        }
    }
}
