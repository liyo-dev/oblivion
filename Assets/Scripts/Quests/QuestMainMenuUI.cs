using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class QuestMainMenuUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private QuestVisibilityItemUI itemPrefab;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private QuestLogListUI quickMenu; // Referencia al menú rápido

    [Header("Animación")]
    [SerializeField] private float introDuration = 0.25f;
    [SerializeField] private Ease introEase = Ease.OutCubic;

    private bool _bound;
    private bool _phasePushed;
    private Tween _currentTween;

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

        EnsureGamePhasePushed();
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
    }

    public void ToggleMenu()
    {
        if (IsOpen)
            HideMenu();
        else
            ShowMenu();
    }

    public void Rebuild()
    {
        if (QuestManager.Instance == null) return;
        EnsureTemplate();

        if (contentRoot == null || itemPrefab == null) return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        var quests = QuestManager.Instance.GetAll()
            .OrderByDescending(q => QuestManager.Instance.GetVisibility(q.Id) == QuestVisibility.Tracked)
            .ThenBy(q => q.Data.GetLocalizedName());

        foreach (var rq in quests)
        {
            var item = Instantiate(itemPrefab, contentRoot);
            var visibility = QuestManager.Instance.GetVisibility(rq.Id);
            item.Bind(rq, visibility, OnVisibilityChanged);
        }

        if (headerText)
        {
            headerText.text = "Misiones (principal)";
        }
    }

    void OnVisibilityChanged(QuestManager.RuntimeQuest rq, QuestVisibility vis)
    {
        QuestManager.Instance?.SetVisibility(rq.Id, vis);
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

    void EnsureTemplate()
    {
        if (itemPrefab != null) return;
        // Crear un template muy sencillo en runtime para no depender de prefabs.
        var go = new GameObject("QuestVisibilityItem_Temp", typeof(RectTransform));
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = 12f;

        var nameObj = new GameObject("Name", typeof(RectTransform));
        nameObj.transform.SetParent(go.transform, false);
        var nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 22;
        nameText.enableAutoSizing = true;
        var layoutElem = nameObj.AddComponent<LayoutElement>();
        layoutElem.flexibleWidth = 1f;

        var stateObj = new GameObject("State", typeof(RectTransform));
        stateObj.transform.SetParent(go.transform, false);
        var stateText = stateObj.AddComponent<TextMeshProUGUI>();
        stateText.fontSize = 18;
        stateText.alignment = TextAlignmentOptions.MidlineLeft;
        var stateLayout = stateObj.AddComponent<LayoutElement>();
        stateLayout.preferredWidth = 160f;

        QuestVisibilityItemUI itemUi = go.AddComponent<QuestVisibilityItemUI>();

        Button MakeButton(string label)
        {
            var btnGo = new GameObject(label + "Btn", typeof(RectTransform));
            btnGo.transform.SetParent(go.transform, false);
            var image = btnGo.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.25f, 0.9f);
            var btn = btnGo.AddComponent<Button>();
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(btnGo.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 18f;
            txt.alignment = TextAlignmentOptions.Center;
            var fitter = txtGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layoutB = btnGo.AddComponent<LayoutElement>();
            layoutB.preferredWidth = 120f;
            layoutB.preferredHeight = 32f;
            return btn;
        }

        var follow = MakeButton("Seguir");
        var show = MakeButton("Mostrar");
        var hide = MakeButton("Ocultar");

        itemUi.GetType().GetField("questName", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(itemUi, nameText);
        itemUi.GetType().GetField("questState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(itemUi, stateText);
        itemUi.GetType().GetField("followButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(itemUi, follow);
        itemUi.GetType().GetField("showButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(itemUi, show);
        itemUi.GetType().GetField("hideButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(itemUi, hide);

        itemPrefab = itemUi;
    }

    void KillTween()
    {
        if (_currentTween != null && _currentTween.IsActive()) _currentTween.Kill();
        _currentTween = null;
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
}
