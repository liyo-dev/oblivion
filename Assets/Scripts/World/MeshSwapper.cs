using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

namespace Game.World
{
    /// <summary>
    /// Componente que permite intercambiar el mesh de un objeto.
    /// Útil para cambios destructivos (montaña entera → montaña partida).
    /// Puede ser llamado desde Timeline (Signal), UnityEvents, o código.
    /// </summary>
    public class MeshSwapper : MonoBehaviour, INotificationReceiver
    {
        [Header("Configuración")]
        [Tooltip("El MeshFilter del objeto que cambiará de mesh")]
        [SerializeField] private MeshFilter targetMeshFilter;
        
        [Tooltip("El nuevo mesh que se aplicará")]
        [SerializeField] private Mesh newMesh;
        
        [Tooltip("¿También cambiar el MeshCollider si existe?")]
        [SerializeField] private bool updateCollider = true;
        
        [Header("Opciones Alternativas")]
        [Tooltip("En lugar de cambiar mesh, ¿desactivar este GO y activar otro?")]
        [SerializeField] private bool useGameObjectSwap = false;
        
        [Tooltip("GameObject a desactivar (ej: montaña entera)")]
        [SerializeField] private GameObject objectToDeactivate;
        
        [Tooltip("GameObject a activar (ej: montaña destruida)")]
        [SerializeField] private GameObject objectToActivate;
        
        [Header("Efectos")]
        [Tooltip("Partículas a reproducir al cambiar (explosión, escombros, etc.)")]
        [SerializeField] private ParticleSystem[] effectsOnSwap;
        
        [Tooltip("¿Mover los efectos a la posición del objeto target antes de reproducir?")]
        [SerializeField] private bool moveEffectsToTarget = true;
        
        [Tooltip("Offset de posición para los efectos (respecto al target)")]
        [SerializeField] private Vector3 effectsPositionOffset = Vector3.zero;
        
        [Tooltip("Audio a reproducir al cambiar")]
        [SerializeField] private AudioSource audioOnSwap;
        
        [Header("Persistencia")]
        [Tooltip("ID único para guardar el estado (vacío = no persistir)")]
        [SerializeField] private string persistenceId = "";
        
        [Header("Eventos")]
        public UnityEvent OnMeshSwapped;
        
        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        
        // Estado
        private bool _hasSwapped = false;
        
        public bool HasSwapped => _hasSwapped;

        private void Start()
        {
            // Restaurar estado si tiene persistencia
            if (!string.IsNullOrEmpty(persistenceId))
            {
                RestoreState();
            }
        }

        /// <summary>
        /// Ejecuta el cambio de mesh o de GameObjects.
        /// Llamar desde Timeline Signal, UnityEvent, o código.
        /// </summary>
        public void Swap()
        {
            if (_hasSwapped)
            {
                if (debugMode) Debug.Log($"[MeshSwapper:{name}] Ya fue cambiado anteriormente, ignorando");
                return;
            }

            if (debugMode) Debug.Log($"[MeshSwapper:{name}] 💥 Ejecutando swap...");

            if (useGameObjectSwap)
            {
                ExecuteGameObjectSwap();
            }
            else
            {
                ExecuteMeshSwap();
            }

            // Reproducir efectos
            PlayEffects();

            // Marcar como cambiado
            _hasSwapped = true;

            // Persistir estado
            if (!string.IsNullOrEmpty(persistenceId))
            {
                SaveState();
            }

            // Disparar evento
            OnMeshSwapped?.Invoke();

            if (debugMode) Debug.Log($"[MeshSwapper:{name}] ✅ Swap completado");
        }

        /// <summary>
        /// Cambia el mesh del MeshFilter
        /// </summary>
        private void ExecuteMeshSwap()
        {
            if (targetMeshFilter == null)
            {
                Debug.LogError($"[MeshSwapper:{name}] ❌ No hay MeshFilter asignado");
                return;
            }

            if (newMesh == null)
            {
                Debug.LogError($"[MeshSwapper:{name}] ❌ No hay nuevo mesh asignado");
                return;
            }

            // Cambiar el mesh visual
            targetMeshFilter.mesh = newMesh;

            // Actualizar collider si existe
            if (updateCollider)
            {
                var meshCollider = targetMeshFilter.GetComponent<MeshCollider>();
                if (meshCollider != null)
                {
                    meshCollider.sharedMesh = newMesh;
                    if (debugMode) Debug.Log($"[MeshSwapper:{name}] MeshCollider actualizado");
                }
            }

            if (debugMode) Debug.Log($"[MeshSwapper:{name}] Mesh cambiado a '{newMesh.name}'");
        }

        /// <summary>
        /// Intercambia GameObjects (desactiva uno, activa otro)
        /// </summary>
        private void ExecuteGameObjectSwap()
        {
            if (objectToDeactivate != null)
            {
                objectToDeactivate.SetActive(false);
                if (debugMode) Debug.Log($"[MeshSwapper:{name}] '{objectToDeactivate.name}' desactivado");
            }

            if (objectToActivate != null)
            {
                objectToActivate.SetActive(true);
                if (debugMode) Debug.Log($"[MeshSwapper:{name}] '{objectToActivate.name}' activado");
            }
        }

