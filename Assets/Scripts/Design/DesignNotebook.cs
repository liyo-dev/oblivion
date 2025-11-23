using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Design/Notebook", fileName = "DesignNotebook")]
public class DesignNotebook : ScriptableObject
{
    [Header("Resumen general")]
    [TextArea(3, 8)] public string highLevelSynopsis = "";

    [Header("Historia principal")]
    public List<DesignStoryCard> storyCards = new();
    public List<DesignStoryLink> storyLinks = new();

    [Header("Notas rápidas de diseño")]
    public List<DesignScratch> quickNotes = new();
}

[Serializable]
public class DesignStoryCard
{
    public string guid = Guid.NewGuid().ToString();
    public string title;
    [TextArea(3, 8)] public string note;
    public Color color = Color.white;
    public Vector2 position;
}

[Serializable]
public class DesignStoryLink
{
    public string fromGuid;
    public string toGuid;
}

[Serializable]
public class DesignScratch
{
    public string title;
    [TextArea(3, 8)] public string note;
    public string tags;
    public Color color = new Color(1f, 0.95f, 0.65f);
    public Vector2 position;
}
