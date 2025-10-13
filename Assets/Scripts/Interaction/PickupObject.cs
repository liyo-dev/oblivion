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
        // 1) Localiza el PlayerCarrySystem de forma robusta
        var carry = whoCalled
            ? whoCalled.GetComponentInParent<PlayerCarrySystem>()
            : null;

        if (carry == null)
        {
            // Plan B: toma el primero de la escena (útil si el evento te pasó el GO equivocado)
            carry = FindFirstObjectByType<PlayerCarrySystem>();
        }

        if (carry == null)
        {
            Debug.LogWarning($"[PickupObject] No encuentro PlayerCarrySystem. ¿Está en el Player?");
            return;
        }

        // 2) Desactiva el hint y la interacción antes de mover
        _interactable.SetHintVisible(false);
        _interactable.EnableInteraction(false);

        // 3) Fuerza a coger **esta caja raíz** (no el hijo que haya golpeado el raycast)
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