using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MainMenuController : MonoBehaviour
{
    [Header("Refs")]
    private SaveSystem _saveSystem;
    public Button continueButton;                 // puede ser null si no la usas

    [Header("World Scene")]
    public string worldScene = "MainWorld";

    [Header("Fade override (opcional)")]
    public EasyTransition.TransitionSettings fadeOverride;
    [Min(0)] public float fadeDelay;

    [Header("UI / Animación (DOTween)")]
    public CanvasGroup rootGroup;                 // si está vacío, se añade
    public RectTransform[] animatedItems;         // hijos que bajan con el intro
    [Min(0f)] public float introDelay = 0.05f;
    [Min(0f)] public float introStagger = 0.04f;
    [Min(0f)] public float introDuration = 0.35f;
    public float introYOffset = 40f;

    Sequence _introSeq;

    void Awake()
    {
        if (!rootGroup)
            rootGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    void OnDisable()
    {
        _introSeq?.Kill(); _introSeq = null;
    }

    void Start()
    {
        // Estado del save
        _saveSystem = FindFirstObjectByType<SaveSystem>();
        if (continueButton) continueButton.interactable = (_saveSystem && _saveSystem.HasSave());

        // Forzar “solo texto”: sin transición ni imagen en TODOS los botones hijos
        ForceMinimalButtons();

        // Intro bonita (no toca selección; eso lo lleva MenuNavigator)
        PlayIntro();
        
#if UNITY_EDITOR && EASY_TRANSITIONS
    if (fadeOverride != null)
        Debug.Log($"[MainMenu] Override: {fadeOverride.name}, transitionIn={(fadeOverride.transitionIn ? fadeOverride.transitionIn.name : "NULL")}, transitionOut={(fadeOverride.transitionOut ? fadeOverride.transitionOut.name : "NULL")}");
#endif
    }

    void ForceMinimalButtons()
    {
        var btns = GetComponentsInChildren<Button>(true);
        foreach (var b in btns)
        {
            if (!b) continue;
            b.transition = Selectable.Transition.None;
            var img = b.GetComponent<Image>();
            if (img) img.enabled = false;
        }
    }

    void PlayIntro()
    {
        rootGroup.alpha = 0f;
        _introSeq?.Kill();
        _introSeq = DOTween.Sequence().SetUpdate(true);
        _introSeq.AppendInterval(introDelay);
        _introSeq.Append(DOTween.To(() => rootGroup.alpha, a => rootGroup.alpha = a, 1f, 0.2f));

        if (animatedItems != null)
        {
            float t = 0f;
            foreach (var rt in animatedItems)
            {
                if (!rt) continue;
                var start = rt.anchoredPosition;
                rt.anchoredPosition = start + new Vector2(0f, -introYOffset);

                var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                _introSeq.Insert(introDelay + t, rt.DOAnchorPos(start, introDuration).SetEase(Ease.OutCubic));
                _introSeq.Insert(introDelay + t, cg.DOFade(1f, introDuration * 0.9f));
                t += introStagger;
            }
        }
    }

    // --------- Acciones ----------
    public void OnNewGame()
    {
        GameBootService.NewGameReset();
        Load("Prologo");
    }

    public void OnContinue()
    {
        if (_saveSystem != null && _saveSystem.HasSave())
        {
            if (GameBootService.IsAvailable)
                GameBootService.Profile?.LoadProfile(_saveSystem);

            Load(worldScene);
        }
        else
        {
            OnNewGame();
        }
    }

    void Load(string sceneName)
    {
        bool useOverride = false;

        // Validación en tiempo de ejecución: solo usamos override si existe y contiene al menos un prefab de transición válido.
        if (fadeOverride != null && (fadeOverride.transitionIn != null || fadeOverride.transitionOut != null))
            useOverride = true;

#if UNITY_EDITOR
        if (fadeOverride != null && !useOverride)
            Debug.LogWarning("[MainMenu] Fade override asignado pero sin 'transitionIn' ni 'transitionOut' dentro del asset. Usando la transición por defecto.");
#endif

        if (useOverride)
            SceneTransitionLoader.Load(sceneName, fadeOverride, fadeDelay);
        else
            SceneTransitionLoader.Load(sceneName);
    }

}
