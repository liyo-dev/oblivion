using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

public class QuestMenuManager : MonoBehaviour
{
    public static bool IsAnyQuestMenuOpen { get; private set; }
    private const float NavigateUpThreshold = 0.6f;

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
    private bool _menuRegistered;
    private InputScope _inputScope;

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
        TearDownMenuRegistration();
        ExitUiScope();
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

        HandleTabSwitchInput();

        RefreshMenuRegistration();
    }

    /// <summary>
    /// Cambia entre misiones visibles/archivadas mientras el menú principal está abierto.
    /// IMPORTANTE: se lee del Action Map UI (LeftShoulderPressedUI/RightShoulderPressedUI), NO del
    /// evento LeftShoulder/RightShoulder del Action Map GamePlay. Ese Action Map se deshabilita
    /// mientras el menú principal está abierto (ver PlayerInputManager.PushUIMode), así que la
    /// rueda del ratón (mapeada a GamePlay.ShoulderLeft/ShoulderRight en teclado+ratón) dejaba de
    /// responder justo cuando el panel estaba visible. LB/RB del Action Map UI sí permanecen
    /// activos en ese momento.
    /// </summary>
    private void HandleTabSwitchInput()
    {
        if (mainMenu == null || !mainMenu.IsOpen) return;

        if (GamepadInputReader.LeftShoulderPressedUI)
            mainMenu.ShowVisibleTab();
        else if (GamepadInputReader.RightShoulderPressedUI)
            mainMenu.ShowHiddenTab();
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
            case GamepadInputReader.InputEventType.Navigate:
                HandleNavigateInput(input);
                break;
            // LeftShoulder/RightShoulder (Action Map GamePlay) YA NO se usan aquí para cambiar de
            // pestaña: ese Action Map se deshabilita mientras el menú principal está abierto (modo
            // UI), justo cuando este atajo hace falta. Ver HandleTabSwitchInput(), que lee del
            // Action Map UI en su lugar y sí sigue activo con el menú abierto.
        }
    }

    void HandleNavigateInput(GamepadInputReader.InputEvent input)
    {
        // El menú de misiones solo debe reaccionar al D-Pad, no al stick analógico.
        // Como los bindings de UI.Navigate pueden provenir de ambos, consultamos el
        // DpadRaw para asegurarnos de ignorar movimientos del joystick.
        var dpad = GamepadInputReader.DpadRaw;

        if (input.Phase == InputActionPhase.Canceled || dpad.sqrMagnitude < 0.0001f)
        {
            _dpadUpHeld = false;
            return;
        }

        if (dpad.y > NavigateUpThreshold)
        {
            _dpadUpPressed = true;
            _dpadUpHeld = true;
        }
        else if (dpad.y < -NavigateUpThreshold)
        {
            _dpadUpHeld = false;
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
        bool quickIsOpen = quickMenu != null && quickMenu.IsVisible;
        bool mainIsOpen = mainMenu != null && mainMenu.IsOpen;

        if (!quickIsOpen && !mainIsOpen)
        {
            if (!CanOpenQuestMenus())
            {
                return;
            }

            if (quickMenu == null)
            {
                Debug.LogWarning("[QuestMenuManager] quickMenu reference is null - cannot open quick menu.");
                return;
            }

            // Asegurar que el GameObject del componente esté activo para permitir coroutines/animaciones
            if (!quickMenu.gameObject.activeInHierarchy)
            {
                quickMenu.gameObject.SetActive(true);

                // Activar ancestros inactivos si es necesario
                var inactiveAncestors = new System.Collections.Generic.List<Transform>();
                var p = quickMenu.transform.parent;
                while (p != null)
                {
                    if (!p.gameObject.activeSelf)
                        inactiveAncestors.Add(p);
                    p = p.parent;
                }

                // Enable ancestors from top-down
                for (int i = inactiveAncestors.Count - 1; i >= 0; i--)
                {
                    inactiveAncestors[i].gameObject.SetActive(true);
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
        RefreshMenuRegistration();
    }

    bool CanOpenMainMenu()
    {
        if (!GameState.CanOpenInventory)
            return false;
        
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            return false;
        
        return true;
    }

    bool CanOpenQuestMenus()
    {
        // Ignorar el propio registro del menú de misiones cuando ya estamos
        // dentro de la transición entre rápido y principal. Solo bloqueamos si
        // hay otros menús abiertos.
        if (MenuManager.AnyOpenExcept(MenuKind.Mission))
            return false;

        if (!GameState.CanOpenInventory)
            return false;

        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
            return false;

        return true;
    }

    void RefreshMenuRegistration()
    {
        bool anyOpen = (mainMenu != null && mainMenu.IsOpen) || (quickMenu != null && quickMenu.IsVisible);
        bool mainMenuOpen = mainMenu != null && mainMenu.IsOpen;

        if (anyOpen)
        {
            IsAnyQuestMenuOpen = true;
            if (!_menuRegistered)
            {
                MenuManager.RegisterOpen(MenuKind.Mission);
                _menuRegistered = true;
            }
            
            // IMPORTANTE: Solo bloquear inputs de gameplay si el menú PRINCIPAL está abierto
            // El menú rápido debe permitir que el jugador se mueva
            if (mainMenuOpen)
            {
                EnsureUiScope();
            }
            else
            {
                // Si solo está el menú rápido, NO bloquear gameplay
                ExitUiScope();
            }
        }
        else
        {
            TearDownMenuRegistration();
        }
    }

    void TearDownMenuRegistration()
    {
        if (_menuRegistered)
        {
            MenuManager.Close(MenuKind.Mission);
            _menuRegistered = false;
        }
        IsAnyQuestMenuOpen = false;
        ExitUiScope();
    }

    void EnsureUiScope()
    {
        if (_inputScope != null) return;
        _inputScope = InputScope.Enter();
    }

    void ExitUiScope()
    {
        _inputScope?.Dispose();
        _inputScope = null;
    }

    sealed class InputScope : IDisposable
    {
        bool _disposed;

        InputScope()
        {
            GamepadInputReader.PushGameplaySuppression(this);

            // Cambiar a modo UI centralizado
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PushUIMode();
        }

        public static InputScope Enter()
        {
            return new InputScope();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            // Restaurar modo Gameplay centralizado
            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();

            GamepadInputReader.PopGameplaySuppression(this);
        }
    }
}
