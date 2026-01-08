using UnityEngine;
using System.Collections.Generic;
using Sendero.Core.Feedback;

/// <summary>
/// Interruptor de presión que se activa al colocar objetos con Rigidbody encima.
/// Detecta objetos con PickupObject y activa diferentes acciones (elevar, hundir, desactivar, instanciar).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PressurePlate : MonoBehaviour
{
    [Header("Configuración del Interruptor")]
    [Tooltip("Si es true, solo acepta objetos con el componente PickupObject")]
    [SerializeField] private bool onlyPickupObjects = true;
    
    [Tooltip("Si es true, el interruptor permanece activo una vez presionado")]
    [SerializeField] private bool lockWhenActivated;
    
    [Tooltip("Masa mínima necesaria para activar el interruptor (para filtrar objetos muy ligeros)")]
    [SerializeField] private float minimumMass = 0.1f;
    
    [Header("Feedback Visual")]
    [Tooltip("Cuánto se hunde la placa cuando se activa (en unidades locales Y)")]
    [SerializeField] private float sinkAmount = 0.2f;
    
    [Tooltip("Velocidad de animación de hundimiento/elevación")]
    [SerializeField] private float animationSpeed = 5f;
    
    [Tooltip("GameObject que contiene el mesh de la placa (se hundirá)")]
    [SerializeField] private Transform plateVisual;
    
    [Header("Feedback de Cámara y Audio")]
    [Tooltip("Intensidad del shake de cámara al activar")]
    [SerializeField] private float cameraShakeIntensity = 0.3f;
    
    [Tooltip("Duración del shake de cámara")]
    [SerializeField] private float cameraShakeDuration = 0.2f;
    
    [Tooltip("Clave de SFX al activar el interruptor")]
    [SerializeField] private string activateSfxKey = "PressurePlate_Activate";
    
    [Tooltip("Clave de SFX al desactivar el interruptor")]
    [SerializeField] private string deactivateSfxKey = "PressurePlate_Deactivate";
    
    [Header("Acciones: Elevar Plataformas")]
    [Tooltip("Plataformas que se elevarán al activar el interruptor")]
    [SerializeField] private PlatformElevator[] platformsToRaise;
    
    [Header("Acciones: Hundir Plataformas")]
    [Tooltip("Plataformas que se hundirán al activar el interruptor")]
    [SerializeField] private PlatformElevator[] platformsToLower;
    
    [Header("Acciones: Desactivar GameObjects")]
    [Tooltip("GameObjects que se desactivarán al activar el interruptor")]
    [SerializeField] private GameObject[] objectsToDeactivate;
    
    [Tooltip("VFX que se instancia al desactivar los objetos")]
    [SerializeField] private GameObject deactivateVFX;
    
    [Tooltip("Tiempo de vida del VFX de desactivación")]
    [SerializeField] private float vfxLifetime = 3f;
    
    [Header("Acciones: Instanciar Recompensas/Enemigos")]
    [Tooltip("Prefabs que se instanciarán al activar (recompensas, enemigos, etc.)")]
    [SerializeField] private GameObject[] prefabsToSpawn;
    
    [Tooltip("Posiciones donde aparecerán los objetos instanciados")]
    [SerializeField] private Transform[] spawnPoints;
    
    [Header("Estado")]
    [SerializeField] private bool isActivated;
    
    private Vector3 _originalPlatePosition;
    private Vector3 _targetPlatePosition;
    private HashSet<Rigidbody> _objectsOnPlate = new HashSet<Rigidbody>();
    private bool _isAnimating;

    // Propiedad pública para consultar estado (compatibilidad con PressurePuzzleController)
    public bool IsActivated => isActivated;
    public bool isPressed => isActivated; // Alias para compatibilidad con código legacy

    private void Start()
    {
        // Guardar posición original de la placa visual
        if (plateVisual != null)
        {
            _originalPlatePosition = plateVisual.localPosition;
            _targetPlatePosition = _originalPlatePosition;
        }
        
        // Verificar que el collider sea trigger
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[PressurePlate] El collider en {name} debería ser trigger. Configurándolo automáticamente.");
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        // Animar la placa visual
        if (plateVisual != null && _isAnimating)
        {
            plateVisual.localPosition = Vector3.Lerp(
                plateVisual.localPosition,
                _targetPlatePosition,
                Time.deltaTime * animationSpeed
            );
            
            // Detener animación cuando está cerca del objetivo
            if (Vector3.Distance(plateVisual.localPosition, _targetPlatePosition) < 0.001f)
            {
                plateVisual.localPosition = _targetPlatePosition;
                _isAnimating = false;
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (lockWhenActivated && isActivated) return;
        
        // Verificar si el objeto tiene Rigidbody
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        
        // Verificar si solo aceptamos objetos con PickupObject
        if (onlyPickupObjects)
        {
            var pickup = rb.GetComponent<PickupObject>();
            if (pickup == null) return;
        }
        
        // Verificar masa mínima
        if (rb.mass < minimumMass) return;
        
        // Añadir a la lista de objetos en la placa
        _objectsOnPlate.Add(rb);
        
        // Activar el interruptor si no estaba activado
        if (!isActivated)
        {
            Activate();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (lockWhenActivated && isActivated) return;
        
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        
        // Remover de la lista
        _objectsOnPlate.Remove(rb);
        
        // Desactivar el interruptor si no hay más objetos
        if (_objectsOnPlate.Count == 0 && isActivated)
        {
            Deactivate();
        }
    }

    /// <summary>
    /// Activa el interruptor y ejecuta todas las acciones
    /// </summary>
    private void Activate()
    {
        isActivated = true;
        
        Debug.Log($"[PressurePlate] 🔴 {name} ACTIVADO");
        
        // Feedback visual - hundir placa
        if (plateVisual != null)
        {
            _targetPlatePosition = _originalPlatePosition + Vector3.down * sinkAmount;
            _isAnimating = true;
        }
        
        // Feedback de cámara
        if (cameraShakeIntensity > 0f && cameraShakeDuration > 0f)
        {
            FeedbackService.CameraShake(cameraShakeIntensity, cameraShakeDuration);
        }
        
        // Feedback de audio
        if (!string.IsNullOrEmpty(activateSfxKey))
        {
            AudioService.Instance?.PlaySFX(activateSfxKey, worldPosition: transform.position);
        }
        
        // Ejecutar acciones
        RaisePlatforms();
        LowerPlatforms();
        DeactivateObjects();
        SpawnObjects();
        
        // Notificar cambio de estado (para PressurePuzzleController y similares)
        SendMessageUpwards("OnPlateStateChanged", this, SendMessageOptions.DontRequireReceiver);
        
        // Callback personalizado
        OnActivated();
    }

    /// <summary>
    /// Desactiva el interruptor (si no está bloqueado)
    /// </summary>
    private void Deactivate()
    {
        if (lockWhenActivated) return;
        
        isActivated = false;
        
        Debug.Log($"[PressurePlate] ⚪ {name} DESACTIVADO");
        
        // Feedback visual - elevar placa
        if (plateVisual != null)
        {
            _targetPlatePosition = _originalPlatePosition;
            _isAnimating = true;
        }
        
        // Feedback de audio
        if (!string.IsNullOrEmpty(deactivateSfxKey))
        {
            AudioService.Instance?.PlaySFX(deactivateSfxKey, worldPosition: transform.position);
        }
        
        // Revertir acciones (elevar/hundir plataformas)
        RevertPlatforms();
        
        // Notificar cambio de estado (para PressurePuzzleController y similares)
        SendMessageUpwards("OnPlateStateChanged", this, SendMessageOptions.DontRequireReceiver);
        
        // Callback personalizado
        OnDeactivated();
    }

    /// <summary>
    /// Eleva las plataformas configuradas
    /// </summary>
    private void RaisePlatforms()
    {
        if (platformsToRaise == null || platformsToRaise.Length == 0) return;
        
        foreach (var platform in platformsToRaise)
        {
            if (platform != null)
            {
                platform.Raise();
            }
        }
    }

    /// <summary>
    /// Hunde las plataformas configuradas
    /// </summary>
    private void LowerPlatforms()
    {
        if (platformsToLower == null || platformsToLower.Length == 0) return;
        
        foreach (var platform in platformsToLower)
        {
            if (platform != null)
            {
                platform.Lower();
            }
        }
    }

    /// <summary>
    /// Revierte el estado de las plataformas al desactivar
    /// </summary>
    private void RevertPlatforms()
    {
        // Revertir plataformas que se elevaron
        if (platformsToRaise != null)
        {
            foreach (var platform in platformsToRaise)
            {
                if (platform != null)
                {
                    platform.Lower();
                }
            }
        }
        
        // Revertir plataformas que se hundieron
        if (platformsToLower != null)
        {
            foreach (var platform in platformsToLower)
            {
                if (platform != null)
                {
                    platform.Raise();
                }
            }
        }
    }

    /// <summary>
    /// Desactiva los GameObjects configurados con VFX
    /// </summary>
    private void DeactivateObjects()
    {
        if (objectsToDeactivate == null || objectsToDeactivate.Length == 0) return;
        
        foreach (var obj in objectsToDeactivate)
        {
            if (obj != null && obj.activeInHierarchy)
            {
                // Instanciar VFX en la posición del objeto
                if (deactivateVFX != null)
                {
                    var fx = Instantiate(deactivateVFX, obj.transform.position, obj.transform.rotation);
                    Destroy(fx, vfxLifetime);
                }
                
                // Desactivar el objeto
                obj.SetActive(false);
                Debug.Log($"[PressurePlate] 🔥 Desactivado: {obj.name}");
            }
        }
    }

    /// <summary>
    /// Instancia los prefabs configurados en los spawn points
    /// </summary>
    private void SpawnObjects()
    {
        if (prefabsToSpawn == null || prefabsToSpawn.Length == 0) return;
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"[PressurePlate] {name} tiene prefabs pero no spawn points configurados");
            return;
        }
        
        for (int i = 0; i < prefabsToSpawn.Length; i++)
        {
            var prefab = prefabsToSpawn[i];
            if (prefab == null) continue;
            
            // Usar el spawn point correspondiente (o el último si hay más prefabs que spawn points)
            var spawnPoint = spawnPoints[Mathf.Min(i, spawnPoints.Length - 1)];
            if (spawnPoint == null)
            {
                Debug.LogWarning($"[PressurePlate] Spawn point {i} es null");
                continue;
            }
            
            // Instanciar
            Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
            Debug.Log($"[PressurePlate] ✨ Instanciado: {prefab.name} en {spawnPoint.name}");
        }
    }

    /// <summary>
    /// Callback que se llama al activar el interruptor.
    /// Sobrescribe en una clase hija para comportamiento personalizado.
    /// </summary>
    protected virtual void OnActivated()
    {
        // Opcional: disparar evento del sistema
        // EventBus.Trigger("PressurePlateActivated", gameObject);
    }

    /// <summary>
    /// Callback que se llama al desactivar el interruptor.
    /// Sobrescribe en una clase hija para comportamiento personalizado.
    /// </summary>
    protected virtual void OnDeactivated()
    {
        // Opcional: disparar evento del sistema
        // EventBus.Trigger("PressurePlateDeactivated", gameObject);
    }

    /// <summary>
    /// Activa el interruptor manualmente desde código externo
    /// </summary>
    public void ForceActivate()
    {
        if (!isActivated)
        {
            Activate();
        }
    }

    /// <summary>
    /// Desactiva el interruptor manualmente desde código externo
    /// </summary>
    public void ForceDeactivate()
    {
        if (isActivated && !lockWhenActivated)
        {
            Deactivate();
        }
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Dibujar área de detección
        Gizmos.color = isActivated ? Color.green : Color.yellow;
        var col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }
        
        // Dibujar líneas a las plataformas que se elevan
        if (platformsToRaise != null)
        {
            Gizmos.color = Color.green;
            foreach (var platform in platformsToRaise)
            {
                if (platform != null)
                {
                    Gizmos.DrawLine(transform.position, platform.transform.position);
                    Gizmos.DrawWireSphere(platform.transform.position, 0.5f);
                }
            }
        }
        
        // Dibujar líneas a las plataformas que se hunden
        if (platformsToLower != null)
        {
            Gizmos.color = Color.red;
            foreach (var platform in platformsToLower)
            {
                if (platform != null)
                {
                    Gizmos.DrawLine(transform.position, platform.transform.position);
                    Gizmos.DrawWireSphere(platform.transform.position, 0.5f);
                }
            }
        }
        
        // Dibujar líneas a los objetos que se desactivan
        if (objectsToDeactivate != null)
        {
            Gizmos.color = Color.magenta;
            foreach (var obj in objectsToDeactivate)
            {
                if (obj != null)
                {
                    Gizmos.DrawLine(transform.position, obj.transform.position);
                    Gizmos.DrawWireCube(obj.transform.position, Vector3.one * 0.3f);
                }
            }
        }
        
        // Dibujar spawn points
        if (spawnPoints != null)
        {
            Gizmos.color = Color.cyan;
            foreach (var spawn in spawnPoints)
            {
                if (spawn != null)
                {
                    Gizmos.DrawLine(transform.position, spawn.position);
                    Gizmos.DrawWireSphere(spawn.position, 0.3f);
                    // Dibujar dirección del spawn
                    Gizmos.DrawRay(spawn.position, spawn.forward * 0.5f);
                }
            }
        }
    }
    #endif
}

