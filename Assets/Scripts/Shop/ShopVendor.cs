using UnityEngine;

/// <summary>
/// Componente que se añade al NPC para abrir una instancia de ShopUI al interactuar.
/// </summary>
[RequireComponent(typeof(ShopController))]
public class ShopVendor : MonoBehaviour
{
    [SerializeField] private ShopUI shopUIPrefab;
    [SerializeField] private Transform uiParent;

    ShopUI _runtimeUI;
    ShopController _controller;

    void Awake()
    {
        _controller = GetComponent<ShopController>();
        if (uiParent == null)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                uiParent = canvas.transform;
        }
    }

    public void Interact()
    {
        if (_runtimeUI == null)
        {
            if (shopUIPrefab == null)
            {
                Debug.LogWarning("[ShopVendor] No se ha asignado el prefab de ShopUI.");
                return;
            }
            _runtimeUI = Instantiate(shopUIPrefab, uiParent ?? transform);
        }

        _runtimeUI.gameObject.SetActive(true);
        _runtimeUI.BindController(_controller);
        _runtimeUI.Open();
    }

    public void CloseShop()
    {
        if (_runtimeUI != null)
            _runtimeUI.Close();
    }
}
