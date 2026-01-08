using UnityEngine;
using System.Collections;

/// <summary>
/// Componente para plataformas que pueden elevarse o hundirse.
/// Usado por PressurePlate u otros mecanismos de puzzle.
/// </summary>
public class PlatformElevator : MonoBehaviour
{
    [Header("Configuración de Movimiento")]
    [Tooltip("Altura a la que se eleva la plataforma (relativa a la posición inicial)")]
    [SerializeField] private float raiseHeight = 3f;
    
    [Tooltip("Velocidad de movimiento de la plataforma")]
    [SerializeField] private float moveSpeed = 2f;
    
    [Tooltip("Curva de animación para suavizar el movimiento")]
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Encadenamiento")]
    [Tooltip("Delay antes de que esta plataforma comience a moverse (útil para efectos en cascada)")]
    [SerializeField] private float delayBeforeMoving;
    
    [Tooltip("Plataformas que se activarán después de que esta termine de moverse")]
    [SerializeField] private PlatformElevator[] chainedPlatforms;
    
    [Header("Feedback")]
    [Tooltip("Clave de SFX al comenzar a moverse")]
    [SerializeField] private string movementStartSfxKey = "Platform_Move_Start";
    
    [Tooltip("Clave de SFX al terminar de moverse")]
    [SerializeField] private string movementStopSfxKey = "Platform_Move_Stop";
    
    [Tooltip("VFX que se instancia al comenzar a moverse")]
    [SerializeField] private GameObject movementVFX;
    
    [Tooltip("Transform donde se instancia el VFX (si es null, usa la posición de la plataforma)")]
    [SerializeField] private Transform vfxSpawnPoint;
    
    [Header("Estado")]
    [SerializeField] private bool isRaised;
    
    private Vector3 _originalPosition;
    private Vector3 _raisedPosition;
    private bool _isMoving;
    private Coroutine _moveCoroutine;

    private void Start()
    {
        // Guardar posición original
        _originalPosition = transform.position;
        _raisedPosition = _originalPosition + Vector3.up * raiseHeight;
        
        // Si comienza elevada, ajustar posición inicial
        if (isRaised)
        {
            transform.position = _raisedPosition;
        }
    }

    /// <summary>
    /// Eleva la plataforma
    /// </summary>
    public void Raise()
    {
        if (_isMoving && isRaised) return;
        
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        
        _moveCoroutine = StartCoroutine(MovePlatform(_raisedPosition, true));
    }

    /// <summary>
    /// Hunde la plataforma (vuelve a su posición original)
    /// </summary>
    public void Lower()
    {
        if (_isMoving && !isRaised) return;
        
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        
        _moveCoroutine = StartCoroutine(MovePlatform(_originalPosition, false));
    }

    /// <summary>
    /// Corrutina que mueve la plataforma suavemente
    /// </summary>
    private IEnumerator MovePlatform(Vector3 targetPosition, bool raising)
    {
        // Delay antes de moverse
        if (delayBeforeMoving > 0f)
        {
            yield return new WaitForSeconds(delayBeforeMoving);
        }
        
        _isMoving = true;
        Vector3 startPosition = transform.position;
        float distance = Vector3.Distance(startPosition, targetPosition);
        float duration = distance / moveSpeed;
        float elapsed = 0f;
        
        // Feedback al comenzar
        PlayMovementStartFeedback();
        
        // Mover la plataforma
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curveT = movementCurve.Evaluate(t);
            
            transform.position = Vector3.Lerp(startPosition, targetPosition, curveT);
            
            yield return null;
        }
        
        // Asegurar que llegue exactamente a la posición final
        transform.position = targetPosition;
        isRaised = raising;
        _isMoving = false;
        
        // Feedback al terminar
        PlayMovementStopFeedback();
        
        // Activar plataformas encadenadas
        ActivateChainedPlatforms(raising);
        
