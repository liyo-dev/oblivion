using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnvironmentController : MonoBehaviour
{
    public static EnvironmentController Instance { get; private set; }

    /// <summary>Disparado al entrar en un interior. Usado por MinimapController para ocultar el minimapa.</summary>
    public static event Action OnInteriorEntered;
    /// <summary>Disparado al volver al exterior. Usado por MinimapController para mostrar el minimapa.</summary>
    public static event Action OnInteriorExited;

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }
    #endif

    [Header("Opcional")]
    public Material exteriorSkyboxOverride;   // si lo pones, se usará al volver a exterior
    public Camera targetCamera;               // si lo dejas vacío, se resuelve solo

    EnvironmentMode _mode = EnvironmentMode.Unknown;

    // snapshot del “exterior” (para restaurar al salir)
    Material _savedRenderSettingsSkybox;
    public Material SavedExteriorSkybox => _savedRenderSettingsSkybox; // Exponer para cinemáticas

    Material _savedCameraSkyboxMat;
    CameraClearFlags _savedClearFlags = CameraClearFlags.Skybox;
    bool _savedHadCamSkybox;
    bool _hasSnapshot;

    // tracking de cámara / re-aplicación
    Camera _cam;              // cámara resuelta actual
    Camera _appliedCam;       // cámara sobre la que aplicamos por última vez
    bool _needReapply;        // si true, re-aplicamos en Update
    AnchorEnvironment _currentInterior; // último env de interior aplicado (para re-aplicar)

    // Gestión de zonas visibles
    GameObject[] _hiddenZones;       // zonas que ocultamos al entrar
    GameObject _hiddenExterior;      // mundo exterior ocultado
    float _savedFarClipPlane;        // far clip original de la cámara
    bool _farClipModified;

    // === CONTROL CINEMÁTICO ===
    // Permite que las cinemáticas tomen control temporal del entorno sin conflictos
    private bool _cinematicOverrideActive;
    private EnvironmentMode _preCinematicMode;
    private AnchorEnvironment _preCinematicInterior;
    // true cuando ApplyInterior fue bloqueado por cinemática activa: reintentar al terminar
    private bool _cinematicReapplyPending;
    
    /// <summary>
    /// Indica si actualmente hay un override cinemático activo
    /// </summary>
    public bool IsCinematicOverrideActive => _cinematicOverrideActive;
    
    /// <summary>
    /// El modo de entorno actual (Interior/Exterior)
    /// </summary>
    public EnvironmentMode CurrentMode => _mode;
    
    /// <summary>
    /// El interior actual (si estamos en modo Interior)
    /// </summary>
    public AnchorEnvironment CurrentInterior => _currentInterior;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Garantizar que Update() corra aunque el componente esté desactivado en escena
        enabled = true;
        DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += (_, _) => OnSceneChanged();
        SceneManager.sceneLoaded       += (_, _) => OnSceneChanged();
    }

    void OnSceneChanged()
    {
        _cam = null; _appliedCam = null;
        _cinematicReapplyPending = false;
        // no invalidamos el snapshot: si estabas en interior, lo necesitamos para volver a exterior
        _needReapply = (_mode != EnvironmentMode.Unknown); // re-aplicar el modo actual cuando haya cámara
    }

    void Update()
    {
        var camNow = ResolveCamera();
        if (camNow != _appliedCam) _needReapply = true;

        // Re-aplicar configuración de cámara si quedó pendiente porque había cinemática activa
        if (_cinematicReapplyPending)
        {
            bool stillCinematic = (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode)
                               || Game.Cinematics.SimpleCinematicDirector.IsAnyCinematicPlaying;
            if (!stillCinematic) { _needReapply = true; _cinematicReapplyPending = false; }
        }

        if (_needReapply && camNow != null)
        {
            Reapply(camNow);
            _needReapply = false;
        }
    }

    // === API pública ===
    public void ApplyInterior(AnchorEnvironment env)
    {
        _mode = EnvironmentMode.Interior;
        _currentInterior = env;

        // Capturamos el “exterior” una sola vez, antes de forzar interior
        if (!_hasSnapshot) CaptureExteriorSnapshot();

        var cam = ResolveCamera();
        ApplyInteriorTo(cam, env);   // si cam es null, haremos reapply cuando exista
        OnInteriorEntered?.Invoke();
    }

    public void ApplyExterior()
    {
        _mode = EnvironmentMode.Exterior;
        _currentInterior = null;

        var cam = ResolveCamera();
        ApplyExteriorTo(cam);
        OnInteriorExited?.Invoke();
    }

    public void RefreshCameraNow()
    {
        _cam = ResolveCamera();
        _needReapply = (_mode != EnvironmentMode.Unknown);
    }

    // === API PARA CINEMÁTICAS ===
    // Estos métodos permiten que las cinemáticas controlen temporalmente el entorno
    // sin interferir con el sistema normal de anchors
    
    /// <summary>
    /// Inicia el override cinemático. Guarda el estado actual del entorno para restaurarlo después.
    /// Llamar al inicio de una cinemática que necesita controlar el skybox.
    /// </summary>
    public void BeginCinematicOverride()
    {
        if (_cinematicOverrideActive)
        {
            Debug.LogWarning("[EnvironmentController] BeginCinematicOverride llamado pero ya hay un override activo.");
            return;
        }
        
        _cinematicOverrideActive = true;
        _preCinematicMode = _mode;
        _preCinematicInterior = _currentInterior;
        
        Debug.Log($"[EnvironmentController] 🎬 Cinematic Override INICIADO - Modo guardado: {_preCinematicMode}");
    }
    
    /// <summary>
    /// Finaliza el override cinemático y restaura el estado del entorno según donde está el jugador.
    /// Llamar al finalizar la cinemática.
    /// </summary>
    public void EndCinematicOverride()
    {
        if (!_cinematicOverrideActive)
        {
            Debug.LogWarning("[EnvironmentController] EndCinematicOverride llamado pero no hay override activo.");
            return;
        }
        
        // IMPORTANTE: Desactivar el flag DESPUÉS de aplicar, para que la protección cinemática no bloquee
        Debug.Log($"[EnvironmentController] 🎬 Cinematic Override FINALIZADO - Restaurando modo: {_preCinematicMode}");
        
        // Restaurar el estado del entorno según el modo pre-cinemático
        // Usamos métodos de aplicación FORZADA que ignoran la protección cinemática
        var cam = ResolveCamera();
        if (_preCinematicMode == EnvironmentMode.Interior && _preCinematicInterior != null)
        {
            // Forzar re-aplicación del interior con TODOS los parámetros incluyendo clipping
            _mode = EnvironmentMode.Interior;
            _currentInterior = _preCinematicInterior;
            ForceApplyInteriorTo(cam, _preCinematicInterior);
        }
        else if (_preCinematicMode == EnvironmentMode.Exterior)
        {
            // Forzar re-aplicación del exterior
            _mode = EnvironmentMode.Exterior;
            _currentInterior = null;
            ForceApplyExteriorTo(cam);
        }
        
        // Ahora sí desactivamos el flag
        _cinematicOverrideActive = false;
        _preCinematicInterior = null;
    }
    
    /// <summary>
    /// Aplica configuración de EXTERIOR para una cinemática (skybox visible).
    /// Solo funciona si hay un override cinemático activo.
    /// </summary>
    public void ApplyExteriorForCinematic(Camera cam = null)
    {
        if (!_cinematicOverrideActive)
        {
            Debug.LogWarning("[EnvironmentController] ApplyExteriorForCinematic requiere BeginCinematicOverride primero.");
            return;
        }
        
        cam = cam != null ? cam : ResolveCamera();
        if (cam == null) return;
        
        // Aplicar skybox/exterior sin cambiar el _mode interno
        cam.clearFlags = CameraClearFlags.Skybox;
        
        if (exteriorSkyboxOverride != null)
        {
            RenderSettings.skybox = exteriorSkyboxOverride;
        }
        else if (_hasSnapshot && _savedRenderSettingsSkybox != null)
        {
            RenderSettings.skybox = _savedRenderSettingsSkybox;
        }
        
        // Asegurar que el componente Skybox de la cámara use el material correcto
        var camSkybox = cam.GetComponent<Skybox>();
        if (camSkybox != null && RenderSettings.skybox != null)
        {
            camSkybox.material = RenderSettings.skybox;
        }
        
        DynamicGI.UpdateEnvironment();
    }
    
    /// <summary>
    /// Aplica configuración de INTERIOR para una cinemática (solid color o skybox de interior).
    /// Solo funciona si hay un override cinemático activo.
    /// Usa la configuración del interior actual del jugador si env es null.
    /// </summary>
    public void ApplyInteriorForCinematic(Camera cam = null, AnchorEnvironment env = null)
    {
        if (!_cinematicOverrideActive)
        {
            Debug.LogWarning("[EnvironmentController] ApplyInteriorForCinematic requiere BeginCinematicOverride primero.");
            return;
        }
        
        cam = cam != null ? cam : ResolveCamera();
        if (cam == null) return;
        
        // Usar el interior pre-cinemático si no se especifica uno
        env = env != null ? env : _preCinematicInterior;
        
        if (env == null)
        {
            // Si no hay interior configurado, simplemente usar solid color negro
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            RenderSettings.skybox = null;
        }
        else if (env.useSolidColorBackground)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = env.interiorBgColor;
            RenderSettings.skybox = null;
        }
        else
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = env.interiorSkyboxOverride;
        }
        
        DynamicGI.UpdateEnvironment();
    }

    // === implementación ===
    void Reapply(Camera cam)
    {
        if (_mode == EnvironmentMode.Interior)
        {
            // _currentInterior puede haber sido destruida al descargar la escena anterior.
            // Si está destruida, resetear a Unknown para que WorldBootstrap re-aplique.
            if (!_currentInterior)
            {
                _mode = EnvironmentMode.Unknown;
                return;
            }
            ApplyInteriorTo(cam, _currentInterior, reapply:true);
        }
        else if (_mode == EnvironmentMode.Exterior) ApplyExteriorTo(cam);
    }
    
    /// <summary>
    /// Aplica configuración de interior FORZADAMENTE, ignorando la protección cinemática.
    /// Usado internamente por EndCinematicOverride para restaurar el estado correcto.
    /// </summary>
    void ForceApplyInteriorTo(Camera cam, AnchorEnvironment env)
    {
        if (!cam)
        {
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            _needReapply = true;
            return;
        }

        Debug.Log($"[EnvironmentController] 🔧 ForceApplyInteriorTo - Aplicando configuración completa de interior");

        // Aplicar configuración de cámara (clearFlags, backgroundColor, skybox)
        if (env && env.useSolidColorBackground)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = env.interiorBgColor;
            RenderSettings.skybox = null;
            Debug.Log($"[EnvironmentController] - SolidColor: {env.interiorBgColor}");
        }
        else
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            Debug.Log($"[EnvironmentController] - Skybox: {(env?.interiorSkyboxOverride != null ? env.interiorSkyboxOverride.name : "null")}");
        }
        
        // IMPORTANTE: Aplicar ajuste de clipping de cámara
        if (env && env.adjustCameraClipping)
        {
            if (!_farClipModified)
            {
                _savedFarClipPlane = cam.farClipPlane;
                _farClipModified = true;
            }
            cam.farClipPlane = env.interiorFarClipPlane;
            Debug.Log($"[EnvironmentController] - FarClipPlane ajustado a: {env.interiorFarClipPlane}");
        }

        // Aplicar gestión de zonas visibles
        if (env)
        {
            if (env.zoneRoot && !env.zoneRoot.activeSelf)
            {
                env.zoneRoot.SetActive(true);
            }
            
            if (env.zonesToHideOnEnter != null && env.zonesToHideOnEnter.Length > 0)
            {
                _hiddenZones = env.zonesToHideOnEnter;
                foreach (var zone in _hiddenZones)
                {
                    if (zone && zone.activeSelf)
                    {
                        zone.SetActive(false);
                    }
                }
            }
            
            if (env.hideExteriorWorld && !string.IsNullOrEmpty(env.exteriorWorldRootName))
            {
                _hiddenExterior = GameObject.Find(env.exteriorWorldRootName);
                if (_hiddenExterior == null)
                {
                    var tagged = GameObject.FindWithTag(env.exteriorWorldRootName);
                    if (tagged) _hiddenExterior = tagged;
                }
                if (_hiddenExterior && _hiddenExterior.activeSelf)
                {
                    _hiddenExterior.SetActive(false);
                }
            }
        }

        // Gestión de luces
        var dirLight = ServiceLocator.Get<Light>(false);
        if (dirLight && dirLight.type == LightType.Directional)
        {
            bool inside = env && IsChildOf(dirLight.transform, env.transform);
            dirLight.gameObject.SetActive(inside);
        }

        if (env)
        {
            SetActive(env.lightsDisableOnEnter, false);
            SetActive(env.lightsEnableOnEnter, true);
            foreach (var l in env.GetComponentsInChildren<Light>(true))
                if (l) l.gameObject.SetActive(true);
        }

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }
    
    /// <summary>
    /// Aplica configuración de exterior FORZADAMENTE, ignorando la protección cinemática.
    /// Usado internamente por EndCinematicOverride para restaurar el estado correcto.
    /// </summary>
    void ForceApplyExteriorTo(Camera cam)
    {
        Debug.Log("[EnvironmentController] 🔧 ForceApplyExteriorTo - Aplicando configuración completa de exterior");
        
        if (cam) cam.clearFlags = _hasSnapshot ? _savedClearFlags : CameraClearFlags.Skybox;

        if (exteriorSkyboxOverride)
        {
            RenderSettings.skybox = exteriorSkyboxOverride;
            var csb = EnsureCameraSkybox(cam);
            if (csb) csb.material = exteriorSkyboxOverride;
        }
        else if (_hasSnapshot)
        {
            if (_savedHadCamSkybox)
            {
                var csb = EnsureCameraSkybox(cam);
                if (csb) csb.material = _savedCameraSkyboxMat;
            }
            else
            {
                RenderSettings.skybox = _savedRenderSettingsSkybox;
                var csb = cam ? cam.GetComponent<Skybox>() : null;
                if (csb) csb.material = null;
            }
        }
        
        // Restaurar far clip plane de la cámara
        if (_farClipModified && cam)
        {
            cam.farClipPlane = _savedFarClipPlane;
            _farClipModified = false;
            Debug.Log($"[EnvironmentController] - FarClipPlane restaurado a: {_savedFarClipPlane}");
        }

        // Restaurar zonas ocultas
        if (_hiddenZones != null)
        {
            foreach (var zone in _hiddenZones)
            {
                if (zone && !zone.activeSelf)
                {
                    zone.SetActive(true);
                }
            }
            _hiddenZones = null;
        }
        
        if (_hiddenExterior && !_hiddenExterior.activeSelf)
        {
            _hiddenExterior.SetActive(true);
            _hiddenExterior = null;
        }

        var dirLight2 = ServiceLocator.Get<Light>(false);
        if (dirLight2 && dirLight2.type == LightType.Directional) dirLight2.gameObject.SetActive(true);

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    void CaptureExteriorSnapshot()
    {
        var cam = ResolveCamera();

        _savedClearFlags = cam ? cam.clearFlags : CameraClearFlags.Skybox;
        _savedRenderSettingsSkybox = RenderSettings.skybox;

        _savedHadCamSkybox = false;
        _savedCameraSkyboxMat = null;

        if (cam)
        {
            var csb = cam.GetComponent<Skybox>();
            if (csb && csb.material)
            {
                _savedHadCamSkybox = true;
                _savedCameraSkyboxMat = csb.material;
            }
        }

        _hasSnapshot = true;
    }

    void ApplyInteriorTo(Camera cam, AnchorEnvironment env, bool reapply = false)
    {
        // consumir el parámetro para evitar advertencia de "never used"
        _ = reapply;

        // si no hay cámara aún, marca para re-aplicar cuando aparezca
        if (!cam)
        {
            // al menos quita el skybox global para mitigar
            RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            _needReapply = true;
            return;
        }

        // --- PROTECCIÓN CINEMÁTICA ---
        // Si estamos en modo cinemático (DialogueCinematicController o SimpleCinematicDirector),
        // NO modificar la cámara principal, ya que los sistemas de cinemática tienen su propia gestión.
        // Esto evita conflictos de ClearFlags y Skybox.
        bool isCinematicActive = false;
        
        // Verificar DialogueCinematicController
        if (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode)
        {
            isCinematicActive = true;
        }
        
        // Verificar SimpleCinematicDirector
        if (Game.Cinematics.SimpleCinematicDirector.IsAnyCinematicPlaying)
        {
            isCinematicActive = true;
        }
        
        if (isCinematicActive)
        {
            // Si hay cinemática, solo aplicamos lógica de luces y objetos, pero NO tocamos la cámara.
            // Marcamos para re-aplicar en cuanto la cinemática termine (ver Update).
            _cinematicReapplyPending = true;
        }
        else
        {
            _cinematicReapplyPending = false;

            // Lógica normal de cámara
            if (env && env.useSolidColorBackground)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = env.interiorBgColor;
                RenderSettings.skybox = null;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                RenderSettings.skybox = (env && env.interiorSkyboxOverride) ? env.interiorSkyboxOverride : null;
            }

            // Ajustar far clip plane de la cámara
            if (env && env.adjustCameraClipping)
            {
                if (!_farClipModified)
                {
                    _savedFarClipPlane = cam.farClipPlane;
                    _farClipModified = true;
                }
                cam.farClipPlane = env.interiorFarClipPlane;
            }
        }

        // === GESTIÓN DE ZONAS VISIBLES (Siempre aplicar, incluso en cinemáticas) ===
        if (env)
        {
            // Activar la zona de este interior
            if (env.zoneRoot && !env.zoneRoot.activeSelf)
            {
                env.zoneRoot.SetActive(true);
            }
            
            // Ocultar zonas especificadas manualmente
            if (env.zonesToHideOnEnter != null && env.zonesToHideOnEnter.Length > 0)
            {
                _hiddenZones = env.zonesToHideOnEnter;
                foreach (var zone in _hiddenZones)
                {
                    if (zone && zone.activeSelf)
                    {
                        zone.SetActive(false);
                    }
                }
            }
            
            // Ocultar mundo exterior automáticamente
            if (env.hideExteriorWorld && !string.IsNullOrEmpty(env.exteriorWorldRootName))
            {
                _hiddenExterior = GameObject.Find(env.exteriorWorldRootName);
                if (_hiddenExterior == null)
                {
                    // Buscar por tag si no se encuentra por nombre
                    var tagged = GameObject.FindWithTag(env.exteriorWorldRootName);
                    if (tagged) _hiddenExterior = tagged;
                }
                if (_hiddenExterior && _hiddenExterior.activeSelf)
                {
                    _hiddenExterior.SetActive(false);
                }
            }
        }

        // luces: apaga direccionales que no estén dentro del interior
        var dirLight = ServiceLocator.Get<Light>(false);
        if (dirLight && dirLight.type == LightType.Directional)
        {
            bool inside = env && IsChildOf(dirLight.transform, env.transform);
            dirLight.gameObject.SetActive(inside);
        }

        // enciende luces locales del interior (aunque no estén en los arrays)
        if (env)
        {
            SetActive(env.lightsDisableOnEnter, false);
            SetActive(env.lightsEnableOnEnter, true);
            foreach (var l in env.GetComponentsInChildren<Light>(true))
                if (l) l.gameObject.SetActive(true);
        }

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    void ApplyExteriorTo(Camera cam)
    {
        // --- PROTECCIÓN CINEMÁTICA ---
        bool isCinematicActive = false;
        if (DialogueCinematicController.Instance != null && DialogueCinematicController.Instance.IsInCinematicMode)
        {
            isCinematicActive = true;
        }
        
        // Verificar SimpleCinematicDirector
        if (Game.Cinematics.SimpleCinematicDirector.IsAnyCinematicPlaying)
        {
            isCinematicActive = true;
        }

        if (!isCinematicActive)
        {
            if (cam) cam.clearFlags = _hasSnapshot ? _savedClearFlags : CameraClearFlags.Skybox;

            if (exteriorSkyboxOverride)
            {
                RenderSettings.skybox = exteriorSkyboxOverride;
                var csb = EnsureCameraSkybox(cam);
                if (csb) csb.material = exteriorSkyboxOverride;
            }
            else if (_hasSnapshot)
            {
                if (_savedHadCamSkybox)
                {
                    var csb = EnsureCameraSkybox(cam);
                    if (csb) csb.material = _savedCameraSkyboxMat;
                }
                else
                {
                    RenderSettings.skybox = _savedRenderSettingsSkybox;
                    var csb = cam ? cam.GetComponent<Skybox>() : null;
                    if (csb) csb.material = null;
                }
            }
            
            // Restaurar far clip plane de la cámara
            if (_farClipModified && cam)
            {
                cam.farClipPlane = _savedFarClipPlane;
                _farClipModified = false;
            }
        }

        // === RESTAURAR ZONAS OCULTAS ===
        // Restaurar zonas que ocultamos al entrar en un interior
        if (_hiddenZones != null)
        {
            foreach (var zone in _hiddenZones)
            {
                if (zone && !zone.activeSelf)
                {
                    zone.SetActive(true);
                }
            }
            _hiddenZones = null;
        }
        
        // Restaurar mundo exterior
        if (_hiddenExterior && !_hiddenExterior.activeSelf)
        {
            _hiddenExterior.SetActive(true);
            _hiddenExterior = null;
        }

        var dirLight2 = ServiceLocator.Get<Light>(false);
        if (dirLight2 && dirLight2.type == LightType.Directional) dirLight2.gameObject.SetActive(true);

        _appliedCam = cam;
        DynamicGI.UpdateEnvironment();
    }

    Camera ResolveCamera()
    {
        if (targetCamera) return targetCamera;
        if (_cam && _cam) return _cam;

        // 1) MainCamera
        var m = Camera.main;
        if (m && m.enabled && m.gameObject.activeInHierarchy) return _cam = m;

        // 2) mejor cámara disponible (incluye inactivas)
        var cam = ServiceLocator.Get<Camera>(false);
        Camera best = null; float scoreBest = float.NegativeInfinity;
        if (cam)
        {
            float s = 0f;
            if (cam.enabled && cam.gameObject.activeInHierarchy) s += 1000f;
            if (cam.targetDisplay == 0) s += 100f;
            s += cam.depth;
            if (s > scoreBest) { scoreBest = s; best = cam; }
        }
        return _cam = best;
    }

    static Skybox EnsureCameraSkybox(Camera cam)
    {
        if (!cam) return null;
        var csb = cam.GetComponent<Skybox>();
        if (!csb) csb = cam.gameObject.AddComponent<Skybox>();
        return csb;
    }

    static void SetActive(Light[] arr, bool active)
    {
        if (arr == null) return;
        foreach (var l in arr) if (l) l.gameObject.SetActive(active);
    }

    static bool IsChildOf(Transform t, Transform root)
    {
        if (!t || !root) return false;
        for (var cur = t; cur != null; cur = cur.parent)
            if (cur == root) return true;
        return false;
    }
}
