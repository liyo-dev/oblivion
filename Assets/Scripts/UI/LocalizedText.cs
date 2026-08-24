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
//
// FIX (24 ago 2026 — botones del Main Menu saliendo en mayúsculas): `_key` no llevaba
// [SerializeField], así que cuando un builder de Editor (MainMenuCreditsExitButtonsBuilder,
// MainMenuPatchNotesBugReportBuilder) hacía `loc.key = "MainMenu_Credits"` sobre un objeto YA
// GUARDADO EN LA ESCENA y luego EditorSceneManager.SaveScene(), esa asignación vivía solo en el
// objeto en memoria de esa sesión del Editor — Unity nunca la escribía en el .unity porque un campo
// privado sin [SerializeField] no se serializa. Al volver a cargar la escena (Play Mode o build), el
// componente deserializado tenía `_key` vacío otra vez, Awake() no llamaba a Bind() (ve la clave
// vacía) y Refresh() nunca sobreescribía el texto — así que el placeholder tecleado a mano por el
// builder (p. ej. "CRÉDITOS", "SALIR", "NOTAS DEL PARCHE", "REPORTAR UN FALLO", todo en mayúsculas
// porque el propio builder los escribía así asumiendo que la localización los pisaría enseguida) se
// quedaba fijo para siempre, mientras que los botones más antiguos del menú (Continuar, Configuración,
// Controles...) ya tenían el placeholder tecleado en el caso correcto desde el principio y por eso el
// bug no se notaba ahí — pero el mismo problema de fondo (la clave nunca sobrevive a un guardado de
// escena) afecta a CUALQUIER LocalizedText baked en una escena, no solo a estos 4 botones: el cambio
// de idioma ES/EN tampoco se estaba reaplicando de verdad tras recargar la escena. Con `_key` ahora
// serializado, la clave que un builder de Editor asigna sí persiste en el .unity, y Refresh() vuelve
// a funcionar para todos los textos localizados de la escena, no solo para los creados en runtime.
public class LocalizedText : MonoBehaviour
{
    // FIX: antes `key` era un campo público simple. Como AddComponent<T>() ejecuta Awake() de forma
    // SÍNCRONA antes de que el código llamante pueda asignar `.key = "..."` (el patrón
    // `go.AddComponent<LocalizedText>().key = "Mi_Clave"`, usado en varios paneles de UI —
    // PatchNotesFlyoutPanel, BugReportFlyoutPanel), Awake() siempre veía key == null. Eso hacía que
    // LocalizationManager.Get() reventara con ArgumentNullException al llamar
    // Dictionary.TryGetValue(null) (Dictionary no admite claves null). Y como la excepción
    // interrumpía Bind() antes de llegar a Subscribe(), la etiqueta se quedaba para siempre sin
    // suscribirse a OnLocaleChanged aunque la clave se asignara justo después — no solo reventaba el
    // log, el texto tampoco se volvía a traducir nunca al cambiar de idioma. Ahora `key` es una
    // propiedad: si se asigna después de Awake(), relanza el bind/subscribe con la clave ya
    // disponible; si Awake() corre con la clave todavía vacía, simplemente no hace nada hasta que
    // llegue una clave válida. El backing field sigue siendo [SerializeField] (ver FIX de arriba) para
    // que una clave asignada por un builder de Editor sobreviva al guardado de la escena — la
    // propiedad sigue haciendo falta tal cual para el caso de AddComponent<T>() en runtime.
    public string key
    {
        get => _key;
        set
        {
            _key = value;
            if (_awoken && !string.IsNullOrEmpty(_key) && (_tmp != null || _legacyText != null))
                Bind();
        }
    }

    [SerializeField] private string _key;
    private TextMeshProUGUI _tmp;
    private Text _legacyText;
    private Coroutine _waitRoutine;
    private bool _subscribed;
    private bool _awoken;

    void Awake()
    {
        _tmp = GetComponent<TextMeshProUGUI>();
        if (_tmp == null)
            _legacyText = GetComponent<Text>();

        _awoken = true;

        if (_tmp == null && _legacyText == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[LocalizedText] '{name}' no tiene TextMeshProUGUI ni Text (Legacy). Clave '{key}' no se aplicará.");
#endif
            return;
        }

        if (!string.IsNullOrEmpty(_key))
            Bind();
    }

    void OnEnable()
    {
        // Por si el objeto se reactiva y aún no logramos suscribirnos (manager no listo en Awake).
        if (!_subscribed && !string.IsNullOrEmpty(_key) && (_tmp != null || _legacyText != null))
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
        if (string.IsNullOrEmpty(_key)) return; // sin clave todavía: conservar el texto actual (placeholder)
        if (_tmp != null)
            _tmp.text = LocalizationManager.Instance.Get(key, _tmp.text);
        else if (_legacyText != null)
            _legacyText.text = LocalizationManager.Instance.Get(key, _legacyText.text);
    }
}
