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

    [Header("Tablero unificado")]
    public Color boardBackground = new Color(0.07f, 0.08f, 0.1f);
    public List<DesignDocumentNote> documents = new();

    [Header("Notas rápidas de diseño")]
    public List<DesignScratch> quickNotes = new();

    [Header("Pizarra de ideas")]
    public List<DesignBlackboardStroke> blackboardStrokes = new();
    public Color blackboardBackground = new Color(0.07f, 0.08f, 0.1f);
    public Color blackboardBrushColor = Color.white;
    public float blackboardBrushSize = 5f;
}

[Serializable]
public class DesignStoryCard
{
    public string guid = Guid.NewGuid().ToString();
    public string title;
    [TextArea(3, 8)] public string note;
    public Color color = Color.white;
    public FontStyle titleFontStyle = FontStyle.Bold;
    public int titleFontSize = 14;
    public bool settingsExpanded = true;
    public Vector2 position;
    public Vector2 size = new Vector2(320f, 420f);
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

[Serializable]
public class DesignDocumentNote
{
    public string guid = Guid.NewGuid().ToString();
    public string title = "Nuevo documento";
    [TextArea(3, 12)] public string body = "Describe la escena, sinopsis o referencias.";
    public Color color = new Color(0.16f, 0.21f, 0.28f);
    public Vector2 position;
    public Vector2 size = new Vector2(360f, 520f);
}

[Serializable]
public class DesignBlackboardStroke
{
    public Color color = Color.white;
    public float thickness = 4f;
    public List<Vector2> points = new();
}
