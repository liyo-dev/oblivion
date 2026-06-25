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

    private Action _onConfirm;
    private Action _onCancel;
    private bool _isShown;
    private float _savedTimeScale;
    private bool _confirmSelected = true;
    private Coroutine _blinkRoutine;

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

        if (messageText) messageText.text = message;

        var loc = LocalizationManager.Instance;
        if (confirmLabel) confirmLabel.text = loc != null ? loc.Get("COMMON_YES", "Sí") : "Sí";
        if (cancelLabel)  cancelLabel.text  = loc != null ? loc.Get("COMMON_NO",  "No") : "No";

        if (confirmButton) { confirmButton.onClick.RemoveAllListeners(); confirmButton.onClick.AddListener(Confirm); }
        if (cancelButton)  { cancelButton.onClick.RemoveAllListeners();  cancelButton.onClick.AddListener(Cancel);  }

        _isShown = true;
        _confirmSelected = true;
        if (panel) panel.SetActive(true);

        SelectButton(_confirmSelected);
        StartBlink();
    }

    void Update()
    {
        if (!_isShown) return;

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
        EventSystem.current?.SetSelectedGameObject(null);
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
