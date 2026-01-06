using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EnvironmentController : MonoBehaviour
{
    public static EnvironmentController Instance { get; private set; }

    [Header("Opcional")]
    public Material exteriorSkyboxOverride;   // si lo pones, se usará al volver a exterior
    public Camera targetCamera;               // si lo dejas vacío, se resuelve solo

    EnvironmentMode _mode = EnvironmentMode.Unknown;

    // snapshot del “exterior” (para restaurar al salir)
    Material _savedRenderSettingsSkybox;
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

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += (_, _) => OnSceneChanged();
        SceneManager.sceneLoaded       += (_, _) => OnSceneChanged();
    }

    void OnSceneChanged()
    {
        _cam = null; _appliedCam = null;
        // no invalidamos el snapshot: si estabas en interior, lo necesitamos para volver a exterior
        _needReapply = (_mode != EnvironmentMode.Unknown); // re-aplicar el modo actual cuando haya cámara
    }

    void Update()
    {
        var camNow = ResolveCamera();
        if (camNow != _appliedCam) _needReapply = true;

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
    }

    public void ApplyExterior()
    {
        _mode = EnvironmentMode.Exterior;
        _currentInterior = null;

        var cam = ResolveCamera();
        ApplyExteriorTo(cam);
    }

    public void RefreshCameraNow()
    {
        _cam = ResolveCamera();
        _needReapply = (_mode != EnvironmentMode.Unknown);
    }

    // === implementación ===
    void Reapply(Camera cam)
    {
        if (_mode == EnvironmentMode.Interior) ApplyInteriorTo(cam, _currentInterior, reapply:true);
        else if (_mode == EnvironmentMode.Exterior) ApplyExteriorTo(cam);
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

        // === GESTIÓN DE ZONAS VISIBLES ===
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
            
            // Ajustar far clip plane de la cámara
            if (env.adjustCameraClipping && cam)
            {
                if (!_farClipModified)
                {
                    _savedFarClipPlane = cam.farClipPlane;
                    _farClipModified = true;
                }
                cam.farClipPlane = env.interiorFarClipPlane;
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
        
        // Restaurar far clip plane de la cámara
        if (_farClipModified && cam)
        {
            cam.farClipPlane = _savedFarClipPlane;
            _farClipModified = false;
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
