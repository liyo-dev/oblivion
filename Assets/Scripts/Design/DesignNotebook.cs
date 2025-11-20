using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Design/Notebook", fileName = "DesignNotebook")]
public class DesignNotebook : ScriptableObject
{
    [Header("Resumen general")]
    [TextArea(3, 8)] public string highLevelSynopsis = "";
    [TextArea(3, 8)] public string toneAndGoals = "";

    [Header("Historia principal")]
    public List<StoryBeat> storyBeats = new();

    [Header("Notas enlazadas al grafo narrativo")]
    public List<GraphLinkedNote> graphNotes = new();

    [Header("Notas rápidas de diseño")]
    public List<DesignScratch> quickNotes = new();

    [Header("Ideas de niveles")]
    public List<LevelIdea> levelIdeas = new();

    [Header("Tareas y pendientes")]
    public List<DesignTask> tasks = new();
}

[Serializable]
public class StoryBeat
{
    public string title;
    [TextArea(3, 8)] public string description;
    public string tags;
}

[Serializable]
public class GraphLinkedNote
{
    public string title;
    [TextArea(3, 8)] public string note;
    public NarrativeGraph graph;
    public string nodeGuid;
    public string cachedNodeTitle;
    public string tags;
}

[Serializable]
public class DesignScratch
{
    public string title;
    [TextArea(3, 8)] public string note;
    public string tags;
}

public enum DesignTaskState
{
    Idea,
    ToDo,
    InProgress,
    Blocked,
    Done
}

[Serializable]
public class DesignTask
{
    public string title;
    [TextArea(2, 6)] public string description;
    public DesignTaskState state = DesignTaskState.Idea;
    public string owner;
    public string relatedScene;
}

[Serializable]
public class LevelIdea
{
    public string name;
    [TextArea(2, 6)] public string fantasy;
    [TextArea(2, 6)] public string challenges;
    [TextArea(2, 6)] public string rewards;
    public string tags;
}
