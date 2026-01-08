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
    
    [Tooltip("Si es true, congela el objeto que activa la placa para que no se mueva ni se caiga")]
    [SerializeField] private bool freezeObjectOnPlate = true;
    
    [Tooltip("Si es true, hace al objeto hijo de la placa para que se mueva con ella")]
    [SerializeField] private bool parentObjectToPlate = false;
    
    [Header("Detección del Jugador")]
    [Tooltip("Si es true, detecta cuando el jugador entra y da feedback (sin activar)")]
    [SerializeField] private bool detectPlayer = true;
    
    [Tooltip("Si es true, el jugador puede activar el interruptor (además del feedback). Si es false, solo da feedback.")]
    [SerializeField] private bool playerCanActivate = true;
    
    [Tooltip("Tag del jugador para detectarlo")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("SFX al detectar jugador sin objeto correcto")]
    [SerializeField] private string playerStepSfxKey = "PressurePlate_PlayerStep";
    
    [Tooltip("Cuánto se hunde la placa cuando el jugador la pisa (menos que con objeto)")]
    [SerializeField] private float playerSinkAmount = 0.05f;
    
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
    private Dictionary<Rigidbody, RigidbodyState> _originalRigidbodyStates = new Dictionary<Rigidbody, RigidbodyState>();
    private bool _isAnimating;
    private bool _playerOnPlate;
    
    // Estructura para guardar el estado original del Rigidbody
    private struct RigidbodyState
    {
        public bool isKinematic;
        public RigidbodyConstraints constraints;
        public Transform originalParent;
    } // Rastrea si el jugador está en la placa

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
        Debug.Log($"[PressurePlate] 🔍 OnTriggerEnter: {other.name} (Tag: {other.tag})");
        
        if (lockWhenActivated && isActivated)
        {
            Debug.Log($"[PressurePlate] ⚠️ Ya está bloqueado y activado");
            return;
        }
        
        // Detectar jugador
        if (detectPlayer && other.CompareTag(playerTag))
        {
            Debug.Log($"[PressurePlate] 👤 JUGADOR detectado! playerCanActivate={playerCanActivate}, isActivated={isActivated}");
            
            OnPlayerEnter();
            
            // Si el jugador puede activar, continuar con la activación
            if (playerCanActivate && !isActivated)
            {
                Debug.Log($"[PressurePlate] ✅ ACTIVANDO por jugador...");
                Activate();
            }
            else
            {
                Debug.Log($"[PressurePlate] ❌ NO activa: playerCanActivate={playerCanActivate}, isActivated={isActivated}");
            }
            
            return;
        }
        
        Debug.Log($"[PressurePlate] 🔍 No es jugador, verificando Rigidbody...");
        
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
        
        // Verificar si ya está en la lista (evitar duplicados)
        if (_objectsOnPlate.Contains(rb))
        {
            Debug.Log($"[PressurePlate] ⚠️ Objeto {rb.name} ya está en la placa, ignorando");
            return;
        }
        
        // Añadir a la lista de objetos en la placa
        _objectsOnPlate.Add(rb);
        
        // Guardar estado original del Rigidbody
        if (!_originalRigidbodyStates.ContainsKey(rb))
        {
            _originalRigidbodyStates[rb] = new RigidbodyState
            {
                isKinematic = rb.isKinematic,
                constraints = rb.constraints,
                originalParent = rb.transform.parent
            };
        }
        
        // Congelar el objeto en la placa
        if (freezeObjectOnPlate)
        {
            rb.isKinematic = true; // Desactiva física para que no se mueva
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Hacer hijo de la placa (opcional)
        if (parentObjectToPlate && plateVisual != null)
        {
            rb.transform.SetParent(plateVisual);
        }
        
        Debug.Log($"[PressurePlate] 📦 Objeto añadido: {rb.name} (Total: {_objectsOnPlate.Count})");
        
        // Activar el interruptor si no estaba activado
        if (!isActivated)
        {
            Activate();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (lockWhenActivated && isActivated) return;
        
        // Detectar cuando el jugador sale
        if (detectPlayer && other.CompareTag(playerTag))
        {
            OnPlayerExit();
            
            // Si el jugador puede activar y lo activó, desactivar al salir (solo si no hay objetos)
            if (playerCanActivate && isActivated && _objectsOnPlate.Count == 0)
            {
                Deactivate();
            }
            
            return;
        }
        
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        
        // Solo procesar si el objeto estaba en nuestra lista
        if (!_objectsOnPlate.Contains(rb)) return;
        
        // Remover de la lista
        _objectsOnPlate.Remove(rb);
        
        Debug.Log($"[PressurePlate] 📦 Objeto removido: {rb.name} (Restantes: {_objectsOnPlate.Count})");
        
        // Restaurar estado original del Rigidbody
        if (_originalRigidbodyStates.TryGetValue(rb, out RigidbodyState originalState))
        {
            rb.isKinematic = originalState.isKinematic;
            rb.constraints = originalState.constraints;
            
            // Restaurar padre original
            if (parentObjectToPlate)
            {
                rb.transform.SetParent(originalState.originalParent);
            }
            
            _originalRigidbodyStates.Remove(rb);
        }
        
        // Desactivar el interruptor si no hay más objetos y el jugador no está (o no puede activar)
        if (_objectsOnPlate.Count == 0 && isActivated)
        {
            // Solo desactivar si el jugador no está O si el jugador no puede activar
            if (!_playerOnPlate || !playerCanActivate)
            {
                Deactivate();
            }
        }
    }

    /// <summary>
    /// Activa el interruptor y ejecuta todas las acciones
    /// </summary>
    private void Activate()
    {
        // Si ya está activado, no hacer nada
        if (isActivated)
        {
            Debug.Log($"[PressurePlate] ⚠️ {name} Ya está activado, ignorando");
            return;
        }
        
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
        // Revertir plataformas que se elevaron (sin VFX, solo shake)
        if (platformsToRaise != null)
        {
            foreach (var platform in platformsToRaise)
            {
                if (platform != null)
                {
                    platform.Lower(isReversion: true); // Sin VFX
                }
            }
        }
        
        // Revertir plataformas que se hundieron (sin VFX, solo shake)
        if (platformsToLower != null)
        {
            foreach (var platform in platformsToLower)
            {
                if (platform != null)
                {
                    platform.Raise(isReversion: true); // Sin VFX
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

    /// <summary>
    /// Llamado cuando el jugador entra en la placa (sin activar)
    /// </summary>
    private void OnPlayerEnter()
    {
        if (_playerOnPlate) return; // Ya está encima
        
        _playerOnPlate = true;
        
        Debug.Log($"[PressurePlate] 👤 {name} - Jugador detectado {(playerCanActivate ? "(puede activar)" : "(necesita objeto)")}");
        
        // Feedback visual - la placa se hunde
        if (plateVisual != null && !isActivated)
        {
            // Si el jugador puede activar, hundir completamente. Si no, solo un poco.
            float sinkAmountToUse = playerCanActivate ? sinkAmount : playerSinkAmount;
            _targetPlatePosition = _originalPlatePosition + Vector3.down * sinkAmountToUse;
            _isAnimating = true;
        }
        
        // Feedback de audio
        if (!string.IsNullOrEmpty(playerStepSfxKey))
        {
            AudioService.Instance?.PlaySFX(playerStepSfxKey, worldPosition: transform.position);
        }
        
        // Callback para comportamiento personalizado
        OnPlayerSteppedOn();
    }

    /// <summary>
    /// Llamado cuando el jugador sale de la placa
    /// </summary>
    private void OnPlayerExit()
    {
        if (!_playerOnPlate) return; // No estaba encima
        
        _playerOnPlate = false;
        
        Debug.Log($"[PressurePlate] 👤 {name} - Jugador salió");
        
        // Volver la placa a su posición original (solo si no está activada)
        if (plateVisual != null && !isActivated)
        {
            _targetPlatePosition = _originalPlatePosition;
            _isAnimating = true;
        }
    }

    /// <summary>
    /// Callback que se llama cuando el jugador pisa la placa.
    /// Sobrescribe en una clase hija para comportamiento personalizado (ej: mostrar hint UI).
    /// </summary>
    protected virtual void OnPlayerSteppedOn()
    {
        // Opcional: mostrar hint al jugador
        // HintSystem.ShowHint("Busca un objeto pesado para activar esta placa");
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

