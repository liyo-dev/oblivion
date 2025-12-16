using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    [SerializeField] private ScrollRect visibleScrollRect;
    [SerializeField] private ScrollRect hiddenScrollRect;
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
    private ColorBlock _visibleTabOriginalColors;
    private ColorBlock _hiddenTabOriginalColors;
    private bool _tabColorsCaptured;

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
        BindTabs();
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

        // Refresh the list on every open to ensure scroll rects receive the correct
        // content size even if quests changed while the menu was hidden.
        Rebuild();

        EnsureSelection();
        EnsureGamePhasePushed();
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
            Debug.Log($"QuestMainMenuUI: Instantiated item for '{rq.Id}' into {(parent == hiddenRoot ? "hidden" : "visible")} parent");
            item.Bind(rq, visibility, OnVisibilityChanged);
            var scroll = parent == hiddenRoot ? hiddenScrollRect : visibleScrollRect;
            item.ConfigureScrollRect(scroll);

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
            visibleHeaderText.text = $"Misiones visibles ({visibleCount})"; //TODO este texto debe ir multificado

        if (hiddenHeaderText)
            hiddenHeaderText.text = $"Misiones ocultas ({hiddenCount})"; //TODO este texto debe ir multificado

        Debug.Log($"QuestMainMenuUI: Rebuild complete. Visible={visibleCount}, Hidden={hiddenCount}");
        RefreshScrollViews();
        UpdateTabVisibility();
    }

    void OnVisibilityChanged(QuestManager.RuntimeQuest rq, QuestVisibility vis)
    {
        QuestManager.Instance?.SetVisibility(rq.Id, NormalizeVisibility(rq.Id, vis, persist: true));
    }

    void BindTabs()
    {
        if (visibleTabButton != null)
        {
            visibleTabButton.onClick.RemoveListener(ShowVisibleTab);
            visibleTabButton.onClick.AddListener(ShowVisibleTab);
        }

        if (hiddenTabButton != null)
        {
            hiddenTabButton.onClick.RemoveListener(ShowHiddenTab);
            hiddenTabButton.onClick.AddListener(ShowHiddenTab);
        }

        // Capture original ColorBlocks so we don't mutate them repeatedly
        if (!_tabColorsCaptured)
        {
            if (visibleTabButton != null)
                _visibleTabOriginalColors = visibleTabButton.colors;
            if (hiddenTabButton != null)
                _hiddenTabOriginalColors = hiddenTabButton.colors;
            _tabColorsCaptured = true;
        }
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
        GameObject selected = EventSystem.current?.currentSelectedGameObject;

        Transform itemRoot = null;
        int indexInContainer = -1;
        bool inHidden = false;
        bool wasShowButton = false;
        bool wasHideButton = false;

        if (selected != null)
        {
            var item = selected.GetComponentInParent<QuestVisibilityItemUI>();
            if (item != null)
            {
                itemRoot = item.transform;

                inHidden = (hiddenContentRoot != null && itemRoot.IsChildOf(hiddenContentRoot));
                indexInContainer = itemRoot.GetSiblingIndex();

                var btn = selected.GetComponent<Button>();
                if (btn != null)
                {
                    wasShowButton = btn == item.GetShowButton();
                    wasHideButton = btn == item.GetHideButton();
                }
            }
        }

        Rebuild();

        // Intentar restaurar la selección en la misma posición
        if (itemRoot != null)
        {
            var targetContainer = inHidden ? hiddenContentRoot : (visibleContentRoot != null ? visibleContentRoot : contentRoot);
            TryRestoreSelection(targetContainer, indexInContainer, wasShowButton, wasHideButton);
        }
        else
        {
            // Fallback: seleccionar el primer botón disponible
            EnsureSelection();
        }
    }

    // Removed GetChildIndexInContainer: use Transform.GetSiblingIndex() instead.

    void TryRestoreSelection(Transform container, int desiredIndex, bool preferShow, bool preferHide)
    {
        if (container == null) return;

        int count = container.childCount;
        if (count == 0) return;

        int idx = desiredIndex;
        // If the item we came from was removed/moved, select the item that occupies the same slot (or the previous one)
        if (idx >= count) idx = count - 1;
        if (idx < 0) idx = 0;

        var child = container.GetChild(idx);
        if (child == null) return;

        // Prefer to select the same column (show/hide) that triggered the change
        var itemUI = child.GetComponent<QuestVisibilityItemUI>();
        Button targetButton = null;
        if (itemUI != null)
        {
            if (preferShow) targetButton = itemUI.GetShowButton();
            else if (preferHide) targetButton = itemUI.GetHideButton();
        }

        // Fallback to first interactable button
        if (targetButton == null || !targetButton.interactable || !targetButton.gameObject.activeInHierarchy)
        {
            var btns = child.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b != null && b.gameObject.activeInHierarchy && b.interactable)
                {
                    targetButton = b;
                    break;
                }
            }
        }

        if (targetButton != null)
            targetButton.Select();
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

        Debug.Log($"QuestMainMenuUI: UpdateTabVisibility showingHidden={showingHidden}");

        if (visibleHeaderText)
            visibleHeaderText.gameObject.SetActive(!showingHidden);

        if (hiddenHeaderText)
            hiddenHeaderText.gameObject.SetActive(showingHidden);

        if (visibleContentRoot)
            visibleContentRoot.gameObject.SetActive(!showingHidden);

        if (hiddenContentRoot)
            hiddenContentRoot.gameObject.SetActive(showingHidden);

        // Force UI/canvas/layout update so newly-activated content becomes visible immediately.
        Canvas.ForceUpdateCanvases();

        if (visibleContentRoot != null && visibleContentRoot.gameObject.activeInHierarchy)
        {
            var rt = visibleContentRoot as RectTransform;
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        if (hiddenContentRoot != null && hiddenContentRoot.gameObject.activeInHierarchy)
        {
            var rt = hiddenContentRoot as RectTransform;
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        UpdateTabButtons(showingHidden);

        // Seleccionar el primer botón del contenido activo
        Transform activeContainer = showingHidden ? hiddenContentRoot : visibleContentRoot;
        if (activeContainer != null && activeContainer.childCount > 0)
        {
            var firstChild = activeContainer.GetChild(0);
            var firstButton = firstChild.GetComponentInChildren<Button>();
            if (firstButton != null && firstButton.interactable)
            {
                firstButton.Select();
                return;
            }
        }

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

        // Use captured original ColorBlock (if available) so we don't apply cumulative changes.
        ColorBlock baseColors = button.colors;
        if (_tabColorsCaptured)
        {
            if (button == visibleTabButton)
                baseColors = _visibleTabOriginalColors;
            else if (button == hiddenTabButton)
                baseColors = _hiddenTabOriginalColors;
        }

        var colors = baseColors;
        colors.colorMultiplier = isActive ? 1.2f : 1f;
        button.colors = colors;
    }

    void EnsureSelection()
    {
        // Intentar seleccionar el primer botón en el contenido activo
        Transform activeContainer = _showingHidden ? hiddenContentRoot : visibleContentRoot;
        if (activeContainer != null && activeContainer.gameObject.activeInHierarchy)
        {
            for (int i = 0; i < activeContainer.childCount; i++)
            {
                var child = activeContainer.GetChild(i);
                var buttons = child.GetComponentsInChildren<Button>();
                foreach (var btn in buttons)
                {
                    if (btn != null && btn.gameObject.activeInHierarchy && btn.interactable)
                    {
                        btn.Select();
                        Debug.Log($"QuestMainMenuUI: EnsureSelection -> selected {btn.name}");
                        return;
                    }
                }
            }
        }

        // Fallback: seleccionar el tab correspondiente
        GameObject target = null;
        if (_showingHidden)
            target = hiddenTabButton != null ? hiddenTabButton.gameObject : target;
        else
            target = visibleTabButton != null ? visibleTabButton.gameObject : target;

        Debug.Log($"QuestMainMenuUI: EnsureSelection -> fallback target={(target!=null?target.name:"null")}");

        if (target != null)
        {
            var btn = target.GetComponent<Button>();
            if (btn != null)
                btn.Select();
        }
    }

    void RefreshScrollViews()
    {
        RefreshScrollView(visibleScrollRect, visibleContentRoot as RectTransform);
        RefreshScrollView(hiddenScrollRect, hiddenContentRoot as RectTransform);
    }

    void RefreshScrollView(ScrollRect scrollRect, RectTransform content)
    {
        if (scrollRect == null || content == null)
            return;

        // Force a layout pass so the ScrollRect receives the correct content height
        // before we reset the scroll position. This avoids situations where the
        // content appears "stuck" because its size was still zero when the value
        // was applied.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        if (viewport != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

        scrollRect.content = content;
        content.anchoredPosition = Vector2.zero;
        scrollRect.verticalNormalizedPosition = 1f;
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
