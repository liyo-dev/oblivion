using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class NarrativeNode
{
    public enum PortAnchor
    {
        Left,
        Right,
        Top,
        Bottom
    }

    // Título visible en la card
    public string displayTitle;

    [Tooltip("Si esta marcado, el jugador NO podra guardar partida mientras esta en este nodo narrativo.")]
    public bool blockSaving = false;

    // Campos legacy (ocultos) para mantener compatibilidad con grafos existentes.
    [HideInInspector] public PortAnchor inputAnchor = PortAnchor.Left;
    [HideInInspector] public PortAnchor outputAnchor = PortAnchor.Right;
    [HideInInspector] public bool overrideNodeColor;
    [HideInInspector] public Color nodeColor = new Color(0.35f, 0.35f, 0.35f);

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
