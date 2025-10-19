using UnityEngine;

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

        void Awake()
        {
            gameObject.SetActive(!startHidden);
            TryBindPlayer();
            PlayerService.OnPlayerRegistered += OnPlayerRegistered;
        }

        void OnDestroy()
        {
            PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
            UnsubscribeInventory();
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

        void Update()
        {
            // Toggle menu con la tecla I
            if (Input.GetKeyDown(KeyCode.I))
            {
                ToggleMenu();
            }
        }

        public void ToggleMenu()
        {
            bool next = !gameObject.activeSelf;
            gameObject.SetActive(next);
            if (next) Refresh();
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
            foreach (var e in items)
            {
                var go = Instantiate(rowPrefab, contentParent);
                var row = go.GetComponent<InventoryItemRow>();
                if (row != null)
                {
                    row.Setup(e.item, e.count);
                }
            }
        }
    }
}
