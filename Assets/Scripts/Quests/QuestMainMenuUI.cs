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
    [SerializeField] private QuestVisibilityItemUI itemPrefab;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI visibleHeaderText;
    [SerializeField] private TextMeshProUGUI hiddenHeaderText;
    [SerializeField] private QuestLogListUI quickMenu; // Referencia al menú rápido
    
    [Header("Visible Tab (debe contener el ScrollRect con su Viewport y Content)")]
    [Tooltip("GameObject raíz del tab de misiones visibles (debe tener ScrollView con ScrollRect)")]
    [SerializeField] private GameObject visibleTabRoot;
    [Tooltip("ScrollRect de misiones visibles (ScrollViewVisibles/Viewport/ScrollRect)")]
    [SerializeField] private ScrollRect visibleScrollRect;
    [Tooltip("Content (Content Visibles - RectTransform hijo del Viewport donde se instancian las misiones visibles)")]
    [SerializeField] private RectTransform visibleContentRoot;
    
    [Header("Hidden Tab (debe contener el ScrollRect con su Viewport y Content)")]
    [Tooltip("GameObject raíz del tab de misiones ocultas (debe tener ScrollView con ScrollRect)")]
    [SerializeField] private GameObject hiddenTabRoot;
    [Tooltip("ScrollRect de misiones ocultas (ScrollViewOcultas/Viewport/ScrollRect)")]
    [SerializeField] private ScrollRect hiddenScrollRect;
    [Tooltip("Content (Content Ocultas - RectTransform hijo del Viewport donde se instancian las misiones ocultas)")]
    [SerializeField] private RectTransform hiddenContentRoot;
    
    [Header("Input Icons")]
    [Tooltip("Icono de LB para mostrar en el header de misiones visibles")]
    [SerializeField] private Image lbIcon;
    [Tooltip("Icono de RB para mostrar en el header de misiones archivadas")]
    [SerializeField] private Image rbIcon;

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
        // Por defecto siempre mostrar misiones visibles/activas
        _showingHidden = false;
        ValidateScrollRectSetup();
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

        // Por defecto siempre mostrar misiones visibles/activas
        _showingHidden = false;

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

    /// <summary>
    /// Cambia a mostrar misiones visibles/activas (controlado por input LB)
    /// </summary>
    public void ShowVisibleTab()
    {
        if (_showingHidden)
        {
            _showingHidden = false;
            UpdateTabVisibility();
        }
    }

    /// <summary>
    /// Cambia a mostrar misiones archivadas/ocultas (controlado por input RB)
    /// </summary>
    public void ShowHiddenTab()
    {
        if (!_showingHidden)
        {
            _showingHidden = true;
            UpdateTabVisibility();
        }
    }

    public void Rebuild()
    {
        if (QuestManager.Instance == null) return;

        if (visibleContentRoot == null || itemPrefab == null)
        {
            Debug.LogError("QuestMainMenuUI: visibleContentRoot o itemPrefab es null");
            return;
        }

        ClearContainer(visibleContentRoot);
        if (hiddenContentRoot != null)
            ClearContainer(hiddenContentRoot);

        var quests = QuestManager.Instance.GetAll()
            .OrderBy(q => q.Data.GetLocalizedName());

        int visibleCount = 0;
        int hiddenCount = 0;

        foreach (var rq in quests)
        {
            var visibility = NormalizeVisibility(rq.Id, QuestManager.Instance.GetVisibility(rq.Id), persist: true);
            var parent = visibility == QuestVisibility.Hidden && hiddenContentRoot != null ? hiddenContentRoot : visibleContentRoot;
            var item = Instantiate(itemPrefab, parent);

            item.Bind(rq, visibility, OnVisibilityChanged);
            var scroll = parent == hiddenContentRoot ? hiddenScrollRect : visibleScrollRect;
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

        // Debug.Log($"QuestMainMenuUI: Rebuild complete. Visible={visibleCount}, Hidden={hiddenCount}");
        RefreshScrollViews();
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
                    wasShowButton = btn == item.GetArchiveButton();
                    wasHideButton = btn == item.GetActivateButton();
                }
            }
        }

        Rebuild();

        // Intentar restaurar la selección en la misma posición
        if (itemRoot != null)
        {
            var targetContainer = inHidden ? hiddenContentRoot : visibleContentRoot;
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

        // Prefer to select the same column (archive/activate) that triggered the change
        var itemUI = child.GetComponent<QuestVisibilityItemUI>();
        Button targetButton = null;
        if (itemUI != null)
        {
            if (preferShow) targetButton = itemUI.GetArchiveButton();
            else if (preferHide) targetButton = itemUI.GetActivateButton();
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
        bool hasHiddenSection = hiddenContentRoot != null && hiddenTabRoot != null;
        bool showingHidden = _showingHidden && hasHiddenSection;

        // Debug.Log($"QuestMainMenuUI: UpdateTabVisibility showingHidden={showingHidden}");

        // Actualizar headers
        if (visibleHeaderText)
            visibleHeaderText.gameObject.SetActive(!showingHidden);

        if (hiddenHeaderText)
            hiddenHeaderText.gameObject.SetActive(showingHidden);

        // Activar/desactivar los tabs raíz completos (esto incluye el ScrollRect entero)
        if (visibleTabRoot != null)
        {
            visibleTabRoot.SetActive(!showingHidden);
            // Debug.Log($"QuestMainMenuUI: visibleTabRoot.SetActive({!showingHidden})");
        }

        if (hiddenTabRoot != null)
        {
            hiddenTabRoot.SetActive(showingHidden);
            // Debug.Log($"QuestMainMenuUI: hiddenTabRoot.SetActive({showingHidden})");
        }

        // Force update del canvas y layouts SOLO en el tab activo
        Canvas.ForceUpdateCanvases();
        
        RectTransform activeContent = showingHidden ? hiddenContentRoot : visibleContentRoot;
        if (activeContent != null && activeContent.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(activeContent);
            // Debug.Log($"QuestMainMenuUI: LayoutRebuilder forzado en {activeContent.name} (height={activeContent.rect.height})");
        }

        // Actualizar visibilidad de los iconos de input (LB/RB)
        UpdateInputIcons(showingHidden);

        // Seleccionar el primer botón del contenido activo
        EnsureSelection();
    }

    void UpdateInputIcons(bool showingHidden)
    {
        // Mostrar el icono correspondiente según la tab activa
        if (lbIcon != null)
            lbIcon.gameObject.SetActive(!showingHidden);
        
        if (rbIcon != null)
            rbIcon.gameObject.SetActive(showingHidden);
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
                var itemUI = child.GetComponent<QuestVisibilityItemUI>();
                
                if (itemUI != null)
                {
                    // Obtener el botón correcto según el panel actual
                    Button targetButton = _showingHidden ? itemUI.GetActivateButton() : itemUI.GetArchiveButton();
                    
                    if (targetButton != null && targetButton.gameObject.activeInHierarchy && targetButton.interactable)
                    {
                        targetButton.Select();
                        // Debug.Log($"QuestMainMenuUI: EnsureSelection -> selected {targetButton.name} from {child.name}");
                        return;
                    }
                }
            }
        }

        // Debug.Log($"QuestMainMenuUI: EnsureSelection -> no hay botones disponibles para seleccionar");
    }

    void RefreshScrollViews()
    {
        RefreshScrollView(visibleScrollRect, visibleContentRoot);
        RefreshScrollView(hiddenScrollRect, hiddenContentRoot);
    }

    void RefreshScrollView(ScrollRect scrollRect, RectTransform content)
    {
        if (scrollRect == null || content == null)
        {
            Debug.LogWarning($"QuestMainMenuUI.RefreshScrollView: ScrollRect o Content es null (scrollRect={scrollRect != null}, content={content != null})");
            return;
        }

        // Verificar que content esté bajo el viewport correcto
        if (scrollRect.viewport != null && !content.IsChildOf(scrollRect.viewport))
        {
            Debug.LogError($"QuestMainMenuUI.RefreshScrollView: Content '{content.name}' no es hijo del Viewport '{scrollRect.viewport.name}'");
            return;
        }

        // Force a layout pass so the ScrollRect receives the correct content height
        // before we reset the scroll position. This avoids situations where the
        // content appears "stuck" because its size was still zero when the value
        // was applied.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.transform as RectTransform;
        if (viewport != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);

        // Asegurar que el content está asignado al ScrollRect y configurado correctamente
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        
        // Log detallado para debugging
        // Debug.Log($"QuestMainMenuUI.RefreshScrollView: ScrollRect '{scrollRect.name}' - Content: '{content.name}', Height: {content.rect.height}, " +
        //           $"Viewport: {(scrollRect.viewport != null ? scrollRect.viewport.name : "null")}, " +
        //           $"Viewport Height: {(scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f)}, " +
        //           $"Child Count: {content.childCount}, " +
        //           $"GameObject Active: {scrollRect.gameObject.activeInHierarchy}, " +
        //           $"Enabled: {scrollRect.enabled}");

        // Resetear scroll a la parte superior
        scrollRect.verticalNormalizedPosition = 1f;
        
        // Forzar update inmediato para que el scroll funcione correctamente
        scrollRect.StopMovement();
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

    /// <summary>
    /// Valida y configura los ScrollRects para asegurar que funcionen correctamente
    /// </summary>
    void ValidateScrollRectSetup()
    {
        // Debug.Log("[QuestMainMenuUI] Validando configuración de ScrollRects...");
        
        // Validar ScrollRect de misiones visibles
        if (visibleScrollRect != null && visibleContentRoot != null)
        {
            visibleScrollRect.content = visibleContentRoot;
            visibleScrollRect.horizontal = false;
            visibleScrollRect.vertical = true;
            visibleScrollRect.movementType = ScrollRect.MovementType.Clamped;
            visibleScrollRect.scrollSensitivity = 15f;
            // Debug.Log($"[QuestMainMenuUI] ✅ ScrollRect visible configurado: content={visibleContentRoot.name}, viewport={visibleScrollRect.viewport?.name ?? "null"}");
        }
        else
        {
            Debug.LogWarning($"[QuestMainMenuUI] ⚠️ ScrollRect o Content visible es null: scrollRect={visibleScrollRect != null}, content={visibleContentRoot != null}");
        }
        
        // Validar ScrollRect de misiones ocultas
        if (hiddenScrollRect != null && hiddenContentRoot != null)
        {
            hiddenScrollRect.content = hiddenContentRoot;
            hiddenScrollRect.horizontal = false;
            hiddenScrollRect.vertical = true;
            hiddenScrollRect.movementType = ScrollRect.MovementType.Clamped;
            hiddenScrollRect.scrollSensitivity = 15f;
            // Debug.Log($"[QuestMainMenuUI] ✅ ScrollRect oculto configurado: content={hiddenContentRoot.name}, viewport={hiddenScrollRect.viewport?.name ?? "null"}");
        }
        else
        {
            Debug.LogWarning($"[QuestMainMenuUI] ⚠️ ScrollRect o Content oculto es null: scrollRect={hiddenScrollRect != null}, content={hiddenContentRoot != null}");
        }
    }
}

