using System;
using UnityEngine;

[Serializable]
public class GraphNoteNode : NarrativeNode
{
    [TextArea(3, 12)] public string note;
    public Color accent = new Color(1f, 0.93f, 0.55f);

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Las notas no bloquean la ejecución narrativa.
        onReadyToAdvance?.Invoke();
    }
}
