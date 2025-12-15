using UnityEngine;
using UnityEngine.UI;

public class UITextCurrentPart : MonoBehaviour
{
    public ModularAutoBuilder builder;
    public string category;

    Text _txt;

    void Awake()
    {
        _txt = GetComponent<Text>();
        if (builder == null) builder = ServiceLocator.Get<ModularAutoBuilder>(false);
    }

    void OnEnable()  => Refresh();
    void Update()    => Refresh();

    void Refresh()
    {
        if (_txt == null || builder == null) return;
        
        // Mapeo de nombres de UI a enum
        PartCategory cat;
        switch (category)
        {
            case "OHS": cat = PartCategory.Ohs; break;
            case "Shield": cat = PartCategory.ShieldR; break;
            default:
                if (!System.Enum.TryParse(category, out cat))
                {
                    _txt.text = "-";
                    return;
                }
                break;
        }

        var sel = builder.GetSelection();
        _txt.text = sel.TryGetValue(cat, out var name) ? name : "None";
    }
}