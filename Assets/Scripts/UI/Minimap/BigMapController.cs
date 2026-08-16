using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Core;

/// <summary>
/// Amplía el minimapa circular a un "mapa grande" centrado en pantalla al pulsar el botón
/// ToggleBigMap (tecla M en teclado, botón Select/View/Back/"-" del mando — ver
/// PlayerControls.inputactions e InputGlyphNames.Select). Reutiliza el mismo RectTransform
/// "MinimapRoot" que ya usan MinimapController/MinimapUIController (mismo RawImage, máscara
/// circular, marcadores y flecha del jugador) en vez de crear una UI paralela: solo cambia su
/// ancla/posición/escala y el zoom de la cámara del minimapa mientras está abierto.
///
/// Se registra en <see cref="MenuManager"/> como <see cref="MenuKind.BigMap"/> siguiendo el mismo
/// patrón que <see cref="QuestMenuManager"/>: bloquea que se abra si hay otro menú activo, y usa el
/// mismo InputScope (PushUIMode + PushGameplaySuppression) para congelar el movimiento del jugador
/// mientras el mapa grande está en pantalla.
/// </summary>
[DisallowMultipleComponent]
public class BigMapController : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsOpen = false;
#endif

    [Header("Referencias")]
    [Tooltip("RectTransform \"MinimapRoot\" — el mismo que usan MinimapController/MinimapUIController.")]
    [SerializeField] RectTransform minimapRoot;
    [SerializeField] MinimapController minimapController;

    [Header("Mapa grande")]
    [Tooltip("Multiplicador de escala del minimapa al abrir el mapa grande.")]
    [SerializeField] float bigScale = 3f;

    // Estado original de minimapRoot, para restaurarlo exactamente al cerrar.
    Vector2 _originalAnchorMin;
    Vector2 _originalAnchorMax;
    Vector2 _originalPivot;
    Vector2 _originalAnchoredPosition;
    Vector3 _originalScale;
    bool _originalStateCaptured;

    InputScope _inputScope;

    void Awake()
    {
        CaptureOriginalState();
    }

    void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleInput;
    }

    void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleInput;

        // Si este componente se desactiva mientras el mapa grande está abierto (cambio de escena,
        // minimapa oculto por interior/batalla, etc.), forzar el cierre para no dejar el input de
        // gameplay suprimido ni el registro de MenuManager huérfano.
        if (IsOpen)
            Close();
    }

    void CaptureOriginalState()
    {
        if (_originalStateCaptured || minimapRoot == null) return;

        _originalAnchorMin = minimapRoot.anchorMin;
        _originalAnchorMax = minimapRoot.anchorMax;
        _originalPivot = minimapRoot.pivot;
        _originalAnchoredPosition = minimapRoot.anchoredPosition;
        _originalScale = minimapRoot.localScale;
        _originalStateCaptured = true;
    }

    void HandleInput(GamepadInputReader.InputEvent input)
    {
        if (input.Phase != InputActionPhase.Performed) return;

        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.ToggleBigMap:
                Toggle();
                break;
            case GamepadInputReader.InputEventType.Cancel:
                if (IsOpen) Close();
                break;
        }
    }

    void Toggle()
    {
        if (IsOpen) Close();
        else TryOpen();
    }

    void TryOpen()
    {
        if (IsOpen || minimapRoot == null) return;

        // Mismas comprobaciones conservadoras que el resto de menús (QuestMenuManager, ShopUI...):
        // no abrir encima de otro menú, diálogo o si el juego no permite abrir "inventario" ahora
        // mismo (cinemática, combate bloqueante, etc.).
        if (MenuManager.AnyOpenExcept(MenuKind.BigMap)) return;
        if (!GameState.CanOpenInventory) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen) return;

        CaptureOriginalState();

        minimapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        minimapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        minimapRoot.pivot = new Vector2(0.5f, 0.5f);
        minimapRoot.anchoredPosition = Vector2.zero;
        minimapRoot.localScale = _originalScale * bigScale;

        if (minimapController != null)
            minimapController.SetBigMapMode(true);

        IsOpen = true;
        MenuManager.TryOpen(MenuKind.BigMap);
        EnsureInputScope();

        GamepadInputReader.PlayUISound("UI_Open");
    }

    void Close()
    {
        if (!IsOpen) return;

        if (minimapRoot != null && _originalStateCaptured)
        {
            minimapRoot.anchorMin = _originalAnchorMin;
            minimapRoot.anchorMax = _originalAnchorMax;
            minimapRoot.pivot = _originalPivot;
            minimapRoot.anchoredPosition = _originalAnchoredPosition;
            minimapRoot.localScale = _originalScale;
        }

        if (minimapController != null)
            minimapController.SetBigMapMode(false);

        IsOpen = false;
        MenuManager.Close(MenuKind.BigMap);
        ExitInputScope();

        GamepadInputReader.PlayUISound("UI_Cancel");
    }

    void EnsureInputScope()
    {
        if (_inputScope != null) return;
        _inputScope = InputScope.Enter();
    }

    void ExitInputScope()
    {
        _inputScope?.Dispose();
        _inputScope = null;
    }

    /// <summary>
    /// Mismo patrón que QuestMenuManager.InputScope: congela el movimiento del jugador
    /// (PushGameplaySuppression) y pasa el input a modo UI centralizado (PushUIMode) mientras el
    /// mapa grande está abierto. Cancel/ToggleBigMap siguen llegando igualmente porque
    /// GamepadInputReader.ShouldSuppress solo filtra el D-Pad durante la supresión de gameplay.
    /// </summary>
    sealed class InputScope : IDisposable
    {
        bool _disposed;

        InputScope()
        {
            GamepadInputReader.PushGameplaySuppression(this);

            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PushUIMode();
        }

        public static InputScope Enter() => new InputScope();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (ServiceLocator.TryGet(out Core.PlayerInputManager pim))
                pim.PopUIMode();

            GamepadInputReader.PopGameplaySuppression(this);
        }
    }
}
