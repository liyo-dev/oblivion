using System.Collections.Generic;
using UnityEngine;
using Invector.vCharacterController;
using Sendero.Core.Feedback;

/// <summary>
/// Controlador de levitación del jugador.
/// Permite al jugador atraer NPCs mientras mantiene presionado el botón de magia,
/// y repelerlos al soltarlo.
/// 
/// Funciona solo con hechizos de tipo MagicKind.Levitation equipados en slots izquierdo o derecho.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLevitationController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private vThirdPersonController controller;
    [SerializeField] private MagicCaster magicCaster;
    [SerializeField] private ManaPool manaPool;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerTargeting targeting;
    
    [Header("Configuración de Animación")]
    [Tooltip("Frame normalizado (0-1) en el que pausar la animación durante hold.")]
    [SerializeField] private float holdPauseNormalizedTime = 0.3f;
    
    [Header("Configuración de Detección")]
    [Tooltip("Offset vertical desde el transform para el origen del cono de detección.")]
    [SerializeField] private float detectionHeightOffset = 1.2f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;
    [SerializeField] private bool showDebugGizmos = false;
    
    // Estado actual de levitación
    private bool _isLevitating;
    private MagicSlot _activeSlot;
    private MagicSpellSO _activeSpell;
    private List<LevitationTarget> _currentTargets = new List<LevitationTarget>();
    
    // Tiempo de inicio de levitación (para efectos y cálculos)
    private float _levitationStartTime;
    
    // Layer del Animator para animaciones de magia
    private int _upperBodyLayerIndex = 1;
    
    // Propiedades de reflexión cacheadas para GamepadInputReader
    private static System.Type _gamepadReaderType;
    private static System.Reflection.PropertyInfo _leftPressedProp;
    private static System.Reflection.PropertyInfo _leftHeldProp;
    private static System.Reflection.PropertyInfo _leftReleasedProp;
    private static System.Reflection.PropertyInfo _rightPressedProp;
    private static System.Reflection.PropertyInfo _rightHeldProp;
    private static System.Reflection.PropertyInfo _rightReleasedProp;
    private static bool _reflectionInitialized;
    
    // Flags para evitar re-iniciar mientras el botón está mantenido
    private bool _leftButtonWasDown;
    private bool _rightButtonWasDown;
    
    // VFX activos
    private GameObject _holdVFXInstance;
    private List<GameObject> _rangeIndicatorInstances = new List<GameObject>();
    
    public bool IsLevitating => _isLevitating;
    public MagicSlot ActiveSlot => _activeSlot;
    public IReadOnlyList<LevitationTarget> CurrentTargets => _currentTargets;
    
    void Awake()
    {
        // Auto-buscar componentes si no están asignados
        if (!controller) controller = GetComponentInParent<vThirdPersonController>();
        if (!magicCaster) magicCaster = GetComponentInParent<MagicCaster>();
        if (!manaPool) manaPool = GetComponentInParent<ManaPool>();
        if (!animator) animator = GetComponentInParent<Animator>();
        if (!targeting) targeting = GetComponentInParent<PlayerTargeting>();
        
        InitializeReflection();
    }
    
    void Start()
    {
        // Verificar configuración y mostrar advertencias
        if (!magicCaster)
        {
            Debug.LogError("[PlayerLevitationController] No se encontró MagicCaster! El sistema de levitación no funcionará.");
            return;
        }
        
        // Verificar si hay algún hechizo de levitación equipado
        var leftSpell = magicCaster.GetSpellForSlot(MagicSlot.Left);
        var rightSpell = magicCaster.GetSpellForSlot(MagicSlot.Right);
        
        bool hasLevitationLeft = leftSpell != null && leftSpell.kind == MagicKind.Levitation;
        bool hasLevitationRight = rightSpell != null && rightSpell.kind == MagicKind.Levitation;
        
        if (showDebugLogs)
        {
            //Debug.Log($"[PlayerLevitationController] Inicializado:");
            //Debug.Log($"  - MagicCaster: {(magicCaster ? "OK" : "MISSING")}");
            //Debug.Log($"  - ManaPool: {(manaPool ? "OK" : "MISSING")}");
            //Debug.Log($"  - Animator: {(animator ? "OK" : "MISSING")}");
            //Debug.Log($"  - Left Spell: {(leftSpell ? leftSpell.displayName : "None")} (Levitation: {hasLevitationLeft})");
            //Debug.Log($"  - Right Spell: {(rightSpell ? rightSpell.displayName : "None")} (Levitation: {hasLevitationRight})");
            //Debug.Log($"  - Reflexión InputReader: {(_gamepadReaderType != null ? "OK" : "FAILED")}");
            //Debug.Log($"  - LeftHeldProp: {(_leftHeldProp != null ? "OK" : "MISSING")}");
            //Debug.Log($"  - RightHeldProp: {(_rightHeldProp != null ? "OK" : "MISSING")}");
        }
        
        // Contar LevitationTargets en la escena
        var targets = FindObjectsByType<LevitationTarget>(FindObjectsSortMode.None);
        //if (showDebugLogs) Debug.Log($"[PlayerLevitationController] LevitationTargets en escena: {targets.Length}");
    }
    
    void InitializeReflection()
    {
        if (_reflectionInitialized) return;
        _reflectionInitialized = true;
        
        try
        {
            _gamepadReaderType = System.Type.GetType("Core.GamepadInputReader, Assembly-CSharp");
            if (_gamepadReaderType == null)
            {
                Debug.LogError("[PlayerLevitationController] No se pudo encontrar Core.GamepadInputReader.");
                return;
            }
            
            _leftPressedProp = _gamepadReaderType.GetProperty("AttackMagicLeftPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _leftHeldProp = _gamepadReaderType.GetProperty("AttackMagicLeftHeld", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _leftReleasedProp = _gamepadReaderType.GetProperty("AttackMagicLeftReleased", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _rightPressedProp = _gamepadReaderType.GetProperty("AttackMagicRightPressed", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _rightHeldProp = _gamepadReaderType.GetProperty("AttackMagicRightHeld", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            _rightReleasedProp = _gamepadReaderType.GetProperty("AttackMagicRightReleased", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[PlayerLevitationController] Error inicializando reflexión: {ex.Message}");
        }
    }
    
    void Update()
    {
        // Si estamos levitando, actualizar el estado
        if (_isLevitating)
        {
            UpdateLevitation();
            CheckForRelease();
        }
        else
        {
            // Verificar si hay que iniciar levitación (botón pressed + hechizo de levitación)
            CheckForLevitationStart();
        }
    }
    
    /// <summary>
    /// Verifica si el jugador está presionando un botón de magia con un hechizo de levitación equipado.
    /// </summary>
    void CheckForLevitationStart()
    {
        if (!magicCaster) return;
        
        bool leftHeld = GetLeftHeld();
        bool rightHeld = GetRightHeld();
        
        // Trackear estado de botones para evitar re-iniciar
        if (!leftHeld) _leftButtonWasDown = false;
        if (!rightHeld) _rightButtonWasDown = false;
        
        // Verificar slot izquierdo (solo si el botón acaba de presionarse)
        var leftSpell = magicCaster.GetSpellForSlot(MagicSlot.Left);
        if (leftSpell != null && leftSpell.kind == MagicKind.Levitation)
        {
            if (showDebugLogs && leftHeld) Debug.Log($"[Levitation] Botón izquierdo mantenido, leftButtonWasDown={_leftButtonWasDown}");
            
            if (leftHeld && !_leftButtonWasDown)
            {
                _leftButtonWasDown = true;
                if (showDebugLogs) Debug.Log($"[Levitation] Intentando iniciar levitación con slot LEFT");
                if (TryStartLevitation(MagicSlot.Left, leftSpell))
                    return;
            }
        }
        
        // Verificar slot derecho (solo si el botón acaba de presionarse)
        var rightSpell = magicCaster.GetSpellForSlot(MagicSlot.Right);
        if (rightSpell != null && rightSpell.kind == MagicKind.Levitation)
        {
            if (showDebugLogs && rightHeld) Debug.Log($"[Levitation] Botón derecho mantenido, rightButtonWasDown={_rightButtonWasDown}");
            
            if (rightHeld && !_rightButtonWasDown)
            {
                _rightButtonWasDown = true;
                if (showDebugLogs) Debug.Log($"[Levitation] Intentando iniciar levitación con slot RIGHT");
                TryStartLevitation(MagicSlot.Right, rightSpell);
            }
        }
    }
    
    /// <summary>
    /// Intenta iniciar la levitación con el slot y hechizo especificados.
    /// </summary>
    bool TryStartLevitation(MagicSlot slot, MagicSpellSO spell)
    {
        // Verificar si podemos lanzar el hechizo (maná, cooldown, etc.)
        if (!magicCaster.CanCastSpell(slot))
        {
            if (showDebugLogs) Debug.Log($"[Levitation] No se puede iniciar levitación: CanCastSpell false para slot {slot}");
            return false;
        }
        
        // Consumir maná
        if (manaPool != null && !manaPool.TrySpend(spell.manaCost))
        {
            if (showDebugLogs) Debug.Log($"[Levitation] Maná insuficiente para {spell.displayName}");
            return false;
        }
        
        // Encontrar targets válidos en el cono de detección
        var targets = FindTargetsInCone(spell);
        if (targets.Count == 0)
        {
            if (showDebugLogs) Debug.Log("[Levitation] No hay targets válidos en el cono de detección");
            // Aún así iniciamos la levitación para mostrar la animación
        }
        
        // Iniciar levitación
        _isLevitating = true;
        _activeSlot = slot;
        _activeSpell = spell;
        _currentTargets = targets;
        _levitationStartTime = Time.time;
        
        if (showDebugLogs) Debug.Log($"[Levitation] Iniciando levitación con {targets.Count} targets");
        
        // Reproducir animación de magia y pausarla
        PlayHoldAnimation(slot);
        
        // Notificar a los targets que están siendo levitados
        foreach (var target in _currentTargets)
        {
            target.BeginLevitation(this, spell);
        }
        
        // Instanciar VFX de hold en el jugador
        SpawnHoldVFX(spell);
        
        // Instanciar indicadores de rango
        SpawnRangeIndicators(spell);
        
        // Camera shake al capturar (solo si capturamos al menos un NPC)
        if (_currentTargets.Count > 0)
        {
            FeedbackService.CameraShake(spell.levitationCaptureShakeIntensity, spell.levitationCaptureShakeDuration);
        }
        
        // Reproducir SFX de cast si está configurado
        if (!string.IsNullOrEmpty(spell.castSFXKey) && AudioService.Instance != null)
        {
            AudioService.Instance.PlaySFX(spell.castSFXKey);
        }
        
        return true;
    }
    
    /// <summary>
    /// Actualiza la lógica de levitación mientras el botón está presionado.
    /// </summary>
    void UpdateLevitation()
    {
        if (_activeSpell == null) return;
        
        float elapsed = Time.time - _levitationStartTime;
        Vector3 playerPos = transform.position;
        Vector3 playerForward = transform.forward;
        
        // Drenar maná después del delay inicial
        if (elapsed > _activeSpell.levitationDrainDelay && manaPool != null)
        {
            float manaToDrain = _activeSpell.levitationManaDrainPerSecond * Time.deltaTime;
            
            // Verificar si hay suficiente maná
            if (manaPool.Current < manaToDrain)
            {
                // Sin maná - cancelar levitación (el NPC cae sin repulsión)
                if (showDebugLogs) Debug.Log("[Levitation] Sin maná - cancelando levitación");
                CancelLevitationNoMana();
                return;
            }
            
            // Drenar maná
            manaPool.TrySpend(manaToDrain);
        }
        
        // Posición objetivo: delante del jugador a cierta distancia
        float holdDistance = _activeSpell.levitationHoldDistance > 0 ? _activeSpell.levitationHoldDistance : 3f;
        Vector3 targetHoldPosition = playerPos + playerForward * holdDistance;
        
        // Actualizar cada target levitado
        for (int i = _currentTargets.Count - 1; i >= 0; i--)
        {
            var target = _currentTargets[i];
            if (target == null || !target.IsBeingLevitated)
            {
                _currentTargets.RemoveAt(i);
                continue;
            }
            
            // Pasar la posición objetivo directamente para que el NPC siga al jugador como un globo
            target.UpdateLevitation(
                _activeSpell,
                targetHoldPosition,
                _activeSpell.levitationPullForce
            );
        }
    }
    
    /// <summary>
    /// Cancela la levitación por falta de maná (el NPC cae sin fuerza de repulsión).
    /// </summary>
    void CancelLevitationNoMana()
    {
        if (!_isLevitating) return;
        
        if (showDebugLogs) Debug.Log($"[Levitation] Cancelando por falta de maná, {_currentTargets.Count} targets");
        
        // Los targets caen sin repulsión
        foreach (var target in _currentTargets)
        {
            if (target != null)
                target.CancelLevitation();
        }
        
        // Destruir VFX
        DestroyHoldVFX();
        DestroyRangeIndicators();
        
        // Detener la animación del jugador
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        
        // Bajar el peso del layer de animación
        if (animator != null)
        {
            StartCoroutine(Co_LowerLayerWeight());
        }
        
        // Resetear estado
        _isLevitating = false;
        _activeSpell = null;
        _currentTargets.Clear();
    }
    
    System.Collections.IEnumerator Co_LowerLayerWeight()
    {
        float t = 0f;
        float duration = 0.22f;
        float startWeight = animator != null ? animator.GetLayerWeight(_upperBodyLayerIndex) : 0f;
        
        while (t < duration && animator != null)
        {
            t += Time.deltaTime;
            animator.SetLayerWeight(_upperBodyLayerIndex, Mathf.Lerp(startWeight, 0f, t / duration));
            yield return null;
        }
        
        if (animator != null)
            animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }
    
    /// <summary>
    /// Verifica si el jugador ha soltado el botón para finalizar la levitación.
    /// </summary>
    void CheckForRelease()
    {
        bool released = (_activeSlot == MagicSlot.Left && GetLeftReleased()) ||
                       (_activeSlot == MagicSlot.Right && GetRightReleased());
        
        // También verificar si ya no está presionado (por si se pierde el release)
        bool stillHeld = (_activeSlot == MagicSlot.Left && GetLeftHeld()) ||
                        (_activeSlot == MagicSlot.Right && GetRightHeld());
        
        if (released || !stillHeld)
        {
            EndLevitation();
        }
    }
    
    /// <summary>
    /// Finaliza la levitación y aplica la repulsión.
    /// </summary>
    void EndLevitation()
    {
        if (!_isLevitating) return;
        
        if (showDebugLogs) Debug.Log($"[Levitation] Finalizando levitación, repeliendo {_currentTargets.Count} targets");
        
        // Calcular dirección de repulsión (desde el jugador hacia afuera)
        Vector3 playerPos = transform.position;
        Vector3 playerForward = transform.forward;
        
        // Camera shake al soltar (más intenso que al capturar)
        if (_currentTargets.Count > 0 && _activeSpell != null)
        {
            FeedbackService.CameraShake(_activeSpell.levitationReleaseShakeIntensity, _activeSpell.levitationReleaseShakeDuration);
        }
        
        // Notificar a los targets que la levitación terminó y aplicar repulsión
        foreach (var target in _currentTargets)
        {
            if (target == null) continue;
            
            // Dirección desde el jugador hacia el target (alejándose del jugador)
            Vector3 pushDir = (target.transform.position - playerPos);
            pushDir.y = 0;
            if (pushDir.sqrMagnitude < 0.01f)
                pushDir = playerForward;
            pushDir = pushDir.normalized;
            
            // Instanciar VFX de release en la posición del NPC
            SpawnReleaseVFX(target.transform.position);
            
            target.EndLevitation(_activeSpell, pushDir, _activeSpell.levitationPushForce);
        }
        
        // Destruir VFX del jugador
        DestroyHoldVFX();
        DestroyRangeIndicators();
        
        // Reproducir la parte final de la animación
        PlayReleaseAnimation();
        
        // Resetear estado
        _isLevitating = false;
        _activeSpell = null;
        _currentTargets.Clear();
    }
    
    /// <summary>
    /// Busca targets válidos dentro del cono de detección.
    /// </summary>
    List<LevitationTarget> FindTargetsInCone(MagicSpellSO spell)
    {
        var results = new List<LevitationTarget>();
        
        Vector3 origin = transform.position + Vector3.up * detectionHeightOffset;
        Vector3 forward = transform.forward;
        float range = spell.levitationRange;
        float halfAngle = spell.levitationAngle * 0.5f;
        
        if (showDebugLogs) Debug.Log($"[Levitation] Buscando targets - Rango: {range}, Ángulo: {spell.levitationAngle}°, Layers: {spell.levitationTargetLayers.value}");
        
        // Buscar todos los colliders en el rango
        var colliders = Physics.OverlapSphere(origin, range, spell.levitationTargetLayers);
        
        if (showDebugLogs) Debug.Log($"[Levitation] Encontrados {colliders.Length} colliders en el rango");
        
        foreach (var col in colliders)
        {
            // Verificar si tiene componente LevitationTarget
            var target = col.GetComponentInParent<LevitationTarget>();
            if (target == null)
            {
                if (showDebugLogs) Debug.Log($"[Levitation] Collider {col.name} no tiene LevitationTarget");
                continue;
            }
            
            if (!target.CanBeLevitated)
            {
                if (showDebugLogs) Debug.Log($"[Levitation] {target.name} no puede ser levitado (CanBeLevitated=false)");
                continue;
            }
            
            // Verificar ángulo
            Vector3 toTarget = (target.transform.position - origin);
            toTarget.y = 0;
            
            float angle = Vector3.Angle(forward, toTarget);
            if (angle > halfAngle)
            {
                if (showDebugLogs) Debug.Log($"[Levitation] {target.name} fuera del cono (ángulo: {angle}° > {halfAngle}°)");
                continue;
            }
            
            // Target válido
            if (!results.Contains(target))
            {
                if (showDebugLogs) Debug.Log($"[Levitation] ✓ Target válido encontrado: {target.name}");
                results.Add(target);
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Reproduce la animación de magia y la pausa en el frame de "preparación".
    /// Usa playback manual para no afectar la locomoción.
    /// </summary>
    void PlayHoldAnimation(MagicSlot slot)
    {
        if (animator == null) return;
        
        // Determinar el estado de animación según el slot
        _currentMagicStatePath = slot == MagicSlot.Left ? "UpperBody.Magic.MagicLeft" : "UpperBody.Magic.MagicRight";
        _currentMagicStateHash = Animator.StringToHash(_currentMagicStatePath);
        
        // Subir el peso del layer superior
        animator.SetLayerWeight(_upperBodyLayerIndex, 1f);
        
        // Reproducir la animación empezando desde 0
        animator.Play(_currentMagicStatePath, _upperBodyLayerIndex, 0f);
        
        // Iniciar corrutina para controlar el playback manualmente
        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(Co_HoldAnimationPlayback());
    }
    
    // Variables para control de animación manual
    private string _currentMagicStatePath;
    private int _currentMagicStateHash;
    private Coroutine _animationCoroutine;
    private bool _animationPausedAtHold;
    
    System.Collections.IEnumerator Co_HoldAnimationPlayback()
    {
        if (animator == null) yield break;
        
        // Esperar a que entre en el estado
        int maxWaitFrames = 10;
        int waited = 0;
        while (animator != null && waited < maxWaitFrames)
        {
            var info = animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
            if (info.fullPathHash == _currentMagicStateHash || info.shortNameHash == _currentMagicStateHash)
                break;
            waited++;
            yield return null;
        }
        
        if (animator == null) yield break;
        
        // Reproducir hasta el punto de pausa
        while (animator != null && _isLevitating)
        {
            var stateInfo = animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
            
            // Si ya pasamos el punto de pausa, mantener en ese punto
            if (stateInfo.normalizedTime >= holdPauseNormalizedTime)
            {
                // Mantener la animación en el punto de pausa reescribiendo el tiempo
                animator.Play(_currentMagicStatePath, _upperBodyLayerIndex, holdPauseNormalizedTime);
                _animationPausedAtHold = true;
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Continúa la animación para la fase de release.
    /// </summary>
    void PlayReleaseAnimation()
    {
        if (animator == null) return;
        
        // Detener la corrutina de hold si está corriendo
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }
        
        // Continuar la animación desde el punto de pausa hasta el final
        if (!string.IsNullOrEmpty(_currentMagicStatePath))
        {
            // Reproducir desde el punto de pausa - la animación continuará naturalmente
            animator.Play(_currentMagicStatePath, _upperBodyLayerIndex, holdPauseNormalizedTime);
        }
        
        _animationPausedAtHold = false;
        
        // Iniciar corrutina para bajar el peso del layer cuando termine
        StartCoroutine(Co_WaitAnimationEndAndLowerLayer());
    }
    
    System.Collections.IEnumerator Co_WaitAnimationEndAndLowerLayer()
    {
        if (animator == null) yield break;
        
        // Esperar a que la animación termine (desde holdPauseNormalizedTime hasta 1.0)
        float remainingNormalized = 1f - holdPauseNormalizedTime;
        
        // Obtener la duración del clip de animación
        float clipDuration = 0.5f; // Duración estimada por defecto
        var stateInfo = animator.GetCurrentAnimatorStateInfo(_upperBodyLayerIndex);
        if (stateInfo.length > 0)
            clipDuration = stateInfo.length;
        
        float waitTime = clipDuration * remainingNormalized;
        yield return new WaitForSeconds(waitTime + 0.1f); // +0.1s de margen
        
        // Bajar suavemente el peso del layer
        float t = 0f;
        float duration = 0.22f;
        float startWeight = animator != null ? animator.GetLayerWeight(_upperBodyLayerIndex) : 0f;
        
        while (t < duration && animator != null)
        {
            t += Time.deltaTime;
            animator.SetLayerWeight(_upperBodyLayerIndex, Mathf.Lerp(startWeight, 0f, t / duration));
            yield return null;
        }
        
        if (animator != null)
            animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
    }
    
    // Helpers para leer inputs via reflexión
    bool GetLeftHeld()
    {
        if (_leftHeldProp == null) return false;
        return (bool)_leftHeldProp.GetValue(null);
    }
    
    bool GetLeftReleased()
    {
        if (_leftReleasedProp == null) return false;
        return (bool)_leftReleasedProp.GetValue(null);
    }
    
    bool GetRightHeld()
    {
        if (_rightHeldProp == null) return false;
        return (bool)_rightHeldProp.GetValue(null);
    }
    
    bool GetRightReleased()
    {
        if (_rightReleasedProp == null) return false;
        return (bool)_rightReleasedProp.GetValue(null);
    }
    
    void OnDisable()
    {
        // Asegurar que la levitación se cancele si el componente se desactiva
        if (_isLevitating)
        {
            foreach (var target in _currentTargets)
            {
                if (target != null)
                    target.CancelLevitation();
            }
            
            _isLevitating = false;
            _currentTargets.Clear();
            
            // Limpiar VFX
            DestroyHoldVFX();
            DestroyRangeIndicators();
            
            if (animator != null)
            {
                animator.speed = 1f;
                animator.SetLayerWeight(_upperBodyLayerIndex, 0f);
            }
        }
    }
    
    #region VFX Management
    
    /// <summary>
    /// Instancia el VFX de hold en el jugador (manos/cuerpo).
    /// </summary>
    void SpawnHoldVFX(MagicSpellSO spell)
    {
        if (spell.levitationHoldVFX == null) return;
        
        _holdVFXInstance = Instantiate(spell.levitationHoldVFX, transform.position, transform.rotation);
        _holdVFXInstance.transform.SetParent(transform);
        _holdVFXInstance.transform.localPosition = Vector3.up * 1.2f; // A la altura del pecho
        
        if (showDebugLogs) Debug.Log("[Levitation] VFX de hold instanciado");
    }
    
    /// <summary>
    /// Destruye el VFX de hold del jugador.
    /// </summary>
    void DestroyHoldVFX()
    {
        if (_holdVFXInstance != null)
        {
            Destroy(_holdVFXInstance);
            _holdVFXInstance = null;
        }
    }
    
    /// <summary>
    /// Instancia los indicadores de rango (círculos de purpurina) en el cono de detección.
    /// </summary>
    void SpawnRangeIndicators(MagicSpellSO spell)
    {
        if (spell.levitationRangeIndicatorVFX == null) return;
        
        DestroyRangeIndicators(); // Limpiar anteriores si los hay
        
        float range = spell.levitationRange;
        int count = Mathf.Max(1, spell.rangeIndicatorCount);
        float spacing = range / count;
        
        for (int i = 0; i < count; i++)
        {
            float distance = spacing * (i + 1);
            Vector3 spawnPos = transform.position + transform.forward * distance;
            spawnPos.y = transform.position.y + 0.1f; // Ligeramente elevado del suelo
            
            var indicator = Instantiate(spell.levitationRangeIndicatorVFX, spawnPos, Quaternion.identity);
            indicator.transform.SetParent(transform); // Seguir al jugador
            _rangeIndicatorInstances.Add(indicator);
        }
        
        if (showDebugLogs) Debug.Log($"[Levitation] {count} indicadores de rango instanciados");
    }
    
    /// <summary>
    /// Destruye todos los indicadores de rango.
    /// </summary>
    void DestroyRangeIndicators()
    {
        foreach (var indicator in _rangeIndicatorInstances)
        {
            if (indicator != null)
                Destroy(indicator);
        }
        _rangeIndicatorInstances.Clear();
    }
    
    /// <summary>
    /// Instancia el VFX de release en la posición especificada (donde está el NPC).
    /// </summary>
    void SpawnReleaseVFX(Vector3 position)
    {
        if (_activeSpell == null || _activeSpell.levitationReleaseVFX == null) return;
        
        var vfx = Instantiate(_activeSpell.levitationReleaseVFX, position, Quaternion.identity);
        
        // Auto-destruir después de un tiempo
        if (_activeSpell.vfxLifetime > 0)
        {
            Destroy(vfx, _activeSpell.vfxLifetime);
        }
    }
    
    #endregion
    
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        // Dibujar el cono de detección si hay un hechizo de levitación equipado
        MagicSpellSO spell = null;
        if (Application.isPlaying && magicCaster != null)
        {
            spell = magicCaster.GetSpellForSlot(MagicSlot.Left);
            if (spell == null || spell.kind != MagicKind.Levitation)
                spell = magicCaster.GetSpellForSlot(MagicSlot.Right);
        }
        
        if (spell == null || spell.kind != MagicKind.Levitation) return;
        
        Vector3 origin = transform.position + Vector3.up * detectionHeightOffset;
        float range = spell.levitationRange;
        float halfAngle = spell.levitationAngle * 0.5f;
        
        Gizmos.color = _isLevitating ? Color.magenta : Color.cyan;
        
        // Dibujar líneas del cono
        Vector3 forward = transform.forward * range;
        Vector3 left = Quaternion.Euler(0, -halfAngle, 0) * forward;
        Vector3 right = Quaternion.Euler(0, halfAngle, 0) * forward;
        
        Gizmos.DrawLine(origin, origin + forward);
        Gizmos.DrawLine(origin, origin + left);
        Gizmos.DrawLine(origin, origin + right);
        Gizmos.DrawWireSphere(origin, 0.2f);
    }
#endif
}

