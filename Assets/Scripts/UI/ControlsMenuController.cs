using System.Linq;
using Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Panel "Controles" del menú principal: lista todas las acciones del juego con su icono/tecla
/// según el dispositivo activo (ver Core.InputGlyphs.ControlsSchemeConfig + ControlRowWidget).
/// Sigue el mismo patrón de apertura/cierre que SettingsMenuController (Show/Close, Start/Cancel
/// del gamepad para cerrar, período de gracia tras abrir) para que MainMenuController lo pueda
/// tratar exactamente igual que ya trata Ajustes.
/// </summary>
[DisallowMultipleComponent]
public class ControlsMenuController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;
    [SerializeField] private Selectable firstSelection;

    [Header("Datos")]
    [SerializeField] private Core.InputGlyphs.ControlsSchemeConfig scheme;

    [Header("Filas")]
    [Tooltip("Contenedor con Vertical Layout Group (normalmente el Content de un ScrollRect) donde se instancian las filas.")]
    [SerializeField] private Transform rowsContainer;
    [SerializeField] private ControlRowWidget rowPrefab;

    [Header("Navegación")]
    [SerializeField, Min(0f), Tooltip("Tiempo mínimo tras abrir antes de aceptar una orden de cierre (evita cerrar con el mismo pulso que abrió el panel).")]
    private float cancelInputGracePeriod = 0.25f;

    System.Action _onClosed;
    EventSystem _eventSystem;
    float _openedAt = -999f;
    bool _rowsBuilt;

    public bool IsVisible => root != null && root.activeInHierarchy;

    void Awake()
    {
        if (!root) root = gameObject;
        _eventSystem = EventSystem.current;
    }

    void Update()
    {
        if (root == null || !root.activeInHierarchy) return;
        if (Time.unscaledTime - _openedAt < cancelInputGracePeriod) return;

        // Mismo mapeo que SettingsMenuController: Start o Cancel (B) cierran el panel.
        if (GamepadInputReader.StartPressed || GamepadInputReader.CancelPressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ControlsMenu-Debug] Update() detectó cierre — Start={GamepadInputReader.StartPressed}, Cancel={GamepadInputReader.CancelPressed}");
#endif
            Close();
        }
    }

    public void Show(System.Action onClosed = null)
    {
        _onClosed = onClosed;
        BuildRowsIfNeeded();

        if (root && !root.activeSelf)
            root.SetActive(true);

        if (!_eventSystem) _eventSystem = EventSystem.current;
        if (_eventSystem != null)
        {
            // Igual que SettingsMenuController.ResolveInitialSelection: si no hay firstSelection
            // asignado a mano en el Inspector, cae a buscar el primer Selectable interactable
            // dentro del panel en vez de dejar el EventSystem sin selección (root podía quedar
            // sin nada seleccionado si el panel no tenía ningún Selectable navegable).
            var target = firstSelection != null
                ? firstSelection.gameObject
                : root.GetComponentsInChildren<Selectable>(true)
                    .FirstOrDefault(s => s != null && s.IsActive() && s.interactable)?.gameObject;

            if (target != null)
                _eventSystem.SetSelectedGameObject(target);
        }

        _openedAt = Time.unscaledTime;
    }

    public void Close(bool silent = false)
    {
        bool wasVisible = root && root.activeSelf;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[ControlsMenu-Debug] Close(silent={silent}) — wasVisible={wasVisible}, " +
                  $"root={(root ? root.name : "NULL")}, hasOnClosedCallback={_onClosed != null}");
#endif

        if (!silent && wasVisible)
            AudioService.Instance?.PlaySFX("UI_Cancel", 1f);

        if (wasVisible)
            root.SetActive(false);

        _onClosed?.Invoke();
        _onClosed = null;
    }

    void BuildRowsIfNeeded()
    {
        // Las filas se instancian una única vez (no en cada Show/Close): cada ControlRowWidget ya
        // se refresca solo cuando cambia InputGlyphService.CurrentFamily, así que reconstruir la
        // lista entera cada vez que se abre el panel solo generaría Instantiate/Destroy de sobra.
        if (_rowsBuilt) return;
        _rowsBuilt = true;

        if (!scheme || !rowPrefab || !rowsContainer)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[ControlsMenu] Faltan referencias (scheme/rowPrefab/rowsContainer) — no se puede poblar la lista de controles.");
#endif
            return;
        }

        foreach (var entry in scheme.entries)
        {
            var row = Instantiate(rowPrefab, rowsContainer);
            row.Bind(entry);
        }
    }
}
