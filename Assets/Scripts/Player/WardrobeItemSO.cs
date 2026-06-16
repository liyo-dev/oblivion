using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "El Sendero/Juego/Wardrobe Item", fileName = "WardrobeItem_01")]
public class WardrobeItemSO : ScriptableObject
{
    [SerializeField] private string wardrobeId;
    [SerializeField] private PartCategory category = PartCategory.Body;
    [SerializeField] private string partName;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    static readonly Dictionary<string, WardrobeItemSO> Registry = new Dictionary<string, WardrobeItemSO>();

    public string WardrobeId => string.IsNullOrEmpty(wardrobeId) ? name : wardrobeId;
    public PartCategory Category => category;
    public string PartName => partName;
    public string DisplayName => string.IsNullOrEmpty(displayName) ? partName : displayName;
    public string Description => description;
    public Sprite Icon => icon;

    void OnEnable() => Register();
#if UNITY_EDITOR
    void OnValidate() => Register();
#endif

    void Register()
    {
        if (string.IsNullOrEmpty(partName)) return;
        if (string.IsNullOrEmpty(wardrobeId)) wardrobeId = name;
        Registry[WardrobeId] = this;
    }

    public static WardrobeItemSO Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Registry.TryGetValue(id, out var item);
        return item;
    }
}
