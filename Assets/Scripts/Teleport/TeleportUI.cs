using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// UI para seleccionar el destino de teletransporte.
/// Muestra una lista de puntos desbloqueados.
/// Navegar con D-pad/stick, A para confirmar, B para cerrar.
/// </summary>
public class TeleportUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Transform listContainer;
    [SerializeField] private GameObject itemPrefab;
    
    [Header("Animation")]
    [SerializeField] private float fadeInDuration = 0.25f;
    [SerializeField] private float fadeOutDuration = 0.15f;
    
    [Header("Input")]
    [SerializeField, Min(0f)] private float openIgnoreInputDuration = 0.15f;
    
    private const float NavRepeatDelay = 0.18f;
    
    private List<TeleportUIItem> _items = new();
    private int _selectedIndex = -1;
    private bool _isOpen;
    private float _nextNavTime;
    private float _ignoreInputUntil;
    private Tween _fadeTween;
    private bool _hasPushedInventory;
    private bool _hasBlockedMovement;
    private string _currentAnchorId; // Anchor actual para excluir de la lista
    
    public bool IsOpen => _isOpen;
    
    // Singleton
    private static TeleportUI _instance;
    public static TeleportUI Instance => _instance;
    
    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _instance = null;
    }
    #endif
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (windowRoot != null)
            windowRoot.SetActive(false);
    }
    
    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
    
    private void OnEnable()
    {
        GamepadInputReader.EnsureInputEventsSubscribed();
        GamepadInputReader.OnInput += HandleGamepadInput;
        TeleportRegistry.OnRegistryChanged += RefreshList;
    }
    
    private void OnDisable()
    {
        GamepadInputReader.OnInput -= HandleGamepadInput;
        TeleportRegistry.OnRegistryChanged -= RefreshList;

        // IMPORTANTE: si el objeto se desactiva o destruye con la UI abierta, liberar el
        // PushUIMode para que el contador de PlayerInputManager no quede desbalanceado.
        if (_isOpen)
        {
            _fadeTween?.Kill();
            if (_hasPushedInventory)
            {
                GameState.Pop(GamePhase.Inventory);
                _hasPushedInventory = false;
            }
            if (Core.PlayerInputManager.Instance != null)
                Core.PlayerInputManager.Instance.PopUIMode();
            if (_hasBlockedMovement)
            {
                if (PlayerService.TryGetComponent<Invector.vCharacterController.vThirdPersonInput>(out var tpi))
                {
                    tpi.enabled = false;
                    tpi.enabled = true;
                }
                _hasBlockedMovement = false;
            }
            _isOpen = false;
        }
    }
    
    private void HandleGamepadInput(GamepadInputReader.InputEvent input)
    {
        if (!_isOpen) return;
        if (Time.unscaledTime < _ignoreInputUntil) return;
        
        switch (input.Type)
        {
            case GamepadInputReader.InputEventType.Submit when input.Phase == InputActionPhase.Performed:
                OnConfirm();
                break;
                
            case GamepadInputReader.InputEventType.Cancel when input.Phase == InputActionPhase.Performed:
                Close();
                break;
                
            case GamepadInputReader.InputEventType.DpadUp:
                HandleVerticalNavigation(-1);
                break;
                
            case GamepadInputReader.InputEventType.DpadDown:
                HandleVerticalNavigation(+1);
                break;
                
            case GamepadInputReader.InputEventType.Navigate when input.Phase == InputActionPhase.Performed:
                HandleNavigateVector(input.Value);
                break;
        }
    }
    
    private bool CanNavigate() => Time.unscaledTime >= _nextNavTime;
    
    private void HandleNavigateVector(Vector2 value)
    {
        if (!CanNavigate()) return;
        
        if (value.y > 0.6f) HandleVerticalNavigation(-1);
        else if (value.y < -0.6f) HandleVerticalNavigation(+1);
    }
    
    private void HandleVerticalNavigation(int direction)
    {
        if (!CanNavigate()) return;
        
        NavigateItems(direction);
        _nextNavTime = Time.unscaledTime + NavRepeatDelay;
    }
    
    private void NavigateItems(int direction)
    {
        if (_items.Count == 0) return;
        
        int newIndex = _selectedIndex + direction;
        
        // Wrap around
        if (newIndex < 0) newIndex = _items.Count - 1;
        if (newIndex >= _items.Count) newIndex = 0;
        
        SelectItem(newIndex);
    }
    
    /// <summary>
    /// Abre la UI de teletransporte.
    /// </summary>
    /// <param name="currentAnchorId">AnchorId del punto actual para excluirlo de la lista. Si es null, usa SpawnManager.CurrentAnchorId.</param>
    public void Open(string currentAnchorId = null)
    {
        if (_isOpen) return;
        
        if (!TeleportRegistry.IsSystemAvailable)
        {
            Debug.LogWarning("[TeleportUI] No hay suficientes puntos desbloqueados.");
            return;
        }
        
        _isOpen = true;
        _ignoreInputUntil = Time.unscaledTime + openIgnoreInputDuration;
        
        // Guardar el anchor actual para excluirlo de la lista
        _currentAnchorId = currentAnchorId ?? SpawnManager.CurrentAnchorId;
        
        // Ocultar el hint mientras el menú está abierto
        if (TeleportHintUI.Instance != null)
            TeleportHintUI.Instance.ForceHide();
        
        // Cambiar a modo UI (deshabilita GamePlay, habilita UI)
        if (Core.PlayerInputManager.Instance != null)
            Core.PlayerInputManager.Instance.PushUIMode();
        
        // Bloquear gameplay
        GameState.Push(GamePhase.Inventory);
        _hasPushedInventory = true;

        // Deshabilitar vThirdPersonInput directamente para que Invector no lea input
        if (PlayerService.TryGetComponent<Invector.vCharacterController.vThirdPersonInput>(out var tpiOpen))
        {
            tpiOpen.enabled = false;
            _hasBlockedMovement = true;
        }
        
        // Poblar lista
        RefreshList();
        
        // Mostrar ventana con animación
        if (windowRoot != null)
            windowRoot.SetActive(true);
        
        _fadeTween?.Kill();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            _fadeTween = canvasGroup.DOFade(1f, fadeInDuration).SetUpdate(true);
        }
        
        // Seleccionar primer item
        if (_items.Count > 0)
            SelectItem(0);
        
        GamepadInputReader.PlayUISound("UI_Open");
        Debug.Log("[TeleportUI] UI abierta.");
    }
    
    /// <summary>
    /// Cierra la UI de teletransporte.
    /// </summary>
    public void Close()
    {
        if (!_isOpen) return;

        _isOpen = false;

        // Evitar que los botones A/B usados para cerrar/confirmar disparen acciones de gameplay
        // (salto, magia) en el mismo frame en que se re-habilita el input de gameplay.
        Core.GamepadInputReader.IgnoreJumpButton(0.3f);
        Core.GamepadInputReader.IgnoreCancelButton(0.3f);

        GamepadInputReader.PlayUISound("UI_Cancel");
        
        _fadeTween?.Kill();
        if (canvasGroup != null)
        {
            _fadeTween = canvasGroup.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (windowRoot != null)
                        windowRoot.SetActive(false);
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                });
        }
        else if (windowRoot != null)
        {
            windowRoot.SetActive(false);
        }
        
        // Restaurar vThirdPersonInput
        if (_hasBlockedMovement)
        {
            if (PlayerService.TryGetComponent<Invector.vCharacterController.vThirdPersonInput>(out var tpiClose))
            {
                tpiClose.enabled = false; // ciclo para resetear estado interno
                tpiClose.enabled = true;
            }
            _hasBlockedMovement = false;
        }

        // Restaurar gameplay
        if (_hasPushedInventory)
        {
            GameState.Pop(GamePhase.Inventory);
            _hasPushedInventory = false;
        }
        
        // Restaurar controles de gameplay
        if (Core.PlayerInputManager.Instance != null)
            Core.PlayerInputManager.Instance.PopUIMode();
        
        Debug.Log("[TeleportUI] UI cerrada.");
    }
    
    private void RefreshList()
    {
        // Limpiar lista actual
        foreach (var item in _items)
        {
            if (item != null && item.gameObject != null)
                Destroy(item.gameObject);
        }
        _items.Clear();
        _selectedIndex = -1;
        
        if (itemPrefab == null || listContainer == null)
        {
            Debug.LogError("[TeleportUI] itemPrefab o listContainer no asignados.");
            return;
        }
        
        // Obtener destinos disponibles (excluyendo posición actual)
        string currentAnchor = _currentAnchorId ?? SpawnManager.CurrentAnchorId;
        Debug.Log($"[TeleportUI] Anchor actual a excluir: '{currentAnchor}'. Puntos totales: {TeleportRegistry.UnlockedCount}");
        
        var destinations = TeleportRegistry.GetAvailableDestinations(currentAnchor);
        Debug.Log($"[TeleportUI] Destinos disponibles (excluyendo actual): {destinations.Count}");
        
        foreach (var point in destinations)
        {
            Debug.Log($"[TeleportUI] Añadiendo destino: {point.displayName} ({point.anchorId})");
            var itemGo = Instantiate(itemPrefab, listContainer);
            var itemComponent = itemGo.GetComponent<TeleportUIItem>();
            
            if (itemComponent == null)
            {
                itemComponent = itemGo.AddComponent<TeleportUIItem>();
            }
            
            itemComponent.Setup(point, OnItemClicked);
            _items.Add(itemComponent);
        }
        
        // Si no hay destinos, cerrar
        if (_items.Count == 0 && _isOpen)
        {
            Debug.Log("[TeleportUI] No hay destinos disponibles.");
            Close();
        }
    }
    
    private void OnItemClicked(TeleportPointData point)
    {
        int index = _items.FindIndex(i => i.PointData.anchorId == point.anchorId);
        if (index >= 0)
        {
            SelectItem(index);
            OnConfirm(); // Click directo = seleccionar y confirmar
        }
    }
    
    private void SelectItem(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        
        // Deseleccionar anterior
        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            _items[_selectedIndex].SetSelected(false);
        
        _selectedIndex = index;
        _items[index].SetSelected(true);
        
        GamepadInputReader.PlayUISound("UI_Navigate");
    }
    
    private void OnConfirm()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count) return;
        
        var selectedPoint = _items[_selectedIndex].PointData;
        if (selectedPoint == null) return;
        
        GamepadInputReader.PlayUISound("UI_Confirm");
        
        // Cerrar UI antes de teletransportar
        Close();
        
        // Iniciar teletransporte
        if (TeleportSystem.Instance != null)
        {
            TeleportSystem.Instance.TeleportTo(selectedPoint.anchorId);
        }
        else
        {
            // Fallback: usar SpawnManager directamente
            Debug.LogWarning("[TeleportUI] TeleportSystem no encontrado, usando teletransporte directo.");
            SpawnManager.TeleportTo(selectedPoint.anchorId, true);
        }
    }
}

