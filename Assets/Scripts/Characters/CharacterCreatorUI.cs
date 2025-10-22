using UnityEngine;

public class CharacterCreatorUI : MonoBehaviour
{
    public ModularAutoBuilder builder;               // asigna MC01
    public RowSelectionHighlighter highlighter;      // asigna Panel (opcional, para saber fila actual)

    // Orden de las categorías según cómo se construyen en CharacterCreatorCanvasBuilder
    static readonly string[] CategoryOrder = {
        "Body","Cloak","Head","Hair","Eyes","Mouth","Hat","Eyebrow","Accessory", // Panel_Left (0-8)
        "OHS","Shield","Bow"  // Panel_Right (9-11)
    };

    PartCategory Parse(string category)
    {
        // Categorías vienen como "Body","Cloak",... exactas desde el CanvasBuilder
        if (System.Enum.TryParse(category, out PartCategory cat)) return cat;
        Debug.LogWarning($"[UI] Categoría desconocida: {category}");
        return PartCategory.Body;
    }

    public void Step(string category, int step)
    {
        var cat = Parse(category);
        if (step >= 0) builder.Next(cat, +1);
        else builder.Prev(cat);
    }

    public void SetNone(string category)
    {
        builder.SetByName(Parse(category), null);
    }

    public void RandomizeAll()
    {
        builder.RandomizeAll();
    }

    // Helpers para el GamepadController
    public PartCategory CurrentHighlightedCategory()
    {
        if (!highlighter) return PartCategory.Body;
        
        int index = highlighter.SelectedIndex;
        if (index >= 0 && index < CategoryOrder.Length)
            return Parse(CategoryOrder[index]);
        
        return PartCategory.Body;
    }
}