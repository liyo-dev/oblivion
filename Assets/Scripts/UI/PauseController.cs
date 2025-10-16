using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PauseController : MonoBehaviour
{
    [Header("Refs")]
    public Button resumeButton;
    public Button optionsButton;
    public Button quitToMainButton;

    [Header("Scene")]
    public string mainMenuScene = "MainMenu";

    [Header("UI")]
    public CanvasGroup rootGroup;
    public List<RectTransform> selectableItems = new();

    // internos
    EventSystem _es;
    GameObject _defaultSelection;

    void Awake()
    {
        _es = EventSystem.current;

        if (!rootGroup)
        {
            rootGroup = GetComponent<CanvasGroup>();
            if (!rootGroup) rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        selectableItems ??= new List<RectTransform>();

        if (selectableItems.Count == 0)
        {
#pragma warning disable 300
            var selectables = GetComponentsInChildren<Selectable>(true);
            if (selectables != null)
            {
                for (int i = 0; i < selectables.Length; i++)
                {
                    var s = selectables[i];
                    if (ReferenceEquals(s, null)) continue;
                    var tr = s.transform;
                    if (ReferenceEquals(tr, null)) continue;
                    var rt = tr as RectTransform;
                    if (rt != null) selectableItems.Add(rt);
                }
            }
#pragma warning restore 300
        }

        // Default selection prefer resumeButton
        GameObject fallback = null;
        if (selectableItems != null)
        {
            for (int i = 0; i < selectableItems.Count; i++)
            {
                var rt = selectableItems[i];
                if (rt != null) { fallback = rt.gameObject; break; }
            }
        }
        _defaultSelection = resumeButton ? resumeButton.gameObject : fallback;
    }

    void OnEnable()
    {
        // Pausar juego
        Time.timeScale = 0f;

        // Mostrar cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureUISelection();
    }

    void OnDisable()
    {
        // Reanudar juego
        Time.timeScale = 1f;

        // opcional: ocultar cursor si tu juego lo requiere (no forzar aquí)
        if (_es != null && _es.currentSelectedGameObject == gameObject)
            _es.SetSelectedGameObject(null);
    }

    void Update()
    {
        KeepUIFocusForGamepad();

        // Cerrar pausa con tecla/botón de cancelar
#if ENABLE_INPUT_SYSTEM
        bool cancel = false;
        if (Gamepad.current != null)
        {
            cancel = Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame;
        }
        else if (Keyboard.current != null)
        {
            cancel = Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#else
        bool cancel = Input.GetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape);
#endif
        if (cancel)
            Resume();
    }

    // ---------------- UI Focus Keeper ----------------
#pragma warning disable 300
    void EnsureUISelection()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;

        if (_es.currentSelectedGameObject == null || !_es.currentSelectedGameObject.activeInHierarchy)
        {
            var toSelect = _defaultSelection;
            if (toSelect == null)
            {
                var firstSel = GetComponentInChildren<Selectable>(true);
                if (!ReferenceEquals(firstSel, null)) toSelect = firstSel.gameObject;
            }
            if (toSelect != null)
            {
                _defaultSelection = toSelect;
                _es.SetSelectedGameObject(toSelect);
            }
        }
    }
#pragma warning restore 300

    void KeepUIFocusForGamepad()
    {
        if (_es == null) _es = EventSystem.current;
        if (_es == null) return;

        bool wantsPadFocus = false;

#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            var gp = Gamepad.current;
            wantsPadFocus =
                gp.dpad.up.wasPressedThisFrame || gp.dpad.down.wasPressedThisFrame ||
                Mathf.Abs(gp.leftStick.ReadValue().y) > 0.25f ||
                gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
        }
        else
        {
            wantsPadFocus = Keyboard.current != null && (
                Keyboard.current.upArrowKey.wasPressedThisFrame ||
                Keyboard.current.downArrowKey.wasPressedThisFrame ||
                Keyboard.current.wKey.wasPressedThisFrame ||
                Keyboard.current.sKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame
            );
        }
#else
        wantsPadFocus = Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f || Input.GetButtonDown("Submit");
#endif

        if (wantsPadFocus && (_es.currentSelectedGameObject == null || !_es.currentSelectedGameObject.activeInHierarchy))
            EnsureUISelection();
    }

    // ---------------- Botones ----------------
    public void Resume()
    {
        gameObject.SetActive(false);
    }

    public void OnOptions()
    {
        // Implementar apertura de opciones (activar otro panel / escena)
        // Por ahora solo placeholder
        if (optionsButton != null)
        {
            // ejemplo: abrir panel de opciones hijo, si existe
            var optionsPanel = transform.Find("OptionsPanel");
            if (optionsPanel != null) optionsPanel.gameObject.SetActive(true);
        }
    }

    public void OnQuitToMain()
    {
        // Asegurar reanudado antes de cambiar de escena
        Time.timeScale = 1f;
        // Usar SceneTransitionLoader si existe en proyecto, si no, fallback a SceneManager
        var loaderType = System.Type.GetType("SceneTransitionLoader, Assembly-CSharp");
        if (loaderType != null)
        {
            // Llamada dinámica a SceneTransitionLoader.Load(string) para evitar dependencia dura
            var method = loaderType.GetMethod("Load", new System.Type[] { typeof(string) });
            method?.Invoke(null, new object[] { mainMenuScene });
        }
        else
        {
            SceneManager.LoadScene(mainMenuScene);
        }
    }
}
