using UnityEngine;

/// <summary>
/// Componente que se añade al NPC para abrir una instancia de ShopUI al interactuar.
/// El ShopUI prefab debe tener ya configurado su ShopController con el inventario de la tienda.
/// Requiere un componente Interactable en el mismo GameObject.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class ShopVendor : MonoBehaviour
{
    [SerializeField] private ShopUI shopUIPrefab;

    ShopUI _runtimeUI;
    Interactable _interactable;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
    }

    void OnEnable()
    {
        if (_interactable != null)
        {
            // Suscribirse a OnFinished: se dispara cuando el diálogo termina
            _interactable.OnFinished.AddListener(OnDialogueFinished);
        }
    }

    void OnDisable()
    {
        if (_interactable != null)
        {
            _interactable.OnFinished.RemoveListener(OnDialogueFinished);
        }
    }

    void OnDialogueFinished()
    {
        // Cuando el diálogo del vendedor termina, abrir la tienda
        OpenShop();
    }

    public void OpenShop()
    {
        StartCoroutine(OpenShopNextFrame());
    }

    System.Collections.IEnumerator OpenShopNextFrame()
    {
        // Esperar al final de frame para que DialogueManager termine de cerrar y libere GameState/Input
        yield return new WaitForEndOfFrame();

        if (_runtimeUI == null)
        {
            if (shopUIPrefab == null)
            {
                Debug.LogWarning("[ShopVendor] No se ha asignado el prefab de ShopUI.");
                yield break;
            }
            _runtimeUI = Instantiate(shopUIPrefab);
        }

        _runtimeUI.gameObject.SetActive(true);
        _runtimeUI.Open();
    }

    public void CloseShop()
    {
        if (_runtimeUI != null)
            _runtimeUI.Close();
    }
}
