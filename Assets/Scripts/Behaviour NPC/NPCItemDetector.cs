using UnityEngine;
using Game.NPC.Modules;

namespace Game.NPC
{
    /// <summary>
    /// Sistema de detección de ítems para completar misiones.
    /// Detecta objetos con SimpleQuestPickup cercanos al NPC y completa automáticamente
    /// el paso de la quest cuando se cumplen las condiciones.
    /// </summary>
    public class NPCItemDetector : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Manager del NPC (auto-detectado si está vacío)")]
        [SerializeField] private NPCBehaviourManagerV2 npcManager;
        
        [Header("Detección")]
        [Tooltip("Radio de detección de ítems")]
        [SerializeField] private float detectionRadius = 3f;
        
        [Tooltip("Ángulo de detección (0-180). 180 = detecta en todas direcciones")]
        [Range(0f, 180f)]
        [SerializeField] private float detectionAngle = 90f;
        
        [Tooltip("Layer de detección de ítems")]
        [SerializeField] private LayerMask detectionLayer = ~0;
        
        [Tooltip("Intervalo de escaneo en segundos")]
        [Min(0.1f)]
        [SerializeField] private float scanInterval = 0.5f;
        
        [Header("Tiempos de Animación")]
        [Tooltip("Duración de la animación de soltar el objeto")]
        [SerializeField] private float dropAnimationDuration = 0.5f;
        
        [Tooltip("Pausa después de soltar antes de destruir el objeto")]
        [SerializeField] private float pauseAfterDrop = 0.3f;
        
        [Tooltip("Pausa antes de completar la quest")]
        [SerializeField] private float pauseBeforeComplete = 0.2f;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool drawGizmos = false;
        
        private float _nextScanTime;
        private bool _isScanning;
        private bool _isProcessingDelivery;
        private QuestManager _cachedQuestManager;
        
        void Awake()
        {
            if (npcManager == null)
            {
                npcManager = GetComponent<NPCBehaviourManagerV2>();
                
                if (npcManager == null)
                {
                    Debug.LogError($"[NPCItemDetector:{name}] No se encontró NPCBehaviourManagerV2");
                }
            }
        }
        
