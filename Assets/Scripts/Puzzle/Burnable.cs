using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Componente para objetos que pueden ser destruidos con elementos mágicos (fuego, hielo, etc.).
/// Puede quemar objetos adicionales en cadena o destruir solo los hijos con mesh.
/// Añadir al GameObject con su Collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Burnable : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Elementos que pueden destruir este objeto (normalmente solo Fire)")]
    [SerializeField] private MagicElement[] acceptedElements = { MagicElement.Fire };
    
    [Tooltip("Si es false, no se destruye nada al quemar (usar junto a onBurned / objectsToActivate para control manual)")]
    [SerializeField] private bool destroyOnBurn = true;

    [Tooltip("Si es true, destruye solo los objetos hijos con mesh (útil para enredaderas). Si es false, destruye el objeto completo.")]
    [SerializeField] private bool destroyOnlyChildrenWithMesh = true;

    [Tooltip("Si es true, el objeto se destruye inmediatamente. Si es false, se reproduce animación/VFX primero.")]
    [SerializeField] private bool destroyImmediately;

    [Tooltip("Tiempo antes de destruir el GameObject si destroyImmediately es false (para dar tiempo a animaciones)")]
    [SerializeField] private float destroyDelay = 2f;
    
    [Header("Objetos en Cadena (opcional para bombas, etc.)")]
    [Tooltip("Lista de objetos Burnable que se quemarán cuando este objeto sea quemado")]
    [SerializeField] private Burnable[] objectsToBurn;

    [Tooltip("Delay antes de quemar los objetos en cadena (para efecto visual)")]
    [SerializeField] private float chainBurnDelay = 0.3f;

    [Header("Activar / Desactivar al quemar")]
    [Tooltip("GameObjects que se activarán cuando este objeto sea quemado")]
    [SerializeField] private GameObject[] objectsToActivate;

    [Tooltip("GameObjects que se desactivarán cuando este objeto sea quemado")]
    [SerializeField] private GameObject[] objectsToDeactivate;
    
    [Header("Efectos")]
    [Tooltip("VFX que aparece al quemar (fuego, chispas, etc.)")]
    [SerializeField] private GameObject burnVFX;
    
    [Tooltip("Tiempo de vida del VFX de quemado")]
    [SerializeField] private float vfxLifetime = 3f;
    
    [Tooltip("Clave de audio que se reproduce al quemar")]
    [SerializeField] private string burnSfxKey = "Object_Burn";
    
    [Header("Animación (opcional)")]
    [Tooltip("Animator que controla la animación de quemado")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Trigger que activa la animación de quemado")]
    [SerializeField] private string burnTrigger = "Burn";
    
    [Header("Eventos")]
    [Tooltip("Se invoca al quemar. Suscribir GOs que reaccionen (activar/desactivar objetos, abrir puertas, encender antorchas…)")]
    public UnityEvent onBurned;

    [Header("Estado")]
    [Tooltip("Si es true, este objeto ya fue quemado")]
    [SerializeField] private bool isBurned;

    /// <summary>
    /// Llamado cuando un proyectil mágico impacta este objeto
    /// </summary>
    public void OnHitByMagic(MagicElement element, Vector3 hitPoint)
    {
        if (isBurned) return; // Ya está quemado
        
        // Verificar si el elemento es aceptado
        bool canBurn = false;
        foreach (var accepted in acceptedElements)
        {
            if (accepted == element)
            {
                canBurn = true;
                break;
            }
        }
        
        if (!canBurn)
        {
            Debug.Log($"[Burnable] {gameObject.name} no puede ser quemado con {element}");
            return;
        }
        
        Debug.Log($"[Burnable] 🔥 {gameObject.name} está siendo quemado con {element}!");
        
        isBurned = true;
        
        // Reproducir VFX
        if (burnVFX != null)
        {
            var fx = Instantiate(burnVFX, hitPoint, Quaternion.identity);
            Destroy(fx, vfxLifetime);
        }
        
        // Reproducir SFX
        if (!string.IsNullOrEmpty(burnSfxKey))
        {
            AudioService.Instance?.PlaySFX(burnSfxKey, worldPosition: hitPoint);
        }
        
        // Reproducir animación si existe
        if (animator != null && !string.IsNullOrEmpty(burnTrigger))
        {
            animator.SetTrigger(burnTrigger);
        }
        
        // Desactivar collider para que el jugador pueda pasar
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
        
        // Destruir según configuración
        if (destroyOnBurn)
        {
            if (destroyOnlyChildrenWithMesh)
            {
                DestroyChildrenWithMesh();
            }
            else
            {
                if (destroyImmediately)
                    Destroy(gameObject);
                else
                    Destroy(gameObject, destroyDelay);
            }
        }
        
        // Quemar objetos en cadena (opcional, para bombas, etc.)
        BurnChainedObjects(element);
        
        // Notificar al sistema de eventos si lo necesitas
        OnBurned();
    }
    
    /// <summary>
    /// Destruye todos los GameObjects hijos que tienen MeshRenderer o MeshFilter
    /// </summary>
    private void DestroyChildrenWithMesh()
    {
        // Buscar todos los MeshRenderer en los hijos
        MeshRenderer[] meshRenderers = GetComponentsInChildren<MeshRenderer>();
        
        foreach (var meshRenderer in meshRenderers)
        {
            // No destruir el propio objeto padre
            if (meshRenderer.gameObject != gameObject)
            {
                if (destroyImmediately)
                {
                    Destroy(meshRenderer.gameObject);
                }
                else
                {
                    // Primero desactivar el renderer para que desaparezca visualmente
                    meshRenderer.enabled = false;
                    // Luego destruir el GameObject después del delay
                    Destroy(meshRenderer.gameObject, destroyDelay);
                }
                
                Debug.Log($"[Burnable] 🔥 Destruyendo hijo con mesh: {meshRenderer.gameObject.name}");
            }
        }
        
        // Si no hay mesh renderers, buscar MeshFilters como fallback
        if (meshRenderers.Length == 0)
        {
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (var meshFilter in meshFilters)
            {
                if (meshFilter.gameObject != gameObject)
                {
                    if (destroyImmediately)
                    {
                        Destroy(meshFilter.gameObject);
                    }
                    else
                    {
                        Destroy(meshFilter.gameObject, destroyDelay);
                    }
                    
                    Debug.Log($"[Burnable] 🔥 Destruyendo hijo con mesh filter: {meshFilter.gameObject.name}");
                }
            }
        }
    }
    
    /// <summary>
    /// Quema los objetos en cadena después de un delay
    /// </summary>
    private void BurnChainedObjects(MagicElement element)
    {
        if (objectsToBurn == null || objectsToBurn.Length == 0) return;
        
        foreach (var obj in objectsToBurn)
        {
            if (obj != null && !obj.isBurned)
            {
                // Quemar con delay para efecto visual
                StartCoroutine(BurnAfterDelay(obj, element, chainBurnDelay));
            }
        }
    }
    
    /// <summary>
    /// Corrutina para quemar un objeto después de un delay
    /// </summary>
    private System.Collections.IEnumerator BurnAfterDelay(Burnable target, MagicElement element, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (target != null)
        {
            target.OnHitByMagic(element, target.transform.position);
        }
    }
    
    /// <summary>
    /// Llamado cuando el objeto es quemado.
    /// Sobrescribe en una clase hija para comportamiento personalizado.
    /// </summary>
    protected virtual void OnBurned()
    {
        foreach (var go in objectsToActivate)
            if (go != null) go.SetActive(true);

        foreach (var go in objectsToDeactivate)
            if (go != null) go.SetActive(false);

        onBurned.Invoke();
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Dibujar líneas a los objetos que se quemarán en cadena
        if (objectsToBurn != null && objectsToBurn.Length > 0)
        {
            Gizmos.color = Color.red;
            foreach (var obj in objectsToBurn)
            {
                if (obj != null)
                {
                    Gizmos.DrawLine(transform.position, obj.transform.position);
                    Gizmos.DrawWireSphere(obj.transform.position, 0.5f);
                }
            }
        }
    }
    #endif
}

