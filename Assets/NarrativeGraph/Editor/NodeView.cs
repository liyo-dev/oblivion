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
            { typeof(NpcAutoMoveNode),          new Color(0.48f, 0.71f, 0.82f) },
            { typeof(PlayTimelineNode),         new Color(0.63f, 0.40f, 0.84f) },
            { typeof(PlayCinematicNode),        new Color(0.55f, 0.42f, 0.86f) },
            { typeof(PlayVoiceNode),            new Color(0.42f, 0.36f, 0.82f) },
            { typeof(PlayMusicNode),            new Color(0.32f, 0.64f, 0.82f) },
            { typeof(StopMusicNode),            new Color(0.28f, 0.36f, 0.65f) },
            { typeof(PlaySfxNode),              new Color(0.35f, 0.60f, 0.78f) },
            { typeof(AdditiveSceneCinematicNode), new Color(0.66f, 0.42f, 0.84f) },
            { typeof(GraphNoteNode),            new Color(1.00f, 0.91f, 0.56f) }
        };

        static readonly Dictionary<Type, Color> ColorCache = new();

        public NarrativeNode Model;
        public Port Input;
        public Port Output;

        private readonly Label _subtitle;

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

            _subtitle = new Label();
            _subtitle.AddToClassList("narrative-node__subtitle");
            titleContainer.Add(_subtitle);
            UpdateSubtitle();

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

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            Model.position = newPos.position;
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
