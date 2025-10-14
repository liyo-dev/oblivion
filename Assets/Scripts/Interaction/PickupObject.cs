using UnityEngine;

[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class PickupObject : MonoBehaviour
{
    [Header("Configuración")]
    [SerializeField] private ObjectType objectType = ObjectType.Caja;
    [SerializeField] private bool canBeDropped = true;

    private Interactable _interactable;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
    }

    void Start()
    {
        if (_interactable == null)
        {
            Debug.LogError($"[PickupObject] Falta Interactable en {name}");
            return;
        }

        // Nos suscribimos una sola vez
        _interactable.OnInteract.AddListener(OnPickup);
    }

    // ¡OJO! Este método puede recibir un hijo golpeado por el raycast o el propio Player
    private void OnPickup(GameObject whoCalled)
    {
        var carry = whoCalled?.GetComponentInParent<PlayerCarrySystem>() 
                     ?? FindFirstObjectByType<PlayerCarrySystem>();

        if (carry == null)
        {
            Debug.LogWarning($"[PickupObject] No se encuentra PlayerCarrySystem en {name}");
            return;
        }

        _interactable.SetHintVisible(false);
        _interactable.EnableInteraction(false);
        carry.PickupObject(gameObject);
    }

    public void OnDropped()
    {
        if (canBeDropped && _interactable != null)
            _interactable.EnableInteraction(true);
    }

    public ObjectType GetObjectType() => objectType;
    public bool CanBeDropped => canBeDropped;
}