using UnityEngine;

/// <summary>
/// Script para configurar automáticamente una caja como objeto recogible
/// Añade todos los componentes necesarios y los configura correctamente
/// </summary>
public class BoxSetup : MonoBehaviour
{
    [Header("Configuración automática")]
    [SerializeField] private bool setupOnStart = true;
    [SerializeField] private string objectType = "Caja";

    void Start()
    {
        if (setupOnStart)
        {
            SetupBox();
        }
    }

    [ContextMenu("Setup Box")]
    public void SetupBox()
    {
        Debug.Log($"[BoxSetup] Configurando {name} como caja recogible...");

        // 1. Asegurar que tiene Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            Debug.Log($"[BoxSetup] Rigidbody añadido a {name}");
        }

        // Configurar Rigidbody para objetos recogibles
        rb.mass = 1f; // Peso ligero para fácil manipulación
        rb.linearDamping = 0.5f; // Un poco de resistencia al aire
        rb.angularDamping = 0.5f;
        rb.useGravity = true;
        rb.isKinematic = false; // IMPORTANTE: debe estar en false inicialmente

        // 2. Asegurar que tiene Collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            // Añadir BoxCollider por defecto
            BoxCollider boxCol = gameObject.AddComponent<BoxCollider>();
            Debug.Log($"[BoxSetup] BoxCollider añadido a {name}");
        }

        // 3. Asegurar que tiene Interactable
        Interactable interactable = GetComponent<Interactable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<Interactable>();
            Debug.Log($"[BoxSetup] Interactable añadido a {name}");
        }

        // Configurar Interactable en modo HandOffToTarget (para usar eventos)
        interactable.SetMode(Interactable.Mode.HandOffToTarget);

        // 4. Asegurar que tiene PickupObject
        PickupObject pickup = GetComponent<PickupObject>();
        if (pickup == null)
        {
            pickup = gameObject.AddComponent<PickupObject>();
            Debug.Log($"[BoxSetup] PickupObject añadido a {name}");
        }

        // 5. Configurar layer si es necesario
        if (gameObject.layer == 0) // Default layer
        {
            // Puedes cambiar esto al layer que uses para objetos interactuables
            // gameObject.layer = LayerMask.NameToLayer("Interactable");
        }

        // 6. Configurar hint visual (opcional)
        Transform hintObject = transform.Find("Hint");
        if (hintObject == null)
        {
            // Crear un objeto hint simple
            GameObject hint = new GameObject("Hint");
            hint.transform.SetParent(transform);
            hint.transform.localPosition = Vector3.up * 0.5f;
            
            // Añadir un ícono simple (puedes reemplazar esto con tu prefab de hint)
            var textMesh = hint.AddComponent<TextMesh>();
            textMesh.text = "A";
            textMesh.characterSize = 0.1f;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.color = Color.yellow;
            
            hint.SetActive(false); // Se activará automáticamente cuando sea interactuable
            Debug.Log($"[BoxSetup] Hint visual creado para {name}");
        }

        Debug.Log($"[BoxSetup] ✅ {name} configurado correctamente como caja recogible");
    }

    void OnDrawGizmos()
    {
        // Mostrar el área de interacción
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 1.2f);
    }
}
