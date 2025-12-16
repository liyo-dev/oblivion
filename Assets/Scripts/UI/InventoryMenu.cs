using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Simple inventory menu that builds a list of `InventoryItemRow` from the player's Inventory.
    /// Attach to a root GameObject (menu panel). Assign `rowPrefab` and `contentParent`.
    /// </summary>
    public class InventoryMenu : MonoBehaviour
    {
        [Header("References")]
        public GameObject rowPrefab;
        public Transform contentParent;

        [Header("Settings")]
        public bool startHidden = true;

        private Inventory _inventory;
        private float _nextToggleTime;

        void Awake()
        {
            gameObject.SetActive(!startHidden);
            TryBindPlayer();
            PlayerService.OnPlayerRegistered += OnPlayerRegistered;

            Core.GamepadInputReader.EnsureInputEventsSubscribed();
            Core.GamepadInputReader.OnInput += HandleGamepadInput;
        }

        void OnDestroy()
        {
            PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
            UnsubscribeInventory();
            Core.GamepadInputReader.OnInput -= HandleGamepadInput;
        }

        private void OnPlayerRegistered(GameObject player) => TryBindPlayer();

        private void TryBindPlayer()
        {
            UnsubscribeInventory();
            if (PlayerService.TryGetComponent<Inventory>(out var inv, includeInactive: true, allowSceneLookup: true))
            {
                _inventory = inv;
                _inventory.OnInventoryChanged += OnInventoryChanged;
            }
        }

        private void UnsubscribeInventory()
        {
            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= OnInventoryChanged;
                _inventory = null;
            }
        }

        private void OnInventoryChanged(ItemData item, int newAmount)
        {
            // Si el menú está abierto, refresca para mostrar el cambio
            if (gameObject.activeSelf)
                Refresh();
        }

        private void HandleGamepadInput(Core.GamepadInputReader.InputEvent input)
        {
            if (input.Type != Core.GamepadInputReader.InputEventType.DpadDown || input.Phase != InputActionPhase.Performed)
                return;

            if (Time.unscaledTime < _nextToggleTime)
                return;

            ToggleMenu();
            _nextToggleTime = Time.unscaledTime + 0.35f;
        }

        public void ToggleMenu()
        {
            bool next = !gameObject.activeSelf;
            gameObject.SetActive(next);
            if (next)
            {
                Refresh();
                StartCoroutine(SelectFirstItemDelayed());
            }
        }

        public void Refresh()
        {
            if (_inventory == null)
            {
                TryBindPlayer();
                if (_inventory == null) return;
            }

            if (contentParent == null || rowPrefab == null) return;

            // Limpiar hijos anteriores
            for (int i = contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(contentParent.GetChild(i).gameObject);
            }

            var items = _inventory.GetAllItems();
            var rows = new System.Collections.Generic.List<InventoryItemRow>();
            
            foreach (var e in items)
            {
                var go = Instantiate(rowPrefab, contentParent);
                var row = go.GetComponent<InventoryItemRow>();
                if (row != null)
                {
                    row.Setup(e.item, e.count);
                    rows.Add(row);
                }
            }

            // Configurar navegación explícita entre items
            ConfigureNavigation(rows);
        }

        private void ConfigureNavigation(System.Collections.Generic.List<InventoryItemRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var button = rows[i].GetButton();
                if (button == null) continue;

                var nav = new Navigation { mode = Navigation.Mode.Explicit };
                
                // Configurar navegación vertical
                if (i > 0)
                    nav.selectOnUp = rows[i - 1].GetButton();
                if (i < rows.Count - 1)
                    nav.selectOnDown = rows[i + 1].GetButton();
                
                button.navigation = nav;
            }
        }

        private System.Collections.IEnumerator SelectFirstItemDelayed()
        {
            // Esperar un frame para que los elementos estén listos
            yield return null;
            
            if (contentParent == null || contentParent.childCount == 0) yield break;

            // Buscar el primer Selectable en los hijos
            for (int i = 0; i < contentParent.childCount; i++)
            {
                var child = contentParent.GetChild(i);
                var selectable = child.GetComponentInChildren<Selectable>();
                if (selectable != null && selectable.interactable && selectable.gameObject.activeInHierarchy)
                {
                    yield return null; // Esperar otro frame
                    selectable.Select();
                    Debug.Log($"[InventoryMenu] Selected: {selectable.gameObject.name}");
                    break;
                }
            }
        }
    }
}
