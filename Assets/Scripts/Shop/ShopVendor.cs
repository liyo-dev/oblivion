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
    System.Collections.IEnumerator _openingCoroutine;

    void Awake()
    {
        _interactable = GetComponent<Interactable>() ?? GetComponentInParent<Interactable>() ?? GetComponentInChildren<Interactable>();
    }

    void OnEnable()
    {
        if (_interactable != null)
        {
            // Suscribirse a OnFinished: se dispara cuando el diálogo termina
            _interactable.OnFinished.AddListener(OnDialogueFinished);
            Debug.Log($"[ShopVendor] Subscribed to Interactable.OnFinished on {_interactable.gameObject.name}");
            // También suscribirse a OnInteract para arrancar un wait-and-open que no dependa exclusivamente de OnFinished
            _interactable.OnInteract.AddListener(OnInteractStart);
        }
        else
        {
            Debug.LogWarning("[ShopVendor] No Interactable found to subscribe OnFinished. Shop will not open automatically.");
        }
    }

    void OnDisable()
    {
        if (_interactable != null)
        {
            _interactable.OnFinished.RemoveListener(OnDialogueFinished);
            _interactable.OnInteract.RemoveListener(OnInteractStart);
            Debug.Log($"[ShopVendor] Unsubscribed from Interactable.OnFinished on {_interactable.gameObject.name}");
        }
    }

    void OnDialogueFinished()
    {
        Debug.Log("[ShopVendor] OnDialogueFinished called - attempting to open shop");
        // Cuando el diálogo del vendedor termina, abrir la tienda
        OpenShop();
    }

    void OnInteractStart(GameObject interactor)
    {
        // If there is dialogue, wait until it finishes; otherwise open immediately after next frame
        if (_openingCoroutine != null)
            StopCoroutine(_openingCoroutine);
        _openingCoroutine = WaitForDialogueEndAndOpen();
        StartCoroutine(_openingCoroutine);
    }

    System.Collections.IEnumerator WaitForDialogueEndAndOpen()
    {
        // If DialogueManager exists and is open, wait until it closes
        var dm = DialogueManager.Instance;
        if (dm != null && dm.IsOpen)
        {
            // Preferred behavior: wait until the last line is fully shown (typewriter finished)
            // so the shop opens immediately after the NPC finishes speaking (no press required).
            int maxChecks = 600; // safety timeout (~10s at 60fps)
            int checks = 0;
            while (checks < maxChecks)
            {
                checks++;
                // If we can detect last line index and that typing finished, break and open
                int currentIndex = dm.CurrentIndex;
                int total = dm.CurrentLineCount;
                bool typing = dm.IsTyping;

                if (total > 0 && currentIndex == total - 1 && !typing)
                {
                    // give one frame to let UI settle
                    yield return new WaitForEndOfFrame();
                    break;
                }

                // If the dialogue was closed early, fall back
                if (!dm.IsOpen) break;

                yield return null;
            }
        }

        // Now attempt to open the shop (uses existing retry logic)
        yield return OpenShopNextFrame();
    }

    public void OpenShop()
    {
        StartCoroutine(OpenShopNextFrame());
    }

    System.Collections.IEnumerator OpenShopNextFrame()
    {
        Debug.Log("[ShopVendor] OpenShopNextFrame started");
        // Esperar al final de frame para que DialogueManager termine de cerrar y libere GameState/Input
        yield return new WaitForEndOfFrame();

        // Esperar unos frames extra hasta que el estado de diálogo se haya salido.
        // Esto evita que MenuManager deniegue la apertura mientras Dialogue sigue activo.
        int waitFrames = 0;
        while (waitFrames < 10 && GameState.Is(GamePhase.Dialogue))
        {
            waitFrames++;
            yield return null;
        }

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

        // Try to open the shop UI. If MenuManager blocks the open, retry for a few frames.
        int attempts = 0;
        const int maxAttempts = 20;
        while (attempts < maxAttempts)
        {
            attempts++;
            _runtimeUI.Open();
            yield return null; // give one frame for MenuManager/Open flows to update
            if (_runtimeUI.IsOpen)
            {
                if (attempts > 1)
                    Debug.Log($"[ShopVendor] Shop opened after {attempts} attempts.");
                yield break;
            }

            // Log why opening was denied from MenuManager side (best-effort)
            Debug.Log($"[ShopVendor] Attempt {attempts}: ShopUI.Open did not result in IsOpen=true. Retrying...");
        }

        Debug.LogWarning("[ShopVendor] Failed to open ShopUI after multiple attempts. MenuManager likely denied the open.");
    }

    public void CloseShop()
    {
        if (_runtimeUI != null)
            _runtimeUI.Close();
    }
}
