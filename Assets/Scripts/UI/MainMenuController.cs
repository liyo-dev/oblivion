using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using DG.Tweening;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuController : MonoBehaviour
{
    [Header("Refs")]
    private SaveSystem _saveSystem;
    public Button continueButton;

    [Header("World Scene")]
    public string worldScene = "MainWorld";

    [Header("Fade override (opcional)")]
    public EasyTransition.TransitionSettings fadeOverride;
    [Min(0)] public float fadeDelay; // sin valor por defecto redundante

    [Header("UI / Animación (DOTween)")]
    public CanvasGroup rootGroup;                         // si está vacío, se añade
    public List<RectTransform> animatedItems = new();     // si está vacío, se auto-rellena
    [Min(0f)] public float introDelay = 0.05f;
    [Min(0f)] public float introStagger = 0.04f;
    [Min(0f)] public float introDuration = 0.35f;
    public float introYOffset = 40f;

    // interno
    EventSystem _es;
    GameObject _defaultSelection;
    Sequence _introSeq;

    void Awake()
    {
        Application.runInBackground = true; // ayuda en editor/foco
        _es = EventSystem.current;

        if (!rootGroup)
        {
            rootGroup = GetComponent<CanvasGroup>();
            if (!rootGroup) rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Asegurar lista no nula
        animatedItems ??= new List<RectTransform>();

        if (animatedItems.Count == 0)
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
                    if (rt != null) animatedItems.Add(rt);
                }
            }
#pragma warning restore 300
        }

        // Selección por defecto robusta
        GameObject fallback = null;
        if (animatedItems != null)
        {
            for (int i = 0; i < animatedItems.Count; i++)
            {
                var rt = animatedItems[i];
                if (rt != null) { fallback = rt.gameObject; break; }
            }
        }
        _defaultSelection = continueButton ? continueButton.gameObject : fallback;
    }

    void OnEnable()
    {
        // Al volver al menú, asegúrate de que hay selección
        EnsureUISelection();
    }

    void OnDisable()
    {
        // Liberar secuencia para evitar fugas si se desactiva el GO
        _introSeq?.Kill();
        _introSeq = null;
    }

    void Start()
    {
        _saveSystem = FindFirstObjectByType<SaveSystem>();
        bool hasSave = (_saveSystem != null) && _saveSystem.HasSave();
        if (continueButton != null)
            continueButton.interactable = hasSave;

        EnsureUISelection();
        PlayIntro();
    }

    void Update()
    {
        KeepUIFocusForGamepad();
    }

    // ---------------- UI Focus Keeper ----------------
#pragma warning disable 300
    void EnsureUISelection()
    {
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

    // ---------------- DOTween Intro ----------------
    void PlayIntro()
    {
        // Estado inicial
        rootGroup.alpha = 0f;
        _introSeq?.Kill();
        _introSeq = DOTween.Sequence().SetUpdate(true);

        // Fade-in del root con DOTween.To (evita ambigüedad de DOFade con CanvasGroup)
        _introSeq.AppendInterval(introDelay);
        _introSeq.Append(
            DOTween.To(() => rootGroup.alpha, a => rootGroup.alpha = a, 1f, 0.2f)
        );

        // Slide + fade de cada item con stagger
        float delayAcc = 0f;
        foreach (var rt in animatedItems)
        {
            if (!rt) continue;

            Vector2 finalPos = rt.anchoredPosition;
            rt.anchoredPosition = finalPos + new Vector2(0f, -introYOffset);

            var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            _introSeq.Insert(introDelay + delayAcc, rt.DOAnchorPos(finalPos, introDuration).SetEase(Ease.OutCubic));
            _introSeq.Insert(introDelay + delayAcc, cg.DOFade(1f, introDuration * 0.9f));

            delayAcc += introStagger;
        }
    }

    // ---------------- Botones ----------------
    public void OnNewGame()
    {
        // Reset seguro: borra save y aplica preset por defecto en el runtimePreset
        GameBootService.NewGameReset();
        // Cargar la escena inicial (prólogo o equivalente)
        Load("Prologo");
    }

    public void OnContinue()
    {
        if (_saveSystem != null && _saveSystem.HasSave())
        {
            // Asegura que el runtimePreset refleja el save actual (por si el servicio persiste)
            if (GameBootService.IsAvailable)
            {
                GameBootService.Profile?.LoadProfile(_saveSystem);
            }
            Load(worldScene);
        }
        else
        {
            OnNewGame();
        }
    }

    void Load(string sceneName)
    {
        if (fadeOverride != null)
            SceneTransitionLoader.Load(sceneName, fadeOverride, fadeDelay);
        else
            SceneTransitionLoader.Load(sceneName);
    }
}
