using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace Game.NPC.Common
{
    /// <summary>
    /// Controla los iconos visuales sobre la cabeza del NPC.
    /// Los prefabs se configuran en NPCCombatConfig y se pasan como parámetros.
    /// Solo usa prefabs reutilizables - La forma más limpia y profesional.
    /// Incluye animaciones DOTween para aparición/desaparición.
    /// </summary>
    public class NPCAlertIconController : MonoBehaviour
    {
        [Header("Configuración de Posición")]
        [Tooltip("Offset adicional sobre la cabeza del NPC")]
        [SerializeField] private Vector3 iconOffset = new Vector3(0f, 0.3f, 0f);
        
        [Tooltip("Altura de fallback si no se encuentra la cabeza (desde transform.position)")]
        [SerializeField] private float fallbackHeight = 2.2f;
        
        [Tooltip("Nombres de huesos a buscar para la cabeza (en orden de prioridad)")]
        [SerializeField] private string[] headBoneNames = { "Head", "head", "Bip001 Head", "mixamorig:Head", "Cabeza" };
        
        [Header("Configuración de Duración")]
        [Tooltip("Duración del icono en segundos (si no se especifica)")]
        [SerializeField] private float iconDuration = 2f;
        
        [Header("Animación de Bounce")]
        [Tooltip("Si está activo, el icono hace bounce animado")]
        [SerializeField] private bool animateBounce = true;
        
        [Tooltip("Amplitud del bounce")]
        [SerializeField] private float bounceAmplitude = 0.15f;
        
        [Tooltip("Velocidad del bounce")]
        [SerializeField] private float bounceSpeed = 4f;
        
        [Header("Animaciones DOTween")]
        [Tooltip("Duración de la animación de aparición")]
        [SerializeField] private float showAnimDuration = 0.25f;
        
        [Tooltip("Duración de la animación de desaparición")]
        [SerializeField] private float hideAnimDuration = 0.15f;
        
        [Tooltip("Escala del icono")]
        [SerializeField] private float iconScale = 1f;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = false;
        
        // Referencias
        private GameObject _currentIconInstance;
        private Coroutine _iconRoutine;
        private Camera _mainCamera;
        private Tween _currentTween;
        private Transform _headBone;
        private bool _headBoneSearched;
        
        // Estado
        private bool _isHiding;
        private bool _isShowing;
        private bool _hiddenDuringDialogue;
        private Vector3 _targetScale;
        
        private void Start()
        {
            UpdateCameraReference();
            FindHeadBone();
        }
        
        private void OnEnable()
        {
            DialogueManager.OnDialogueStarted += OnDialogueStarted;
            DialogueManager.OnDialogueClosed += OnDialogueClosed;
        }
        
        private void OnDisable()
        {
            DialogueManager.OnDialogueStarted -= OnDialogueStarted;
            DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        }
        
        /// <summary>
        /// Busca el hueso de la cabeza del NPC para posicionar el icono correctamente
        /// </summary>
        private void FindHeadBone()
        {
            if (_headBoneSearched) return;
            _headBoneSearched = true;
            
            // Buscar Animator primero
            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (_headBone != null)
                {
                    if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] ✅ Cabeza encontrada via Animator: {_headBone.name}");
                    return;
                }
            }
            
            // Buscar por nombre de hueso
            foreach (string boneName in headBoneNames)
            {
                _headBone = FindChildRecursive(transform, boneName);
                if (_headBone != null)
                {
                    if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] ✅ Cabeza encontrada por nombre: {_headBone.name}");
                    return;
                }
            }
            
            if (showDebugLogs) Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ No se encontró hueso de cabeza, usando fallback height={fallbackHeight}");
        }
        
        private Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    return child;
                    
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
        
        /// <summary>
        /// Calcula la posición donde debe aparecer el icono.
        /// Usa fallbackHeight como la altura desde los pies del NPC, más iconOffset adicional.
        /// Si se encuentra la cabeza y está cerca de fallbackHeight, usa la cabeza como referencia.
        /// </summary>
        private Vector3 GetIconWorldPosition()
        {
            // Posición base: pies del NPC
            Vector3 feetPosition = transform.position;
            
            // La altura configurada (alertIconHeight) se usa directamente
            Vector3 targetPos = feetPosition + Vector3.up * fallbackHeight;
            
            // Si encontramos la cabeza, verificar si está cerca de la altura configurada
            // y usarla para un posicionamiento más preciso
            if (_headBone != null)
            {
                float headHeight = _headBone.position.y - feetPosition.y;
                
                // Si la cabeza está por debajo de la altura configurada,
                // usar la cabeza + un pequeño offset para mantener el icono visible
                if (headHeight > 0 && headHeight < fallbackHeight)
                {
                    // Usar la posición de la cabeza + la diferencia hasta fallbackHeight
                    float extraHeight = fallbackHeight - headHeight;
                    targetPos = _headBone.position + Vector3.up * Mathf.Max(extraHeight, 0.2f);
                }
            }
            
            // Agregar offset adicional (X, Z principalmente, Y es pequeño ~0.2)
            return targetPos + iconOffset;
        }
        
        /// <summary>
        /// Cuando inicia un diálogo, ocultar el icono temporalmente
        /// </summary>
        private void OnDialogueStarted(Transform npcInvolved)
        {
            if (_currentIconInstance != null && !_isHiding && !_hiddenDuringDialogue)
            {
                _hiddenDuringDialogue = true;
                
                // Matar cualquier tween en progreso
                _currentTween?.Kill();
                
                // Ocultar con animación rápida pero NO destruir
                _currentIconInstance.transform.DOScale(Vector3.zero, 0.1f)
                    .SetEase(Ease.InBack)
                    .SetId(this);
                    
                if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] 🔇 Ocultando icono durante diálogo");
            }
        }
        
        /// <summary>
        /// Cuando termina un diálogo, restaurar el icono si estaba visible
        /// </summary>
        private void OnDialogueClosed(Transform npcInvolved)
        {
            if (_hiddenDuringDialogue && _currentIconInstance != null && !_isHiding)
            {
                _hiddenDuringDialogue = false;
                
                // Restaurar escala con animación
                DOTween.Kill(this);
                _currentIconInstance.transform.DOScale(_targetScale, 0.2f)
                    .SetEase(Ease.OutBack)
                    .SetId(this);
                    
                if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] 🔊 Restaurando icono tras diálogo");
            }
        }
        
        /// <summary>
        /// Actualiza la referencia de cámara para usar la cámara activa actual
        /// </summary>
        private void UpdateCameraReference()
        {
            // Prioridad 1: Camera.main si está habilitada
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.isActiveAndEnabled)
            {
                _mainCamera = mainCam;
                return;
            }
            
            // Prioridad 2: Buscar cualquier cámara habilitada con mayor depth
            Camera[] allCameras = Camera.allCameras;
            Camera bestCamera = null;
            float highestDepth = float.MinValue;
            
            foreach (Camera cam in allCameras)
            {
                if (cam != null && cam.isActiveAndEnabled && cam.depth > highestDepth)
                {
                    highestDepth = cam.depth;
                    bestCamera = cam;
                }
            }
            
            if (bestCamera != null)
            {
                _mainCamera = bestCamera;
                return;
            }
            
            _mainCamera = mainCam;
        }
        
        private Camera GetCurrentCamera()
        {
            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
            {
                UpdateCameraReference();
            }
            return _mainCamera;
        }
        
        /// <summary>
        /// Muestra un icono de alerta usando un prefab de GameObject
        /// </summary>
        public void ShowAlertIcon(GameObject iconPrefab, float duration = -1f)
        {
            if (iconPrefab == null)
            {
                Debug.LogWarning($"[NPCAlertIconController:{name}] IconPrefab es null");
                return;
            }
            
            // Asegurarse de encontrar la cabeza
            if (!_headBoneSearched) FindHeadBone();
            
            // Limpiar icono anterior inmediatamente
            HideAlertIconImmediate();
            
            float useDuration = duration > 0f ? duration : iconDuration;
            _targetScale = Vector3.one * iconScale;
            _iconRoutine = StartCoroutine(ShowIconRoutine(iconPrefab, useDuration));
            
            if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] 🔔 Mostrando icono por {useDuration}s");
        }

        /// <summary>
        /// Muestra un bocadillo de texto (speech/thought bubble)
        /// </summary>
        public void ShowSpeechBubble(GameObject bubblePrefab, string text, float duration = 3f)
        {
            if (bubblePrefab == null) return;
            
            if (!_headBoneSearched) FindHeadBone();
            HideAlertIconImmediate();
            
            _targetScale = Vector3.one * iconScale;
            _iconRoutine = StartCoroutine(ShowIconRoutine(bubblePrefab, duration, (instance) => {
                // Configurar texto
                var tmpro = instance.GetComponentInChildren<TMPro.TMP_Text>();
                if (tmpro != null) tmpro.text = text;
                else {
                    var uiText = instance.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (uiText != null) uiText.text = text;
                }
            }));
            
            if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] 💬 Mostrando bocadillo: '{text}'");
        }
        
        /// <summary>
        /// Muestra el icono de alerta (❗) - Detectó al jugador
        /// </summary>
        public void ShowAlert(GameObject alertPrefab, float duration = -1f)
        {
            if (alertPrefab != null)
            {
                ShowAlertIcon(alertPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ alertPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Muestra el icono de interrogación (❓) - Buscando al jugador
        /// </summary>
        public void ShowQuestion(GameObject questionPrefab, float duration = -1f)
        {
            if (questionPrefab != null)
            {
                ShowAlertIcon(questionPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ questionPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Establece la altura del icono sobre el NPC.
        /// El valor representa la altura TOTAL desde los pies del NPC donde debe aparecer el icono.
        /// Si se encuentra la cabeza del NPC, se usa como referencia y se ajusta el offset.
        /// Si no se encuentra, se usa directamente como altura desde transform.position.
        /// </summary>
        public void SetIconOffset(Vector3 offset)
        {
            // Guardar la altura deseada como fallbackHeight (altura desde los pies)
            if (offset.y > 0)
            {
                fallbackHeight = offset.y;
            }
            
            // El iconOffset será un pequeño offset adicional sobre el punto de anclaje
            // (ya sea la cabeza encontrada o el fallbackHeight)
            iconOffset = new Vector3(offset.x, 0.2f, offset.z);
            
            if (showDebugLogs) 
                Debug.Log($"[NPCAlertIcon:{name}] SetIconOffset: altura deseada={offset.y}, fallbackHeight={fallbackHeight}");
        }
        
        /// <summary>
        /// Establece directamente la altura del icono (valor Y desde los pies del NPC)
        /// </summary>
        public void SetIconHeight(float height)
        {
            if (height > 0)
            {
                fallbackHeight = height;
            }
            
            if (showDebugLogs)
                Debug.Log($"[NPCAlertIcon:{name}] SetIconHeight: {height}");
        }
        
        /// <summary>
        /// Muestra el icono de admiración (❗) - ¡Encontró al jugador!
        /// </summary>
        public void ShowExclamation(GameObject exclamationPrefab, float duration = -1f)
        {
            if (exclamationPrefab != null)
            {
                ShowAlertIcon(exclamationPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ exclamationPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Muestra un icono persistente (sin duración) - se mantiene hasta llamar HideAlertIcon()
        /// Útil para indicar que hay una narrativa/quest disponible
        /// </summary>
        public void ShowPersistentIcon(GameObject iconPrefab)
        {
            if (iconPrefab == null)
            {
                Debug.LogWarning($"[NPCAlertIconController:{name}] ⚠️ persistentIconPrefab es null");
                return;
            }
            
            // No mostrar si ya hay un icono activo del mismo prefab
            if (_currentIconInstance != null && _currentIconInstance.name.Contains(iconPrefab.name))
            {
                return;
            }
            
            // Asegurarse de encontrar la cabeza
            if (!_headBoneSearched) FindHeadBone();
            
            UpdateCameraReference();
            HideAlertIconImmediate();
            
            _targetScale = Vector3.one * iconScale;
            _iconRoutine = StartCoroutine(ShowPersistentIconRoutine(iconPrefab));
            
            if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] 📌 Mostrando icono persistente");
        }
        
        /// <summary>
        /// Verifica si hay un icono persistente activo
        /// </summary>
        public bool HasPersistentIcon => _currentIconInstance != null && !_isHiding;
        
        /// <summary>
        /// Oculta el icono de alerta actual con animación
        /// </summary>
        public void HideAlertIcon()
        {
            if (_iconRoutine != null)
            {
                StopCoroutine(_iconRoutine);
                _iconRoutine = null;
            }
            
            // Si ya se está ocultando o no hay icono, salir
            if (_isHiding || _currentIconInstance == null) return;

            _isHiding = true;
            _hiddenDuringDialogue = false;
            
            // Matar todos los tweens de este objeto
            DOTween.Kill(this);
            
            // Capturar referencia para el closure
            GameObject instanceToHide = _currentIconInstance;
            var iconTransform = instanceToHide.transform;
            
            // Asegurar que tiene escala válida antes de animar
            if (iconTransform.localScale.sqrMagnitude < 0.01f)
            {
                // Si la escala ya es casi cero, destruir directamente
                Destroy(instanceToHide);
                _currentIconInstance = null;
                _isHiding = false;
                return;
            }
            
            Vector3 targetPos = iconTransform.position + Vector3.down * 0.2f;
            
            Sequence hideSeq = DOTween.Sequence();
            hideSeq.Append(iconTransform.DOScale(Vector3.zero, hideAnimDuration).SetEase(Ease.InBack));
            hideSeq.Join(iconTransform.DOMove(targetPos, hideAnimDuration).SetEase(Ease.InQuad));
            hideSeq.OnComplete(() =>
            {
                if (instanceToHide != null)
                {
                    Destroy(instanceToHide);
                }
                
                if (_currentIconInstance == instanceToHide)
                {
                    _currentIconInstance = null;
                    _isHiding = false;
                }
            });
            hideSeq.SetId(this);
            _currentTween = hideSeq;
            
            if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] 🔕 Ocultando icono con animación");
        }
        
        /// <summary>
        /// Oculta el icono inmediatamente sin animación (para cleanup)
        /// </summary>
        public void HideAlertIconImmediate()
        {
            DOTween.Kill(this);
            _currentTween?.Kill();
            
            if (_iconRoutine != null)
            {
                StopCoroutine(_iconRoutine);
                _iconRoutine = null;
            }
            
            if (_currentIconInstance != null)
            {
                Destroy(_currentIconInstance);
                _currentIconInstance = null;
            }
            
            _isHiding = false;
            _isShowing = false;
            _hiddenDuringDialogue = false;
            
            if (showDebugLogs) Debug.Log($"[NPCAlertIcon:{name}] ⚡ Icono eliminado inmediatamente");
        }
        
        private IEnumerator ShowIconRoutine(GameObject iconPrefab, float duration, System.Action<GameObject> onInstanceCreated = null)
        {
            _isShowing = true;
            
            // Instanciar en WORLD SPACE
            _currentIconInstance = Instantiate(iconPrefab);
            _currentIconInstance.name = $"{iconPrefab.name}_{name}_Alert";
            
            // Callback de configuración
            onInstanceCreated?.Invoke(_currentIconInstance);
            
            // Calcular posición inicial (ligeramente debajo de la posición final para el pop-up)
            Vector3 targetPos = GetIconWorldPosition();
            Vector3 startPos = targetPos + Vector3.down * 0.3f;
            
            _currentIconInstance.transform.position = startPos;
            _currentIconInstance.transform.rotation = Quaternion.identity;
            _currentIconInstance.transform.localScale = Vector3.zero;
            _currentIconInstance.SetActive(true);
            
            // Aplicar billboard inicial
            ApplyBillboard(_currentIconInstance.transform);
            
            // Animar aparición
            var iconTransform = _currentIconInstance.transform;
            
            DOTween.Kill(this); // Matar tweens anteriores de este objeto
            
            Sequence showSeq = DOTween.Sequence();
            showSeq.Append(iconTransform.DOScale(_targetScale, showAnimDuration).SetEase(Ease.OutBack));
            showSeq.Join(iconTransform.DOMove(targetPos, showAnimDuration).SetEase(Ease.OutBack));
            showSeq.SetId(this);
            _currentTween = showSeq;
            
            yield return new WaitForSeconds(showAnimDuration);
            
            _isShowing = false;
            
            // Loop de actualización durante la duración
            float elapsed = 0f;
            float remainingDuration = duration - showAnimDuration;
            float baseTime = Time.time;
            
            while (elapsed < remainingDuration)
            {
                if (_currentIconInstance == null || _isHiding) yield break;
                
                // No actualizar si está oculto por diálogo
                if (!_hiddenDuringDialogue)
                {
                    // Actualizar posición siguiendo la cabeza del NPC
                    Vector3 newTargetPos = GetIconWorldPosition();
                    
                    // Aplicar bounce si está activado
                    if (animateBounce)
                    {
                        float bounce = Mathf.Sin((Time.time - baseTime) * bounceSpeed) * bounceAmplitude;
                        newTargetPos.y += bounce;
                    }
                    
                    _currentIconInstance.transform.position = newTargetPos;
                    
                    // Mantener billboard hacia cámara
                    ApplyBillboard(_currentIconInstance.transform);
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Terminar con animación de ocultado
            HideAlertIcon();
        }
        
        /// <summary>
        /// Aplica efecto billboard para que el icono mire a la cámara
        /// </summary>
        private void ApplyBillboard(Transform iconTransform)
        {
            var cam = GetCurrentCamera();
            if (cam != null && iconTransform != null)
            {
                Vector3 lookDir = cam.transform.position - iconTransform.position;
                lookDir.y = 0; // Solo rotar en Y para mantener vertical
                
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    iconTransform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
        }
        
        /// <summary>
        /// Rutina para icono persistente (sin duración, se mantiene hasta HideAlertIcon)
        /// </summary>
        private IEnumerator ShowPersistentIconRoutine(GameObject iconPrefab)
        {
            _isShowing = true;
            
            // Instanciar en WORLD SPACE
            _currentIconInstance = Instantiate(iconPrefab);
            _currentIconInstance.name = $"{iconPrefab.name}_{name}_Persistent";
            
            // Calcular posición inicial
            Vector3 targetPos = GetIconWorldPosition();
            Vector3 startPos = targetPos + Vector3.down * 0.3f;
            
            _currentIconInstance.transform.position = startPos;
            _currentIconInstance.transform.rotation = Quaternion.identity;
            _currentIconInstance.transform.localScale = Vector3.zero;
            _currentIconInstance.SetActive(true);
            
            // Aplicar billboard inicial
            ApplyBillboard(_currentIconInstance.transform);
            
            // Animar aparición
            var iconTransform = _currentIconInstance.transform;
            
            DOTween.Kill(this);
            
            Sequence showSeq = DOTween.Sequence();
            showSeq.Append(iconTransform.DOScale(_targetScale, showAnimDuration).SetEase(Ease.OutBack));
            showSeq.Join(iconTransform.DOMove(targetPos, showAnimDuration).SetEase(Ease.OutBack));
            showSeq.SetId(this);
            _currentTween = showSeq;
            
            yield return new WaitForSeconds(showAnimDuration);
            
            _isShowing = false;
            float baseTime = Time.time;
            
            // Loop de actualización infinito (hasta que se llame HideAlertIcon)
            while (_currentIconInstance != null && !_isHiding)
            {
                if (!_hiddenDuringDialogue)
                {
                    Vector3 newTargetPos = GetIconWorldPosition();
                    
                    if (animateBounce)
                    {
                        float bounce = Mathf.Sin((Time.time - baseTime) * bounceSpeed) * bounceAmplitude;
                        newTargetPos.y += bounce;
                    }
                    
                    _currentIconInstance.transform.position = newTargetPos;
                    ApplyBillboard(_currentIconInstance.transform);
                }
                
                yield return null;
            }
        }
        
        private void OnDestroy()
        {
            DOTween.Kill(this);
            _currentTween?.Kill();
            HideAlertIconImmediate();
        }
        
        /// <summary>
        /// Propiedad para verificar si hay un icono visible
        /// </summary>
        public bool HasActiveIcon => _currentIconInstance != null && !_isHiding;
    }
}
