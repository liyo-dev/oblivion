using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class NarrativeNode
{
    // Título visible en la card
    public string displayTitle;

    // Técnicos (ocultos en inspector normal)
    [HideInInspector] public string guid = Guid.NewGuid().ToString();
    [HideInInspector] public Vector2 position;
    [HideInInspector] public List<string> outputs = new();

    public abstract void Enter(NarrativeContext ctx, Action onReadyToAdvance);
    public virtual void Exit(NarrativeContext ctx) {}
}

public sealed class NarrativeContext
{
    public NarrativeGraph Graph;
    public NarrativeRunner Runner;
    public SimpleBlackboard Blackboard;
    public ExposedPropertyTable Exposed;
    public INarrativeSignals Signals;
}