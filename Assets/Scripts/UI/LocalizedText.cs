using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    private TextMeshProUGUI _tmp;

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        Refresh();
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLocaleChanged += Refresh;
    }

    void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLocaleChanged -= Refresh;
    }

    public void Refresh()
    {
        if (LocalizationManager.Instance == null) return;
        _tmp.text = LocalizationManager.Instance.Get(key, _tmp.text);
    }
}