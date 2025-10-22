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
        if (builder == null) builder = FindFirstObjectByType<ModularAutoBuilder>();
    }

    void OnEnable()  => Refresh();
    void Update()    => Refresh();

    void Refresh()
    {
        if (_txt == null || builder == null) return;
        if (!System.Enum.TryParse(category, out PartCategory cat))
        {
            _txt.text = "-";
            return;
        }

        var sel = builder.GetSelection();
        _txt.text = sel.TryGetValue(cat, out var name) ? name : "None";
    }
}