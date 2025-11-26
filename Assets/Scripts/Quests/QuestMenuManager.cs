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

    private bool _lastFrameDpadUp;
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
        bool quickIsOpen = quickMenu != null && quickMenu.IsVisible;
        bool mainIsOpen = mainMenu != null && mainMenu.IsOpen;

        if (!quickIsOpen && !mainIsOpen)
        {
            if (quickMenu != null)
                quickMenu.ShowPanel(true, ignoreRestrictions: true);
        }
        else if (quickIsOpen && !mainIsOpen)
        {
            if (mainMenu != null && CanOpenMainMenu())
            {
                if (quickMenu != null)
                    quickMenu.ShowPanel(false, ignoreRestrictions: true);
                mainMenu.ShowMenu();
                ResetDpadHold();
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
        if (!GameState.CanOpenInventory) return false;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return false;
        return true;
    }

    private bool DetectDpadUpPressed()
    {
        bool dpadUpPressed = GamepadInputReader.DpadUpPressed;

        float dpadVertical = 0f;
        var nav = GamepadInputReader.Navigation;
        dpadVertical = nav.y;
        bool axisPressed = dpadVertical > 0.5f && !_lastFrameDpadUp;
        _lastFrameDpadUp = dpadVertical > 0.5f;

        return dpadUpPressed || axisPressed;
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
