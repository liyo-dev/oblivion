using UnityEngine;

/// <summary>
/// Marca a un GameObject como enemigo para sistemas de puertas/gates.
/// Dispara onEnemyGone cuando el enemigo muere, es desactivado o destruido.
/// </summary>
public class EnemyMarker : MonoBehaviour
{
    public System.Action<EnemyMarker> onEnemyGone;
    
    private bool _hasNotified;
    private Damageable _damageable;

    private void Awake()
    {
        _damageable = GetComponent<Damageable>();
    }

    private void Start()
    {
        // Suscribirse al evento de muerte si existe Damageable
        if (_damageable != null)
        {
            _damageable.OnDied += OnEnemyDied;
        }
    }

    private void OnEnemyDied()
    {
        NotifyGone();
    }

    private void OnDisable()
    {
        NotifyGone();
    }

    private void OnDestroy()
    {
        // Desuscribirse del evento
        if (_damageable != null)
        {
            _damageable.OnDied -= OnEnemyDied;
        }
        
        NotifyGone();
    }

    /// <summary>
    /// Notifica una sola vez que el enemigo ya no está activo
    /// </summary>
    private void NotifyGone()
    {
        if (_hasNotified) return;
        _hasNotified = true;
        
        onEnemyGone?.Invoke(this);
    }

    /// <summary>
    /// Resetea el estado para permitir re-usar el marker (ej: si el NPC revive)
    /// </summary>
    public void ResetMarker()
    {
        _hasNotified = false;
    }
}