        void Start()
        {
            // Verificar si el NPC tiene quest config con detección de ítems
            if (npcManager == null || npcManager.Configuration == null)
                return;
            
            var questConfig = npcManager.Configuration.questConfig;
            if (questConfig == null || !questConfig.enableItemDetection)
            {
                if (debugMode)
                    Debug.Log($"[NPCItemDetector:{name}] Item detection desactivado en config");
                enabled = false;
                return;
            }
            
            _isScanning = true;
            _isProcessingDelivery = false;
            _nextScanTime = Time.time + scanInterval;
            
            // Cachear QuestManager
            _cachedQuestManager = QuestManager.Instance;
            
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] Sistema de detección activado. Radio: {detectionRadius}, Intervalo: {scanInterval}s");
        }
        
        void Update()
        {
            if (!_isScanning)
                return;
            
            if (Time.time < _nextScanTime)
                return;
            
            _nextScanTime = Time.time + scanInterval;
            ScanForItems();
        }
        
        void ScanForItems()
        {
            // Early exit si ya estamos procesando una entrega
            if (_isProcessingDelivery)
                return;
            
            if (npcManager == null || npcManager.Configuration == null)
                return;
            
            var questConfig = npcManager.Configuration.questConfig;
            if (questConfig == null || questConfig.questChain == null || questConfig.questChain.Length == 0)
                return;
            
            // Usar QuestManager cacheado
            if (_cachedQuestManager == null)
            {
                _cachedQuestManager = QuestManager.Instance;
                if (_cachedQuestManager == null)
                    return;
            }
            
            // Escanear objetos cercanos (máximo 10 para evitar sobrecarga)
            var colliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayer);
            
            // Early exit si no hay nada cercano
            if (colliders.Length == 0)
                return;
            
            foreach (var col in colliders)
            {
                // Buscar SimpleQuestPickup en el objeto
                var pickup = col.GetComponent<SimpleQuestPickup>();
                if (pickup == null)
                    continue;
                
                // Verificar ángulo de visión (si no es 180°)
                if (detectionAngle < 180f)
                {
                    Vector3 directionToItem = col.transform.position - transform.position;
                    // Evitar normalización si es innecesario usando dot product
                    float dot = Vector3.Dot(transform.forward, directionToItem.normalized);
                    float angleThreshold = Mathf.Cos(detectionAngle * Mathf.Deg2Rad);
                    
                    if (dot < angleThreshold)
                        continue; // Fuera del ángulo de visión
                }
                
                // Buscar quest activa que coincida
                for (int i = 0; i < questConfig.questChain.Length; i++)
                {
                    var entry = questConfig.questChain[i];
                    
                    // Solo procesar quests con auto-detección activada
                    if (!entry.autoDetectItemDelivery)
                        continue;
                    
                    // Verificar que la quest esté activa
                    if (entry.questData == null)
                        continue;
                    
                    string questId = entry.questData.questId;
                    var questState = _cachedQuestManager.GetState(questId);
                    
                    if (questState != QuestState.Active)
                        continue;
                    
                    // Verificar tag del ítem (si está configurado)
                    if (!string.IsNullOrEmpty(entry.itemTag))
                    {
                        if (!col.CompareTag(entry.itemTag))
                            continue;
                    }
                    
                    // ¡Encontrado! Completar el paso
                    if (debugMode)
                        Debug.Log($"[NPCItemDetector:{name}] Ítem detectado para quest {questId}, completando paso {entry.itemDeliveryStepIndex}");
                    
                    // Marcar que estamos procesando para evitar detecciones duplicadas
                    _isProcessingDelivery = true;
                    
                    // IMPORTANTE: Procesar la entrega de forma asíncrona para que las animaciones se reseteen correctamente
                    StartCoroutine(ProcessItemDelivery(col.gameObject, questId, entry.itemDeliveryStepIndex, entry));
                    
                    // Detener el escaneo - ya encontramos el item
                    _isScanning = false;
                    return;
                }
            }
        }
        
        void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;
            
            // Dibujar radio de detección
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            // Dibujar ángulo de visión
            if (detectionAngle < 180f)
            {
                Gizmos.color = Color.cyan;
                Vector3 forward = transform.forward * detectionRadius;
                Vector3 left = Quaternion.Euler(0, -detectionAngle, 0) * forward;
                Vector3 right = Quaternion.Euler(0, detectionAngle, 0) * forward;
                
                Gizmos.DrawLine(transform.position, transform.position + left);
                Gizmos.DrawLine(transform.position, transform.position + right);
            }
        }
        
        /// <summary>
        /// Fuerza al jugador a volver al estado idle, reseteando animaciones de pickup/carry
        /// </summary>
        private void ForcePlayerToIdle()
        {
            // Obtener el animator del jugador
            if (!PlayerService.TryGetComponent(out Animator playerAnimator))
            {
                if (debugMode)
                    Debug.LogWarning($"[NPCItemDetector:{name}] No se pudo obtener Animator del player");
                return;
            }
            
            // Resetear parámetros del animator solo si existen
            TrySetAnimatorBool(playerAnimator, "IsCarrying", false);
            TrySetAnimatorBool(playerAnimator, "IsPickingUp", false);
            TrySetAnimatorTrigger(playerAnimator, "DropObject");
            
            // Resetear velocidades de movimiento a 0 para forzar idle
            TrySetAnimatorFloat(playerAnimator, "InputMagnitude", 0f);
            TrySetAnimatorFloat(playerAnimator, "Speed", 0f);
            TrySetAnimatorFloat(playerAnimator, "VerticalVelocity", 0f);
            
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] Jugador forzado a idle");
        }
        
        private void TrySetAnimatorBool(Animator anim, string paramName, bool value)
        {
            if (anim == null) return;
            foreach (var param in anim.parameters)
            {
                if (param.name == paramName && param.type == AnimatorControllerParameterType.Bool)
                {
                    anim.SetBool(paramName, value);
                    return;
                }
            }
        }
        
        private void TrySetAnimatorFloat(Animator anim, string paramName, float value)
        {
            if (anim == null) return;
            foreach (var param in anim.parameters)
            {
                if (param.name == paramName && param.type == AnimatorControllerParameterType.Float)
                {
                    anim.SetFloat(paramName, value);
                    return;
                }
            }
        }
        
        private void TrySetAnimatorTrigger(Animator anim, string paramName)
        {
            if (anim == null) return;
            foreach (var param in anim.parameters)
            {
                if (param.name == paramName && param.type == AnimatorControllerParameterType.Trigger)
                {
                    anim.SetTrigger(paramName);
                    return;
                }
            }
        }
        
        /// <summary>
        /// Procesa la entrega del ítem de forma asíncrona para evitar conflictos de animación
        /// </summary>
        private System.Collections.IEnumerator ProcessItemDelivery(GameObject itemObject, string questId, int stepIndex, Game.NPC.Modules.QuestChainEntry entry)
        {
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] Iniciando ProcessItemDelivery para quest {questId}");
            
            // 1. PRIMERO: Reproducir animación de soltar sin soltar físicamente el objeto
            if (PlayerService.TryGetComponent(out PlayerCarrySystem carrySystem))
            {
                if (carrySystem.IsCarrying && carrySystem.CarriedObject == itemObject)
                {
                    if (debugMode)
                        Debug.Log($"[NPCItemDetector:{name}] Reproduciendo animación de drop");
                    
                    // Reproducir solo la animación de throw sin soltar el objeto
                    if (PlayerService.TryGetComponent(out Animator animator))
                    {
                        animator.CrossFade("CarryThrow_NoWeapon", 0.2f, 1);
                    }
                    
                    // Esperar a que la animación de soltar termine
                    yield return new WaitForSeconds(dropAnimationDuration);
                    
                    // Forzar limpieza del estado de carrying antes de destruir el objeto
                    ForceStopCarrying(carrySystem, itemObject);
                    
                    // Pequeña pausa después de soltar para que se vea natural
                    yield return new WaitForSeconds(pauseAfterDrop);
                }
            }
            
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] Animación completada, destruyendo objeto");
            
            // 2. DESPUÉS: Destruir el objeto
            if (itemObject != null)
                Destroy(itemObject);
            
            // 3. Esperar varios frames para asegurar que la destrucción se procesó
            yield return new WaitForSeconds(pauseBeforeComplete);
            
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] Completando quest step");
            
            // 4. FINALMENTE: Completar la quest (esto puede lanzar cinemática)
            var qm = QuestManager.Instance;
            if (qm != null)
                qm.MarkStepDone(questId, stepIndex);
            
            // 5. Invocar eventos (pueden iniciar acciones post-quest)
            entry.onQuestCompleted?.Invoke();
            
            // Marcar fin de procesamiento
            _isProcessingDelivery = false;
            
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] ProcessItemDelivery completado");
        }
        
        /// <summary>
        /// Fuerza la limpieza del estado de carrying sin soltar físicamente el objeto
        /// </summary>
        private void ForceStopCarrying(PlayerCarrySystem carrySystem, GameObject carriedObject)
        {
            // Usar reflexión para limpiar el estado interno del PlayerCarrySystem
            var type = carrySystem.GetType();
            
            // Cancelar el Invoke de PhysicallyDropObject si existe
            carrySystem.CancelInvoke("PhysicallyDropObject");
            
            // Limpiar las variables privadas usando reflexión
            var carriedObjectField = type.GetField("_carriedObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var carriedRigidbodyField = type.GetField("_carriedRigidbody", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var carriedPickupObjectField = type.GetField("_carriedPickupObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var isCarryingField = type.GetField("_isCarrying", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (carriedObjectField != null) carriedObjectField.SetValue(carrySystem, null);
            if (carriedRigidbodyField != null) carriedRigidbodyField.SetValue(carrySystem, null);
            if (carriedPickupObjectField != null) carriedPickupObjectField.SetValue(carrySystem, null);
            if (isCarryingField != null) isCarryingField.SetValue(carrySystem, false);
            
            // Restaurar el peso de la capa del animator
            if (PlayerService.TryGetComponent(out Animator animator))
            {
                animator.SetLayerWeight(1, 0f);
            }
            
            // Restaurar el ActionMode
            if (PlayerService.TryGetComponent(out PlayerActionManager actionManager))
            {
                actionManager.PopMode(ActionMode.Carrying);
            }
            
            if (debugMode)
                Debug.Log($"[NPCItemDetector:{name}] Estado de carrying limpiado");
        }
    }
}

