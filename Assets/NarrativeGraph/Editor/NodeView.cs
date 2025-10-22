using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sendero.Narrative.Editor
{
    public class NodeView : Node
    {
        public NarrativeNode Model;
        public Port Input;
        public Port Output;

        private Label _subtitle;

        public NodeView(NarrativeNode model)
        {
            Model = model;

            title = model.GetType().Name;
            SetPosition(new Rect(model.position, new Vector2(300, 200)));

            _subtitle = new Label();
            _subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _subtitle.style.whiteSpace = WhiteSpace.Normal;
            _subtitle.style.marginLeft = 6;
            _subtitle.style.marginBottom = 4;
            _subtitle.style.opacity = 0.9f;
            titleContainer.Add(_subtitle);
            UpdateSubtitle();

            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "In";
            inputContainer.Add(Input);

            Output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            Output.portName = "Next";
            outputContainer.Add(Output);

            var c = ColorForType(model);
            titleContainer.style.backgroundColor = new StyleColor(c);
            titleContainer.style.color = new StyleColor(Color.white);

            RefreshExpandedState();
            RefreshPorts();
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
            return n switch
            {
                StartNode                => new Color(0.15f, 0.55f, 0.95f),
                OfferQuestNode           => new Color(0.20f, 0.70f, 0.40f),
                WaitQuestCompleteNode    => new Color(0.10f, 0.55f, 0.30f),
                DeliverItemProximityNode => new Color(0.85f, 0.60f, 0.20f),
                ActivateGameObjectNode   => new Color(0.75f, 0.35f, 0.35f),
                PlayTimelineNode         => new Color(0.55f, 0.35f, 0.75f),
                WaitBattleWinNode        => new Color(0.90f, 0.30f, 0.30f),
                BranchBoolNode           => new Color(0.25f, 0.25f, 0.25f),
                WaitCustomEventNode      => new Color(0.25f, 0.65f, 0.85f),
                _                        => new Color(0.35f, 0.35f, 0.35f),
            };
        }
    }
}
