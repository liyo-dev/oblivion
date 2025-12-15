using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSetNone : MonoBehaviour
{
    public CharacterCreatorUI ui;
    public string category;

    void Awake()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(() =>
        {
            if (ui == null) ui = ServiceLocator.Get<CharacterCreatorUI>(false);
            if (ui == null) return;
            ui.SetNone(category);
        });
    }
}