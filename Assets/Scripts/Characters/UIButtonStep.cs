using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonStep : MonoBehaviour
{
    public CharacterCreatorUI ui;     // asignado por el builder
    public string category;           // "Body", "Hair", etc.
    public int step = +1;             // +1 next, -1 prev

    void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (ui == null) ui = ServiceLocator.Get<CharacterCreatorUI>(false);
            if (ui == null) return;
            ui.Step(category, step);
        });
    }
}