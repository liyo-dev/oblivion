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
        if (panel) panel.SetActive(true);

        // Forzar selección del botón confirmar para que el estado Selected sea visible
        if (confirmButton) EventSystem.current?.SetSelectedGameObject(confirmButton.gameObject);
    }

    void Update()
    {
        if (!_isShown) return;

#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.buttonSouth.wasPressedThisFrame) { Confirm(); return; }
            if (gp.buttonEast.wasPressedThisFrame)  { Cancel();  return; }
        }
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { Confirm(); return; }
            if (kb.escapeKey.wasPressedThisFrame) { Cancel(); return; }
        }
#else
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) { Confirm(); return; }
        if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
#endif
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
