using UnityEngine;
using System.Collections;
using Sendero.Core.Feedback;

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
    
    [Tooltip("Duración del VFX antes de destruirse automáticamente")]
    [SerializeField] private float vfxLifetime = 3f;
    
    [Header("Estado")]
    [SerializeField] private bool isRaised;
    
    private Vector3 _originalPosition;
    private Vector3 _raisedPosition;
    private bool _isMoving;
    private Coroutine _moveCoroutine;
    private GameObject _currentVFX; // VFX activo actual

    private void Start()
    {
        // Guardar posición original ACTUAL (donde está ahora)
        _originalPosition = transform.position;
        _raisedPosition = _originalPosition + Vector3.up * raiseHeight;
        
        // Sincronizar el flag isRaised con la posición real
        // Si está marcado en Inspector pero no está en posición elevada, teletransportar
        if (isRaised)
        {
            Debug.Log($"[PlatformElevator] {name} está marcado como 'Is Raised', teletransportando a posición elevada");
            transform.position = _raisedPosition;
            _originalPosition = _raisedPosition - Vector3.up * raiseHeight; // Recalcular posición original
        }
        
        // Debug.Log($"[PlatformElevator] {name} - Inicializado. Original: {_originalPosition}, Raised: {_raisedPosition}, Current: {transform.position}, IsRaised: {isRaised}");
    }

    /// <summary>
    /// Eleva la plataforma
    /// </summary>
    /// <param name="isReversion">Si es true, es una reversión (sin VFX, solo shake)</param>
    public void Raise(bool isReversion = false)
    {
        // Si ya está en movimiento hacia arriba, no hacer nada
        if (_isMoving && isRaised) return;
        
        // Si ya está en la posición elevada (tolerancia de 0.1), no mover
        if (Vector3.Distance(transform.position, _raisedPosition) < 0.1f)
        {
            isRaised = true;
            Debug.Log($"[PlatformElevator] {name} ya está en posición elevada");
            return;
        }
        
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        
        Debug.Log($"[PlatformElevator] {name} - Elevando a {_raisedPosition} (desde {transform.position})");
        _moveCoroutine = StartCoroutine(MovePlatform(_raisedPosition, true, isReversion));
    }

    /// <summary>
    /// Hunde la plataforma (vuelve a su posición original)
    /// </summary>
    /// <param name="isReversion">Si es true, es una reversión (sin VFX, solo shake)</param>
    public void Lower(bool isReversion = false)
    {
        // Si ya está en movimiento hacia abajo, no hacer nada
        if (_isMoving && !isRaised) return;
        
        // Si ya está en la posición baja (tolerancia de 0.1), no mover
        if (Vector3.Distance(transform.position, _originalPosition) < 0.1f)
        {
            isRaised = false;
            Debug.Log($"[PlatformElevator] {name} ya está en posición original");
            return;
        }
        
        if (_moveCoroutine != null)
        {
            StopCoroutine(_moveCoroutine);
        }
        
        Debug.Log($"[PlatformElevator] {name} - Bajando a {_originalPosition} (desde {transform.position})");
        _moveCoroutine = StartCoroutine(MovePlatform(_originalPosition, false, isReversion));
    }

    /// <summary>
    /// Corrutina que mueve la plataforma suavemente
    /// </summary>
    private IEnumerator MovePlatform(Vector3 targetPosition, bool raising, bool isReversion = false)
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
        
        // Feedback al comenzar (sin VFX si es reversión)
        PlayMovementStartFeedback(isReversion);
        
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
        PlayMovementStopFeedback(isReversion);
        
        // Activar plataformas encadenadas
        ActivateChainedPlatforms(raising, isReversion);
        
        // Callback
        OnMovementComplete(raising);
    }

    /// <summary>
    /// Activa las plataformas encadenadas
    /// </summary>
    private void ActivateChainedPlatforms(bool raising, bool isReversion = false)
    {
        if (chainedPlatforms == null || chainedPlatforms.Length == 0) return;
        
        foreach (var platform in chainedPlatforms)
        {
            if (platform != null)
            {
                if (raising)
                {
                    platform.Raise(isReversion);
                }
                else
                {
                    platform.Lower(isReversion);
                }
            }
        }
    }

    /// <summary>
    /// Reproduce feedback al comenzar a moverse
    /// </summary>
    private void PlayMovementStartFeedback(bool isReversion = false)
    {
        // SFX (siempre, reversión o no)
        if (!string.IsNullOrEmpty(movementStartSfxKey))
        {
            AudioService.Instance?.PlaySFX(movementStartSfxKey, worldPosition: transform.position);
        }
        
        // Camera shake - SOLO en reversión (al bajar después de quitar objeto)
        // Al subir, el shake ya se hace en el PressurePlate.Activate()
        if (isReversion)
        {
            FeedbackService.CameraShake(0.2f, 0.3f);
        }
        
        // VFX - Solo si NO es reversión
        if (!isReversion && movementVFX != null)
        {
            // Destruir VFX anterior si existe
            if (_currentVFX != null)
            {
                Destroy(_currentVFX);
            }
            
            Vector3 spawnPos = vfxSpawnPoint != null ? vfxSpawnPoint.position : transform.position;
            _currentVFX = Instantiate(movementVFX, spawnPos, Quaternion.identity, transform);
        }
    }

    /// <summary>
    /// Reproduce feedback al terminar de moverse
    /// </summary>
    private void PlayMovementStopFeedback(bool isReversion = false)
    {
        // SFX
        if (!string.IsNullOrEmpty(movementStopSfxKey))
        {
            AudioService.Instance?.PlaySFX(movementStopSfxKey, worldPosition: transform.position);
        }
        
        // Destruir VFX cuando termina el movimiento (si había VFX)
        if (_currentVFX != null)
        {
            Destroy(_currentVFX, vfxLifetime); // Delay configurable para que termine la animación
            _currentVFX = null;
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

    /// <summary>
    /// Eleva sin VFX (para revertir una bajada previa). Usar en onDeactivated de PressurePlate.
    /// </summary>
    public void RaiseRevert() => Raise(isReversion: true);

    /// <summary>
    /// Baja sin VFX (para revertir una subida previa). Usar en onDeactivated de PressurePlate.
    /// </summary>
    public void LowerRevert() => Lower(isReversion: true);

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

