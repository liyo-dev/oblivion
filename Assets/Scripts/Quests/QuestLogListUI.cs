using UnityEngine;
using TMPro;
using System.Collections;
using DG.Tweening;

public class QuestLogListUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform contentRoot;      // ScrollView/Viewport/Content
    [SerializeField] private QuestLogItemUI itemPrefab;  // Prefab de item misión
    [SerializeField] private TextMeshProUGUI headerText; // "Misiones" (opcional)
    [SerializeField] private bool showInactive = false;  // filtrar inactivas
    [SerializeField] private GameObject panelRoot;       // El panel completo para show/hide
    [SerializeField] private GameObject scrollView;      // Solo el ScrollView para ocultar
    [SerializeField] private TextMeshProUGUI helpText;   // Texto de ayuda para cambiar

    [Header("Animación (DOTween)")]
    [SerializeField] private RectTransform animatedRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private float hideAfterSeconds = 4f;
    [SerializeField] private float hideDistance = 420f;
    [SerializeField] private float tweenDuration = 0.35f;
    [SerializeField] private Ease tweenEase = Ease.InOutSine;

    bool _bound;                    // ya suscrito al manager
    QuestManager _qm;               // cache del manager suscrito
    Coroutine _waitCo;
    private bool _isPanelVisible = false; // Estado del panel
    private Tween _panelTween;
    private Vector2 _shownPos;
    private Vector2 _hiddenPos;
    private Coroutine _autoHideCo;

    public bool IsVisible => _isPanelVisible;

    void OnEnable()
    {
        // Empieza a esperar al manager si aún no existe
        _waitCo = StartCoroutine(BindWhenReady());

        if (animatedRoot)
        {
            _shownPos = animatedRoot.anchoredPosition;
            _hiddenPos = _shownPos + Vector2.down * hideDistance;
            animatedRoot.anchoredPosition = _hiddenPos;
        }

        // Avoid deactivating the panelRoot if it's the same GameObject that holds this component,
        // because that would disable this component immediately after enabling it.
        if (panelRoot && panelRoot != this.gameObject) panelRoot.SetActive(false);
        if (scrollView) scrollView.SetActive(false);
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        UpdateHelpText();
    }

    void OnDisable()
    {
        Unbind();
        if (_waitCo != null) { StopCoroutine(_waitCo); _waitCo = null; }
        KillTween();
        if (_autoHideCo != null) StopCoroutine(_autoHideCo);
        _isPanelVisible = false;
    }

    IEnumerator BindWhenReady()
    {
        // Espera a que QuestManager exista (creado por tu escena Start)
        while (QuestManager.Instance == null) yield return null;

        // Si cambió de instancia (p.ej. reload), re-suscribe limpio
        if (_qm != QuestManager.Instance)
        {
            Unbind();
            _qm = QuestManager.Instance;
            _qm.OnQuestsChanged += Rebuild;
            _qm.OnQuestStarted += OnQuestStarted;
            _qm.OnQuestVisibilityChanged += OnQuestVisibilityChanged;
            _bound = true;
        }

        Rebuild();
    }

    void Unbind()
    {
        if (_bound && _qm != null)
        {
            _qm.OnQuestsChanged -= Rebuild;
            _qm.OnQuestStarted -= OnQuestStarted;
            _qm.OnQuestVisibilityChanged -= OnQuestVisibilityChanged;
        }
        _bound = false;
        _qm = null;
    }

    public void Rebuild()
    {
        if (!contentRoot || itemPrefab == null) return;
        if (QuestManager.Instance == null) return; // por si se descargó la escena

        // limpiar
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        // poblar
        foreach (var rq in QuestManager.Instance.GetAll())
        {
            if (QuestManager.Instance.GetVisibility(rq.Id) == QuestVisibility.Hidden) continue;
            if (!showInactive && rq.State == QuestState.Inactive) continue;
            var go = Instantiate(itemPrefab, contentRoot);
            go.Bind(rq); // el propio item gestiona nulls internos
        }
    }

    void OnQuestStarted(string questId)
    {
        // Mostrar automáticamente el panel cuando aparece una nueva misión
        ShowPanel(true, ignoreRestrictions: true);
        RestartAutoHide();
    }

    void OnQuestVisibilityChanged(string questId, QuestVisibility vis)
    {
        Rebuild();
    }

    public void TogglePanel()
    {
        ShowPanel(!_isPanelVisible);
    }

    public void ShowPanel(bool show, bool ignoreRestrictions = false)
    {
        if (show && !ignoreRestrictions)
        {
            if (!GameState.CanOpenInventory) return;
            if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;
        }

        _isPanelVisible = show;

        if (_isPanelVisible)
            AnimateShow();
        else
            AnimateHide();

        UpdateHelpText();
    }

    void AnimateShow()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (scrollView) scrollView.SetActive(true);
        KillTween();
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.blocksRaycasts = true;
            panelGroup.interactable = true;
            _panelTween = panelGroup.DOFade(1f, tweenDuration).SetEase(tweenEase).SetUpdate(true);
        }
        if (animatedRoot)
        {
            animatedRoot.anchoredPosition = _hiddenPos;
            _panelTween = animatedRoot.DOAnchorPos(_shownPos, tweenDuration).SetEase(tweenEase).SetUpdate(true);
        }
        RestartAutoHide();
    }

    void AnimateHide()
    {
        KillTween();
        if (panelGroup)
        {
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
            _panelTween = panelGroup.DOFade(0f, tweenDuration).SetEase(tweenEase).SetUpdate(true)
                .OnComplete(() => { if (panelRoot) panelRoot.SetActive(false); });
        }
        if (animatedRoot)
        {
            _panelTween = animatedRoot.DOAnchorPos(_hiddenPos, tweenDuration).SetEase(tweenEase).SetUpdate(true)
                .OnComplete(() =>
                {
                    if (scrollView) scrollView.SetActive(false);
                    if (panelGroup == null && panelRoot) panelRoot.SetActive(false);
                });
        }
        else
        {
            if (panelRoot) panelRoot.SetActive(false);
            if (scrollView) scrollView.SetActive(false);
        }
        if (_autoHideCo != null) { StopCoroutine(_autoHideCo); _autoHideCo = null; }
    }

    void RestartAutoHide()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_autoHideCo != null) StopCoroutine(_autoHideCo);
        _autoHideCo = StartCoroutine(AutoHideAfterDelay());
    }

    IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSecondsRealtime(hideAfterSeconds);
        _isPanelVisible = false;
        AnimateHide();
        UpdateHelpText();
    }

    void UpdateHelpText()
    {
        if (!helpText) return;
        string key = _isPanelVisible ? "UI_MISIONES_OCULTAR" : "UI_MISIONES_SHOW";
        string fallback = _isPanelVisible ? "Ocultar misiones" : "Mostrar misiones";
        if (LocalizationManager.Instance != null)
        {
            helpText.text = LocalizationManager.Instance.Get(key, fallback);
        }
        else
        {
            helpText.text = fallback;
        }
    }

    void KillTween()
    {
        if (_panelTween != null && _panelTween.IsActive()) _panelTween.Kill();
        _panelTween = null;
    }
}