        /// <summary>
        /// Reproduce efectos visuales y de audio
        /// </summary>
        private void PlayEffects()
        {
            if (effectsOnSwap != null && effectsOnSwap.Length > 0)
            {
                // Calcular posición objetivo para los efectos
                Vector3 targetPosition = transform.position;
                
                if (moveEffectsToTarget)
                {
                    // Usar la posición del MeshFilter target si existe
                    if (targetMeshFilter != null)
                    {
                        // Usar el centro del bounds del mesh para mejor posicionamiento
                        var targetRenderer = targetMeshFilter.GetComponent<Renderer>();
                        if (targetRenderer != null)
                        {
                            targetPosition = targetRenderer.bounds.center;
                        }
                        else
                        {
                            targetPosition = targetMeshFilter.transform.position;
                        }
                    }
                    // O usar el centro del objeto a desactivar si es GameObject swap
                    else if (useGameObjectSwap && objectToDeactivate != null)
                    {
                        var objRenderer = objectToDeactivate.GetComponent<Renderer>();
                        if (objRenderer != null)
                        {
                            targetPosition = objRenderer.bounds.center;
                        }
                        else
                        {
                            targetPosition = objectToDeactivate.transform.position;
                        }
                    }
                }
                
                // Aplicar offset
                targetPosition += effectsPositionOffset;
                
                if (debugMode) Debug.Log($"[MeshSwapper:{name}] 💨 Instanciando {effectsOnSwap.Length} efectos en posición {targetPosition}");
                
                foreach (var effectPrefab in effectsOnSwap)
                {
                    if (effectPrefab != null)
                    {
                        // ✅ INSTANCIAR el efecto en la posición objetivo
                        GameObject effectInstance = Instantiate(effectPrefab.gameObject, targetPosition, Quaternion.identity);
                        
                        // Obtener el ParticleSystem de la instancia
                        ParticleSystem ps = effectInstance.GetComponent<ParticleSystem>();
                        if (ps != null)
                        {
                            // Reproducir
                            ps.Play(true);
                            
                            // Calcular duración total para auto-destrucción
                            float duration = ps.main.duration + ps.main.startLifetime.constantMax;
                            
                            // Destruir después de que termine
                            Destroy(effectInstance, duration + 1f);
                            
                            if (debugMode) Debug.Log($"[MeshSwapper:{name}] ✅ Efecto '{effectPrefab.name}' instanciado en {targetPosition}, se destruirá en {duration + 1f}s");
                        }
                        else
                        {
                            // Si no tiene ParticleSystem, destruir después de 5 segundos
                            Destroy(effectInstance, 5f);
                        }
                    }
                }
            }

            if (audioOnSwap != null)
            {
                audioOnSwap.Play();
            }
        }

        #region Persistencia

        private void SaveState()
        {
            PlayerPrefs.SetInt($"MeshSwapper_{persistenceId}", _hasSwapped ? 1 : 0);
            PlayerPrefs.Save();
            if (debugMode) Debug.Log($"[MeshSwapper:{name}] Estado guardado: {_hasSwapped}");
        }

        private void RestoreState()
        {
            bool wasSwapped = PlayerPrefs.GetInt($"MeshSwapper_{persistenceId}", 0) == 1;
            
            if (wasSwapped)
            {
                if (debugMode) Debug.Log($"[MeshSwapper:{name}] Restaurando estado: ya estaba swapped");
                
                // Aplicar el swap silenciosamente (sin efectos)
                if (useGameObjectSwap)
                {
                    if (objectToDeactivate != null) objectToDeactivate.SetActive(false);
                    if (objectToActivate != null) objectToActivate.SetActive(true);
                }
                else if (targetMeshFilter != null && newMesh != null)
                {
                    targetMeshFilter.mesh = newMesh;
                    if (updateCollider)
                    {
                        var meshCollider = targetMeshFilter.GetComponent<MeshCollider>();
                        if (meshCollider != null) meshCollider.sharedMesh = newMesh;
                    }
                }
                
                _hasSwapped = true;
            }
        }

        /// <summary>
        /// Resetea el estado (útil para testing)
        /// </summary>
        [ContextMenu("Reset State")]
        public void ResetState()
        {
            _hasSwapped = false;
            if (!string.IsNullOrEmpty(persistenceId))
            {
                PlayerPrefs.DeleteKey($"MeshSwapper_{persistenceId}");
            }
            Debug.Log($"[MeshSwapper:{name}] Estado reseteado");
        }

        #endregion

        #region Editor Helpers

        [ContextMenu("Test Swap")]
        private void TestSwap()
        {
            Swap();
        }

        private void OnValidate()
        {
            // Auto-detectar MeshFilter si no está asignado
            if (targetMeshFilter == null && !useGameObjectSwap)
            {
                targetMeshFilter = GetComponent<MeshFilter>();
            }
        }

        #endregion
        
        #region Timeline Signal Receiver
        
        /// <summary>
        /// Recibe notificaciones de Timeline (Signal Emitters).
        /// Para usar: añade un Signal Track en Timeline, apunta a este objeto,
        /// y usa cualquier SignalAsset - al recibirlo ejecutará Swap().
        /// </summary>
        public void OnNotify(Playable origin, INotification notification, object context)
        {
            if (debugMode) Debug.Log($"[MeshSwapper:{name}] 📡 Signal recibido de Timeline: {notification}");
            Swap();
        }
        
        #endregion
    }
}
