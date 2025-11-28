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

    private void Awake()
    {
        if (quickMenu != null) quickMenu.ShowPanel(false, ignoreRestrictions: true);
        if (mainMenu != null) mainMenu.HideMenu();
    }

    void OnEnable()
    {
        if (autoShowQuickOnQuestInit)
        {
            _autoShowRoutine = StartCoroutine(AutoShowQuickMenuRoutine());
        }
    }

    void OnDisable()
    {
        if (_autoShowRoutine != null)
        {
            StopCoroutine(_autoShowRoutine);
            _autoShowRoutine = null;
        }
    }

    private void Update()
    {
        if (DetectStartPressed())
        {
            CloseAllMenus();
            return;
        }

        HandleDpadUpHeld();

        if (DetectDpadUpPressed())
        {
            HandleDpadUpPressed();
        }

        if (DetectBPressed())
        {
            HandleBPressed();
        }
    }

    private void HandleDpadUpPressed()
    {
        Debug.Log("[QuestMenuManager] HandleDpadUpPressed called.");
        bool quickIsOpen = quickMenu != null && quickMenu.IsVisible;
        bool mainIsOpen = mainMenu != null && mainMenu.IsOpen;

        Debug.Log($"[QuestMenuManager] Quick menu open: {quickIsOpen}, Main menu open: {mainIsOpen}");

        if (!quickIsOpen && !mainIsOpen)
        {
            if (quickMenu != null)
            {
                Debug.Log("[QuestMenuManager] Opening quick menu.");
                quickMenu.ShowPanel(true, ignoreRestrictions: true);
            }
        }
        else if (quickIsOpen && !mainIsOpen)
        {
            if (mainMenu != null && CanOpenMainMenu())
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

        if (DetectDpadUpHeld())
        {
            _dpadHoldTime += Time.unscaledDeltaTime;
            if (_dpadHoldTime >= holdTimeForMainMenu && mainMenu != null && CanOpenMainMenu())
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

    private bool DetectDpadUpHeld()
    {
        float dpadVertical = 0f;
        var nav = GamepadInputReader.Navigation;
        dpadVertical = nav.y;
        return dpadVertical > 0.5f;
    }

    private void ResetDpadHold()
    {
        _dpadHoldTime = 0f;
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

    private bool DetectDpadUpPressed()
    {
        bool dpadUpPressed = GamepadInputReader.DpadUpPressed || GamepadInputReader.NavigateUp;
        Debug.Log($"[QuestMenuManager] DetectDpadUpPressed: {dpadUpPressed}");
        return dpadUpPressed;
    }

    private bool DetectBPressed()
    {
        return GamepadInputReader.CancelPressed;
    }

    private bool DetectStartPressed()
    {
        return GamepadInputReader.StartPressed;
    }

    private System.Collections.IEnumerator AutoShowQuickMenuRoutine()
    {
        while (autoShowQuickOnQuestInit)
        {
            var current = QuestManager.Instance;
            if (current == null)
            {
                _lastQuestManager = null;
            }
            else if (current != _lastQuestManager)
            {
                _lastQuestManager = current;
                if (autoShowDelay > 0f)
                    yield return new WaitForSecondsRealtime(autoShowDelay);
                else
                    yield return null;

                if (quickMenu != null)
                    quickMenu.ShowPanel(true, ignoreRestrictions: true);
            }

            yield return null;
        }
    }
}
