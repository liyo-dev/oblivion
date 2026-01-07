using UnityEngine;

/// <summary>
/// Componente para enredaderas u objetos que pueden ser destruidos con fuego.
/// Añadir al GameObject de la enredadera con su Collider.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BurnableVine : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("Elementos que pueden destruir esta enredadera (normalmente solo Fire)")]
    [SerializeField] private MagicElement[] acceptedElements = { MagicElement.Fire };
    
    [Tooltip("Si es true, la enredadera se destruye inmediatamente. Si es false, se reproduce animación/VFX primero.")]
    [SerializeField] private bool destroyImmediately = false;
    
    [Tooltip("Tiempo antes de destruir el GameObject si destroyImmediately es false (para dar tiempo a animaciones)")]
    [SerializeField] private float destroyDelay = 2f;
    
    [Header("Efectos")]
    [Tooltip("VFX que aparece al quemar (fuego, chispas, etc.)")]
    [SerializeField] private GameObject burnVFX;
    
    [Tooltip("Tiempo de vida del VFX de quemado")]
    [SerializeField] private float vfxLifetime = 3f;
    
    [Tooltip("Clave de audio que se reproduce al quemar")]
    [SerializeField] private string burnSFXKey = "Vine_Burn";
    
    [Header("Animación (opcional)")]
    [Tooltip("Animator que controla la animación de quemado")]
    [SerializeField] private Animator animator;
    
    [Tooltip("Trigger que activa la animación de quemado")]
    [SerializeField] private string burnTrigger = "Burn";
    
    [Header("Estado")]
    [Tooltip("Si es true, esta enredadera ya fue quemada")]
    [SerializeField] private bool isBurned = false;

    /// <summary>
    /// Llamado cuando un proyectil mágico impacta esta enredadera
    /// </summary>
    public void OnHitByMagic(MagicElement element, Vector3 hitPoint)
    {
        if (isBurned) return; // Ya está quemada
        
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
            Debug.Log($"[BurnableVine] {gameObject.name} no puede ser quemado con {element}");
            return;
        }
        
        Debug.Log($"[BurnableVine] 🔥 {gameObject.name} está siendo quemado con {element}!");
        
        isBurned = true;
        
        // Reproducir VFX
        if (burnVFX != null)
        {
            var fx = Instantiate(burnVFX, hitPoint, Quaternion.identity);
            Destroy(fx, vfxLifetime);
        }
        
        // Reproducir SFX
        if (!string.IsNullOrEmpty(burnSFXKey))
        {
            AudioService.Instance?.PlaySFX(burnSFXKey, worldPosition: hitPoint);
        }
        
        // Reproducir animación si existe
        if (animator != null && !string.IsNullOrEmpty(burnTrigger))
        {
            animator.SetTrigger(burnTrigger);
        }
        
        // Desactivar collider para que el jugador pueda pasar
        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        // Destruir o desactivar
        if (destroyImmediately)
        {
            Destroy(gameObject);
        }
        else
        {
            // Dar tiempo a la animación y luego destruir
            Destroy(gameObject, destroyDelay);
        }
        
        // Notificar al sistema de eventos si lo necesitas
        OnVineBurned();
    }
    
    /// <summary>
    /// Llamado cuando la enredadera es quemada.
    /// Sobrescribe este método en una clase hija para comportamiento personalizado.
    /// </summary>
    protected virtual void OnVineBurned()
    {
        // Opcional: disparar evento del sistema
        // EventBus.Trigger("VineBurned", gameObject);
    }
    
#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (isBurned)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
        }
    }
#endif
}