        // Callback
        OnMovementComplete(raising);
    }

    /// <summary>
    /// Activa las plataformas encadenadas
    /// </summary>
    private void ActivateChainedPlatforms(bool raising)
    {
        if (chainedPlatforms == null || chainedPlatforms.Length == 0) return;
        
        foreach (var platform in chainedPlatforms)
        {
            if (platform != null)
            {
                if (raising)
                {
                    platform.Raise();
                }
                else
                {
                    platform.Lower();
                }
            }
        }
    }

    /// <summary>
    /// Reproduce feedback al comenzar a moverse
    /// </summary>
    private void PlayMovementStartFeedback()
    {
        // SFX
        if (!string.IsNullOrEmpty(movementStartSfxKey))
        {
            AudioService.Instance?.PlaySFX(movementStartSfxKey, worldPosition: transform.position);
        }
        
        // VFX
        if (movementVFX != null)
        {
            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            Instantiate(movementVFX, spawnPos, Quaternion.identity, transform);
        }
    }

    /// <summary>
    /// Reproduce feedback al terminar de moverse
    /// </summary>
    private void PlayMovementStopFeedback()
    {
        // SFX
        if (!string.IsNullOrEmpty(movementStopSfxKey))
        {
            AudioService.Instance?.PlaySFX(movementStopSfxKey, worldPosition: transform.position);
        }
    }

    /// <summary>
    /// Callback que se llama al completar el movimiento
    /// </summary>
    protected virtual void OnMovementComplete(bool wasRaised)
    {
        Debug.Log($"[PlatformElevator] {name} completó movimiento. Elevada: {wasRaised}");
    }

    /// <summary>
    /// Teletransporta la plataforma a la posición elevada sin animación
    /// </summary>
    public void TeleportToRaised()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        
        transform.position = _raisedPosition;
        isRaised = true;
        _isMoving = false;
    }

    /// <summary>
    /// Teletransporta la plataforma a la posición original sin animación
    /// </summary>
    public void TeleportToLowered()
    {
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        
        transform.position = _originalPosition;
        isRaised = false;
        _isMoving = false;
    }

    /// <summary>
    /// Actualiza la altura de elevación (útil para ajustar en runtime)
    /// </summary>
    public void SetRaiseHeight(float height)
    {
        raiseHeight = height;
        _raisedPosition = _originalPosition + Vector3.up * raiseHeight;
    }

    public bool IsMoving => _isMoving;
    public bool IsRaised => isRaised;

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Calcular posiciones
        Vector3 originalPos = Application.isPlaying ? _originalPosition : transform.position;
        Vector3 raisedPos = originalPos + Vector3.up * raiseHeight;
        
        // Dibujar posición original
        Gizmos.color = Color.yellow;
        DrawPlatformGizmo(originalPos);
        
        // Dibujar posición elevada
        Gizmos.color = Color.green;
        DrawPlatformGizmo(raisedPos);
        
        // Dibujar línea de recorrido
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(originalPos, raisedPos);
        
        // Dibujar flechas para indicar dirección
        DrawArrow(originalPos, Vector3.up, raiseHeight);
        
        // Dibujar líneas a plataformas encadenadas
        if (chainedPlatforms != null && chainedPlatforms.Length > 0)
        {
            Gizmos.color = Color.magenta;
            foreach (var platform in chainedPlatforms)
            {
                if (platform != null)
                {
                    Gizmos.DrawLine(transform.position, platform.transform.position);
                }
            }
        }
    }

    private void DrawPlatformGizmo(Vector3 position)
    {
        Gizmos.DrawWireCube(position, Vector3.one * 0.5f);
    }

    private void DrawArrow(Vector3 origin, Vector3 direction, float length)
    {
        Vector3 end = origin + direction * length;
        Gizmos.DrawLine(origin, end);
        
        // Puntas de la flecha
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;
        Gizmos.DrawLine(end, end + right * 0.3f);
        Gizmos.DrawLine(end, end + left * 0.3f);
    }
    #endif
}

