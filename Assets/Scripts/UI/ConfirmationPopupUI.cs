using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Popup de confirmación reutilizable (singleton). Pausa el juego mientras está abierto.
/// Colocar en Start.unity para que sobreviva cambios de escena.
/// </summary>
public class ConfirmationPopupUI : MonoBehaviour
{
    public static ConfirmationPopupUI Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TextMeshProUGUI confirmLabel;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI cancelLabel;

    [Header("Input")]
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar input de confirmar/cancelar. Evita que la misma pulsación que abrió el popup (p.ej. botón Sur del gamepad) lo confirme instantáneamente en el mismo frame.")]
    private float inputGracePeriod = 0.15f;

    private Action _onConfirm;
    private Action _onCancel;
    private bool _isShown;
    private float _savedTimeScale;
    private bool _confirmSelected = false;
    private Coroutine _blinkRoutine;
    private float _shownAt;
    private GameObject _previousSelected;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (panel) panel.SetActive(false);
    }

    public void Show(string message, Action onConfirm, Action onCancel = null)
    {
        if (_isShown) return;

        _onConfirm = onConfirm;
        _onCancel = onCancel;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // Guardamos qué botón tenía el foco antes de abrir el popup para poder
        // restaurar la selección al cerrarlo. Sin esto, el EventSystem se queda
        // sin objeto seleccionado y el menú deja de responder a mando/teclado
        // (INC-047: controles bloqueados tras confirmar/cancelar "Nueva Partida").
        _previousSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        if (messageText) messageText.text = message;

        var loc = LocalizationManager.Instance;
        if (confirmLabel) confirmLabel.text = loc != null ? loc.Get("COMMON_YES", "Sí") : "Sí";
        if (cancelLabel)  cancelLabel.text  = loc != null ? loc.Get("COMMON_NO",  "No") : "No";

        if (confirmButton) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }
        if (cancelButton)  { cancelButton.onClick.RemoveAllListeners();  cancelButton.onClick.AddListener(Cancel);  }

        _isShown = true;
        _confirmSelected = false;
        _shownAt = Time.unscaledTime;
        if (panel) panel.SetActive(true);

        SelectButton(_confirmSelected);
        StartBlink();
    }

    void Update()
    {
        if (!_isShown) return;

        // Ignorar input mientras dure el periodo de gracia: evita que la misma pulsación que
        // abrió el popup (p.ej. botón Sur del gamepad usado para pulsar "Nueva Partida") se lea
        // de nuevo aquí en el mismo frame y confirme el popup antes de que el jugador lo vea.
        if (Time.unscaledTime - _shownAt < inputGracePeriod) return;

#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame)  { Confirm(); return; }
            if (gp.buttonEast.wasPressedThisFrame)   { Cancel();  return; }
            if (gp.dpad.left.wasPressedThisFrame || gp.leftStick.left.wasPressedThisFrame)
                ToggleSelection();
            if (gp.dpad.right.wasPressedThisFrame || gp.leftStick.right.wasPressedThisFrame)
                ToggleSelection();
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Confirm(); return; }
            if (kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (kb.leftArrowKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                ToggleSelection();
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Confirm(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.Tab))
            ToggleSelection();
#endif
    }

    void ToggleSelection()
    {
        _confirmSelected = !_confirmSelected;
        SelectButton(_confirmSelected);
        StopBlink();
        StartBlink();
    }

    void SelectButton(bool confirm)
    {
        var target = confirm ? confirmButton : cancelButton;
        if (target) EventSystem.current?.SetSelectedGameObject(target.gameObject);
    }

    void StartBlink()
    {
        if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
        _blinkRoutine = StartCoroutine(BlinkSelected());
    }

    void StopBlink()
    {
        if (_blinkRoutine != null) { StopCoroutine(_blinkRoutine); _blinkRoutine = null; }
        ResetButtonScale(confirmButton);
        ResetButtonScale(cancelButton);
    }

    void ResetButtonScale(Button btn)
    {
        if (btn) btn.transform.localScale = Vector3.one;
    }

    System.Collections.IEnumerator BlinkSelected()
    {
        float t = 0f;
        const float speed = 4f;
        const float amplitude = 0.07f;
        var btn = _confirmSelected ? confirmButton : cancelButton;
        while (_isShown && btn != null)
        {
            t += Time.unscaledDeltaTime * speed;
            float s = 1f + Mathf.Sin(t) * amplitude;
            btn.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (btn) btn.transform.localScale = Vector3.one;
    }

    void Confirm()
    {
        if (!_isShown) return;
        var cb = _onConfirm;
        Hide();
        cb?.Invoke();
    }

    void Cancel()
    {
        if (!_isShown) return;
        var cb = _onCancel;
        Hide();
        cb?.Invoke();
    }

    void Hide()
    {
        _isShown = false;
        StopBlink();
        Time.timeScale = _savedTimeScale;
        if (panel) panel.SetActive(false);
        if (confirmButton) confirmButton.onClick.RemoveAllListeners();
        if (cancelButton)  cancelButton.onClick.RemoveAllListeners();
        _onConfirm = null;
        _onCancel  = null;

        // Restauramos el foco al botón que lo tenía antes de abrir el popup (p.ej. "Nueva
        // Partida" en el menú principal). Antes se limpiaba con SetSelectedGameObject(null)
        // y nunca se reasignaba, dejando el menú sin objeto seleccionado: con mando/teclado
        // los controles parecían "muertos" al volver (INC-047).
        EventSystem.current?.SetSelectedGameObject(null);
        if (_previousSelected != null && _previousSelected.activeInHierarchy)
        {
            var selectable = _previousSelected.GetComponent<Selectable>();
            if (selectable != null && selectable.IsInteractable())
                EventSystem.current?.SetSelectedGameObject(_previousSelected);
        }
        _previousSelected = null;
    }

    /// <summary>
    /// Descarga la escena aditiva y la vuelve a cargar. Útil para reiniciar un nivel sin destruir Start.unity.
    /// </summary>
    public void ReloadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        StartCoroutine(ReloadSceneRoutine(sceneName));
    }

    private IEnumerator ReloadSceneRoutine(string sceneName)
    {
        var scene = SceneManager.GetSceneByName(sceneName);
        if (scene.isLoaded)
        {
            var unload = SceneManager.UnloadSceneAsync(sceneName);
            while (unload != null && !unload.isDone) yield return null;
        }
        var load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (load != null && !load.isDone) yield return null;
    }
}
