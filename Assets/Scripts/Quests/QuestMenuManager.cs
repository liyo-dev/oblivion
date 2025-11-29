using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class QuestMenuManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private QuestLogListUI quickMenu; // El menú rápido (QuickQuestMenu)
    [SerializeField] private QuestMainMenuUI mainMenu; // El menú principal (QuestMainMenu)

    [Header("Auto apertura")]
    [SerializeField] private bool autoShowQuickOnQuestInit = true;
    [SerializeField] private float autoShowDelay = 0.35f;
    [SerializeField] private float holdTimeForMainMenu = 0.6f;

    private Coroutine _autoShowRoutine;
    private QuestManager _lastQuestManager;
    private float _dpadHoldTime;
    private bool _dpadUpHeld;
    private bool _dpadUpPressed;
    private bool _bPressed;
    private bool _startPressed;

    private void Awake()
    {
        if (quickMenu != null) quickMenu.ShowPanel(false, ignoreRestrictions: true);
        if (mainMenu != null) mainMenu.HideMenu();
    }

    private void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
        TrySubscribeQuestManager();
    }

    private void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
        ResetDpadHold();
        _dpadUpPressed = false;
        _bPressed = false;
        _startPressed = false;
        UnsubscribeQuestManager();
    }

    private void Update()
    {
        TrySubscribeQuestManager();

        if (_startPressed)
        {
            CloseAllMenus();
            _startPressed = false;
            return;
        }

        HandleDpadUpHeld();

        if (_dpadUpPressed)
        {
            HandleDpadUpPressed();
            _dpadUpPressed = false;
        }

        if (_bPressed)
        {
            HandleBPressed();
            _bPressed = false;
        }
    }

    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.Start when input.Phase == InputActionPhase.Performed:
                _startPressed = true;
                break;
            case GamepadInputReader.InputEventType.Cancel when input.Phase == InputActionPhase.Performed:
                _bPressed = true;
                break;
            case GamepadInputReader.InputEventType.DpadUp:
                _dpadUpPressed |= input.Phase == InputActionPhase.Performed;
                _dpadUpHeld = input.Phase == InputActionPhase.Performed;
                if (input.Phase == InputActionPhase.Canceled)
                    _dpadUpHeld = false;
                break;
        }
    }

    private void TrySubscribeQuestManager()
    {
        if (QuestManager.Instance == _lastQuestManager) return;

        UnsubscribeQuestManager();
        _lastQuestManager = QuestManager.Instance;

        if (_lastQuestManager != null)
            _lastQuestManager.OnQuestStarted += HandleQuestStarted;
    }

    private void UnsubscribeQuestManager()
    {
        if (_lastQuestManager == null) return;

        _lastQuestManager.OnQuestStarted -= HandleQuestStarted;
        _lastQuestManager = null;
    }

    private void HandleQuestStarted(string questId)
    {
        if (!autoShowQuickOnQuestInit || quickMenu == null) return;

        if (_autoShowRoutine != null)
            StopCoroutine(_autoShowRoutine);

        _autoShowRoutine = StartCoroutine(AutoShowQuickMenu());
    }

    private IEnumerator AutoShowQuickMenu()
    {
        if (autoShowDelay > 0f)
            yield return new WaitForSecondsRealtime(autoShowDelay);

        quickMenu.ShowPanel(true, ignoreRestrictions: true);
        _autoShowRoutine = null;
    }

    private void HandleDpadUpPressed()
    {
        Debug.Log("[QuestMenuManager] HandleDpadUpPressed called.");
        bool quickIsOpen = quickMenu != null && quickMenu.IsVisible;
        bool mainIsOpen = mainMenu != null && mainMenu.IsOpen;

        Debug.Log($"[QuestMenuManager] Quick menu open: {quickIsOpen}, Main menu open: {mainIsOpen}");

        if (!quickIsOpen && !mainIsOpen)
        {
            if (!CanOpenQuestMenus())
            {
                Debug.Log("[QuestMenuManager] Cannot open quick menu because another menu is active or access is blocked.");
                return;
            }

            Debug.Log("[QuestMenuManager] Opening quick menu.");
            if (quickMenu == null)
            {
                Debug.LogWarning("[QuestMenuManager] quickMenu reference is null - cannot open quick menu.");
                return;
            }

            Debug.Log($"[QuestMenuManager] quickMenu GO='{quickMenu.gameObject.name}', activeSelf={quickMenu.gameObject.activeSelf}, activeInHierarchy={quickMenu.gameObject.activeInHierarchy}");
            // Asegurar que el GameObject del componente esté activo para permitir coroutines/animaciones
            if (!quickMenu.gameObject.activeInHierarchy)
            {
                Debug.Log("[QuestMenuManager] Activating quickMenu GameObject before ShowPanel");
                quickMenu.gameObject.SetActive(true);

                // Log and activate any inactive ancestors (canvas or parent containers)
                var inactiveAncestors = new System.Collections.Generic.List<Transform>();
                var p = quickMenu.transform.parent;
                while (p != null)
                {
                    if (!p.gameObject.activeSelf)
                        inactiveAncestors.Add(p);
                    p = p.parent;
                }

                if (inactiveAncestors.Count > 0)
                {
                    foreach (var anc in inactiveAncestors)
                        Debug.Log($"[QuestMenuManager] Inactive ancestor: {anc.name} (activeSelf={anc.gameObject.activeSelf})");

                    // Enable ancestors from top-down
                    for (int i = inactiveAncestors.Count - 1; i >= 0; i--)
                    {
                        var anc = inactiveAncestors[i];
                        Debug.Log($"[QuestMenuManager] Activating ancestor: {anc.name}");
                        anc.gameObject.SetActive(true);
                    }
                }
            }

            // Attach ActiveStateDebugger to help detect who disables the GO
            if (quickMenu.gameObject.GetComponent<ActiveStateDebugger>() == null)
            {
                quickMenu.gameObject.AddComponent<ActiveStateDebugger>();
            }

            try
            {
                quickMenu.ShowPanel(true, ignoreRestrictions: true);
                Debug.Log("[QuestMenuManager] Called ShowPanel on quickMenu");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[QuestMenuManager] Exception while showing quickMenu: {ex}");
            }
        }
        else if (quickIsOpen && !mainIsOpen)
        {
            if (mainMenu != null && CanOpenQuestMenus() && CanOpenMainMenu())
            {
                Debug.Log("[QuestMenuManager] Transitioning from quick menu to main menu.");
                if (quickMenu != null)
                    quickMenu.ShowPanel(false, ignoreRestrictions: true);
                mainMenu.ShowMenu();
                ResetDpadHold();
            }
            else
            {
                Debug.LogWarning("[QuestMenuManager] Cannot open main menu. CanOpenMainMenu returned false.");
            }
        }
    }

    private void HandleDpadUpHeld()
    {
        bool quickIsOpen = quickMenu != null && quickMenu.IsVisible;
        bool mainIsOpen = mainMenu != null && mainMenu.IsOpen;

        if (!quickIsOpen || mainIsOpen)
        {
            ResetDpadHold();
            return;
        }

        if (_dpadUpHeld)
        {
            _dpadHoldTime += Time.unscaledDeltaTime;
            if (_dpadHoldTime >= holdTimeForMainMenu && mainMenu != null && CanOpenQuestMenus() && CanOpenMainMenu())
            {
                quickMenu.ShowPanel(false, ignoreRestrictions: true);
                mainMenu.ShowMenu();
                ResetDpadHold();
            }
        }
        else
        {
            ResetDpadHold();
        }
    }

    private void ResetDpadHold()
    {
        _dpadHoldTime = 0f;
        _dpadUpHeld = false;
    }

    private void HandleBPressed()
    {
        bool mainIsOpen = mainMenu != null && mainMenu.IsOpen;
        bool quickIsOpen = quickMenu != null && quickMenu.IsVisible;

        if (mainIsOpen)
        {
            mainMenu.HideMenu();
            if (quickMenu != null)
                quickMenu.ShowPanel(true, ignoreRestrictions: true);
        }
        else if (quickIsOpen)
        {
            quickMenu.ShowPanel(false, ignoreRestrictions: true);
        }
    }

    private void CloseAllMenus()
    {
        if (mainMenu != null)
            mainMenu.HideMenu();
        if (quickMenu != null)
            quickMenu.ShowPanel(false, ignoreRestrictions: true);
    }

    bool CanOpenMainMenu()
    {
        Debug.Log("[QuestMenuManager] Checking if main menu can open.");
        if (!GameState.CanOpenInventory)
        {
            Debug.LogWarning("[QuestMenuManager] Cannot open main menu. GameState.CanOpenInventory is false.");
            return false;
        }
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            Debug.LogWarning("[QuestMenuManager] Cannot open main menu. DialogueManager is open.");
            return false;
        }
        Debug.Log("[QuestMenuManager] Main menu can open.");
        return true;
    }

    bool CanOpenQuestMenus()
    {
        if (MenuManager.AnyOpen())
        {
            Debug.LogWarning("[QuestMenuManager] Cannot open quest menus because another menu is open.");
            return false;
        }

        if (!GameState.CanOpenInventory)
        {
            Debug.LogWarning("[QuestMenuManager] Cannot open quest menus. GameState denies inventory access.");
            return false;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            Debug.LogWarning("[QuestMenuManager] Cannot open quest menus. DialogueManager is open.");
            return false;
        }

        return true;
    }

}
