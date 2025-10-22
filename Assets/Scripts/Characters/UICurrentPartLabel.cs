using UnityEngine;
using UnityEngine.UI;

public class UICurrentPartLabel : MonoBehaviour
{
    public ModularAutoBuilder builder;
    public string category; // "Body","Hair","Hat","OHS"...

    Text _txt;
    PartCategory _cat;

    void Awake()
    {
        _txt = GetComponent<Text>();
        if (!_txt) _txt = gameObject.AddComponent<Text>();
        if (System.Enum.TryParse(category, true, out _cat) == false) enabled = false;
    }

    void Update()
    {
        if (!builder || !_txt) return;
        var opts = builder.GetOptions(_cat);
        var sel  = builder.GetSelection();
        if (sel.TryGetValue(_cat, out var name))
            _txt.text = name;
        else
            _txt.text = "None";
    }
}