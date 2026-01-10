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
        [Header("Configuración por Defecto")]
        [Tooltip("Offset vertical sobre el NPC donde aparece el icono")]
        [SerializeField] private Vector3 iconOffset = new Vector3(0f, 2.5f, 0f);
        
        [Tooltip("Duración del icono en segundos (si no se especifica)")]
        [SerializeField] private float iconDuration = 2f;
        
        [Tooltip("Si está activo, el icono hace bounce animado")]
        [SerializeField] private bool animateBounce = true;
        
        [Tooltip("Amplitud del bounce")]
        [SerializeField] private float bounceAmplitude = 0.2f;
        
        [Tooltip("Velocidad del bounce")]
        [SerializeField] private float bounceSpeed = 3f;
        
        [Header("Animaciones DOTween")]
        [Tooltip("Duración de la animación de aparición")]
        [SerializeField] private float showAnimDuration = 0.3f;
        
        [Tooltip("Duración de la animación de desaparición")]
        [SerializeField] private float hideAnimDuration = 0.2f;
        
        private GameObject _currentIconInstance;
        private Coroutine _iconRoutine;
        private Camera _mainCamera;
        private Tween _currentTween;
        private bool _isHiding;
        private bool _hiddenDuringDialogue; // Si se ocultó por diálogo
        private Vector3 _savedIconScale;    // Escala guardada antes de ocultar
        
        private void Start()
        {
            UpdateCameraReference();
        }
        
        private void OnEnable()
        {
            // Suscribirse a eventos de diálogo para ocultar iconos durante conversaciones
            DialogueManager.OnDialogueStarted += OnDialogueStarted;
            DialogueManager.OnDialogueClosed += OnDialogueClosed;
        }
        
        private void OnDisable()
        {
            DialogueManager.OnDialogueStarted -= OnDialogueStarted;
            DialogueManager.OnDialogueClosed -= OnDialogueClosed;
        }
        
        /// <summary>
        /// Cuando inicia un diálogo, ocultar el icono temporalmente
        /// </summary>
        private void OnDialogueStarted(Transform npcInvolved)
        {
            if (_currentIconInstance != null && !_isHiding && !_hiddenDuringDialogue)
            {
                _hiddenDuringDialogue = true;
                _savedIconScale = _currentIconInstance.transform.localScale;
                
                // Ocultar con animación rápida
                _currentTween?.Kill();
                _currentIconInstance.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack);
            }
        }
        
        /// <summary>
        /// Cuando termina un diálogo, restaurar el icono si estaba visible
        /// </summary>
        private void OnDialogueClosed(Transform npcInvolved)
        {
            if (_hiddenDuringDialogue && _currentIconInstance != null)
            {
                _hiddenDuringDialogue = false;
                
                // Restaurar con animación
                _currentTween?.Kill();
                _currentIconInstance.transform.DOScale(_savedIconScale, 0.2f).SetEase(Ease.OutBack);
            }
        }
        
        /// <summary>
        /// Actualiza la referencia de cámara para usar la cámara activa actual
        /// </summary>
        private void UpdateCameraReference()
        {
            // Buscar la cámara activa y habilitada
            // Prioridad 1: Camera.main si está habilitada
            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.isActiveAndEnabled)
            {
                _mainCamera = mainCam;
                return;
            }
            
            // Prioridad 2: Buscar cualquier cámara habilitada con mayor depth
            // (útil cuando hay múltiples cámaras, como durante diálogos)
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
            
            // Fallback: usar Camera.main aunque esté desactivada
            // (mejor que null)
            _mainCamera = mainCam;
        }
        
        /// <summary>
        /// Obtiene la cámara actual para el billboard (actualiza si es necesario)
        /// </summary>
        private Camera GetCurrentCamera()
        {
            // Si no hay cámara o la anterior ya no es válida, actualizar
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
            
            HideAlertIcon(); // Limpiar icono anterior si existe
            
            float useDuration = duration > 0f ? duration : iconDuration;
            _iconRoutine = StartCoroutine(ShowIconRoutine(iconPrefab, useDuration));
        }
        
        /// <summary>
        /// Muestra el icono de alerta (❗) - Detectó al jugador
        /// </summary>
        public void ShowAlert(GameObject alertPrefab, float duration = -1f)
        {
            if (alertPrefab != null)
            {
                // Debug.Log($"[NPCAlertIcon:{name}] ❗ Mostrando icono de alerta");
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
                // Debug.Log($"[NPCAlertIcon:{name}] ❓ Mostrando icono de interrogación (buscando)");
                ShowAlertIcon(questionPrefab, duration);
            }
            else
            {
                Debug.LogWarning($"[NPCAlertIcon:{name}] ⚠️ questionPrefab no proporcionado");
            }
        }
        
        /// <summary>
        /// Establece el offset del icono respecto al NPC
        /// </summary>
        public void SetIconOffset(Vector3 offset)
        {
            iconOffset = offset;
        }
        
        /// <summary>
        /// Muestra el icono de admiración (❗) - ¡Encontró al jugador!
        /// </summary>
        public void ShowExclamation(GameObject exclamationPrefab, float duration = -1f)
        {
            if (exclamationPrefab != null)
            {
                // Debug.Log($"[NPCAlertIcon:{name}] ❗ Mostrando icono de admiración (¡encontrado!)");
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
            
            // IMPORTANTE: Actualizar referencia de cámara antes de mostrar el icono
            // Esto es crucial para NPCs que se activan dinámicamente después de condiciones narrativas
            UpdateCameraReference();
            
            HideAlertIcon(); // Limpiar icono anterior si existe
            
            _iconRoutine = StartCoroutine(ShowPersistentIconRoutine(iconPrefab));
            // Debug.Log($"[NPCAlertIcon:{name}] 📍 Mostrando icono persistente");
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
            
            if (_currentIconInstance != null && !_isHiding)
            {
                _isHiding = true;
                
                // Matar cualquier tween anterior
                _currentTween?.Kill();
                
                // Animar desaparición: escala + mover hacia abajo (en WORLD SPACE)
                var iconTransform = _currentIconInstance.transform;
                float targetY = iconTransform.position.y - 0.3f;
                
                Sequence hideSeq = DOTween.Sequence();
                hideSeq.Append(iconTransform.DOScale(Vector3.zero, hideAnimDuration).SetEase(Ease.InBack));
                hideSeq.Join(iconTransform.DOMoveY(targetY, hideAnimDuration).SetEase(Ease.InQuad));
                hideSeq.OnComplete(() =>
                {
                    if (_currentIconInstance != null)
                    {
                        Destroy(_currentIconInstance);
                        _currentIconInstance = null;
                    }
                    _isHiding = false;
                });
            }
        }
        
        /// <summary>
        /// Oculta el icono inmediatamente sin animación (para cleanup)
        /// </summary>
        public void HideAlertIconImmediate()
        {
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
        }
        
        private IEnumerator ShowIconRoutine(GameObject iconPrefab, float duration)
        {
            // CRÍTICO: Instanciar en WORLD SPACE, NO como hijo del NPC
            _currentIconInstance = Instantiate(iconPrefab);
            
            // Calcular posición inicial en WORLD SPACE
            Vector3 worldStartPos = transform.position + iconOffset + new Vector3(0f, -0.5f, 0f);
            _currentIconInstance.transform.position = worldStartPos;
            _currentIconInstance.transform.rotation = Quaternion.identity;
            _currentIconInstance.transform.localScale = Vector3.zero;
            _currentIconInstance.SetActive(true);
            
            // IMPORTANTE: Aplicar rotación de billboard ANTES de animar para evitar distorsión
            var initialCam = GetCurrentCamera();
            if (initialCam != null)
            {
                Vector3 lookDir = initialCam.transform.position - _currentIconInstance.transform.position;
                lookDir.y = 0; // Ignorar diferencia vertical
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    _currentIconInstance.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
            
            // Animar aparición (en WORLD SPACE)
            var iconTransform = _currentIconInstance.transform;
            Vector3 targetWorldPos = transform.position + iconOffset;
            
            Sequence showSeq = DOTween.Sequence();
            showSeq.Append(iconTransform.DOScale(Vector3.one, showAnimDuration).SetEase(Ease.OutBack));
            showSeq.Join(iconTransform.DOMoveY(targetWorldPos.y, showAnimDuration).SetEase(Ease.OutBack));
            
            yield return new WaitForSeconds(showAnimDuration);
            
            // Animar durante la duración restante
            float elapsed = 0f;
            float remainingDuration = duration - showAnimDuration;
            
            while (elapsed < remainingDuration)
            {
                if (_currentIconInstance == null) yield break;
                
                // Actualizar posición siguiendo al NPC en WORLD SPACE
                Vector3 npcPosition = transform.position;
                Vector3 targetPos = npcPosition + iconOffset;
                
                // Aplicar bounce si está activado
                if (animateBounce)
                {
                    float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
                    targetPos.y += bounce;
                }
                
                _currentIconInstance.transform.position = targetPos;
                
                // Billboard hacia la cámara activa (solo rotación horizontal, sin copiar el pitch/roll de la cámara)
                var cam = GetCurrentCamera();
                if (cam != null)
                {
                    // Calcular dirección hacia la cámara pero solo en el plano horizontal
                    Vector3 lookDir = cam.transform.position - _currentIconInstance.transform.position;
                    lookDir.y = 0; // Ignorar diferencia vertical para evitar inclinación
                    
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        _currentIconInstance.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Limpiar al terminar
            HideAlertIcon();
        }
        
        /// <summary>
        /// Rutina para icono persistente (sin duración, se mantiene hasta HideAlertIcon)
        /// </summary>
        private IEnumerator ShowPersistentIconRoutine(GameObject iconPrefab)
        {
            // CRÍTICO: Instanciar en WORLD SPACE, NO como hijo del NPC
            _currentIconInstance = Instantiate(iconPrefab);
            _currentIconInstance.name = iconPrefab.name + "_Persistent";
            
            // Calcular posición inicial en WORLD SPACE
            Vector3 worldStartPos = transform.position + iconOffset + new Vector3(0f, -0.5f, 0f);
            _currentIconInstance.transform.position = worldStartPos;
            _currentIconInstance.transform.rotation = Quaternion.identity;
            _currentIconInstance.transform.localScale = Vector3.zero;
            _currentIconInstance.SetActive(true);
            
            // Aplicar rotación de billboard ANTES de animar
            UpdateCameraReference();
            var initialCam = GetCurrentCamera();
            if (initialCam != null)
            {
                Vector3 lookDir = initialCam.transform.position - _currentIconInstance.transform.position;
                lookDir.y = 0;
                if (lookDir.sqrMagnitude > 0.001f)
                {
                    _currentIconInstance.transform.rotation = Quaternion.LookRotation(lookDir);
                }
            }
            
            // Animar aparición (en WORLD SPACE)
            var iconTransform = _currentIconInstance.transform;
            Vector3 targetWorldPos = transform.position + iconOffset;
            
            Sequence showSeq = DOTween.Sequence();
            showSeq.Append(iconTransform.DOScale(Vector3.one, showAnimDuration).SetEase(Ease.OutBack));
            showSeq.Join(iconTransform.DOMoveY(targetWorldPos.y, showAnimDuration).SetEase(Ease.OutBack));
            _currentTween = showSeq.SetAutoKill(false);
            
            yield return new WaitForSeconds(showAnimDuration);
            
            // Loop de actualización
            while (_currentIconInstance != null && !_isHiding)
            {
                // Actualizar posición siguiendo al NPC en WORLD SPACE
                Vector3 targetPos = transform.position + iconOffset;
                
                if (animateBounce)
                {
                    float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmplitude;
                    targetPos.y += bounce;
                }
                
                _currentIconInstance.transform.position = targetPos;
                
                // Billboard hacia cámara
                var cam = GetCurrentCamera();
                if (cam != null && _currentIconInstance != null)
                {
                    Vector3 lookDir = cam.transform.position - _currentIconInstance.transform.position;
                    lookDir.y = 0;
                    
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        _currentIconInstance.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
                
                yield return null;
            }
        }
        
        private void OnDestroy()
        {
            _currentTween?.Kill();
            HideAlertIconImmediate();
        }
    }
}

