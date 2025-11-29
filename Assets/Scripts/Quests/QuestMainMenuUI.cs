using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

public class QuestMainMenuUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Transform visibleContentRoot;
    [SerializeField] private Transform hiddenContentRoot;
    [SerializeField] private QuestVisibilityItemUI itemPrefab;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI visibleHeaderText;
    [SerializeField] private TextMeshProUGUI hiddenHeaderText;
    [SerializeField] private Button visibleTabButton;
    [SerializeField] private Button hiddenTabButton;
    [SerializeField] private MenuNavigator navigator;
    [SerializeField] private QuestLogListUI quickMenu; // Referencia al menú rápido

    [Header("Animación")]
    [SerializeField] private float introDuration = 0.25f;
    [SerializeField] private Ease introEase = Ease.OutCubic;

    private bool _bound;
    private bool _phasePushed;
    private Tween _currentTween;
    private bool _showingHidden;
    private PlayerActionManager _actionManager;
    private bool _actionModeActive;

    public bool IsOpen
    {
        get
        {
            if (panelGroup != null)
                return panelGroup.gameObject.activeSelf && panelGroup.alpha > 0.001f && panelGroup.blocksRaycasts;
            return panelRoot != null && panelRoot.activeSelf;
        }
    }

    void OnEnable()
    {
        Bind();
        Rebuild();
    }

    void OnDisable()
    {
        Unbind();
        ReleaseGamePhase();
        KillTween();
        ReleaseActionMode();
    }

    public void ShowMenu()
    {
        if (!CanOpenMainMenu()) return;

        KillTween();
        if (panelRoot) panelRoot.SetActive(true);
        if (panelGroup)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
            _currentTween = panelGroup.DOFade(1f, introDuration).SetEase(introEase).SetUpdate(true);
        }

        if (quickMenu != null && quickMenu.IsVisible)
        {
            quickMenu.ShowPanel(false, ignoreRestrictions: true);
        }

        EnsureSelection();
        EnsureGamePhasePushed();
        EnsureNavigatorReady();
        EnsureActionMode();
    }

    public void HideMenu()
    {
        KillTween();
        if (panelGroup)
        {
            _currentTween = panelGroup.DOFade(0f, introDuration).SetEase(introEase).SetUpdate(true)
                .OnComplete(() => { if (panelRoot) panelRoot.SetActive(false); });
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }
        else if (panelRoot)
        {
            panelRoot.SetActive(false);
        }

        ReleaseGamePhase();
        ReleaseActionMode();
    }

    public void ToggleMenu()
    {
        if (IsOpen)
            HideMenu();
        else
            ShowMenu();
    }

    public void ShowVisibleTab()
    {
        _showingHidden = false;
        UpdateTabVisibility();
    }

    public void ShowHiddenTab()
    {
        _showingHidden = true;
        UpdateTabVisibility();
    }

    public void Rebuild()
    {
        if (QuestManager.Instance == null) return;

        var visibleRoot = visibleContentRoot != null ? visibleContentRoot : contentRoot;
        var hiddenRoot = hiddenContentRoot;

        if (visibleRoot == null || itemPrefab == null) return;

        ClearContainer(visibleRoot);
        if (hiddenRoot != null)
            ClearContainer(hiddenRoot);

        var quests = QuestManager.Instance.GetAll()
            .OrderBy(q => q.Data.GetLocalizedName());

        int visibleCount = 0;
        int hiddenCount = 0;

        foreach (var rq in quests)
        {
            var visibility = NormalizeVisibility(rq.Id, QuestManager.Instance.GetVisibility(rq.Id), persist: true);
            var parent = visibility == QuestVisibility.Hidden && hiddenRoot != null ? hiddenRoot : visibleRoot;
            var item = Instantiate(itemPrefab, parent);
            item.Bind(rq, visibility, OnVisibilityChanged);

            if (visibility == QuestVisibility.Hidden)
                hiddenCount++;
            else
                visibleCount++;
        }

        if (headerText)
        {
            headerText.text = "Misiones (principal)";
        }

        if (visibleHeaderText)
            visibleHeaderText.text = $"Misiones visibles ({visibleCount})";

        if (hiddenHeaderText)
            hiddenHeaderText.text = $"Misiones ocultas ({hiddenCount})";

        UpdateTabVisibility();
    }

    void OnVisibilityChanged(QuestManager.RuntimeQuest rq, QuestVisibility vis)
    {
        QuestManager.Instance?.SetVisibility(rq.Id, NormalizeVisibility(rq.Id, vis, persist: true));
    }

    void Bind()
    {
        if (_bound || QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestsChanged += Rebuild;
        QuestManager.Instance.OnQuestVisibilityChanged += OnQuestVisibilityChanged;
        _bound = true;
    }

    void Unbind()
    {
        if (!_bound || QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestsChanged -= Rebuild;
        QuestManager.Instance.OnQuestVisibilityChanged -= OnQuestVisibilityChanged;
        _bound = false;
    }

    void OnQuestVisibilityChanged(string questId, QuestVisibility vis)
    {
        Rebuild();
    }

    void KillTween()
    {
        if (_currentTween != null && _currentTween.IsActive()) _currentTween.Kill();
        _currentTween = null;
    }

    void ClearContainer(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    void UpdateTabVisibility()
    {
        bool hasHiddenSection = hiddenContentRoot != null;
        bool showingHidden = _showingHidden && hasHiddenSection;

        if (visibleHeaderText)
            visibleHeaderText.gameObject.SetActive(!showingHidden);

        if (hiddenHeaderText)
            hiddenHeaderText.gameObject.SetActive(showingHidden);

        if (visibleContentRoot)
            visibleContentRoot.gameObject.SetActive(!showingHidden);

        if (hiddenContentRoot)
            hiddenContentRoot.gameObject.SetActive(showingHidden);

        UpdateTabButtons(showingHidden);
        EnsureSelection();
    }

    void UpdateTabButtons(bool showingHidden)
    {
        SetTabButtonState(visibleTabButton, !showingHidden);
        SetTabButtonState(hiddenTabButton, showingHidden);
    }

    void SetTabButtonState(Button button, bool isActive)
    {
        if (button == null) return;

        var colors = button.colors;
        colors.colorMultiplier = isActive ? 1.2f : 1f;
        button.colors = colors;
    }

    void EnsureSelection()
    {
        var es = EventSystem.current;
        if (es == null) return;

        GameObject target = null;

        if (navigator != null)
        {
            navigator.RefreshItemsFromChildren(resetSelection: false);
            var first = navigator.items.FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy && b.interactable);
            if (first != null)
            {
                navigator.ForceSelect(first, resetCooldown: true);
                return;
            }
        }

        if (_showingHidden)
            target = hiddenTabButton != null ? hiddenTabButton.gameObject : target;
        else
            target = visibleTabButton != null ? visibleTabButton.gameObject : target;

        if (target != null)
            es.SetSelectedGameObject(target);
    }

    void EnsureNavigatorReady()
    {
        if (navigator == null) return;

        navigator.ResetCooldown();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            navigator.ForceSelect(navigator.items.Count > 0 ? navigator.items[0] : null, resetCooldown: true);
        }
    }

    QuestVisibility NormalizeVisibility(string questId, QuestVisibility current, bool persist = false)
    {
        if (current == QuestVisibility.Tracked)
        {
            if (persist)
                QuestManager.Instance?.SetVisibility(questId, QuestVisibility.Visible);
            return QuestVisibility.Visible;
        }

        return current;
    }

    bool CanOpenMainMenu()
    {
        if (!GameState.CanOpenInventory) return false;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return false;
        return true;
    }

    void EnsureGamePhasePushed()
    {
        if (_phasePushed) return;
        GameState.Push(GamePhase.QuestMenu);
        _phasePushed = true;
    }

    void ReleaseGamePhase()
    {
        if (!_phasePushed) return;
        if (GameState.Is(GamePhase.QuestMenu))
            GameState.Pop(GamePhase.QuestMenu);
        _phasePushed = false;
    }

    void EnsureActionMode()
    {
        if (_actionModeActive) return;
        if (_actionManager == null)
            PlayerService.TryGetComponent(out _actionManager, includeInactive: true, allowSceneLookup: true);

        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Inventory);
            _actionModeActive = true;
        }
    }

    void ReleaseActionMode()
    {
        if (!_actionModeActive || _actionManager == null) return;
        _actionManager.PopMode(ActionMode.Inventory);
        _actionModeActive = false;
    }
}
