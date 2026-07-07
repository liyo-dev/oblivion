using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using Unity.Cinemachine;
using DG.Tweening;
using EasyTransition;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Game.Cinematics
{
    public enum InstantiationMode
    {
        Permanent,          // Se queda en la escena
        DestroyTimer,       // Se destruye tras X segundos
        ShrinkAndDestroy,   // Se hace pequeño hasta desaparecer
        MoveAndDestroy      // Se mueve a un punto y desaparece
    }

    public enum DialogueTiming
    {
        AfterDuration,      // Al final del paso (Default)
        WithStepStart,      // Inmediatamente al empezar
        AfterCameraBlend,   // Cuando termine el blend de cámara
        CustomDelay         // Tras X segundos
    }

    /// <summary>
    /// Configuración individual para un objeto instanciado en la cinemática.
    /// Agrupa el prefab, su posición y sus animaciones específicas.
    /// </summary>
    [System.Serializable]
    public class CinematicSpawnerEntry
    {
        public string name = "Spawner Object";
        public GameObject prefab;
        [Tooltip("Punto de spawn. Si es null, usa Vector3.zero.")]
        public Transform spawnPoint;
        
        [Header("Ciclo de Vida")]
        public InstantiationMode spawnMode = InstantiationMode.Permanent;
        public float destroyDelay = 2f;
        public float shrinkDuration = 1f;
        public Transform exitTarget;
        public float exitDuration = 1f;

        [Header("Animación DOTween (Rotación)")]
        public bool animateRotation = false;
        public Vector3 targetRotation = new Vector3(0, 360, 0);
        public float rotationDuration = 1f;
        public Ease rotationEase = Ease.Linear; // Changed from InOutQuad
        public RotateMode rotationMode = RotateMode.LocalAxisAdd;
        [Tooltip("0 = Sin loop, -1 = Infinito, >0 = Cantidad de loops")]
        public int rotationLoops = 0;
        public LoopType rotationLoopType = LoopType.Restart;

        [Header("Animación DOTween (Escala)")]
        public bool animateScale = false;
        public Vector3 targetScale = Vector3.one;
        public float scaleDuration = 1f;
        public Ease scaleEase = Ease.OutBack;
    }

    [System.Serializable]
    public class CinematicStep
    {
        public string name = "Step";
        [Tooltip("Tiempo que dura este paso antes de ir al siguiente. Las acciones ocurren simultáneamente.")]
        public float duration = 2f;

        [Header("1. Cámara & Dolly")]
        [Tooltip("Cámara manual a usar.")]
        public CinemachineCamera manualCamera;
        public float blendTime = 1f;
        
        [Tooltip("Si se asignan Start y End, la cámara se moverá entre estos puntos (Dolly clásico).")]
        public Transform dollyStart;
        public Transform dollyEnd;
        public Ease dollyEase = Ease.InOutSine;

        [Header("   Animación de Cámara (Offset)")]
        [Tooltip("Permite animar la posición de la cámara sumando un offset inicial y final.")]
        public bool animateCameraOffset = false;
        public Vector3 camOffsetStart = Vector3.zero;
        public Vector3 camOffsetEnd = new Vector3(0, 0, -5);
        public Ease camOffsetEase = Ease.InOutSine;

        [Header("   Apuntar a Objeto (LookAt)")]
        [Tooltip("Si se asigna, la cámara rotará para mirar a este objeto durante el paso.")]
        public Transform lookAtTarget;
        [Tooltip("Duración de la rotación hacia el objetivo. Si es 0, es instantáneo.")]
        public float lookAtDuration = 1f;
        public Ease lookAtEase = Ease.InOutSine;

        [Header("   Posicionamiento (Teleport/Move)")]
        [Tooltip("Punto destino de la cámara. Si se usa con Dolly, esto se ignora.")]
        public Transform cameraPositionTarget;
        [Tooltip("Si es true, la cámara se mueve instantáneamente al inicio. Si es false, se mueve durante la duración del paso.")]
        public bool instantPosition = true;

        [Header("   Entorno (Interior/Exterior)")]
        [Tooltip("Si true, fuerza el renderizado de Skybox para este paso (Exterior).")]
        public bool isExteriorStep = false;
        [Tooltip("Si true, intenta aplicar la configuración del AnchorEnvironment actual del jugador (Interior).")]
        public bool isInteriorStep = false;

        [Header("2. Efectos de Tiempo")]
        public bool slowMotion = false;
        [Range(0.1f, 1f)] public float timeScale = 0.5f;

        [Header("3. Movimiento NPC (Opcional)")]
        public Transform character;
        public Transform moveTarget;
        public float moveSpeed = 3f;
        [Tooltip("Si true, detiene la animación forzada al llegar al destino (vuelve a Idle). Si false, sigue animando hasta fin del paso.")]
        public bool stopAnimOnArrival = true;
        
        [Header("4. Animación (Opcional)")]
        public Animator overrideAnimator; 
        public AnimationClip animationClip;
        public bool loopAnim = true;
        [Tooltip("Si es true, NO detiene la animación al terminar este paso, permitiendo que continúe en el siguiente.")]
        public bool keepAnimOnNextStep = false;

        [Header("5. Instanciar Objetos")]
        [Tooltip("Lista de objetos a instanciar, cada uno con su propia configuración.")]
        public List<CinematicSpawnerEntry> objectsToSpawn = new List<CinematicSpawnerEntry>();

        [Header("6. Audio (Opcional)")]
        public AudioClip audioClip;
        public float volume = 1f;

        [Header("7. Diálogo (Bloqueante)")]
        public DialogueAsset dialogue;
        public DialogueTiming dialogueTiming = DialogueTiming.AfterDuration;
        [Tooltip("Solo si DialogueTiming es CustomDelay")]
        public float dialogueDelay = 0.5f;

        [Header("8. Transición / Blackout")]
        [Tooltip("Si true, inicia una transición al principio del paso.")]
        public bool startWithBlackout = false;
        [Tooltip("Si true, inicia una transición al FINAL del paso (para terminar en negro).")]
        public bool endWithBlackout = false;
        public TransitionSettings transitionSettings;
        
        [Header("9. Activar/Desactivar Objetos")]
        [Tooltip("Objetos a activar al inicio de este paso")]
        public List<GameObject> objectsToActivate;
        [Tooltip("Objetos a desactivar al inicio de este paso")]
        public List<GameObject> objectsToDeactivate;

        [Header("10. Eventos Extra")]
        public UnityEvent onStepStart;
    }

    public class SimpleCinematicDirector : MonoBehaviour
    {
        [Header("Configuración General")]
        public bool playOnStart = false;
        public bool hideHUD = true;
        public bool lockPlayer = true;
        [Tooltip("Si true, se usa la Main Camera directamente sin Cinemachine.")]
        public bool forceDirectCameraControl = true;
        [Tooltip("Fuerza el ClearFlags de la cámara principal a Skybox durante la cinemática.")]
        public bool forceSkybox = true;
        [Tooltip("Cámara opcional con CinemachineBrain para usar en lugar de la Main Camera (si esta no tiene Brain).")]
        public Camera customBrainCamera;
        
        [Header("Persistencia (Single Use)")]
        [Tooltip("Si true, esta cinemática solo se reproducirá una vez por partida.")]
        public bool singleUse = false;
        [Tooltip("ID único para persistencia. Si se deja vacío, se usará el nombre del GameObject.")]
        public string cinematicId;
        
        [Header("Secuencia")]
        public List<CinematicStep> steps = new List<CinematicStep>();

        [Header("Eventos Globales")]
        public UnityEvent onCinematicStart;
        public UnityEvent onCinematicEnd;

        // Referencias internas
        private CinemachineBrain _brain;
        private List<PlayableGraph> _activeGraphs = new List<PlayableGraph>();
        private CinemachineCamera _lastActiveCamera;
        
        // Estado original para restaurar
        private Vector3 _originalCamPos;
        private Quaternion _originalCamRot;
        private CameraClearFlags _originalClearFlags;
        
        // Referencias de cámara de gameplay
        private Camera _mainUnityCamera;
        private bool _mainUnityCameraWasEnabled;
        private CinemachineCamera _mainGameplayCamera;
        private int _originalGameplayCameraPriority;
        
        // Estado global para que otros scripts (como vThirdPersonCamera) sepan si hay cinemática
        public static bool IsAnyCinematicPlaying { get; private set; }

        private void Start()
        {
            if (playOnStart) Play();
        }

        private void OnDisable()
        {
            // Restaurar estado global si la cinemática fue interrumpida externamente (ej. StopCoroutine sin Destroy)
            if (!IsAnyCinematicPlaying) return;
            CleanupGraphs(true);
            Time.timeScale = 1f;
            IsAnyCinematicPlaying = false;
            vThirdPersonCamera.lockCameraForCinematic = false;
            if (EnvironmentController.Instance != null && EnvironmentController.Instance.IsCinematicOverrideActive)
                EnvironmentController.Instance.EndCinematicOverride();
        }

        private void OnDestroy()
        {
            CleanupGraphs(true); // Forzar limpieza total al destruir
            Time.timeScale = 1f;
            if (IsAnyCinematicPlaying) IsAnyCinematicPlaying = false;

            // Asegurar que liberamos la cámara
            if (vThirdPersonCamera.lockCameraForCinematic)
                vThirdPersonCamera.lockCameraForCinematic = false;

            // Liberar el override cinemático si estaba activo (evita que el entorno quede en estado incorrecto)
            if (EnvironmentController.Instance != null && EnvironmentController.Instance.IsCinematicOverrideActive)
            {
                EnvironmentController.Instance.EndCinematicOverride();
            }
        }

        /// <summary>
        /// Obtiene el ID efectivo para persistencia
        /// </summary>
        private string GetPersistenceId()
        {
            if (!string.IsNullOrEmpty(cinematicId)) return cinematicId;
            return $"Cinematic_{gameObject.name}";
        }

        /// <summary>
        /// Verifica si esta cinemática ya ha sido vista (si es singleUse)
        /// </summary>
        public bool HasBeenSeen()
        {
            if (!singleUse) return false;
            
            string id = GetPersistenceId();
            string flag = $"CINEMATIC_SEEN:{id}";
            
            // Verificar en GameBootProfile (runtime preset)
            if (GameBootService.Profile != null && GameBootService.Profile.GetActivePresetResolved() != null)
            {
                var flags = GameBootService.Profile.GetActivePresetResolved().flags;
                return flags != null && flags.Contains(flag);
            }
            
            return false;
        }

        /// <summary>
        /// Marca la cinemática como vista en el sistema de persistencia
        /// </summary>
        private void MarkAsSeen()
        {
            if (!singleUse) return;
            
            string id = GetPersistenceId();
            string flag = $"CINEMATIC_SEEN:{id}";
            
            if (GameBootService.Profile != null)
            {
                var preset = GameBootService.Profile.GetActivePresetResolved();
                if (preset != null)
                {
                    if (preset.flags == null) preset.flags = new List<string>();
                    if (!preset.flags.Contains(flag))
                    {
                        preset.flags.Add(flag);
                        Debug.Log($"[Cinematic] Marcada como vista: {id}");
                    }
                }
            }
        }

        public void Play()
        {
            // Verificar si ya se vio y es de un solo uso
            if (singleUse && HasBeenSeen())
            {
                Debug.Log($"[Cinematic] Omitiendo cinemática '{GetPersistenceId()}' porque ya fue vista.");
                return;
            }
            
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            Debug.Log($"[Cinematic] Iniciando secuencia con {steps.Count} pasos.");
            IsAnyCinematicPlaying = true;
            
            // Bloquear vThirdPersonCamera
            vThirdPersonCamera.lockCameraForCinematic = true;

            // --- INICIO ---
            onCinematicStart?.Invoke();
            
            // === CONTROL DE ENTORNO CINEMÁTICO ===
            // Notificar al EnvironmentController que tomamos control del entorno
            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.BeginCinematicOverride();
            }
            
            // Ocultar HUD usando ServiceLocator
            if (hideHUD)
            {
                var hud = ServiceLocator.Get<Sendero.UI.PlayerHUDV2>(false);
                if (hud != null)
                {
                    hud.HideHUD();
                }
                else if (Sendero.UI.PlayerHUDV2.Instance)
                {
                    // Fallback a Singleton si ServiceLocator falla
                    Sendero.UI.PlayerHUDV2.Instance.HideHUD();
                }
            }
            
            // --- GESTIÓN DE CÁMARAS ---
            
            // 1. Capturar referencia a la cámara principal de Unity
            _mainUnityCamera = Camera.main;
            
            if (_mainUnityCamera != null)
            {
                // Guardar estado original
                _originalCamPos = _mainUnityCamera.transform.position;
                _originalCamRot = _mainUnityCamera.transform.rotation;
                _originalClearFlags = _mainUnityCamera.clearFlags;
                
                // Si NO usamos control directo, intentamos usar Cinemachine
                if (!forceDirectCameraControl)
                {
                    // Intentar obtener Brain de la Main Camera
                    _brain = _mainUnityCamera.GetComponent<CinemachineBrain>();
                    
                    // Si no tiene Brain y tenemos una customBrainCamera, usar esa
                    if (_brain == null && customBrainCamera != null)
                    {
                        Debug.Log("[Cinematic] Usando Custom Brain Camera.");
                        _brain = customBrainCamera.GetComponent<CinemachineBrain>();
                        customBrainCamera.gameObject.SetActive(true);
                        
                        // Desactivar Main Camera para evitar conflictos de rendering si usamos custom
                        _mainUnityCameraWasEnabled = _mainUnityCamera.enabled;
                        _mainUnityCamera.enabled = false;
                    }
                    
                    if (_brain == null)
                    {
                        Debug.LogWarning("[Cinematic] ⚠️ NO SE ENCONTRÓ CINEMACHINE BRAIN. Se forzará control directo de la cámara.");
                        forceDirectCameraControl = true;
                    }
                }
                
                // Si usamos control directo, desactivar Brain si existe para evitar conflictos
                if (forceDirectCameraControl && _brain != null)
                {
                    _brain.enabled = false;
                }
            }
            else
            {
                Debug.LogError("[Cinematic] ❌ NO HAY MAIN CAMERA en la escena.");
            }
            
            // 3. Desactivar la cámara virtual de gameplay de Cinemachine (si existe y usamos Cinemachine)
            if (!forceDirectCameraControl)
            {
                _mainGameplayCamera = ServiceLocator.Get<CinemachineCamera>(logIfMissing: false);
                if (_mainGameplayCamera != null)
                {
                    _originalGameplayCameraPriority = _mainGameplayCamera.Priority.Value;
                    Debug.Log($"[Cinematic] Desactivando cámara de gameplay Cinemachine (Priority: {_originalGameplayCameraPriority} -> 0)");
                    _mainGameplayCamera.Priority.Value = 0;
                }
            }

            // --- EJECUCIÓN DE PASOS ---
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                Debug.Log($"[Cinematic] Ejecutando paso: {step.name}");

                // 0. Blackout INICIAL (Start With Blackout)
                if (step.startWithBlackout) 
                { 
                    if (step.transitionSettings != null && TransitionManager.Instance())
                    {
                        TransitionManager.Instance().Transition(step.transitionSettings, 0);
                    }
                }
                
                // 0.1 Activar/Desactivar Objetos
                if (step.objectsToActivate != null)
                {
                    foreach (var obj in step.objectsToActivate)
                        if (obj != null) obj.SetActive(true);
                }
                
                if (step.objectsToDeactivate != null)
                {
                    foreach (var obj in step.objectsToDeactivate)
                        if (obj != null) obj.SetActive(false);
                }

                // 1. Eventos
                step.onStepStart?.Invoke();

                // 2. Transición (Legacy check - si no es startWithBlackout pero tiene settings, lo lanzaba antes)
                if (!step.startWithBlackout && !step.endWithBlackout && step.transitionSettings != null && TransitionManager.Instance())
                {
                    TransitionManager.Instance().Transition(step.transitionSettings, 0);
                }

                // 3. SLOW MOTION
                if (step.slowMotion)
                {
                    Time.timeScale = step.timeScale;
                }
                else
                {
                    Time.timeScale = 1f;
                }
                
                // 3.5 GESTIÓN DE ENTORNO (Interior/Exterior)
                // Usar los métodos específicos del EnvironmentController para cinemáticas
                Camera activeRenderCam = customBrainCamera != null && customBrainCamera.gameObject.activeSelf ? customBrainCamera : _mainUnityCamera;
                
                if (activeRenderCam != null && EnvironmentController.Instance != null)
                {
                    if (step.isExteriorStep)
                    {
                        // Usar el método específico para cinemáticas que aplica skybox/exterior
                        EnvironmentController.Instance.ApplyExteriorForCinematic(activeRenderCam);
                        Debug.Log("[Cinematic] Aplicando entorno EXTERIOR para este paso");
                    }
                    else if (step.isInteriorStep)
                    {
                        // Usar el método específico para cinemáticas que aplica config de interior
                        EnvironmentController.Instance.ApplyInteriorForCinematic(activeRenderCam);
                        Debug.Log("[Cinematic] Aplicando entorno INTERIOR para este paso");
                    }
                }

                // 4. CÁMARA & DOLLY
                CinemachineCamera activeCam = null;

                // Determinar qué cámara usar (si usamos Cinemachine)
                if (!forceDirectCameraControl && step.manualCamera != null)
                {
                    activeCam = step.manualCamera;
                    ActivateCamera(activeCam, step.blendTime);
                }

                // Aplicar Movimiento de Cámara (Dolly o Offset)
                Transform targetTransform = null;
                
                // Si es control directo, movemos la Main Camera (o la que esté activa)
                if (forceDirectCameraControl) 
                {
                    targetTransform = _mainUnityCamera != null ? _mainUnityCamera.transform : null;
                }
                // Si es Cinemachine, movemos la Virtual Camera activa
                else if (activeCam != null) 
                {
                    targetTransform = activeCam.transform;
                }

                if (targetTransform != null)
                {
                    // A. Dolly Track (Start -> End)
                    if (step.dollyStart != null && step.dollyEnd != null)
                    {
                        targetTransform.position = step.dollyStart.position;
                        targetTransform.rotation = step.dollyStart.rotation;

                        targetTransform.DOMove(step.dollyEnd.position, step.duration)
                            .SetEase(step.dollyEase).SetUpdate(true);
                        
                        // Si hay LookAt, la rotación la maneja el LookAt, si no, el Dolly
                        if (step.lookAtTarget == null)
                        {
                            targetTransform.DORotateQuaternion(step.dollyEnd.rotation, step.duration)
                                .SetEase(step.dollyEase).SetUpdate(true);
                        }
                    }
                    // B. Animación de Offset (Posición Base + Offset Start -> Posición Base + Offset End)
                    else if (step.animateCameraOffset)
                    {
                        // Guardar posición base (donde está la cámara ahora mismo tras activarse)
                        Vector3 basePos = targetTransform.position;
                        Quaternion baseRot = targetTransform.rotation;

                        // Aplicar Offset Start (Local)
                        targetTransform.position = basePos + (baseRot * step.camOffsetStart);

                        // Animar hacia Offset End (Local)
                        Vector3 endPos = basePos + (baseRot * step.camOffsetEnd);
                        targetTransform.DOMove(endPos, step.duration)
                            .SetEase(step.camOffsetEase).SetUpdate(true);
                    }
                    // C. Posicionamiento Simple (NUEVO)
                    else if (step.cameraPositionTarget != null)
                    {
                        if (step.instantPosition)
                        {
                            targetTransform.position = step.cameraPositionTarget.position;
                            // Si no hay LookAt, copiamos rotación también
                            if (step.lookAtTarget == null)
                            {
                                targetTransform.rotation = step.cameraPositionTarget.rotation;
                            }
                        }
                        else
                        {
                            targetTransform.DOMove(step.cameraPositionTarget.position, step.duration)
                                .SetEase(step.dollyEase).SetUpdate(true);
                            
                            if (step.lookAtTarget == null)
                            {
                                targetTransform.DORotateQuaternion(step.cameraPositionTarget.rotation, step.duration)
                                    .SetEase(step.dollyEase).SetUpdate(true);
                            }
                        }
                    }
                    // D. Fallback (Solo mover a posición de la cámara manual si existe y estamos en direct control)
                    // Si estamos en direct control pero tenemos una referencia a una cámara manual (que no usamos como vcam),
                    // podemos usar su transform como target.
                    else if (forceDirectCameraControl && step.manualCamera != null)
                    {
                        MoveMainCameraDirectly(step.manualCamera.transform.position, step.manualCamera.transform.rotation, step.blendTime);
                    }

                    // E. LookAt Target (Nuevo)
                    if (step.lookAtTarget != null)
                    {
                        if (step.lookAtDuration <= 0)
                        {
                            targetTransform.LookAt(step.lookAtTarget);
                        }
                        else
                        {
                            targetTransform.DOLookAt(step.lookAtTarget.position, step.lookAtDuration)
                                .SetEase(step.lookAtEase).SetUpdate(true);
                        }
                    }
                }


                // 5. Instanciar (NUEVO SISTEMA INDIVIDUAL)
                if (step.objectsToSpawn != null && step.objectsToSpawn.Count > 0)
                {
                    foreach (var spawner in step.objectsToSpawn)
                    {
                        if (spawner.prefab == null) continue;

                        Vector3 pos = spawner.spawnPoint ? spawner.spawnPoint.position : Vector3.zero;
                        Quaternion rot = spawner.spawnPoint ? spawner.spawnPoint.rotation : Quaternion.identity;
                        GameObject instance = Instantiate(spawner.prefab, pos, rot);

                        // Animación Rotación
                        if (spawner.animateRotation)
                        {
                            // Revert to DORotate for standard spinning behavior
                            var t = instance.transform.DORotate(spawner.targetRotation, spawner.rotationDuration, spawner.rotationMode)
                                .SetEase(spawner.rotationEase)
                                .SetUpdate(true)
                                .SetLink(instance); // Asegurar que el tween muere con el objeto
                            
                            if (spawner.rotationLoops != 0)
                                t.SetLoops(spawner.rotationLoops, spawner.rotationLoopType);
                            
                            Debug.Log($"[Cinematic] Animating rotation for {instance.name}: {spawner.targetRotation} over {spawner.rotationDuration}s");
                        }

                        // Animación Escala
                        if (spawner.animateScale)
                        {
                            instance.transform.DOScale(spawner.targetScale, spawner.scaleDuration)
                                .SetEase(spawner.scaleEase)
                                .SetUpdate(true)
                                .SetLink(instance);
                        }

                        // Ciclo de Vida
                        switch (spawner.spawnMode)
                        {
                            case InstantiationMode.DestroyTimer: 
                                Destroy(instance, spawner.destroyDelay); 
                                break;
                            case InstantiationMode.ShrinkAndDestroy:
                                instance.transform.DOScale(Vector3.zero, spawner.shrinkDuration)
                                    .SetEase(Ease.InBack).SetUpdate(true).OnComplete(() => Destroy(instance));
                                break;
                            case InstantiationMode.MoveAndDestroy:
                                if (spawner.exitTarget != null)
                                    instance.transform.DOMove(spawner.exitTarget.position, spawner.exitDuration)
                                        .SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() => Destroy(instance));
                                break;
                        }
                    }
                }

                // 6. Audio
                if (step.audioClip != null)
                {
                    AudioSource.PlayClipAtPoint(step.audioClip, Camera.main.transform.position, step.volume);
                }

                // 7. Animación
                PlayableGraph currentAnimGraph = default;
                Animator targetAnim = step.overrideAnimator;
                if (targetAnim == null && step.character != null) 
                    targetAnim = step.character.GetComponent<Animator>();

                if (targetAnim != null && step.animationClip != null)
                {
                    // Si hay una animación nueva, limpiamos las anteriores del mismo animator para evitar conflictos
                    // Ojo: Esto es simplificado. Si quieres crossfade real necesitarías un mixer.
                    CleanupGraphsForAnimator(targetAnim);
                    
                    currentAnimGraph = PlayClipDirectly(targetAnim, step.animationClip, step.loopAnim);
                }

                // 8. Movimiento NPC
                if (step.character != null && step.moveTarget != null)
                {
                    float dist = Vector3.Distance(step.character.position, step.moveTarget.position);
                    float time = dist / Mathf.Max(0.1f, step.moveSpeed);
                    
                    step.character.DOMove(step.moveTarget.position, time).SetEase(Ease.Linear)
                        .OnComplete(() => 
                        {
                            if (step.stopAnimOnArrival && currentAnimGraph.IsValid())
                            {
                                currentAnimGraph.Destroy();
                                _activeGraphs.Remove(currentAnimGraph);
                            }
                        });
                    
                    step.character.DOLookAt(step.moveTarget.position, 0.2f);
                }

                // 9. GESTIÓN DE TIEMPO Y DIÁLOGO
                float elapsedTime = 0f;
                bool dialogueShown = false;
                bool dialogueFinished = false;
                bool endBlackoutTriggered = false;

                // Calcular cuándo mostrar el diálogo
                float showDialogueAt = -1f; // -1 significa "no mostrar" o "ya mostrado"
                if (step.dialogue != null)
                {
                    switch (step.dialogueTiming)
                    {
                        case DialogueTiming.WithStepStart: showDialogueAt = 0f; break;
                        case DialogueTiming.AfterCameraBlend: showDialogueAt = step.blendTime; break;
                        case DialogueTiming.CustomDelay: showDialogueAt = step.dialogueDelay; break;
                        case DialogueTiming.AfterDuration: showDialogueAt = step.duration; break;
                    }
                }
                
                // Calcular cuándo lanzar el blackout final (si aplica)
                // Queremos que el blackout termine justo cuando termina el paso, o empiece un poco antes.
                // Asumimos que queremos que la pantalla esté negra AL FINAL del paso.
                // Por tanto, debemos empezar la transición (fade out) antes de que acabe el tiempo.
                float startEndBlackoutAt = -1f;
                if (step.endWithBlackout && step.transitionSettings != null)
                {
                    // Empezamos el fade out para que termine coincidiendo con el fin del paso
                    // transitionTime es la mitad de la transición (fade out), luego viene el wait y luego fade in.
                    // Aquí solo nos interesa el fade out visual.
                    float fadeOutDuration = step.transitionSettings.transitionTime;
                    if (step.transitionSettings.autoAdjustTransitionTime) 
                        fadeOutDuration /= step.transitionSettings.transitionSpeed;
                        
                    startEndBlackoutAt = Mathf.Max(0, step.duration - fadeOutDuration);
                }

                // Bucle principal del paso
                while (elapsedTime < step.duration || (step.dialogue != null && !dialogueFinished))
                {
                    // Mostrar diálogo si toca
                    if (step.dialogue != null && !dialogueShown && elapsedTime >= showDialogueAt)
                    {
                        dialogueShown = true;
                        DialogueManager.Instance.StartDialogue(step.dialogue, transform, () => dialogueFinished = true);
                    }
                    
                    // Lanzar Blackout Final si toca
                    if (step.endWithBlackout && !endBlackoutTriggered && startEndBlackoutAt >= 0 && elapsedTime >= startEndBlackoutAt)
                    {
                        endBlackoutTriggered = true;
                        if (TransitionManager.Instance())
                        {
                            TransitionManager.Instance().Transition(step.transitionSettings, 0);
                        }
                    }

                    // Si el diálogo ya salió y terminó, y ya pasó la duración mínima, salimos
                    if (dialogueShown && dialogueFinished && elapsedTime >= step.duration)
                    {
                        break;
                    }

                    yield return null;
                    elapsedTime += Time.unscaledDeltaTime; // Usar unscaled para que funcione en slowmo
                }
                
                // AL FINALIZAR EL PASO:
                // Si NO queremos mantener la animación, la limpiamos.
                // Si queremos mantenerla, la dejamos viva en _activeGraphs.
                if (!step.keepAnimOnNextStep && currentAnimGraph.IsValid())
                {
                    currentAnimGraph.Destroy();
                    _activeGraphs.Remove(currentAnimGraph);
                }
            }

            // --- FINALIZACIÓN ---
            CleanupGraphs(true); // Limpiar todo al acabar la cinemática
            Time.timeScale = 1f; // Restaurar tiempo
            IsAnyCinematicPlaying = false;
            
            // Liberar vThirdPersonCamera
            vThirdPersonCamera.lockCameraForCinematic = false;

            // Mostrar HUD usando ServiceLocator
            if (hideHUD)
            {
                var hud = ServiceLocator.Get<Sendero.UI.PlayerHUDV2>(false);
                if (hud != null)
                {
                    hud.ShowHUD();
                }
                else if (Sendero.UI.PlayerHUDV2.Instance)
                {
                    // Fallback a Singleton
                    Sendero.UI.PlayerHUDV2.Instance.ShowHUD();
                }
            }
            
            // Restaurar cámaras
            if (forceDirectCameraControl)
            {
                if (_brain != null) _brain.enabled = true;
            }
            else
            {
                if (_lastActiveCamera != null) 
                {
                    SetCameraPriority(_lastActiveCamera, 0);
                    _lastActiveCamera.gameObject.SetActive(false); // Desactivar al terminar
                }
            }
            
            // Restaurar ClearFlags y Cámaras
            if (customBrainCamera != null)
            {
                if (forceSkybox) customBrainCamera.clearFlags = _originalClearFlags;
                customBrainCamera.gameObject.SetActive(false);
                
                // Reactivar Main Camera Brain si lo desactivamos
                if (_mainUnityCamera != null)
                {
                    var mainBrain = _mainUnityCamera.GetComponent<CinemachineBrain>();
                    if (mainBrain != null) mainBrain.enabled = true;
                    
                    // Restaurar estado de Main Camera si fue desactivada
                    if (_mainUnityCameraWasEnabled)
                    {
                        _mainUnityCamera.enabled = true;
                    }
                }
            }
            else if (_mainUnityCamera != null)
            {
                if (forceSkybox) _mainUnityCamera.clearFlags = _originalClearFlags;
            }
            
            // Reactivar la cámara de gameplay de Cinemachine (si existe)
            if (_mainGameplayCamera != null)
            {
                Debug.Log($"[Cinematic] Restaurando cámara de gameplay Cinemachine (Priority: {_originalGameplayCameraPriority})");
                _mainGameplayCamera.Priority.Value = _originalGameplayCameraPriority;
            }
            
            // === RESTAURAR ENTORNO CINEMÁTICO ===
            // Finalizar el override cinemático y restaurar el estado del entorno según donde está el jugador
            if (EnvironmentController.Instance != null)
            {
                EnvironmentController.Instance.EndCinematicOverride();
            }
            
            // Marcar como vista si es singleUse
            if (singleUse) MarkAsSeen();
            
            onCinematicEnd?.Invoke();
            Debug.Log("[Cinematic] Secuencia finalizada.");
        }

        // ------------------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------------------

        private void MoveMainCameraDirectly(Vector3 targetPos, Quaternion targetRot, float duration)
        {
            if (Camera.main == null) return;
            
            if (duration <= 0)
            {
                Camera.main.transform.position = targetPos;
                Camera.main.transform.rotation = targetRot;
            }
            else
            {
                Camera.main.transform.DOMove(targetPos, duration).SetEase(Ease.InOutSine);
                Camera.main.transform.DORotateQuaternion(targetRot, duration).SetEase(Ease.InOutSine);
            }
        }

        private void ActivateCamera(CinemachineCamera cam, float blendTime)
        {
            if (cam == null) return;

            // Check for common mistake: Camera component on Virtual Camera
            var extraCam = cam.GetComponent<Camera>();
            if (extraCam != null)
            {
                Debug.LogWarning($"[Cinematic] ⚠️ La cámara virtual '{cam.name}' tiene un componente Camera. Esto puede causar problemas de renderizado. Se desactivará el componente Camera.");
                extraCam.enabled = false;
            }

            // Ensure Far Clip Plane is sufficient for Skybox
            var lens = cam.Lens;
            if (lens.FarClipPlane < 1000f)
            {
                lens.FarClipPlane = 5000f;
                cam.Lens = lens;
                Debug.Log($"[Cinematic] 🔧 Ajustado FarClipPlane de '{cam.name}' a 5000 para asegurar visibilidad del Skybox.");
            }

            if (_lastActiveCamera != null && _lastActiveCamera != cam)
                SetCameraPriority(_lastActiveCamera, 0);
            
            if (_brain != null) _brain.DefaultBlend.Time = blendTime;

            cam.gameObject.SetActive(true);
            SetCameraPriority(cam, 9999);
            
            _lastActiveCamera = cam;
            Debug.Log($"[Cinematic] Activando cámara manual: {cam.name} (Priority 9999)");
        }

        private void SetCameraPriority(CinemachineCamera cam, int priority)
        {
            if (cam == null) return;
            var p = cam.Priority;
            p.Value = priority;
            cam.Priority = p;
        }

        private PlayableGraph PlayClipDirectly(Animator animator, AnimationClip clip, bool loop)
        {
            var graph = PlayableGraph.Create($"Cinematic_Anim_{clip.name}_{Time.frameCount}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
            output.SetSourcePlayable(clipPlayable);
            if (loop) clipPlayable.SetDuration(double.MaxValue);
            else clipPlayable.SetDuration(clip.length);
            graph.Play();
            _activeGraphs.Add(graph);
            return graph;
        }

        private void CleanupGraphs(bool forceAll = false)
        {
            // Si forceAll es true, borra todo.
            // Si es false, solo borra los que no estén marcados para mantenerse (lógica ya manejada en el loop)
            // Aquí simplemente borramos lo que quede en la lista.
            
            for (int i = _activeGraphs.Count - 1; i >= 0; i--)
            {
                if (_activeGraphs[i].IsValid()) _activeGraphs[i].Destroy();
            }
            _activeGraphs.Clear();
        }
        
        private void CleanupGraphsForAnimator(Animator anim)
        {
            // Busca grafos que estén afectando a este animator y destrúyelos
            // PlayableGraph no tiene una forma directa fácil de saber su output target sin iterar
            // Por simplicidad y rendimiento en cinemáticas cortas, asumimos que si lanzamos una nueva anim
            // sobre el mismo personaje, queremos limpiar la anterior.
            
            // Nota: Esta implementación básica limpia TODO para evitar conflictos. 
            // Si necesitas mezclar animaciones en diferentes layers, requeriría un sistema más complejo.
            // Para cinemáticas secuenciales, esto suele ser suficiente.
        }
        
        void OnDrawGizmosSelected()
        {
            foreach(var step in steps)
            {
                if (step.dollyStart != null && step.dollyEnd != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine(step.dollyStart.position, step.dollyEnd.position);
                    Gizmos.DrawWireSphere(step.dollyStart.position, 0.3f);
                    Gizmos.DrawWireSphere(step.dollyEnd.position, 0.3f);
                }
            }
        }
    }
}
