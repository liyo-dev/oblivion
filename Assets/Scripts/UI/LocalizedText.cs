using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// FIX: antes exigía TextMeshProUGUI (RequireComponent) y solo escribía en _tmp.text, por lo que
// no podía usarse en ningún GameObject con el componente legacy UnityEngine.UI.Text (Text (Legacy)).
// Varios menús (ej: SettingsMenu.prefab) usan Text legacy para sus etiquetas estáticas, así que
// esas etiquetas nunca podían localizarse pese a tener este script disponible. Ahora detecta cuál
// de los dos componentes de texto tiene el GameObject y escribe en el que corresponda.
//
// FIX: si este componente ejecutaba su Awake() antes de que LocalizationManager.Instance existiera
// (objetos instanciados en tiempo de ejecución, orden de Awake entre escenas, etc.), Refresh() salía
// sin hacer nada Y, además, el "if (LocalizationManager.Instance != null)" que seguía tampoco se
// cumplía — así que el texto se quedaba PARA SIEMPRE con el placeholder tecleado en el editor
// (normalmente en inglés) y jamás se suscribía a OnLocaleChanged, ni siquiera después de que el
// manager terminara de cargar. Esto explicaba literales en inglés sueltos en menús cuyo resto de
// textos sí estaba traducido. Ahora, si el manager todavía no existe, esperamos con una corrutina
// hasta que aparezca y entonces sí refrescamos y nos suscribimos.
public class LocalizedText : MonoBehaviour
{
    public string key;
    private TextMeshProUGUI _tmp;
    private Text _legacyText;
    private Coroutine _waitRoutine;
    private bool _subscribed;

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

        Bind();
    }

    void OnEnable()
    {
        // Por si el objeto se reactiva y aún no logramos suscribirnos (manager no listo en Awake).
        if (!_subscribed && (_tmp != null || _legacyText != null))
            Bind();
    }

    void Bind()
    {
        if (LocalizationManager.Instance != null)
        {
            Refresh();
            Subscribe();
            return;
        }

        // El manager todavía no ha hecho su Awake(); esperar en vez de rendirnos con el texto
        // placeholder del editor.
        if (_waitRoutine == null)
            _waitRoutine = StartCoroutine(WaitForManager());
    }

    IEnumerator WaitForManager()
    {
        while (LocalizationManager.Instance == null)
            yield return null;

        _waitRoutine = null;
        Refresh();
        Subscribe();
    }

    void Subscribe()
    {
        if (_subscribed || LocalizationManager.Instance == null)
            return;
        LocalizationManager.Instance.OnLocaleChanged += Refresh;
        _subscribed = true;
    }

    void OnDisable()
    {
        if (_waitRoutine != null)
        {
            StopCoroutine(_waitRoutine);
            _waitRoutine = null;
        }
    }

    void OnDestroy()
    {
        if (_subscribed && LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLocaleChanged -= Refresh;
        _subscribed = false;
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
