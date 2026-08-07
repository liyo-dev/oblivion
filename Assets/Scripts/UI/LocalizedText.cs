using TMPro;
using UnityEngine;
using UnityEngine.UI;

// FIX: antes exigía TextMeshProUGUI (RequireComponent) y solo escribía en _tmp.text, por lo que
// no podía usarse en ningún GameObject con el componente legacy UnityEngine.UI.Text (Text (Legacy)).
// Varios menús (ej: SettingsMenu.prefab) usan Text legacy para sus etiquetas estáticas, así que
// esas etiquetas nunca podían localizarse pese a tener este script disponible. Ahora detecta cuál
// de los dos componentes de texto tiene el GameObject y escribe en el que corresponda.
public class LocalizedText : MonoBehaviour
{
    public string key;
    private TextMeshProUGUI _tmp;
    private Text _legacyText;

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        if (_tmp == null)
            _legacyText = GetComponent<Text>();

        if (_tmp == null && _legacyText == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[LocalizedText] '{name}' no tiene TextMeshProUGUI ni Text (Legacy). Clave '{key}' no se aplicará.");
#endif
            return;
        }

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
        if (_tmp != null)
            _tmp.text = LocalizationManager.Instance.Get(key, _tmp.text);
        else if (_legacyText != null)
            _legacyText.text = LocalizationManager.Instance.Get(key, _legacyText.text);
    }
}